using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

// NPC native queue admission. A saved staged native unit is its only payment
// source. Player UI grids, owner files and cooking tickets confer no authority.
internal static partial class RebirthNpcStationQueueOriginal
{
    private const string Element="npcStationQueueOriginal",Debit="npcStationQueueDebit";
    private static World indexedWorld;private static string indexedSave;private static bool indexed;
    private static readonly Dictionary<string,RebirthNpcStableId> stationHolds=new Dictionary<string,RebirthNpcStableId>();
    private sealed class SetterPermit
    {internal TileEntityWorkstation Station;internal string Method;internal object Value;internal bool Used,Returned,Failed;internal Func<bool> Current;}
    [ThreadStatic]private static SetterPermit setter;
    private static string PositionKey(Vector3i p)=>p.x+","+p.y+","+p.z;
    private static Vector3i Position(XElement r)=>new Vector3i((int)r.Attribute("x"),(int)r.Attribute("y"),(int)r.Attribute("z"));
    private static string Binding(XElement r)
    {
        var copy=new XElement(r);
        foreach(string name in new[]{"binding","phase","leaseProof","debitProof","queueProof","completedTicks",
            "outSlot","outFrame","terminalAfter","outputProof","pickupBag","pickupCredit","pickupProof","pickupPre","note","observedTicks","awarded","awardTries","creditSaved","attemptSession","refundTicks","wPrior","wFrom","wTo","wQueueTo","wSession","wTicks","wFromProof","wToProof"})copy.Attribute(name)?.Remove();
        return RebirthRemoteResourceDurableJournal.Digest(copy.ToString(SaveOptions.DisableFormatting));
    }
    internal static bool TryRead(RebirthNpcStableId id,Guid op,out XElement row)
    {
        row=null;
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var person))return false;
            string operation=op.ToString("N");
            row=(person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse)
                .SingleOrDefault(x=>x.Name==Element&&(string)x.Attribute("operation")==operation);
            return row!=null&&(int?)row.Attribute("version")==1&&(string)row.Attribute("npc")==id.ToString()&&
                (string)row.Attribute("operation")==op.ToString("N")&&(string)row.Attribute("binding")==Binding(row);
        }
        catch{row=null;return false;}
    }
    private static bool Save(RebirthNpcStableId id,XElement old,XElement next)
    {
        try
        {
            if((string)next.Attribute("binding")!=Binding(next))return false;
            string before=old?.ToString(SaveOptions.DisableFormatting),after=next.ToString(SaveOptions.DisableFormatting);
            if(Encoding.UTF8.GetByteCount(after)>6*1024*1024)return false;
            if(!RebirthNpcAggregatePersistenceStore.Mutate(id,person=>
            {
                var rows=person.UnknownOptionalElements??Array.Empty<string>();
                string operation=(string)next.Attribute("operation");
                var existing=rows.Select(XElement.Parse).SingleOrDefault(x=>x.Name==Element&&
                    (string)x.Attribute("operation")==operation);
                if(old==null?existing!=null:existing==null||!XNode.DeepEquals(existing,old))throw new InvalidOperationException("NPC queue original changed.");
                person.UnknownOptionalElements=old==null?rows.Concat(new[]{after}).ToArray():rows.Select(x=>x==before?after:x).ToArray();
            }))return false;
            RebirthNpcAggregatePersistenceStore.Save();
            if(!TryRead(id,Guid.ParseExact((string)next.Attribute("operation"),"N"),out var saved)||!XNode.DeepEquals(saved,next))return false;
            if(Holds(next))stationHolds[PositionKey(Position(next))]=id;
            else if(stationHolds.TryGetValue(PositionKey(Position(next)),out var held)&&held==id)stationHolds.Remove(PositionKey(Position(next)));
            return true;
        }
        catch{return false;}
    }
    private static bool Index()
    {
        var world=GameManager.Instance?.World;string save=GameIO.GetSaveGameDir();
        if(ReferenceEquals(indexedWorld,world)&&indexedSave==save&&indexed)return true;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false)return false;
        stationHolds.Clear();indexed=false;indexedWorld=world;indexedSave=save;
        try
        {
            foreach(var person in RebirthNpcAggregatePersistenceStore.SnapshotViews())
            {
                if(!person.UnknownOptionalElements.Any(x=>XElement.Parse(x).Name==Element))continue;
                var id=person.Identity.StableNpcId;
                if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var saved))return false;
                foreach(var row in (saved.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse).Where(x=>x.Name==Element))
                {
                    if(!Guid.TryParseExact((string)row.Attribute("operation"),"N",out var op)||!TryRead(id,op,out var valid)||
                        (string)valid.Attribute("world")!=world.worldState?.Guid)return false;
                    if(!Holds(valid))continue;
                    string key=PositionKey(Position(valid));if(stationHolds.ContainsKey(key))return false;stationHolds.Add(key,id);
                }
            }
            indexed=true;return true;
        }
        catch{return false;}
    }
    internal static bool BlocksStation(TileEntityWorkstation station)
    {
        if(station==null||station is RebirthCamperStationState||!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false)return false;
        if(!Index())return true;
        return stationHolds.ContainsKey(PositionKey(station.ToWorldPos()));
    }
    internal static bool HasPending(RebirthNpcStableId id)=>HasPendingExcept(id,null);
    // Any holding row of this NPC except the one with `operation` (a withdrawal/refund asking about its own hold).
    internal static bool HasPendingExcept(RebirthNpcStableId id,string operation)
    {
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var saved))return false;
            return (saved.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse)
                .Any(x=>x.Name==Element&&Holds(x)&&(operation==null||(string)x.Attribute("operation")!=operation));
        }
        catch{return true;}
    }
    internal static bool ConsumeStationSetter(TileEntityWorkstation station,MethodBase method,object[] arguments)
    {
        var p=setter;
        if(p==null||p.Used||!ReferenceEquals(p.Station,station)||method?.Name!=p.Method||p.Current?.Invoke()!=true||
            (p.Value==null?arguments?.Length!=0:arguments?.Length!=1||!ReferenceEquals(arguments[0],p.Value)))return false;
        p.Used=true;return true;
    }
    internal static void ObserveStationSetter(TileEntityWorkstation station,MethodBase method,bool ran,Exception error)
    {
        var p=setter;if(p==null||!p.Used||!ReferenceEquals(p.Station,station)||method?.Name!=p.Method)return;
        if(error!=null)p.Failed=true;
        else if(ran)p.Returned=true;
    }
    private static bool Set(TileEntityWorkstation station,string method,object value,Func<bool> current,Action action)=>Set(station,method,value,current,action,out _);
    // `used` = the one-use permit was consumed (the native body started). A false result with used==false means the setter provably did not run.
    private static bool Set(TileEntityWorkstation station,string method,object value,Func<bool> current,Action action,out bool used)
    {
        used=false;
        if(setter!=null||!current())return false;
        var p=new SetterPermit{Station=station,Method=method,Value=value,Current=current};setter=p;
        try{action();return p.Used&&p.Returned&&!p.Failed;}finally{used=p.Used;setter=null;}
    }
    private static string NativeLocks(ItemStackGrid grid)=>grid.SlotLocks==null?"none":
        new string(Enumerable.Range(0,grid.SlotLocks.Length).Select(i=>grid.SlotLocks[i]?'1':'0').ToArray());
    private static XElement NativePhysical(EntityRebirthHumanoidNPC npc)=>new XElement("physical",
        new XElement("bag",new XAttribute("locks",NativeLocks(npc.bag.ItemGrid)),npc.bag.ItemGrid.items.Select(x=>new XElement("slot",new XAttribute("frame",x==null||x.IsEmpty()?"":RebirthHumanEquipment.Frame(x))))),
        new XElement("belt",new XAttribute("locks",NativeLocks(npc.inventory.ItemGrid)),npc.inventory.ItemGrid.items.Select(x=>new XElement("slot",new XAttribute("frame",x==null||x.IsEmpty()?"":RebirthHumanEquipment.Frame(x))))),
        new XElement("equipment",new XAttribute("locks",NativeLocks(npc.equipment.ItemGrid)),npc.equipment.ItemGrid.items.Select(x=>new XElement("slot",new XAttribute("frame",x==null||x.IsEmpty()?"":RebirthHumanEquipment.Frame(x))))));
    private static bool Physical(EntityRebirthHumanoidNPC npc,XElement row,bool after)
    {
        return PhysicalCleared(npc,row,after?Units(row).Count:0);
    }
    private static XElement Receipt(XElement row)=>new XElement(Debit,new XAttribute("operation",(string)row.Attribute("operation")),
        new XAttribute("binding",(string)row.Attribute("binding")),new XAttribute("credit",(string)row.Attribute("credit")));
    internal static bool TryReadManagedDebit(RebirthNpcStableId id,XElement receipt,out Guid stack)
    {
        stack=Guid.Empty;
        if(!Guid.TryParseExact((string)receipt.Attribute("operation"),"N",out var op)||!TryRead(id,op,out var row)||(string)row.Attribute("phase")=="prepared")return false;
        int index=(int?)receipt.Attribute("unit")??0;var units=Units(row);
        if(index<0||index>=units.Count||!XNode.DeepEquals(receipt,ReceiptFor(row,index))||!Guid.TryParseExact(units[index].Credit,"N",out var credit))return false;
        // A refunded debit keeps its exact receipt but no longer manages the stack: the returned credit stack is governed by its bag binding alone.
        stack=(string)row.Attribute("phase")=="refunded"?Guid.Empty:credit;return true;
    }
    private static string DebitProof(XElement row)=>RebirthRemoteResourceDurableJournal.Digest(Receipt(row).ToString(SaveOptions.DisableFormatting)+"|"+row.Element("inventoryBefore"));
    private static bool SavedDebit(RebirthNpcStableId id,XElement row)
    {
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var person)||
                !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(id,out var saved)||saved.NativeStacks==null||
                saved.Revision!=(uint)row.Attribute("revision")+1)return false;
            var expected=new XElement(row.Element("inventoryBefore").Elements().Single());
            var units=Units(row);
            foreach(var unit in units)expected.Elements().Single(x=>(string)x.Attribute("id")==unit.Credit).Remove();
            expected.SetAttributeValue("revision",saved.Revision);
            if(!XNode.DeepEquals(expected,saved.NativeStacks.Write()))return false;
            var equipment=person.UnknownOptionalElements.Select(XElement.Parse).SingleOrDefault(x=>x.Name=="humanEquipment");
            var receipts=equipment?.Elements(Debit).Where(x=>(string)x.Attribute("operation")== (string)row.Attribute("operation")).ToArray();
            return receipts!=null&&receipts.Length==units.Count&&
                Enumerable.Range(0,units.Count).All(i=>receipts.Any(r=>XNode.DeepEquals(r,ReceiptFor(row,i))));
        }
        catch{return false;}
    }
    private static bool Current(RebirthNpcConcreteWorkContext c,XElement row,out EntityRebirthHumanoidNPC npc,out EntityPlayer owner,out TileEntityWorkstation station)
    {
        npc=null;owner=null;station=null;
        var intent=RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent;var a=c?.Assignment;
        if(a==null||!RebirthNpcRecipeQueueWork.Allows(a)||intent==null||!RebirthNpcRecipeQueueWork.Allows(intent)||
            !RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||intent.PrimaryId!=a.AssignmentId||intent.Actor is not EntityRebirthHumanoidNPC actual||
            actual.IsDead()||actual.IsPreparedRestorationPending||actual.Hand==null||actual.Hand.IsBusy||actual.GetAttackTarget()!=null||
            actual.bag?.ItemGrid==null||actual.inventory?.ItemGrid?.items==null||actual.equipment?.ItemGrid?.items==null||
            (actual.position-a.TargetPosition).sqrMagnitude>RebirthNpcWorkRuntime.WorkRadiusSquared||
            !RebirthNpcRecipeQueueWork.ResolveSet(a,out var set)||!UnitsCurrent(actual,a,row,set)||
            !BoundAssignment(a,row)||
            // Embodiment generation is pinned only before the debit: once the debit is attempted/saved a restart legitimately re-embodies
            // the same stable NPC, and the physical/station images below are the stronger identity.
            ((uint?)row.Attribute("embodiment")!=intent.Generation&&(string)row.Attribute("phase")!="debit-attempted"&&(string)row.Attribute("phase")!="queue-attempted")||
            RebirthNpcInteractionSessionService.IsInventoryAccessing(a.NpcId))return false;
        var target=set[0].Target;
        owner=RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId);
        if(!RebirthNpcRecipeStagingWork.StationCurrent(target,owner)||owner.Died!=(int)row.Attribute("deaths")||
            !RebirthOriginalActionScope.Matches(owner,(string)row.Attribute("owner"),(string)row.Attribute("creation"))||
            !string.Equals(actual.RebirthRuntimeState.OwnerId,a.ActorId,StringComparison.OrdinalIgnoreCase)||
            !RebirthHumanRecipeReading.Knows(a.NpcId,target.Recipe.GetName()))return false;
        station=actual.world.GetTileEntity(target.Station) as TileEntityWorkstation;
        if(station==null||(string)row.Attribute("incarnation")!=target.Incarnation||!Position(row).Equals(target.Station)||
            actual.world.GetBlock(target.Station).rawData!=(uint)row.Attribute("blockRaw"))return false;
        npc=actual;return true;
    }
    private static bool FullStation(TileEntityWorkstation station,XElement row,string image)=>
        RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,station.Queue,station.IsBurning,out var bytes)&&
        Convert.ToBase64String(bytes)==row.Element(image)?.Value;
    internal static bool Prepare(RebirthNpcConcreteWorkContext c)
    {
        try
        {
            var a=c.Assignment;var intent=RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent;
            if(!RebirthNpcRecipeQueueWork.Allows(a)||intent==null||!RebirthNpcRecipeQueueWork.Allows(intent)||!RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||
                intent.Actor is not EntityRebirthHumanoidNPC npc||!RebirthNpcRecipeQueueWork.ResolveSet(a,out var set)||
                !RebirthNpcRecipeQueueWork.EligibleSet(set)||!set.All(x=>RebirthNpcRecipeStagingWork.SavedStage(x.Staged,x.Source))||
                !set.All(x=>RebirthNpcWorkNativeTransfer.TryAdvance(RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId),npc,x.Source.Operation))||
                HasPending(a.NpcId)||RebirthNpcWorkNativeTransferOriginal.HasPending(a.NpcId)||
                RemoteResourceTransactions.ActionBlocksNpc(a.NpcId)||RebirthNpcMetabolismCustody.Blocks(a.NpcId)||
                (npc.position-a.TargetPosition).sqrMagnitude>RebirthNpcWorkRuntime.WorkRadiusSquared)return false;
            var staged=set[0].Staged;var target=set[0].Target;var source=set[0].Source;
            var owner=RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId);
            if(!RebirthNpcRecipeStagingWork.StationCurrent(target,owner)||!RebirthHumanRecipeReading.Knows(a.NpcId,target.Recipe.GetName()))return false;
            var station=npc.world.GetTileEntity(target.Station) as TileEntityWorkstation;
            if(station==null||BlocksStation(station)||RebirthRemoteResourcePlacedStationCustody.Blocks(station)||RebirthRemoteResourceSourceCustody.BlocksStation(station)||
                RemoteResourceTransactions.ActionBlocksNative(station)||station.Queue==null||station.Queue.Length<1||
                station.Queue.Any(x=>x==null||x.Recipe!=null||x.Multiplier!=0||x.RepairItem!=null)||
                !RebirthStationToolAvailability.HasRequirement(station.Tools,target.Recipe))return false;
            string area=station.block?.Properties.GetString("RebirthCraftingStation");if(string.IsNullOrEmpty(area))area=station.block?.GetBlockName();
            if(string.IsNullOrEmpty(area)||target.Recipe.craftingArea!=area||!RebirthRemoteResourceCraftRecipe.TryClone(target.Recipe,out var recipe))return false;
            // One native ingredient entry per item type; the count is the number of staged one-unit stacks of that type.
            recipe.ingredients.Clear();
            foreach(var group in set.Select(x=>RebirthHumanEquipment.Decode(x.Source.Frame)).GroupBy(x=>x.itemValue.type))
            {var entry=group.First().Clone();entry.count=group.Count();recipe.ingredients.Add(entry);}
            recipe.UseIngredientModifier=false;
            if(RebirthRecipeOutputs.HasOutputs(recipe)||RebirthCookingBatch.IsBatch(recipe)||RebirthCookingHeat.Managed(recipe))return false;
            var after=RebirthCookingRemoteAdmissionBackend.ReadQueue(RebirthCookingRemoteAdmissionBackend.Queue(station.Queue));
            // Management duration bonus: exactly once, only for recipes classified as cooking (the proven eligible cooking path); there is
            // the NPC's own Cooking/craft skill and trait time effect (ApplyCraftTime, the player formula) is applied first, then Management.
            float time=RebirthServiceCraftSkillService.ApplyCraftTime(a.NpcId,target.Recipe,recipe.craftingTime);
            if(RebirthServiceCraftSkillService.ClassifyRecipe(target.Recipe)=="skill.cooking")
                time=Math.Max(0.1f,RebirthCompanionLeadership.ManagementAdjustedCookingDuration(time,a.NpcId));
            int slot=after.Length-1;after[slot]=new RecipeQueueItem{Recipe=recipe,Multiplier=1,Quality=1,StartingEntityId=npc.entityId,
                CraftingTimeLeft=time,OneItemCraftTime=time,IsCrafting=true};
            RebirthRemoteResourcePlacedStationGuards.Install();
            if(!RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,station.Queue,station.IsBurning,out var beforeBody)||
                !RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,after,station.IsBurning,out var afterBody))return false;
            // The terminal pre-image must be provably empty: with no earlier output or receipt row, the first
            // output/receipt pair after this admission can only be this queued job.
            if(!RebirthStationTerminalSnapshot.TryCapture(station,out var terminal)||station.Output==null||station.Output.Any(x=>x!=null&&!x.IsEmpty())||
                station.CraftCompleteList?.Count>0||target.Recipe.itemValueType<=0)return false;
            var equipment=RebirthHumanEquipment.Read(npc);if(equipment.Element("swap")!=null||equipment.Element("bookMove")!=null)return false;
            RebirthHumanEquipment.Save(npc,equipment);
            if(!RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(a.NpcId,out var saved)||saved.NativeStacks==null||saved.Revision==uint.MaxValue||
                !saved.NativeStacks.FitsObservationBytes(1024*1024))return false;
            foreach(var unit in set)
            {
                var locks=npc.bag.ItemGrid.SlotLocks;int unitSlot=unit.Source.BagSlot;
                if(unitSlot<0||unitSlot>=npc.bag.SlotCount||RebirthHumanEquipment.Frame(npc.bag.ItemGrid[unitSlot])!=unit.Source.Frame||
                    locks==null||unitSlot>=locks.Length||locks[unitSlot])return false;
                var bound=equipment.Element("bindings")?.Elements("binding").SingleOrDefault(x=>(string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==unitSlot);
                if((string)bound?.Attribute("id")!=unit.Source.CreditId.ToString("N"))return false;
            }
            var row=new XElement(Element,new XAttribute("version",1),new XAttribute("npc",a.NpcId),new XAttribute("assignment",a.AssignmentId),
                new XAttribute("operation",c.OperationId.ToString("N")),new XAttribute("source",source.Operation.ToString("N")),
                new XAttribute("sourceAssignment",staged.AssignmentId),new XAttribute("sourceBinding",source.Binding),new XAttribute("credit",source.CreditId.ToString("N")),
                new XAttribute("sourceStartedTicks",staged.CreatedUtcTicks),new XAttribute("sourceTarget",staged.TargetKey),
                new XAttribute("world",npc.world.worldState.Guid),new XAttribute("owner",source.Owner),new XAttribute("creation",RebirthCamperCharacterAccess.CurrentCreation(owner)),
                new XAttribute("deaths",owner.Died),new XAttribute("embodiment",intent.Generation),new XAttribute("recipe",target.Recipe.GetName()),
                new XAttribute("incarnation",target.Incarnation),new XAttribute("x",target.Station.x),new XAttribute("y",target.Station.y),new XAttribute("z",target.Station.z),
                new XAttribute("blockRaw",npc.world.GetBlock(target.Station).rawData),
                new XAttribute("v2","1"),new XAttribute("outType",target.Recipe.itemValueType),new XAttribute("outCount",target.Recipe.count),new XAttribute("terminalBefore",terminal.Digest()),
                new XAttribute("bagSlot",source.BagSlot),new XAttribute("frame",source.Frame),new XAttribute("revision",saved.Revision),new XAttribute("startedTicks",a.CreatedUtcTicks),
                new XElement("inventoryBefore",saved.NativeStacks.Write()),NativePhysical(npc),new XElement("before",Convert.ToBase64String(beforeBody)),
                new XElement("after",Convert.ToBase64String(afterBody)),new XElement("queueAfter",RebirthRemoteResourcePlacedStationQueue.Encode(after)),
                set.Skip(1).Select(UnitElement));
            row.SetAttributeValue("binding",Binding(row));row.SetAttributeValue("phase","prepared");
            if(!RebirthNpcWorkOutcomeService.CanAdmitExactOutcomeIdentity(a)||!Current(c,row,out _,out _,out var final)||
                !ReferenceEquals(final,station)||!FullStation(station,row,"before"))return false;
            return Save(a.NpcId,null,row);
        }
        catch{return false;}
    }
    private static bool DebitNative(RebirthNpcConcreteWorkContext c,XElement row)
    {
        if(!Current(c,row,out var npc,out _,out var station)||!FullStation(station,row,"before"))return false;
        if(SavedDebit(c.Assignment.NpcId,row))return true;
        if(((string)row.Attribute("phase")!="prepared"&&(string)row.Attribute("phase")!="debit-attempted")||!Physical(npc,row,false)||
            !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(c.Assignment.NpcId,out var before)||before.NativeStacks==null||
            before.Revision!=(uint)row.Attribute("revision")||!XNode.DeepEquals(before.NativeStacks.Write(),row.Element("inventoryBefore").Elements().Single()))return false;
        var attempted=new XElement(row);attempted.SetAttributeValue("phase","debit-attempted");
        // A retry from an already-saved debit-attempted row is only reachable with the saved AND live pre-image intact (checked above/below).
        if(((string)row.Attribute("phase")=="prepared"&&!Save(c.Assignment.NpcId,row,attempted))||
            !Current(c,attempted,out npc,out _,out station)||!Physical(npc,attempted,false)||!FullStation(station,attempted,"before"))return false;
        var equipment=RebirthHumanEquipment.Read(npc);
        var units=Units(row);
        foreach(var unit in units)
        {
            var bound=equipment.Element("bindings")?.Elements("binding").SingleOrDefault(x=>(string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==unit.BagSlot);
            if((string)bound?.Attribute("id")!=unit.Credit)return false;
        }
        var originalNpc=npc;var originalStation=station;
        // Units are cleared in order inside this one frame and persisted by ONE save below: a crash anywhere before that save restores the
        // pre-image (nothing debited), after it every unit is debited. A permit failure after k clears leaves the original retained (never replayed).
        for(int k=0;k<units.Count;k++)
        {
            int cleared=k;
            if(!DebitSlot(npc,units[k].BagSlot,()=>Current(c,attempted,out var live,out _,out var exact)&&
                ReferenceEquals(live,originalNpc)&&ReferenceEquals(exact,originalStation)&&PhysicalCleared(live,attempted,cleared)&&FullStation(exact,attempted,"before")))
            {
                // Put back every slot already cleared (and the failing one if it was emptied) so an unsaved partial debit can never be
                // persisted by an autosave; the saved row stays debit-attempted and is retried from the intact pre-image.
                RollbackDebit(c,attempted,originalNpc,originalStation,units,k);
                return false;
            }
        }
        if(!Current(c,attempted,out npc,out _,out station)||!Physical(npc,attempted,true)||!FullStation(station,attempted,"before"))
        {
            // All N slots are cleared in memory but nothing is saved: restore them so an autosave cannot persist the loss.
            RollbackDebit(c,attempted,originalNpc,originalStation,units,units.Count-1);
            return false;
        }
        for(int k=0;k<units.Count;k++)equipment.Add(ReceiptFor(row,k));
        equipment.Element("bindings")?.Elements("binding")
            .Where(x=>(string)x.Attribute("area")=="bag"&&units.Any(u=>u.BagSlot==(int)x.Attribute("index"))).ToList().ForEach(x=>x.Remove());
        RebirthHumanEquipment.Save(npc,equipment);return SavedDebit(c.Assignment.NpcId,attempted);
    }
    private static void RollbackDebit(RebirthNpcConcreteWorkContext c,XElement attempted,EntityRebirthHumanoidNPC npc,TileEntityWorkstation station,List<Unit> units,int upTo)
    {
        try
        {
            for(int j=0;j<=upTo&&j<units.Count;j++)
            {
                var slot=npc.bag.ItemGrid[units[j].BagSlot];
                if(slot!=null&&!slot.IsEmpty())continue;
                CreditSlot(npc,units[j].BagSlot,RebirthHumanEquipment.Decode(units[j].Frame),()=>Current(c,attempted,out var live,out _,out var exact)&&
                    ReferenceEquals(live,npc)&&ReferenceEquals(exact,station)&&FullStation(exact,attempted,"before"));
            }
        }
        catch{}
    }
    private static string StampKey(XElement row)=>"rebirth.npc.queue.returned.v1:"+(string)row.Attribute("operation");
    private static byte[] Stamp(XElement row)=>Encoding.UTF8.GetBytes((string)row.Attribute("binding"));
    private static bool ApplyQueue(RebirthNpcConcreteWorkContext c,XElement row)
    {
        if((string)row.Attribute("phase")!="debit-attempted"||!SavedDebit(c.Assignment.NpcId,row)||
            !Current(c,row,out var npc,out _,out var station)||!Physical(npc,row,true)||!FullStation(station,row,"before")||
            !RebirthRemoteResourceBlockHost.Ready(npc.world,Position(row),out var chunk,out _,out var providerCurrent)||
            chunk.ChunkCustomData.dict.ContainsKey(StampKey(row))||
            !RebirthRemoteResourcePlacedStationQueue.TryDecode(row.Element("queueAfter").Value,out var queue))return false;
        var attempted=new XElement(row);attempted.SetAttributeValue("phase","queue-attempted");attempted.SetAttributeValue("debitProof",DebitProof(row));
        // Names the world instance that entered the native setter: a later session (restart/reload) knows the in-memory call is gone and can
        // classify the station by its loaded image (see RecoverUnreturned).
        attempted.SetAttributeValue("attemptSession",WorldSession(npc.world));
        if(!Save(c.Assignment.NpcId,row,attempted))return false;
        bool Common()=>providerCurrent()&&Current(c,attempted,out var live,out _,out var exact)&&ReferenceEquals(exact,station)&&Physical(live,attempted,true);
        // Each actual native setter consumes a separate one-use permission.
        if(!Set(station,"set_Queue",queue,()=>Common()&&FullStation(station,attempted,"before"),()=>station.Queue=queue))return false;
        return FinishEntered(c,attempted,station,chunk,providerCurrent);
    }
    // Tail of the queue insertion once set_Queue returned (or was proven persisted): the tick reset and the returned-call stamp.
    private static bool FinishEntered(RebirthNpcConcreteWorkContext c,XElement attempted,TileEntityWorkstation station,Chunk chunk,Func<bool> providerCurrent)
    {
        bool Common()=>providerCurrent()&&Current(c,attempted,out var live,out _,out var exact)&&ReferenceEquals(exact,station)&&Physical(live,attempted,true);
        if(!Set(station,nameof(TileEntityWorkstation.ResetTickTime),null,()=>Common()&&FullStation(station,attempted,"after")&&QueueCurrent(station,attempted),station.ResetTickTime))return false;
        if(!Common()||!FullStation(station,attempted,"after")||!QueueCurrent(station,attempted)||chunk.ChunkCustomData.dict.ContainsKey(StampKey(attempted)))return false;
        string key=StampKey(attempted);chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=Stamp(attempted)});station.setModified();chunk.isModified=true;return true;
    }
}
