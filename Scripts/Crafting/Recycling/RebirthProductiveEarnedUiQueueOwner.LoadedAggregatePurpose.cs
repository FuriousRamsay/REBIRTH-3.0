using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    // Identity only: the actual loaded aggregate and the independently saved
    // insertion original jointly qualify this retired-row witness. Native Add
    // and settlement still require their own complete member capabilities.
    internal static bool TryResolveLoadedAggregatePurpose(RebirthWorkstationCompletionPipeline.RemoteOutputHostSource source,
        string purpose,out Witness witness)
        =>TryResolveLoadedAggregatePurpose(source==null?null:new RebirthProductiveEarnedUiDeferredTransport.LoadedMemberSource(source),purpose,out witness);
    internal static bool TryResolveLoadedAggregatePurpose(RebirthProductiveEarnedUiDeferredTransport.LoadedMemberSource source,
        string purpose,out Witness witness)
    {
        witness=null;IDisposable memory=null;
        try
        {
            if(source?.Current()!=true||source.DeferredAddBody==null||source.Native?.Current()!=true||
                string.IsNullOrEmpty(purpose)||purpose.Length>4096)return false;
            var membership=source.DeferredMembership??source.Request.Element("deferredNativeContribution");
            if(membership==null||!((string)membership.Attribute("recipient")==purpose||
                membership.Element("members")?.Elements("member").Any(member=>(string)member.Attribute("source")==purpose)==true))return false;
            var node=RebirthCookingRemoteAdmission.ReadProtocolXml(purpose,4096);
            if(node.Name!="queueOwnerPurpose"||(int?)node.Attribute("version")!=1||node.Attributes().Count()!=11||node.HasElements||
                !Guid.TryParseExact((string)node.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                !Guid.TryParseExact((string)node.Attribute("birth"),"N",out var birth)||birth==Guid.Empty||
                birth!=source.Birth||!RebirthOriginalActionScope.Validate((string)node.Attribute("owner"),(string)node.Attribute("creation"))||
                (int?)node.Attribute("entity") is not int entity||(int?)node.Attribute("deaths") is not int deaths||deaths<0||
                (string)node.Attribute("world")!=RebirthProductiveEarnedArtifact.WorldIdentity(source.World))return false;
            string path=Path.Combine(source.Save,"RebirthProductiveEarnedUi","queueOwners",(string)node.Attribute("owner"),operation.ToString("N")+".original");
            if(!RebirthProductiveEarnedArtifact.SafePath(path)||File.Exists(path+".tmp"))return false;
            long length=new FileInfo(path).Length;
            if(length<1||length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*length+65536),out memory))return false;
            byte[] image=File.ReadAllBytes(path);
            if(image.LongLength!=length||ProofHash(image)!=(string)node.Attribute("original"))return false;
            var original=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(image),RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
            if(original.Name!="nativeQueueOwnerOriginal"||
                (string)original.Attribute("operation")!=operation.ToString("N")||(string)original.Attribute("birth")!=birth.ToString("N")||
                (string)original.Attribute("owner")!=(string)node.Attribute("owner")||(string)original.Attribute("creation")!=(string)node.Attribute("creation")||
                (int?)original.Attribute("entity")!=entity||(int?)original.Attribute("deaths")!=deaths||
                (string)original.Attribute("world")!=(string)node.Attribute("world")||
                !string.Equals(Path.GetFullPath((string)original.Attribute("save")),source.Save,RebirthStationCompletionOriginalScope.PathComparison)||
                original.Elements("recipeBinding").Count()!=1||
                ProofHash(Encoding.UTF8.GetBytes((string)original.Element("recipeBinding")))!=(string)node.Attribute("definition")||
                !RebirthProductiveEarnedArtifact.FileMatches(path,image)||!source.Current()||source.Native.Current()!=true)return false;
            var value=new Witness{World=source.World,Station=source.Native.Tile,Operation=operation,Birth=birth,
                OriginalOwnerKey=(string)node.Attribute("owner"),Creation=(string)node.Attribute("creation"),StartingEntityId=entity,Deaths=deaths,
                RecipeBinding=(string)original.Element("recipeBinding"),Save=source.Save,WorldKey=(string)node.Attribute("world"),
                Path=path,Image=image,ClientPurpose=purpose,FinalOutputSource=source.Remote,AuthorityLocalSource=source.Local,Published=true,RowRetired=true,References=1,Retention=memory};
            memory=null;
            if(!value.SourceCurrent()){value.Dispose();return false;}
            witness=value;return true;
        }
        catch{return false;}
        finally{memory?.Dispose();}
    }
}
