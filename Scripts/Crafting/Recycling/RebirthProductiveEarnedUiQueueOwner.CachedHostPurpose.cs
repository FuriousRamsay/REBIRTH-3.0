using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    // Identity-only arm, issued by an actual saved native cached original.
    // RowRetired prevents this source from certifying a new queue action.
    internal static bool TryResolveCachedHostPurpose(RebirthWorkstationCompletionPipeline.CachedHostOriginal source,string purpose,out Witness witness)
    {
        witness=null;
        if(source?.Current()!=true||!source.ContainsOriginalPurpose(purpose)||purpose.Length>4096)return false;
        try
        {
            XElement node;
            using(var text=new StringReader(purpose))
            using(var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096,MaxCharactersFromEntities=1}))node=XElement.Load(reader);
            if(node.Name!="queueOwnerPurpose"||(int?)node.Attribute("version")!=1||
                !Guid.TryParseExact((string)node.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                !Guid.TryParseExact((string)node.Attribute("birth"),"N",out var birth)||birth==Guid.Empty||
                !RebirthWorkstationCompletionOriginal.ValidKey((string)node.Attribute("owner"))||
                !RebirthSurvivorRequestScope.TryNormalize((string)node.Attribute("creation"),out var creation)||creation!=(string)node.Attribute("creation"))return false;
            var retention=source.Retain();if(retention==null)return false;
            var value=new Witness{World=source.World,Station=source.Station,Operation=operation,Birth=birth,
                OriginalOwnerKey=(string)node.Attribute("owner"),Creation=(string)node.Attribute("creation"),StartingEntityId=(int)node.Attribute("entity"),Deaths=(int)node.Attribute("deaths"),
                Save=Path.GetFullPath(GameIO.GetSaveGameDir()),WorldKey=RebirthProductiveEarnedArtifact.WorldIdentity(source.World),
                Path=source.IdentityPath,Image=source.IdentityImage,ClientPurpose=purpose,CachedHostSource=source,Published=true,RowRetired=true,References=1,Retention=retention};
            if(!value.SourceCurrent()){value.Dispose();return false;}witness=value;return true;
        }
        catch{return false;}
    }
    internal static bool TryMapCachedHostAggregate(RebirthWorkstationCompletionPipeline.CachedHostOriginal source,out CraftCompleteData aggregate)
    {
        aggregate=null;if(source?.Current()!=true)return false;
        try
        {
            long measured=source.MeasureMembership();
            if(measured<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8*measured+65536),out var memory))return false;
            using(memory)
            {
                var ledger=source.CopyMembership();var bytes=Convert.FromBase64String((string)ledger.Element("aggregate"));
                using(var callback=source.Memory.TryEnterCallbacks())
                {
                    if(callback==null)return false;var rows=source.ActualReceiptRows?.Where(row=>RowImage(row).SequenceEqual(bytes)).ToArray();
                    if(rows?.Length!=1)return false;aggregate=rows[0];return true;
                }
            }
        }
        catch{return false;}
    }
}
