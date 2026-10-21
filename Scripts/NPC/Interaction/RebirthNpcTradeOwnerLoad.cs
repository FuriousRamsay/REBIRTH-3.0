using System;
using System.IO;
using System.Linq;

// Own actual native Read -> authenticated PlayerId join -> ToPlayer lineage.
// This observes/restores no inventory and never issues an item-setter grant.
internal static class RebirthNpcTradeOwnerLoad
{
    internal sealed class Source
    {
        private readonly Load state;
        private Source(Load state){this.state=state;}
        internal EntityPlayerLocal Player=>state.Player;
        internal Guid Operation=>state.Operation;
        internal bool Current(EntityPlayerLocal player)=>ReferenceEquals(loaded,state)&&state.Current(player);
        internal static Source CaptureCurrent()
        {var state=loaded;return state!=null&&state.Current(state.Player)?new Source(state):null;}
        internal bool TryStage(out int step,out int phase)
        {
            step=phase=0;if(!Current(Player))return false;
            if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(state.Buffs,RebirthNpcTradeOwnerCheckpoint.Marker(Operation),out float p,out bool present)||!present||
                p!=1f&&p!=2f&&p!=3f||!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(state.Buffs,RebirthNpcTradeOwnerExchange.Step(Operation),out float s,out bool hasStep))return false;
            if(!hasStep)s=0f;
            if(float.IsNaN(s)||float.IsInfinity(s)||s<0||s>378||s!=Math.Floor(s)||((int)s&1)!=0)return false;
            step=(int)s;phase=(int)p;return true;
        }
        internal bool PersistCurrent(RebirthNpcTradeOriginal original)
        {
            if(original==null||original.Operation!=Operation||original.Account!=state.Account||original.Creation!=state.Creation||original.Deaths!=state.Deaths||
                original.World!=state.World.worldState.Guid||!TryStage(out int step,out int phase)||
                !RebirthNpcTradePrefix.Matches(original,Player.bag.ItemGrid.items,Player.inventory.ItemGrid.items,step)||
                phase!=1&&step!=RebirthNpcTradePrefix.Changes(original)*2)return false;
            if(RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&RebirthGearNativePlayerFile.TryRead(owner,out var local))
            {
                if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(local,RebirthNpcTradeOwnerCheckpoint.Marker(Operation),out float oldPhase,out bool hasPhase)||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(local,RebirthNpcTradeOwnerExchange.Step(Operation),out float oldStep,out bool hasStep))return false;
                // A contradictory positive local checkpoint is unknown; this
                // native join must not overwrite it as a fresh Before.
                if(hasPhase&&(oldPhase!=phase||(hasStep?oldStep:0f)!=step||local.deaths!=original.Deaths||
                    !RebirthNpcTradePrefix.Matches(original,RebirthGearPlayerDataInventory.ReadSlots(local,true),RebirthGearPlayerDataInventory.ReadSlots(local,false),step)))return false;
            }
            // Actual native hydration permits only republication of the same
            // owner checkpoint/current ID here; no inventory or marker setter.
            GameManager.Instance.SaveLocalPlayerData();
            return Current(Player)&&RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(Player,original,out int savedStep,out int savedPhase)&&savedStep==step&&savedPhase==phase;
        }
    }
    private sealed class Load:IDisposable
    {
        internal PlayerDataFile Data;internal byte[] Bag,Belt,Buffs,BagFrame,BeltFrame;
        internal World World;internal EntityPlayerLocal Player;internal INetConnection Connection;internal NetPackagePlayerId Packet;
        internal Guid Operation;internal int Deaths,Entity;internal string Account,Creation;
        internal bool ReadReturned,Hydrating,Hydrated,Joined,Consumed,Closed;internal IDisposable Memory;internal long Next;
        internal Load(){}
        internal bool Current(EntityPlayerLocal player)=>!Closed&&!Consumed&&Joined&&Hydrated&&ThreadManager.IsMainThread()&&
            ReferenceEquals(Player,player)&&ReferenceEquals(World,GameManager.Instance?.World)&&ReferenceEquals(player.world,World)&&World.IsRemote()&&
            ReferenceEquals(World.GetPrimaryPlayer(),player)&&ReferenceEquals(World.GetEntity(Entity),player)&&player.entityId==Entity&&player.IsSpawned()&&!player.IsDead()&&player.Died==Deaths&&
            ReferenceEquals(Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])&&!Connection.IsDisconnected()&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.CanonicalId==Account&&RebirthCamperCharacterAccess.CurrentCreation(player)==Creation&&
            BagFrame.SequenceEqual(RebirthCamperInventorySnapshot.Capture(player.bag.ItemGrid.items))&&BeltFrame.SequenceEqual(RebirthCamperInventorySnapshot.Capture(player.inventory.ItemGrid.items))&&
            SameStamp(player,Operation,Buffs);
        internal static Load Begin(PlayerDataFile data)
        {
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(16L*1024*1024,out var memory))return null;
            return new Load{Data=data,Memory=memory};
        }
        public void Dispose(){if(Closed)return;Closed=true;Data=null;Packet=null;Player=null;World=null;Connection=null;Bag=Belt=Buffs=BagFrame=BeltFrame=null;Memory?.Dispose();Memory=null;}
    }
    private static readonly object gate=new object();
    private static Load next,loaded;
    [ThreadStatic]private static Load joining;
    private static Load incomingBoundary;
    private static bool installed;
    internal static bool HasLifecycleCustody{get{lock(gate)return next!=null||loaded!=null||joining!=null||incomingBoundary!=null;}}
    internal static bool HasOutgoingLifecycleCustody
    {get{lock(gate)return next!=null||loaded!=null||joining!=null||incomingBoundary!=null&&!IncomingBoundaryCurrent();}}
    private static bool IncomingBoundaryCurrent()
    {
        var source=incomingBoundary;
        try
        {
            return source!=null&&ThreadManager.IsMainThread()&&!source.Closed&&source.ReadReturned&&!source.Joined&&!source.Hydrating&&!source.Hydrated&&
                source.World?.IsRemote()==true&&ReferenceEquals(source.World,GameManager.Instance?.World)&&source.Packet!=null&&
                ReferenceEquals(source.Data,source.Packet.playerDataFile)&&SameData(source)&&
                ReferenceEquals(source.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])&&!source.Connection.IsDisconnected();
        }
        catch{return false;}
    }
    internal sealed class Retirement
    {
        private readonly Load source;private readonly World world;private readonly string directory;
        private readonly RebirthNpcNetworkEpoch.ClientProjectionRetirement ended;
        private Retirement(Load source,World world,string directory,RebirthNpcNetworkEpoch.ClientProjectionRetirement ended)
        {this.source=source;this.world=world;this.directory=directory;this.ended=ended;}
        internal static Retirement Capture(World world,string directory,RebirthNpcNetworkEpoch.ClientProjectionRetirement ended)
        {
            lock(gate)
            {
                if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||ended==null||!ended.Matches(world,directory)||next!=null||joining!=null||incomingBoundary!=null&&!IncomingBoundaryCurrent()||
                    loaded!=null&&!ReferenceEquals(loaded.World,world))throw new InvalidOperationException("Trade observation retirement scope unavailable");
                return new Retirement(loaded,world,directory,ended);
            }
        }
        internal bool MatchesIncoming(RebirthNpcClientNativeReentry.Source incoming)
        {
            if(!ThreadManager.IsMainThread()||incoming?.Current()!=true||!ended.HasEnded||!ended.Matches(world,directory))return false;
            if(source==null)return true;
            return incoming.MatchesTradeObservation(source.Operation,source.Account,source.Creation,source.World.worldState.Guid,source.Deaths);
        }
        internal bool MatchesIncomingAdmission(RebirthNpcClientNativeReentry.Source incoming)
        {
            if(!ThreadManager.IsMainThread()||incoming?.Current()!=true||!ended.Matches(world,directory)||
                !ended.MatchesIncomingNamespace(incoming))return false;
            if(source==null)return true;
            return incoming.MatchesTradeObservation(source.Operation,source.Account,source.Creation,source.World.worldState.Guid,source.Deaths);
        }
        internal bool ReleaseDetachedObservation()
        {
            lock(gate)
            {
                if(!ThreadManager.IsMainThread()||!ended.Matches(world,directory)||!ended.HasEnded)return false;
                if(source==null)return true;
                if(ReferenceEquals(loaded,source)||ReferenceEquals(next,source)||ReferenceEquals(joining,source)||ReferenceEquals(incomingBoundary,source))return false;
                // Observation bytes/budget only. No original, native hold,
                // receipt, callback, player or item is changed by this disposal.
                source.Dispose();return true;
            }
        }
    }
    internal static Func<RebirthNpcTradeOwnerExchange.Release,bool>[] CaptureRetirement()
    {
        lock(gate)
        {
            if(!ThreadManager.IsMainThread()||next!=null||joining!=null||incomingBoundary!=null&&!IncomingBoundaryCurrent())throw new InvalidOperationException("Trade native read still active");
            var source=loaded;if(source==null)return Array.Empty<Func<RebirthNpcTradeOwnerExchange.Release,bool>>();
            var operation=source.Operation;string account=source.Account,creation=source.Creation,world=source.World.worldState.Guid;int deaths=source.Deaths;
            return new Func<RebirthNpcTradeOwnerExchange.Release,bool>[]{release=>release.MatchesNative(operation,account,creation,world,deaths)};
        }
    }
    internal static RebirthNpcSaveQuarantineDomain PrepareQuarantine(World world)
    {
        lock(gate)
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||joining!=null||next!=null||incomingBoundary!=null&&!IncomingBoundaryCurrent()||
                loaded!=null&&(!ReferenceEquals(loaded.World,world)||loaded.Closed||loaded.Hydrating||!loaded.Joined))throw new InvalidOperationException("Trade native observer original scope unavailable");
            var original=loaded;var incoming=incomingBoundary;var doc=LifecycleImage(original);string image=doc.OuterXml;
            var originalPlayer=original?.Player;var originalWorld=original?.World;var originalConnection=original?.Connection;var originalMemory=original?.Memory;
            return new RebirthNpcSaveQuarantineDomain("trade-native-owner-load",doc,
                ()=>{lock(gate)return next==null&&joining==null&&ReferenceEquals(incomingBoundary,incoming)&&(incoming==null||IncomingBoundaryCurrent())&&ReferenceEquals(loaded,original)&&ReferenceEquals(original?.Player,originalPlayer)&&
                    ReferenceEquals(original?.World,originalWorld)&&ReferenceEquals(original?.Connection,originalConnection)&&ReferenceEquals(original?.Memory,originalMemory)&&LifecycleImage(original).OuterXml==image;},
                ()=>{lock(gate)loaded=null;},()=>{lock(gate)loaded=original;});
        }
    }
    private static System.Xml.XmlDocument LifecycleImage(Load source)
    {
        var doc=new System.Xml.XmlDocument{XmlResolver=null};var root=doc.CreateElement("tradeNativeOwnerLoad");doc.AppendChild(root);
        if(source==null)return doc;
        if(source.Closed||source.Consumed||source.Data!=null||source.Packet!=null)throw new InvalidOperationException("Trade native join still executing");
        root.SetAttribute("operation",source.Operation.ToString("N"));root.SetAttribute("account",source.Account??string.Empty);root.SetAttribute("creation",source.Creation??string.Empty);
        root.SetAttribute("entity",source.Entity.ToString(System.Globalization.CultureInfo.InvariantCulture));root.SetAttribute("deaths",source.Deaths.ToString(System.Globalization.CultureInfo.InvariantCulture));
        root.SetAttribute("next",source.Next.ToString(System.Globalization.CultureInfo.InvariantCulture));root.SetAttribute("joined",source.Joined?"1":"0");root.SetAttribute("hydrated",source.Hydrated?"1":"0");
        foreach(var pair in new[]{new{Key="bag",Value=source.Bag},new{Key="belt",Value=source.Belt},new{Key="buffs",Value=source.Buffs},new{Key="bagFrame",Value=source.BagFrame},new{Key="beltFrame",Value=source.BeltFrame}})
        {if(pair.Value==null)throw new InvalidOperationException("Trade native original frame missing");var row=doc.CreateElement(pair.Key);root.AppendChild(row);row.InnerText=Convert.ToBase64String(pair.Value);}
        return doc;
    }
    internal static object CaptureAuditState(EntityPlayerLocal player)
    {
        if(!ThreadManager.IsMainThread())return new{available=false};
        var source=loaded;
        return new{available=true,observed=source!=null&&ReferenceEquals(source.Player,player),
            operation=source!=null&&ReferenceEquals(source.Player,player)?source.Operation.ToString("N"):string.Empty,
            current=source!=null&&ReferenceEquals(source.Player,player)&&source.Current(player)};
    }
    internal static void Install()
    {
        if(installed)return;
        if(GameManager.Instance?.World!=null)throw new InvalidOperationException("Native trade owner observation must install before World creation.");
        RebirthDogLifecycleService.InstallTradeNativeOwnerReadObservers();
        RebirthProductiveEarnedUiDeferredTransport.InstallSelectedSendObserver();RebirthNpcClientNativeReentry.Install();installed=true;
    }
    internal static object EnterRead(PlayerDataFile data,PooledBinaryReader reader,uint version,global::StreamModeRead mode,bool ran)
    {
        if(installed&&mode==global::StreamModeRead.Persistency)return RebirthNpcTradeLocalOwnerLoad.EnterRead(data,reader,version,mode,ran);
        if(!installed||!ran||data==null||reader?.BaseStream==null||version!=uint.MaxValue||mode!=global::StreamModeRead.FromServer)return null;
        return Load.Begin(data);
    }
    internal static void ObserveRead(bool ran,object state)
    {
        if(RebirthNpcTradeLocalOwnerLoad.IsState(state)){RebirthNpcTradeLocalOwnerLoad.ObserveRead(ran,state);return;}
        if(state is not Load source)return;
        if(!ran){source.ReadReturned=false;lock(gate)if(ReferenceEquals(next,source))next=null;source.Dispose();return;}
        try
        {
            var data=source.Data;
            if(!ran||data==null||data.bDead||data.bagData==null||data.inventoryData==null||data.buffData==null||
                data.bagData.Length+data.inventoryData.Length>4L*1024*1024||data.buffData.Length>1024*1024)return;
            source.Bag=data.bagData.ToArray();source.Belt=data.inventoryData.ToArray();source.Buffs=data.buffData.ToArray();source.Deaths=data.deaths;
            if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,RebirthNpcTradeOwnerCheckpoint.Held,out float held,out bool hasHeld)||!hasHeld||held!=1f||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,RebirthNpcTradeOwnerCheckpoint.HeldLife,out float life,out bool hasLife)||!hasLife||life!=data.deaths||
                !TryOperation(source.Buffs,out source.Operation))return;
            source.ReadReturned=true;
            lock(gate)
            {
                // The exact main-thread PlayerId observation is retained in
                // its own incoming scope during outgoing detachment. A later
                // raw read cannot displace that synchronous source.
                if(incomingBoundary!=null){source.ReadReturned=false;return;}
                next?.Dispose();next=source;
            }
        }
        catch{}
        finally{if(!source.ReadReturned)source.Dispose();}
    }
    private static bool SameData(Load source)
    {
        var data=source.Data;
        return data!=null&&!data.bDead&&data.deaths==source.Deaths&&data.bagData!=null&&data.inventoryData!=null&&data.buffData!=null&&
            data.bagData.Length==source.Bag.Length&&data.inventoryData.Length==source.Belt.Length&&data.buffData.Length==source.Buffs.Length&&
            source.Bag.SequenceEqual(data.bagData.ToArray())&&source.Belt.SequenceEqual(data.inventoryData.ToArray())&&source.Buffs.SequenceEqual(data.buffData.ToArray());
    }
    internal static object EnterJoin(NetPackagePlayerId packet,World world,GameManager callbacks,bool ran)
    {
        Load source=null;bool accepted=false;
        try
        {
            if(!installed||!ran||joining!=null||incomingBoundary!=null||!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||
                !ReferenceEquals(callbacks,GameManager.Instance)||!RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var connection)||
                !ReferenceEquals(connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])||connection.IsDisconnected())return null;
            lock(gate)
            {
                source=next;if(source?.ReadReturned!=true||!ReferenceEquals(source.Data,packet.playerDataFile)||!SameData(source))return null;
                next=null;source.Packet=packet;source.World=world;source.Connection=connection;incomingBoundary=source;
            }
            // Outside the read lock. The authenticated incoming observation
            // remains strongly retained; only the old loaded/flow/hold owners
            // participate in the outgoing typed transaction.
            if(!RebirthNpcLifecycle.PrepareNativeEpochBoundary())return null;
            lock(gate)
            {
                // Even another join in the same World cannot replace an
                // unresolved native observation. Consumption or typed outgoing
                // detachment must release its installed slot first.
                if(!IncomingBoundaryCurrent()||joining!=null||loaded!=null)return null;
                joining=source;accepted=true;return source;
            }
        }
        catch{return null;}
        finally
        {
            lock(gate)
            {
                if(source!=null&&ReferenceEquals(incomingBoundary,source))
                {
                    incomingBoundary=null;
                    if(!accepted)
                    {
                        source.Packet=null;source.World=null;source.Connection=null;
                        if(next==null)next=source;
                    }
                }
            }
        }
    }
    internal static object EnterHydration(PlayerDataFile data,EntityPlayer actor,global::StreamModeRead mode,bool ran)
    {
        if(installed&&mode==global::StreamModeRead.Persistency)return RebirthNpcTradeLocalOwnerLoad.EnterHydration(data,actor,mode,ran);
        var source=joining;
        try
        {
            if(!ran||source==null||source.Hydrating||source.Hydrated||mode!=global::StreamModeRead.FromServer||!ReferenceEquals(source.Data,data)||
                actor is not EntityPlayerLocal player||player.entityId!=source.Packet.id||!ReferenceEquals(player.world,source.World)||!SameData(source))return null;
            source.Player=player;source.Entity=player.entityId;source.Hydrating=true;return source;
        }
        catch{return null;}
    }
    internal static void ObserveHydration(PlayerDataFile data,EntityPlayer actor,bool ran,object state)
    {
        if(RebirthNpcTradeLocalOwnerLoad.IsState(state)){RebirthNpcTradeLocalOwnerLoad.ObserveHydration(data,actor,ran,state);return;}
        if(state is not Load source)return;
        try
        {
            if(!ran||!ReferenceEquals(joining,source)||!source.Hydrating||!ReferenceEquals(source.Data,data)||!ReferenceEquals(source.Player,actor)||
                actor.Died!=source.Deaths||!SameData(source)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return;
            source.BagFrame=RebirthCamperInventorySnapshot.Capture(RebirthGearPlayerDataInventory.ReadSlots(data,true));
            source.BeltFrame=RebirthCamperInventorySnapshot.Capture(RebirthGearPlayerDataInventory.ReadSlots(data,false));
            if(!source.BagFrame.SequenceEqual(RebirthCamperInventorySnapshot.Capture(source.Player.bag.ItemGrid.items))||
                !source.BeltFrame.SequenceEqual(RebirthCamperInventorySnapshot.Capture(source.Player.inventory.ItemGrid.items))||!SameStamp(source.Player,source.Operation,source.Buffs))return;
            source.Account=owner.CanonicalId;source.Creation=RebirthCamperCharacterAccess.CurrentCreation(actor);source.Hydrated=!string.IsNullOrWhiteSpace(source.Creation);
        }
        catch{}
    }
    internal static void EndHydration(object state,bool failed=false){if(RebirthNpcTradeLocalOwnerLoad.IsState(state)){RebirthNpcTradeLocalOwnerLoad.EndHydration(state,failed);return;}if(state is Load source){source.Hydrating=false;if(failed)source.Hydrated=false;}}
    internal static void ObserveJoin(NetPackagePlayerId packet,World world,bool ran,object state)
    {
        if(state is not Load source)return;
        try
        {
            if(!ran||!ReferenceEquals(joining,source)||!source.Hydrated||!ReferenceEquals(source.Packet,packet)||!ReferenceEquals(source.World,world)||
                !ReferenceEquals(world,GameManager.Instance?.World)||!ReferenceEquals(world.GetPrimaryPlayer(),source.Player)||
                !ReferenceEquals(world.GetEntity(source.Entity),source.Player)||!ReferenceEquals(source.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])||!SameData(source))return;
            source.Joined=true;lock(gate){loaded?.Dispose();loaded=source;}
        }
        catch{}
    }
    internal static void EndJoin(object state,Exception error)
    {
        if(state is not Load source)return;
        if(ReferenceEquals(joining,source))joining=null;source.Packet=null;
        if(error!=null)source.Joined=false;
        if(!source.Joined){lock(gate)if(ReferenceEquals(loaded,source))loaded=null;source.Dispose();}
        else source.Data=null; // Drop the native PDS graph after actual join.
    }
    internal static void Update()
    {
        var source=loaded;if(source==null)return;
        if(!ThreadManager.IsMainThread())return;
        // A World switch is not disposal authority. Leave the exact old source
        // (including its retry metadata) available to the typed boundary.
        if(!ReferenceEquals(source.World,GameManager.Instance?.World))return;
        long now=DateTime.UtcNow.Ticks;if(ReferenceEquals(source.World,GameManager.Instance?.World)&&source.Next>now)return;
        source.Next=DateTime.UtcNow.AddSeconds(1).Ticks;
        // PlayerId/ToPlayer can return before the native spawn completes.
        // Keep this one bounded observation while that exact live join waits;
        // no capability is exposed until Current confirms spawned identity.
        if(source.Joined&&source.Hydrated&&source.Player!=null&&!source.Player.IsDead()&&!source.Player.IsSpawned()&&
            ReferenceEquals(source.World,GameManager.Instance?.World)&&ReferenceEquals(source.Player.world,source.World)&&
            ReferenceEquals(source.World.GetPrimaryPlayer(),source.Player)&&ReferenceEquals(source.World.GetEntity(source.Entity),source.Player)&&
            source.Player.Died==source.Deaths&&ReferenceEquals(source.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])&&
            !source.Connection.IsDisconnected())return;
        if(!source.Current(source.Player)){lock(gate)if(ReferenceEquals(loaded,source))loaded=null;source.Dispose();return;}
        if(RebirthNpcTradeClientFlow.ResumeLoaded(Source.CaptureCurrent())){source.Consumed=true;lock(gate)if(ReferenceEquals(loaded,source))loaded=null;source.Dispose();}
    }
    private static bool SameStamp(EntityPlayerLocal player,Guid operation,byte[] buffs)
    {
        foreach(string key in new[]{RebirthNpcTradeOwnerCheckpoint.Marker(operation),RebirthNpcTradeOwnerExchange.Step(operation),
            RebirthNpcTradeOwnerCheckpoint.Held,RebirthNpcTradeOwnerCheckpoint.HeldLife,RebirthNpcTradeOwnerCheckpoint.HoldMarker(operation)})
            if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(buffs,key,out float value,out bool present)||player.Buffs.GetCustomVar(key)!=(present?value:0f))return false;
        return true;
    }
    internal static bool TryOperation(byte[] bytes,out Guid operation)
    {
        operation=Guid.Empty;string prefix="rbNpcTradeHold_";
        using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;
            int count=reader.ReadUInt16();for(int index=0;index<count;index++)new BuffValue().Read(reader,version);
            count=reader.ReadUInt16();for(int index=0;index<count;index++)
            {
                string name=reader.ReadString();float value=reader.ReadSingle();
                if(!name.StartsWith(prefix,StringComparison.Ordinal)||value!=1f)continue;
                if(operation!=Guid.Empty||name.Length!=prefix.Length+32||!Guid.TryParseExact(name.Substring(prefix.Length),"N",out operation)||operation==Guid.Empty)return false;
            }
            return stream.Position==stream.Length&&operation!=Guid.Empty;
        }
    }
}
