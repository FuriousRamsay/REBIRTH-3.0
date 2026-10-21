using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    // Positive Prepared custody is a real original, never absence of an escape
    // receipt. Its immutable native saved Before is armed before refund grant.
    private sealed class CancellationHostCustody:IRebirthRemoteResourceRetainedNativeBuffers
    {
        internal readonly object Gate=new object();internal Guid Operation,Source;internal string Creation,Root,RetirementRoot;
        internal byte[] Anchor,Snapshot,RetirementBytes;internal readonly HashSet<string> Refusals=new HashSet<string>(StringComparer.Ordinal);internal string RetirementCause;internal bool Closing,Closed,EarlyEscaped;internal int Entered,MaxEscapeBytes;
        public bool IsDrained{get{lock(Gate)return Closed&&Entered==0&&Anchor==null&&Snapshot==null&&RetirementBytes==null;}}
    }
    private sealed class CancellationHostEscape
    {internal CancellationHostCustody Custody;internal string Method;internal Guid Key;internal byte[] Command;internal bool Finished;}
    private static readonly object cancellationHostCustodyGate=new object();
    private static readonly ConditionalWeakTable<TileEntityWorkstation,CancellationHostCustody> cancellationHostCustodies=new ConditionalWeakTable<TileEntityWorkstation,CancellationHostCustody>();
    private static readonly List<CancellationHostCustody> cancellationRetiringCustodies=new List<CancellationHostCustody>();
    private static bool HasCancellationHostCustody(TileEntityWorkstation station)
    {if(!cancellationHostCustodies.TryGetValue(station,out var custody))return false;lock(custody.Gate)return custody.Anchor!=null&&!custody.Closed&&!custody.Closing;}
    private static RebirthRemoteResourceClientOriginalJournal HostCustodyJournal(CancellationHostCustody custody,bool escape)
        =>HostCustodyJournalAt(escape?custody.RetirementRoot:custody.Root,custody.Operation,escape?custody.MaxEscapeBytes:custody.Anchor.Length);
    private static RebirthRemoteResourceClientOriginalJournal HostCustodyJournalAt(string root,Guid operation,int maximum)
    {
        CompletionAncestors(root);int count=0;
        foreach(string folder in new[]{root,Path.Combine(root,"completed"),Path.Combine(root,"completed",operation.ToString("N")),Path.Combine(root,"failedCandidates")})
        {
            CompletionAncestors(folder);if(!Directory.Exists(folder))continue;
            foreach(string path in Directory.EnumerateFileSystemEntries(folder,operation.ToString("N")+"*"))
            {
                if(++count>32)throw new InvalidDataException();
                if(folder==Path.Combine(root,"completed")&&Directory.Exists(path)&&Path.GetFileName(path)==operation.ToString("N"))continue;
                if(Directory.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||new FileInfo(path).Length>2L*maximum+8192||folder.EndsWith("failedCandidates",StringComparison.Ordinal))throw new InvalidDataException();
            }
        }
        return new RebirthRemoteResourceClientOriginalJournal(root);
    }
    private static int MeasureHostCustody(XElement body)
    {
        using(var counter=new RebirthRemoteResourcePlacedStationStreams.Counter(MaximumResultBytes))
        using(var writer=System.Xml.XmlWriter.Create(counter,new System.Xml.XmlWriterSettings{Encoding=new UTF8Encoding(false,true),OmitXmlDeclaration=true,Indent=false,CloseOutput=false}))
        {body.WriteTo(writer);writer.Flush();return checked((int)counter.Length);}
    }
    private static bool HostCustodyRecord(CancellationHostCustody custody,RebirthCamperTransferJournal.Record record,byte[] bytes,RebirthCamperTransferJournal.Phase phase)
        =>record!=null&&record.Operation==custody.Operation&&record.Vehicle==custody.Source&&record.OwnerCreation==custody.Creation&&record.Revision==0&&
            record.State==phase&&record.Before.SequenceEqual(bytes)&&record.After.SequenceEqual(bytes);
    private static bool ArmPendingCancellationHostCustody(CancellationServer original)
    {
        if(original.HostCustody!=null)return false;
        if(!original.Current()||!original.Memory.Reserve(checked(original.Memory.ReservedBytes+65536)))return false;
        var custody=new CancellationHostCustody{Operation=original.Operation,Source=original.Source,Creation=original.Original.Creation,
            Root=Path.Combine(original.Directory,"REBIRTH","NativePlacedCancellationHostCustody"),RetirementRoot=Path.Combine(original.Directory,"REBIRTH","NativePlacedCancellationHostRetirement")};
        lock(cancellationHostCustodyGate)
        {
            if(cancellationHostCustodies.TryGetValue(original.Station,out var prior)&&!prior.Closed)return false;
            cancellationHostCustodies.Remove(original.Station);cancellationHostCustodies.Add(original.Station,custody);original.HostCustody=custody;return true;
        }
    }
    private static bool EnsureCancellationHostCustody(CancellationServer original)
    {
        var pending=original.HostCustody;
        if(pending==null)return false;
        lock(pending.Gate){if(pending.EarlyEscaped||pending.Closing||pending.Closed)return false;if(pending.Anchor!=null)return CancellationHostCustodyCurrent(original)&&RegisterReceipt1BeforeLoad(original);}
        if(original.CustodyEscaped||!original.Current()||!original.BeforeSaved||original.Permission==null||!original.FullTile(original.BeforeTile)||
            !ReadCancellationSnapshot(original,false,out var saved)||!ReadCancellationAccepted(original,original.Accepted))return false;
        if(original.SnapshotBudget==null&&!CancellationSnapshotBudget.TryOpen(original,out original.SnapshotBudget))return false;
        var accepted=CancellationJournal(original.Operation,"Accepted").Read(original.Operation);
        if(!original.Memory.Reserve(checked(original.Memory.ReservedBytes+4L*accepted.Before.Length+65536)))return false;
        var body=new XElement("placedCancellationHostCustody",new XAttribute("version",2),new XAttribute("cancel",original.Operation.ToString("N")),
            new XAttribute("source",original.Source.ToString("N")),new XAttribute("binding",original.Binding),new XAttribute("birth",original.Birth),
            new XAttribute("world",original.Actor.world.worldState.Guid),new XAttribute("owner",original.Original.Owner),new XAttribute("creation",original.Original.Creation),
            new XAttribute("deaths",original.Original.Deaths),new XElement("request",original.Header),new XElement("permission",original.Permission),
            new XElement("acceptedNativeOriginal",Encoding.UTF8.GetString(accepted.Before)),new XElement("savedBefore",saved.Canonical));
        int size=MeasureHostCustody(body);
        if(size<1||size>MaximumResultBytes/2||!original.Memory.Reserve(checked(original.Memory.ReservedBytes+96L*size+4L*saved.Snapshot.Length+65536)))return false;
        var custody=new CancellationHostCustody{Operation=original.Operation,Source=original.Source,Creation=original.Original.Creation,
            Root=Path.Combine(original.Directory,"REBIRTH","NativePlacedCancellationHostCustody"),
            RetirementRoot=Path.Combine(original.Directory,"REBIRTH","NativePlacedCancellationHostRetirement"),Anchor=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting)),Snapshot=saved.Snapshot};
        custody.MaxEscapeBytes=MeasureHostCustody(new XElement("placedCancellationHostRetirement",new XAttribute("version",1),new XAttribute("cancel",custody.Operation.ToString("N")),
            new XAttribute("key",Guid.Empty.ToString("N")),new XAttribute("decision","refuse-original"),new XAttribute("boundary",new string('X',512)),new XElement("completeCustodyOriginal",Encoding.UTF8.GetString(custody.Anchor))));
        if(custody.MaxEscapeBytes>MaximumResultBytes||!original.Memory.Reserve(checked(original.Memory.ReservedBytes+40L*custody.MaxEscapeBytes)))return false;
        lock(pending.Gate)
        {
            if(pending.EarlyEscaped||pending.Closing||pending.Closed||pending.Anchor!=null||!original.FullTile(original.BeforeTile))return false;
            if(!original.SnapshotBudget.AttachHostCustody(pending))return false;
            pending.Anchor=custody.Anchor;pending.Snapshot=custody.Snapshot;pending.MaxEscapeBytes=custody.MaxEscapeBytes;
            var record=HostCustodyJournal(pending,false).Prepare(pending.Operation,pending.Source,pending.Creation,0,pending.Anchor,pending.Anchor);
            return HostCustodyRecord(pending,record,pending.Anchor,RebirthCamperTransferJournal.Phase.Prepared)&&RegisterReceipt1BeforeLoad(original);
        }
    }
    private static bool CancellationHostCustodyCurrent(CancellationServer original)
    {
        var custody=original.HostCustody;if(custody==null||original.CustodyEscaped)return false;
        lock(custody.Gate)
        {
            if(custody.EarlyEscaped||custody.Closing||custody.Closed||custody.Anchor==null)return false;
            lock(cancellationHostCustodyGate)if(!cancellationHostCustodies.TryGetValue(original.Station,out var actual)||!ReferenceEquals(actual,custody))return false;
            return HostCustodyRecord(custody,HostCustodyJournal(custody,false).Read(custody.Operation),custody.Anchor,original.QueueAttempted?RebirthCamperTransferJournal.Phase.Uncertain:RebirthCamperTransferJournal.Phase.Prepared);
        }
    }
    // This runs inside the qualified actual native factory/read scope BEFORE
    // OnReadComplete or LoadChunk exposes the constructed object. No World API.
    private static bool BindCancellationHostCustody(CancellationLoading loading)
    {
        var custody=loading.Original.Original?.HostCustody??loading.Original.Restart?.HostCustody;if(custody==null)return true;
        lock(custody.Gate)
        {
            if(custody.Closed||custody.Closing||loading.Tile==null||loading.Original.Before!=null&&!custody.Snapshot.SequenceEqual(loading.Original.Snapshot))return false;
            lock(cancellationHostCustodyGate)
            {
                if(cancellationHostCustodies.TryGetValue(loading.Tile,out var prior)&&!ReferenceEquals(prior,custody)&&!prior.Closed)return false;
                cancellationHostCustodies.Remove(loading.Tile);cancellationHostCustodies.Add(loading.Tile,custody);return true;
            }
        }
    }
    private static bool WriteCancellationCustodyRetirement(CancellationHostCustody custody,string method)
    {
        if(custody.Closed||custody.Anchor==null)return true;
        if(method==null||method.Length>512)throw new InvalidDataException();
        if(custody.RetirementBytes==null)
        {
            custody.RetirementCause="lifecycle-retirement";
            custody.RetirementBytes=Encoding.UTF8.GetBytes(new XElement("placedCancellationHostRetirement",new XAttribute("version",1),
                new XAttribute("cancel",custody.Operation.ToString("N")),new XAttribute("cause",custody.RetirementCause),
                new XElement("completeCustodyOriginal",Encoding.UTF8.GetString(custody.Anchor))).ToString(SaveOptions.DisableFormatting));
            if(custody.RetirementBytes.Length>custody.MaxEscapeBytes)throw new InvalidDataException();
        }
        var escaped=HostCustodyJournal(custody,true).Prepare(custody.Operation,custody.Source,custody.Creation,0,custody.RetirementBytes,custody.RetirementBytes);
        if(!HostCustodyRecord(custody,escaped,custody.RetirementBytes,escaped.State))return false;
        if(escaped.State==RebirthCamperTransferJournal.Phase.Prepared)escaped=HostCustodyJournal(custody,true).Advance(custody.Operation,escaped.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(escaped.State!=RebirthCamperTransferJournal.Phase.Uncertain&&escaped.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
        var journal=HostCustodyJournal(custody,false);var record=journal.Prepare(custody.Operation,custody.Source,custody.Creation,0,custody.Anchor,custody.Anchor);
        if(record==null||!HostCustodyRecord(custody,record,custody.Anchor,record.State))return false;
        // Metadata retirement is not an attempted gameplay effect. Preserve a
        // positive never-entered v2 original across clean world shutdown. Only
        // EnterCancellationCustodyQueueAttempt may consume its Prepared phase.
        return (record.State==RebirthCamperTransferJournal.Phase.Prepared||record.State==RebirthCamperTransferJournal.Phase.Uncertain)&&
            HostCustodyRecord(custody,record,custody.Anchor,record.State);
    }
    private static CancellationHostEscape WriteCancellationHostRefusal(CancellationHostCustody custody,string boundary,bool completedDecision)
    {
        if(custody.Refusals.Contains(boundary))return null;
        if(boundary.Length>512||custody.Refusals.Count>=16)throw new InvalidDataException();
        Guid key;using(var sha=System.Security.Cryptography.SHA256.Create())
        {var digest=sha.ComputeHash(Encoding.UTF8.GetBytes(custody.Operation.ToString("N")+"|host-custody-refusal|"+boundary));var id=new byte[16];Buffer.BlockCopy(digest,0,id,0,16);key=new Guid(id);}
        var bytes=Encoding.UTF8.GetBytes(new XElement("placedCancellationHostRefusal",new XAttribute("version",1),new XAttribute("cancel",custody.Operation.ToString("N")),
            new XAttribute("key",key.ToString("N")),new XAttribute("boundary",boundary),new XAttribute("decision","refuse-original"),
            new XElement("completeCustodyOriginal",Encoding.UTF8.GetString(custody.Anchor))).ToString(SaveOptions.DisableFormatting));
        if(bytes.Length>custody.MaxEscapeBytes)throw new InvalidDataException();
        var root=Path.Combine(Path.GetDirectoryName(custody.Root),"NativePlacedCancellationHostRefusal");
        var journal=HostCustodyJournalAt(root,key,custody.MaxEscapeBytes);var record=journal.Prepare(key,custody.Source,custody.Creation,0,bytes,bytes);
        if(record.Operation!=key||record.Vehicle!=custody.Source||record.OwnerCreation!=custody.Creation||record.Revision!=0||!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))throw new InvalidDataException();
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(key,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(completedDecision)
        {
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(key,record.State,RebirthCamperTransferJournal.Phase.Applied);
            if(record.State!=RebirthCamperTransferJournal.Phase.Applied)throw new InvalidDataException();
            custody.Refusals.Add(boundary);return null;
        }
        if(record.State==RebirthCamperTransferJournal.Phase.Applied){custody.Refusals.Add(boundary);return null;}
        if(record.State!=RebirthCamperTransferJournal.Phase.Uncertain)throw new InvalidDataException();
        custody.Entered++;return new CancellationHostEscape{Custody=custody,Method=boundary,Key=key,Command=bytes};
    }
    internal static bool RefuseCancellationHostDamage(TileEntityWorkstation station)
    {
        if(station==null)return false;CancellationHostCustody custody;
        lock(cancellationHostCustodyGate)if(!cancellationHostCustodies.TryGetValue(station,out custody))return false;
        lock(custody.Gate)
        {
            if(custody.Closed||custody.Anchor==null)return false;
            try{WriteCancellationHostRefusal(custody,"native-damage",true);}catch{/* Exact financial custody stays held. */}
            return true;
        }
    }
    private static bool EnterCancellationHostEscape(TileEntityWorkstation station,MethodBase method,out object state,string command=null)
    {
        state=null;CancellationHostCustody custody;
        lock(cancellationHostCustodyGate)if(!cancellationHostCustodies.TryGetValue(station,out custody))return true;
        lock(custody.Gate)
        {
            if(custody.Closed)return true;
            if(custody.Anchor==null){custody.EarlyEscaped=true;return true;} // No financial grant can follow this pending original.
            // A durable escape cannot revoke an in-flight client refund grant.
            // Keep exact custody and refuse the boundary until release.
            try{state=WriteCancellationHostRefusal(custody,method.DeclaringType.FullName+"."+method.Name+(command==null?"":"["+command+"]"),false);}
            catch{/* Refuse the native body even when attempt WAL fails. */}
            return false;
        }
    }
    private static void FinishCancellationHostEscape(object state,bool runOriginal)
    {
        if(state is not CancellationHostEscape entered)return;var custody=entered.Custody;
        lock(custody.Gate)
        {
            if(entered.Finished)return;entered.Finished=true;
            try
            {
                if(!runOriginal&&!custody.Closed&&entered.Command!=null)
                {
                    var root=Path.Combine(Path.GetDirectoryName(custody.Root),"NativePlacedCancellationHostRefusal");
                    var journal=HostCustodyJournalAt(root,entered.Key,custody.MaxEscapeBytes);var record=journal.Read(entered.Key);
                    if(record?.State==RebirthCamperTransferJournal.Phase.Uncertain&&record.Vehicle==custody.Source&&record.OwnerCreation==custody.Creation&&
                        record.Revision==0&&record.Before.SequenceEqual(entered.Command)&&record.After.SequenceEqual(entered.Command))
                    {journal.Advance(entered.Key,record.State,RebirthCamperTransferJournal.Phase.Applied);custody.Refusals.Add(entered.Method);}
                }
            }
            catch{/* Native original was refused; incomplete refusal metadata grants nothing. */}
            finally{entered.Command=null;custody.Entered--;if(custody.Closing)TryRetireCancellationHostCustody(custody);}
        }
    }
    private static bool TryRetireCancellationHostCustody(CancellationHostCustody custody)
    {
        try
        {
            if(custody.Closed)return true;
            if(!WriteCancellationCustodyRetirement(custody,"original-retirement")||custody.Entered!=0)return false;
            custody.Anchor=null;custody.Snapshot=null;custody.RetirementBytes=null;custody.Refusals.Clear();custody.Closed=true;return true;
        }
        catch{return false;}
    }
    private static void RetireCancellationHostCustody(CancellationServer original)
    {
        var custody=original.HostCustody;original.HostCustody=null;if(custody==null)return;
        lock(custody.Gate){custody.Closing=true;if(TryRetireCancellationHostCustody(custody))return;}
        lock(cancellationHostCustodyGate)if(!cancellationRetiringCustodies.Contains(custody))cancellationRetiringCustodies.Add(custody);
    }
    private static void PumpRetiringCancellationHostCustodies()
    {
        CancellationHostCustody[] items;lock(cancellationHostCustodyGate)items=cancellationRetiringCustodies.ToArray();
        foreach(var custody in items)lock(custody.Gate)if(TryRetireCancellationHostCustody(custody))lock(cancellationHostCustodyGate)cancellationRetiringCustodies.Remove(custody);
    }
    internal static bool EnterCancellationBlockEscape(string command,WorldBase world,Vector3i position,MethodBase method,bool runOriginal,out object state)
    {
        state=null;if(!runOriginal||!ProtocolActive||!ThreadManager.IsMainThread()||command!="extract"&&command!="take"&&command!="remove")return true;
        if(world is not World actual||actual.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,actual)||actual.GetTileEntity(position) is not TileEntityWorkstation station)return true;
        return EnterCancellationHostEscape(station,method,out state,command);
    }
    internal static void ObserveCancellationBlockEscape(bool runOriginal,object state)=>FinishCancellationHostEscape(state,runOriginal);
    private static bool HasCancellationCustodyHooks(Type native,string method,Type[] arguments,Type observer)
    {
        var original=HarmonyLib.AccessTools.Method(native,method,arguments);var info=original==null?null:HarmonyLib.Harmony.GetPatchInfo(original);
        return info!=null&&info.Prefixes.Any(value=>value.PatchMethod.DeclaringType==observer)&&
            info.Postfixes.Any(value=>value.PatchMethod.DeclaringType==observer)&&info.Finalizers.Any(value=>value.PatchMethod.DeclaringType==observer);
    }
    private static bool CancellationHostCustodyHooksInstalled=>RebirthWorkstationDamagePolicy.IsInstalled&&
        HasCancellationCustodyHooks(typeof(GameManager),nameof(GameManager.PickupBlockServer),new[]{typeof(Vector3i),typeof(BlockValue),typeof(int),typeof(PlatformUserIdentifierAbs)},typeof(RebirthPlacedOriginalNativePickupPatch))&&
        HasCancellationCustodyHooks(typeof(Block),nameof(Block.TakeItemWithTimerDone),new[]{typeof(TimerEventData)},typeof(CancellationHostPickup))&&
        HasCancellationCustodyHooks(typeof(BlockWorkstation),nameof(BlockWorkstation.OnBlockActivated),
        new[]{typeof(string),typeof(WorldBase),typeof(Vector3i),typeof(BlockValue),typeof(EntityPlayerLocal)},typeof(RebirthCampfireProcessingActivate))&&
        HasCancellationCustodyHooks(typeof(BlockWorkstation),nameof(BlockWorkstation.OnBlockRemoved),new[]{typeof(WorldBase),typeof(Chunk),typeof(Vector3i),typeof(BlockValue)},typeof(RebirthWorkstationDamagePolicy));
}
