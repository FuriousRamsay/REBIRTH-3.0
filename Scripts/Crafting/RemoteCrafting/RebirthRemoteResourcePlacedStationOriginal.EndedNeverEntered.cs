using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    internal sealed class EndedNeverEnteredClient
    {
        internal readonly EntityPlayerLocal Player;internal readonly RebirthRemoteResourceClientOriginal.Intent Original;internal readonly string Terminal;
        private EndedNeverEnteredClient(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,string text){Player=player;Original=original;Terminal=text;}
        internal bool Current=>EndedClientScope(Player,Original)&&ClientPermissions().Read(Original.Operation)==null&&
            SmallPlacedJournal(Original.Operation,SmallJournalDomain.ClientUsed).Read(Original.Operation)==null&&RebirthRemoteResourceOwnerAction.CanReleaseEndedPlacedNoEffect(this);
        internal static EndedNeverEnteredClient FromAuthenticatedResult(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,XElement reply)
        {
            if(!EndedClientScope(player,original)||reply.Name!="placedStationEndedNoPayment"||reply.Attributes().Count()!=1||(int?)reply.Attribute("version")!=1||reply.Elements().Count()!=1||reply.Element("terminal")==null)return null;
            string text=reply.Element("terminal").Value;var full=RebirthCookingRemoteAdmission.ReadProtocolXml(text,MaximumResultBytes);
            var root=full.Element("outcome")?.Elements().SingleOrDefault();
            if(full.Name!="placedNeverEnteredTerminal"||full.Attributes().Count()!=1||(int?)full.Attribute("version")!=1||full.Elements().Count()!=2||
                !XNode.DeepEquals(full.Element("fetchClientOriginal"),original.Copy())||root==null||(int?)root.Attribute("phase")!=4||(int?)root.Attribute("outcome")!=2||
                (string)root.Attribute("disposition")!="unused-no-payment"||root.Element("ownerAction")!=null||root.Element("ownerRefund")!=null||
                (int?)root.Attribute("deaths")!=original.Deaths||(string)root.Attribute("creation")!=original.Creation||
                root.Element("sources")==null||root.Element("sources").Elements("source").Any(source=>(int?)source.Attribute("attempted")!=0)||
                root.Element("removed")?.Value!=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(new ItemStack[0])))return null;
            if(root.Name!="resourceOutcome"||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
                (string)root.Attribute("owner")!=owner.StorageKey||(ulong?)root.Attribute("epoch")!=original.Epoch||(ulong?)root.Attribute("request")!=original.Request||
                checked((byte?)(int?)root.Attribute("kind"))!=(byte)original.Kind||root.Element("requirements")?.Value!=original.Copy().Element("remoteRequirements")?.Value||
                !Guid.TryParseExact((string)root.Attribute("operation"),"N",out var source)||source!=RebirthRemoteResourceDurableJournal.Operation((string)root.Attribute("world"),owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind))return null;
            var detached=new RebirthRemoteResourceDurableJournal.Record(source,0,root);
            if((string)root.Attribute("ownerProof")!=RebirthRemoteResourceDurableJournal.Digest("authenticated-no-payment|"+owner.StorageKey+"|"+detached.Binding))return null;
            var result=new EndedNeverEnteredClient(player,original,text);return result.Current?result:null;
        }
    }
    internal static bool IsEndedQueryScope(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)=>EndedClientScope(player,original);
    private static bool EndedClientScope(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)=>ThreadManager.IsMainThread()&&player?.world?.IsRemote()==true&&
        ReferenceEquals(GameManager.Instance?.World,player.world)&&ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&player.IsSpawned()&&!player.IsDead()&&IsCandidate(original)&&
        player.world.worldState.Guid==original.World&&player.Died>original.Deaths&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.CanonicalId==original.Owner&&
        RebirthCamperCharacterAccess.CurrentCreation(player)==original.Creation;
    private static void AcceptEndedNeverEntered(EntityPlayerLocal player,ClientJob job,XElement reply)
    {
        long workspace=checked(RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes+32L*Encoding.UTF8.GetByteCount(reply.ToString(SaveOptions.DisableFormatting))+65536);
        if(!RebirthRemoteResourcePlacedStationPreparation.TryReserveCancellationRead(Guid.NewGuid(),workspace,out var memory))return;
        using(memory)
        {
        if(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.HistoricalSource,out var scratch))return;
        using(scratch)
        {
        var proof=EndedNeverEnteredClient.FromAuthenticatedResult(player,job.Original,reply);
        if(proof==null||!RebirthRemoteResourceClientOriginal.CompleteEndedPlacedNoEffect(proof)||!RebirthRemoteResourcePlacedStationClientCustody.ReleaseEndedNoPayment(proof))return;
        clientJobs.Remove(job.Original.Operation);
        }
        }
    }
}
