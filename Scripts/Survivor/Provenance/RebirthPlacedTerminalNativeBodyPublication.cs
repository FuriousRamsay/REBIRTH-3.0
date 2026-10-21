using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    private static bool TerminalOwner(EntityPlayer actor,TerminalBody t)
    {return actor!=null&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&actor.world.worldState.Guid==t.World&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&actor.entityId==t.NativeOwner&&actor.Died>=t.Deaths&&RebirthCamperCharacterAccess.CurrentCreation(actor)==t.Creation&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&owner.StorageKey==t.Owner;}
    private static bool TerminalFatalLineage(TerminalBody t)
    {
        try{string path=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","terminal-native-fatal",t.Episode.ToString("N")+".xml");return File.Exists(path)&&new FileInfo(path).Length<=4096&&DecodeTerminalFatal(File.ReadAllText(path,new UTF8Encoding(false,true)),out var f)&&f.Episode==t.Episode&&f.World==t.World&&f.Owner==t.Owner&&f.Creation==t.Creation&&f.Entity==t.NativeOwner&&f.Policy==t.Policy&&(f.Deaths==t.Deaths||f.Deaths==t.Deaths-1);}catch{return false;}
    }
    private static string TerminalSourcePath(TerminalBody t)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","terminal-native-source",t.Operation.ToString("N")+".xml");
    private static string TerminalSourceImage(TerminalBody t,string proof)
    {var x=new XElement("placedTerminalNativeSource",new XAttribute("version",1),new XAttribute("operation",t.Operation.ToString("N")),new XAttribute("body",t.Digest),new XAttribute("episode",t.Episode.ToString("N")),new XAttribute("native",proof));x.SetAttributeValue("checksum",Hash(x.ToString(SaveOptions.DisableFormatting)));return x.ToString(SaveOptions.DisableFormatting);}
    private static bool ReadTerminalSource(string path,TerminalBody t,out string proof)
    {proof=null;try{if(!File.Exists(path)||new FileInfo(path).Length>4096)return false;string image=File.ReadAllText(path,new UTF8Encoding(false,true));var x=OwedXml(image);proof=(string)x.Attribute("native");return FullHash(proof)&&TerminalSourceImage(t,proof)==image;}catch{return false;}}
    private static bool CheckpointTerminalSource(EntityPlayer actor,TerminalBody t,out string proof)
    {
        proof=null;try
        {
            if(!TerminalOwner(actor,t)||!TerminalFatalLineage(t)||!TryHostTerminalMirror(actor,t.Owed,t.Trace,out var terminal)||terminal!=t.Terminal)return false;string path=TerminalSourcePath(t),folder=Path.GetDirectoryName(path);if(File.Exists(path)||File.Exists(path+".bak"))return ReadTerminalSource(path,t,out proof);
            Directory.CreateDirectory(folder);var pending=Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").ToArray();if(pending.Length!=0){if(pending.Length!=1||!ReadTerminalSource(pending[0],t,out proof))return false;return RebirthDurableFileCommit.TryPublish(pending[0],path,out _)&&ReadTerminalSource(path,t,out proof);}
            if(MatchNativeBodySnapshot(t,out var snapshot)){proof=Hash("terminal-native-source-snapshot|"+t.Image+"|"+NativeBodySnapshotImage(snapshot));}
            else
            {
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RebirthCamperEnergySavedOwner.TryRead(owner,out var actual,out var file)||file.id!=t.NativeOwner||file.deaths!=t.Deaths||!actual.Matches(RebirthPlacedOriginal.ReadActor(t.Actor))||!SavedEquipment(file).SequenceEqual(Convert.FromBase64String(t.Gear)))return false;
            string digest=Hash(t.Trace);if(t.Owed?!SavedOwedMarkerDigest(file,digest,5):!Marker(file,digest))return false;
            // Historical final primary is read only. Never overwrite a later
            // actor vector or infer loss from inventory/counter absence.
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(t.Image);writer.Write(file.id);writer.Write(file.deaths);foreach(var bytes in new[]{file.bagData.ToArray(),file.inventoryData.ToArray(),file.equipmentData.ToArray(),file.buffData.ToArray()}){writer.Write(bytes.Length);writer.Write(bytes);}writer.Flush();using(var hash=System.Security.Cryptography.SHA256.Create())proof=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}
            }
            string image=TerminalSourceImage(t,proof),temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var data=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(data,0,data.Length);stream.Flush(true);}return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&ReadTerminalSource(path,t,out proof);
        }catch{return false;}
    }
    private static string TerminalOperationPath(Guid operation)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","terminal-native-operations",operation.ToString("N")+".xml");
    private static string TerminalPacketPath(string native)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","terminal-native-packets",Hash(native)+".xml");
    private static string TerminalInvocationPath(TerminalBody t)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","terminal-native-attempts",t.Operation.ToString("N")+".xml");
    private static bool SaveTerminalAt(string path,TerminalBody t,bool replace=false)
    {
        try{if(File.Exists(path)||File.Exists(path+".bak")){if(!ReadTerminalBody(path,out var old))return false;if(old.Image==t.Image)return true;if(!replace)return false;}Directory.CreateDirectory(Path.GetDirectoryName(path));var pending=Directory.EnumerateFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".*.tmp").ToArray();string temp;if(pending.Length!=0){if(pending.Length!=1||!ReadTerminalBody(pending[0],out var staged)||staged.Image!=t.Image)return false;temp=pending[0];}else{temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(t.Image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}}return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&ReadTerminalBody(path,out var saved)&&saved.Image==t.Image;}catch{return false;}
    }
    private static bool ReadTerminalOperation(Guid operation,out TerminalBody t)
    {return ReadTerminalBody(TerminalOperationPath(operation),out t)&&t.Operation==operation;}
    private static bool OwnTerminalParcel(OwedPartition p,TerminalBody t)
    {
        try{if(p.Operation!=t.Operation||p.World!=t.World||p.Owner!=t.Owner||p.Creation!=t.Creation||p.Deaths!=t.Deaths||p.Trace!=t.Digest||!TerminalFatalLineage(t)||!ReadTerminalSource(TerminalSourcePath(t),t,out var source)||p.Native!=source)return false;var rows=t.Inputs.Where(i=>t.Overflow||i.Failed).Select(i=>i.Item).ToArray();return rows.Length>0&&Rows(rows).SequenceEqual(Convert.FromBase64String(p.Owed))&&(t.Native.Length==0||RebirthPlacedOriginalBodyPublication.TryPublished(t.Operation,t.Native,out _));}catch{return false;}
    }
    private static bool TerminalMaterialSettled(TerminalBody t)
    {
        try{if(!ReadTerminalSource(TerminalSourcePath(t),t,out _)||t.Native.Length!=0&&!RebirthPlacedOriginalBodyPublication.TryPublished(t.Operation,t.Native,out _))return false;var rows=t.Inputs.Where(i=>t.Overflow||i.Failed).Select(i=>i.Item).ToArray();if(rows.Length==0)return true;if(!ReadOwed(t.Operation,out var p)||!OwnTerminalParcel(p,t)||!OwedHeadFor(p,out var head))return false;return rows.Length==head.Delivered.Length&&rows.Select((row,index)=>row.count==head.Delivered[index]).All(value=>value);}catch{return false;}
    }
    private static bool PrepareTerminalNativeDeferred(EntityPlayer actor,TerminalBody t)
    {
        try{if(!TerminalOwner(actor,t)||t.Native.Length==0||t.Overflow||!ReadTerminalBody(TerminalBodyMapPath(t.Trace),out var frozen)||frozen.Image!=t.Image||!TerminalFatalLineage(t)||!TryHostTerminalMirror(actor,t.Owed,t.Trace,out var terminal)||terminal!=t.Terminal||!ReadTerminalSource(TerminalSourcePath(t),t,out _))return false;return SaveTerminalAt(TerminalOperationPath(t.Operation),t);}catch{return false;}
    }
    private static bool HasTerminalNativePermit(TerminalBody t)
    {return DeferredRead(DeferredPath(new DeferredBody{Operation=t.Operation},true),out var permit)&&permit.Terminal&&permit.Operation==t.Operation&&permit.Trace==t.Image&&permit.Phase==1;}
    private static bool BeginTerminalNativeInvocation(TerminalBody t)
    {
        try{if(!HasTerminalNativePermit(t)||!ReadTerminalSource(TerminalSourcePath(t),t,out _))return false;string path=TerminalInvocationPath(t),folder=Path.GetDirectoryName(path);return !File.Exists(path)&&!File.Exists(path+".bak")&&(!Directory.Exists(folder)||!Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").Any())&&SaveTerminalAt(path,t);}catch{return false;}
    }
    internal sealed class TerminalNativeBodyDescriptor
    {internal Guid Operation;internal string World,Owner,Creation,Binding;internal int Deaths,NativeOwner;}
    internal static bool TryDescribeTerminalNativeBody(World world,Guid operation,string request,out TerminalNativeBodyDescriptor own)
    {
        own=null;try{if(world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||!ReadTerminalOperation(operation,out var t)||t.World!=world.worldState.Guid||t.Native!=request||!ReadTerminalSource(TerminalSourcePath(t),t,out _)||!ReadTerminalBody(TerminalInvocationPath(t),out var invoked)||invoked.Image!=t.Image)return false;own=new TerminalNativeBodyDescriptor{Operation=t.Operation,World=t.World,Owner=t.Owner,Creation=t.Creation,Binding=t.Digest,Deaths=t.Deaths,NativeOwner=t.NativeOwner};return true;}catch{return false;}
    }
    private sealed class TerminalRequestFrame
    {internal TerminalRequestFrame Previous;internal NetPackageRequestToSpawnEntity Packet;internal World World;internal string Directory;internal ClientInfo Sender;internal TerminalBody Body;}
    [ThreadStatic]private static TerminalRequestFrame terminalIncoming;
    private static object EnterTerminalNativeRequest(NetPackageRequestToSpawnEntity packet,World world,out bool classified,out bool allow)
    {
        classified=false;allow=true;try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||packet?.Sender==null||packet.ecd==null||packet.ecd.entityClass!="Backpack".GetHashCode()||packet.ecd.id< -1)return null;var actor=world.GetEntity(packet.Sender.entityId) as EntityPlayer;if(actor==null||!packet.ValidEntityIdForSender(actor.entityId)||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId),packet.Sender))return null;
            var native=ReadEcd(Convert.ToBase64String(EcdBytes(packet.ecd)));bool fresh=native.id==-1;native.id=-1;string request=Convert.ToBase64String(EcdBytes(native));string path=TerminalPacketPath(request);if(!File.Exists(path)&&!File.Exists(path+".bak"))return null;if(!ReadTerminalBody(path,out var t)||t.Native!=request||!TerminalOwner(actor,t))return null;classified=true;allow=false;if(!fresh||!HasTerminalNativePermit(t)||!BeginTerminalNativeInvocation(t))return null;
            var frame=new TerminalRequestFrame{Previous=terminalIncoming,Packet=packet,World=world,Directory=GameIO.GetSaveGameDir(),Sender=packet.Sender,Body=t};terminalIncoming=frame;allow=true;return frame;
        }catch{if(classified)allow=false;return null;}
    }
    private static bool ObserveTerminalNativeAccepted(EntityBackpack body)
    {
        try{var f=terminalIncoming;return f!=null&&f.Packet?.ecd?.id==body.entityId&&body.entityId>0&&ReferenceEquals(GameManager.Instance?.World,f.World)&&f.Directory==GameIO.GetSaveGameDir()&&ReferenceEquals(f.World.GetEntity(body.entityId),body)&&ReferenceEquals(f.Packet.Sender,f.Sender)&&RebirthPlacedOriginalBodyPublication.AdmitTerminal(f.World,f.Body.Operation,body,f.Body.Native);}catch{return false;}
    }    private static bool RetainTerminalParcel(TerminalBody t,string source)
    {
        try{if(!ReadTerminalSource(TerminalSourcePath(t),t,out var saved)||saved!=source)return false;if(t.Native.Length!=0&&!RebirthPlacedOriginalBodyPublication.TryPublished(t.Operation,t.Native,out _))return false;var rows=t.Inputs.Where(i=>t.Overflow||i.Failed).Select(i=>i.Item.Clone()).ToArray();if(rows.Length==0)return true;var p=DecodeOwed(OwedBody(new OwedPartition{Operation=t.Operation,World=t.World,Owner=t.Owner,Creation=t.Creation,Deaths=t.Deaths,Trace=t.Digest,Body=string.Empty,Native=source,Owed=Convert.ToBase64String(Rows(rows))}));return SaveOwed(p)&&OwnTerminalParcel(p,t);}catch{return false;}
    }
    private static bool ProgressTerminalNativeHost(TerminalBody t,out string settlement)
    {
        settlement=null;try{if(!ReadTerminalSource(TerminalSourcePath(t),t,out var source)||!TerminalFatalLineage(t)||!SaveTerminalAt(TerminalOperationPath(t.Operation),t))return false;string body=string.Empty;if(t.Native.Length!=0&&(!ReadTerminalBody(TerminalInvocationPath(t),out var attempt)||attempt.Image!=t.Image||!RebirthPlacedOriginalBodyPublication.TryPublished(t.Operation,t.Native,out body)))return false;if(!RetainTerminalParcel(t,source))return false;string head=string.Empty;if(t.Inputs.Any(i=>t.Overflow||i.Failed)){if(!ReadOwed(t.Operation,out var parcel)||!OwnTerminalParcel(parcel,t)||!OwedHeadFor(parcel,out var value)||!TerminalMaterialSettled(t))return false;head=HeadBody(value);}settlement=Hash(t.Image+"|"+source+"|"+body+"|"+head);return true;}catch{return false;}
    }}