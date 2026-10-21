using System;
using System.IO;
using System.Runtime.CompilerServices;

// The authoritative resource gate always consults durable receipts, including
// after a process restart. An in-memory empty request table is never evidence
// that a saved camper has no unresolved owner debit.
internal static class RebirthCamperEnergyCustody
{
    private sealed class Publication
    {internal Guid Operation;internal string Directory;internal RebirthCamperSavedVehicle.Expected Expected;}
    private static ConditionalWeakTable<EntityVehicle,Publication> publications=new ConditionalWeakTable<EntityVehicle,Publication>();
    private static World journalWorld;
    private static RebirthCamperTransferJournal currentJournal;
    private static RebirthCamperTransferPendingIndex pendingIndex;
    internal static void ClearRuntimeScope()
    {
        pendingIndex?.Dispose();pendingIndex=null;currentJournal=null;journalWorld=null;
        publications=new ConditionalWeakTable<EntityVehicle,Publication>();
    }
    internal static RebirthCamperTransferJournal Journal()
    {
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World==null||GameManager.Instance.World.IsRemote())
            throw new InvalidOperationException("Authoritative current save required.");
        string save=GameIO.GetSaveGameDir();if(string.IsNullOrWhiteSpace(save))throw new InvalidOperationException("Current save directory missing.");
        string directory=Path.GetFullPath(Path.Combine(save,"REBIRTH","CamperEnergyTransfers"));
        var world=GameManager.Instance.World;
        if(!ReferenceEquals(journalWorld,world)||currentJournal==null||
            !string.Equals(currentJournal.DirectoryPath,directory,StringComparison.OrdinalIgnoreCase))
        {
            ClearRuntimeScope();
            currentJournal=new RebirthCamperTransferJournal(directory);
            pendingIndex=new RebirthCamperTransferPendingIndex(currentJournal);journalWorld=world;
        }
        return currentJournal;
    }
    internal static RebirthCamperTransferJournal.Record FindPending(Guid vehicle)
    {Journal();return pendingIndex.FindPending(vehicle);}
    internal static bool Ready(EntityVehicle vehicle)
    {
        if(!ThreadManager.IsMainThread()||vehicle?.world==null||vehicle.world.IsRemote()||
            !ReferenceEquals(GameManager.Instance?.World,vehicle.world)||!ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)||
            !RebirthCamperRadial.IsCamper(vehicle))return false;
        try
        {
            var runtime=RebirthCamperEnergyRuntime.For(vehicle);
            var journal=Journal();var pending=pendingIndex.FindPending(runtime.Identity);
            if(pending==null)return true;
            // Rehydrate the exact original source hold before returning control
            // to the native update; retain its durable receipt for reconciliation.
            if(!runtime.Hold(pending.Operation))return false;
            var frame=RebirthCamperEnergyTransferFrame.Read(pending);
            if(frame.Survivor!=RebirthSurvivorMode.IsEnabledForCurrentWorld())return false;
            if(pending.State!=RebirthCamperTransferJournal.Phase.Uncertain)return false;
            // Uncertain is written only after exact final owner AFTER proof.
            // Recovery publishes that immutable paid change, never debits again.
            if(!runtime.AlreadyMatchesAfter(frame.Change)&&!runtime.Commit(frame.Change))return false;
            string directory=GameIO.GetSaveGameDir();
            if(!publications.TryGetValue(vehicle,out var publication))
            {
                publication=new Publication {Operation=pending.Operation,Directory=directory,Expected=RebirthCamperSavedVehicle.RequestSave(vehicle)};
                publications.Add(vehicle,publication);return false;
            }
            if(publication.Operation!=pending.Operation||publication.Directory!=directory)return false;
            if(!RebirthCamperSavedVehicle.Verify(vehicle,publication.Expected,()=>Current(vehicle,directory)&&runtime.AlreadyMatchesAfter(frame.Change)))return false;
            journal.Advance(pending.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            publications.Remove(vehicle);runtime.Release(pending.Operation);return true;
        }
        catch{return false;}
    }
    private static bool Current(EntityVehicle vehicle,string directory)=>ThreadManager.IsMainThread()&&vehicle?.world!=null&&
        ReferenceEquals(GameManager.Instance?.World,vehicle.world)&&ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)&&
        !vehicle.world.IsRemote()&&GameIO.GetSaveGameDir()==directory;
}
