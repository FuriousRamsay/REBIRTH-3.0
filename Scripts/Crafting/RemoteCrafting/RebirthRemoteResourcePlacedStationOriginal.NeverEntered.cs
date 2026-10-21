using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private sealed class NeverEnteredSend:IRebirthRemoteResourceRetainedNativeBuffers
    {internal bool Pending;public bool IsDrained=>!Pending;}
    private static void SendNeverEntered(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,string text,RebirthRemoteResourcePlacedStationPreparation.UploadRead memory)
    {
        var buffers=new NeverEnteredSend{Pending=true};if(!memory.RetainNativeBuffers(buffers))return;
        bool queued=NetPackageRebirthRemoteRepairResult.SendObserved(actor.entityId,original,text,sent=>{buffers.Pending=false;memory.Dispose();});
        if(!queued)buffers.Pending=false;
    }
    // A positive source coordinator outcome, not a failed preparation result.
    // Its complete immutable admission and canonical empty outcome are saved
    // before either side can retire metadata. No artifact is required because
    // the source original proves that every source attempt remained zero.
    internal sealed class NeverEnteredTerminal
    {
        internal readonly RebirthRemoteResourceDurableJournal.Record Root;
        internal readonly RebirthRemoteResourceClientOriginal.Intent Original;
        private readonly EntityPlayer actor;private readonly string bytes;
        internal NeverEnteredTerminal(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,string bytes)
        {this.actor=actor;Root=root;Original=original;this.bytes=bytes;}
        internal bool Current=>NeverEnteredScope(actor,Original)&&ReadNeverEntered(Root,Original)&&NeverEnteredBytes(Root,Original)==bytes&&SavedNeverEntered(Root,Original,bytes);
    }
    private static bool NeverEnteredScope(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)=>ServerScope(actor,original)||
        ThreadManager.IsMainThread()&&actor?.world?.IsRemote()==false&&AllowsReviewedPlacedMode&&actor.IsSpawned()&&!actor.IsDead()&&IsCandidate(original)&&
        ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&actor.world.worldState.Guid==original.World&&actor.Died>original.Deaths&&
        RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&owner.CanonicalId==original.Owner&&RebirthOriginalActionScope.Matches(actor,owner.StorageKey,original.Creation);
    private static void RequestEndedNeverEntered(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(!NeverEnteredScope(actor,original)||actor.Died<=original.Deaths||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
            !RemoteResourceWorldIdentity.TryGet(actor.world,out string world))return;
        var source=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
        if(!RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,source,out var root,out bool unknown)||unknown||root==null)return;
        if(root.Phase<=1)
        {
            // The original source coordinator durably stamps attempted1
            // BEFORE every setter. Full coherent history plus the actual
            // authenticated later native life therefore proves this exact
            // original never crossed that boundary. No current slots are
            // compared and no new-life financial image is written.
            if(!NeverEnteredSources(root.Copy())||root.HasOwnerAction||root.Copy().Element("ownerRefund")!=null||
                !TryReadAdmission(root,out var saved)||!XNode.DeepEquals(saved.Copy(),original.Copy())||!RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root))return;
            if(!RebirthRemoteResourceDurableJournal.TryAdvance(root,next=>{next.SetAttributeValue("phase",3);next.SetAttributeValue("outcome",2);
                next.Element("removed").Value=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(new ItemStack[0]));
                next.Element("reason").Value="Actual original life ended before every durable source attempt and owner permission boundary.";},
                ()=>NeverEnteredScope(actor,original)&&actor.Died>original.Deaths&&RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root),out root))return;
        }
        TryReplyNeverEntered(actor,original,root);
    }
    private static RebirthRemoteResourceClientOriginalJournal NeverEnteredJournal()=>new RebirthRemoteResourceClientOriginalJournal(
        System.IO.Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","PlacedStationNeverEntered"));
    private static string NeverEnteredBytes(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original)=>
        new XElement("placedNeverEnteredTerminal",new XAttribute("version",1),original.Copy(),new XElement("outcome",root.Copy())).ToString(SaveOptions.DisableFormatting);
    private static bool NeverEnteredSources(XElement image)=>image.Element("sources")!=null&&image.Element("sources").Elements("source").All(source=>(int?)source.Attribute("attempted")==0);
    private static bool ReadNeverEntered(RebirthRemoteResourceDurableJournal.Record expected,RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(expected==null||!RebirthRemoteResourceDurableJournal.TryRead(expected.Owner,expected.Operation,out var current,out bool unknown)||unknown||current==null||
            current.Binding!=expected.Binding||!XNode.DeepEquals(current.Copy(),expected.Copy())||!TryReadAdmission(current,out var saved)||!XNode.DeepEquals(saved.Copy(),original.Copy()))return false;
        var image=current.Copy();return current.Phase==4&&(string)image.Attribute("disposition")=="unused-no-payment"&&
            (int?)image.Attribute("outcome")==2&&NeverEnteredSources(image)&&!current.HasOwnerAction&&image.Element("ownerRefund")==null&&
            image.Element("removed")?.Value==Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(new ItemStack[0]))&&
            RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(current);
    }
    private static bool SavedNeverEntered(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,string text)
    {
        var saved=NeverEnteredJournal().Read(original.Operation);var bytes=Encoding.UTF8.GetBytes(text);
        return saved?.State==RebirthCamperTransferJournal.Phase.Applied&&saved.Vehicle==root.Operation&&saved.OwnerCreation==original.Creation&&saved.Revision==0&&
            saved.Before.SequenceEqual(bytes)&&saved.After.SequenceEqual(bytes);
    }
    private static bool TryReplyNeverEntered(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root)
    {
        if(root==null||root.Phase!=3&&root.Phase!=4)return false;
        if(root.Phase==4&&(string)root.Copy().Attribute("disposition")!="unused-no-payment")return false;
        long workspace=checked(RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes+32L*(Encoding.UTF8.GetByteCount(root.Copy().ToString(SaveOptions.DisableFormatting))+Encoding.UTF8.GetByteCount(original.Copy().ToString(SaveOptions.DisableFormatting)))+65536);
        if(!RebirthRemoteResourcePlacedStationPreparation.TryReserveCancellationRead(Guid.NewGuid(),workspace,out var memory))return true;
        using(memory)
        {
            if(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.HistoricalSource,out var scratch))return true;
            using(scratch)
            {
                var image=root.Copy();
                if(!NeverEnteredScope(actor,original)||!TryReadAdmission(root,out var admission)||!XNode.DeepEquals(admission.Copy(),original.Copy())||
                    (int?)image.Attribute("outcome")!=2||!NeverEnteredSources(image)||root.HasOwnerAction||image.Element("ownerRefund")!=null||
                    image.Element("removed")?.Value!=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(new ItemStack[0]))||
                    !RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root))return true;
                string proof=RebirthRemoteResourceDurableJournal.Digest("authenticated-no-payment|"+root.Owner+"|"+root.Binding);
                if(root.Phase==3&&!RebirthRemoteResourceDurableJournal.TryAdvance(root,next=>{next.SetAttributeValue("phase",4);next.SetAttributeValue("disposition","unused-no-payment");next.SetAttributeValue("ownerProof",proof);},
                    ()=>memory.Current&&NeverEnteredScope(actor,original)&&RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root),out root))return true;
                if(!ReadNeverEntered(root,original)||(string)root.Copy().Attribute("ownerProof")!=proof)return true;
                string text=NeverEnteredBytes(root,original);byte[] bytes=Encoding.UTF8.GetBytes(text);if(bytes.Length>MaximumResultBytes)return true;
                var journal=NeverEnteredJournal();var saved=journal.Prepare(original.Operation,root.Operation,original.Creation,0,bytes,bytes);
                if(saved.Vehicle!=root.Operation||saved.OwnerCreation!=original.Creation||saved.Revision!=0||!saved.Before.SequenceEqual(bytes)||!saved.After.SequenceEqual(bytes))return true;
                if(saved.State==RebirthCamperTransferJournal.Phase.Prepared)saved=journal.Advance(original.Operation,saved.State,RebirthCamperTransferJournal.Phase.Uncertain);
                if(saved.State==RebirthCamperTransferJournal.Phase.Uncertain)saved=journal.Advance(original.Operation,saved.State,RebirthCamperTransferJournal.Phase.Applied);
                var terminal=new NeverEnteredTerminal(actor,root,original,text);
                if(!terminal.Current||!RebirthRemoteResourceSourceCustody.RetirePlacedNeverEntered(terminal)||
                    !RebirthRemoteResourcePlacedStationPreparation.RetireNeverEntered(terminal)||!RebirthRemoteResourcePlacedStationCustody.ReleaseNeverEntered(terminal))return true;
                readySources.Remove(root.Operation);
                if(actor.Died>original.Deaths)
                {
                    var ended=new XElement("placedStationEndedNoPayment",new XAttribute("version",1),new XElement("terminal",text));
                    if(FitsReply(ended))SendNeverEntered(actor,original,ended.ToString(SaveOptions.DisableFormatting),memory);return true;
                }
                var reply=new XElement("placedStationNoPayment",new XAttribute("version",1),new XAttribute("original",original.Operation.ToString("N")),new XAttribute("digest",original.Digest),
                    new XAttribute("sourceWorld",root.World),new XAttribute("source",root.Operation.ToString("N")),new XAttribute("binding",root.Binding),new XAttribute("proof",proof),original.Copy());
                SendNeverEntered(actor,original,reply.ToString(SaveOptions.DisableFormatting),memory);return true;
            }
        }
    }
}
