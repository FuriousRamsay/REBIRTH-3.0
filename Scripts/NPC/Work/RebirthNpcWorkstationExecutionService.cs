using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable disable

public enum RebirthNpcWorkstationJobState : byte
{
    Planned = 0, Supplying = 1, Queueing = 2, Running = 3,
    Collecting = 4, Completed = 5, Suspended = 6, Failed = 7, Cancelled = 8
}

public sealed class RebirthNpcWorkstationMaterialRequirement
{
    public string ItemKey { get; set; }
    public int Quantity { get; set; }
    public bool Fuel { get; set; }
}

public sealed class RebirthNpcWorkstationJobRequest
{
    public string SessionKey { get; set; }
    public string SettlementId { get; set; }
    public Vector3i WorkstationPosition { get; set; }
    public string RecipeName { get; set; }
    public int CraftCount { get; set; }
    public int Priority { get; set; }
    public bool StartWorkstation { get; set; }
    public bool CollectOutput { get; set; }
    public bool AllowPartialSupply { get; set; }
    public IList<RebirthNpcWorkstationMaterialRequirement> Materials { get; set; }
}

public sealed class RebirthNpcWorkstationJobSnapshot
{
    public Guid JobId { get; internal set; }
    public RebirthNpcWorkstationJobState State { get; internal set; }
    public Vector3i Position { get; internal set; }
    public string RecipeName { get; internal set; }
    public int CraftCount { get; internal set; }
    public Guid SupplyPackageId { get; internal set; }
    public Guid CollectionPackageId { get; internal set; }
    public string Detail { get; internal set; }
    public long UpdatedUtcTicks { get; internal set; }
}

/// <summary>
/// Server-authoritative workstation orchestration. Vanilla remains the owner of recipe execution,
/// queue persistence, fuel consumption and tile-entity synchronization. This service coordinates
/// supply hauling, bounded native queue insertion, start/stop intent, output collection and recovery.
/// </summary>
public static class RebirthNpcWorkstationExecutionService
{
    private sealed class Job
    {
        public Guid Id;
        public RebirthNpcWorkstationJobRequest Request;
        public RebirthNpcWorkstationJobState State;
        public RebirthNpcWorkstationJobState ResumeState=RebirthNpcWorkstationJobState.Planned;
        public Guid SupplyPackageId = Guid.Empty; // Supply is currently transferred directly, without a logistics package.
        public Guid CollectionPackageId;
        public int QueueAttempts;
        public int MissingWorkstationTicks;
        public int StableIdleTicks;
        public long CreatedUtcTicks;
        public long UpdatedUtcTicks;
        public string Detail;
        public bool SupplyCommitted; public bool QueueCommitted;
        public ulong[] CollectionAssignmentIds=new ulong[0];
        public Dictionary<string,int> BaselineOutput=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<Guid, Job> Jobs = new Dictionary<Guid, Job>();
    private static readonly Queue<Guid> TerminalHistory = new Queue<Guid>();
    private const int MaxTerminalHistory = 256;
    private static long submitted, rejected, supplyPackages, queueAttempts, queued;
    private static long started, collections, completed, suspended, failed, cancelled;
    private static long missingStations, busyStations, reconciliations;

    public static bool TrySubmit(RebirthNpcWorkstationJobRequest request,
        out Guid jobId, out string detail)
    {
        jobId = Guid.Empty;
        detail = string.Empty;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.IsRemote()) return Reject("Authoritative world is unavailable.", out detail);
        if (request == null || string.IsNullOrWhiteSpace(request.SessionKey) ||
            string.IsNullOrWhiteSpace(request.SettlementId) ||
            string.IsNullOrWhiteSpace(request.RecipeName) || request.CraftCount <= 0)
            return Reject("Session, settlement, recipe and positive craft count are required.", out detail);
        if (request.CraftCount > 10000)
            return Reject("Craft count exceeds the bounded maximum of 10000.", out detail);
        TileEntityWorkstation workstation = world.GetTileEntity(request.WorkstationPosition) as TileEntityWorkstation;
        if (workstation == null) return Reject("Target position does not contain a loaded workstation.", out detail);
        Recipe recipe=CraftingManager.GetRecipe(request.RecipeName);if(recipe==null)return Reject("Recipe was not found: "+request.RecipeName,out detail);
        RebirthNpcWorkstationJobRequest normalized=Clone(request);normalized.Materials=BuildAuthoritativeMaterials(recipe,request.CraftCount,request.Materials);

        Job job = new Job
        {
            Id = Guid.NewGuid(), Request = normalized, State = RebirthNpcWorkstationJobState.Planned,
            CreatedUtcTicks = DateTime.UtcNow.Ticks, UpdatedUtcTicks = DateTime.UtcNow.Ticks,
            Detail = "Workstation job accepted.", BaselineOutput=CountItems(workstation.Output)
        };
        lock (Sync) Jobs[job.Id] = job;
        jobId = job.Id;
        Interlocked.Increment(ref submitted);
        detail = job.Detail;
        return true;
    }

    public static bool Cancel(Guid jobId, out string detail)
    {
        lock (Sync)
        {
            Job job;
            if (!Jobs.TryGetValue(jobId, out job)) { detail = "Workstation job was not found."; return false; }
            if (IsTerminal(job.State)) { detail = "Workstation job is already terminal."; return false; }
            SetTerminalLocked(job, RebirthNpcWorkstationJobState.Cancelled, "Cancelled by request.");
            Interlocked.Increment(ref cancelled);
            detail = job.Detail;
            return true;
        }
    }

    public static void Tick(int budget)
    {
        if (budget <= 0) return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.IsRemote()) return;
        Job[] snapshot;
        lock (Sync)
        {
            List<Job> active = new List<Job>();
            foreach (Job job in Jobs.Values) if (!IsTerminal(job.State)) active.Add(job);
            active.Sort((a, b) => b.Request.Priority.CompareTo(a.Request.Priority));
            if (active.Count > budget) active.RemoveRange(budget, active.Count - budget);
            snapshot = active.ToArray();
        }
        for (int i = 0; i < snapshot.Length; i++) Reconcile(world, snapshot[i]);
    }

    private static void Reconcile(World world, Job job)
    {
        Interlocked.Increment(ref reconciliations);
        TileEntityWorkstation workstation = world.GetTileEntity(job.Request.WorkstationPosition) as TileEntityWorkstation;
        if (workstation == null)
        {
            job.MissingWorkstationTicks++;
            Interlocked.Increment(ref missingStations);
            Suspend(job, job.MissingWorkstationTicks > 30
                ? "Workstation remained unavailable beyond recovery window."
                : "Workstation is unloaded or unavailable.", job.MissingWorkstationTicks > 30);
            return;
        }
        job.MissingWorkstationTicks = 0;
        if (workstation.IsUserAccessing())
        {
            Interlocked.Increment(ref busyStations);
            Suspend(job, "Workstation is currently being edited by a player.", false);
            return;
        }

        switch (job.State)
        {
            case RebirthNpcWorkstationJobState.Suspended:
                job.State=job.ResumeState;job.Detail="Resuming workstation phase "+job.ResumeState+".";job.UpdatedUtcTicks=DateTime.UtcNow.Ticks;return;

            case RebirthNpcWorkstationJobState.Planned:
                if (HasSupplyRequirements(job.Request) && !job.SupplyCommitted)
                {
                    string supplyDetail;
                    if (!RebirthNpcNativeWorkstationQueueBridge.TrySupplyFromSettlement(
                        workstation, job.Request.SettlementId, job.Request.Materials,
                        job.Request.AllowPartialSupply, out supplyDetail))
                    { Suspend(job, "Material supply deferred: " + supplyDetail, false); return; }
                    job.SupplyCommitted=true;job.State = RebirthNpcWorkstationJobState.Queueing;
                    job.Detail = supplyDetail;
                    Interlocked.Increment(ref supplyPackages);
                }
                else job.State = RebirthNpcWorkstationJobState.Queueing;
                break;

            case RebirthNpcWorkstationJobState.Supplying:
                job.State = RebirthNpcWorkstationJobState.Queueing;
                break;

            case RebirthNpcWorkstationJobState.Queueing:
                if(job.QueueCommitted){job.State=RebirthNpcWorkstationJobState.Running;break;}
                job.QueueAttempts++;
                Interlocked.Increment(ref queueAttempts);
                string queueDetail;
                if (!RebirthNpcNativeWorkstationQueueBridge.TryQueue(
                    workstation, job.Request.RecipeName, job.Request.CraftCount, out queueDetail))
                {
                    if (job.QueueAttempts >= 8) Fail(job, "Queue insertion failed after retries: " + queueDetail);
                    else Suspend(job, "Queue insertion deferred: " + queueDetail, false);
                    return;
                }
                job.QueueCommitted=true;Interlocked.Increment(ref queued);
                if (job.Request.StartWorkstation && RebirthNpcNativeWorkstationQueueBridge.TrySetBurning(workstation, true))
                    Interlocked.Increment(ref started);
                job.State = RebirthNpcWorkstationJobState.Running;
                job.Detail = "Recipe queued; vanilla workstation execution is authoritative.";
                break;

            case RebirthNpcWorkstationJobState.Running:
                if (RebirthWorkstationFuelPreservation.HasProductiveWork(workstation))
                { job.StableIdleTicks = 0; break; }
                if (++job.StableIdleTicks < 3) break;
                if (job.Request.CollectOutput)
                {
                    RebirthNpcLogisticsPackageResult collection = CreateCollectionPackage(job, workstation);
                    if (collection != null && collection.AssignmentsCreated > 0)
                    {
                        job.CollectionPackageId = collection.PackageId;job.CollectionAssignmentIds=collection.AssignmentIds??new ulong[0];
                        job.State = RebirthNpcWorkstationJobState.Collecting;
                        job.Detail = "Output collection package created.";
                        Interlocked.Increment(ref collections);
                        break;
                    }
                }
                Complete(job, "Workstation job completed.");
                break;

            case RebirthNpcWorkstationJobState.Collecting:
                bool allTerminal=true;bool anyFailed=false;
                for(int i=0;i<job.CollectionAssignmentIds.Length;i++){RebirthNpcWorkAssignment a;if(!RebirthNpcWorkAssignmentService.TryGet(job.CollectionAssignmentIds[i],out a)){allTerminal=false;continue;}if(a.Status==RebirthNpcWorkAssignmentStatus.Failed||a.Status==RebirthNpcWorkAssignmentStatus.Cancelled){anyFailed=true;break;}if(a.Status!=RebirthNpcWorkAssignmentStatus.Completed)allTerminal=false;}
                if(anyFailed){Fail(job,"Output collection failed; workstation job remains incomplete.");break;}
                if(allTerminal)Complete(job,"Workstation job and owned output collection completed.");
                break;
        }
        job.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
    }

    private static RebirthNpcLogisticsPackageResult CreateCollectionPackage(Job job, TileEntityWorkstation workstation)
    {
        RebirthNpcWorkstationJobRequest request=job.Request;List<RebirthNpcLogisticsDemand> demands = new List<RebirthNpcLogisticsDemand>();
        Dictionary<string, int> output = CountItems(workstation != null ? workstation.Output : null);
        foreach(string key in new List<string>(output.Keys)){int before;job.BaselineOutput.TryGetValue(key,out before);output[key]=Math.Max(0,output[key]-before);if(output[key]==0)output.Remove(key);}
        foreach (KeyValuePair<string, int> pair in output)
            demands.Add(new RebirthNpcLogisticsDemand
            {
                Source = new RebirthNpcLogisticsEndpointRef { Kind = RebirthNpcLogisticsEndpointKind.WorkstationOutput, Position = request.WorkstationPosition },
                Destination = new RebirthNpcLogisticsEndpointRef { Kind = RebirthNpcLogisticsEndpointKind.Settlement, SettlementId = request.SettlementId },
                ItemKey = pair.Key, Quantity = pair.Value, AllowPartial = true, Priority = request.Priority,
                TargetPosition = request.WorkstationPosition.ToVector3(), HasTargetPosition = true
            });
        if (demands.Count == 0) return null;
        return RebirthNpcSettlementLogisticsPackageService.CreateAndStart(new RebirthNpcLogisticsPackageRequest
        {
            SessionKey = request.SessionKey, Demands = demands, MaxQuantityPerTrip = 1000,
            MaxAssignments = 128, StartImmediately = true
        });
    }

    private static Dictionary<string, int> CountItems(ItemStack[] slots)
    {
        Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (slots == null) return result;
        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty() || stack.count <= 0) continue;
            ItemClass item = ItemClass.GetForId(stack.itemValue.type);
            if (item == null || string.IsNullOrWhiteSpace(item.Name)) continue;
            int current; result.TryGetValue(item.Name, out current);
            result[item.Name] = current > int.MaxValue - stack.count ? int.MaxValue : current + stack.count;
        }
        return result;
    }

    private static bool HasSupplyRequirements(RebirthNpcWorkstationJobRequest request)
    { return request.Materials != null && request.Materials.Count > 0; }

    private static IList<RebirthNpcWorkstationMaterialRequirement> BuildAuthoritativeMaterials(Recipe recipe,int craftCount,IList<RebirthNpcWorkstationMaterialRequirement> requested)
    {
        var result=new List<RebirthNpcWorkstationMaterialRequirement>();
        if(recipe!=null&&recipe.ingredients!=null)foreach(ItemStack ingredient in recipe.ingredients){if(ingredient==null||ingredient.IsEmpty()||ingredient.count<=0)continue;ItemClass item=ItemClass.GetForId(ingredient.itemValue.type);if(item==null||string.IsNullOrWhiteSpace(item.Name))continue;long q=(long)ingredient.count*Math.Max(1,craftCount);result.Add(new RebirthNpcWorkstationMaterialRequirement{ItemKey=item.Name,Quantity=(int)Math.Min(int.MaxValue,q),Fuel=false});}
        if(requested!=null)for(int i=0;i<requested.Count;i++){var m=requested[i];if(m!=null&&m.Fuel&&!string.IsNullOrWhiteSpace(m.ItemKey)&&m.Quantity>0)result.Add(new RebirthNpcWorkstationMaterialRequirement{ItemKey=m.ItemKey,Quantity=m.Quantity,Fuel=true});}
        return result;
    }

    private static RebirthNpcWorkstationJobRequest Clone(RebirthNpcWorkstationJobRequest r)
    {
        List<RebirthNpcWorkstationMaterialRequirement> materials = new List<RebirthNpcWorkstationMaterialRequirement>();
        if (r.Materials != null) for (int i = 0; i < r.Materials.Count; i++)
        { RebirthNpcWorkstationMaterialRequirement m = r.Materials[i]; if (m != null) materials.Add(new RebirthNpcWorkstationMaterialRequirement { ItemKey = m.ItemKey, Quantity = m.Quantity, Fuel = m.Fuel }); }
        return new RebirthNpcWorkstationJobRequest
        {
            SessionKey = r.SessionKey.Trim(), SettlementId = r.SettlementId.Trim(), WorkstationPosition = r.WorkstationPosition,
            RecipeName = r.RecipeName.Trim(), CraftCount = r.CraftCount, Priority = r.Priority == 0 ? 280 : r.Priority,
            StartWorkstation = r.StartWorkstation, CollectOutput = r.CollectOutput,
            AllowPartialSupply = r.AllowPartialSupply, Materials = materials
        };
    }

    private static void Suspend(Job job, string detail, bool terminal)
    {
        if(terminal)
        {
            lock(Sync)SetTerminalLocked(job,RebirthNpcWorkstationJobState.Failed,detail);
            Interlocked.Increment(ref failed);
            return;
        }
        if(job.State!=RebirthNpcWorkstationJobState.Suspended)job.ResumeState=job.State;
        job.State=RebirthNpcWorkstationJobState.Suspended;
        job.Detail=detail; job.UpdatedUtcTicks=DateTime.UtcNow.Ticks;
        Interlocked.Increment(ref suspended);
    }
    private static void Fail(Job job, string detail)
    {
        lock(Sync)SetTerminalLocked(job,RebirthNpcWorkstationJobState.Failed,detail);
        Interlocked.Increment(ref failed);
    }
    private static void Complete(Job job, string detail)
    {
        lock(Sync)SetTerminalLocked(job,RebirthNpcWorkstationJobState.Completed,detail);
        Interlocked.Increment(ref completed);
    }
    private static void SetTerminalLocked(Job job,RebirthNpcWorkstationJobState state,string detail)
    {
        if(job==null)return;
        bool wasTerminal=IsTerminal(job.State);
        job.State=state; job.Detail=detail??string.Empty; job.UpdatedUtcTicks=DateTime.UtcNow.Ticks;
        if(!wasTerminal)TerminalHistory.Enqueue(job.Id);
        TrimTerminalHistoryLocked();
    }
    private static void TrimTerminalHistoryLocked()
    {
        while(TerminalHistory.Count>MaxTerminalHistory)
        {
            Guid id=TerminalHistory.Dequeue(); Job old;
            if(Jobs.TryGetValue(id,out old)&&IsTerminal(old.State))Jobs.Remove(id);
        }
    }
    private static bool Reject(string message, out string detail) { detail = message; Interlocked.Increment(ref rejected); return false; }
    private static bool IsTerminal(RebirthNpcWorkstationJobState state) { return state == RebirthNpcWorkstationJobState.Completed || state == RebirthNpcWorkstationJobState.Failed || state == RebirthNpcWorkstationJobState.Cancelled; }

    public static RebirthNpcWorkstationJobSnapshot[] GetSnapshots()
    {
        lock (Sync)
        {
            RebirthNpcWorkstationJobSnapshot[] result = new RebirthNpcWorkstationJobSnapshot[Jobs.Count]; int i = 0;
            foreach (Job job in Jobs.Values) result[i++] = new RebirthNpcWorkstationJobSnapshot
            { JobId = job.Id, State = job.State, Position = job.Request.WorkstationPosition, RecipeName = job.Request.RecipeName,
              CraftCount = job.Request.CraftCount, SupplyPackageId = job.SupplyPackageId, CollectionPackageId = job.CollectionPackageId,
              Detail = job.Detail, UpdatedUtcTicks = job.UpdatedUtcTicks };
            return result;
        }
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) { Jobs.Clear(); TerminalHistory.Clear(); }
    }

    public static string GetReport()
    {
        RebirthNpcWorkstationJobSnapshot[] s = GetSnapshots();
        StringBuilder b = new StringBuilder("[REBIRTH NPC Workstation Execution] jobs=").Append(s.Length)
            .Append(" submitted=").Append(Interlocked.Read(ref submitted)).Append(" rejected=").Append(Interlocked.Read(ref rejected))
            .Append(" supplyPackages=").Append(Interlocked.Read(ref supplyPackages)).Append(" queueAttempts=").Append(Interlocked.Read(ref queueAttempts))
            .Append(" queued=").Append(Interlocked.Read(ref queued)).Append(" started=").Append(Interlocked.Read(ref started))
            .Append(" collections=").Append(Interlocked.Read(ref collections)).Append(" completed=").Append(Interlocked.Read(ref completed))
            .Append(" suspended=").Append(Interlocked.Read(ref suspended)).Append(" failed=").Append(Interlocked.Read(ref failed))
            .Append(" cancelled=").Append(Interlocked.Read(ref cancelled)).Append(" missingStations=").Append(Interlocked.Read(ref missingStations))
            .Append(" busyStations=").Append(Interlocked.Read(ref busyStations)).Append(" reconciliations=").Append(Interlocked.Read(ref reconciliations));
        for (int i = 0; i < s.Length; i++) b.AppendLine().Append("  job=").Append(s[i].JobId).Append(" state=").Append(s[i].State)
            .Append(" pos=").Append(s[i].Position).Append(" recipe=").Append(s[i].RecipeName).Append(" count=").Append(s[i].CraftCount)
            .Append(" supply=").Append(s[i].SupplyPackageId).Append(" collect=").Append(s[i].CollectionPackageId).Append(" detail=").Append(s[i].Detail);
        return b.ToString();
    }
}

/// <summary>Version-tolerant bridge to native workstation queue and burning members.</summary>
public static class RebirthNpcNativeWorkstationQueueBridge
{


    public static bool TrySupplyFromSettlement(TileEntityWorkstation workstation, string settlementId,
        IList<RebirthNpcWorkstationMaterialRequirement> materials, bool allowPartial, out string detail)
    {
        detail = string.Empty;
        if (workstation == null || materials == null || materials.Count == 0)
        { detail = "No supply work was required."; return true; }
        RebirthNpcSettlementInventoryEndpoint source = RebirthNpcSettlementInventoryEndpointRegistry.Ensure(settlementId);
        if (source == null) { detail = "Settlement inventory endpoint is unavailable."; return false; }
        int moved = 0;
        for (int i = 0; i < materials.Count; i++)
        {
            RebirthNpcWorkstationMaterialRequirement requirement = materials[i];
            if (requirement == null || string.IsNullOrWhiteSpace(requirement.ItemKey) || requirement.Quantity <= 0) continue;
            int requested = requirement.Quantity;
            int available = source.GetAvailableDebitQuantity(requirement.ItemKey);
            int quantity = allowPartial ? Math.Min(requested, available) : requested;
            if (quantity <= 0 || (!allowPartial && available < requested))
            { detail = "Settlement lacks required material: " + requirement.ItemKey; return false; }
            ItemValue value = ItemClass.GetItem(requirement.ItemKey, false);
            if (value.IsEmpty()) { detail = "Unknown item: " + requirement.ItemKey; return false; }
            ItemStack[] slots = requirement.Fuel ? GetFuelSlots(workstation) : workstation.Input;
            if (slots == null) { detail = requirement.Fuel ? "Workstation fuel slots are unavailable." : "Workstation input slots are unavailable."; return false; }
            ItemStack[] proposed = ItemStack.Clone((IList<ItemStack>)slots);
            if (!TryPlanCredit(proposed, value, quantity))
            { detail = "Workstation has insufficient destination capacity for " + requirement.ItemKey; return false; }
            Guid tx = Guid.NewGuid();
            IRebirthNpcExternalInventoryReservation reservation;
            string error;
            if (!source.TryReserveDebit(tx, requirement.ItemKey, quantity, out reservation, out error) || reservation == null)
            { detail = "Settlement reservation failed: " + error; return false; }
            try
            {
                if (!reservation.Commit(out error)) { detail = "Settlement commit failed: " + error; return false; }
                if (requirement.Fuel) SetFuelSlots(workstation, proposed); else SetInputSlots(workstation, proposed);
                moved += quantity;
            }
            finally { reservation.Dispose(); }
        }
        workstation.setModified();
        detail = "Supplied " + moved.ToString(CultureInfo.InvariantCulture) + " workstation items from settlement inventory.";
        return true;
    }

    private static ItemStack[] GetFuelSlots(TileEntityWorkstation workstation)
    {
        return workstation != null ? workstation.Fuel : null;
    }

    private static void SetInputSlots(TileEntityWorkstation workstation, ItemStack[] slots)
    {
        if (workstation != null)
            workstation.Input = slots;
    }

    private static void SetFuelSlots(TileEntityWorkstation workstation, ItemStack[] slots)
    {
        if (workstation != null)
            workstation.Fuel = slots;
    }

    private static bool TryPlanCredit(ItemStack[] slots, ItemValue value, int quantity)
    {
        int remaining = quantity;
        int max = ItemClass.GetForId(value.type).Stacknumber.Value;
        ItemStack probe = new ItemStack(value.Clone(), 1);
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty()) continue;
            int ignored;
            if (!stack.CanStackPartlyWith(probe, out ignored)) continue;
            int add = Math.Min(remaining, Math.Max(0, max - stack.count));
            if (add > 0) { stack.count += add; remaining -= add; }
        }
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            ItemStack stack = slots[i];
            if (stack != null && !stack.IsEmpty()) continue;
            int add = Math.Min(remaining, max);
            slots[i] = new ItemStack(value.Clone(), add); remaining -= add;
        }
        return remaining == 0;
    }

    public static bool TryQueue(TileEntityWorkstation workstation, string recipeName, int count, out string detail)
    {
        detail = string.Empty;
        if (workstation == null || string.IsNullOrWhiteSpace(recipeName) || count <= 0)
        { detail = "Invalid queue request."; return false; }
        try
        {
            Recipe recipe = ResolveRecipe(recipeName);
            if (recipe == null) { detail = "Recipe was not found: " + recipeName; return false; }
            RecipeQueueItem[] queue = workstation.Queue;
            if (queue == null || queue.Length == 0) { detail = "Workstation queue is unavailable."; return false; }
            int slot = -1;
            for (int i = 0; i < queue.Length; i++) if (queue[i] == null || queue[i].Recipe == null || queue[i].Multiplier <= 0) { slot = i; break; }
            if (slot < 0) { detail = "Workstation queue is full."; return false; }
            RecipeQueueItem entry = CreateQueueItem(recipe, count);
            if (entry == null) { detail = "Native RecipeQueueItem could not be constructed."; return false; }
            queue[slot] = entry;
            workstation.Queue = queue;
            detail = "Queued recipe in native slot " + slot.ToString(CultureInfo.InvariantCulture) + ".";
            return true;
        }
        catch (Exception ex) { detail = ex.GetType().Name + ": " + ex.Message; return false; }
    }

    public static bool TrySetBurning(TileEntityWorkstation workstation, bool value)
    {
        if (workstation == null) return false;
        try { workstation.IsBurning = value; workstation.setModified(); return true; }
        catch { return false; }
    }

    private static Recipe ResolveRecipe(string recipeName)
    {
        return CraftingManager.GetRecipe(recipeName);
    }

    private static RecipeQueueItem CreateQueueItem(Recipe recipe, int count)
    {
        if (recipe == null || count <= 0 || count > short.MaxValue)
            return null;

        return new RecipeQueueItem
        {
            Recipe = recipe,
            Multiplier = (short)count,
            CraftingTimeLeft = 0f,
            OneItemCraftTime = -1f,
            IsCrafting = false,
            StartingEntityId = -1
        };
    }

}
