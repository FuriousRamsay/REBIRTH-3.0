using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static partial class RebirthConsumptionDurableDelivery
{
    private sealed class InventoryNativeLoad
    {
        internal EntityPlayer Actor;internal World World;internal PlayerDataFile Data;internal string Account,Save,Path;
        internal byte[] Native;internal int Deaths;internal bool Dead;internal Guid[] Operations;
        internal IDisposable Memory;
        internal long NextResume;internal int ResumeCursor;internal readonly HashSet<Guid> Reported=new HashSet<Guid>();
        internal bool OwnerCurrent()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(Actor.world,World)&&
            ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())==Save&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(Actor,out var owner)&&owner.CanonicalId==Account;
        internal bool Current()=>OwnerCurrent()&&Actor.Died==Deaths;
    }
    private static readonly List<InventoryNativeLoad> inventoryNativeLoads=new List<InventoryNativeLoad>();
    // The existing native load observer has already tied these bytes to the
    // exact opened native file, complete native Read and actual ToPlayer call.
    internal static bool ObserveInventoryServerNativeHydration(EntityPlayer actor,PlayerDataFile data,byte[] native,string account,string path)
    {
        if(!InventoryProtocolActive||!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||data==null||native==null||native.Length<6||native.Length>16*1024*1024||
            !RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path)||!RebirthRemoteResourcePlacedStationSavedReceipt.TryPlayerConsumptionKeys(data.buffData.ToArray(),out var operations))return false;
        RebirthDebug.Log("metabolism","[REBIRTH ServingCold] parsed native player serving keys="+operations.Length);
        if(operations.Length==0)return true;
        foreach(var old in inventoryNativeLoads.Where(value=>!ReferenceEquals(value.World,GameManager.Instance?.World)||ReferenceEquals(value.Actor,actor)).ToArray())
        {inventoryNativeLoads.Remove(old);old.Memory.Dispose();}
        if(inventoryNativeLoads.Count>=4||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var memory))return false;
        // Native spawn reuses data for its ToClient packet after ToPlayer.
        // Keep only the exact fields observed at the owner-file hydration.
        var snapshot=new PlayerDataFile
        {
            bagData=new MemoryStream(data.bagData.ToArray(),false),
            inventoryData=new MemoryStream(data.inventoryData.ToArray(),false),
            buffData=new MemoryStream(data.buffData.ToArray(),false),
            deaths=data.deaths,
            bDead=data.bDead
        };
        inventoryNativeLoads.Add(new InventoryNativeLoad{Actor=actor,World=actor.world,Data=snapshot,Account=account,Path=path,Save=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir()),
            Native=(byte[])native.Clone(),Deaths=snapshot.deaths,Dead=snapshot.bDead,Operations=operations,Memory=memory});
        RebirthDebug.Log("metabolism","[REBIRTH ServingCold] native player serving load registered");
        return true;
    }
    private static bool HoldsLoadedInventoryActor(EntityPlayer actor)=>actor!=null&&inventoryNativeLoads.Any(value=>ReferenceEquals(value.Actor,actor)&&value.OwnerCurrent()&&value.Operations.Length>0);
    private static void CompleteInventoryNativeLoad(Guid operation)
    {
        foreach(var load in inventoryNativeLoads.Where(value=>value.Operations.Contains(operation)).ToArray())
        {
            load.Operations=load.Operations.Where(value=>value!=operation).ToArray();
            if(load.Operations.Length==0){inventoryNativeLoads.Remove(load);load.Memory.Dispose();}
        }
    }
}
