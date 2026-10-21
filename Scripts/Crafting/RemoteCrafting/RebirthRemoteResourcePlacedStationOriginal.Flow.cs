using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    // Only the reviewed, dependency-checked pre-World bootstrap may enable.
    private static bool protocolActive=false;
    internal static bool ProtocolActive=>protocolActive;
    internal enum PreparationStatus{Unknown,Waiting,Ready}
    private sealed class SavedReady{internal EntityPlayer Actor;internal RebirthRemoteResourcePlacedStationCustody.Claim Claim;internal string Original,Reference;}
    private static readonly Dictionary<Guid,SavedReady> readySources=new Dictionary<Guid,SavedReady>();
    private static World readyWorld;private static string readyDirectory;
    private static void BindReadyWorld()
    {var world=GameManager.Instance?.World;string directory=GameIO.GetSaveGameDir();if(!ReferenceEquals(world,readyWorld)||directory!=readyDirectory){readySources.Clear();readyWorld=world;readyDirectory=directory;}}
    internal static bool IsEntryCandidate(EntityPlayerLocal player,ItemActionEntryCraft entry)
    {
        var recipe=(entry?.ItemController as XUiC_RecipeEntry)?.Recipe;
        return player?.world?.IsRemote()==true&&AllowsReviewedPlacedMode&&recipe!=null&&!recipe.IsScrap&&
            !RebirthCookingHeat.Managed(recipe)&&!RebirthRecyclerRuntime.IsMarked(recipe)&&!RebirthStationGridQueue.IsMarked(recipe)&&
            RebirthRemoteResourcePlacedStationScope.TryObserveWindow(player,entry,out _,out _);
    }
    internal static bool HoldTargetBeforeSources(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root)
    {
        if(!ProtocolActive||!TryReadAdmission(root,out var original)||!ServerScope(actor,original)||
            !ValidateInput(original,out var position,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _))return false;
        var station=actor.world.GetTileEntity(position) as TileEntityWorkstation;
        if(RebirthRemoteResourcePlacedStationCustody.TryReadGuardClaim(station,out var existing))return existing.Matches(root,original)&&existing.TargetCurrent();
        return root.SourcesUnattempted&&RebirthRemoteResourcePlacedStationCustody.TryReserveBeforeSources(actor,root,original,out _);
    }
    internal static PreparationStatus PrepareBeforeSources(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,
        IReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease> leases)
    {
        try
        {
            BindReadyWorld();
            if(!ProtocolActive||root==null||root.Phase!=1||root.HasOwnerAction||!TryReadAdmission(root,out var original)||!ServerScope(actor,original))return PreparationStatus.Unknown;
            if(readySources.TryGetValue(root.Operation,out var ready))
            {
                if(!ReferenceEquals(ready.Actor,actor)||ready.Original!=original.Digest||
                    !RebirthRemoteResourcePlacedStationPreparation.TryReadRetained(actor,root,out _,out var retainedClaim,out var allocation,out var retainedLeases)||
                    !ReferenceEquals(ready.Claim,retainedClaim)||!retainedClaim.ExactBefore()||retainedLeases.Count!=leases.Count||
                    leases.Any(pair=>!retainedLeases.TryGetValue(pair.Key,out var actual)||!ReferenceEquals(actual,pair.Value))||
                    !RebirthRemoteResourcePlacedStationReference.TryResolve(root,ready.Reference,allocation,out _))return PreparationStatus.Unknown;
                return PreparationStatus.Ready;
            }
            if(!root.SourcesUnattempted||
                !RebirthRemoteResourcePlacedStationCustody.TryReserveBeforeSources(actor,root,original,out var claim)||
                !RebirthRemoteResourcePlacedStationPreparation.TryBegin(actor,root,original,claim,leases,out var preparation))return PreparationStatus.Unknown;
            var state=preparation.Pump(out _);
            if(state==RebirthRemoteResourcePlacedStationPreparation.Progress.Observing)return PreparationStatus.Waiting;
            if(state!=RebirthRemoteResourcePlacedStationPreparation.Progress.ArtifactSaved||
                !RebirthRemoteResourcePlacedStationReference.TryCapture(root,original,preparation.Allocation,out var reference)||
                !RebirthRemoteResourcePlacedStationReference.TryResolve(root,reference.Encode(),preparation.Allocation,out var full)||
                !CanPublishOwnerEnvelope(root,full,reference,preparation.Allocation))return PreparationStatus.Unknown;
            // This maps actual saved-Before/full native capacity+source proof
            // and bounded owner transport, never artifact existence alone.
            if(!claim.ExactBefore()||readySources.Count>=32)return PreparationStatus.Unknown;
            readySources.Add(root.Operation,new SavedReady{Actor=actor,Claim=claim,Original=original.Digest,Reference=reference.Encode()});return PreparationStatus.Ready;
        }
        catch{return PreparationStatus.Unknown;}
    }
    private const int MaximumResultBytes=12*1024*1024;
    private static XElement ReplyBody(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourcePlacedStationArtifactReader.Image artifact,
        RebirthRemoteResourcePlacedStationReference.Image reference,bool permission,bool debitPermission)
    {
        var full=artifact.Copy();var original=artifact.Original;var source=root.Copy();
        string paid=root.Phase<2?Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(source.Element("sources").Elements("source")
            .SelectMany(row=>RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(row.Element("paid").Value))).ToArray())):source.Element("removed").Value;
        return new XElement("placedStationResult",new XAttribute("version",1),new XAttribute("original",original.Operation.ToString("N")),
            new XAttribute("digest",original.Digest),new XAttribute("source",root.Operation.ToString("N")),new XAttribute("sourceWorld",root.World),new XAttribute("binding",root.Binding),
            new XAttribute("phase",root.Phase),new XAttribute("outcome",(int)source.Attribute("outcome")),new XAttribute("permission",permission),
            new XAttribute("debitPermission",debitPermission),new XAttribute("ownerProof",(string)source.Attribute("ownerProof")??""),
            new XElement("reference",reference.Encode()),new XElement("beforeActor",original.Copy().Element("actorBefore").Value),
            new XElement("afterActor",full.Element("plan").Element("actorAfter").Value),new XElement("afterTile",full.Element("plan").Element("afterTile").Value),
            new XElement("queue",full.Element("plan").Element("queue").Value),new XElement("paid",paid));
    }
    private static bool FitsReply(XElement body)
    {
        using(var counter=new RebirthRemoteResourcePlacedStationStreams.Counter(MaximumResultBytes))
        using(var writer=XmlWriter.Create(counter,new XmlWriterSettings{Encoding=new UTF8Encoding(false,true),OmitXmlDeclaration=true,Indent=false,NewLineHandling=NewLineHandling.None,CloseOutput=false}))
        {body.WriteTo(writer);writer.Flush();return counter.Length+4096<=MaximumResultBytes;}
    }
    private static bool CanPublishOwnerEnvelope(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourcePlacedStationArtifactReader.Image artifact,
        RebirthRemoteResourcePlacedStationReference.Image reference,RebirthRemoteResourcePlacedStationAllocation.Admission allocation)
    {
        if(allocation==null||!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,
            RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope,out var scratch))return false;
        using(scratch)
        {
        // Count the ACTUAL future owner/native payload before source effects;
        // existing request/result fragmentation limits are not widened.
        var body=ReplyBody(root,artifact,reference,true,true);
        return allocation!=null&&artifact.HasCapturedAllocation&&FitsSavedEarnedOwnerBeforeSources(artifact)&&reference.Source==root.Operation&&reference.Binding==root.Binding&&
            ApplicationCommand(root,artifact,reference).Length<=MaximumApplicationCommand&&FitsReply(body)&&
            RebirthRemoteResourcePlacedStationAllocation.TryAdmitOwnerReplyBeforeHydration(body,out long workspace)&&
            RebirthRemoteResourcePlacedStationAllocation.TryCompletionWorkspace(artifact,out long completion,out _)&&
            artifact.ReadWorkspace>=RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes&&workspace>=RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes&&scratch.Current&&
            allocation.ReserveReadWorkspace(checked(artifact.ReadWorkspace+workspace-2*RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes+completion));
        }
    }
}
