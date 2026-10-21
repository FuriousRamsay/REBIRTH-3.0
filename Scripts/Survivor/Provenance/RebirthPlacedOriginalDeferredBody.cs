using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml;
using System.Collections.Generic;

// Own placement body protocol. Native fatal authority and native body publication
// remain separate; neither receipt nor a missing attempt proves a spawned body.
internal static partial class RebirthPlacedOriginalDeathPartition
{
    private const int DeferredMaximum=RebirthRemoteResourceFragments.MaximumBody;
    private sealed class DeferredBody
    { internal Guid Operation; internal bool Owed,Terminal; internal string Trace,Creation,World,Owner,Character,Permit,Nonce; internal int NativeOwner,Phase; internal bool SessionPermit; internal World WorldReference; internal string Directory; internal float Next; }
    private static readonly Dictionary<Guid,DeferredBody> deferredBodies=new Dictionary<Guid,DeferredBody>();
    private static World deferredWorld; private static string deferredDirectory;private static string deferredTransportEpoch=Guid.NewGuid().ToString("N");
    [ThreadStatic] private static DeferredBody deferredExecuting;
    [ThreadStatic] private static EntityCreationData deferredCreation;
    private static string DeferredPath(DeferredBody d,bool server)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original",server?"body-prepared-server":"body-deferred-owner",d.Operation.ToString("N")+".xml");
    private static string DeferredImage(DeferredBody d)=>new XElement("placedDeferredBody",new XAttribute("version",1),new XAttribute("operation",d.Operation.ToString("N")),new XAttribute("owed",d.Owed),new XAttribute("terminal",d.Terminal),new XAttribute("phase",d.Phase),new XAttribute("permit",d.Permit??string.Empty),new XAttribute("nonce",d.Nonce??string.Empty),new XElement("trace",d.Trace)).ToString(SaveOptions.DisableFormatting);
    private static DeferredBody DeferredDecode(string image)
    {
        if(image==null||Encoding.UTF8.GetByteCount(image)>DeferredMaximum)throw new InvalidDataException();XElement x;using(var input=new StringReader(image))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=DeferredMaximum}))x=XElement.Load(reader);if(x.Name!="placedDeferredBody"||(int?)x.Attribute("version")!=1)throw new InvalidDataException();
        var d=new DeferredBody{Operation=Guid.ParseExact((string)x.Attribute("operation"),"N"),Owed=(bool)x.Attribute("owed"),Terminal=(bool)x.Attribute("terminal"),Phase=(int)x.Attribute("phase"),Permit=(string)x.Attribute("permit"),Nonce=(string)x.Attribute("nonce"),Trace=(string)x.Element("trace")};
        if(d.Terminal){var t=DecodeTerminalBody(d.Trace);d.Creation=t.Native;d.World=t.World;d.Owner=t.Owner;d.Character=t.Creation;d.NativeOwner=t.NativeOwner;if(t.Operation!=d.Operation||t.Native.Length==0||t.Overflow)throw new InvalidDataException();}else if(d.Owed){var t=DecodeOwedPhysical(d.Trace);d.Creation=t.Creation;d.World=t.Child.World;d.Owner=t.Child.Owner;d.Character=t.Child.Creation;d.NativeOwner=t.Observation.NativeOwnerEntity;if(t.Child.Operation!=d.Operation||t.Overflow)throw new InvalidDataException();}
        else{var t=DecodeIntent(d.Trace);d.Creation=t.Creation;d.World=t.Observation.Original.World;d.Owner=t.Observation.Original.Owner;d.Character=t.Observation.Original.Creation;d.NativeOwner=t.Observation.NativeOwnerEntity;if(t.Observation.Original.Operation!=d.Operation)throw new InvalidDataException();}
        if(d.Operation==Guid.Empty||d.Creation.Length==0||d.Phase<0||d.Phase>2||d.Phase==0&&(d.Permit.Length!=0||d.Nonce.Length!=0)||d.Phase>0&&(!Guid.TryParseExact(d.Nonce,"N",out var nonce)||nonce==Guid.Empty||d.Permit!=DeferredPermit(d))||DeferredImage(d)!=image)throw new InvalidDataException();return d;
    }
    private static string DeferredPermitWire(DeferredBody d)=>new XElement("placedDeferredPermit",new XAttribute("version",1),new XAttribute("operation",d.Operation.ToString("N")),new XAttribute("trace",Hash(d.Trace)),new XAttribute("nonce",d.Nonce),new XAttribute("permit",d.Permit)).ToString(SaveOptions.DisableFormatting);
    private static string DeferredPermit(DeferredBody d)=>Hash("placed-body-prepared-v1|"+d.Operation.ToString("N")+"|"+d.Owed+"|"+d.Terminal+"|"+d.Trace+"|"+d.Nonce);
    private static bool DeferredRead(string path,out DeferredBody d)
    {d=null;try{if(!File.Exists(path)||new FileInfo(path).Length>DeferredMaximum)return false;d=DeferredDecode(File.ReadAllText(path,new UTF8Encoding(false,true)));return true;}catch{return false;}}
    private static bool RecoverHostDeferredPermit(DeferredBody original,out DeferredBody permit)
    {
        permit=null;try{string path=DeferredPath(original,true);if(File.Exists(path)||File.Exists(path+".bak"))return DeferredRead(path,out permit)&&permit.Phase==1&&SameDeferred(permit,original);string folder=Path.GetDirectoryName(path);if(!Directory.Exists(folder))return false;var files=Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").ToArray();if(files.Length!=1||!DeferredRead(files[0],out var pending)||pending.Phase!=1||!SameDeferred(pending,original))return false;return RebirthDurableFileCommit.TryPublish(files[0],path,out _)&&DeferredRead(path,out permit)&&DeferredImage(permit)==DeferredImage(pending);}catch{return false;}
    }    private static bool SameDeferred(DeferredBody a,DeferredBody b)=>a.Operation==b.Operation&&a.Owed==b.Owed&&a.Terminal==b.Terminal&&a.Trace==b.Trace;
    private static bool DeferredSave(DeferredBody d,bool server)
    {
        try
        {
            string path=DeferredPath(d,server),image=DeferredImage(d);DeferredDecode(image);if(File.Exists(path)||File.Exists(path+".bak")){if(!DeferredRead(path,out var old)||!SameDeferred(old,d)||old.Phase>d.Phase)return false;if(old.Phase==d.Phase)return DeferredImage(old)==image;}
            Directory.CreateDirectory(Path.GetDirectoryName(path));var pending=Directory.EnumerateFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".*.tmp").ToArray();string temp;
            if(pending.Length!=0){if(pending.Length!=1||!DeferredRead(pending[0],out var prior)||DeferredImage(prior)!=image)return false;temp=pending[0];}
            else{temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}}
            return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&DeferredRead(path,out var saved)&&DeferredImage(saved)==image;
        }catch{return false;}
    }
    private static bool HasDeferredPermit(bool owed,Guid operation,string trace)
    {try{var d=new DeferredBody{Owed=owed,Operation=operation,Trace=trace};return DeferredRead(DeferredPath(d,true),out var host)&&host.Phase==1&&!host.Terminal&&host.Owed==owed&&host.Operation==operation&&host.Trace==trace&&host.Permit==DeferredPermit(host);}catch{return false;}}    private static void DeferredScope(World world)
    {if(ReferenceEquals(deferredWorld,world)&&deferredDirectory==GameIO.GetSaveGameDir())return;deferredBodies.Clear();deferredWire.Clear();terminalTraceWire.Clear();deferredTransportEpoch=Guid.NewGuid().ToString("N");deferredWorld=world;deferredDirectory=GameIO.GetSaveGameDir();}
    private static bool DeferredOwner(EntityPlayer player,DeferredBody d)
    {return player!=null&&ReferenceEquals(player.world,GameManager.Instance?.World)&&player.world.worldState.Guid==d.World&&player.entityId==d.NativeOwner&&RebirthCamperCharacterAccess.CurrentCreation(player)==d.Character&&(d.Terminal?TerminalOwner(player,DecodeTerminalBody(d.Trace)):d.Owed?OwedIdentity(player,DecodeOwedPhysical(d.Trace).Child):RebirthPlacedOriginal.SamePartitionOwner(player,DecodeIntent(d.Trace).Observation.Original));}
    private static void QueueDeferredBody(EntityPlayerLocal player,bool owed,string trace,bool terminal=false)
    {
        var probe=new DeferredBody{Owed=owed,Terminal=terminal,Trace=trace,Phase=0,Permit=string.Empty,Nonce=string.Empty};probe.Operation=terminal?DecodeTerminalBody(trace).Operation:owed?DecodeOwedPhysical(trace).Child.Operation:DecodeIntent(trace).Observation.Original.Operation;var d=DeferredDecode(DeferredImage(probe));if(!DeferredOwner(player,d))throw new InvalidDataException();if(DeferredOriginalRetired(player,d))return;DeferredScope((World)player.world);
        if(DeferredRead(DeferredPath(d,false),out var old)){if(!SameDeferred(old,d))throw new InvalidDataException();d=old;}else if(!DeferredSave(d,false))throw new InvalidDataException("Original body retained; durable deferred admission unavailable.");
        if(deferredBodies.TryGetValue(d.Operation,out var live)&&SameDeferred(live,d)&&live.Phase==d.Phase)d=live;d.WorldReference=(World)player.world;d.Directory=GameIO.GetSaveGameDir();foreach(var attempted in deferredBodies.Values.Where(v=>v.Phase==2).ToArray())deferredBodies.Remove(attempted.Operation);if(deferredBodies.Count>=128&&!deferredBodies.ContainsKey(d.Operation))return;deferredBodies[d.Operation]=d;
    }
    private static void SendDeferred(EntityPlayerLocal player,DeferredBody d)
    {if(player.world.IsRemote())QueueDeferredFragments(player,d);else ReceiveDeferredBody((World)player.world,player.entityId,DeferredImage(d));}
    internal static void ReceiveDeferredBody(World world,int entity,string image)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world))return;var actor=world.GetEntity(entity) as EntityPlayer;var d=DeferredDecode(image);if(d.Phase>1||!DeferredOwner(actor,d))return;
            // Positively save the existing purpose-specific intent only AFTER
            // its real fatal lineage has reached this host. Earlier retries wait.
            if(d.Terminal){if(!PrepareTerminalNativeDeferred(actor,DecodeTerminalBody(d.Trace)))return;}else if(d.Owed){var t=DecodeOwedPhysical(d.Trace);if(!ValidOwedPhysicalOwner(actor,t,true)||!SaveOwedPhysical(t,true))return;RememberOwedBody(t);}
            else{var t=DecodeIntent(d.Trace);if(!RebirthPlacedOriginal.ValidatePartition(actor,t.Observation.Original,t.Observation.Policy,t.Observation.NativeOwnerEntity)||!Save(t.Observation,true)||!SaveIntent(t,true)||!SavePhysical(t,true))return;RememberIntent(world,t);}
            if(RecoverHostDeferredPermit(d,out var retained)){if(retained.Phase!=1||!SameDeferred(retained,d))return;d=retained;}else{d.Phase=1;d.Nonce=Guid.NewGuid().ToString("N");d.Permit=DeferredPermit(d);if(!DeferredSave(d,true))return;}
            if(actor is EntityPlayerLocal local&&ReferenceEquals(world.GetPrimaryPlayer(),local))AcceptDeferredBody(world,entity,DeferredPermitWire(d));else SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPlacedBodyPermit>().Setup(entity,DeferredPermitWire(d)),_attachedToEntityId:entity);
        }catch{/* Ambiguous or not-yet-fatal original retains its owner body. */}
    }
    internal static void AcceptDeferredBody(World world,int entity,string image)
    {
        try{if(image.StartsWith("<placedTerminalNativeStatus",StringComparison.Ordinal)){ReceiveTerminalNativeStatus(world,entity,image);return;}if(image.StartsWith("<placedTerminalNativeMirror",StringComparison.Ordinal)){ReceiveTerminalNativeMirror(world,entity,image);return;}if(world?.GetPrimaryPlayer() is not EntityPlayerLocal player||player.entityId!=entity)return;var x=OwedXml(image);if(x.Name!="placedDeferredPermit"||(int?)x.Attribute("version")!=1)return;Guid operation=Guid.ParseExact((string)x.Attribute("operation"),"N");var probe=new DeferredBody{Operation=operation};if(!DeferredRead(DeferredPath(probe,false),out var old)||old.Phase>1||Hash(old.Trace)!=(string)x.Attribute("trace"))return;var permit=DeferredDecode(DeferredImage(old));permit.Phase=1;permit.Nonce=(string)x.Attribute("nonce");permit.Permit=(string)x.Attribute("permit");DeferredDecode(DeferredImage(permit));if(DeferredPermitWire(permit)!=image||!DeferredOwner(player,permit)||!DeferredSave(permit,false))return;DeferredScope(world);permit.WorldReference=world;permit.Directory=GameIO.GetSaveGameDir();permit.SessionPermit=true;deferredBodies[permit.Operation]=permit;}catch{}
    }
    private static void UpdateDeferredBodies(World world)
    {
        try
        {
            DeferredScope(world);deferredWire.Pump(4);terminalTraceWire.Pump(4);UpdateTerminalNativeBodies(world);if(world?.GetPrimaryPlayer() is not EntityPlayerLocal player)return;
            foreach(var d in deferredBodies.Values.ToArray())
            {
                if(!ReferenceEquals(d.WorldReference,world)||d.Directory!=GameIO.GetSaveGameDir()||!DeferredOwner(player,d)||UnityEngine.Time.realtimeSinceStartup<d.Next)continue;d.Next=UnityEngine.Time.realtimeSinceStartup+1f;if(d.Terminal?TerminalOwnerDone(DecodeTerminalBody(d.Trace)):DeferredOriginalRetired(player,d)){deferredBodies.Remove(d.Operation);continue;}
                if(world.IsRemote()&&SingletonMonoBehaviour<ConnectionManager>.Instance?.IsConnected!=true){d.SessionPermit=false;continue;}if(d.Phase==2){if(world.IsRemote())SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPlacedBodyExecute>().Setup(player.entityId,d.Operation,Hash(d.Trace),d.Nonce,d.Permit));continue;}if(d.Phase==0||d.Phase==1&&!d.SessionPermit){SendDeferred(player,d);continue;}if(d.Phase!=1)continue;
                if(world.IsRemote()){var connection=SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0];if(connection is not NetConnectionAbs native||!native.fullConnection||!RebirthLogisticsOriginalTransport.CanSendNative(connection))continue;}

                // Persist before callback. A crash/throw from here never retries
                // an unknown native invocation or reconstructs another body.
                d.Phase=2;if(!DeferredSave(d,false)){d.Phase=1;continue;}
                if(world.IsRemote()){SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPlacedBodyExecute>().Setup(player.entityId,d.Operation,Hash(d.Trace),d.Nonce,d.Permit));continue;}
                var ecd=ReadEcd(d.Creation);if(ecd.id!=-1)continue;
                if(!world.IsRemote())
                {
                    if(!DeferredRead(DeferredPath(d,true),out var host)||host.Phase!=1||!SameDeferred(host,d))continue;
                    bool began=d.Terminal?BeginTerminalNativeInvocation(DecodeTerminalBody(d.Trace)):d.Owed?BeginOwedBodyInvocation(DecodeOwedPhysical(d.Trace)):BeginBodyInvocation(DecodeIntent(d.Trace));if(!began)continue;
                }
                var previous=deferredExecuting;var previousCreation=deferredCreation;deferredExecuting=d;deferredCreation=ecd;
                try{GameManager.Instance.RequestToSpawnEntityServer(ecd);}finally{deferredExecuting=previous;deferredCreation=previousCreation;}
            }
        }catch{/* Attempted native outcomes remain retained, never replayed. */}
    }
    private static bool ObserveDeferredBody(EntityBackpack body)
    {
        try{var d=deferredExecuting;var world=GameManager.Instance?.World;if(d==null||world?.IsRemote()!=false||!ReferenceEquals(d.WorldReference,world)||d.Directory!=GameIO.GetSaveGameDir()||deferredCreation?.id!=body.entityId||body.entityId<=0||!ReferenceEquals(world.GetEntity(body.entityId),body))return false;if(d.Terminal)return RebirthPlacedOriginalBodyPublication.AdmitTerminal(world,d.Operation,body,d.Creation);if(d.Owed)return RebirthPlacedOriginalBodyPublication.AdmitOwed(world,d.Operation,body,d.Creation);var t=DecodeIntent(d.Trace);if(!ValidStoredBodyFrame(world,t.Observation.Original,d.Creation))return false;RebirthPlacedOriginalBodyPublication.Admit(world,t.Observation.Original,d.NativeOwner,body,d.Creation);return true;}catch{return false;}
    }
}
[UnityEngine.Scripting.Preserve]
public sealed class NetPackageRebirthPlacedBodyPermit:NetPackage
{
    private int entity;private string image=string.Empty;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthPlacedBodyPermit Setup(int player,string body){entity=player;image=body;return this;}
    public override void read(PooledBinaryReader reader){entity=reader.ReadInt32();int length=reader.ReadInt32();if(length<1||length>2048)throw new InvalidDataException();var bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();image=new UTF8Encoding(false,true).GetString(bytes);}public override void write(PooledBinaryWriter writer){base.write(writer);if(Encoding.UTF8.GetByteCount(image)>2048)throw new InvalidDataException();writer.Write(entity);RebirthPlacedOriginalDeathPartition.WriteWire(writer,image);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==true)RebirthPlacedOriginalDeathPartition.AcceptDeferredBody(world,entity,image);}public int GetLength()=>8+Encoding.UTF8.GetByteCount(image);
}