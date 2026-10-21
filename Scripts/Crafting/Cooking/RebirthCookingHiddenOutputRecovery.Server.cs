using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Existing hidden native Output remains custody until saved native owner/drop
// delivery and saved empty Output. No client-authored item list is accepted.
internal static partial class RebirthCookingHiddenOutputRecovery
{
    private sealed class Publication:IDisposable
    {internal string Image;internal IDisposable Native;internal Func<bool> Saved;internal float Until;public void Dispose(){Native?.Dispose();Native=null;}}
    private static readonly Dictionary<Guid,Publication> publications=new Dictionary<Guid,Publication>();
    private static readonly Dictionary<string,RebirthLogisticsEndpointIdentity.Admission> identities=new Dictionary<string,RebirthLogisticsEndpointIdentity.Admission>();
    private static readonly Dictionary<Guid,bool> holds=new Dictionary<Guid,bool>();
    private static readonly HashSet<string> savedBirths=new HashSet<string>();
    private static readonly HashSet<Guid> entering=new HashSet<Guid>();
    private static World serverWorld;private static string serverSave;
    private static string DirectoryFor(string name)=>Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name);
    private static RebirthCamperTransferJournal Originals()=>new RebirthCamperTransferJournal(DirectoryFor("CookingHiddenOutputs"));
    private static RebirthCamperTransferJournal Deltas()=>new RebirthCamperTransferJournal(DirectoryFor("CookingHiddenOutputOwners"));
    private static RebirthCamperTransferJournal Acks()=>new RebirthCamperTransferJournal(DirectoryFor("CookingHiddenOutputAcks"));
    private static byte[] Bytes(XElement node)=>Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting));
    private static string Cells(ItemStack[] items)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(items));
    private static ItemStack[] Cells(string text)=>RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(text));
    private static bool Same(ItemStack[] a,ItemStack[] b)=>a!=null&&b!=null&&a.Length==b.Length&&a.Select((item,index)=>RebirthStationGridIngredients.IsSameStackSnapshot(item,b[index])).All(value=>value);
    private static void Scope()
    {
        var world=GameManager.Instance?.World;string save=GameIO.GetSaveGameDir();if(ReferenceEquals(serverWorld,world)&&serverSave==save)return;
        foreach(var p in publications.Values)p.Dispose();foreach(var i in identities.Values)i.Dispose();publications.Clear();identities.Clear();holds.Clear();savedBirths.Clear();serverWorld=world;serverSave=save;
    }
    internal static void TickServer()
    {
        // Scope changes must still clear old entries, even when idle.
        Scope();float now=UnityEngine.Time.realtimeSinceStartup;
        if(publications.Count==0&&identities.Count==0)return;
        foreach(var p in publications.ToArray())if(now>=p.Value.Until){p.Value.Dispose();publications.Remove(p.Key);}
        foreach(var i in identities.ToArray())if(i.Value.Closed){i.Value.Dispose();identities.Remove(i.Key);}
    }
    private static Guid Key(string world,string location,string birth)=>RebirthCookingRemoteAdmissionBackend.StationKey(world,"hidden-output|"+location+"|"+birth);
    private static bool HasHistory(string world,string location)
    {
        string directory=DirectoryFor("CookingHiddenOutputs");if(!Directory.Exists(directory))return false;
        foreach(var record in Originals().ReadForIndex())
        {var root=XElement.Parse(Encoding.UTF8.GetString(record.Before));if((string)root.Attribute("world")==world&&(string)root.Attribute("location")==location)return true;}
        return false;
    }
    private static bool Birth(EntityPlayer actor,TileEntityWorkstation station,string location,string creation,out string birth)
    {
        birth=null;if(station is RebirthCamperStationState){birth=location;return true;}
        RebirthLogisticsEndpointIdentity.Install();
        bool known=RebirthLogisticsEndpointIdentity.TryGetIncarnation(actor.world,station,out birth);
        // The native marker already identifies this exact live station. Queue/output
        // publication subsequently saves the complete chunk containing that marker.
        // Re-saving identity first creates a competing snapshot wait on the same chunk.
        if(known)
        {
            if(identities.TryGetValue(location,out var previous)){previous.Dispose();identities.Remove(location);}
            return true;
        }
        // Missing old identity is uncertainty, not permission to rebind an old
        // paid source. A genuinely fresh target must have no hidden history.
        string directory=DirectoryFor("CookingHiddenOutputs");
        if(!known&&Directory.Exists(directory))foreach(string file in Directory.EnumerateFiles(directory,"*.0.rcj"))
        {
            string name=Path.GetFileName(file);if(name.Length!=38||!Guid.TryParseExact(name.Substring(0,32),"N",out var id))throw new InvalidDataException();
            var old=Originals().Read(id);var root=XElement.Parse(Encoding.UTF8.GetString(old.Before));
            if((string)root.Attribute("world")==actor.world.worldState.Guid&&(string)root.Attribute("location")==location)return false;
        }
        if(!identities.TryGetValue(location,out var admitted))
        {
            if(identities.Count>=4)return false;
            bool Current()=>RebirthCookingRemoteAdmissionBackend.Current(actor,location,creation,out var live,out _)&&ReferenceEquals(live,station);
            admitted=RebirthLogisticsEndpointIdentity.Begin(actor.world,station,Current);if(admitted==null)return false;identities[location]=admitted;
        }
        if(!admitted.Current()){admitted.Dispose();identities.Remove(location);return false;}
        if(!admitted.Saved())return false;birth=admitted.Id;savedBirths.Add(birth);admitted.Dispose();identities.Remove(location);return true;
    }
    internal static bool TryStationBirth(EntityPlayer actor,TileEntityWorkstation station,string location,string creation,out string birth)
    {Scope();return Birth(actor,station,location,creation,out birth);}
    internal static bool StationBirthMatches(EntityPlayer actor,TileEntityWorkstation station,string location,XElement original)=>
        original.Attribute("birth")!=null&&(station is RebirthCamperStationState?(string)original.Attribute("birth")==location:
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(actor.world,station,out string actual)&&actual==(string)original.Attribute("birth"));
    private static byte[] AckBody(RebirthCamperTransferJournal.Record parent,RebirthCookingOwnerDelta.Plan plan)
        =>Bytes(new XElement("hiddenOutputAck",new XAttribute("version",1),new XAttribute("original",RebirthStationPublicationRecord.Digest(parent.Before)),new XAttribute("delta",plan.Operation.ToString("N"))));
    private static bool Exact(RebirthCamperTransferJournal.Record r,Guid operation,Guid parent,string creation,byte[] bytes)
        =>r!=null&&r.Operation==operation&&r.Vehicle==parent&&r.OwnerCreation==creation&&r.Revision==0&&r.Before.SequenceEqual(bytes)&&r.After.SequenceEqual(bytes);
    private static bool Acked(RebirthCamperTransferJournal.Record parent)
    {
        var ack=RebirthCamperTransferJournal.ReadExisting(DirectoryFor("CookingHiddenOutputAcks"),parent.Operation);
        if(ack==null)return false;var delta=Deltas().FindApplied(parent.Operation,parent.OwnerCreation).SingleOrDefault();if(delta==null)return false;
        var plan=RebirthCookingOwnerDelta.Decode(Encoding.UTF8.GetString(delta.Before));
        return ack.State==RebirthCamperTransferJournal.Phase.Applied&&Exact(ack,parent.Operation,parent.Vehicle,parent.OwnerCreation,AckBody(parent,plan));
    }
    internal static bool ServerHeld(TileEntityWorkstation station)
    {
        if(station==null||GameManager.Instance?.World?.IsRemote()!=false)return false;
        try
        {
            Scope();
            if(publications.Count==0&&entering.Count==0&&identities.Count==0&&
                !Directory.Exists(DirectoryFor("CookingHiddenOutputs")))return false;
            RebirthLogisticsEndpointIdentity.Install();string location=RebirthCookingStationLocator.Encode(station),birth;
            if(station is RebirthCamperStationState)birth=location;
            else if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(serverWorld,station,out birth))return HasHistory(serverWorld.worldState.Guid,location);
            var key=Key(serverWorld.worldState.Guid,location,birth);if(holds.TryGetValue(key,out var held))return held;
            if(!Directory.Exists(DirectoryFor("CookingHiddenOutputs")))return holds[key]=false;
            var journal=Originals();return holds[key]=journal.FindPending(key)!=null||journal.ReadForIndex().Any(record=>record.Vehicle==key&&record.State==RebirthCamperTransferJournal.Phase.Applied&&!Acked(record));
        }
        catch{return true;}
    }
    private static bool Published(Guid operation,EntityPlayer actor,TileEntityWorkstation station,string image,Func<bool> current)
    {
        if(publications.TryGetValue(operation,out var p)&&(p.Image!=image||UnityEngine.Time.realtimeSinceStartup>=p.Until)){p.Dispose();publications.Remove(operation);p=null;}
        if(p==null)
        {
            if(publications.Count>=4)return false;var native=RebirthCookingOutputPlanPublication.BeginOutputPublication(actor,station,current,out var saved);if(native==null)return false;
            p=new Publication{Image=image,Native=native,Saved=saved,Until=UnityEngine.Time.realtimeSinceStartup+25f};publications[operation]=p;
        }
        return current()&&p.Saved();
    }
    internal static string Handle(EntityPlayer actor,string request,string creation,string location,ItemValue hint)
    {
        Guid key=Guid.Empty;bool entered=false;
        try
        {
            Scope();if(!Guid.TryParseExact(request,"N",out _)||!RebirthCookingRemoteAdmissionBackend.Current(actor,location,creation,out var station,out _)||
                !RebirthWorldCharacterService.TryGetIdentity(actor,out var identity))return RebirthCookingRemoteAdmission.Pending;
            if((hint==null||!hint.HasMetadata("rebirth.cooking.hidden.original"))&&station.Output?.All(item=>item!=null&&item.IsEmpty())==true&&!HasHistory(actor.world.worldState.Guid,location))return "hidden-empty:"+request;
            if(!Birth(actor,station,location,creation,out var birth))return RebirthCookingRemoteAdmission.Pending;
            key=Key(actor.world.worldState.Guid,location,birth);if(!(entered=entering.Add(key)))return RebirthCookingRemoteAdmission.Pending;
            var journal=Originals();RebirthCamperTransferJournal.Record parent=null;
            if(hint!=null&&hint.TryGetMetadata("rebirth.cooking.hidden.original",out string offered))
            {if(!Guid.TryParseExact(offered,"N",out var id))return RebirthCookingRemoteAdmission.Pending;parent=journal.Read(id);if(parent==null||parent.Vehicle!=key)return RebirthCookingRemoteAdmission.Pending;}
            else parent=journal.FindPending(key)??journal.FindApplied(key,creation).Where(record=>!Acked(record)).SingleOrDefault();
            if(parent==null)
            {
                var output=station.Output;if(output==null||output.Length>byte.MaxValue||output.Any(item=>item==null||item.count<0))return RebirthCookingRemoteAdmission.Pending;
                if(output.All(item=>item.IsEmpty()))return "hidden-empty:"+request;
                var root=new XElement("cookingHiddenOutput",new XAttribute("version",1),new XAttribute("world",actor.world.worldState.Guid),new XAttribute("owner",identity.CanonicalId),
                    new XAttribute("creation",creation),new XAttribute("location",location),new XAttribute("birth",birth),new XAttribute("queue",RebirthCookingOutputPlanPublication.QueueKey(station)),
                    new XElement("before",Cells(output)),new XElement("after",Cells(ItemStack.CreateArray(output.Length))));
                byte[] bytes=Bytes(root);if(bytes.Length>4*1024*1024)return RebirthCookingRemoteAdmission.Pending;
                holds.Remove(key);var operation=Guid.NewGuid();journal.AssertAvailable(key,operation);parent=journal.Prepare(operation,key,creation,0,bytes,bytes);holds[key]=true;
            }
            var original=XElement.Parse(Encoding.UTF8.GetString(parent.Before));
            if(!parent.Before.SequenceEqual(parent.After)||parent.OwnerCreation!=creation||(string)original.Attribute("world")!=actor.world.worldState.Guid||
                (string)original.Attribute("owner")!=identity.CanonicalId||(string)original.Attribute("location")!=location||(string)original.Attribute("birth")!=birth)return RebirthCookingRemoteAdmission.Pending;
            string before=original.Element("before").Value,after=original.Element("after").Value;var enteredWorld=actor.world;string save=GameIO.GetSaveGameDir();
            bool Current(string image)=>GameIO.GetSaveGameDir()==save&&ReferenceEquals(GameManager.Instance?.World,enteredWorld)&&
                RebirthCookingRemoteAdmissionBackend.Current(actor,location,creation,out var live,out _)&&ReferenceEquals(live,station)&&
                (station is RebirthCamperStationState||RebirthLogisticsEndpointIdentity.TryGetIncarnation(enteredWorld,station,out var id)&&id==birth)&&
                RebirthCookingOutputPlanPublication.QueueKey(station)==(string)original.Attribute("queue")&&Cells(station.Output)==image;
            var deltas=Deltas();var applied=deltas.FindApplied(parent.Operation,creation).ToArray();if(applied.Length>1)return RebirthCookingRemoteAdmission.Pending;
            var delta=applied.SingleOrDefault()??deltas.FindPending(parent.Operation);
            string Reply(string phase,RebirthCamperTransferJournal.Record record)=>phase+parent.Operation.ToString("N")+":"+Convert.ToBase64String(record.Before);
            if(parent.State==RebirthCamperTransferJournal.Phase.Applied)
            {
                if(delta?.State!=RebirthCamperTransferJournal.Phase.Applied)return RebirthCookingRemoteAdmission.Pending;
                var terminal=RebirthCookingOwnerDelta.Decode(Encoding.UTF8.GetString(delta.Before));
                if(terminal.Owner!=identity.CanonicalId||terminal.Creation!=creation||terminal.World!=actor.world.worldState.Guid)return RebirthCookingRemoteAdmission.Pending;
                if(terminal.Overflow!=null&&!RebirthCookingOverflowOriginal.Terminal(parent,actor,identity.CanonicalId,creation,terminal.Overflow,terminal.Deaths,terminal.OverflowOwnerEntity))return RebirthCookingRemoteAdmission.Pending;
                if(hint!=null&&hint.TryGetMetadata("rebirth.cooking.hidden.ack",out string ackId)&&ackId==terminal.Operation.ToString("N"))
                {
                    var acks=Acks();byte[] body=AckBody(parent,terminal);var ack=acks.Read(parent.Operation);
                    if(ack==null)
                    {
                        if(!Current(after)||terminal.Deaths>actor.Died||terminal.Deaths==actor.Died&&!RebirthCookingOwnerDelta.Saved(identity,terminal,3))return Reply("hidden-done:",delta);
                        ack=acks.Prepare(parent.Operation,key,creation,0,body,body);
                    }
                    if(!Exact(ack,parent.Operation,key,creation,body)||ack.State==RebirthCamperTransferJournal.Phase.Rejected)return RebirthCookingRemoteAdmission.Pending;
                    if(ack.State==RebirthCamperTransferJournal.Phase.Prepared)ack=acks.Advance(ack.Operation,ack.State,RebirthCamperTransferJournal.Phase.Uncertain);
                    if(ack.State==RebirthCamperTransferJournal.Phase.Uncertain)acks.Advance(ack.Operation,ack.State,RebirthCamperTransferJournal.Phase.Applied);
                    if(!Acked(parent))return RebirthCookingRemoteAdmission.Pending;holds.Remove(key);return "hidden-acked:"+parent.Operation.ToString("N");
                }
                return Reply("hidden-done:",delta);
            }
            if(parent.State==RebirthCamperTransferJournal.Phase.Prepared)
            {
                if(!Current(before)||!Published(parent.Operation,actor,station,before,()=>Current(before)))return RebirthCookingRemoteAdmission.Pending;
                parent=journal.Advance(parent.Operation,parent.State,RebirthCamperTransferJournal.Phase.Uncertain);
            }
            if(parent.State!=RebirthCamperTransferJournal.Phase.Uncertain||!Current(before)&&!Current(after))return RebirthCookingRemoteAdmission.Pending;
            if(hint!=null&&hint.TryGetMetadata("rebirth.cooking.hidden.delta",out string expected))
            {
                if(!Guid.TryParseExact(expected,"N",out var expectedId))return RebirthCookingRemoteAdmission.Pending;var old=deltas.Read(expectedId);
                if(old==null||old.Vehicle!=parent.Operation||old.OwnerCreation!=creation)return RebirthCookingRemoteAdmission.Pending;
                if(old.State==RebirthCamperTransferJournal.Phase.Rejected){RebirthCookingOwnerDelta.ObserveRejected(identity,old);return Reply("hidden-retry:",old);}
            }
            RebirthCookingOwnerDelta.Plan plan;
            if(delta==null)
            {
                if(!Current(before))return RebirthCookingRemoteAdmission.Pending;
                foreach(var rejected in deltas.FindRejected(parent.Operation,creation))if(!RebirthCookingOwnerDelta.ObserveRejected(identity,rejected))return Reply("hidden-retry:",rejected);
                if(!RebirthCamperStationPlayerSavedSource.TryCopySavedSource(identity,out var bag,out var belt,out var locks)||
                    !RebirthRecyclerOwnerDelivery.PlanWithOverflow(bag,belt,locks,null,actor.inventory.Length,Cells(before).Where(item=>!item.IsEmpty()).ToArray(),out var afterBag,out var afterBelt,out var overflow))return RebirthCookingRemoteAdmission.Pending;
                plan=new RebirthCookingOwnerDelta.Plan{Operation=Guid.NewGuid(),World=actor.world.worldState.Guid,Owner=identity.CanonicalId,Creation=creation,Deaths=actor.Died,OverflowOwnerEntity=actor.entityId,
                    SourceHash=RebirthCookingRemoteAdmission.SourceHash(bag,belt,locks,actor.inventory.Length),BeforeBag=bag,AfterBag=afterBag,BeforeBelt=belt,AfterBelt=afterBelt,Overflow=overflow.Length==0?null:overflow};
                byte[] bytes=Encoding.UTF8.GetBytes(RebirthCookingOwnerDelta.Encode(plan));deltas.AssertAvailable(parent.Operation,plan.Operation);delta=deltas.Prepare(plan.Operation,parent.Operation,creation,0,bytes,bytes);
            }
            else plan=RebirthCookingOwnerDelta.Decode(Encoding.UTF8.GetString(delta.Before));
            if(plan.Owner!=identity.CanonicalId||plan.Creation!=creation||plan.World!=actor.world.worldState.Guid||delta.State!=RebirthCamperTransferJournal.Phase.Applied&&plan.Deaths!=actor.Died)return RebirthCookingRemoteAdmission.Pending;
            if(delta.State==RebirthCamperTransferJournal.Phase.Prepared)
            {
                if(RebirthCookingOwnerDelta.Saved(identity,plan,1))delta=deltas.Advance(delta.Operation,delta.State,RebirthCamperTransferJournal.Phase.Uncertain);
                else if(RebirthCookingOwnerDelta.TryReadUnspentSource(identity,plan,out var bag,out var belt,out var locks)&&RebirthCookingRemoteAdmission.SourceHash(bag,belt,locks,actor.inventory.Length)!=plan.SourceHash)
                {delta=deltas.Advance(delta.Operation,delta.State,RebirthCamperTransferJournal.Phase.Rejected);return Reply("hidden-retry:",delta);}
            }
            if(delta.State==RebirthCamperTransferJournal.Phase.Uncertain&&RebirthCookingOwnerDelta.Saved(identity,plan,2))delta=deltas.Advance(delta.Operation,delta.State,RebirthCamperTransferJournal.Phase.Applied);
            if(delta.State!=RebirthCamperTransferJournal.Phase.Applied)return Reply(delta.State==RebirthCamperTransferJournal.Phase.Uncertain?"hidden:1:":"hidden:0:",delta);
            if(plan.Overflow!=null&&!RebirthCookingOverflowOriginal.Advance(parent,actor,identity.CanonicalId,creation,plan.Overflow,plan.Deaths,plan.OverflowOwnerEntity,()=>Current(before)||Current(after)))return RebirthCookingRemoteAdmission.Pending;
            if(Current(before)){station.Output=Cells(after);station.setModified();}
            if(!Current(after)||!Published(parent.Operation,actor,station,after,()=>Current(after)))return RebirthCookingRemoteAdmission.Pending;
            journal.Advance(parent.Operation,parent.State,RebirthCamperTransferJournal.Phase.Applied);return Reply("hidden-done:",delta);
        }
        catch(Exception error){Log.Warning("[Cooking] Hidden output original retained: "+error.GetType().Name);return RebirthCookingRemoteAdmission.Pending;}
        finally{if(entered)entering.Remove(key);}
    }
}
