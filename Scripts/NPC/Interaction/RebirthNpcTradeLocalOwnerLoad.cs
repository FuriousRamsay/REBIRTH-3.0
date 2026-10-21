using System;
using System.IO;
using System.Linq;

// Actual local native Load/OpenRead/Read/ToPlayer lineage. This observer never
// sets inventory, clears a hold, rewrites an original or issues a permission.
internal static partial class RebirthNpcTradeLocalOwnerLoad
{
    private sealed class Load:IDisposable
    {
        internal PlayerDataFile Data;internal World World;internal EntityPlayerLocal Player;
        internal string Directory,Path,Account,Creation;internal Stream Stream;internal PooledBinaryReader Reader;
        internal byte[] Bag,Belt,Buffs,BagFrame,BeltFrame;internal Guid Operation;internal int Deaths,Entity;
        internal bool ReadEntered,ReadReturned,Loaded,Hydrating,Hydrated,Closed;internal IDisposable Memory;
        public void Dispose(){if(Closed)return;Closed=true;Data=null;Player=null;World=null;Stream=null;Reader=null;Bag=Belt=Buffs=BagFrame=BeltFrame=null;Memory?.Dispose();Memory=null;}
    }
    internal sealed class Source
    {
        private readonly Load state;private Source(Load state){this.state=state;}
        internal EntityPlayerLocal Player=>state.Player;internal Guid Operation=>state.Operation;
        internal static Source CaptureCurrent(){var value=loaded;return value!=null&&RebirthNpcTradeLocalOwnerLoad.Current(value,value.Player)?new Source(value):null;}
        internal bool Current(EntityPlayerLocal player)=>ReferenceEquals(loaded,state)&&RebirthNpcTradeLocalOwnerLoad.Current(state,player);
        internal bool TryStage(out int step,out int phase)
        {
            step=phase=0;
            if(!Current(Player)||!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(state.Buffs,RebirthNpcTradeOwnerCheckpoint.Marker(Operation),out float p,out bool hasPhase)||!hasPhase||
                p!=1f&&p!=2f&&p!=3f||!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(state.Buffs,RebirthNpcTradeOwnerExchange.Step(Operation),out float s,out bool hasStep))return false;
            if(!hasStep)s=0;
            if(float.IsNaN(s)||float.IsInfinity(s)||s<0||s>378||s!=Math.Floor(s)||((int)s&1)!=0)return false;
            step=(int)s;phase=(int)p;return true;
        }
        internal bool Matches(RebirthNpcTradeOriginal original)=>original!=null&&Current(Player)&&original.Operation==Operation&&original.Account==state.Account&&
            original.Creation==state.Creation&&original.World==state.World.worldState.Guid&&original.Deaths==state.Deaths&&TryStage(out int step,out int phase)&&
            (phase==1||step==RebirthNpcTradePrefix.Changes(original)*2)&&RebirthNpcTradePrefix.Matches(original,Player.bag.ItemGrid.items,Player.inventory.ItemGrid.items,step)&&
            RebirthNpcTradeOwnerOriginalStore.IsRetained(Player,original);
        internal bool PersistCurrent(RebirthNpcTradeOriginal original)
        {
            if(!Matches(original)||!TryStage(out int step,out int phase))return false;
            if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||saved.bDead||saved.deaths!=original.Deaths||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,RebirthNpcTradeOwnerCheckpoint.Marker(Operation),out float p,out bool hasPhase)||!hasPhase||p!=phase||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,RebirthNpcTradeOwnerExchange.Step(Operation),out float s,out bool hasStep)||(hasStep?s:0f)!=step||
                !RebirthNpcTradePrefix.Matches(original,RebirthGearPlayerDataInventory.ReadSlots(saved,true),RebirthGearPlayerDataInventory.ReadSlots(saved,false),step))return false;
            GameManager.Instance.SaveLocalPlayerData();
            return Matches(original)&&RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(Player,original,out int actualStep,out int actualPhase)&&actualStep==step&&actualPhase==phase;
        }
        internal bool Transfer(RebirthNpcTradeService.OwnerAdmission.LocalPermission permission)
        {
            if(permission==null||!permission.Current()||!Matches(permission.Original))return false;
            if(ReferenceEquals(loaded,state))loaded=null;if(ReferenceEquals(pending,state))pending=null;state.Dispose();return true;
        }
    }
    private static Load loading,pending,loaded;
    internal sealed class Retirement
    {
        private sealed class Member
        {
            internal Load Source;internal Guid Operation;internal string Account,Creation,World;internal int Deaths;internal bool Released;
        }
        private readonly Member[] members;
        private Retirement(Member[] members){this.members=members;}
        internal bool Complete=>Array.TrueForAll(members,value=>value.Released);
        internal static Retirement Capture(World world)
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||loading!=null)
                throw new InvalidOperationException("Local native trade observation still active");
            var sources=new[]{pending,loaded}.Where(value=>value!=null).Distinct().ToArray();
            if(sources.Any(value=>!ReferenceEquals(value.World,world)||value.Closed||value.Hydrating||!value.ReadReturned||!value.Loaded||
                value.Operation==Guid.Empty||string.IsNullOrWhiteSpace(value.Account)||string.IsNullOrWhiteSpace(value.Creation)||
                value.Deaths<0||!Guid.TryParse(value.World.worldState?.Guid,out _)))
                throw new InvalidOperationException("Exact local native trade observation required");
            return new Retirement(sources.Select(value=>new Member{Source=value,Operation=value.Operation,Account=value.Account,
                Creation=value.Creation,World=value.World.worldState.Guid,Deaths=value.Deaths}).ToArray());
        }
        internal void Observe(RebirthNpcTradeOwnerExchange.Release proof)
        {
            if(!ThreadManager.IsMainThread()||proof==null||!proof.Current())return;
            foreach(var member in members)
            {
                if(member.Released||ReferenceEquals(loading,member.Source)||ReferenceEquals(pending,member.Source)||ReferenceEquals(loaded,member.Source)||
                    !proof.MatchesNative(member.Operation,member.Account,member.Creation,member.World,member.Deaths))continue;
                // Only detached observation bytes and their budget are retired.
                // The saved native release remains the sole disposition proof.
                member.Source.Dispose();member.Released=true;
            }
        }
    }
    private static long nextContinuation;
    internal static void ObserveRelease(RebirthNpcTradeOwnerExchange.Release proof)
    {
        var source=loaded;
        if(!ThreadManager.IsMainThread()||proof==null||!proof.Current()||source==null||source.Closed||!source.Hydrated||
            !proof.MatchesNative(source.Operation,source.Account,source.Creation,source.World.worldState.Guid,source.Deaths))return;
        loaded=null;if(ReferenceEquals(pending,source))pending=null;source.Dispose();
    }
    internal static void Update()
    {
        if(!ThreadManager.IsMainThread()||DateTime.UtcNow.Ticks<nextContinuation)return;
        nextContinuation=DateTime.UtcNow.AddSeconds(1).Ticks;
        RebirthDogLifecycleService.ObserveNativeOwnerReentries();
        var source=Source.CaptureCurrent();if(source==null)return;
        var reentry=NativeReentry.Capture(source);
        if(reentry!=null)RebirthNpcSaveQuarantineBundle.ObserveServerTradeNativeReentry(reentry);
        RebirthNpcTradeService.ResumeLocalLoaded(source);
    }
    internal static bool HasLifecycleCustody=>loading!=null||pending!=null||loaded!=null;
    internal static object CaptureAuditState(EntityPlayerLocal player)
    {
        var value=loaded;
        bool current=ThreadManager.IsMainThread()&&value!=null&&ReferenceEquals(value.Player,player)&&Current(value,player);
        var source=current?Source.CaptureCurrent():null;
        int step=0,phase=0;bool stage=source!=null&&source.TryStage(out step,out phase);
        return new{kind="local-native-load",current,loading=loading!=null,pending=pending!=null,loaded=value!=null,
            operation=value?.Operation.ToString("N")??string.Empty,account=value?.Account??string.Empty,
            creation=value?.Creation??string.Empty,hydrated=value?.Hydrated==true,stage,step,phase};
    }
    internal static RebirthNpcSaveQuarantineDomain PrepareQuarantine(World world)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||loading!=null||
            pending!=null&&(!ReferenceEquals(pending.World,world)||pending.Closed||pending.Hydrating)||
            loaded!=null&&(!ReferenceEquals(loaded.World,world)||loaded.Closed||loaded.Hydrating))throw new InvalidOperationException("Local trade native load still active or foreign");
        var originalPending=pending;var originalLoaded=loaded;var pendingData=pending?.Data;var loadedData=loaded?.Data;
        var pendingPlayer=pending?.Player;var loadedPlayer=loaded?.Player;var doc=LifecycleImage();string image=doc.OuterXml;
        return new RebirthNpcSaveQuarantineDomain("trade-local-native-load",doc,()=>loading==null&&ReferenceEquals(pending,originalPending)&&ReferenceEquals(loaded,originalLoaded)&&
            ReferenceEquals(pending?.Data,pendingData)&&ReferenceEquals(loaded?.Data,loadedData)&&ReferenceEquals(pending?.Player,pendingPlayer)&&ReferenceEquals(loaded?.Player,loadedPlayer)&&LifecycleImage().OuterXml==image,
            ()=>{pending=null;loaded=null;},()=>{pending=originalPending;loaded=originalLoaded;});
    }
    private static System.Xml.XmlDocument LifecycleImage()
    {
        var doc=new System.Xml.XmlDocument{XmlResolver=null};var root=doc.CreateElement("tradeLocalNativeLoad");doc.AppendChild(root);
        foreach(var entry in new[]{new{Key="pending",Value=pending},new{Key="loaded",Value=loaded}})
        {
            var value=entry.Value;if(value==null)continue;if(value.Closed||value.Hydrating||!value.ReadReturned||!value.Loaded)throw new InvalidOperationException("Local trade native lineage incomplete");
            var row=doc.CreateElement(entry.Key);root.AppendChild(row);row.SetAttribute("operation",value.Operation.ToString("N"));row.SetAttribute("path",value.Path??string.Empty);
            row.SetAttribute("directory",value.Directory??string.Empty);row.SetAttribute("account",value.Account??string.Empty);row.SetAttribute("creation",value.Creation??string.Empty);
            row.SetAttribute("entity",value.Entity.ToString(System.Globalization.CultureInfo.InvariantCulture));row.SetAttribute("deaths",value.Deaths.ToString(System.Globalization.CultureInfo.InvariantCulture));row.SetAttribute("hydrated",value.Hydrated?"1":"0");
            foreach(var frame in new[]{new{Key="bag",Value=value.Bag},new{Key="belt",Value=value.Belt},new{Key="buffs",Value=value.Buffs},new{Key="bagFrame",Value=value.BagFrame},new{Key="beltFrame",Value=value.BeltFrame}})
            {var cell=doc.CreateElement(frame.Key);row.AppendChild(cell);cell.SetAttribute("present",frame.Value!=null?"1":"0");if(frame.Value!=null)cell.InnerText=Convert.ToBase64String(frame.Value);}
        }
        return doc;
    }
    internal static object EnterLoad(PlayerDataFile data,string directory,string name,bool ran)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!ran||!ThreadManager.IsMainThread()||world?.IsRemote()!=false||data==null||loading!=null||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||name!=owner.CanonicalId||
                Path.GetFullPath(directory)!=Path.GetFullPath(GameIO.GetPlayerDataDir()))return null;
            // Capture/detach the outgoing native observer before setting the
            // incoming loading slot, which correctly refuses quarantine while
            // an actual Read/ToPlayer lineage is still executing.
            if(!RebirthNpcLifecycle.PrepareNativeEpochBoundary()||!ReferenceEquals(world,GameManager.Instance?.World)||
                loading!=null||pending!=null||loaded!=null||
                Path.GetFullPath(directory)!=Path.GetFullPath(GameIO.GetPlayerDataDir())||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var actualOwner)||actualOwner.CanonicalId!=owner.CanonicalId)return null;
            string path=Path.GetFullPath(Path.Combine(directory,name+"."+PlayerDataFile.EXT));
            if(!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(16L*1024*1024,out var memory))return null;
            return loading=new Load{Data=data,World=world,Directory=Path.GetFullPath(GameIO.GetSaveGameDir()),Path=path,Account=owner.CanonicalId,Memory=memory};
        }
        catch{return null;}
    }
    internal static void ObserveOpen(string path,Stream stream,bool ran)
    {
        var source=loading;
        if(ran&&source!=null&&source.Stream==null&&stream!=null&&Path.GetFullPath(path)==source.Path&&stream.CanRead&&stream.CanSeek&&stream.Position==0&&stream.Length>=6&&stream.Length<=16L*1024*1024)source.Stream=stream;
    }
    internal static object EnterRead(PlayerDataFile data,PooledBinaryReader reader,uint version,global::StreamModeRead mode,bool ran)
    {
        var source=loading;
        if(!ran||source==null||source.ReadEntered||!ReferenceEquals(source.Data,data)||version!=PlayerDataFile.cFileVersion||mode!=global::StreamModeRead.Persistency||
            !ReferenceEquals(reader?.BaseStream,source.Stream)||source.Stream==null||source.Stream.Position!=5)return null;
        source.Reader=reader;source.ReadEntered=true;return source;
    }
    internal static bool IsState(object state)=>state is Load;
    internal static void ObserveRead(bool ran,object state)
    {
        if(state is not Load source)return;
        source.ReadReturned=false;if(!ran)return;
        var data=source.Data;
        if(!source.ReadEntered||source.Reader?.BaseStream!=source.Stream||source.Stream.Position!=source.Stream.Length||data.bDead||data.bagData==null||data.inventoryData==null||data.buffData==null||
            data.bagData.Length+data.inventoryData.Length>4L*1024*1024||data.buffData.Length>1024*1024)return;
        source.Bag=data.bagData.ToArray();source.Belt=data.inventoryData.ToArray();source.Buffs=data.buffData.ToArray();source.Deaths=data.deaths;
        if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,RebirthNpcTradeOwnerCheckpoint.Held,out float held,out bool present)||!present||held!=1f||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,RebirthNpcTradeOwnerCheckpoint.HeldLife,out float life,out bool hasLife)||!hasLife||life!=source.Deaths||
            !RebirthNpcTradeOwnerLoad.TryOperation(source.Buffs,out source.Operation))return;
        source.ReadReturned=true;
    }
    internal static void ObserveLoad(PlayerDataFile data,bool ran,object state)
    {
        if(state is not Load source||!ran||!ReferenceEquals(loading,source)||!ReferenceEquals(source.Data,data)||!source.ReadReturned||!SameData(source)||
            loaded!=null||pending!=null&&!ReferenceEquals(pending,source))return;
        source.Loaded=true;pending=source;
    }
    internal static void EndLoad(object state,Exception error)
    {
        if(state is not Load source)return;if(ReferenceEquals(loading,source))loading=null;source.Stream=null;source.Reader=null;
        if(error!=null||!source.Loaded){if(ReferenceEquals(pending,source))pending=null;source.Dispose();}
    }
    private static bool SameData(Load source)=>source.Data!=null&&!source.Data.bDead&&source.Data.deaths==source.Deaths&&
        source.Data.bagData!=null&&source.Data.inventoryData!=null&&source.Data.buffData!=null&&source.Bag.SequenceEqual(source.Data.bagData.ToArray())&&
        source.Belt.SequenceEqual(source.Data.inventoryData.ToArray())&&source.Buffs.SequenceEqual(source.Data.buffData.ToArray());
    internal static object EnterHydration(PlayerDataFile data,EntityPlayer actor,global::StreamModeRead mode,bool ran)
    {
        var source=pending;
        if(!ran||!ThreadManager.IsMainThread()||loaded!=null||source?.Loaded!=true||source.Hydrating||source.Hydrated||mode!=global::StreamModeRead.Persistency||
            !ReferenceEquals(source.Data,data)||actor is not EntityPlayerLocal player||!ReferenceEquals(player.world,source.World)||!ReferenceEquals(source.World,GameManager.Instance?.World)||!SameData(source))return null;
        source.Player=player;source.Entity=player.entityId;source.Hydrating=true;return source;
    }
    internal static void ObserveHydration(PlayerDataFile data,EntityPlayer actor,bool ran,object state)
    {
        if(state is not Load source||!ran||loaded!=null||!source.Hydrating||!ReferenceEquals(pending,source)||!ReferenceEquals(source.Data,data)||!ReferenceEquals(source.Player,actor)||
            actor.Died!=source.Deaths||!SameData(source))return;
        source.BagFrame=RebirthCamperInventorySnapshot.Capture(RebirthGearPlayerDataInventory.ReadSlots(data,true));source.BeltFrame=RebirthCamperInventorySnapshot.Capture(RebirthGearPlayerDataInventory.ReadSlots(data,false));
        if(!source.BagFrame.SequenceEqual(RebirthCamperInventorySnapshot.Capture(actor.bag.ItemGrid.items))||!source.BeltFrame.SequenceEqual(RebirthCamperInventorySnapshot.Capture(actor.inventory.ItemGrid.items)))return;
        source.Creation=RebirthCamperCharacterAccess.CurrentCreation(actor);if(string.IsNullOrWhiteSpace(source.Creation))return;
        source.Hydrated=true;loaded=source;pending=null;
    }
    internal static void EndHydration(object state,bool failed)
    {
        if(state is not Load source)return;source.Hydrating=false;
        if(failed){if(ReferenceEquals(loaded,source))loaded=null;if(ReferenceEquals(pending,source))pending=null;source.Dispose();}
        else if(source.Hydrated)source.Data=null;
    }
    private static bool Current(Load source,EntityPlayerLocal player)=>!source.Closed&&source.Loaded&&source.Hydrated&&ThreadManager.IsMainThread()&&
        ReferenceEquals(source.Player,player)&&ReferenceEquals(source.World,GameManager.Instance?.World)&&!source.World.IsRemote()&&ReferenceEquals(player.world,source.World)&&
        ReferenceEquals(player,source.World.GetPrimaryPlayer())&&ReferenceEquals(player,source.World.GetEntity(source.Entity))&&player.entityId==source.Entity&&player.IsSpawned()&&!player.IsDead()&&player.Died==source.Deaths&&
        Path.GetFullPath(GameIO.GetSaveGameDir())==source.Directory&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.CanonicalId==source.Account&&
        RebirthCamperCharacterAccess.CurrentCreation(player)==source.Creation&&source.BagFrame.SequenceEqual(RebirthCamperInventorySnapshot.Capture(player.bag.ItemGrid.items))&&
        source.BeltFrame.SequenceEqual(RebirthCamperInventorySnapshot.Capture(player.inventory.ItemGrid.items))&&SameStamp(source,player);
    private static bool SameStamp(Load source,EntityPlayerLocal player)
    {
        foreach(string key in new[]{RebirthNpcTradeOwnerCheckpoint.Held,RebirthNpcTradeOwnerCheckpoint.HeldLife,RebirthNpcTradeOwnerCheckpoint.HoldMarker(source.Operation),
            RebirthNpcTradeOwnerCheckpoint.Marker(source.Operation),RebirthNpcTradeOwnerExchange.Step(source.Operation)})
            if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(source.Buffs,key,out float value,out bool present)||player.Buffs.GetCustomVar(key)!=(present?value:0f))return false;
        return true;
    }
}
