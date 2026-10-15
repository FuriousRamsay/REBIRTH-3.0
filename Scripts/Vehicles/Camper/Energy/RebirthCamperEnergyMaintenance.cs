using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;

// One focused cargo-to-powered-block repair lane. No backpack/remote actor debit.
public static class RebirthCamperEnergyMaintenance
{
    public const float IntervalSeconds=60f;
    private const string PaidKey="RebirthCamperMaintenancePaid";
    private sealed class Run
    {
        internal EntityVehicle Vehicle;internal float Next;internal Queue<Vector3i> Targets=new Queue<Vector3i>();
        internal Bag Bag;internal ItemStackGrid Grid;
        internal RebirthCamperTransferJournal.Record Record;internal RebirthCamperEnergyMaintenanceFrame Before,After;
        internal TileEntityPowered Tile;internal RebirthCamperEnergyMaintenanceChunkWitness Chunk;
        internal RebirthCamperSavedVehicle.Expected Saved;internal int Stage;internal float NextQueue;internal bool RepairRequested;
        internal string LastReason;
    }
    private static readonly Dictionary<EntityVehicle,Run> runs=new Dictionary<EntityVehicle,Run>();
    private static bool installed;
    public static void Install(Harmony harmony)
    {
        if(installed)return;
        if(harmony==null||!RebirthCamperEnergyMaintenanceMarkers.Install(harmony))throw new InvalidOperationException("Exact native Powered maintenance ABI unavailable.");
        RebirthCamperEnergyMaintenanceCargoGuard.Install(harmony);
        ModEvents.GameUpdate.RegisterHandler(Update);ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);installed=true;
    }
    private static RebirthCamperTransferJournal Journal()
    {
        string directory=GameIO.GetSaveGameDir();
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World==null||GameManager.Instance.World.IsRemote()||string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Current authoritative maintenance save required.");
        return new RebirthCamperTransferJournal(Path.Combine(directory,"REBIRTH","CamperMaintenanceTransfers"));
    }
    private static bool Current(EntityVehicle v)=>ThreadManager.IsMainThread()&&v?.world!=null&&!v.world.IsRemote()&&
        ReferenceEquals(v.world,GameManager.Instance?.World)&&ReferenceEquals(v.world.GetEntity(v.entityId),v)&&RebirthCamperRadial.IsCamper(v);
    // Bootstrap calls this BEFORE any Camp timer/resource processing.
    public static bool Ready(EntityVehicle vehicle)
    {
        if(!installed||!Current(vehicle))return false;
        try
        {
            if(!runs.TryGetValue(vehicle,out var run)){run=new Run{Vehicle=vehicle,Bag=vehicle.bag,Grid=vehicle.bag.ItemGrid,Next=Time.realtimeSinceStartup+IntervalSeconds};runs.Add(vehicle,run);}
            if(run.Record==null)
            {
                var pending=Journal().FindPending(RebirthCamperEnergyRuntime.For(vehicle).Identity);
                if(pending==null)return true;
                RebirthCamperEnergyMaintenanceFrame.ValidatePair(pending,out run.Before,out run.After);run.Record=pending;
            }
            if(!RebirthCamperEnergyRuntime.For(vehicle).Hold(run.Record.Operation))return false;
            RebirthCamperEnergyMaintenanceCargoGuard.HoldCargo(vehicle);return false;
        }
        catch{return false;} // Malformed receipt never becomes ready.
    }
    public static bool IsBusy(EntityVehicle vehicle)=>runs.TryGetValue(vehicle,out var run)&&run.Record!=null;
    private static EntityPlayer Owner(EntityVehicle v,out string creation,out string key)
    {
        creation=null;key=null;var owner=v.GetOwner();if(owner==null)return null;
        foreach(var entity in v.world.Entities.list)
        {
            if(!(entity is EntityPlayer p)||p.IsDead())continue;
            var native=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(p.entityId);
            if(native==null||!owner.Equals(native.PrimaryId)&&!owner.Equals(native.NativeId))continue;
            if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()||RebirthCharacterCreationHoldService.IsHeld(p)||
                !RebirthWorldCharacterService.TryGet(p,out var r)||r==null||!r.IsComplete||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(r)||
                r.Origin?.BackgroundId!="background.maintenance_technician")return null;
            creation=RebirthCamperCharacterAccess.CurrentCreation(p);key=r.StablePlayerKey;
            return string.IsNullOrEmpty(creation)||string.IsNullOrEmpty(key)?null:p;
        }
        return null;
    }
    private static bool Eligible(EntityVehicle v,TileEntityPowered te,EntityPlayer owner)
    {
        if(te==null||!te.IsPlayerPlaced||owner==null||!ReferenceEquals(v.world.GetTileEntity(te.ToWorldPos()),te)||
            !RebirthCamperEnergyBounds.Contains(v.position,te.ToWorldPos().ToVector3(),RebirthCamperEnergyRuntime.Observe(v).Range))return false;
        var persistent=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(owner.entityId);var id=v.GetOwner();
        if(persistent==null||id==null)return false;
        if(te is TileEntityPowerSource source&&source.GetOwner()!=null)return source.GetOwner().Equals(persistent.PrimaryId)||source.GetOwner().Equals(persistent.NativeId);
        if(te is TileEntityPoweredRangedTrap trap)return trap.IsOwner(id);
        return v.world.GetLandClaimOwner(te.ToWorldPos(),persistent)==EnumLandClaimOwner.Self;
    }
    private static bool PoweredAdmission(EntityVehicle v,TileEntityPowered te,EntityPlayer owner)
    {
        if(!Eligible(v,te,owner)||!PowerManager.HasInstance)return false;
        var position=te.ToWorldPos();
        if(!PowerManager.Instance.PowerItemDictionary.TryGetValue(position,out var node)||node==null||
            !ReferenceEquals(node.TileEntity,te)||!ReferenceEquals(te.GetPowerItem(),node)||node.BlockID!=v.world.GetBlock(position).type)return false;
        // Native PowerSource overrides IsPowered as IsOn; consumers use their
        // actual received power state. Do not infer either from a UI projection.
        return node.IsPowered;
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        var world=GameManager.Instance?.World;if(!installed||world==null||world.IsRemote()||!ThreadManager.IsMainThread())return;
        foreach(var entity in world.Entities.list.ToArray())if(entity is EntityVehicle vehicle&&RebirthCamperRadial.IsCamper(vehicle))
        {
            try
            {
                Ready(vehicle);var run=runs[vehicle];
                if(run.Record!=null){Progress(run);continue;}
                var snapshot=RebirthCamperEnergyRuntime.Observe(vehicle);
                if(snapshot?.Mode!=RebirthCamperEnergyMode.Camp){run.Targets.Clear();run.Next=Time.realtimeSinceStartup+IntervalSeconds;continue;}
                var owner=Owner(vehicle,out var creation,out var key);
                if(owner==null||RebirthCamperRemoteAdmission.NativeInteractionOwner(vehicle)>=0||RebirthCamperEnergyRuntime.IsHeld(vehicle))continue;
                if(Time.realtimeSinceStartup>=run.Next)
                {
                    run.Next=Time.realtimeSinceStartup+IntervalSeconds;run.Targets.Clear();
                    if(PowerManager.HasInstance)foreach(var node in PowerManager.Instance.PowerItemDictionary.Values)
                        if(node?.TileEntity is TileEntityPowered te&&PoweredAdmission(vehicle,te,owner))run.Targets.Enqueue(node.Position);
                }
                while(run.Targets.Count>0)
                {var pos=run.Targets.Dequeue();if(Prepare(run,owner,creation,key,pos))break;}
            }
            catch(Exception ex){if(runs.TryGetValue(vehicle,out var run))Warn(run,"exception-"+ex.GetType().Name);}
        }
    }
    private static bool Prepare(Run run,EntityPlayer owner,string creation,string key,Vector3i pos)
    {
        var v=run.Vehicle;var te=v.world.GetTileEntity(pos) as TileEntityPowered;var block=v.world.GetBlock(pos);
        if(!ReferenceEquals(v.bag,run.Bag)||!ReferenceEquals(v.bag.ItemGrid,run.Grid)||!PoweredAdmission(v,te,owner)||block.isair||block.ischild||block.damage<=0||block.damage>=block.Block.MaxDamage||!block.Block.CanRepair(block)||
            block.Block.RepairItems==null||block.Block.RepairItems.Count==0||block.Block.shape.UseRepairDamageState(block))return false;
        var cargo=ItemStack.Clone(v.bag.ItemGrid.items);var after=ItemStack.Clone(cargo);var locks=RebirthCamperEnergyItems.Locks(v.bag.LockedSlots);
        // Full currently damaged health, actual native first-allocation cost formula.
        // No invented resource multiplier, XP, repair tool or item degradation effect.
        foreach(var cost in block.Block.RepairItems)
        {
            var required=ItemClass.GetItem(cost.ItemName);if(required==null||required.IsEmpty())return false;
            int needed=Utils.FastMax((int)(cost.Count*((float)block.damage/block.Block.MaxDamage)*block.Block.ResourceScale),1);
            for(int i=0;i<after.Length&&needed>0;i++)
            {
                var cell=after[i];if(locks!=null&&i<locks.Length&&locks[i]||cell.IsEmpty()||cell.itemValue.type!=required.type||cell.itemValue.HasModSlots&&cell.itemValue.HasMods())continue;
                int take=Math.Min(needed,cell.count);needed-=take;cell.count-=take;if(cell.count==0)after[i]=ItemStack.Empty.Clone();
            }
            if(needed>0)return false;
        }
        var runtime=RebirthCamperEnergyRuntime.For(v);var operation=Guid.NewGuid();var previous=RebirthCamperEnergyMaintenanceMarkers.Get(te);
        if(previous!=null)
        {
            // Never overwrite another camper's unresolved target receipt.
            var prior=Journal().Read(previous.Operation);
            if(prior==null||prior.State!=RebirthCamperTransferJournal.Phase.Applied&&prior.State!=RebirthCamperTransferJournal.Phase.Rejected)return false;
        }
        var receipt=new RebirthCamperEnergyMaintenanceReceipt {Target=previous?.Target??Guid.NewGuid(),Operation=operation,Vehicle=runtime.Identity,
            World=v.world.worldState.Guid,Owner=key,Creation=creation,Position=pos,BlockRaw=block.rawData,BeforeDamage=block.damage,AfterDamage=0};
        var applied=RebirthCamperEnergyMaintenanceReceipt.Decode(receipt.Encode());applied.Applied=true;
        var before=new RebirthCamperEnergyMaintenanceFrame{Receipt=receipt,Cargo=cargo,Locks=locks};
        var next=new RebirthCamperEnergyMaintenanceFrame{Receipt=applied,Cargo=after,Locks=locks};
        if(!runtime.Hold(operation))return false;
        try{run.Record=Journal().Prepare(operation,runtime.Identity,key+":"+creation,0,before.Encode(),next.Encode());}
        catch{runtime.Release(operation);throw;}
        run.Before=before;run.After=next;run.Tile=te;run.Stage=0;run.LastReason=null;RebirthCamperEnergyMaintenanceCargoGuard.HoldCargo(v);
        RebirthCamperEnergyMaintenanceMarkers.Set(te,receipt);return true;
    }
    private static bool Scope(Run run,out EntityPlayer owner)
    {
        var v=run.Vehicle;owner=Owner(v,out var creation,out var key);var receipt=run.Before.Receipt;
        if(!Current(v)||!ReferenceEquals(v.bag,run.Bag)||!ReferenceEquals(v.bag.ItemGrid,run.Grid)||owner==null||key!=receipt.Owner||creation!=receipt.Creation||v.world.worldState.Guid!=receipt.World||
            RebirthCamperEnergyRuntime.Observe(v)?.Mode!=RebirthCamperEnergyMode.Camp)return false;
        var te=v.world.GetTileEntity(receipt.Position) as TileEntityPowered;
        if(te==null||!Eligible(v,te,owner))return false;
        run.Tile=te;var marker=RebirthCamperEnergyMaintenanceMarkers.Get(te);var block=v.world.GetBlock(receipt.Position);
        return marker!=null&&(marker.Encode().SequenceEqual(run.Before.Receipt.Encode())||marker.Encode().SequenceEqual(run.After.Receipt.Encode()))&&
            block.rawData==receipt.BlockRaw;
    }
    private static void Progress(Run run)
    {
        if(!Scope(run,out var owner)){Warn(run,"current-owner-target-scope");return;}
        var v=run.Vehicle;var before=run.Before;var after=run.After;var operation=run.Record.Operation;
        var marker=RebirthCamperEnergyMaintenanceMarkers.Get(run.Tile);
        if(marker.Applied&&(!after.Matches(v.bag)||!PaymentMarker(v,operation,"paid")))return;
        if(run.Record.State==RebirthCamperTransferJournal.Phase.Uncertain)run.Stage=2;
        if(run.Stage==0)
        {
            if(!before.Matches(v.bag))
            {
                // A native save can have persisted partial coordinator writes.
                // Resume only exact per-slot before/after images under own marker.
                if(!PaymentMarker(v,operation,"preparing")&&!PaymentMarker(v,operation,"paid"))return;
                run.Stage=1;
            }
            else
            {
                if(!ChunkSaved(run))return;
                run.Chunk?.Dispose();run.Chunk=null;run.Stage=1;
            }
        }
        if(run.Stage==1)
        {
            if(!marker.Applied&&v.world.GetBlock(marker.Position).damage!=marker.BeforeDamage)return;
            if(!RebirthCamperEnergyMaintenanceFrame.SameLocks(before.Locks,RebirthCamperEnergyItems.Locks(v.bag.LockedSlots)))return;
            var live=v.bag.ItemGrid.items;if(live.Length!=before.Cargo.Length)return;
            for(int i=0;i<live.Length;i++)if(!RebirthCamperEnergyItems.Same(live[i],before.Cargo[i])&&!RebirthCamperEnergyItems.Same(live[i],after.Cargo[i]))return;
            if(PaymentMarker(v,operation,"paid")){if(!after.Matches(v.bag))return;}
            else
            {
                SetPayment(v,operation,"preparing");
                for(int i=0;i<live.Length;i++)if(!RebirthCamperEnergyItems.Same(live[i],after.Cargo[i]))RebirthCamperEnergyMaintenanceCargoGuard.SetSlot(v.bag,i,after.Cargo[i].Clone());
                if(!after.Matches(v.bag))return;SetPayment(v,operation,"paid");
            }
            if(run.Saved==null){run.Saved=RebirthCamperSavedVehicle.RequestSave(v);return;}
            if(!RebirthCamperSavedVehicle.Verify(v,run.Saved,()=>Scope(run,out _)&&after.Matches(v.bag)&&PaymentMarker(v,operation,"paid")))return;
            run.Record=Journal().Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);run.Stage=2;
        }
        if(run.Stage!=2||!after.Matches(v.bag)||!PaymentMarker(v,operation,"paid"))return;
        if(!marker.Applied)
        {
            var block=v.world.GetBlock(marker.Position);
            if(!run.RepairRequested)
            {
                if(block.damage!=marker.BeforeDamage)return;
                run.RepairRequested=true;
                // Native server SetBlocksRPC synchronously ChangeBlocks before
                // network publication. Keep target repair+receipt on this call.
                lock(run.Tile.GetChunk())
                {
                    block.Block.DamageBlock(v.world,marker.Position,block,-(marker.BeforeDamage-marker.AfterDamage),owner.entityId);
                    var committed=v.world.GetBlock(marker.Position);
                    if(committed.rawData==marker.BlockRaw&&committed.damage==marker.AfterDamage)
                        RebirthCamperEnergyMaintenanceMarkers.Set(run.Tile,after.Receipt);
                }
            }
            var actual=v.world.GetBlock(marker.Position);
            if(actual.rawData!=marker.BlockRaw||actual.damage!=marker.AfterDamage)return;
            RebirthCamperEnergyMaintenanceMarkers.Set(run.Tile,after.Receipt);run.Chunk?.Dispose();run.Chunk=null;
        }
        if(!ChunkSaved(run))return;
        Journal().Advance(operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
        run.Chunk?.Dispose();run.Chunk=null;run.Record=null;run.Saved=null;run.Stage=0;run.RepairRequested=false;
        RebirthCamperEnergyMaintenanceCargoGuard.ReleaseCargo(v);RebirthCamperEnergyRuntime.For(v).Release(operation);
    }
    private static bool ChunkSaved(Run run)
    {
        if(run.Chunk!=null)return run.Chunk.Saved();
        if(Time.realtimeSinceStartup<run.NextQueue)return false;run.NextQueue=Time.realtimeSinceStartup+5f;
        run.Chunk=RebirthCamperEnergyMaintenanceChunkWitness.TryQueue(run.Vehicle.world,run.Tile,()=>Scope(run,out _));return false;
    }
    private static bool PaymentMarker(EntityVehicle v,Guid operation,string phase)=>v.vehicle.GetUpdatedItemValue().TryGetMetadata(PaidKey,out string value)&&value==operation.ToString("N")+":"+phase;
    private static void Warn(Run run,string reason)
    {if(run.LastReason==reason)return;run.LastReason=reason;Log.Warning("[REBIRTH EnergyMaintenance] entity="+run.Vehicle.entityId+" stage="+run.Stage+" pending/refused="+reason);}
    private static void SetPayment(EntityVehicle v,Guid operation,string phase)
    {string value=operation.ToString("N")+":"+phase;if(v.vehicle.GetUpdatedItemValue().TryGetMetadata(PaidKey,out string prior)&&prior==value)return;v.vehicle.GetUpdatedItemValue().SetMetadata(PaidKey,value);v.SendSyncData(4);}
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data)
    {foreach(var run in runs.Values){run.Chunk?.Dispose();RebirthCamperEnergyMaintenanceCargoGuard.ReleaseCargo(run.Vehicle);}runs.Clear();}
}
