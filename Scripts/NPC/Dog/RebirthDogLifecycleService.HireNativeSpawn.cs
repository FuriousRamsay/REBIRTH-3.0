using System;
using System.IO;
using System.Linq;

public static partial class RebirthDogLifecycleService
{
    private sealed class HireNativeSpawn
    {
        internal HireNativeSpawn Previous;internal World World;internal ClientInfo Peer;internal string Owner;internal bool Returned;
    }
    [ThreadStatic]private static HireNativeSpawn EnteredHireNativeSpawn;
    internal static void EnterHireNativeSpawn(ClientInfo peer,bool run,out object state)
    {
        state=null;if(!run||!ThreadManager.IsMainThread()||peer?.InternalId==null||GameManager.Instance?.World==null)return;
        try
        {
            string root=HireRoot(),account=peer.InternalId.CombinedString;if(!Directory.Exists(root)||!HasPendingHireAccount(root,account))return;
            var scope=new HireNativeSpawn{Previous=EnteredHireNativeSpawn,World=GameManager.Instance.World,Peer=peer,Owner=account};EnteredHireNativeSpawn=scope;state=scope;
        }
        catch{}
    }
    internal static void ObserveHireNativeSpawn(ClientInfo peer,bool run,object state)
    {
        if(state is not HireNativeSpawn scope)return;
        try
        {
            if(!run||!ThreadManager.IsMainThread()||!ReferenceEquals(EnteredHireNativeSpawn,scope)||!ReferenceEquals(scope.Peer,peer)||!ReferenceEquals(scope.World,GameManager.Instance?.World))return;
            lock(HireColdGate)
            {
                var loaded=HireNativeOwners.Values.SingleOrDefault(value=>ReferenceEquals(value.Spawn,scope));
                scope.Returned=loaded?.Hydrated==true&&ReferenceEquals(scope.World.GetEntity(peer.entityId),loaded.Actor)&&
                    RebirthStablePlayerIdentity.TryResolveServerEntity(loaded.Actor,out var identity)&&identity.CanonicalId==scope.Owner;
            }
        }
        catch{}
        finally{EndHireNativeSpawn(state);}
    }
    internal static void EndHireNativeSpawn(object state)
    {if(state is HireNativeSpawn scope&&ReferenceEquals(EnteredHireNativeSpawn,scope))EnteredHireNativeSpawn=scope.Previous;}
    private static bool SameHireNativeSpawnInput(HireNativeOwnerLoad loaded,PlayerDataFile data)
    {
        return loaded.Spawn!=null&&ReferenceEquals(EnteredHireNativeSpawn,loaded.Spawn)&&ReferenceEquals(loaded.Spawn.World,loaded.World)&&loaded.Spawn.Owner==loaded.Owner&&
            data.deaths==loaded.Deaths&&data.bDead==loaded.Dead&&
            data.inventoryData.ToArray().SequenceEqual(loaded.NativeBelt)&&data.bagData.ToArray().SequenceEqual(loaded.NativeBag)&&data.buffData.ToArray().SequenceEqual(loaded.NativeBuffs);
    }
}
