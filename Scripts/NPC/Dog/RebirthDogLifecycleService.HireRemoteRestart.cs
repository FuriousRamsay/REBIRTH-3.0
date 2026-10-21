using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public static partial class RebirthDogLifecycleService
{
    private static readonly object HireRemoteReadGate=new object();
    private static IDisposable HireRemoteReadBudget;
    private sealed class HireRemoteRead
    {
        internal PlayerDataFile Data;internal PooledBinaryReader Reader;internal byte[] Belt,Bag,Buffs;
        internal Guid Nonce;internal string Dog;internal int Phase,Deaths;internal bool Returned,EnteredHydration,Hydrated,Joined;
        internal NetPackagePlayerId Packet;internal World World;internal INetConnection Connection;internal EntityPlayerLocal Actor;
        internal ItemStack[] BeltGrid,BagGrid,BeltRefs,BagRefs;
        internal IDisposable NativeBudget;
    }
    private static HireRemoteRead HireRemoteLoaded;
    private static HireRemoteRead HireRemoteNextRead;
    private sealed class HireRemoteReady {internal World World;internal EntityPlayer Actor;internal ClientInfo Peer;internal Guid Nonce;internal string Dog,Account,Creation;internal int Deaths;}
    private static readonly System.Collections.Generic.Dictionary<Guid,HireRemoteReady> HireRemoteReadies=new System.Collections.Generic.Dictionary<Guid,HireRemoteReady>();
    private sealed class HireRemoteTerminal {internal World World;internal EntityPlayer Actor;internal string Original,Account,Creation,Dog;internal Guid Nonce;internal int Deaths;internal byte Phase;}
    private static readonly System.Collections.Generic.Dictionary<Guid,HireRemoteTerminal> HireRemoteTerminals=new System.Collections.Generic.Dictionary<Guid,HireRemoteTerminal>();
    [ThreadStatic]private static HireRemoteRead HireRemoteJoining;
    private sealed class HireRemoteDogBirth {internal EntityRebirthDogCompanion Dog;internal World World;internal string Stable;}
    private static readonly System.Collections.Generic.Dictionary<string,HireRemoteDogBirth> HireRemoteBodies=new System.Collections.Generic.Dictionary<string,HireRemoteDogBirth>();
    private static string HireNonceKey(string kind,int index)=>"rbDogHire"+kind+index;
    private static void StampHireClick(RemoteHireRequest request)
    {
        byte[] nonce=request.Nonce.ToByteArray(),dog=Guid.ParseExact(request.Dog.RebirthRuntimeState.StableId.ToString(),"N").ToByteArray();
        for(int index=0;index<8;index++)
        {
            request.Actor.Buffs.SetCustomVar(HireNonceKey("Click",index),BitConverter.ToUInt16(nonce,index*2)+1f,true);
            request.Actor.Buffs.SetCustomVar(HireNonceKey("Dog",index),BitConverter.ToUInt16(dog,index*2)+1f,true);
        }
        request.Actor.Buffs.SetCustomVar("rbDogHirePhase",1f,true);
    }
    private static bool ReadHireStamp(byte[] buffs,out Guid nonce,out string dog,out int phase)
    {
        nonce=Guid.Empty;dog=null;phase=0;var n=new byte[16];var d=new byte[16];
        for(int index=0;index<8;index++)foreach(bool original in new[]{true,false})
        {
            if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(buffs,HireNonceKey(original?"Click":"Dog",index),out float value,out bool present)||!present||value<1||value>65536||value!=(int)value)return false;
            ushort word=(ushort)((int)value-1);var target=original?n:d;target[index*2]=(byte)word;target[index*2+1]=(byte)(word>>8);
        }
        if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(buffs,"rbDogHirePhase",out float state,out bool exists)||!exists||state<1||state>6||state!=(int)state)return false;
        nonce=new Guid(n);dog=new Guid(d).ToString("N");phase=(int)state;return nonce!=Guid.Empty&&new Guid(d)!=Guid.Empty;
    }
    private static bool HirePdsStamp(PlayerDataFile data,Guid nonce,string dog,int phase)
    {
        return data?.buffData!=null&&data.buffData.Length<=1048576&&ReadHireStamp(data.buffData.ToArray(),out var captured,out var body,out int state)&&captured==nonce&&body==dog&&state==phase;
    }
    internal static object EnterHireRemoteRead(PlayerDataFile data,PooledBinaryReader reader,uint version,global::StreamModeRead mode,bool run)
    {
        // Worker-safe: no World, Unity, identity lookup or mutation. Storage was
        // reserved on the main thread by the early installer before native reads.
        if(!run||mode!=global::StreamModeRead.FromServer||version!=uint.MaxValue||reader==null||HireRemoteReadBudget==null)return null;
        lock(HireRemoteReadGate)
        {
            if(HireRemoteLoaded!=null&&(!HireRemoteLoaded.Joined||HireRemoteNextRead!=null))return null;
            var source=new HireRemoteRead{Data=data,Reader=reader};if(HireRemoteLoaded==null)HireRemoteLoaded=source;else HireRemoteNextRead=source;return source;
        }
    }
    internal static void ObserveHireRemoteRead(bool run,object state)
    {
        if(state is not HireRemoteRead source)return;
        lock(HireRemoteReadGate)try
        {
            var data=source.Data;
            if(!run||data?.inventoryData==null||data.bagData==null||data.buffData==null||data.inventoryData.Length>1048576||data.bagData.Length>1048576||data.buffData.Length>1048576)return;
            source.Buffs=data.buffData.ToArray();if(!ReadHireStamp(source.Buffs,out source.Nonce,out source.Dog,out source.Phase)||source.Phase==6)return;
            source.Belt=data.inventoryData.ToArray();source.Bag=data.bagData.ToArray();source.Deaths=data.deaths;source.Returned=true;
        }
        catch{}
        finally{source.Reader=null;if(!source.Returned){if(ReferenceEquals(HireRemoteLoaded,source))HireRemoteLoaded=null;if(ReferenceEquals(HireRemoteNextRead,source))HireRemoteNextRead=null;}}
    }
    internal static object EnterHireRemoteJoin(NetPackagePlayerId packet,World world,GameManager callbacks,bool run)
    {
        if(!run||!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||!ReferenceEquals(callbacks,GameManager.Instance)||HireRemoteJoining!=null||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var connection)||!ReferenceEquals(connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0]))return null;
        lock(HireRemoteReadGate)
        {
            // An actual authenticated fresh PlayerId join retires old OBSERVATION
            // references only. Its native stamp and host original remain intact.
            if(RemoteHireClient!=null)RetireRemoteHireObservation(RemoteHireClient);
            if(HireRemoteNextRead!=null&&ReferenceEquals(HireRemoteNextRead.Data,packet.playerDataFile))
            {RetireRemoteHireRead(HireRemoteLoaded);HireRemoteLoaded=HireRemoteNextRead;HireRemoteNextRead=null;}
            var source=HireRemoteLoaded;if(source?.Returned!=true||!ReferenceEquals(source.Data,packet.playerDataFile)||!SameHireRemoteRead(source))return null;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out source.NativeBudget))return null;
            source.Packet=packet;source.World=world;source.Connection=connection;HireRemoteJoining=source;return source;
        }
    }
    private static bool SameHireRemoteRead(HireRemoteRead source)=>source.Data!=null&&source.Data.deaths==source.Deaths&&!source.Data.bDead&&
        source.Belt.SequenceEqual(source.Data.inventoryData.ToArray())&&source.Bag.SequenceEqual(source.Data.bagData.ToArray())&&source.Buffs.SequenceEqual(source.Data.buffData.ToArray());
    internal static void EnterHireRemoteHydration(PlayerDataFile data,EntityPlayer actor,global::StreamModeRead mode,bool run)
    {
        var source=HireRemoteJoining;
        if(!run||mode!=global::StreamModeRead.FromServer||source==null||!ReferenceEquals(source.Data,data)||source.EnteredHydration||!SameHireRemoteRead(source)||
            actor is not EntityPlayerLocal local||local.entityId!=source.Packet.id||!ReferenceEquals(local.world,source.World))return;
        source.EnteredHydration=true;source.Actor=local;
    }
    internal static void ObserveHireRemoteHydration(PlayerDataFile data,EntityPlayer actor,bool run)
    {
        var source=HireRemoteJoining;
        if(!run||source?.EnteredHydration!=true||!ReferenceEquals(source.Data,data)||!ReferenceEquals(source.Actor,actor))return;
        var belt=RebirthGearPlayerDataInventory.ReadSlots(data,false);var bag=RebirthGearPlayerDataInventory.ReadSlots(data,true);
        source.BeltGrid=actor.inventory?.ItemGrid.items;source.BagGrid=actor.bag?.ItemGrid.items;
        // Native hydration itself binds these new objects. Unrelated rows may
        // undergo native normalization; only the chosen financial original is
        // later required to match its complete saved before/after item image.
        if(!SameHireRemoteRead(source)||source.BeltGrid==null||source.BagGrid==null||belt==null||bag==null||source.BeltGrid.Length!=belt.Length||source.BagGrid.Length!=bag.Length)return;
        source.BeltRefs=(ItemStack[])source.BeltGrid.Clone();source.BagRefs=(ItemStack[])source.BagGrid.Clone();source.Hydrated=true;
    }
    internal static void ObserveHireRemoteJoin(NetPackagePlayerId packet,World world,bool run,object state)
    {
        if(state is not HireRemoteRead source)return;
        if(run&&source.Hydrated&&ReferenceEquals(source.Packet,packet)&&ReferenceEquals(source.World,world)&&ReferenceEquals(world.GetPrimaryPlayer(),source.Actor)&&ReferenceEquals(world.GetEntity(source.Actor.entityId),source.Actor)&&
            ReferenceEquals(source.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0]))source.Joined=true;
    }
    internal static void EndHireRemoteJoin(object state,Exception error)
    {
        if(state is not HireRemoteRead source)return;
        if(error!=null)source.Joined=false;
        if(ReferenceEquals(HireRemoteJoining,source))HireRemoteJoining=null;
        source.Packet=null;if(!source.Joined){source.NativeBudget?.Dispose();source.NativeBudget=null;lock(HireRemoteReadGate)if(ReferenceEquals(HireRemoteLoaded,source))HireRemoteLoaded=null;}
    }
    internal static void ObserveHireRemoteDogBirth(EntityRebirthDogCompanion dog,global::StreamModeRead mode)
    {
        if(!ThreadManager.IsMainThread()||mode!=global::StreamModeRead.FromServer||dog?.NativeIdentityReadable!=true||dog.RebirthSavedStableIdForDebug.IsEmpty||
            !dog.NativeStableIdentity.IsEmpty&&dog.NativeStableIdentity!=dog.RebirthSavedStableIdForDebug||HireRemoteBodies.Count>=256)return;
        var world=GameManager.Instance?.World;if(world?.IsRemote()!=true)return;
        string stable=dog.RebirthSavedStableIdForDebug.ToString();HireRemoteBodies[stable]=new HireRemoteDogBirth{Dog=dog,World=world,Stable=stable};
    }
    private static void PumpRemoteHireRestart()
    {
        var retained=RemoteHireClient;
        if(retained!=null&&(!ReferenceEquals(retained.World,GameManager.Instance?.World)||!ReferenceEquals(retained.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])))RetireRemoteHireObservation(retained);
        lock(HireRemoteReadGate)if(HireRemoteLoaded?.World!=null&&!ReferenceEquals(HireRemoteLoaded.World,GameManager.Instance?.World)){RetireRemoteHireRead(HireRemoteLoaded);HireRemoteLoaded=null;}
        foreach(var entry in HireRemoteReadies.Where(entry=>!ReferenceEquals(entry.Value.World,GameManager.Instance?.World)||
            !ReferenceEquals(entry.Value.World.GetEntity(entry.Value.Actor.entityId),entry.Value.Actor)||!ReferenceEquals(entry.Value.Peer,SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(entry.Value.Actor.entityId))).ToArray())HireRemoteReadies.Remove(entry.Key);
        foreach(var entry in HireRemoteBodies.Where(entry=>!ReferenceEquals(entry.Value.World,GameManager.Instance?.World)&&!ReferenceEquals(entry.Value.Dog,RemoteHireClient?.Dog)).ToArray())HireRemoteBodies.Remove(entry.Key);
        foreach(var entry in HireRemoteTerminals.Where(entry=>!ReferenceEquals(entry.Value.World,GameManager.Instance?.World)).ToArray())HireRemoteTerminals.Remove(entry.Key);
        if(RemoteHireClient!=null)return;
        HireRemoteRead source;lock(HireRemoteReadGate)source=HireRemoteLoaded;
        if(source?.Joined!=true||!ReferenceEquals(source.World,GameManager.Instance?.World)||!ReferenceEquals(source.World.GetPrimaryPlayer(),source.Actor)||source.Actor.Died!=source.Deaths||source.Actor.IsDead()||
            !ReferenceEquals(source.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])||!HireRemoteBodies.TryGetValue(source.Dog,out var body)||!ReferenceEquals(body.World,source.World)||
            !ReferenceEquals(source.World.GetEntity(body.Dog.entityId),body.Dog)||!ReferenceEquals(source.Actor.inventory.ItemGrid.items,source.BeltGrid)||!ReferenceEquals(source.Actor.bag.ItemGrid.items,source.BagGrid)||
            !source.BeltRefs.Select((row,index)=>ReferenceEquals(row,source.BeltGrid[index])).All(value=>value)||!source.BagRefs.Select((row,index)=>ReferenceEquals(row,source.BagGrid[index])).All(value=>value)||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return;
        string creation=RebirthCamperCharacterAccess.CurrentCreation(source.Actor);if(string.IsNullOrEmpty(creation))return;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(16L*1024*1024,out var budget))return;
        RemoteHireClient=new RemoteHireRequest{Nonce=source.Nonce,Actor=source.Actor,Dog=body.Dog,World=source.World,Connection=source.Connection,Creation=creation,Account=owner.CanonicalId,Deaths=source.Deaths,
            Budget=budget,ColdRead=source,DebitEntered=source.Phase>=2,Debited=source.Phase>=3,RefundEntered=source.Phase>=4,Refunded=source.Phase==5};
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthDogHireOwnerReady>().Setup(source.Actor.entityId,source.Nonce,source.Dog),false);
    }
    private static void RetireRemoteHireRead(HireRemoteRead source)
    {
        if(source==null)return;source.NativeBudget?.Dispose();source.NativeBudget=null;source.Data=null;source.Reader=null;source.Packet=null;source.Actor=null;source.World=null;source.Connection=null;
        source.Belt=source.Bag=source.Buffs=null;source.BeltGrid=source.BagGrid=source.BeltRefs=source.BagRefs=null;
    }
    private static void RetireRemoteHireObservation(RemoteHireRequest original)
    {
        if(!ReferenceEquals(RemoteHireClient,original))return;RemoteHireClient=null;
        lock(HireRemoteReadGate)
        {
            if(ReferenceEquals(HireRemoteLoaded,original.ColdRead))HireRemoteLoaded=null;
            RetireRemoteHireRead(original.ColdRead);original.ColdRead=null;
        }
        foreach(var item in HireRemoteBodies.Where(pair=>ReferenceEquals(pair.Value.World,original.World)).ToArray())HireRemoteBodies.Remove(item.Key);
        original.Slots.Clear();original.Actor=null;original.Dog=null;original.World=null;original.Connection=null;original.Budget?.Dispose();original.Budget=null;
        // No phase6, PDS send, native slot setter, refund, or terminal declaration.
    }
    private static bool BindColdRemoteHireSources(RemoteHireRequest pending,XElement image,bool terminalOnly)
    {
        var source=pending.ColdRead;if(source==null||pending.Sources!=null)return pending.Sources!=null;
        if(source.Phase==2||source.Phase==4)return false;
        if(!source.Joined||source.Nonce!=pending.Nonce||source.Dog!=(string)image.Attribute("dog"))return false;
        if(terminalOnly){pending.Sources=new XElement("sources",image.Elements("source").Select(row=>new XElement(row))).ToString(SaveOptions.DisableFormatting);return true;}
        var belt=RebirthGearPlayerDataInventory.ReadSlots(source.Data,false);var bag=RebirthGearPlayerDataInventory.ReadSlots(source.Data,true);int cost=0;
        foreach(var row in image.Elements("source"))
        {
            if(row.Attributes().Count()!=4||row.Elements().Count()!=2||pending.Slots.Count>=HireItemCount)return false;
            bool isBelt=(bool)row.Attribute("belt");int slot=(int)row.Attribute("slot");var grid=isBelt?source.BeltGrid:source.BagGrid;var saved=isBelt?belt:bag;var refs=isBelt?source.BeltRefs:source.BagRefs;
            var before=DecodeHireOriginalItem(row,"beforeItem","beforeCount");var after=DecodeHireOriginalItem(row,"afterItem","afterCount");
            if(slot<0||slot>=grid.Length||slot>=saved.Length||!ReferenceEquals(refs[slot],grid[slot])||!RebirthStationGridIngredients.IsSameStackSnapshot(saved[slot],pending.Refunded||!pending.Debited?before:after)||
                !RebirthStationGridIngredients.IsSameStackSnapshot(grid[slot],saved[slot])||before.count<=after.count||before.itemValue.type!=ItemClass.GetItem(HireItemName).type||pending.Slots.Any(value=>value.Belt==isBelt&&value.Slot==slot))return false;
            cost=checked(cost+before.count-after.count);pending.Slots.Add(new HireDebitSource{Belt=isBelt,Slot=slot,Grid=grid,Before=before,After=after,OriginalReference=grid[slot],AppliedReference=pending.Debited?grid[slot]:null});
        }
        if(cost!=HireItemCount)return false;pending.Sources=RemoteHireSources(pending.Slots);return true;
    }
    private static void SaveRemoteHireRequest(HireSettlement original)
    {
        if(!IsRemoteHire(original))return;
        string path=Path.Combine(original.Directory,"remote-request.xml");var image=new XDocument(new XElement("dogHireRemoteRequest",new XAttribute("version",1),new XAttribute("nonce",original.RemoteNonce.ToString("N")),new XElement("original",original.Original)));
        if(!RebirthAtomicXmlFile.TryWrite(path,image,out var error)||!XNode.DeepEquals(ReadHireXml(path),image.Root))throw new IOException("Remote hire request publication failed: "+error);
    }
    private static bool HasRemoteHireOriginalForDog(RebirthNpcStableId dog)
    {
        string root=HireRoot();if(!Directory.Exists(root))return false;RequireHirePath(root);
        var candidates=Directory.EnumerateDirectories(root,dog+".*").Take(65).ToArray();if(candidates.Length>64)return false;
        foreach(string directory in candidates)
        {
            try
            {
                var prepared=ReadHireXml(Path.Combine(directory,"prepared.xml"));string text=(string)prepared.Element("original");if(!HasHireStage(directory,"prepared",text))continue;
                var image=ReadRemoteHireXml(text);var remote=ReadHireXml(Path.Combine(directory,"remote-request.xml"));
                if((string)image.Attribute("dog")==dog.ToString()&&SameHirePath((string)image.Attribute("save"),GameIO.GetSaveGameDir())&&
                    (string)remote.Element("original")==text&&Guid.TryParseExact((string)remote.Attribute("nonce"),"N",out var nonce)&&nonce!=Guid.Empty)return true;
            }
            catch{}
        }
        return false;
    }
    private static void ReleaseRemoteHireClient(RemoteHireRequest pending)
    {
        if(!ReferenceEquals(RemoteHireClient,pending))return;
        pending.Actor.Buffs.SetCustomVar("rbDogHirePhase",6f,true);
        pending.Actor.Buffs.RemoveCustomVar("rbDogHire_"+pending.Nonce.ToString("N"));
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(pending.Actor),false);
        RemoteHireClient=null;pending.Budget?.Dispose();pending.Budget=null;
        if(pending.ColdRead is HireRemoteRead source)
        {
#if REBIRTH_DEBUG
            if(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest")RemoteHireLastJoin=new{source.Returned,source.Hydrated,source.Joined,source.Phase,source.Dog};
#endif
            lock(HireRemoteReadGate)if(ReferenceEquals(HireRemoteLoaded,source))HireRemoteLoaded=null;
            source.Data=null;source.Actor=null;source.World=null;source.Connection=null;source.Belt=source.Bag=source.Buffs=null;source.BeltGrid=source.BagGrid=source.BeltRefs=source.BagRefs=null;
            source.NativeBudget?.Dispose();source.NativeBudget=null;
            HireRemoteBodies.Remove(source.Dog);pending.ColdRead=null;
        }
    }
    private static bool ReadRemoteHireRequest(HireSettlement original,PlayerDataFile actualLoad)
    {
        string path=Path.Combine(original.Directory,"remote-request.xml");var image=ReadHireXml(path);
        if(image.Name!="dogHireRemoteRequest"||image.Attributes().Count()!=2||(int?)image.Attribute("version")!=1||image.Elements().Count()!=1||(string)image.Element("original")!=original.Original||
            !Guid.TryParseExact((string)image.Attribute("nonce"),"N",out var nonce)||nonce==Guid.Empty||actualLoad.buffData==null||actualLoad.buffData.Length>1048576||
            !ReadHireStamp(actualLoad.buffData.ToArray(),out var actual,out var dog,out int phase)||actual!=nonce||dog!=original.State.StableId.ToString()||phase!=(original.Refunded?5:original.Debited?3:1))return false;
        original.RemoteNonce=nonce;return true;
    }
    private static void RegisterRemoteHireTerminal(string text,string directory,HireNativeOwnerLoad loaded,HireNativeDogLoad body,bool never,bool refunded)
    {
        if(ReferenceEquals(loaded.World.GetPrimaryPlayer(),loaded.Actor)||HireRemoteTerminals.Count>=4)return;
        var image=ReadRemoteHireXml(text);
        var original=new HireSettlement{Directory=directory,Original=text,State=body.Dog.RebirthRuntimeState,Debited=!never,Refunded=refunded};
        if(!ReadRemoteHireRequest(original,loaded.Data)||(int?)image.Attribute("deaths")>loaded.Deaths||(string)image.Attribute("account")!=loaded.Owner)return;
        HireRemoteTerminals[original.RemoteNonce]=new HireRemoteTerminal{World=loaded.World,Actor=loaded.Actor,Original=text,Account=loaded.Owner,Creation=(string)image.Attribute("creation"),Dog=body.Stable.ToString(),
            Nonce=original.RemoteNonce,Deaths=loaded.Deaths,Phase=never?(byte)3:(byte)2};
        if(HireRemoteReadies.TryGetValue(original.RemoteNonce,out var ready))SendRemoteHireTerminal(ready);
    }
    private static bool SendRemoteHireTerminal(HireRemoteReady ready)
    {
        if(!HireRemoteTerminals.TryGetValue(ready.Nonce,out var terminal)||!ReferenceEquals(terminal.World,ready.World)||!ReferenceEquals(terminal.Actor,ready.Actor)||terminal.Account!=ready.Account||terminal.Creation!=ready.Creation||terminal.Deaths!=ready.Deaths||terminal.Dog!=ready.Dog)return false;
        ready.Peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthDogHireOwnerGrant>().Setup(ready.Actor.entityId,terminal.Nonce,terminal.Phase,terminal.Original));
        HireRemoteTerminals.Remove(ready.Nonce);HireRemoteReadies.Remove(ready.Nonce);return true;
    }
    internal static void ReceiveRemoteHireReady(World world,int actor,Guid nonce,string dog)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(world,GameManager.Instance?.World))return;
        var player=world.GetEntity(actor) as EntityPlayer;var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor);
        if(player==null||peer==null||nonce==Guid.Empty||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var account)||HireRemoteReadies.Count>=4&&!HireRemoteReadies.ContainsKey(nonce))return;
        HireRemoteReadies[nonce]=new HireRemoteReady{World=world,Actor=player,Peer=peer,Nonce=nonce,Dog=dog,Account=account.CanonicalId,Creation=RebirthCamperCharacterAccess.CurrentCreation(player),Deaths=player.Died};
        if(SendRemoteHireTerminal(HireRemoteReadies[nonce]))return;
        foreach(var original in HireSettlements.Values)
            if(original.Player.entityId==actor&&original.RemoteNonce==nonce&&original.State.StableId.ToString()==dog&&HireCurrent(original))
            {ConsumeRemoteHireReady(original);return;}
    }
    private static bool ConsumeRemoteHireReady(HireSettlement original)
    {
        if(!HireRemoteReadies.TryGetValue(original.RemoteNonce,out var ready)||!ReferenceEquals(ready.World,original.World)||!ReferenceEquals(ready.Actor,original.Player)||
            !ReferenceEquals(ready.Peer,SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(original.Player.entityId))||ready.Account!=original.Account||ready.Creation!=original.Creation||
            ready.Deaths!=original.Deaths||ready.Dog!=original.State.StableId.ToString()||!HireCurrent(original))return false;
        original.RemoteClientReady=true;HireRemoteReadies.Remove(original.RemoteNonce);return true;
    }
}

[UnityEngine.Scripting.Preserve]public sealed class NetPackageRebirthDogHireOwnerReady:NetPackage
{
    private int actor;private Guid nonce;private string dog;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthDogHireOwnerReady Setup(int a,Guid n,string d){actor=a;nonce=n;dog=d;return this;}
    public override void read(PooledBinaryReader reader){actor=reader.ReadInt32();nonce=new Guid(reader.ReadBytes(16));dog=RebirthDogLifecycleService.ReadHireWire(reader);if(dog.Length!=32)throw new InvalidDataException();}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write(actor);writer.Write(nonce.ToByteArray());RebirthDogLifecycleService.WriteHireWire(writer,dog);}
    public override void ProcessPackage(World world,GameManager callbacks){if(ValidEntityIdForSender(actor))RebirthDogLifecycleService.ReceiveRemoteHireReady(world,actor,nonce,dog);}
    public int GetLength()=>0;
}
