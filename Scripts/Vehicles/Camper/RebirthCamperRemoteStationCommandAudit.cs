using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

// Existing original/native status observation for the authenticated debug Bridge.
// No command dispatch, status query packet, admission, archive or owner save.
internal static class RebirthCamperRemoteStationCommandAudit
{
    internal static JObject Describe(RebirthCamperRemoteStationCommand command)=>new JObject{
        ["operation"]=command.Operation.ToString("N"),["job"]=command.Job.ToString("N"),["kind"]=command.Kind.ToString(),
        ["recipe"]=command.RecipeName,["batches"]=command.Batches,["tier"]=command.Tier};
    private static void Safe(string path)
    {for(var directory=new DirectoryInfo(Path.GetFullPath(path));directory!=null;directory=directory.Parent)if(directory.Exists&&(directory.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();}
    private static XElement Read(string path)
    {
        Safe(Path.GetDirectoryName(path));var file=new FileInfo(path);if(!file.Exists||file.Length<1||file.Length>8192||(file.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8192}))return XElement.Load(reader);
    }
    internal static JObject Capture(EntityPlayer actor,string selectedOperation)
    {
        var result=new JObject{["available"]=false,["schema"]=1};var rows=new JArray();result["records"]=rows;
#if REBIRTH_DEBUG
        try
        {
            if(GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||!ThreadManager.IsMainThread()||actor?.world==null||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor))return result;
            Guid selected=Guid.Empty;if(!string.IsNullOrEmpty(selectedOperation)&&(!Guid.TryParseExact(selectedOperation,"N",out selected)||selected==Guid.Empty))return result;
            if(actor.world.IsRemote())
            {
                if(actor is not EntityPlayerLocal player||!ReferenceEquals(actor.world.GetPrimaryPlayer(),actor))return result;
                var client=RebirthCamperRemoteCommandOutbox.CaptureCommandAudit(player,selected);
                client["session"]=RebirthCamperRemoteStationClient.CaptureCommandAudit(player);client["role"]="owner";client["survivorMode"]=RebirthSurvivorMode.IsEnabledForCurrentWorld();return client;
            }
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity))return result;
            string creation=RebirthCamperCharacterAccess.CurrentCreation(actor),save=Path.GetFullPath(GameIO.GetSaveGameDir()).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            var world=actor.world;int deaths=actor.Died;int count=0;
            foreach(string area in new[]{"RemoteStationCommands","RemoteStationCommandHistory"})
            {
                string directory=Path.Combine(save,"REBIRTH",area,identity.StorageKey);Safe(directory);if(!Directory.Exists(directory))continue;
                var files=selected==Guid.Empty?Directory.EnumerateFiles(directory,"*.xml").Where(path=>Path.GetFileName(path).Length==36):new[]{Path.Combine(directory,selected.ToString("N")+".xml")}.Where(File.Exists);
                foreach(string path in files)
                {
                    if(++count>128){result["truncated"]=true;return result;}
                    var node=Read(path);int version=(int?)node.Attribute("version")??0;
                    var command=new RebirthCamperRemoteStationCommand((RebirthCamperRemoteStationCommandKind)checked((byte)(int)node.Attribute("kind")),Guid.ParseExact((string)node.Attribute("operation"),"N"),
                        Guid.ParseExact((string)node.Attribute("job"),"N"),(string)node.Attribute("recipe"),(int)node.Attribute("batches"),(int)node.Attribute("tier"));
                    if(!RebirthCamperStationAddress.TryParse((string)node.Attribute("address"),out var address)||!address.IsValid||
                        node.Name!="stationCommandIdentity"||node.HasElements||(version!=1&&version!=2)||Path.GetFileName(path)!=command.Operation.ToString("N")+".xml"||
                        (string)node.Attribute("owner")!=identity.StorageKey||(string)node.Attribute("world")!=world.worldState.Guid||(string)node.Attribute("save")!=save)throw new InvalidDataException();
                    if((string)node.Attribute("creation")!=creation)continue;
                    string binding=RebirthCamperRemoteStationCommandStatus.Binding(command,identity.StorageKey,creation,address);
                    var expected=new XElement("stationCommandIdentity",new XAttribute("version",version),new XAttribute("world",world.worldState.Guid),new XAttribute("save",save),new XAttribute("owner",identity.StorageKey),
                        new XAttribute("creation",creation),new XAttribute("address",address.ToString()),new XAttribute("operation",command.Operation.ToString("N")),new XAttribute("kind",(byte)command.Kind),
                        new XAttribute("job",command.Job.ToString("N")),new XAttribute("recipe",command.RecipeName),new XAttribute("batches",command.Batches),new XAttribute("tier",command.Tier),new XAttribute("binding",binding));
                    if(version==2){if((int?)node.Attribute("admittedDeaths") is not int admitted||admitted<0)throw new InvalidDataException();expected.Add(new XAttribute("invocationProtocol",1),new XAttribute("admittedDeaths",admitted));}
                    if(!XNode.DeepEquals(node,expected))throw new InvalidDataException();
                    var row=Describe(command);row["binding"]=binding;row["address"]=address.ToString();row["vehicle"]=address.Vehicle.ToString("N");row["installation"]=address.Installation.ToString("N");
                    row["owner"]=identity.StorageKey;row["creation"]=creation;row["world"]=world.worldState.Guid;row["area"]=area;row["readable"]=true;
                    row["status"]="Unknown";row["effectProof"]=null;row["leaseCurrent"]=false;
                    if(RebirthCamperRemoteStationTransport.TryGetCurrent(actor,out _,out var currentAddress,out var currentCreation,out var lease)&&currentAddress.Equals(address)&&currentCreation==creation)
                    {
                        row["leaseCurrent"]=true;
                        var status=RebirthCamperRemoteStationCommandStatus.ObserveStatus(actor,address,creation,lease,command,out var observation);
                        row["status"]=status.ToString();row["effectProof"]=observation?.EffectProof;row["originalDeaths"]=observation?.OriginalDeaths??-1;
                        row["effectOperation"]=observation?.EffectOperation.ToString("N");row["bindingQualified"]=observation?.RequestBinding==binding;
                    }
                    rows.Add(row);
                }
            }
            if(rows.OfType<JObject>().GroupBy(row=>(string)row["operation"]).Any(group=>group.Count()!=1))throw new InvalidDataException();
            result["available"]=ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(actor.world,world)&&ReferenceEquals(world.GetEntity(actor.entityId),actor)&&actor.Died==deaths&&
                RebirthCamperCharacterAccess.CurrentCreation(actor)==creation&&Path.GetFullPath(GameIO.GetSaveGameDir()).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)==save;
            result["role"]="authority";
            result["survivorMode"]=RebirthSurvivorMode.IsEnabledForCurrentWorld();
        }
        catch(Exception error){result["available"]=false;result["error"]=error.GetType().Name;}
#endif
        return result;
    }
}
