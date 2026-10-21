using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

// Actual remote native owner hydration. This reads received native data only;
// it neither reads host saves nor restores a projection/financial graph.
internal static class RebirthNpcClientNativeReentry
{
    private sealed class State:IDisposable
    {
        internal PlayerDataFile Data;internal World World;internal ConnectionManager Manager;internal NetPackagePlayerId Packet;internal INetConnection Channel;
        internal EntityPlayerLocal Actor;internal byte[] Bag,Belt,Buffs;internal ItemStack[] BagSlots,BeltSlots;
        internal string Directory,Account,Creation,NativeWorld;internal bool Qualified;internal uint Epoch,Connection;internal long Next;internal int Deaths,Entity;internal bool Read,Joined,Hydrated,Failed;internal IDisposable Memory;
        public void Dispose(){Failed=true;Data=null;Packet=null;Actor=null;Bag=Belt=Buffs=null;BagSlots=BeltSlots=null;Memory?.Dispose();Memory=null;}
    }
    internal sealed class Source
    {
        private readonly State state;private Source(State state){this.state=state;}
        internal World World=>state.World;internal string Directory=>state.Directory;internal EntityPlayerLocal Actor=>state.Actor;
        internal string Account=>state.Account;internal string Creation=>state.Creation;internal int Deaths=>state.Deaths;
        internal uint Epoch=>state.Epoch;internal uint Connection=>state.Connection;
        internal bool Current()
        {
            try
            {
                var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
                bool valid=ThreadManager.IsMainThread()&&!state.Failed&&state.Read&&state.Joined&&state.Hydrated&&
                    state.World?.IsRemote()==true&&ReferenceEquals(GameManager.Instance?.World,state.World)&&manager?.IsServer==false&&
                    ReferenceEquals(manager.GetConnectionToServer()?[0],state.Channel)&&!state.Channel.IsDisconnected()&&
                    ReferenceEquals(state.Actor?.world,state.World)&&ReferenceEquals(state.World.GetEntity(state.Entity),state.Actor)&&
                    ReferenceEquals(state.World.GetPrimaryPlayer(),state.Actor)&&state.Actor.entityId==state.Entity&&state.Actor.IsSpawned()&&!state.Actor.IsDead()&&
                    state.Actor.Died==state.Deaths&&RebirthCamperCharacterAccess.CurrentCreation(state.Actor)==state.Creation&&
                    RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.CanonicalId==state.Account&&
                    RebirthNpcSaveQuarantineScope.SameDirectory(state.Directory,GameIO.GetSaveGameDir())&&Epoch!=0&&Connection!=0&&
                    RebirthNpcNetworkEpoch.ClientEpoch==Epoch&&RebirthNpcNetworkEpoch.ClientConnectionEpoch==Connection;
                if(valid){state.Qualified=true;if(state.NativeWorld==null)state.NativeWorld=state.World.worldState?.Guid;}return valid;
            }
            catch{return false;}
        }
        internal bool ScopeCurrent()
        {
            try
            {
                var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var channels=manager?.GetConnectionToServer();
                return ThreadManager.IsMainThread()&&state.Qualified&&state.Read&&state.Joined&&state.Hydrated&&state.World?.IsRemote()==true&&
                    ReferenceEquals(GameManager.Instance?.World,state.World)&&ReferenceEquals(manager,state.Manager)&&manager?.IsServer==false&&
                    channels!=null&&channels.Length>0&&ReferenceEquals(channels[0],state.Channel)&&!state.Channel.IsDisconnected()&&
                    RebirthNpcSaveQuarantineScope.SameDirectory(state.Directory,GameIO.GetSaveGameDir())&&state.NativeWorld==state.World.worldState?.Guid&&
                    RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.CanonicalId==state.Account&&
                    Epoch!=0&&Connection!=0&&RebirthNpcNetworkEpoch.ClientEpoch==Epoch&&RebirthNpcNetworkEpoch.ClientConnectionEpoch==Connection;
            }
            catch{return false;}
        }
        internal bool MatchesTrade(RebirthNpcTradeOriginal original)
        {
            try
            {
                if(!Current()||original==null||original.Account!=Account||original.Creation!=Creation||original.Deaths!=Deaths||original.World!=World.worldState?.Guid||
                    !TryHeldOperation(out var operation)||operation!=original.Operation||!RebirthNpcTradeOwnerOriginalStore.IsRetained(Actor,original)||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(state.Buffs,RebirthNpcTradeOwnerCheckpoint.Marker(operation),out float phase,out bool present)||!present||
                    phase!=1f&&phase!=2f&&phase!=3f||!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(state.Buffs,RebirthNpcTradeOwnerExchange.Step(operation),out float value,out bool hasStep))return false;
                if(!hasStep)value=0f;
                if(float.IsNaN(value)||float.IsInfinity(value)||value<0||value>378||value!=Math.Floor(value)||((int)value&1)!=0||
                    phase!=1f&&(int)value!=RebirthNpcTradePrefix.Changes(original)*2)return false;
                return RebirthNpcTradePrefix.Matches(original,state.BagSlots,state.BeltSlots,(int)value);
            }
            catch{return false;}
        }
        private bool TryHeldOperation(out Guid operation)
        {
            operation=Guid.Empty;
            return Current()&&RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(state.Buffs,RebirthNpcTradeOwnerCheckpoint.Held,out float held,out bool hasHeld)&&hasHeld&&held==1f&&
                RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(state.Buffs,RebirthNpcTradeOwnerCheckpoint.HeldLife,out float life,out bool hasLife)&&hasLife&&life==Deaths&&
                RebirthNpcTradeOwnerLoad.TryOperation(state.Buffs,out operation);
        }
        internal bool MatchesTradeObservation(Guid operation,string account,string creation,string world,int deaths)
        {
            if(!Current()||Account!=account||Creation!=creation||World.worldState?.Guid!=world||Deaths!=deaths||
                !TryHeldOperation(out var actual)||actual!=operation||!RebirthNpcTradeOwnerOriginalStore.TryReadRetained(Actor,operation,out var original))return false;
            return MatchesTrade(original);
        }
        internal static Source Capture(object value)=>value is State state?new Source(state):null;
    }
    private static readonly object gate=new object();private static State next,current;private static readonly List<State> retained=new List<State>();[ThreadStatic]private static State joining;
    internal static void Install()
    {
        var harmony=new Harmony("rebirth.npc.client-native-reentry.3.0");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(Read));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(Join));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(Hydrate));
    }
    private static bool Same(State source)=>source.Data!=null&&!source.Data.bDead&&source.Data.deaths==source.Deaths&&
        source.Data.bagData!=null&&source.Data.inventoryData!=null&&source.Data.buffData!=null&&
        source.Bag.SequenceEqual(source.Data.bagData.ToArray())&&source.Belt.SequenceEqual(source.Data.inventoryData.ToArray())&&source.Buffs.SequenceEqual(source.Data.buffData.ToArray());
    [HarmonyPatch(typeof(PlayerDataFile),nameof(PlayerDataFile.Read),new[]{typeof(PooledBinaryReader),typeof(uint),typeof(StreamModeRead)})]
    private static class Read
    {
        private static void Prefix(PlayerDataFile __instance,uint _version,StreamModeRead _eStreamMode,bool __runOriginal,out State __state)
        {
            __state=null;if(!__runOriginal||_version!=uint.MaxValue||_eStreamMode!=StreamModeRead.FromServer)return;
            if(RebirthProductiveEarnedObservationBudget.TryRetainDetached(16L*1024*1024,out var memory))__state=new State{Data=__instance,Memory=memory};
        }
        private static void Postfix(PlayerDataFile __instance,bool __runOriginal,State __state)
        {
            if(__state==null)return;
            try
            {
                if(!__runOriginal||__instance.bDead||__instance.bagData==null||__instance.inventoryData==null||__instance.buffData==null||
                    __instance.bagData.Length+__instance.inventoryData.Length>4L*1024*1024||__instance.buffData.Length>1024*1024)return;
                __state.Bag=__instance.bagData.ToArray();__state.Belt=__instance.inventoryData.ToArray();__state.Buffs=__instance.buffData.ToArray();__state.Deaths=__instance.deaths;
                __state.BagSlots=RebirthGearPlayerDataInventory.ReadSlots(__instance,true);__state.BeltSlots=RebirthGearPlayerDataInventory.ReadSlots(__instance,false);
                if(__state.BagSlots==null||__state.BeltSlots==null)return;
                __state.BagSlots=__state.BagSlots.Select(value=>value?.Clone()).ToArray();__state.BeltSlots=__state.BeltSlots.Select(value=>value?.Clone()).ToArray();
                __state.Read=true;lock(gate){next?.Dispose();next=__state;}
            }
            catch{}
            finally{if(!__state.Read)__state.Dispose();}
        }
        private static Exception Finalizer(Exception __exception,State __state){if(__exception!=null)__state?.Dispose();return __exception;}
    }
    [HarmonyPatch(typeof(NetPackagePlayerId),nameof(NetPackagePlayerId.ProcessPackage),new[]{typeof(World),typeof(GameManager)})]
    private static class Join
    {
        [HarmonyPriority(Priority.Last)]private static void Prefix(NetPackagePlayerId __instance,World _world,GameManager _callbacks,bool __runOriginal,out State __state)
        {
            __state=null;
            try
            {
            if(!__runOriginal||!ThreadManager.IsMainThread()||joining!=null||_world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,_world)||
                !ReferenceEquals(_callbacks,GameManager.Instance)||!RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(__instance,out var channel)||
                !ReferenceEquals(channel,SingletonMonoBehaviour<ConnectionManager>.Instance?.GetConnectionToServer()?[0])||channel.IsDisconnected())return;
            lock(gate)
            {
                if(next?.Read!=true||next.Failed||!ReferenceEquals(next.Data,__instance.playerDataFile)||!Same(next))return;
                __state=next;next=null;__state.Packet=__instance;__state.World=_world;__state.Manager=SingletonMonoBehaviour<ConnectionManager>.Instance;__state.Channel=channel;__state.Directory=GameIO.GetSaveGameDir();
            }
            if(!RebirthNpcLifecycle.PrepareNativeEpochBoundary()){__state.Dispose();__state=null;return;}
            joining=__state;
            }
            catch{if(ReferenceEquals(joining,__state))joining=null;__state?.Dispose();__state=null;}
        }
        private static void Postfix(bool __runOriginal,State __state)
        {
            if(__state==null)return;
            if(!__runOriginal||!ReferenceEquals(joining,__state)||!__state.Hydrated||__state.Failed){__state.Dispose();return;}
            __state.Joined=true;lock(gate)
            {
                if(current!=null){if(Ended(current))current.Dispose();else{if(retained.Count>=16){__state.Dispose();return;}retained.Add(current);}}
                current=__state;
            }
        }
        private static Exception Finalizer(Exception __exception,State __state)
        {if(ReferenceEquals(joining,__state))joining=null;if(__exception!=null)__state?.Dispose();return __exception;}
    }
    [HarmonyPatch(typeof(PlayerDataFile),nameof(PlayerDataFile.ToPlayer),new[]{typeof(EntityPlayer),typeof(StreamModeRead)})]
    private static class Hydrate
    {
        private static void Postfix(PlayerDataFile __instance,EntityPlayer _player,StreamModeRead _eStreamMode,bool __runOriginal)
        {
            var source=joining;
            try
            {
            if(!__runOriginal||source==null||source.Hydrated||_eStreamMode!=StreamModeRead.FromServer||!ReferenceEquals(source.Data,__instance)||
                _player is not EntityPlayerLocal actor||!ReferenceEquals(actor.world,source.World)||actor.entityId!=source.Packet.id||actor.Died!=source.Deaths||!Same(source)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return;
            if(!RebirthCamperInventorySnapshot.Capture(source.BagSlots).SequenceEqual(RebirthCamperInventorySnapshot.Capture(actor.bag.ItemGrid.items))||
                !RebirthCamperInventorySnapshot.Capture(source.BeltSlots).SequenceEqual(RebirthCamperInventorySnapshot.Capture(actor.inventory.ItemGrid.items)))return;
            source.Actor=actor;source.Entity=actor.entityId;source.Account=owner.CanonicalId;source.Creation=RebirthCamperCharacterAccess.CurrentCreation(actor);source.Hydrated=!string.IsNullOrWhiteSpace(source.Creation);
            }
            catch{if(source!=null)source.Failed=true;}
        }
        private static Exception Finalizer(Exception __exception){if(__exception!=null&&joining!=null)joining.Failed=true;return __exception;}
    }
    private static bool Ended(State source)
    {
        try
        {
            if(source.Channel==null||source.Manager==null)return false;
            var channels=source.Manager.GetConnectionToServer();
            return source.Channel.IsDisconnected()||channels==null||channels.Length==0||!ReferenceEquals(channels[0],source.Channel);
        }
        catch{return false;}
    }
    internal static void Update()
    {
        if(!ThreadManager.IsMainThread())return;
        State value;lock(gate){foreach(var old in retained.ToArray())if(Ended(old)){old.Dispose();retained.Remove(old);}value=current;}
        if(value!=null&&Ended(value)){value.Dispose();lock(gate)if(ReferenceEquals(current,value))current=null;return;}
        if(value==null||value.Failed||DateTime.UtcNow.Ticks<value.Next)return;value.Next=DateTime.UtcNow.AddSeconds(1).Ticks;
        if(value.Epoch==0&&ReferenceEquals(value.World,GameManager.Instance?.World)&&
            ReferenceEquals(value.Channel,SingletonMonoBehaviour<ConnectionManager>.Instance?.GetConnectionToServer()?[0]))
        {uint epoch=RebirthNpcNetworkEpoch.ClientEpoch,connection=RebirthNpcNetworkEpoch.ClientConnectionEpoch;if(epoch!=0&&connection!=0){value.Epoch=epoch;value.Connection=connection;}}
        var source=Source.Capture(value);
        if(source?.Current()==true)RebirthNpcSaveQuarantineBundle.ObserveClientNativeReentry(source);
    }
}
