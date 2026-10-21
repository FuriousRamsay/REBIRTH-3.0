using Platform;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Authenticated command identity BEFORE native hold. No slot image, payment,
// refund, source permission or synthetic native Before is carried here.
internal static class RebirthLogisticsHeaderAdmission
{
    internal enum RequestMode:byte { Prepare,Query,Cancel }
    private sealed class Header
    {
        internal RebirthLogisticsNativeBodyIntent.Command Command;internal int Entity,State,BodyIndex;internal string Nonce;internal INetConnection FreshConnection;
    }
    private const int MaximumBytes=65536;
    private static World ownerWorld;private static Header pending;private static DateTime next;private static bool quarantined;
    private static XElement CommandXml(RebirthLogisticsNativeBodyIntent.Command c)=>new XElement("command",new XAttribute("operation",c.Operation.ToString("N")),new XAttribute("world",c.World),new XAttribute("owner",c.Owner),new XAttribute("creation",c.Creation),new XAttribute("deaths",c.Deaths),new XAttribute("epoch",c.Epoch),new XAttribute("request",c.Request),new XAttribute("action",(byte)c.Action),new XElement("target",Convert.ToBase64String(Encoding.UTF8.GetBytes(c.Target))),new XElement("selection",Convert.ToBase64String(Encoding.UTF8.GetBytes(c.Selection))));
    private static bool Hex(string text,int length)=>text?.Length==length&&text.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    private static RebirthLogisticsNativeBodyIntent.Command ParseCommand(XElement root)
    {
        if(root?.Name!="command"||root.Attributes().Count()!=8||root.Elements().Count()!=2||root.Element("target")==null||root.Element("selection")==null||root.Elements().Any(e=>e.HasElements))throw new InvalidDataException();
        var c=new RebirthLogisticsNativeBodyIntent.Command(Guid.ParseExact((string)root.Attribute("operation"),"N"),(string)root.Attribute("world"),(string)root.Attribute("owner"),(string)root.Attribute("creation"),(int)root.Attribute("deaths"),(ulong)root.Attribute("epoch"),(int)root.Attribute("request"),(QuickStackRadialAction)checked((byte)(int)root.Attribute("action")),new UTF8Encoding(false,true).GetString(Convert.FromBase64String(root.Element("target").Value)),new UTF8Encoding(false,true).GetString(Convert.FromBase64String(root.Element("selection").Value)));
        if(c.Operation==Guid.Empty||!Hex(c.World,32)||!Hex(c.Owner,64)||!RebirthOriginalActionScope.Validate(c.Owner,c.Creation)||c.Deaths<0||c.Deaths==int.MaxValue||c.Epoch==0||c.Request<1||(byte)c.Action>(byte)QuickStackRadialAction.PullDroneCredits||c.Target.Length>512||c.Selection.Length>8192||!XNode.DeepEquals(root,CommandXml(c)))throw new InvalidDataException();return c;
    }
    private static XElement Write(Header h)=>new XElement("logisticsHeader",new XAttribute("version",h.BodyIndex==0?1:2),new XAttribute("state",h.State),new XAttribute("entity",h.Entity),new XAttribute("nonce",h.Nonce),new XAttribute("binding",h.Command.Binding),h.BodyIndex==0?null:new XAttribute("bodyIndex",h.BodyIndex),CommandXml(h.Command));
    private static Header Parse(string text)
    {
        using(var input=new StringReader(text))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))
        {
            var root=XElement.Load(reader);if(root.Name!="logisticsHeader"||root.Attributes().Count()!=((string)root.Attribute("version")=="2"?6:5)||root.Elements().Count()!=1||((string)root.Attribute("version")!="1"&&(string)root.Attribute("version")!="2"))throw new InvalidDataException();
            var h=new Header{Command=ParseCommand(root.Element("command")),Entity=(int)root.Attribute("entity"),State=(int)root.Attribute("state"),Nonce=(string)root.Attribute("nonce"),BodyIndex=(int?)root.Attribute("bodyIndex")??0};
            if(h.BodyIndex<0||h.BodyIndex>2||((string)root.Attribute("version")=="2"&&h.BodyIndex==0)||h.Entity<0||h.State<0||h.State>3||!Guid.TryParseExact(h.Nonce,"N",out var nonce)||nonce==Guid.Empty||h.Nonce!=nonce.ToString("N")||(string)root.Attribute("binding")!=h.Command.Binding||!XNode.DeepEquals(root,Write(h)))throw new InvalidDataException();return h;
        }
    }
    private static string ReadText(string path)
    {
        var file=new FileInfo(path);if(!file.Exists||file.Length<1||file.Length>MaximumBytes||(file.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))return XElement.Load(reader).ToString(SaveOptions.DisableFormatting);
    }
    private static string HostPath(string owner,Guid operation)=>Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsHeaders",owner,operation.ToString("N")+".xml");
    private static string Archive(string path)=>Path.Combine(Path.GetDirectoryName(path),"archive",Path.GetFileName(path));
    private static Header ReadHost(string owner,Guid operation)
    {
        string path=HostPath(owner,operation),archive=Archive(path);Header value=null;
        foreach(string candidate in new[]{path,archive})
        {
            if(candidate==path&&File.Exists(candidate)&&File.Exists(candidate+".tmp")&&!File.Exists(candidate+".bak"))
            {
                var before=Parse(ReadText(candidate));var after=Parse(ReadText(candidate+".tmp"));
                if(before.BodyIndex==1&&after.BodyIndex==2&&before.State==after.State&&before.Entity==after.Entity&&before.Nonce==after.Nonce&&before.Command.Binding==after.Command.Binding&&
                    RebirthLogisticsBodyAdmissionIndex.Ready(after.Command,after.Nonce))
                {after.BodyIndex=1;if(!XNode.DeepEquals(Write(before),Write(after)))throw new InvalidDataException();File.Replace(candidate+".tmp",candidate,null);}
            }
            if(File.Exists(candidate+".tmp")||File.Exists(candidate+".bak"))throw new InvalidDataException();if(!File.Exists(candidate))continue;
            var h=Parse(ReadText(candidate));if(h.Command.Owner!=owner||h.Command.Operation!=operation||value!=null&&!XNode.DeepEquals(Write(value),Write(h)))throw new InvalidDataException();value=h;
        }
        return value;
    }
    private static bool SaveHost(Header h,bool replace)
    {
        string path=HostPath(h.Command.Owner,h.Command.Operation);if(File.Exists(Archive(path))||File.Exists(path+".tmp")||File.Exists(path+".bak"))return false;
        string text=Write(h).ToString(SaveOptions.DisableFormatting);var bytes=Encoding.UTF8.GetBytes(text);if(bytes.Length>MaximumBytes)return false;
        Directory.CreateDirectory(Path.GetDirectoryName(path));using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        if(replace)File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);return XNode.DeepEquals(Write(h),Write(ReadHost(h.Command.Owner,h.Command.Operation)));
    }
    private static bool HostBudget(string owner,int incoming)
    {
        string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsHeaders");if(!Directory.Exists(root))return true;int total=0,own=0,directories=0;long bytes=0;
        foreach(string directory in Directory.EnumerateDirectories(root))
        {
            string nativeOwner=Path.GetFileName(directory);if(++directories>4096||!Hex(nativeOwner,64))return false;
            foreach(string path in Directory.EnumerateFiles(directory,"*",SearchOption.TopDirectoryOnly))
            {
                if(++total>4096)return false;var h=Parse(ReadText(path));if(Path.GetFileName(path)!=h.Command.Operation.ToString("N")+".xml"||h.Command.Owner!=nativeOwner)return false;
                bool terminal=h.State>=2||RebirthLogisticsTransferJournal.TryRead(nativeOwner,h.Command.Operation,out var original)&&original.Phase==4&&RebirthLogisticsNativeBodyIntent.Command.From(original).Binding==h.Command.Binding;
                if(terminal){string archive=Archive(path);if(File.Exists(archive))return false;Directory.CreateDirectory(Path.GetDirectoryName(archive));File.Move(path,archive);continue;}
                if(nativeOwner==owner&&++own>=128)return false;bytes+=new FileInfo(path).Length;if(bytes+incoming>33554432)return false;
            }
        }
        return total<4096&&bytes+incoming<=33554432;
    }
    private static void RecoverPreparedHeader(EntityPlayer actor,RebirthLogisticsNativeBodyIntent.Command command)
    {
        string path=HostPath(command.Owner,command.Operation),temp=path+".tmp";
        if(File.Exists(path)||File.Exists(Archive(path))||File.Exists(path+".bak")||!File.Exists(temp))return;
        var h=Parse(ReadText(temp));
        // Only an initial never-Admitting metadata write can be recovered.
        // Any attempted transition/old incarnation remains unknown.
        if((h.State!=0&&h.State!=2)||h.Entity!=actor.entityId||h.Command.Binding!=command.Binding||actor.IsDead()||actor.Died!=command.Deaths||command.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor))return;
        File.Move(temp,path);
    }
    internal static void Receive(ClientInfo sender,string commandText,RequestMode mode)
    {
        try
        {
            var world=GameManager.Instance?.World;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(!RebirthLogisticsOriginalTransport.Active||!ThreadManager.IsMainThread()||world?.IsRemote()!=false||sender==null||!ReferenceEquals(manager?.Clients.ForEntityId(sender.entityId),sender)||(byte)mode>2||!RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner))return;
            if(commandText==null||commandText.Length>MaximumBytes||Encoding.UTF8.GetByteCount(commandText)>MaximumBytes)return;
            var actor=world.GetEntity(sender.entityId) as EntityPlayer;var c=ParseCommand(XElement.Parse(commandText));
            if(!ReferenceEquals(actor?.world,world)||c.Owner!=owner.StorageKey||c.World!=RebirthLogisticsTransferJournal.WorldKey(world))return;
            RecoverPreparedHeader(actor,c);var h=ReadHost(c.Owner,c.Operation);
            if(h==null)
            {
                if(mode==RequestMode.Query||actor.IsDead()||actor.Died!=c.Deaths||c.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||RebirthLogisticsTransferJournal.TryRead(c.Owner,c.Operation,out _))return;
                h=new Header{Command=c,Entity=actor.entityId,Nonce=Guid.NewGuid().ToString("N"),State=mode==RequestMode.Cancel?2:0,BodyIndex=mode==RequestMode.Cancel?0:1};
                if(!HostBudget(c.Owner,Encoding.UTF8.GetByteCount(Write(h).ToString(SaveOptions.DisableFormatting)))||!SaveHost(h,false))return;
            }
            if(h.Command.Binding!=c.Binding)return;
            if(h.BodyIndex==1)
            {
                // Persisted Initializing cannot ACK or arm an owner hold.
                if(!RebirthLogisticsBodyAdmissionIndex.Initialize(c,h.Nonce))return;
                h.BodyIndex=2;if(!SaveHost(h,true))return;
            }
            if(h.BodyIndex==2&&!RebirthLogisticsBodyAdmissionIndex.Ready(c,h.Nonce))return;
            // Legacy headers are read for recovery, never newly ACKed as a
            // mandatory-index protocol from absence of old body evidence.
            if(h.BodyIndex==0&&h.State<2)return;
            if(mode==RequestMode.Cancel&&h.State==0){h.State=2;if(!SaveHost(h,true))return;}
            if(!RebirthLogisticsOriginalTransport.TryCurrentPeer(actor,out var peer,out var native)||!ReferenceEquals(peer,sender)||!RebirthLogisticsOriginalTransport.CanSendNative(native))return;
            manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthLogisticsHeaderAck>().Setup(c.Operation,c.Binding,h.Nonce,h.State,h.Entity),_attachedToEntityId:actor.entityId);
        }
        catch{/* Unknown header metadata does not grant a native hold/effect. */}
    }
    internal static bool TryBodyIndexHeader(RebirthLogisticsTransferJournal.Record original,out string nonce,out bool required)
    {
        nonce=null;required=false;
        try
        {
            var h=ReadHost(original.Owner,original.Operation);
            if(h==null||h.BodyIndex==1||h.Command.Binding!=RebirthLogisticsNativeBodyIntent.Command.From(original).Binding)return false;
            required=h.BodyIndex==2;nonce=h.Nonce;
            return !required||RebirthLogisticsBodyAdmissionIndex.Ready(h.Command,h.Nonce);
        }catch{return false;}
    }
    internal static bool ValidateNativeBefore(EntityPlayer actor,RebirthLogisticsTransferJournal.Record original,PlayerDataFile saved)
    {
        try
        {
            var h=ReadHost(original.Owner,original.Operation);
            return h!=null&&(h.BodyIndex==0||h.BodyIndex==2&&RebirthLogisticsBodyAdmissionIndex.Ready(h.Command,h.Nonce))&&h.State<=1&&h.Entity==actor.entityId&&!saved.bDead&&saved.id==h.Entity&&saved.deaths==original.Deaths&&h.Command.Binding==RebirthLogisticsNativeBodyIntent.Command.From(original).Binding&&RebirthLogisticsOwnerReceipt.TryReadDetachedHeader(saved,out var nonce)&&nonce==h.Nonce&&RebirthLogisticsOwnerReceipt.TryReadDetachedOperation(saved,out var operation,out int phase,out float held)&&operation==original.Operation&&phase==0&&held==1f;
        }
        catch{return false;}
    }
    // Read-only terminal upload fence identity; never creates/rearms a header.
    internal static bool TryAdmittedTerminalNonce(EntityPlayer actor,RebirthLogisticsTransferJournal.Record original,out string nonce)
    {
        nonce=null;
        try
        {
            if(original?.Phase!=4||actor==null||GameManager.Instance?.World?.IsRemote()!=false)return false;
            var h=ReadHost(original.Owner,original.Operation);
            if(h==null||h.State!=1||h.Entity!=actor.entityId||h.Command.Binding!=RebirthLogisticsNativeBodyIntent.Command.From(original).Binding)return false;
            nonce=h.Nonce;return true;
        }
        catch{return false;}
    }
    internal static string EncodeHistoricalCommand(RebirthLogisticsNativeBodyIntent.Command command)=>CommandXml(command).ToString(SaveOptions.DisableFormatting);
    internal static bool TryHistoricalHeader(EntityPlayer actor,Guid operation,string nonce,out RebirthLogisticsNativeBodyIntent.Command command,out int entity)
    {
        command=null;entity=-1;
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return false;
            var h=ReadHost(owner.StorageKey,operation);
            if(h==null||(h.State!=0&&h.State!=3)||h.Nonce!=nonce||h.Entity!=actor.entityId||h.Command.World!=RebirthLogisticsTransferJournal.WorldKey(actor.world)||h.Command.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||actor.Died<h.Command.Deaths||actor.Died>h.Command.Deaths+1)return false;
            command=h.Command;entity=h.Entity;return true;
        }
        catch{return false;}
    }
    internal static bool TryHistoricalNonce(EntityPlayer actor,Guid operation,out string nonce)
    {
        nonce=null;try{if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return false;var h=ReadHost(owner.StorageKey,operation);if(h==null||!TryHistoricalHeader(actor,operation,h.Nonce,out _,out _))return false;nonce=h.Nonce;return true;}catch{return false;}
    }
    internal static bool EndHistoricalHeader(EntityPlayer actor,Guid operation,string nonce)
    {
        if(!TryHistoricalHeader(actor,operation,nonce,out var c,out _))return false;
        var h=ReadHost(c.Owner,operation);if(h.State==3)return true;h.State=3;return SaveHost(h,true);
    }
    internal static bool BeginAdmission(EntityPlayer actor,RebirthLogisticsTransferJournal.Record original)
    {
        try
        {
            var h=ReadHost(original.Owner,original.Operation);if(RebirthLogisticsNativeDeathReceipt.HasHistoricalFatalCandidate(original.Owner,original.Operation)||h==null||h.BodyIndex==1||h.BodyIndex==2&&!RebirthLogisticsBodyAdmissionIndex.Ready(h.Command,h.Nonce)||h.State>1||h.Command.Binding!=RebirthLogisticsNativeBodyIntent.Command.From(original).Binding||h.Entity!=actor.entityId||actor.Died!=original.Deaths||actor.IsDead()||!RebirthOriginalActionScope.Matches(actor,original.Owner,original.Creation))return false;
            if(h.State==1)return true;h.State=1;return SaveHost(h,true);
        }
        catch{return false;}
    }
    internal static void OnNativeFatal(EntityPlayer actor)
    {
        try
        {
            var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(actor?.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||!actor.IsDead()||actor.Health>0||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return;
            string directory=Path.GetDirectoryName(HostPath(owner.StorageKey,Guid.NewGuid()));if(!Directory.Exists(directory))return;int count=0;
            foreach(string path in Directory.EnumerateFiles(directory,"*.xml",SearchOption.TopDirectoryOnly))
            {
                if(++count>128)return;var h=ReadHost(owner.StorageKey,Guid.ParseExact(Path.GetFileNameWithoutExtension(path),"N"));
                if(h==null||h.State!=0||h.Entity!=actor.entityId||h.Command.World!=RebirthLogisticsTransferJournal.WorldKey(world)||h.Command.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||actor.Died<h.Command.Deaths||actor.Died>h.Command.Deaths+1)continue;
                // A delayed second Prepare is harmless only when this SAME
                // old-life native primary already contains our accepted nonce.
                // Build no receipt from a later command or a missing upload.
                var c=h.Command;var original=new RebirthLogisticsTransferJournal.Record{Operation=c.Operation,World=c.World,Owner=c.Owner,Creation=c.Creation,Deaths=c.Deaths,Epoch=c.Epoch,Request=c.Request,Action=c.Action,Target=c.Target,Selection=c.Selection};
                if(RebirthLogisticsOwnerReceipt.TrySaved(owner,c.Operation,0,out var saved)&&ValidateNativeBefore(actor,original,saved))RebirthLogisticsPreAdmissionReceipt.Prepare(original,saved);
                RebirthLogisticsNativeDeathReceipt.CaptureHistoricalHeaderFatal(c,h.Nonce,actor);
                // Ended metadata itself is never native Before/payment authority.
                h.State=3;if(SaveHost(h,true))RebirthLogisticsHistoricalBeforeCapsule.TryAdopt(actor,c.Operation,h.Nonce);
            }
        }
        catch{/* Native fatal always proceeds. */}
    }
    private static string OwnerDirectory(RebirthLogisticsNativeBodyIntent.Command c)=>Path.Combine(GameIO.GetPlayerDataDir(),"RebirthLogisticsHeaderAdmission",c.World,c.Owner);
    private static string OwnerPath(Header h)=>Path.Combine(OwnerDirectory(h.Command),h.Command.Operation.ToString("N"),"header.xml");
    private static string Marker(Header h,string kind)=>new XElement("ownerHeader",new XAttribute("version",1),new XAttribute("kind",kind),new XAttribute("nonce",h.Nonce??string.Empty),new XAttribute("entity",h.Entity),CommandXml(h.Command)).ToString(SaveOptions.DisableFormatting);
    private static bool WriteOnce(string path,string text)
    {
        if(File.Exists(path+".bak")||File.Exists(path)&&ReadText(path)!=text||File.Exists(path+".tmp")&&ReadText(path+".tmp")!=text)return false;
        if(File.Exists(path)){if(File.Exists(path+".tmp"))File.Delete(path+".tmp");return true;}Directory.CreateDirectory(Path.GetDirectoryName(path));if(!File.Exists(path+".tmp"))using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){byte[] bytes=Encoding.UTF8.GetBytes(text);if(bytes.Length>MaximumBytes)return false;stream.Write(bytes,0,bytes.Length);stream.Flush(true);}File.Move(path+".tmp",path);return ReadText(path)==text;
    }
    private static bool HasMarker(Header h,string kind)
    {
        string path=OwnerPath(h)+"."+kind;
        if(kind=="cancel"&&File.Exists(path+".tmp")&&!File.Exists(path+".bak")&&ReadText(path+".tmp")==Marker(h,kind))
        {if(!WriteOnce(path,Marker(h,kind)))throw new InvalidDataException();}
        if(File.Exists(path+".tmp")||File.Exists(path+".bak"))throw new InvalidDataException();if(!File.Exists(path))return false;if(ReadText(path)!=Marker(h,kind))throw new InvalidDataException();return true;
    }
    private static Header ReadAttempted(Header original,string originalPath=null)
    {
        string source=originalPath??OwnerPath(original),path=source+".holdAttempted";if(File.Exists(path+".tmp")||File.Exists(path+".bak"))throw new InvalidDataException();
        var root=XElement.Parse(ReadText(path));string nonce=(string)root.Attribute("nonce");if(!Guid.TryParseExact(nonce,"N",out var id)||id==Guid.Empty||nonce!=id.ToString("N"))throw new InvalidDataException();
        var h=new Header{Command=original.Command,Entity=original.Entity,Nonce=nonce};if(ReadText(path)!=Marker(h,"holdAttempted"))throw new InvalidDataException();
        string accepted=source+".accepted";if(File.Exists(accepted+".tmp")||File.Exists(accepted+".bak")||File.Exists(accepted)&&ReadText(accepted)!=Marker(h,"accepted"))throw new InvalidDataException();return h;
    }
    private static Header ReadCompletionIdentity(Header original,string originalPath=null)
    {
        string source=originalPath??OwnerPath(original),attempted=source+".holdAttempted";
        if(File.Exists(attempted)||File.Exists(attempted+".tmp")||File.Exists(attempted+".bak"))return ReadAttempted(original,source);
        string path=source+".accepted";if(File.Exists(path+".tmp")||File.Exists(path+".bak"))throw new InvalidDataException();var root=XElement.Parse(ReadText(path));string nonce=(string)root.Attribute("nonce");
        if(!Guid.TryParseExact(nonce,"N",out var id)||id==Guid.Empty||nonce!=id.ToString("N"))throw new InvalidDataException();var h=new Header{Command=original.Command,Entity=original.Entity,Nonce=nonce};if(ReadText(path)!=Marker(h,"accepted"))throw new InvalidDataException();return h;
    }
    private static string DoneMarker(Header h,int state,string proof)
    {
        var root=XElement.Parse(Marker(h,"done"));root.Add(new XAttribute("terminal",state),new XAttribute("proof",proof));return root.ToString(SaveOptions.DisableFormatting);
    }
    private static bool ReadDone(Header h)
    {
        string path=OwnerPath(h)+".done";if(File.Exists(path+".bak"))throw new InvalidDataException();if(!File.Exists(path)&&!File.Exists(path+".tmp"))return false;
        string text=ReadText(File.Exists(path)?path:path+".tmp");var root=XElement.Parse(text);int state=(int)root.Attribute("terminal");string proof=(string)root.Attribute("proof");
        if((state!=2&&state!=3&&state!=4)||state<4&&(!Guid.TryParseExact(proof,"N",out var nonce)||nonce==Guid.Empty||nonce.ToString("N")!=proof||h.Nonce.Length!=0&&h.Nonce!=proof||HasMarker(h,"holdAttempted"))||state==4&&(!Hex(proof,64)||ReadCompletionIdentity(h).Nonce!=h.Nonce)||text!=DoneMarker(h,state,proof)||!WriteOnce(path,text))throw new InvalidDataException();return true;
    }
    private static bool OwnerIdentity(EntityPlayerLocal player,Header h)=>player?.world?.IsRemote()==true&&ReferenceEquals(GameManager.Instance?.World,player.world)&&ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&h.Command.World==RebirthLogisticsTransferJournal.WorldKey(player.world)&&RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)&&owner.StorageKey==h.Command.Owner;
    private static bool OwnerCurrent(EntityPlayerLocal player,Header h)=>OwnerIdentity(player,h)&&h.Command.Creation==RebirthCamperCharacterAccess.CurrentCreation(player);
    internal static void ResetWorld(World world)
    {
        RebirthLogisticsHistoricalBeforeCapsule.ResetWorld(world);
        if(!ThreadManager.IsMainThread()||ReferenceEquals(ownerWorld,world))return;
        ownerWorld=world;pending=null;quarantined=false;next=DateTime.MinValue;
        // Only volatile pointers are released; all original sidecars remain.
    }
    internal static bool HasPending=>pending!=null||quarantined;
    internal static Guid PendingOperation=>pending?.Command.Operation??Guid.Empty;
    internal static bool Start(EntityPlayerLocal player,RebirthLogisticsNativeBodyIntent.Command command)
    {
        try
        {
            if(!ThreadManager.IsMainThread())return false;Tick(player);if(HasPending||player.IsDead()||player.Died!=command.Deaths)return false;
            var h=new Header{Command=command,Entity=player.entityId,Nonce=string.Empty};if(!OwnerCurrent(player,h)||!WriteOnce(OwnerPath(h),Marker(h,"intent")))return false;
            pending=h;ownerWorld=player.world;next=DateTime.MinValue;return true;
        }
        catch{quarantined=true;return false;}
    }
    private static bool Load(EntityPlayerLocal player)
    {
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner))return false;
        string root=Path.Combine(GameIO.GetPlayerDataDir(),"RebirthLogisticsHeaderAdmission",RebirthLogisticsTransferJournal.WorldKey(player.world),owner.StorageKey);if(!Directory.Exists(root))return true;int count=0;
        foreach(string directory in Directory.EnumerateDirectories(root))
        {
            if(Path.GetFileName(directory)=="archive")continue;if(++count>128)throw new InvalidDataException();
            string path=Path.Combine(directory,"header.xml");var xml=XElement.Parse(ReadText(File.Exists(path)?path:path+".tmp"));var c=ParseCommand(xml.Element("command"));var h=new Header{Command=c,Entity=(int)xml.Attribute("entity"),Nonce=string.Empty};
            if(h.Entity<0||Path.GetFileName(directory)!=c.Operation.ToString("N")||!OwnerIdentity(player,h)||!WriteOnce(path,Marker(h,"intent")))throw new InvalidDataException();
            string accepted=path+".accepted";
            if(File.Exists(accepted)||File.Exists(accepted+".tmp"))
            {var a=XElement.Parse(ReadText(File.Exists(accepted)?accepted:accepted+".tmp"));h.Nonce=(string)a.Attribute("nonce");if(!Guid.TryParseExact(h.Nonce,"N",out var n)||n==Guid.Empty||h.Nonce!=n.ToString("N")||!WriteOnce(accepted,Marker(h,"accepted")))throw new InvalidDataException();}
            if(File.Exists(accepted+".tmp")||File.Exists(accepted+".bak"))throw new InvalidDataException();
            if(h.Nonce.Length==0&&(File.Exists(path+".done")||File.Exists(path+".done.tmp")))
            {string done=File.Exists(path+".done")?path+".done":path+".done.tmp";if((int)XElement.Parse(ReadText(done)).Attribute("terminal")==4)h=ReadCompletionIdentity(h);}
            if(ReadDone(h)){ArchiveOwner(h);continue;}
            if(pending!=null)throw new InvalidDataException();pending=h;
        }
        return true;
    }
    private static void ArchiveOwner(Header h)
    {
        string source=Path.GetDirectoryName(OwnerPath(h)),destination=Path.Combine(OwnerDirectory(h.Command),"archive",h.Command.Operation.ToString("N"));if(Directory.Exists(destination))throw new InvalidDataException();
        Directory.CreateDirectory(Path.GetDirectoryName(destination));Directory.Move(source,destination);RebirthLogisticsHistoricalBeforeCapsule.ForgetTransport(h.Command.Operation);
    }
    private static void Finish(Header h,int state,string proof)
    {if(!WriteOnce(OwnerPath(h)+".done",DoneMarker(h,state,proof)))return;ArchiveOwner(h);if(ReferenceEquals(pending,h))pending=null;}
    internal static bool CancelBeforeHold(EntityPlayerLocal player)
    {
        try{if(pending==null||!OwnerCurrent(player,pending)||HasMarker(pending,"holdAttempted"))return false;return WriteOnce(OwnerPath(pending)+".cancel",Marker(pending,"cancel"));}
        catch{quarantined=true;return true;}
    }
    internal static void Tick(EntityPlayerLocal player)
    {
        if(!ThreadManager.IsMainThread()||player?.world?.IsRemote()!=true)return;
        ResetWorld(player.world);
        try
        {
            if(quarantined)return;if(pending==null&&!Load(player))return;var h=pending;if(h==null||!OwnerIdentity(player,h))return;
            if(RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var existing,out _)&&existing==h.Command.Operation){RebirthLogisticsHistoricalBeforeCapsule.OwnerTick(player,h.Command,h.Nonce);return;} // Never re-Begin an already native-bound GUID, even if a local sidecar was lost.
            bool attempted=HasMarker(h,"holdAttempted"),cancel=HasMarker(h,"cancel");
            if(!attempted&&(!OwnerCurrent(player,h)||h.Entity!=player.entityId||player.Died!=h.Command.Deaths))
            {if(!WriteOnce(OwnerPath(h)+".cancel",Marker(h,"cancel")))return;cancel=true;}
            if(attempted&&RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var actual,out _)&&actual==h.Command.Operation)return;
            if(attempted)return; // Unknown native Begin outcome is never replay permission.
            if(!cancel&&h.FreshConnection!=null&&RebirthLogisticsOriginalTransport.TryOwnerConnection(out _,out var currentConnection)&&ReferenceEquals(h.FreshConnection,currentConnection)&&RebirthLogisticsOriginalTransport.CanSendNative(h.FreshConnection)&&!string.IsNullOrEmpty(h.Nonce)&&HasMarker(h,"accepted")&&h.Entity==player.entityId&&player.IsSpawned()&&!player.IsDead()&&player.Died==h.Command.Deaths&&RebirthLogisticsOwnerReceipt.CanBegin(player,h.Command.Operation))
            {
                if(!WriteOnce(OwnerPath(h)+".holdAttempted",Marker(h,"holdAttempted")))return;
                RebirthLogisticsOwnerReceipt.Begin(player,h.Command.Operation,h.Nonce);RebirthLogisticsHistoricalBeforeCapsule.OwnerTick(player,h.Command,h.Nonce);return;
            }
            if(DateTime.UtcNow<next)return;next=DateTime.UtcNow.AddSeconds(3);
            var persistent=GameManager.Instance.GetPersistentLocalPlayer();if(persistent?.PrimaryId==null||!RebirthLogisticsOriginalTransport.TryOwnerConnection(out var manager,out var connection)||!RebirthLogisticsOriginalTransport.CanSendNative(connection))return;
            try{manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthLogisticsHeaderRequest>().Setup(player.entityId,persistent.PrimaryId,CommandXml(h.Command).ToString(SaveOptions.DisableFormatting),cancel?RequestMode.Cancel:RequestMode.Prepare),false);}
            catch{/* Send may have been accepted. Retry this same header only. */}
        }
        catch{quarantined=true;}
    }
    internal static void Ack(World world,Guid operation,string binding,string nonce,int state,int entity)
    {
        try
        {
            var player=world?.GetPrimaryPlayer();var h=pending;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(ownerWorld,world)||h==null||h.Command.Operation!=operation||h.Command.Binding!=binding||!OwnerIdentity(player,h)||state<0||state>3||!Guid.TryParseExact(nonce,"N",out var id)||id==Guid.Empty||nonce!=id.ToString("N")||h.Nonce.Length!=0&&h.Nonce!=nonce)return;
            if(state==2||state==3){if(!HasMarker(h,"holdAttempted"))Finish(h,state,nonce);return;}
            if(state!=0||!OwnerCurrent(player,h)||entity!=h.Entity||entity!=player.entityId||HasMarker(h,"cancel"))return;
            var accepted=new Header{Command=h.Command,Entity=h.Entity,Nonce=nonce};if(!WriteOnce(OwnerPath(h)+".accepted",Marker(accepted,"accepted"))||!RebirthLogisticsOriginalTransport.TryOwnerConnection(out _,out var connection))return;h.Nonce=nonce;h.FreshConnection=connection;next=DateTime.MinValue;
        }
        catch{quarantined=true;}
    }
    internal static bool TryAcceptedNonce(EntityPlayerLocal player,RebirthLogisticsTransferJournal.Record original,out string nonce)
    {
        nonce=null;
        try
        {
            var c=RebirthLogisticsNativeBodyIntent.Command.From(original);var probe=new Header{Command=c,Nonce=string.Empty};if(!OwnerCurrent(player,probe))return false;
            string path=OwnerPath(probe),archive=Path.Combine(OwnerDirectory(c),"archive",c.Operation.ToString("N"),"header.xml");
            if(File.Exists(path)&&File.Exists(archive))return false;if(!File.Exists(path))path=archive;
            if(File.Exists(path+".tmp")||File.Exists(path+".bak"))return false;var root=XElement.Parse(ReadText(path));probe.Entity=(int)root.Attribute("entity");
            if(probe.Entity<0||ReadText(path)!=Marker(probe,"intent"))return false;var identity=ReadCompletionIdentity(probe,path);nonce=identity.Nonce;return true;
        }
        catch{return false;}
    }
    internal static bool CanQueryCompleted(EntityPlayerLocal player,Guid operation)
    {
        try
        {
            var h=pending;if(h==null||h.Command.Operation!=operation||!OwnerCurrent(player,h)||RebirthLogisticsOwnerReceipt.HasPendingOriginal(player)||player.Buffs.GetCustomVar("rbLogisticsOwnerHeld")!=0f||!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var actual,out int phase)||actual!=operation||(phase!=4&&phase!=5))return false;
            var identity=ReadCompletionIdentity(h);return RebirthLogisticsOwnerReceipt.TryLocalHeader(player,out var nonce)&&nonce==identity.Nonce; // Query allocation/correlation only.
        }
        catch{return false;}
    }
    internal static bool Complete(EntityPlayerLocal player,RebirthLogisticsTransferJournal.Record original)
    {
        try
        {
            var h=pending;if(h==null||!OwnerCurrent(player,h)||original.Phase!=4||RebirthLogisticsNativeBodyIntent.Command.From(original).Binding!=h.Command.Binding||RebirthLogisticsOwnerReceipt.HasPendingOriginal(player)||player.Buffs.GetCustomVar("rbLogisticsOwnerHeld")!=0f||!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var actual,out int phase)||actual!=original.Operation||(phase!=4&&(phase!=5||original.Outcome!=3||string.IsNullOrEmpty(original.OwnerNoEffectProof))))return false;
            var completed=ReadCompletionIdentity(h);if(!RebirthLogisticsOwnerReceipt.TryLocalHeader(player,out var nonce)||nonce!=completed.Nonce)return false;Finish(completed,4,RebirthLogisticsOriginalTransport.Digest(RebirthLogisticsTransferJournal.Encode(original)));
            if(!Directory.Exists(Path.GetDirectoryName(OwnerPath(completed)))){pending=null;quarantined=false;return true;}return false;
        }
        catch{quarantined=true;return false;}
    }

}

public sealed class NetPackageRebirthLogisticsHeaderRequest:NetPackage
{
    private int entity;private PlatformUserIdentifierAbs user;private string text;private RebirthLogisticsHeaderAdmission.RequestMode mode;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthLogisticsHeaderRequest Setup(int actor,PlatformUserIdentifierAbs identity,string command,RebirthLogisticsHeaderAdmission.RequestMode request){entity=actor;user=identity;text=command;mode=request;return this;}
    public override void read(PooledBinaryReader reader){if(reader.ReadByte()!=1)throw new InvalidDataException();entity=reader.ReadInt32();user=PlatformUserIdentifierAbs.FromStream(reader);mode=(RebirthLogisticsHeaderAdmission.RequestMode)reader.ReadByte();text=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,65536);}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write((byte)1);writer.Write(entity);user.ToStream(writer);writer.Write((byte)mode);RebirthSurvivorNetworkCodec.WriteString(writer,text,65536);}
    public override void ProcessPackage(World world,GameManager callbacks){if(RebirthLogisticsOriginalTransport.AcceptPacket(this,world,callbacks,true)&&ValidEntityIdForSender(entity)&&ValidUserIdForSender(user))RebirthLogisticsHeaderAdmission.Receive(Sender,text,mode);}
    public int GetLength()=>180+2*(text?.Length??0);
}
public sealed class NetPackageRebirthLogisticsHeaderAck:NetPackage
{
    private Guid operation;private string binding,nonce;private int state,entity;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthLogisticsHeaderAck Setup(Guid original,string hash,string acceptance,int phase,int nativeEntity){operation=original;binding=hash;nonce=acceptance;state=phase;entity=nativeEntity;return this;}
    public override void read(PooledBinaryReader reader){if(reader.ReadByte()!=1||!Guid.TryParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32),"N",out operation))throw new InvalidDataException();binding=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);nonce=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32);state=reader.ReadByte();entity=reader.ReadInt32();}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write((byte)1);RebirthSurvivorNetworkCodec.WriteString(writer,operation.ToString("N"),32);RebirthSurvivorNetworkCodec.WriteString(writer,binding,64);RebirthSurvivorNetworkCodec.WriteString(writer,nonce,32);writer.Write((byte)state);writer.Write(entity);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(RebirthLogisticsOriginalTransport.AcceptPacket(this,world,callbacks,false))RebirthLogisticsHeaderAdmission.Ack(world,operation,binding,nonce,state,entity);}
    public int GetLength()=>320;
}
