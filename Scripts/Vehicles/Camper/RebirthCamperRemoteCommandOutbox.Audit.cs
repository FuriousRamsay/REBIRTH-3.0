using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

internal static partial class RebirthCamperRemoteCommandOutbox
{
    // Read-only: do not call TryReadPending, which may retire a positively
    // never-sent original. Observing a command cannot send or settle it.
    internal static JObject CaptureCommandAudit(EntityPlayerLocal player,Guid selected)
    {
        var result=new JObject{["available"]=false,["schema"]=1};var rows=new JArray();result["records"]=rows;
        try
        {
            string root=Root(player,out var owner,out var creation,out var world);int count=0;
            foreach(string area in new[]{"pending","completed"})
            {
                string directory=Path.Combine(root,area);Safe(directory);if(!Directory.Exists(directory))continue;
                var directories=selected==Guid.Empty?Directory.EnumerateFileSystemEntries(directory):new[]{Path.Combine(directory,selected.ToString("N"))}.Where(Directory.Exists);
                foreach(string retained in directories)
                {
                    if(++count>128){result["truncated"]=true;return result;}
                    if(!Directory.Exists(retained)||!Guid.TryParseExact(Path.GetFileName(retained),"N",out var id)||id==Guid.Empty)throw new InvalidDataException();
                    Safe(retained);var image=Read(Path.Combine(retained,"original.xml"));var command=Command(image);
                    var address=new RebirthCamperStationAddress(Guid.ParseExact((string)image.Attribute("vehicle"),"N"),Guid.ParseExact((string)image.Attribute("installation"),"N"));
                    if(command.Operation!=id||!address.IsValid||(string)image.Attribute("owner")!=owner||(string)image.Attribute("world")!=world)throw new InvalidDataException();
                    string originalCreation=(string)image.Attribute("creation");
                    if(!Matches(image,owner,originalCreation,world,address,command))throw new InvalidDataException();
                    if(originalCreation!=creation)continue;
                    var row=RebirthCamperRemoteStationCommandAudit.Describe(command);
                    row["address"]=address.ToString();row["vehicle"]=address.Vehicle.ToString("N");row["installation"]=address.Installation.ToString("N");
                    row["owner"]=owner;row["creation"]=creation;row["world"]=world;row["area"]=area;row["readable"]=true;
                    row["identityFingerprint"]=Digest(image.ToString(SaveOptions.DisableFormatting));
                    row["binding"]=RebirthCamperRemoteStationCommandStatus.Binding(command,owner,creation,address);
                    string attempt=Path.Combine(retained,"attempt.xml"),terminal=Path.Combine(retained,"terminal.xml"),never=Path.Combine(retained,"neverAttempted.xml");
                    var entries=Directory.EnumerateFileSystemEntries(retained).Take(6).ToArray();
                    if(entries.Length>4||entries.Any(path=>path!=Path.Combine(retained,"original.xml")&&path!=attempt&&path!=terminal&&path!=never||!File.Exists(path)))throw new InvalidDataException();
                    bool attempted=File.Exists(attempt);if(attempted&&!XNode.DeepEquals(Read(attempt),Attempt(image)))throw new InvalidDataException();
                    row["attempted"]=attempted;row["neverAttempted"]=File.Exists(never);row["terminal"]="Unknown";
                    if(File.Exists(never)&&(!XNode.DeepEquals(Read(never),new XElement("neverAttempted",new XAttribute("version",1),new XElement(image)))||attempted))throw new InvalidDataException();
                    if(File.Exists(terminal))
                    {
                        var saved=Read(terminal);var status=(RebirthCamperRemoteStationCommandStatus.State)checked((byte)(int)saved.Attribute("status"));
                        if(status!=RebirthCamperRemoteStationCommandStatus.State.Applied&&status!=RebirthCamperRemoteStationCommandStatus.State.Rejected||!XNode.DeepEquals(saved,Terminal(image,status)))throw new InvalidDataException();
                        row["terminal"]=status.ToString();
                    }
                    rows.Add(row);
                }
            }
            if(rows.OfType<JObject>().GroupBy(row=>(string)row["operation"]).Any(group=>group.Count()!=1))throw new InvalidDataException();
            result["available"]=Root(player,out var currentOwner,out var currentCreation,out var currentWorld)==root&&currentOwner==owner&&currentCreation==creation&&currentWorld==world;
        }
        catch(Exception error){result["available"]=false;result["error"]=error.GetType().Name;}
        return result;
    }
}
