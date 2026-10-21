using System;
using System.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private static readonly object savedEarnedOwnerIssuer=new object();
    private static bool FitsSavedEarnedOwnerBeforeSources(RebirthRemoteResourcePlacedStationArtifactReader.Image artifact)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())return true;
        if(!RebirthProductiveEarnedUiQueueOwner.AcceptsSavedSource4||artifact==null)return false;
        var full=artifact.Copy();var saved=full.Element("savedBefore");var plan=full.Element("plan");
        if(saved==null||plan==null||(int?)saved.Attribute("tileLength") is not int before||before<1)return false;
        long after=plan.Element("afterTile")?.Value.Length??0,queue=plan.Element("queue")?.Value.Length??0;
        // Includes both complete native images, row codecs, XML/identity/proof
        // framing and conservative native capture framing. Qualify BEFORE any
        // source attempt, not through a late owner artifact-size refusal.
        long bound=checked(4L*((before+2L)/3L)+after+4L*queue+32768L);
        if(after<1||queue<1||bound>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;
        byte[] snapshot=Convert.FromBase64String(saved.Element("snapshot").Value);long offset=(long)saved.Attribute("tileOffset");
        if(offset<8||offset>snapshot.LongLength-before)return false;var tile=new byte[before];Buffer.BlockCopy(snapshot,(int)offset,tile,0,before);
        return RebirthProductiveEarnedUiQueueOwner.FitsSavedSource4NativeFrames(tile,Convert.FromBase64String(plan.Element("afterTile").Value));
    }
    // This is an additional producer for the EXISTING ordinary queue-owner
    // issuer. It is not an AddRecipe callback, financial grant or completion.
    internal sealed class SavedEarnedOwner
    {
        internal readonly EntityPlayer Actor;internal readonly World World;internal readonly TileEntityWorkstation Station;internal readonly RecipeQueueItem Row;internal readonly int EntityId,OriginalCount;
        internal readonly Guid Source;internal readonly string Binding,Proof,Owner,Creation,WorldKey,Birth;internal readonly int Deaths;
        private readonly byte[] before,after;private readonly Func<bool> verify;
        internal SavedEarnedOwner(object issuer,EntityPlayer actor,TileEntityWorkstation station,RecipeQueueItem row,RebirthRemoteResourceDurableJournal.Record root,
            RebirthRemoteResourceClientOriginal.Intent original,string birth,string proof,byte[] first,byte[] last,Func<bool> current,int count=0)
        {if(!ReferenceEquals(issuer,savedEarnedOwnerIssuer))throw new InvalidOperationException("Actual saved placed enqueue producer required.");
            Actor=actor;World=actor?.world??GameManager.Instance?.World;Station=station;Row=row;EntityId=row.StartingEntityId;OriginalCount=count>0?count:row.Multiplier;Source=root.Operation;Binding=root.Binding;Proof=proof;Owner=root.Owner;Creation=original.Creation;Deaths=original.Deaths;WorldKey=root.World;Birth=birth;
            before=(byte[])first.Clone();after=(byte[])last.Clone();verify=current;}
        internal bool Current()=>verify?.Invoke()==true;
        internal byte[] Before()=>Current()?(byte[])before.Clone():null;
        internal byte[] After()=>Current()?(byte[])after.Clone():null;
    }
    private static bool PublishSavedEarnedOwner(WorldApplication application,RebirthRemoteResourceDurableJournal.Record root,string proof)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())return true;
        if(!RebirthProductiveEarnedUiQueueOwner.AcceptsSavedSource4||!ApplicationAfter(application)||
            !worldApplications.TryGetValue(root.Operation,out var active)||!ReferenceEquals(active,application))return false;
        var plan=application.Artifact.Copy().Element("plan");int slot=(int)plan.Attribute("slot");var station=application.Claim.Station;
        if(slot<0||slot>=station.Queue.Length||!RebirthRemoteResourcePlacedStationQueue.TryDecode(plan.Element("queue").Value,out var queued)||
            queued.Length!=station.Queue.Length||queued[slot]?.Recipe==null||station.Queue[slot]?.Recipe==null)return false;
        var row=station.Queue[slot];string rowBinding=RebirthProductiveEarnedUiQueueOwner.NativeBinding(queued[slot]);
        bool Current()=>ApplicationAfter(application)&&ReferenceEquals(station.Queue[slot],row)&&
            RebirthProductiveEarnedUiQueueOwner.NativeBinding(row)==rowBinding&&
            TryReadCompletion(root,application.Artifact,application.Reference,application.Allocation,out string actual)&&actual==proof;
        if(!Current())return false;
        var witness=new SavedEarnedOwner(savedEarnedOwnerIssuer,application.Actor,station,row,root,application.Artifact.Original,application.Artifact.Birth,proof,
            application.Claim.CopyBefore(),application.After,Current);
        bool adopted=RebirthProductiveEarnedUiQueueOwner.AdoptSavedSource4(witness)&&Current();
        if(adopted)ObserveSource4OwnerIdentity(root,application.Artifact,proof,"actual-saved-enqueue");return adopted;
    }
    private static bool PublishColdSavedEarnedOwner(EntityPlayer actor,RebirthRemoteResourcePlacedStationColdRead.Original cold,string proof)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())return true;
        if(!cold.Current||!RebirthProductiveEarnedUiQueueOwner.AcceptsSavedSource4||!ServerScope(actor,cold.Artifact.Original))return false;
        var root=cold.Source;var artifact=cold.Artifact;
        if(RebirthProductiveEarnedUiQueueOwner.HasSavedSource4Identity(root,artifact,proof))
        {if(cold.Current)ObserveSource4OwnerIdentity(root,artifact,proof,"historical-identity-only");return cold.Current;}
        Source4NativeOriginal[] matches;
        lock(source4LoadGate)matches=source4NativeOriginals.Where(value=>value.Reference.Source==root.Operation&&value.Reference.Binding==root.Binding&&
            value.Reference.Original==artifact.Original.Digest&&value.Reference.Artifact==cold.ArtifactDigest&&value.Proof==proof&&value.LoadedCurrent()).ToArray();
        if(matches.Length!=1)return false;var loaded=matches[0];var station=loaded.Tile;var image=artifact.Copy();var plan=image.Element("plan");int slot=(int)plan.Attribute("slot");
        if(slot<0||slot>=station.Queue.Length||!RebirthRemoteResourcePlacedStationQueue.TryDecode(plan.Element("queue").Value,out var queued)||
            queued.Length!=station.Queue.Length||queued[slot]?.Recipe==null||station.Queue[slot]?.Recipe==null)return false;
        var row=station.Queue[slot];string rowBinding=RebirthProductiveEarnedUiQueueOwner.NativeBinding(queued[slot]);
        byte[] after=Convert.FromBase64String(plan.Element("afterTile").Value);var saved=image.Element("savedBefore");
        byte[] snapshot=Convert.FromBase64String(saved.Element("snapshot").Value);long offset=(long)saved.Attribute("tileOffset");int length=(int)saved.Attribute("tileLength");
        if(offset<8||length<1||offset>snapshot.LongLength-length)return false;var before=new byte[length];Buffer.BlockCopy(snapshot,(int)offset,before,0,length);
        bool Current()=>cold.Current&&ServerScope(actor,artifact.Original)&&loaded.LoadedCurrent()&&Source4CompletionCurrent(loaded)&&
            ReferenceEquals(station.Queue[slot],row)&&RebirthProductiveEarnedUiQueueOwner.NativeBinding(row)==rowBinding&&
            RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,station.Queue,station.IsBurning,out var actual,out _)&&actual.SequenceEqual(after);
        if(!Current())return false;
        var witness=new SavedEarnedOwner(savedEarnedOwnerIssuer,actor,station,row,root,artifact.Original,artifact.Birth,proof,before,after,Current);
        bool adopted=RebirthProductiveEarnedUiQueueOwner.AdoptSavedSource4(witness)&&Current();
        if(adopted)ObserveSource4OwnerIdentity(root,artifact,proof,"actual-native-load-query");return adopted;
    }
    // Invoked by the existing real completion owner's lookup. The actor may
    // be offline: identity comes from the authenticated saved original plus
    // actual native initial load, never the ID of an arbitrary later row.
    internal static bool TryIssueColdSource4Owner(TileEntityWorkstation station,RecipeQueueItem row)
    {
        if(!ThreadManager.IsMainThread()||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||station==null||row?.Recipe==null)return false;
        Source4NativeOriginal[] matches;
        lock(source4LoadGate)matches=source4NativeOriginals.Where(value=>ReferenceEquals(value.Tile,station)&&value.LoadedCurrent()&&
            value.Phase==4&&value.Used).ToArray();
        if(matches.Length!=1)return false;var source=matches[0];
        if(!TryOpenSource4History(source,out var history))return false;
        using(history){var artifact=history.Artifact;var root=history.Source;var image=artifact.Copy();var plan=image.Element("plan");int slot=(int)plan.Attribute("slot");
        if(slot<0||slot>=station.Queue.Length||!ReferenceEquals(station.Queue[slot],row)||!RebirthRemoteResourcePlacedStationQueue.TryDecode(plan.Element("queue").Value,out var rows)||
            rows.Length!=station.Queue.Length||rows[slot]?.Recipe==null)return false;
        string binding=RebirthProductiveEarnedUiQueueOwner.NativeBinding(rows[slot]);
        byte[] after=Convert.FromBase64String(plan.Element("afterTile").Value);var saved=image.Element("savedBefore");
        byte[] snapshot=Convert.FromBase64String(saved.Element("snapshot").Value);long offset=(long)saved.Attribute("tileOffset");int length=(int)saved.Attribute("tileLength");
        if(offset<8||length<1||offset>snapshot.LongLength-length)return false;var before=new byte[length];Buffer.BlockCopy(snapshot,(int)offset,before,0,length);
        bool Current()=>history.Current&&source.OriginalRowCurrent()&&ReferenceEquals(source.Row,row)&&Source4CompletionCurrent(source)&&
            RebirthProductiveEarnedUiQueueOwner.NativeBinding(row)==binding&&source.OriginalCount==rows[slot].Multiplier;
        if(!Current())return false;
        var token=new SavedEarnedOwner(savedEarnedOwnerIssuer,null,station,row,root,artifact.Original,artifact.Birth,source.Proof,before,after,Current,source.OriginalCount);
        bool adopted=RebirthProductiveEarnedUiQueueOwner.AdoptSavedSource4(token)&&Current();
        if(adopted)ObserveSource4OwnerIdentity(root,artifact,source.Proof,"actual-native-load-owner-lookup");return adopted;}
    }
}
