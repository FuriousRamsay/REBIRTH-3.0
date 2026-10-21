using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    internal static bool TryBindCachedOriginal(RebirthWorkstationCompletionPipeline.CachedPrimaryReentry source,RecipeQueueItem row,out Witness witness)
    {
        witness=null;if(source?.IsIssuing()!=true||row?.Recipe==null||!ReferenceEquals(source.Station.Queue[(int)source.Before.Attribute("slot")],row)||
            source.OwnerPurpose==null||source.OwnerPurpose.Length>4096)return false;
        ObserveWorld();
        try
        {
            XElement original;
            using(var text=new StringReader(source.OwnerPurpose))
            using(var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096,MaxCharactersFromEntities=1}))original=XElement.Load(reader);
            if(original.Name!="queueOwnerPurpose"||(int?)original.Attribute("version")!=1||
                !Guid.TryParseExact((string)original.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                !Guid.TryParseExact((string)original.Attribute("birth"),"N",out var birth)||birth==Guid.Empty||
                (string)original.Attribute("owner")!=source.Owner||(string)original.Attribute("creation")!=source.Creation||
                (int?)original.Attribute("entity")!=row.StartingEntityId||
                (string)original.Attribute("recipe")!=ProofHash(Encoding.UTF8.GetBytes(NativeBinding(row)))||
                (string)original.Attribute("definition")!=ProofHash(Encoding.UTF8.GetBytes(Binding(row))))return false;
            var prior=originals.Where(value=>ReferenceEquals(value.Station,source.Station)&&value.Operation==operation&&value.SourceCurrent()).ToArray();
            if(prior.Length==1&&prior[0].Matches(row)){witness=prior[0];return true;}
            if(prior.Length!=0||originals.Count>=8)return false;
            // The private source owns the positively saved original plus actual
            // native load/Open lineage. This arm never mints enqueue permission.
            var result=new Witness{World=source.World,Station=source.Station,Recipe=row.Recipe,OriginalOwnerKey=source.Owner,Creation=source.Creation,
                StartingEntityId=row.StartingEntityId,OriginalCount=(int)source.Before.Attribute("beforeCount"),Deaths=(int)original.Attribute("deaths"),
                Operation=operation,Birth=birth,RecipeBinding=Binding(row),NativeRecipeBinding=NativeBinding(row),
                Save=Path.GetFullPath(GameIO.GetSaveGameDir()),WorldKey=RebirthProductiveEarnedArtifact.WorldIdentity(source.World),
                Path=source.Path,Image=source.Image,CachedSource=source,ClientPurpose=source.OwnerPurpose,Published=true,Retention=source.RetainOriginal()};
            if(result.Retention==null||!result.Current()){result.Dispose();return false;}originals.Add(result);witness=result;return true;
        }
        catch{return false;}
    }
}
