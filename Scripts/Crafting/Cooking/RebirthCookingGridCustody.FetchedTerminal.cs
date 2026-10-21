using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthCookingGridCustody
{
    private sealed class FetchedTerminalObservation
    {
        internal WeakReference<World> World;internal WeakReference<EntityPlayerLocal> Actor;
        internal ConnectionManager Manager;internal INetConnection Connection;internal string Directory,Creation,Owner,NativeWorld,BodyHash;internal int Deaths;
        internal bool Current()
        {
            if(!ThreadManager.IsMainThread()||!World.TryGetTarget(out var world)||!Actor.TryGetTarget(out var actor))return false;
            return ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(actor.world,world)&&
                ReferenceEquals(world.GetPrimaryPlayer(),actor)&&ReferenceEquals(world.GetEntity(actor.entityId),actor)&&actor.IsSpawned()&&!actor.IsDead()&&actor.Died==Deaths&&
                world.worldState?.Guid==NativeWorld&&GameIO.GetSaveGameDir()==Directory&&RebirthCamperCharacterAccess.CurrentCreation(actor)==Creation&&
                ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,Manager)&&
                (world.IsRemote()?Manager?.IsServer==false&&Manager.IsConnected&&Connection!=null&&!Connection.IsDisconnected()&&Manager.connectionToServer?.Length>0&&ReferenceEquals(Manager.connectionToServer[0],Connection):Manager?.IsServer==true)&&
                RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&identity.CanonicalId==Owner;
        }
    }
    // Cold terminal metadata cannot restore paid grid contents by itself. A
    // fresh reply in this exact native session must requalify the same child.
    private static readonly System.Collections.Generic.Dictionary<Guid,FetchedTerminalObservation> fetchedTerminalObservations=new System.Collections.Generic.Dictionary<Guid,FetchedTerminalObservation>();
    private static readonly System.Collections.Generic.Queue<Guid> fetchedTerminalOrder=new System.Collections.Generic.Queue<Guid>();
    private static bool FreshFetchedTerminal(RebirthCamperTransferJournal.Record record)=>
        fetchedTerminalObservations.TryGetValue(record.Operation,out var value)&&value.Current()&&
        value.BodyHash==RebirthRemoteResourceDurableJournal.Digest(new UTF8Encoding(false,true).GetString(record.Before));
    private static RebirthCamperTransferJournal FetchedTerminals()=>new RebirthCamperTransferJournal(
        Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingGridFetchedTerminals"));
    private static bool TerminalHex(string value)=>value?.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    private static XElement FetchedImage(RebirthCamperTransferJournal.Record record)
    {
        if(record==null||record.State==RebirthCamperTransferJournal.Phase.Rejected||!record.Before.SequenceEqual(record.After)||record.Before.Length>8*1024*1024)
            throw new InvalidDataException();
        var grid=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),8*1024*1024);
        if(grid.Name!="cookingGridOriginal"||(int?)grid.Attribute("version")!=1||
            (long?)grid.Attribute("fetchId") is not long fetch||fetch<=0||(int?)grid.Attribute("deaths") is not int deaths||deaths<0||
            (string)grid.Attribute("creation")!=record.OwnerCreation||
            !new[]{"fetched","fetched-return"}.Contains((string)grid.Attribute("action"))||grid.Element("afterActor")==null)
            throw new InvalidDataException();
        return grid;
    }
    // This verifies a host's already committed owner terminal against the
    // exact local child. It never infers a new grant from a current inventory.
    private static bool MatchesFetchedTerminal(RebirthCamperTransferJournal.Record gridRecord,XElement terminal)
    {
        var grid=FetchedImage(gridRecord);
        if(terminal.Name!="cookingFetchedTerminal"||(int?)terminal.Attribute("version")!=1||terminal.Attributes().Count()!=13||terminal.Nodes().Any()||
            (string)terminal.Attribute("child")!=gridRecord.Operation.ToString("N")||
            (long?)terminal.Attribute("request")!=(long?)grid.Attribute("fetchId")||
            (string)terminal.Attribute("world")!=(string)grid.Attribute("world")||
            (string)terminal.Attribute("owner")!=(string)grid.Attribute("owner")||
            (string)terminal.Attribute("creation")!=gridRecord.OwnerCreation||
            (int?)terminal.Attribute("deaths")!=(int?)grid.Attribute("deaths")||
            !TerminalHex((string)terminal.Attribute("sourceWorld"))||!TerminalHex((string)terminal.Attribute("sourceBinding"))||
            !Guid.TryParseExact((string)terminal.Attribute("sourceOperation"),"N",out var source)||source==Guid.Empty)return false;
        var expectedSource=RebirthRemoteResourceDurableJournal.Operation((string)terminal.Attribute("sourceWorld"),
            RebirthStablePlayerIdentity.ComputeStorageKey((string)grid.Attribute("owner")),gridRecord.OwnerCreation,1,(ulong)(long)grid.Attribute("fetchId"),RemoteResourceClientOperation.CookingPull);
        if(source!=expectedSource)return false;
        string action=new XElement("ownerAction",new XAttribute("domain","cooking-grid"),new XAttribute("child",gridRecord.Operation.ToString("N")),
            new XElement("original",new UTF8Encoding(false,true).GetString(gridRecord.Before))).ToString(SaveOptions.DisableFormatting);
        return (string)terminal.Attribute("gridHash")==RebirthRemoteResourceDurableJournal.Digest(action)&&
            (string)terminal.Attribute("ownerProof")==RebirthRemoteResourceDurableJournal.Digest(gridRecord.Operation.ToString("N")+"|2|"+grid.Element("afterActor").Value+"|"+action)&&
            (string)terminal.Attribute("disposition")==((string)grid.Attribute("action")=="fetched"?"grid-owned":"refunded");
    }
    private static bool HasFetchedTerminal(RebirthCamperTransferJournal.Record gridRecord)
        =>HasFetchedTerminal(gridRecord,out _);
    private static bool HasFetchedTerminal(RebirthCamperTransferJournal.Record gridRecord,out bool observed)
    {
        observed=false;
        try
        {
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingGridFetchedTerminals");
            FileAttributes attributes;
            try{attributes=File.GetAttributes(directory);}
            catch(FileNotFoundException){return false;}catch(DirectoryNotFoundException){return false;}
            if((attributes&FileAttributes.Directory)==0||(attributes&FileAttributes.ReparsePoint)!=0){observed=true;return false;}
            observed=Directory.EnumerateFileSystemEntries(directory,gridRecord.Operation.ToString("N")+".*").Any();
            var saved=RebirthCamperTransferJournal.ReadExisting(directory,gridRecord.Operation);
            observed|=saved!=null;
            return saved?.State==RebirthCamperTransferJournal.Phase.Applied&&saved.Vehicle==gridRecord.Vehicle&&saved.OwnerCreation==gridRecord.OwnerCreation&&saved.Revision==gridRecord.Revision&&
                saved.Before.SequenceEqual(saved.After)&&saved.Before.Length<=4096&&
                MatchesFetchedTerminal(gridRecord,RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(saved.Before),4096));
        }
        catch{observed=true;return false;}
    }
    internal static bool RetireFetchedTerminal(EntityPlayerLocal player,long request,string sourceWorld,string sourceOperation,string sourceBinding,
        string child,string gridHash,string ownerProof,string disposition)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||player?.world==null||!player.IsSpawned()||player.IsDead()||request<=0||
                !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
                !ReferenceEquals(player.world.GetEntity(player.entityId),player)||!Guid.TryParseExact(child,"N",out var operation)||operation==Guid.Empty||
                !TerminalHex(gridHash)||!TerminalHex(ownerProof)||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return false;
            if(!player.world.IsRemote()&&!RebirthCookingLegacyPull.MatchesLocalTerminal(player,request,sourceWorld,sourceOperation,sourceBinding,child,gridHash,ownerProof,disposition))return false;
            string directory=GameIO.GetSaveGameDir(),creation=RebirthCamperCharacterAccess.CurrentCreation(player);var world=player.world;int deaths=player.Died;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var channels=manager?.connectionToServer;
            var observation=new FetchedTerminalObservation{World=new WeakReference<World>(world),Actor=new WeakReference<EntityPlayerLocal>(player),
                Manager=manager,Connection=channels?.Length>0?channels[0]:null,Directory=directory,Creation=creation,Owner=identity.CanonicalId,NativeWorld=world.worldState.Guid,Deaths=deaths};
            if(!observation.Current())return false;
            bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(player.world,world)&&
                ReferenceEquals(world.GetPrimaryPlayer(),player)&&ReferenceEquals(world.GetEntity(player.entityId),player)&&!player.IsDead()&&player.Died==deaths&&
                GameIO.GetSaveGameDir()==directory&&RebirthCamperCharacterAccess.CurrentCreation(player)==creation&&
                RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)&&owner.CanonicalId==identity.CanonicalId;
            var records=Records().Where(record=>record.OwnerCreation==creation&&record.Operation==operation).ToArray();if(records.Length!=1)return false;
            var original=records[0];var grid=FetchedImage(original);
            if((long?)grid.Attribute("fetchId")!=request||(string)grid.Attribute("owner")!=identity.CanonicalId||
                (string)grid.Attribute("world")!=world.worldState.Guid||(int)grid.Attribute("deaths")>deaths)return false;
            var terminal=new XElement("cookingFetchedTerminal",new XAttribute("version",1),new XAttribute("child",child),new XAttribute("request",request),
                new XAttribute("world",world.worldState.Guid),new XAttribute("owner",identity.CanonicalId),new XAttribute("creation",creation),new XAttribute("deaths",(int)grid.Attribute("deaths")),
                new XAttribute("sourceWorld",sourceWorld),new XAttribute("sourceOperation",sourceOperation),new XAttribute("sourceBinding",sourceBinding),
                new XAttribute("gridHash",gridHash),new XAttribute("ownerProof",ownerProof),new XAttribute("disposition",disposition));
            if(!MatchesFetchedTerminal(original,terminal)||!Current()||!observation.Current())return false;
            byte[] bytes=Encoding.UTF8.GetBytes(terminal.ToString(SaveOptions.DisableFormatting));if(bytes.Length>4096)return false;
            var journal=FetchedTerminals();var saved=journal.Prepare(operation,original.Vehicle,creation,original.Revision,bytes,bytes);
            if(!saved.Before.SequenceEqual(bytes)||!saved.After.SequenceEqual(bytes)||!Current())return false;
            if(saved.State==RebirthCamperTransferJournal.Phase.Prepared)saved=journal.Advance(operation,saved.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(!Current())return false;
            if(saved.State==RebirthCamperTransferJournal.Phase.Uncertain)journal.Advance(operation,saved.State,RebirthCamperTransferJournal.Phase.Applied);
            if(!HasFetchedTerminal(original)||!Current())return false;
            var current=Journal().Read(operation);
            if(current==null||current.Vehicle!=original.Vehicle||current.OwnerCreation!=creation||current.Revision!=original.Revision||
                !current.Before.SequenceEqual(original.Before)||!current.After.SequenceEqual(original.After))return false;
            // Metadata only. Host phase two already verified the original native
            // owner file and committed phase four; never execute Apply here.
            if(current.State==RebirthCamperTransferJournal.Phase.Prepared)current=Journal().Advance(operation,current.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(!Current())return false;
            if(current.State==RebirthCamperTransferJournal.Phase.Uncertain)current=Journal().Advance(operation,current.State,RebirthCamperTransferJournal.Phase.Applied);
            if(!Current()||!observation.Current()||current.State!=RebirthCamperTransferJournal.Phase.Applied||!HasFetchedTerminal(current))return false;
            observation.BodyHash=RebirthRemoteResourceDurableJournal.Digest(new UTF8Encoding(false,true).GetString(current.Before));
            if(!fetchedTerminalObservations.ContainsKey(operation))fetchedTerminalOrder.Enqueue(operation);
            fetchedTerminalObservations[operation]=observation;
            while(fetchedTerminalObservations.Count>128)fetchedTerminalObservations.Remove(fetchedTerminalOrder.Dequeue());
            Active.RemoveAll(intent=>intent.Operation==operation&&ReferenceEquals(intent.WorldObject,world)&&intent.Owner==identity.CanonicalId&&intent.Creation==creation);
            return true;
        }
        catch{return false;}
    }
}
