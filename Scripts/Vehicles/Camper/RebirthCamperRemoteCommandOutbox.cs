using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Client-owned command metadata only. Neither an intent nor a terminal mirror
// proves item payment, native publication, or permission to execute a command.
internal static partial class RebirthCamperRemoteCommandOutbox
{
    private const int MaximumBytes=8192,MaximumPending=256;
    private static readonly HashSet<Guid> publishing=new HashSet<Guid>();
    private static string Digest(string value)
    {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
    private static void Safe(string path)
    {for(var item=new DirectoryInfo(Path.GetFullPath(path));item!=null;item=item.Parent)if(item.Exists&&(item.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Command outbox path changed.");}
    private static string Root(EntityPlayerLocal player,out string owner,out string creation,out string world)
    {
        if(!ThreadManager.IsMainThread()||player?.world?.IsRemote()!=true||player.IsDead()||!ReferenceEquals(GameManager.Instance?.World,player.world)||
            !ReferenceEquals(player.world.GetPrimaryPlayer(),player)||!ReferenceEquals(player.world.GetEntity(player.entityId),player)||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity==null)throw new InvalidOperationException("Current native command owner required.");
        owner=identity.StorageKey;creation=RebirthCamperCharacterAccess.CurrentCreation(player);world=player.world.worldState.Guid.ToString();
        string save=GameIO.GetSaveGameDir();if(string.IsNullOrWhiteSpace(save)||string.IsNullOrEmpty(creation)||creation.Length>512)throw new InvalidOperationException("Command owner scope unavailable.");
        string root=Path.GetFullPath(Path.Combine(save,"REBIRTH","RemoteStationClientCommands",owner,Digest(world)));Safe(root);return root;
    }
    private static XElement Image(string owner,string creation,string world,RebirthCamperStationAddress address,RebirthCamperRemoteStationCommand command,int version=2)
        =>new XElement("stationCommandOutbox",new XAttribute("version",version),new XAttribute("owner",owner),new XAttribute("creation",creation),new XAttribute("world",world),
            new XAttribute("vehicle",address.Vehicle.ToString("N")),new XAttribute("installation",address.Installation.ToString("N")),new XAttribute("operation",command.Operation.ToString("N")),
            new XAttribute("job",command.Job.ToString("N")),new XAttribute("kind",(byte)command.Kind),new XAttribute("recipe",command.RecipeName),new XAttribute("batches",command.Batches),new XAttribute("tier",command.Tier),version==2?new XAttribute("attemptProtocol",1):null);
    private static bool Matches(XElement image,string owner,string creation,string world,RebirthCamperStationAddress address,RebirthCamperRemoteStationCommand command)
    {
        int version=(int?)image.Attribute("version")??0;
        return (version==1||version==2)&&XNode.DeepEquals(image,Image(owner,creation,world,address,command,version));
    }
    private static XElement Attempt(XElement image)=>new XElement("attempt",new XAttribute("digest",Digest(image.ToString(SaveOptions.DisableFormatting))));
    private static XElement Terminal(XElement image,RebirthCamperRemoteStationCommandStatus.State status)
        =>new XElement("terminal",new XAttribute("digest",Digest(image.ToString(SaveOptions.DisableFormatting))),new XAttribute("status",(byte)status));
    private static XElement Read(string path)
    {
        Safe(Path.GetDirectoryName(path));var file=new FileInfo(path);
        if(!file.Exists||file.Length<1||file.Length>MaximumBytes||(file.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Bounded original command metadata required.");
        using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))return XElement.Load(reader);
    }
    private static void WriteNew(string path,XElement image)
    {
        byte[] bytes=new UTF8Encoding(false,true).GetBytes(image.ToString(SaveOptions.DisableFormatting));if(bytes.Length>MaximumBytes)throw new InvalidDataException("Command metadata exceeds bound.");
        Safe(Path.GetDirectoryName(path));Directory.CreateDirectory(Path.GetDirectoryName(path));Safe(Path.GetDirectoryName(path));
        using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        if(!XNode.DeepEquals(Read(path),image))throw new InvalidDataException("Command metadata readback differs.");
    }
    private static RebirthCamperRemoteStationCommand Command(XElement image)
    {
        int version=(int?)image.Attribute("version")??0;
        if(image.Name!="stationCommandOutbox"||image.HasElements||
            !(version==1&&image.Attributes().Count()==12||version==2&&image.Attributes().Count()==13&&(int?)image.Attribute("attemptProtocol")==1))throw new InvalidDataException("Invalid command original.");
        return new RebirthCamperRemoteStationCommand((RebirthCamperRemoteStationCommandKind)checked((byte)(int)image.Attribute("kind")),Guid.ParseExact((string)image.Attribute("operation"),"N"),
            Guid.ParseExact((string)image.Attribute("job"),"N"),(string)image.Attribute("recipe"),(int)image.Attribute("batches"),(int)image.Attribute("tier"));
    }
    // Explicit v2 protocol evidence only: first Send requires the complete
    // attempt marker. No missing legacy marker or staged file proves no send.
    private static bool RetireNeverAttempted(string root,string directory,XElement image,Guid operation)
    {
        if((int?)image.Attribute("version")!=2||publishing.Contains(operation))return false;
        Safe(directory);
        string attempt=Path.Combine(directory,"attempt.xml"),proofPath=Path.Combine(directory,"neverAttempted.xml"),terminalPath=Path.Combine(directory,"terminal.xml");
        var entries=Directory.EnumerateFileSystemEntries(directory).Take(5).ToArray();
        foreach(string entry in entries)
            if(entry!=Path.Combine(directory,"original.xml")&&entry!=attempt&&entry!=proofPath&&entry!=terminalPath||!File.Exists(entry))throw new InvalidDataException("Unknown command outbox companion retained.");
        if(entries.Length>4)throw new InvalidDataException("Command outbox companion bound exceeded.");
        if(File.Exists(attempt))
        {
            if(File.Exists(proofPath)||!XNode.DeepEquals(Read(attempt),Attempt(image)))throw new InvalidDataException("Conflicting command send evidence retained.");
            return false;
        }
        var proof=new XElement("neverAttempted",new XAttribute("version",1),new XElement(image));
        var terminal=Terminal(image,RebirthCamperRemoteStationCommandStatus.State.Rejected);
        // A terminal without its positive protocol proof is not ours to repair.
        if(File.Exists(terminalPath)&&!File.Exists(proofPath))throw new InvalidDataException("Unqualified command terminal retained.");
        if(File.Exists(proofPath)){if(!XNode.DeepEquals(Read(proofPath),proof))throw new InvalidDataException("Changed never-attempted original.");}else WriteNew(proofPath,proof);
        if(File.Exists(terminalPath)){if(!XNode.DeepEquals(Read(terminalPath),terminal))throw new InvalidDataException("Conflicting never-attempted terminal.");}else WriteNew(terminalPath,terminal);
        if(File.Exists(attempt)||!XNode.DeepEquals(Read(Path.Combine(directory,"original.xml")),image)||!XNode.DeepEquals(Read(proofPath),proof)||!XNode.DeepEquals(Read(terminalPath),terminal))return false;
        string completed=Path.Combine(root,"completed",operation.ToString("N"));Safe(completed);
        if(Directory.Exists(completed)||File.Exists(completed))throw new InvalidDataException("Conflicting command archive retained.");
        Directory.CreateDirectory(Path.GetDirectoryName(completed));Safe(completed);Directory.Move(directory,completed);
        if(!XNode.DeepEquals(Read(Path.Combine(completed,"original.xml")),image)||!XNode.DeepEquals(Read(Path.Combine(completed,"neverAttempted.xml")),proof)||!XNode.DeepEquals(Read(Path.Combine(completed,"terminal.xml")),terminal))throw new InvalidDataException("Never-attempted archive readback differs.");
        return true;
    }
    // Only one unresolved original for this exact current owner/station may be
    // attached to a fresh native UI lease. This returns data for STATUS ONLY.
    internal static bool TryReadPending(EntityPlayerLocal player,RebirthCamperStationAddress address,out RebirthCamperRemoteStationCommand command)
    {
        command=null;
        try
        {
            if(!address.IsValid)return false;string root=Root(player,out var owner,out var creation,out var world);string pending=Path.Combine(root,"pending");Safe(pending);
            if(!Directory.Exists(pending))return true;int count=0;
            foreach(string directory in Directory.EnumerateFileSystemEntries(pending))
            {
                if(++count>MaximumPending||!Directory.Exists(directory)||!Guid.TryParseExact(Path.GetFileName(directory),"N",out var id)||id==Guid.Empty)return false;
                var image=Read(Path.Combine(directory,"original.xml"));var candidate=Command(image);
                if(candidate.Operation!=id||(string)image.Attribute("owner")!=owner||(string)image.Attribute("world")!=world)return false;
                string recordedCreation=(string)image.Attribute("creation");
                var recordedAddress=new RebirthCamperStationAddress(Guid.ParseExact((string)image.Attribute("vehicle"),"N"),Guid.ParseExact((string)image.Attribute("installation"),"N"));
                if(!recordedAddress.IsValid||string.IsNullOrEmpty(recordedCreation)||recordedCreation.Length>512||
                    !Matches(image,owner,recordedCreation,world,recordedAddress,candidate))return false;
                if(recordedCreation!=creation||!recordedAddress.Equals(address))continue;
                if(!RebirthCamperRemoteStationCommandStatus.HasNativeOriginal(candidate)||!Matches(image,owner,creation,world,address,candidate))return false;
                if(RetireNeverAttempted(root,directory,image,id))continue;
                if(command!=null)return false;
                command=candidate;
            }
            return Root(player,out var currentOwner,out var currentCreation,out var currentWorld)==root&&currentOwner==owner&&currentCreation==creation&&currentWorld==world;
        }
        catch{command=null;return false;}
    }
    internal static bool TryRecordFirstAttempt(EntityPlayerLocal player,RebirthCamperStationAddress address,RebirthCamperRemoteStationCommand command,out bool first)
    {
        first=false;bool ownPublication=false;
        try
        {
            if(!address.IsValid||!RebirthCamperRemoteStationCommandStatus.HasNativeOriginal(command))return false;
            string root=Root(player,out var owner,out var creation,out var world);var expected=Image(owner,creation,world,address,command);
            string pending=Path.Combine(root,"pending",command.Operation.ToString("N")),completed=Path.Combine(root,"completed",command.Operation.ToString("N"));Safe(pending);Safe(completed);
            if(Directory.Exists(completed)&&Directory.Exists(pending))return false;
            if(Directory.Exists(completed))return Matches(Read(Path.Combine(completed,"original.xml")),owner,creation,world,address,command);
            if(Directory.Exists(pending))return Matches(Read(Path.Combine(pending,"original.xml")),owner,creation,world,address,command);
            if(!TryReadPending(player,address,out var previous)||previous!=null)return false;
            string pendingRoot=Path.GetDirectoryName(pending);
            if(Directory.Exists(pendingRoot)&&Directory.EnumerateFileSystemEntries(pendingRoot).Take(MaximumPending+1).Count()>=MaximumPending)return false;
            if(!publishing.Add(command.Operation))return false;ownPublication=true;
            WriteNew(Path.Combine(pending,"original.xml"),expected);
            // This is the only permission for the one following Send, including
            // ambiguous send failure. A recovered directory is never resent.
            WriteNew(Path.Combine(pending,"attempt.xml"),Attempt(expected));
            if(!XNode.DeepEquals(Read(Path.Combine(pending,"original.xml")),expected))return false;
            if(Root(player,out var currentOwner,out var currentCreation,out var currentWorld)!=root||currentOwner!=owner||currentCreation!=creation||currentWorld!=world)return false;
            first=true;return true;
        }
        catch{return false;}
        finally{if(ownPublication)publishing.Remove(command.Operation);}
    }
    // Positive local protocol outcome only. This cannot classify an attempted
    // command, substitute a peer receipt or authorize native compensation.
    internal static bool TryResolveNeverAttempted(EntityPlayerLocal player,RebirthCamperStationAddress address,RebirthCamperRemoteStationCommand command)
    {
        try
        {
            if(command==null||!address.IsValid||publishing.Contains(command.Operation))return false;
            string root=Root(player,out var owner,out var creation,out var world),id=command.Operation.ToString("N");
            string pending=Path.Combine(root,"pending",id),completed=Path.Combine(root,"completed",id);Safe(pending);Safe(completed);
            if(Directory.Exists(pending))
            {
                if(Directory.Exists(completed))return false;
                var original=Read(Path.Combine(pending,"original.xml"));
                if(!Matches(original,owner,creation,world,address,command)||!RetireNeverAttempted(root,pending,original,command.Operation))return false;
            }
            var image=Read(Path.Combine(completed,"original.xml"));
            if((int?)image.Attribute("version")!=2||!Matches(image,owner,creation,world,address,command))return false;
            var expected=new XElement("neverAttempted",new XAttribute("version",1),new XElement(image));
            string proof=Path.Combine(completed,"neverAttempted.xml"),terminal=Path.Combine(completed,"terminal.xml"),originalPath=Path.Combine(completed,"original.xml");
            var entries=Directory.EnumerateFileSystemEntries(completed).Take(4).ToArray();
            if(entries.Length!=3||entries.Any(entry=>entry!=originalPath&&entry!=proof&&entry!=terminal)||
                !XNode.DeepEquals(Read(proof),expected)||!XNode.DeepEquals(Read(terminal),Terminal(image,RebirthCamperRemoteStationCommandStatus.State.Rejected)))return false;
            return Root(player,out var currentOwner,out var currentCreation,out var currentWorld)==root&&currentOwner==owner&&currentCreation==creation&&currentWorld==world;
        }
        catch{return false;}
    }
    // Only actual v2 originals declare the enforced server admission boundary.
    // Legacy outboxes may query status, but cannot ask a new server to classify
    // an absent legacy identity as a before-dispatch refusal.
    internal static bool DeclaresAdmissionProtocol(EntityPlayerLocal player,RebirthCamperStationAddress address,RebirthCamperRemoteStationCommand command)
    {
        try
        {
            if(command==null||!address.IsValid)return false;
            string root=Root(player,out var owner,out var creation,out var world);
            string directory=Path.Combine(root,"pending",command.Operation.ToString("N"));Safe(directory);
            var image=Read(Path.Combine(directory,"original.xml"));
            return (int?)image.Attribute("version")==2&&(int?)image.Attribute("attemptProtocol")==1&&
                Matches(image,owner,creation,world,address,command)&&File.Exists(Path.Combine(directory,"attempt.xml"))&&
                XNode.DeepEquals(Read(Path.Combine(directory,"attempt.xml")),Attempt(image));
        }
        catch{return false;}
    }
    // Called only after the typed authenticated reply matched the live pending
    // original. Archives metadata; never releases a native financial hold.
    internal static bool TryMirrorTerminal(EntityPlayerLocal player,RebirthCamperStationAddress address,RebirthCamperRemoteStationCommand command,RebirthCamperRemoteStationCommandStatus.State status)
    {
        try
        {
            if(status!=RebirthCamperRemoteStationCommandStatus.State.Applied&&status!=RebirthCamperRemoteStationCommandStatus.State.Rejected)return false;
            string root=Root(player,out var owner,out var creation,out var world);var expected=Image(owner,creation,world,address,command);
            string pending=Path.Combine(root,"pending",command.Operation.ToString("N")),completed=Path.Combine(root,"completed",command.Operation.ToString("N"));Safe(pending);Safe(completed);
            if(Directory.Exists(pending)&&Directory.Exists(completed))return false;
            string source=Directory.Exists(completed)?completed:pending;
            var retained=Read(Path.Combine(source,"original.xml"));if(!Matches(retained,owner,creation,world,address,command))return false;expected=retained;
            if(File.Exists(Path.Combine(source,"neverAttempted.xml")))return false; // local no-send proof cannot be overwritten by a peer outcome
            var terminal=Terminal(expected,status);
            string path=Path.Combine(source,"terminal.xml");if(File.Exists(path)){if(!XNode.DeepEquals(Read(path),terminal))return false;}else WriteNew(path,terminal);
            if(source==pending){string parent=Path.GetDirectoryName(completed);Safe(parent);Directory.CreateDirectory(parent);Directory.Move(pending,completed);}
            return XNode.DeepEquals(Read(Path.Combine(completed,"original.xml")),expected)&&XNode.DeepEquals(Read(Path.Combine(completed,"terminal.xml")),terminal);
        }
        catch{return false;}
    }
}
