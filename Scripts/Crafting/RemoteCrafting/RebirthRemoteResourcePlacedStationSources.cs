using System;
using System.Collections.Generic;
using System.Linq;

// Pure composition of the already retained source plan. Capacity may ignore
// native debit shrinkage, but never mandatory same-chunk receipt growth.
internal static class RebirthRemoteResourcePlacedStationSources
{
    internal static bool TryPreview(RebirthRemoteResourceDurableJournal.Record root,TileEntityWorkstation target,
        IReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease> leases,out ItemStack[] outputAfter,out int chunkGrowth)
    {
        outputAfter=null;chunkGrowth=0;
        try
        {
            if(root==null||root.Phase>1||target==null||leases==null||target.GetChunk()==null)return false;
            var sources=root.Copy().Element("sources").Elements("source").ToArray();if(sources.Length<1||sources.Length>RebirthRemoteResourceDurableJournal.MaximumSources)return false;
            var output=ItemStack.Clone(target.Output);int growth=0;bool targetFound=false;
            foreach(var source in sources)
            {
                if((int)source.Attribute("attempted")!=0)return false;if(source.Element("npc")!=null)continue;
                string id=(string)source.Attribute("id");
                if(!leases.TryGetValue(id,out var lease)||lease?.Original==null||lease.Original.Operation!=root.Operation||lease.Original.Binding!=root.Binding||
                    lease.Original.Owner!=root.Owner||lease.Original.Domain!=RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource||!lease.Current())return false;
                if(!(lease.Source.Endpoint is TileEntity))continue;
                if(!lease.TryPreviewTileDebit(out var tile,out string key,out var prior,out var next)||tile.GetChunk()==null)return false;
                if(ReferenceEquals(tile.GetChunk(),target.GetChunk()))
                {
                    // Stage0 positively publishes this existing marker, so the
                    // native dictionary count/key/expiry/network framing does
                    // not grow. This exact codec adds one114-byte effect.
                    if(next.Length!=prior.Length+114||!tile.GetChunk().ChunkCustomData.dict.TryGetValue(key,out var marker)||
                        marker.data==null||!marker.data.SequenceEqual(prior))return false;
                    growth=checked(growth+next.Length-prior.Length);
                }
                if(ReferenceEquals(tile,target))
                {
                    if(targetFound||lease.Original.Kind!=RemoteResourceSourceKind.WorkstationOutput)return false;
                    var before=lease.Original.CopyStage(0);if(before.Length!=target.Output.Length||before.Where((item,index)=>!RebirthStationGridIngredients.IsSameStackSnapshot(item,target.Output[index])).Any())return false;
                    output=lease.Original.CopyStage(1);targetFound=true;
                }
            }
            outputAfter=output;chunkGrowth=growth;return true;
        }
        catch{return false;}
    }
}
