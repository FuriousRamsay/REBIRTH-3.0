using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    // Metadata proof for one FIRST native mutation after a real Before load.
    // This type never grants an owner refund, a second queue attempt, or a
    // pretend native After load. Its After is the actual saved publication.
    private sealed class CancellationBeforeTwoPublication
    {
        private readonly CancellationServer original;private readonly string permission,before,birth,world;
        private readonly Guid recovery;private readonly long beforeGeneration;private readonly TileEntityWorkstation tile;private readonly Chunk chunk;
        private string after,eventImage;
        private CancellationBeforeTwoPublication(CancellationServer value,CancellationLoaded loaded)
        {original=value;permission=value.Permission;before=value.Receipt1BeforeEvidence;birth=value.Birth;world=value.Original.World;
            recovery=loaded.Original.Recovery;beforeGeneration=loaded.Generation;tile=loaded.Tile;chunk=loaded.Chunk;}
        internal static CancellationBeforeTwoPublication Capture(CancellationServer original,CancellationLoaded loaded)
        {
            if(loaded?.Installed()!=true||!ReferenceEquals(original.Station,loaded.Tile)||!ReferenceEquals(original.Chunk,loaded.Chunk)||!original.Current()||
                original.QueueAttempted||!CancellationHostCustodyCurrent(original)||!SavedCancellationOwner(original,2f,true,out _)||
                loaded.Original.Evidence!=original.Receipt1BeforeEvidence||!original.FullTile(original.BeforeTile))return null;
            return new CancellationBeforeTwoPublication(original,loaded);
        }
        internal bool Current
        {
            get
            {
                if(!original.Current()||original.Permission!=permission||original.Birth!=birth||original.Original.World!=world||!original.AfterSaved||
                    !original.QueueAttempted||!original.QueueCompleted||original.QueueReentryAttempted||original.CustodyEscaped||beforeGeneration<1||
                    !ReferenceEquals(original.Station,tile)||!ReferenceEquals(original.Chunk,chunk)||!CancellationHostCustodyCurrent(original)||
                    !SavedCancellationOwner(original,2f,true,out _)||!ReadCancellationSnapshot(original,false,out var savedBefore)||savedBefore.Canonical!=before||
                    !ReadCancellationSnapshot(original,true,out var savedAfter)||!original.FullTile(original.AfterTile))return false;
                foreach(string purpose in new[]{"Mutation","Terminal"})
                {
                    var record=CancellationJournal(original.Operation,purpose,MaximumApplicationCommand).Read(original.Operation);
                    byte[] full=purpose=="Mutation"?CancellationMutation(original):CancellationServerTerminalBytes(original);
                    if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Operation!=original.Operation||record.Vehicle!=original.Source||record.OwnerCreation!=original.Original.Creation||
                        record.Revision!=0||!record.Before.SequenceEqual(full)||!record.After.SequenceEqual(full))return false;
                }
                if(after!=null)return after==savedAfter.Canonical&&eventImage!=null;
                after=savedAfter.Canonical;var body=RebirthCookingRemoteAdmission.ReadProtocolXml(after,MaximumResultBytes);
                eventImage=new XElement("placedCancellationReconstruction",new XAttribute("version",4),new XAttribute("cause","before-load-first-attempt-saved-after"),
                    new XAttribute("cancel",original.Operation.ToString("N")),new XAttribute("source",original.Source.ToString("N")),new XAttribute("binding",original.Binding),
                    new XAttribute("birth",birth),new XAttribute("generation",(long)body.Attribute("generation")),new XAttribute("recovery",recovery.ToString("N")),
                    new XAttribute("world",world),new XAttribute("chunkX",chunk.X),new XAttribute("chunkZ",chunk.Z),new XAttribute("tileOffset",savedAfter.Offset),
                    new XAttribute("tileLength",savedAfter.Tile.Length)).ToString(SaveOptions.DisableFormatting);
                return true;
            }
        }
        internal string Encode()=>Current?eventImage:null;
    }
}
