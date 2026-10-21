using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Station-owned fuel intent. No player inventory setters, actor-payment receipts
// or Survivor rewards are involved. Native vehicle readback settles the intent.
internal static partial class RebirthCamperStationFuelOriginal
{
    private const int MaximumImage=4*1024*1024,MaximumProof=16*1024*1024,MaximumOperations=8192;
    private sealed class Frame
    {
        internal Guid Operation,Vehicle,Station;
        internal string World,Owner,Creation,Binding,Definition;
        internal int Deaths;internal bool Burning;internal byte[] Image;
        internal int Version=1,Batches,Tier;internal Guid Job;
        internal RebirthCamperRemoteStationCommandKind Kind;internal string Recipe;
    }
    private sealed class Pending
    {
        internal EntityVehicle Vehicle;internal RebirthCamperStationState Station;
        internal RebirthCamperModuleRuntime Runtime;internal World World;internal object WorldState;
        internal string Save,Root;internal Frame Before,After;
        internal RebirthCamperTransferJournal Journal;internal RebirthCamperTransferJournal.Record Record;
        internal RebirthCamperSavedVehicle.Expected Expected;
        internal bool Advancing;internal float Next;
    }
    private sealed class Scope{internal World World;internal string Save;}
    private static readonly Dictionary<EntityVehicle,Pending> pending=new Dictionary<EntityVehicle,Pending>();
    private static readonly ConditionalWeakTable<EntityVehicle,Scope> restored=new ConditionalWeakTable<EntityVehicle,Scope>();
    private static bool installed;
    internal static void Install()
    {if(installed)return;ModEvents.GameUpdate.RegisterHandler(Tick);installed=true;}
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        if(!ThreadManager.IsMainThread()||pending.Count==0)return;
        foreach(var p in pending.Values.ToArray())
        {
            if(!ReferenceEquals(GameManager.Instance?.World,p.World)||p.Save!=GameIO.GetSaveGameDir()||!ReferenceEquals(p.World.GetEntity(p.Vehicle.entityId),p.Vehicle))
            {p.Expected?.Dispose();pending.Remove(p.Vehicle);restored.Remove(p.Vehicle);continue;} // Original files remain for the next actual vehicle load.
            Advance(p);
        }
    }
    internal static bool IsHeld(RebirthCamperStationState station)
        =>BlocksVehicle(station?.BoundVehicle);
    internal static bool BlocksVehicle(EntityVehicle vehicle)
        =>vehicle?.world?.IsRemote()==false&&(!RestorePending(vehicle)||pending.ContainsKey(vehicle));
    private static string Hash(byte[] bytes)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    private static void Safe(string path)
    {for(var dir=new DirectoryInfo(Path.GetFullPath(path));dir!=null;dir=dir.Parent)if(dir.Exists&&(dir.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Fuel original path changed.");}
    private static string Root(string save,Guid vehicle)
    {
        if(string.IsNullOrWhiteSpace(save)||vehicle==Guid.Empty)throw new InvalidOperationException("Native save scope required.");
        string root=Path.Combine(save,"REBIRTH","CamperFuelOriginals",vehicle.ToString("N"));Safe(root);
        if(Directory.Exists(root))foreach(string child in Directory.EnumerateFileSystemEntries(root))
            if(!Directory.Exists(child)||!new[]{"journal","before","after"}.Contains(Path.GetFileName(child)))throw new InvalidDataException("Unrecognized fuel original root.");
        return root;
    }
    private static byte[] Encode(Frame f)
    {
        var body=new XElement("stationFuelOriginal",new XAttribute("version",f.Version),new XAttribute("operation",f.Operation.ToString("N")),
            new XAttribute("vehicle",f.Vehicle.ToString("N")),new XAttribute("station",f.Station.ToString("N")),new XAttribute("world",f.World),new XAttribute("owner",f.Owner),
            new XAttribute("creation",f.Creation),new XAttribute("binding",f.Binding),new XAttribute("deaths",f.Deaths),new XAttribute("burning",f.Burning),
            new XElement("definition",f.Definition),new XElement("image",Convert.ToBase64String(f.Image)));
        if(f.Version==2)body.Add(new XAttribute("kind",(byte)f.Kind),new XAttribute("job",f.Job.ToString("N")),new XAttribute("recipe",f.Recipe),new XAttribute("batches",f.Batches),new XAttribute("tier",f.Tier));
        return Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));
    }
    private static Frame Decode(byte[] bytes)
    {
        if(bytes==null||bytes.Length<1||bytes.Length>MaximumImage*2)throw new InvalidDataException();
        var node=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(bytes),MaximumImage*2);
        int version=(int?)node.Attribute("version")??0;
        if(node.Name!="stationFuelOriginal"||(version!=1&&version!=2)||node.Attributes().Count()!=(version==1?10:15)||node.Elements().Count()!=2)throw new InvalidDataException();
        var f=new Frame{Operation=Guid.ParseExact((string)node.Attribute("operation"),"N"),Vehicle=Guid.ParseExact((string)node.Attribute("vehicle"),"N"),Station=Guid.ParseExact((string)node.Attribute("station"),"N"),
            World=(string)node.Attribute("world"),Owner=(string)node.Attribute("owner"),Creation=(string)node.Attribute("creation"),Binding=(string)node.Attribute("binding"),Deaths=(int)node.Attribute("deaths"),Burning=(bool)node.Attribute("burning"),
            Definition=node.Element("definition").Value,Image=Convert.FromBase64String(node.Element("image").Value)};
        f.Version=version;
        if(version==2)
        {
            f.Kind=(RebirthCamperRemoteStationCommandKind)checked((byte)(int)node.Attribute("kind"));f.Job=Guid.ParseExact((string)node.Attribute("job"),"N");f.Recipe=(string)node.Attribute("recipe");f.Batches=(int)node.Attribute("batches");f.Tier=(int)node.Attribute("tier");
            if(f.Kind!=RebirthCamperRemoteStationCommandKind.Craft&&f.Kind!=RebirthCamperRemoteStationCommandKind.Cancel)throw new InvalidDataException();
            new RebirthCamperRemoteStationCommand(f.Kind,f.Operation,f.Job,f.Recipe,f.Batches,f.Tier);
        }
        if(f.Operation==Guid.Empty||f.Vehicle==Guid.Empty||f.Station==Guid.Empty||string.IsNullOrWhiteSpace(f.World)||string.IsNullOrWhiteSpace(f.Creation)||f.Creation.Length>512||f.Owner?.Length!=64||f.Binding?.Length!=64||
            f.Deaths<0||f.Image.Length<1||f.Image.Length>MaximumImage||f.Definition.Length>262144||!Encode(f).SequenceEqual(bytes))throw new InvalidDataException();return f;
    }
    private static void CheckPair(RebirthCamperTransferJournal.Record record,out Frame before,out Frame after)
    {
        before=Decode(record.Before);after=Decode(record.After);
        if(record.Operation!=before.Operation||record.Vehicle!=before.Vehicle||record.OwnerCreation!=before.Creation||before.Operation!=after.Operation||before.Vehicle!=after.Vehicle||before.Station!=after.Station||
            before.World!=after.World||before.Owner!=after.Owner||before.Creation!=after.Creation||before.Binding!=after.Binding||before.Deaths!=after.Deaths||before.Definition!=after.Definition||before.Version!=after.Version||
            before.Kind!=after.Kind||before.Job!=after.Job||before.Recipe!=after.Recipe||before.Batches!=after.Batches||before.Tier!=after.Tier)throw new InvalidDataException("Station original changed identity.");
        if(!RebirthNativeItemCodec.TryDecode(before.Definition,out var item)||item==null||RebirthNativeItemCodec.Encode(item)!=before.Definition)throw new InvalidDataException();
        var old=RebirthCamperStationSnapshot.Restore(before.Station,new ItemStack(item.Clone(),1),before.Image);
        var next=RebirthCamperStationSnapshot.Restore(after.Station,new ItemStack(item.Clone(),1),after.Image);
        if(old.IsBurning!=before.Burning||old.lastTickTime!=0||old.Revision!=record.Revision)throw new InvalidDataException("Fuel original preimage disagrees.");
        if(before.Version==2)
        {
            if(before.Burning!=after.Burning||!RebirthCamperStationNativeCrafting.ValidateStationInputOriginal(old,next,
                new RebirthCamperRemoteStationCommand(before.Kind,before.Operation,before.Job,before.Recipe,before.Batches,before.Tier)))throw new InvalidDataException("Invalid station-owned input change.");
            old.Input=next.Input;old.Queue=next.Queue;
        }
        else old.IsBurning=after.Burning;
        old.lastTickTime=0;old.setModified();
        if(next.IsBurning!=after.Burning||!RebirthCamperStationSnapshot.Capture(before.Station,old).SequenceEqual(after.Image))throw new InvalidDataException("Fuel original contains another effect.");
    }
    private static void CheckFiles(string directory,int maximum,Guid operation=default(Guid),long maximumBytes=1024L*1024*1024)
    {
        Safe(directory);if(!Directory.Exists(directory))return;int count=0;long bytes=0;
        foreach(string path in operation==Guid.Empty?Directory.EnumerateFileSystemEntries(directory):Directory.EnumerateFileSystemEntries(directory,operation.ToString("N")+"*"))
        {
            var file=new FileInfo(path);string name=file.Name;
            if(++count>maximum||!file.Exists||(file.Attributes&FileAttributes.ReparsePoint)!=0||file.Length<1||file.Length>2L*MaximumProof+8192||
                (bytes=checked(bytes+file.Length))>maximumBytes||name.Length!=38||!name.EndsWith(".rcj",StringComparison.Ordinal)||name[32]!='.'||name[34]!='.'||name[33]<'0'||name[33]>'3'||!Guid.TryParseExact(name.Substring(0,32),"N",out _))
                throw new InvalidDataException("Unrecognized or over-budget fuel original; preserve custody.");
        }
    }
    private static bool Current(Pending p)
    {
        if(!ThreadManager.IsMainThread()||p?.Vehicle?.world==null||p.Vehicle.world.IsRemote()||p.Save!=GameIO.GetSaveGameDir()||
            !ReferenceEquals(GameManager.Instance?.World,p.World)||!ReferenceEquals(p.Vehicle.world,p.World)||!ReferenceEquals(p.World.worldState,p.WorldState)||p.World.worldState?.Guid!=p.Before.World||
            !ReferenceEquals(p.World.GetEntity(p.Vehicle.entityId),p.Vehicle)||RebirthCamperEnergyRuntime.IsHeld(p.Vehicle)||!RebirthCamperModuleRuntime.TryGetExisting(p.Vehicle,out var runtime)||!ReferenceEquals(runtime,p.Runtime)||runtime.Identity!=p.Before.Vehicle||runtime.TransferPending)return false;
        var installed=runtime.Stations.Find(p.Before.Station);
        return ReferenceEquals(installed?.State,p.Station)&&installed.InstalledItem?.count==1&&RebirthNativeItemCodec.Encode(installed.InstalledItem.itemValue)==p.Before.Definition;
    }
    private static bool Matches(Pending p,Frame f)
        =>Current(p)&&RebirthCamperStationSnapshot.Capture(f.Station,p.Station).SequenceEqual(f.Image);
    private static void RequestCurrentSave(Pending p,Frame frame)
    {
        if(!Matches(p,frame))return;
        if(p.Expected!=null)
        {
            using(var live=RebirthCamperSavedVehicle.Capture(p.Vehicle))
                if(live.Bag.SequenceEqual(p.Expected.Bag)&&live.Entity.SequenceEqual(p.Expected.Entity))return;
            // A changed health/position/native body is evidence of a changed
            // observation, not a timeout. Keep that current body; never restore
            // unrelated vehicle state from the original expected snapshot.
            p.Expected.Dispose();p.Expected=null;
        }
        p.Runtime.Persist(true);
        if(Matches(p,frame))p.Expected=RebirthCamperSavedVehicle.RequestSave(p.Vehicle);
    }
    internal static bool Begin(EntityPlayer actor,RebirthCamperStationState station,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command,Action enteringPublication=null,ItemStack[] input=null,RecipeQueueItem[] queue=null)
    {
        try
        {
            bool inputCommand=command!=null&&(command.Kind==RebirthCamperRemoteStationCommandKind.Craft||command.Kind==RebirthCamperRemoteStationCommandKind.Cancel);
            bool CurrentLease()=>actor!=null&&!actor.IsDead()&&actor.world?.IsRemote()==false&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&
                creation==RebirthCamperCharacterAccess.CurrentCreation(actor)&&RebirthStationLiveAccess.TryGetMobileLease(actor,address,creation,out var held,out var currentLease)&&ReferenceEquals(held,station)&&ReferenceEquals(currentLease,lease);
            if(command==null||(!command.IsFuel&&!inputCommand)||inputCommand&&(RebirthSurvivorMode.IsEnabledForCurrentWorld()||input==null||queue==null)||actor==null||actor.IsDead()||actor.Died<0||station?.BoundVehicle==null||IsHeld(station)||
                !CurrentLease()||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return false;
            var runtime=RebirthCamperModuleRuntime.For(station.BoundVehicle);var installed=runtime.Stations.Find(address.Installation);
            if(runtime.Identity!=address.Vehicle||!ReferenceEquals(installed?.State,station)||installed.InstalledItem?.count!=1||RebirthCamperEnergyRuntime.IsHeld(station.BoundVehicle)||RebirthCamperEnergyCustody.FindPending(address.Vehicle)!=null)return false;
            using(var native=RebirthCamperSavedVehicle.Capture(station.BoundVehicle))
                if(native.Bag==null||native.Bag.Length>8*1024*1024||native.Entity==null||native.Entity.Length<1||native.Entity.Length>ushort.MaxValue)return false;
            string save=GameIO.GetSaveGameDir(),root=Root(save,address.Vehicle),directory=Path.Combine(root,"journal");
            // Reserve all three two-frame phase files before accepting intent.
            // Terminal histories stay immutable; capacity refusal is pre-effect.
            CheckFiles(Path.Combine(root,"before"),MaximumOperations*3,maximumBytes:1024L*1024*1024-6L*MaximumProof-32768);
            CheckFiles(Path.Combine(root,"after"),MaximumOperations*3,maximumBytes:1024L*1024*1024-6L*MaximumProof-32768);
            CheckFiles(directory,MaximumOperations*3,maximumBytes:1024L*1024*1024-12L*MaximumImage-32768);var journal=new RebirthCamperTransferJournal(directory);
            if(journal.Read(command.Operation)!=null||journal.FindPending(address.Vehicle)!=null||Directory.EnumerateFiles(directory,"*.0.rcj").Take(MaximumOperations).Count()>=MaximumOperations)return false;
            // Neutralize only the clock while entering durable station custody.
            // No stock, queue or burning state changes before Prepare/readback.
            station.lastTickTime=0;
            var before=new Frame{Operation=command.Operation,Vehicle=address.Vehicle,Station=address.Installation,World=actor.world.worldState.Guid,Owner=owner.StorageKey,Creation=creation,
                Binding=RebirthCamperRemoteStationCommandStatus.Binding(command,owner.StorageKey,creation,address),Deaths=actor.Died,Burning=station.IsBurning,
                Definition=RebirthNativeItemCodec.Encode(installed.InstalledItem.itemValue),Image=RebirthCamperStationSnapshot.Capture(address.Installation,station)};
            if(before.Image.Length>MaximumImage)return false;
            var next=RebirthCamperStationSnapshot.Restore(address.Installation,installed.InstalledItem.Clone(),before.Image);
            if(inputCommand)
            {
                before.Version=2;before.Kind=command.Kind;before.Job=command.Job;before.Recipe=command.RecipeName;before.Batches=command.Batches;before.Tier=command.Tier;
                next.Input=ItemStack.Clone(input);next.Queue=(RecipeQueueItem[])queue.Clone();
                if(!RebirthCamperStationNativeCrafting.ValidateStationInputOriginal(station,next,command))return false;
            }
            else next.IsBurning=command.Kind==RebirthCamperRemoteStationCommandKind.FuelOn;
            next.lastTickTime=0;next.setModified();
            var after=Decode(Encode(before));after.Burning=next.IsBurning;after.Image=RebirthCamperStationSnapshot.Capture(address.Installation,next);
            if(after.Image.Length>MaximumImage||!CurrentLease())return false;
            // Failed publication must invalidate the no-pending bootstrap too.
            // A staged/renamed original is discovered before the clock can run.
            enteringPublication?.Invoke();
            restored.Remove(station.BoundVehicle);
            var record=journal.Prepare(command.Operation,address.Vehicle,creation,station.Revision,Encode(before),Encode(after));
            CheckPair(record,out before,out after);
            var p=new Pending{Vehicle=station.BoundVehicle,Station=station,Runtime=runtime,World=actor.world,WorldState=actor.world.worldState,Save=save,Root=root,Before=before,After=after,Journal=journal,Record=record};
            pending.Add(p.Vehicle,p);return true;
        }
        catch(Exception error){Log.Warning("[CamperFuel] Original retained: "+error.GetType().Name);return false;}
    }
    // Runs before any station on this vehicle can process, including cold load.
    internal static bool RestorePending(EntityVehicle vehicle)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||vehicle?.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,vehicle.world)||!ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)||!RebirthCamperModuleRuntime.TryGetExisting(vehicle,out var runtime))return false;
            if(pending.TryGetValue(vehicle,out var existing))
            {
                if(ReferenceEquals(existing.Runtime,runtime)&&ReferenceEquals(existing.World,vehicle.world)&&ReferenceEquals(existing.WorldState,vehicle.world.worldState)&&existing.Save==GameIO.GetSaveGameDir()&&
                    ReferenceEquals(runtime.Stations.Find(existing.Before.Station)?.State,existing.Station))
                {Advance(existing);return true;}
                existing.Expected?.Dispose();pending.Remove(vehicle);restored.Remove(vehicle); // Rehydrate only from the retained original below.
            }
            string save=GameIO.GetSaveGameDir();
            if(restored.TryGetValue(vehicle,out var scope)&&ReferenceEquals(scope.World,vehicle.world)&&scope.Save==save)return true;
            string root=Root(save,runtime.Identity),directory=Path.Combine(root,"journal");CheckFiles(directory,MaximumOperations*3);
            if(Directory.Exists(directory))
            {
                var journal=new RebirthCamperTransferJournal(directory);RebirthCamperTransferJournal.Record unresolved=null;
                foreach(bool postimage in new[]{false,true})
                {
                    string proofs=Path.Combine(root,postimage?"after":"before");CheckFiles(proofs,MaximumOperations*3);
                    if(!Directory.Exists(proofs))continue;
                    foreach(string name in Directory.EnumerateFiles(proofs).Select(Path.GetFileName).Select(name=>name.Substring(0,32)).Distinct())
                    {
                        Guid operation=Guid.ParseExact(name,"N");var original=journal.Read(operation);var observation=RebirthCamperTransferJournal.ReadExisting(proofs,operation);
                        if(original==null||observation?.State==RebirthCamperTransferJournal.Phase.Rejected||!ValidateProof(observation,original,postimage,out _))throw new InvalidDataException("Orphan or conflicting native fuel observation.");
                    }
                }
                foreach(var record in journal.ReadForIndex())
                {
                    CheckPair(record,out var before,out var after);
                    if(before.World!=vehicle.world.worldState?.Guid||before.Vehicle!=runtime.Identity)throw new InvalidDataException("Foreign fuel original.");
                    if(record.State==RebirthCamperTransferJournal.Phase.Applied)
                    {if(!ReadProof(root,record,false,out _)||!ReadProof(root,record,true,out _))throw new InvalidDataException("Fuel terminal lacks saved native observation.");continue;}
                    if(record.State==RebirthCamperTransferJournal.Phase.Rejected)throw new InvalidDataException("Unsupported fuel rejection.");
                    if(unresolved!=null)throw new InvalidDataException("Conflicting fuel custody.");unresolved=record;
                }
                if(unresolved!=null)
                {
                    CheckPair(unresolved,out var before,out var after);var station=runtime.Stations.Find(before.Station)?.State;
                    if(station==null)throw new InvalidDataException("Fuel installation missing.");
                    var p=new Pending{Vehicle=vehicle,Station=station,Runtime=runtime,World=vehicle.world,WorldState=vehicle.world.worldState,Save=save,Root=root,Before=before,After=after,Journal=journal,Record=unresolved};
                    pending.Add(vehicle,p);Advance(p);
                }
            }
            else if(new[]{"before","after"}.Any(name=>Directory.Exists(Path.Combine(root,name))&&Directory.EnumerateFileSystemEntries(Path.Combine(root,name)).Any()))
                throw new InvalidDataException("Native fuel observations have no original journal.");
            restored.Remove(vehicle);restored.Add(vehicle,new Scope{World=vehicle.world,Save=save});return true;
        }
        catch(Exception){return false;} // Unknown files are custody, never unused.
    }
    private static void Advance(Pending p)
    {
        if(p.Advancing||UnityEngine.Time.realtimeSinceStartup<p.Next)return;p.Next=UnityEngine.Time.realtimeSinceStartup+.5f;p.Advancing=true;
        try
        {
            if(!Current(p))return;
            // The native saved image includes every installed station. Hold all
            // clocks before capturing it, not one frame later in TickMobile.
            for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
            {var held=p.Runtime.Stations.GetSlot(slot)?.State;if(held!=null)held.lastTickTime=0;}
            p.Record=p.Journal.Read(p.Before.Operation);CheckPair(p.Record,out _,out _);
            if(p.Record.State==RebirthCamperTransferJournal.Phase.Applied)
            {if(ReadProof(p.Root,p.Record,false,out _)&&ReadProof(p.Root,p.Record,true,out _))Release(p);return;}
            if(p.Record.State==RebirthCamperTransferJournal.Phase.Prepared)
            {
                if(!Matches(p,p.Before))return;
                if(!ReadProof(p.Root,p.Record,false,out _))
                {
                    RequestCurrentSave(p,p.Before);
                    if(!RebirthCamperSavedVehicle.Verify(p.Vehicle,p.Expected,()=>Matches(p,p.Before))||!PublishProof(p,false))return;
                }
                if(!Matches(p,p.Before))return;
                p.Record=p.Journal.Advance(p.Record.Operation,p.Record.State,RebirthCamperTransferJournal.Phase.Uncertain);p.Expected?.Dispose();p.Expected=null;
            }
            if(p.Record.State!=RebirthCamperTransferJournal.Phase.Uncertain||!ReadProof(p.Root,p.Record,false,out _))return;
            if(Matches(p,p.Before))
            {
                // A cold original may proceed only from the exact native primary
                // Before that is still loaded. A live match alone grants nothing.
                using(var before=RebirthCamperSavedVehicle.Capture(p.Vehicle))
                    if(!RebirthCamperSavedVehicle.Verify(p.Vehicle,before,()=>Matches(p,p.Before)))return;
                if(p.After.Version==1)p.Station.IsBurning=p.After.Burning;
                p.Station.lastTickTime=0;
                var installed=p.Runtime.Stations.Find(p.After.Station).InstalledItem;
                var next=RebirthCamperStationSnapshot.Restore(p.After.Station,installed.Clone(),p.After.Image);
                if(p.After.Version==2)
                {
                    // The entire Before image was just matched and read from the
                    // native primary. Apply only this original stock/queue delta.
                    p.Station.Input=ItemStack.Clone(next.Input);p.Station.Queue=next.Queue;
                }
                p.Station.RestoreRevision(next.Revision);if(!Matches(p,p.After))return;
                p.Expected?.Dispose();p.Expected=null;
            }
            if(!Matches(p,p.After))return;
            if(!ReadProof(p.Root,p.Record,true,out _))
            {
                // This also rebuilds native metadata after a prior Persist
                // threw with live After already installed.
                RequestCurrentSave(p,p.After);
                if(!RebirthCamperSavedVehicle.Verify(p.Vehicle,p.Expected,()=>Matches(p,p.After))||!PublishProof(p,true))return;
            }
            if(!Matches(p,p.After))return;
            p.Record=p.Journal.Advance(p.Record.Operation,p.Record.State,RebirthCamperTransferJournal.Phase.Applied);Release(p);
        }
        catch(Exception error){Log.Warning("[CamperFuel] Saved settlement retained: "+error.GetType().Name);}
        finally{p.Advancing=false;}
    }
    private static void Release(Pending p)
    {p.Expected?.Dispose();p.Expected=null;p.Station.lastTickTime=0;pending.Remove(p.Vehicle);if(p.After.Version==2)RebirthCamperStationWindowSession.MarkOriginalProjection(p.Station);}
    private static bool PublishProof(Pending p,bool after)
    {
        if(p.Expected?.Bag==null||p.Expected.Entity==null||p.Expected.Bag.Length>8*1024*1024||p.Expected.Entity.Length>ushort.MaxValue||!Matches(p,after?p.After:p.Before)||
            !RebirthCamperSavedVehicle.Verify(p.Vehicle,p.Expected,()=>Matches(p,after?p.After:p.Before)))return false;
        string directory=Path.Combine(p.Root,after?"after":"before");CheckFiles(directory,3,p.Record.Operation);
        var body=new XElement("savedStationFuel",new XAttribute("version",1),new XAttribute("operation",p.Record.Operation.ToString("N")),new XAttribute("vehicle",p.Record.Vehicle.ToString("N")),
            new XAttribute("creation",p.Record.OwnerCreation),new XAttribute("before",Hash(p.Record.Before)),new XAttribute("after",Hash(p.Record.After)),new XAttribute("postimage",after),
            new XAttribute("entityId",p.Expected.Id),new XAttribute("entityClass",p.Expected.Class),new XElement("bag",Convert.ToBase64String(p.Expected.Bag)),new XElement("entity",Convert.ToBase64String(p.Expected.Entity)));
        byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));if(bytes.Length>MaximumProof)return false;
        var journal=new RebirthCamperTransferJournal(directory);var record=journal.Read(p.Record.Operation);
        if(record==null)record=journal.Prepare(p.Record.Operation,p.Record.Vehicle,p.Record.OwnerCreation,0,bytes,bytes);
        // A partially published immutable observation is never replaced by a
        // later native save with different unrelated vehicle fields.
        if(!ValidateProof(record,p.Record,after,out _))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(record.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(record.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
        return ReadProof(p.Root,p.Record,after,out _)&&Matches(p,after?p.After:p.Before);
    }
    private static bool ValidateProof(RebirthCamperTransferJournal.Record proof,RebirthCamperTransferJournal.Record original,bool after,out string digest)
    {
        digest=null;if(proof==null||proof.Revision!=0||proof.Operation!=original.Operation||proof.Vehicle!=original.Vehicle||proof.OwnerCreation!=original.OwnerCreation||proof.Before.Length>MaximumProof||!proof.Before.SequenceEqual(proof.After))return false;
        var bytes=proof.Before;var node=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(bytes),MaximumProof);
        if(node.Name!="savedStationFuel"||node.Attributes().Count()!=9||node.Elements().Count()!=2||(int?)node.Attribute("version")!=1||(string)node.Attribute("operation")!=original.Operation.ToString("N")||
            (string)node.Attribute("vehicle")!=original.Vehicle.ToString("N")||(string)node.Attribute("creation")!=original.OwnerCreation||(string)node.Attribute("before")!=Hash(original.Before)||(string)node.Attribute("after")!=Hash(original.After)||
            (bool?)node.Attribute("postimage")!=after||(int?)node.Attribute("entityId") is not int id||id<0||(int?)node.Attribute("entityClass") is not int kind||kind<0)return false;
        byte[] bag=Convert.FromBase64String(node.Element("bag").Value),entity=Convert.FromBase64String(node.Element("entity").Value);
        if(bag.Length>8*1024*1024||entity.Length<1||entity.Length>ushort.MaxValue||Convert.ToBase64String(bag)!=node.Element("bag").Value||Convert.ToBase64String(entity)!=node.Element("entity").Value||
            !Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting)).SequenceEqual(bytes))return false;
        digest=Hash(bytes);return true;
    }
    private static bool ReadProof(string root,RebirthCamperTransferJournal.Record original,bool after,out string digest)
    {
        digest=null;string directory=Path.Combine(root,after?"after":"before");CheckFiles(directory,3,original.Operation);
        var proof=RebirthCamperTransferJournal.ReadExisting(directory,original.Operation);
        return proof?.State==RebirthCamperTransferJournal.Phase.Applied&&ValidateProof(proof,original,after,out digest);
    }
    internal static RebirthCamperRemoteStationCommandStatus.State ReadStatus(EntityPlayer actor,RebirthCamperStationAddress address,string creation,RebirthCamperRemoteStationCommand command,out string proof,out int deaths)
    {
        proof=null;deaths=-1;
        try
        {
            if(!ThreadManager.IsMainThread()||command==null||actor?.world?.IsRemote()!=false||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return RebirthCamperRemoteStationCommandStatus.State.Unknown;
            string root=Root(GameIO.GetSaveGameDir(),address.Vehicle),directory=Path.Combine(root,"journal");CheckFiles(directory,3,command.Operation);
            var record=RebirthCamperTransferJournal.ReadExisting(directory,command.Operation);if(record==null)return RebirthCamperRemoteStationCommandStatus.State.Unknown;
            CheckPair(record,out var before,out var after);
            if(before.World!=actor.world.worldState?.Guid||before.Owner!=owner.StorageKey||before.Creation!=creation||before.Vehicle!=address.Vehicle||before.Station!=address.Installation||
                before.Binding!=RebirthCamperRemoteStationCommandStatus.Binding(command,owner.StorageKey,creation,address)||
                (before.Version==1?(!command.IsFuel||after.Burning!=(command.Kind==RebirthCamperRemoteStationCommandKind.FuelOn)):
                    (command.Kind!=before.Kind||command.Job!=before.Job||command.RecipeName!=before.Recipe||command.Batches!=before.Batches||command.Tier!=before.Tier)))return RebirthCamperRemoteStationCommandStatus.State.Unknown;
            deaths=before.Deaths;
            if(record.State==RebirthCamperTransferJournal.Phase.Applied&&ReadProof(root,record,false,out _)&&ReadProof(root,record,true,out proof))return RebirthCamperRemoteStationCommandStatus.State.Applied;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared||record.State==RebirthCamperTransferJournal.Phase.Uncertain){proof=Hash(record.Before);return RebirthCamperRemoteStationCommandStatus.State.Pending;}
            return RebirthCamperRemoteStationCommandStatus.State.Unknown;
        }
        catch{proof=null;deaths=-1;return RebirthCamperRemoteStationCommandStatus.State.Unknown;}
    }
    internal static bool HasOriginal(RebirthCamperStationAddress address,Guid operation)
    {
        try{string root=Root(GameIO.GetSaveGameDir(),address.Vehicle);return new[]{"journal","before","after"}.Any(name=>{string path=Path.Combine(root,name);return Directory.Exists(path)&&Directory.EnumerateFileSystemEntries(path,operation.ToString("N")+"*").Any();});}
        catch{return true;} // An unreadable candidate cannot fall through to a different effect authority.
    }
}
