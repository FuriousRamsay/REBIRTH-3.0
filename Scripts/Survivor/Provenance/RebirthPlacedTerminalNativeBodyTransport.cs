using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    private static readonly RebirthRemoteResourceRepairSendQueue terminalTraceWire=new RebirthRemoteResourceRepairSendQueue();
    private static bool NativeFinalTraceOwner(EntityPlayer actor,bool owed,string image,out Guid operation)
    {
        operation=Guid.Empty;try{if(owed){var t=DecodeOwedPhysical(image);operation=t.Child.Operation;return OwedIdentity(actor,t.Child)&&actor.entityId==t.Observation.NativeOwnerEntity&&actor.Died>=t.Child.Deaths+1;}var trace=DecodePhysical(image);operation=trace.Observation.Original.Operation;return RebirthPlacedOriginal.SamePartitionOwner(actor,trace.Observation.Original)&&actor.entityId==trace.Observation.NativeOwnerEntity&&actor.Died>=trace.Observation.Original.Deaths+1;}catch{return false;}
    }
    private static void QueueNativeFinalTrace(EntityPlayerLocal player,bool owed,string trace)
    {
        try
        {
            if(!NativeFinalTraceOwner(player,owed,trace,out var operation))return;var world=(World)player.world;if(!world.IsRemote()){ReceiveNativeFinalTrace(world,player.entityId,owed,trace);return;}
            string image=new XElement("placedNativeFinalTrace",new XAttribute("version",1),new XAttribute("owed",owed),new XElement("trace",trace)).ToString(SaveOptions.DisableFormatting),key=operation.ToString("N")+"|"+Hash(image);if(terminalTraceWire.IsQueued(key))return;var body=new RebirthRemoteResourceFragments.Body(Encoding.UTF8.GetBytes(image));var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var connection=manager?.connectionToServer?[0];string directory=GameIO.GetSaveGameDir();
            terminalTraceWire.Enqueue(key,body,()=>ReferenceEquals(GameManager.Instance?.World,world)&&directory==GameIO.GetSaveGameDir()&&NativeFinalTraceOwner(player,owed,trace,out var same)&&same==operation,()=>RebirthLogisticsOriginalTransport.CanSendNative(connection),index=>manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPlacedNativeFinalTrace>().Setup(player.entityId,operation,body.Hash,body.Length,body.Count,index,body.Part(index)),false));
        }catch{/* Existing full native callback journal retains all custody. */}
    }
    private static string TerminalMirrorWire(TerminalBody t,string source)=>new XElement("placedTerminalNativeMirror",new XAttribute("version",1),new XAttribute("operation",t.Operation.ToString("N")),new XAttribute("original",t.Original.ToString("N")),new XAttribute("episode",t.Episode.ToString("N")),new XAttribute("owed",t.Owed),new XAttribute("trace",Hash(t.Trace)),new XAttribute("terminal",t.Terminal),new XAttribute("source",source)).ToString(SaveOptions.DisableFormatting);
    private static void SendTerminalNativeMirror(World world,EntityPlayer actor,TerminalBody t,string source)
    {string wire=TerminalMirrorWire(t,source);if(actor is EntityPlayerLocal local&&ReferenceEquals(world.GetPrimaryPlayer(),local))ReceiveTerminalNativeMirror(world,actor.entityId,wire);else{var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var client=manager?.Clients.ForEntityId(actor.entityId);if(client!=null&&RebirthLogisticsOriginalTransport.CanSendNative(client.netConnection?[0]))manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPlacedBodyPermit>().Setup(actor.entityId,wire),_attachedToEntityId:actor.entityId);}}
    private static string TerminalOwnerPath(TerminalBody t)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","terminal-native-owner",t.Operation.ToString("N")+".xml");
    private static string TerminalOwnerImage(TerminalBody t,string source)
    {var x=new XElement("placedTerminalNativeOwner",new XAttribute("version",1),new XAttribute("source",source),new XElement("body",t.Image));x.SetAttributeValue("checksum",Hash(x.ToString(SaveOptions.DisableFormatting)));return x.ToString(SaveOptions.DisableFormatting);}
    private static bool ReadTerminalOwner(string path,out TerminalBody t,out string source)
    {
        t=null;source=null;try{if(!File.Exists(path)||new FileInfo(path).Length>DeferredMaximum)return false;string image=File.ReadAllText(path,new UTF8Encoding(false,true));XElement x;using(var input=new StringReader(image))using(var reader=System.Xml.XmlReader.Create(input,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=DeferredMaximum}))x=XElement.Load(reader);if(x.Name!="placedTerminalNativeOwner"||(int?)x.Attribute("version")!=1)return false;t=DecodeTerminalBody((string)x.Element("body"));source=(string)x.Attribute("source");return FullHash(source)&&TerminalOwnerImage(t,source)==image;}catch{return false;}
    }
    private static bool SaveTerminalOwner(TerminalBody t,string source)
    {
        try{string path=TerminalOwnerPath(t),image=TerminalOwnerImage(t,source),folder=Path.GetDirectoryName(path);if(File.Exists(path)||File.Exists(path+".bak"))return ReadTerminalOwner(path,out var saved,out var proof)&&saved.Image==t.Image&&proof==source;Directory.CreateDirectory(folder);var pending=Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").ToArray();string temp;if(pending.Length!=0){if(pending.Length!=1||!ReadTerminalOwner(pending[0],out var old,out var native)||old.Image!=t.Image||native!=source)return false;temp=pending[0];}else{temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}}return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&ReadTerminalOwner(path,out var retained,out var retainedProof)&&retained.Image==t.Image&&retainedProof==source;}catch{return false;}
    }
    private static void ReceiveTerminalNativeMirror(World world,int entity,string wire)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,world)||world?.GetPrimaryPlayer() is not EntityPlayerLocal player||player.entityId!=entity)return;var x=OwedXml(wire);if(x.Name!="placedTerminalNativeMirror"||(int?)x.Attribute("version")!=1)return;Guid original=Guid.ParseExact((string)x.Attribute("original"),"N");bool owed=(bool)x.Attribute("owed");string trace;
            if(owed){if(!ReadOwedPhysical(original,false,out var own))return;trace=own.Image;}else{string path=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","physical-owner",original.ToString("N")+".xml");if(ReadPhysical(path,out var own))trace=own.Image;else if(ReadIntent(BodyIntentPath(original,false),out var intent))trace=intent.Image;else return;}
            if(Hash(trace)!=(string)x.Attribute("trace")||!NativeFinalTraceOwner(player,owed,trace,out var ownOriginal)||ownOriginal!=original)return;var t=DecodeTerminalBody(TerminalBodyImage(new TerminalBody{Operation=Guid.ParseExact((string)x.Attribute("operation"),"N"),Episode=Guid.ParseExact((string)x.Attribute("episode"),"N"),Owed=owed,Trace=trace,Terminal=(string)x.Attribute("terminal")}));string source=(string)x.Attribute("source");if(!FullHash(source)||TerminalMirrorWire(t,source)!=wire||!SaveTerminalOwner(t,source))return;
            RememberTerminalOwner(world,t,source);
            // The full new current-death capsule is now durable. Native spawn,
            // old hold retirement and its own failed-row parcel are deliberately
            // separate next protocol transitions, never an old item/head replay.
        }catch{}
    }    private static void ReceiveNativeFinalTrace(World world,int entity,bool owed,string trace)
    {
        try
        {
            var actor=world?.GetEntity(entity) as EntityPlayer;if(world?.IsRemote()!=false||!NativeFinalTraceOwner(actor,owed,trace,out _))return;
            if(TryPrepareTerminalNativeBody(actor,owed,trace,out var terminal)){if(CheckpointTerminalSource(actor,terminal,out var source)&&SaveTerminalAt(TerminalOperationPath(terminal.Operation),terminal)){if(terminal.Native.Length==0)RetainTerminalParcel(terminal,source);SendTerminalNativeMirror(world,actor,terminal,source);}return;}
            if(owed)ReceiveOwedPhysical(actor,trace);else ReceivePhysical(world,entity,trace);
        }catch{/* No missing/terminal record permits another old source effect. */}
    }
    internal static void ReceiveNativeFinalFragment(NetPackage packet,World world,int entity,Guid operation,string hash,int total,int count,int index,string text)
    {
        try
        {
            var sender=packet?.Sender;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||sender==null||sender.entityId!=entity||!packet.ValidEntityIdForSender(entity)||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(entity),sender)||total<1||total>DeferredMaximum||world.GetEntity(entity) is not EntityPlayer actor||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return;
            DeferredScope(world);string scope="placed-native-final|"+world.worldState.Guid+"|"+owner.StorageKey+"|"+entity+"|"+RebirthCamperCharacterAccess.CurrentCreation(actor)+"|"+deferredTransportEpoch;if(!RebirthRemoteResourceFragments.TryAdd(scope,operation,hash,total,count,index,text,DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond,out var bytes)||bytes==null)return;
            string image=new UTF8Encoding(false,true).GetString(bytes);XElement x;using(var input=new StringReader(image))using(var reader=System.Xml.XmlReader.Create(input,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=DeferredMaximum}))x=XElement.Load(reader);if(x.Name!="placedNativeFinalTrace"||(int?)x.Attribute("version")!=1)return;bool owed=(bool)x.Attribute("owed");string trace=(string)x.Element("trace");if(!NativeFinalTraceOwner(actor,owed,trace,out var original)||original!=operation||new XElement("placedNativeFinalTrace",new XAttribute("version",1),new XAttribute("owed",owed),new XElement("trace",trace)).ToString(SaveOptions.DisableFormatting)!=image)return;ReceiveNativeFinalTrace(world,entity,owed,trace);
        }catch{}
    }    private static string TerminalOriginalImage(TerminalBody t)=>t.Owed?ChildBody(DecodeOwedPhysical(t.Trace).Child):RebirthPlacedOriginal.EncodeRecord(DecodePhysical(t.Trace).Observation.Original);
    private static string TerminalRetiredPath(Guid original)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","terminal-native-retired",original.ToString("N")+".xml");
    private static string TerminalRetiredImage(TerminalBody t,string source)
    {var x=new XElement("placedTerminalNativeRetired",new XAttribute("version",1),new XAttribute("operation",t.Operation.ToString("N")),new XAttribute("original",t.Original.ToString("N")),new XAttribute("owed",t.Owed),new XAttribute("oldImage",Hash(TerminalOriginalImage(t))),new XAttribute("body",t.Digest),new XAttribute("source",source));x.SetAttributeValue("checksum",Hash(x.ToString(SaveOptions.DisableFormatting)));return x.ToString(SaveOptions.DisableFormatting);}
    internal static bool HasTerminalNativeRetirement(EntityPlayer actor,Guid original,bool owed,string originalHash)
    {
        try{string path=TerminalRetiredPath(original);if(!File.Exists(path)||new FileInfo(path).Length>4096)return false;string image=File.ReadAllText(path,new UTF8Encoding(false,true));var x=OwedXml(image);Guid operation=Guid.ParseExact((string)x.Attribute("operation"),"N");var probe=new TerminalBody{Operation=operation};return ReadTerminalOwner(TerminalOwnerPath(probe),out var t,out var source)&&TerminalOwner(actor,t)&&t.Original==original&&t.Owed==owed&&Hash(TerminalOriginalImage(t))==originalHash&&TerminalRetiredImage(t,source)==image;}catch{return false;}
    }
    private static bool SaveTerminalRetired(TerminalBody t,string source)
    {
        try{string path=TerminalRetiredPath(t.Original),image=TerminalRetiredImage(t,source),folder=Path.GetDirectoryName(path);if(File.Exists(path)||File.Exists(path+".bak"))return File.Exists(path)&&new FileInfo(path).Length<=4096&&File.ReadAllText(path,new UTF8Encoding(false,true))==image;Directory.CreateDirectory(folder);var pending=Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").ToArray();string temp;if(pending.Length!=0){if(pending.Length!=1||new FileInfo(pending[0]).Length>4096||File.ReadAllText(pending[0],new UTF8Encoding(false,true))!=image)return false;temp=pending[0];}else{temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}}return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&File.ReadAllText(path,new UTF8Encoding(false,true))==image;}catch{return false;}
    }
    private static bool RetireTerminalNativeMetadata(EntityPlayerLocal player,TerminalBody t,string source)
    {
        try
        {
            if(!TerminalOwner(player,t)||!ReadTerminalOwner(TerminalOwnerPath(t),out var retained,out var native)||retained.Image!=t.Image||native!=source||!SaveTerminalRetired(t,source))return false;
            if(t.Owed){var old=DecodeOwedPhysical(t.Trace).Child;if(owedAttempts.TryGetValue(old.Parent,out var attempt)&&SameChild(attempt.Child,old))owedAttempts.Remove(old.Parent);}
            else RebirthPlacedOriginal.RetireTerminalNativeOwnerMetadata(player,t.Original,Hash(TerminalOriginalImage(t)));
            deferredBodies.Remove(t.Original);return true;
        }catch{return false;}
    }
    private static bool DeferredOriginalRetired(EntityPlayer player,DeferredBody d)
    {try{if(d.Terminal)return false;string image=d.Owed?ChildBody(DecodeOwedPhysical(d.Trace).Child):RebirthPlacedOriginal.EncodeRecord(DecodeIntent(d.Trace).Observation.Original);return HasTerminalNativeRetirement(player,d.Operation,d.Owed,Hash(image));}catch{return false;}}    private sealed class TerminalOwnerRuntime{internal TerminalBody Body;internal string Source;internal float Next;}
    private static readonly Dictionary<Guid,TerminalOwnerRuntime> terminalOwnerPending=new Dictionary<Guid,TerminalOwnerRuntime>();
    private static World terminalOwnerWorld;private static string terminalOwnerDirectory;private static IEnumerator<string> terminalOwnerScan;private static float terminalOwnerNextScan;
    private static void TerminalOwnerScope(World world)
    {if(ReferenceEquals(world,terminalOwnerWorld)&&terminalOwnerDirectory==GameIO.GetSaveGameDir())return;terminalOwnerScan?.Dispose();terminalOwnerScan=null;terminalOwnerPending.Clear();terminalOwnerWorld=world;terminalOwnerDirectory=GameIO.GetSaveGameDir();terminalOwnerNextScan=0;}
    private static string TerminalOwnerDonePath(TerminalBody t)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","terminal-native-owner-completed",t.Operation.ToString("N")+".xml");
    private static string TerminalOwnerDoneImage(TerminalBody t,string source,string settlement)
    {var x=new XElement("placedTerminalNativeDone",new XAttribute("version",1),new XAttribute("operation",t.Operation.ToString("N")),new XAttribute("body",t.Digest),new XAttribute("source",source),new XAttribute("settlement",settlement));x.SetAttributeValue("checksum",Hash(x.ToString(SaveOptions.DisableFormatting)));return x.ToString(SaveOptions.DisableFormatting);}
    private static bool TerminalOwnerDone(TerminalBody t)
    {try{string path=TerminalOwnerDonePath(t);if(!File.Exists(path)||new FileInfo(path).Length>4096||!ReadTerminalOwner(TerminalOwnerPath(t),out var own,out var source)||own.Image!=t.Image)return false;string image=File.ReadAllText(path,new UTF8Encoding(false,true));var x=OwedXml(image);string settled=(string)x.Attribute("settlement");return FullHash(settled)&&TerminalOwnerDoneImage(t,source,settled)==image;}catch{return false;}}
    private static void RememberTerminalOwner(World world,TerminalBody t,string source)
    {TerminalOwnerScope(world);if(TerminalOwnerDone(t))return;if(terminalOwnerPending.ContainsKey(t.Operation))return;if(terminalOwnerPending.Count<128)terminalOwnerPending[t.Operation]=new TerminalOwnerRuntime{Body=t,Source=source};}
    internal static void ReceiveTerminalNativeQuery(World world,int entity,Guid operation,string digest,string source,int after)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||world.GetEntity(entity) is not EntityPlayer actor||!ReadTerminalOperation(operation,out var t)||!TerminalOwner(actor,t)||t.Digest!=digest||!ReadTerminalSource(TerminalSourcePath(t),t,out var native)||native!=source||after<0)return;bool complete=ProgressTerminalNativeHost(t,out var settlement);
            if(t.Inputs.Any(i=>t.Overflow||i.Failed)&&ReadOwed(operation,out var parcel)&&OwnTerminalParcel(parcel,t))QueryOwed(world,actor,operation,after);
            string wire=new XElement("placedTerminalNativeStatus",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),new XAttribute("body",t.Digest),new XAttribute("source",source),new XAttribute("complete",complete),new XAttribute("settlement",settlement??string.Empty)).ToString(SaveOptions.DisableFormatting);
            if(actor is EntityPlayerLocal local&&ReferenceEquals(world.GetPrimaryPlayer(),local))ReceiveTerminalNativeStatus(world,entity,wire);else{var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var client=manager?.Clients.ForEntityId(entity);if(client!=null&&RebirthLogisticsOriginalTransport.CanSendNative(client.netConnection?[0]))manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPlacedBodyPermit>().Setup(entity,wire),_attachedToEntityId:entity);}
        }catch{}
    }
    private static void ReceiveTerminalNativeStatus(World world,int entity,string wire)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world?.GetPrimaryPlayer() is not EntityPlayerLocal player||player.entityId!=entity)return;var x=OwedXml(wire);if(x.Name!="placedTerminalNativeStatus"||(int?)x.Attribute("version")!=1||!(bool)x.Attribute("complete"))return;Guid op=Guid.ParseExact((string)x.Attribute("operation"),"N");var probe=new TerminalBody{Operation=op};if(!ReadTerminalOwner(TerminalOwnerPath(probe),out var t,out var source)||!TerminalOwner(player,t)||t.Digest!=(string)x.Attribute("body")||source!=(string)x.Attribute("source"))return;string settlement=(string)x.Attribute("settlement");if(!FullHash(settlement))return;
            string path=TerminalOwnerDonePath(t),image=TerminalOwnerDoneImage(t,source,settlement);if(File.Exists(path)||File.Exists(path+".bak")){if(!TerminalOwnerDone(t))return;}else{Directory.CreateDirectory(Path.GetDirectoryName(path));var pending=Directory.EnumerateFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".*.tmp").ToArray();string temp;if(pending.Length!=0){if(pending.Length!=1||new FileInfo(pending[0]).Length>4096||File.ReadAllText(pending[0],new UTF8Encoding(false,true))!=image)return;temp=pending[0];}else{temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}}if(!RebirthDurableFileCommit.TryPublish(temp,path,out _)||!TerminalOwnerDone(t))return;}
            deferredBodies.Remove(op);terminalOwnerPending.Remove(op);
        }catch{}
    }
    private static void QueryTerminalOwner(EntityPlayerLocal player,TerminalOwnerRuntime pending)
    {
        var t=pending.Body;owedCompleted.TryGetValue(t.Operation,out int after);if(player.world.IsRemote()){var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;if(RebirthLogisticsOriginalTransport.CanSendNative(manager?.connectionToServer?[0]))manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPlacedTerminalBodyQuery>().Setup(player.entityId,t.Operation,t.Digest,pending.Source,after),false);}else ReceiveTerminalNativeQuery((World)player.world,player.entityId,t.Operation,t.Digest,pending.Source,after);
    }
    private static void UpdateTerminalNativeBodies(World world)
    {
        try
        {
            TerminalOwnerScope(world);if(world?.GetPrimaryPlayer() is not EntityPlayerLocal player)return;
            string folder=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","terminal-native-owner");if(terminalOwnerScan==null&&UnityEngine.Time.realtimeSinceStartup>=terminalOwnerNextScan&&Directory.Exists(folder))terminalOwnerScan=Directory.EnumerateFiles(folder,"*.xml").GetEnumerator();for(int n=0;n<2&&terminalOwnerScan!=null;n++){if(!terminalOwnerScan.MoveNext()){terminalOwnerScan.Dispose();terminalOwnerScan=null;terminalOwnerNextScan=UnityEngine.Time.realtimeSinceStartup+3f;break;}if(ReadTerminalOwner(terminalOwnerScan.Current,out var t,out var source)&&TerminalOwner(player,t))RememberTerminalOwner(world,t,source);}
            foreach(var pending in terminalOwnerPending.Values.ToArray())
            {
                var t=pending.Body;if(!TerminalOwner(player,t)||TerminalOwnerDone(t)){terminalOwnerPending.Remove(t.Operation);continue;}if(UnityEngine.Time.realtimeSinceStartup<pending.Next)continue;pending.Next=UnityEngine.Time.realtimeSinceStartup+1f;
                // Producer/metadata retirement is wired below only after the full
                // authenticated own capsule has been positively saved.
                if(!RetireTerminalNativeMetadata(player,t,pending.Source))continue;
                if(t.Native.Length!=0&&!t.Overflow)QueueDeferredBody(player,t.Owed,t.Image,true);
                QueryTerminalOwner(player,pending);
                if(DeferredRead(DeferredPath(new DeferredBody{Operation=t.Operation},false),out var attempted)&&attempted.Phase==2)terminalOwnerPending.Remove(t.Operation);
            }
        }catch{}
    }
}
[UnityEngine.Scripting.Preserve]
public sealed class NetPackageRebirthPlacedNativeFinalTrace:NetPackage
{
    private int entity,total,count,index;private Guid operation;private string hash=string.Empty,text=string.Empty;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthPlacedNativeFinalTrace Setup(int player,Guid original,string digest,int bytes,int parts,int part,string chunk){entity=player;operation=original;hash=digest;total=bytes;count=parts;index=part;text=chunk;return this;}
    public override void read(PooledBinaryReader reader){entity=reader.ReadInt32();var bytes=reader.ReadBytes(16);if(bytes.Length!=16)throw new EndOfStreamException();operation=new Guid(bytes);hash=reader.ReadString();total=reader.ReadInt32();count=reader.ReadInt32();index=reader.ReadInt32();text=reader.ReadString();if(hash.Length!=64||text.Length>RebirthRemoteResourceFragments.ChunkText)throw new InvalidDataException();}
    public override void write(PooledBinaryWriter writer){base.write(writer);RebirthPlacedOriginalDeathPartition.WriteDeferredFragmentPayload(writer,entity,operation,hash,total,count,index,text);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==false&&ValidEntityIdForSender(entity))RebirthPlacedOriginalDeathPartition.ReceiveNativeFinalFragment(this,world,entity,operation,hash,total,count,index,text);}public int GetLength()=>128+Encoding.UTF8.GetByteCount(text);
}
[UnityEngine.Scripting.Preserve]
public sealed class NetPackageRebirthPlacedTerminalBodyQuery:NetPackage
{
    private int entity,after;private Guid operation;private string digest=string.Empty,source=string.Empty;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthPlacedTerminalBodyQuery Setup(int player,Guid original,string body,string proof,int sequence){entity=player;operation=original;digest=body;source=proof;after=sequence;return this;}
    public override void read(PooledBinaryReader reader){entity=reader.ReadInt32();var bytes=reader.ReadBytes(16);if(bytes.Length!=16)throw new EndOfStreamException();operation=new Guid(bytes);digest=reader.ReadString();source=reader.ReadString();after=reader.ReadInt32();if(digest.Length!=64||source.Length!=64||after<0)throw new InvalidDataException();}
    public override void write(PooledBinaryWriter writer){base.write(writer);if(digest.Length!=64||source.Length!=64||after<0)throw new InvalidDataException();writer.Write(entity);writer.Write(operation.ToByteArray());writer.Write(digest);writer.Write(source);writer.Write(after);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==false&&ValidEntityIdForSender(entity))RebirthPlacedOriginalDeathPartition.ReceiveTerminalNativeQuery(world,entity,operation,digest,source,after);}public int GetLength()=>160;
}