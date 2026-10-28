// Actual outbox, address and immutable command classes. Native owner/world
// doubles; actual filesystem is isolated under a freshly generated temp root.
// NOT_RUN. This does not prove native payments, server state or remote delivery.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
class WorldState {internal Guid Guid;}
class World {internal WorldState worldState=new WorldState();internal EntityPlayerLocal Player;internal bool IsRemote()=>true;internal EntityPlayerLocal GetPrimaryPlayer()=>Player;internal EntityPlayerLocal GetEntity(int id)=>Player;}
class EntityPlayerLocal {internal World world;internal int entityId=1;internal bool IsDead()=>false;}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class RebirthSurvivorMode {internal static bool IsEnabledForCurrentWorld()=>false;}
static class GameIO {internal static string Directory;internal static string GetSaveGameDir()=>Directory;}
class RebirthStablePlayerIdentity {internal string StorageKey=new string('a',64);internal static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return true;}}
static class RebirthCamperCharacterAccess {internal static string Creation="original-creation";internal static string CurrentCreation(EntityPlayerLocal player)=>Creation;}
static class RebirthCamperRemoteStationCommandStatus
{
    internal enum State {Unknown,Pending,Applied,Rejected}
    // PRODUCTION_KIND_CLASSIFIER
}
// PRODUCTION_OUTBOX_TYPES
static class RebirthCamperCommandOutboxFixture
{
    private static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
    private static EntityPlayerLocal Owner(Guid world)
    {var p=new EntityPlayerLocal();p.world=new World{Player=p};p.world.worldState.Guid=world;GameManager.Instance.World=p.world;return p;}
    public static void Main()
    {
        string temp=Path.GetFullPath(Path.GetTempPath()),root=Path.GetFullPath(Path.Combine(temp,"rebirth-command-outbox-"+Guid.NewGuid().ToString("N")));
        Need(root.StartsWith(temp,StringComparison.OrdinalIgnoreCase)&&!Directory.Exists(root),"Fresh contained fixture directory required");
        Directory.CreateDirectory(root);GameIO.Directory=root;
        try
        {
            Guid world=Guid.NewGuid();var player=Owner(world);var address=new RebirthCamperStationAddress(Guid.NewGuid(),Guid.NewGuid());
            var command=new RebirthCamperRemoteStationCommand(RebirthCamperRemoteStationCommandKind.CraftPlayer,Guid.NewGuid(),Guid.NewGuid(),"recipe",2,1);
            Need(RebirthCamperRemoteCommandOutbox.TryRecordFirstAttempt(player,address,command,out bool first)&&first,"Durable initial send intent refused");
            Need(RebirthCamperRemoteCommandOutbox.DeclaresAdmissionProtocol(player,address,command),"actual v2 attempt did not declare server admission protocol");
            Need(RebirthCamperRemoteCommandOutbox.TryRecordFirstAttempt(player,address,command,out first)&&!first,"Duplicate intent permitted resend");
            player=Owner(world);
            Need(RebirthCamperRemoteCommandOutbox.TryReadPending(player,address,out var restored)&&restored.Operation==command.Operation&&restored.Job==command.Job&&restored.Batches==2,"Cold native-body replacement lost metadata original");
            var conflict=new RebirthCamperRemoteStationCommand(command.Kind,command.Operation,command.Job,"changed",2,1);
            Need(!RebirthCamperRemoteCommandOutbox.TryRecordFirstAttempt(player,address,conflict,out first),"Changed same-ID original accepted");
            string path=Directory.EnumerateFiles(root,"original.xml",SearchOption.AllDirectories).Single(),original=File.ReadAllText(path);
            var oldProtocol=XElement.Parse(original);oldProtocol.SetAttributeValue("version",1);oldProtocol.Attribute("attemptProtocol").Remove();File.WriteAllText(path,oldProtocol.ToString(SaveOptions.DisableFormatting));
            Need(!RebirthCamperRemoteCommandOutbox.DeclaresAdmissionProtocol(player,address,command),"legacy outbox declared new server admission refusal permission");File.WriteAllText(path,original);
            Need(!RebirthCamperRemoteCommandOutbox.DeclaresAdmissionProtocol(player,address,conflict),"changed complete original borrowed admission protocol declaration");
            var corrupt=XElement.Parse(original);corrupt.SetAttributeValue("vehicle","invalid");File.WriteAllText(path,corrupt.ToString());
            Need(!RebirthCamperRemoteCommandOutbox.TryReadPending(player,address,out _),"Malformed retained original ignored");
            Need(File.ReadAllText(path)==corrupt.ToString(),"Unknown original was deleted or repaired");File.WriteAllText(path,original);
            Need(!RebirthCamperRemoteCommandOutbox.TryMirrorTerminal(player,address,command,RebirthCamperRemoteStationCommandStatus.State.Pending),"Pending became terminal");
            Need(RebirthCamperRemoteCommandOutbox.TryMirrorTerminal(player,address,command,RebirthCamperRemoteStationCommandStatus.State.Applied),"Typed terminal metadata archive failed");
            Need(RebirthCamperRemoteCommandOutbox.TryReadPending(player,address,out restored)&&restored==null,"Archived original still pending");
            Need(RebirthCamperRemoteCommandOutbox.TryRecordFirstAttempt(player,address,command,out first)&&!first,"Archived original permitted resend");
            Need(!RebirthCamperRemoteCommandOutbox.TryMirrorTerminal(player,address,command,RebirthCamperRemoteStationCommandStatus.State.Rejected),"Conflicting terminal replaced applied metadata");
            foreach(bool burning in new[]{true,false})
            {
                var fuel=RebirthCamperRemoteStationCommand.Fuel(Guid.NewGuid(),burning);
                Need(RebirthCamperRemoteCommandOutbox.TryRecordFirstAttempt(player,address,fuel,out first)&&first,"Fuel first attempt not durably retained");
                player=Owner(world);
                Need(RebirthCamperRemoteCommandOutbox.TryReadPending(player,address,out restored)&&restored.IsFuel&&restored.Kind==fuel.Kind&&restored.Operation==fuel.Operation&&restored.Job==fuel.Operation,"Cold fuel intent lost operation/state");
                Need(RebirthCamperRemoteCommandOutbox.TryRecordFirstAttempt(player,address,restored,out first)&&!first,"Cold fuel permitted mutating resend");
                var opposite=RebirthCamperRemoteStationCommand.Fuel(fuel.Operation,!burning);
                Need(!RebirthCamperRemoteCommandOutbox.TryRecordFirstAttempt(player,address,opposite,out first),"Same fuel operation changed state");
                Need(RebirthCamperRemoteCommandOutbox.TryMirrorTerminal(player,address,fuel,RebirthCamperRemoteStationCommandStatus.State.Applied),"Fuel terminal not mirrored");
                Need(RebirthCamperRemoteCommandOutbox.TryReadPending(player,address,out restored)&&restored==null,"Fuel terminal remained pending");
                Need(RebirthCamperRemoteCommandOutbox.TryRecordFirstAttempt(player,address,fuel,out first)&&!first,"Completed fuel resent after opposite operation");
            }
            var inputJob=Guid.NewGuid();var inputCommand=new RebirthCamperRemoteStationCommand(RebirthCamperRemoteStationCommandKind.Craft,inputJob,inputJob,"recipe",3,0);
            Need(RebirthCamperRemoteCommandOutbox.TryRecordFirstAttempt(player,address,inputCommand,out first)&&first,"Native station-input intent bypassed outbox");player=Owner(world);
            Need(RebirthCamperRemoteCommandOutbox.TryReadPending(player,address,out restored)&&restored.Kind==RebirthCamperRemoteStationCommandKind.Craft&&restored.Operation==inputJob,"Cold native input intent lost");
            Need(RebirthCamperRemoteCommandOutbox.TryRecordFirstAttempt(player,address,restored,out first)&&!first,"Cold station-input intent resent");
            Need(RebirthCamperRemoteCommandOutbox.TryMirrorTerminal(player,address,inputCommand,RebirthCamperRemoteStationCommandStatus.State.Applied),"Native input saved terminal not archived");
            NeverAttemptedCases(root,world,address);
        }
        finally
        {
            Need(Path.GetFullPath(root).StartsWith(temp,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(root).StartsWith("rebirth-command-outbox-",StringComparison.Ordinal),"Refuse cleanup outside fixture root");
            Directory.Delete(root,true);
        }
    }
    private static void NeverAttemptedCases(string root,Guid world,RebirthCamperStationAddress address)
    {
        // All prefix/crash simulation edits affect only newly generated fixture
        // files. Production never removes an attempt or rewrites an original.
        for(int scenario=0;scenario<8;scenario++)
        {
            GameIO.Directory=Path.Combine(root,"never-attempted-"+scenario);Directory.CreateDirectory(GameIO.Directory);
            var player=Owner(world);var command=RebirthCamperRemoteStationCommand.Fuel(Guid.NewGuid(),true);
            Need(RebirthCamperRemoteCommandOutbox.TryRecordFirstAttempt(player,address,command,out bool first)&&first,"Fixture original prefix not published");
            string originalPath=Directory.EnumerateFiles(GameIO.Directory,"original.xml",SearchOption.AllDirectories).Single(),directory=Path.GetDirectoryName(originalPath),attempt=Path.Combine(directory,"attempt.xml");
            var image=XElement.Load(originalPath);Need((int?)image.Attribute("version")==2&&(int?)image.Attribute("attemptProtocol")==1,"New outbox lacks explicit attempt protocol");
            if(scenario!=1)File.Delete(attempt);
            if(scenario==2){image.SetAttributeValue("version",1);image.Attribute("attemptProtocol").Remove();File.WriteAllText(originalPath,image.ToString(SaveOptions.DisableFormatting));}
            if(scenario==3)File.WriteAllText(Path.Combine(directory,"attempt.xml.tmp"),"retained-stage");
            if(scenario==4)File.WriteAllText(Path.Combine(directory,"neverAttempted.xml"),new XElement("neverAttempted",new XAttribute("version",1),new XElement(image)).ToString(SaveOptions.DisableFormatting));
            if(scenario==5)File.WriteAllText(Path.Combine(directory,"neverAttempted.xml"),"<changed/>");
            if(scenario==6)File.WriteAllText(Path.Combine(directory,"terminal.xml"),"<terminal/>");
            if(scenario==7){var foreign=new XElement(image);foreign.SetAttributeValue("creation","different-creation");File.WriteAllText(originalPath,foreign.ToString(SaveOptions.DisableFormatting));}
            player=Owner(world);bool read=RebirthCamperRemoteCommandOutbox.TryReadPending(player,address,out var pending);
            if(scenario==0||scenario==4)
            {
                Need(read&&pending==null&&!Directory.Exists(directory),"Positive never-attempted prefix was not retired");
                string archived=Directory.EnumerateFiles(GameIO.Directory,"neverAttempted.xml",SearchOption.AllDirectories).Single();
                Need(XNode.DeepEquals(XElement.Load(archived),new XElement("neverAttempted",new XAttribute("version",1),new XElement(image))),"Local no-send proof lost full original");
                Need(RebirthCamperRemoteCommandOutbox.TryRecordFirstAttempt(player,address,command,out first)&&!first,"Locally retired original authorized resend");
                Need(RebirthCamperRemoteCommandOutbox.TryResolveNeverAttempted(player,address,command),"Saved local proof did not resolve exact live original");
                Need(!RebirthCamperRemoteCommandOutbox.TryMirrorTerminal(player,address,command,RebirthCamperRemoteStationCommandStatus.State.Applied),"Peer outcome replaced local no-send proof");
            }
            else if(scenario==1||scenario==2)
            {
                Need(read&&pending!=null&&pending.Operation==command.Operation&&Directory.Exists(directory),"Attempted or legacy original inferred never sent");
                Need(!RebirthCamperRemoteCommandOutbox.TryResolveNeverAttempted(player,address,command),"Attempted/legacy original resolved from missing send evidence");
                if(scenario==2)Need(RebirthCamperRemoteCommandOutbox.TryMirrorTerminal(player,address,command,RebirthCamperRemoteStationCommandStatus.State.Rejected),"Legacy exact typed terminal compatibility lost");
            }
            else if(scenario==7)Need(read&&pending==null&&Directory.Exists(directory),"Foreign creation original was retired");
            else Need(!read&&Directory.Exists(directory)&&File.Exists(originalPath),"Ambiguous stage/proof/terminal was repaired or released");
        }
    }
}
