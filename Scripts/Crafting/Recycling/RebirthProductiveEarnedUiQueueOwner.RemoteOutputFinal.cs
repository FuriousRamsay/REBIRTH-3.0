using System;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;
internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    // Existing insertion identity, qualified by this original's actual saved
    // final owner and private native load. No row shape is insertion authority.
    internal static bool TryUseRemoteOutputFinal(RebirthWorkstationCompletionPipeline.RemoteOutputHostSource source,out Witness owner)
        =>TryUseRemoteOutputLoaded(source,false,out owner);
    internal static bool TryUseRemoteOutputDeferred(RebirthWorkstationCompletionPipeline.RemoteOutputHostSource source,out Witness owner)
        =>TryUseRemoteOutputLoaded(source,true,out owner);
    private static bool TryUseRemoteOutputLoaded(RebirthWorkstationCompletionPipeline.RemoteOutputHostSource source,bool deferred,out Witness owner)
    {
        owner=null;IDisposable memory=null;
        try
        {
            if(source==null||(deferred?source.DeferredAddBody==null:!source.FinalCallbackSaved)||!source.Current()||source.Native?.Current()!=true)return false;
            var station=source.Native.Tile;int index=(int)source.Before.Attribute("slot");
            if(station?.Queue==null||index<0||index>=station.Queue.Length||station.Queue[index]?.Recipe==null)return false;
            string purpose=(string)source.Original.Element("before").Element("remoteNativeOriginal").Attribute("ownerPurpose");
            var node=RebirthCookingRemoteAdmission.ReadProtocolXml(purpose,4096);var row=station.Queue[index];
            string expectedOwner=deferred?(string)source.RootBefore.Attribute("startingOwnerKey"):source.Viewer;
            string expectedCreation=deferred?(string)source.RootBefore.Attribute("startingOwnerCreation"):source.Creation;
            int expectedDeaths=deferred?(int)source.RootBefore.Attribute("startingOwnerDeaths"):source.Deaths;
            if(deferred&&((bool?)source.RootBefore.Attribute("startingOwnerKnown")!=true||
                !RebirthOriginalActionScope.Validate(expectedOwner,expectedCreation)||expectedDeaths<0))return false;
            if(node.Name!="queueOwnerPurpose"||(int?)node.Attribute("version")!=1||node.Attributes().Count()!=11||node.HasElements||
                !Guid.TryParseExact((string)node.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                (string)node.Attribute("birth")!=source.Birth.ToString("N")||(string)node.Attribute("owner")!=expectedOwner||
                (string)node.Attribute("creation")!=expectedCreation||(int?)node.Attribute("deaths")!=expectedDeaths||
                (int?)node.Attribute("entity")!=(int)source.Before.Attribute("startingOwner")||row.StartingEntityId!=(int)node.Attribute("entity")||
                (string)node.Attribute("world")!=RebirthProductiveEarnedArtifact.WorldIdentity(source.World)||
                (string)node.Attribute("recipe")!=ProofHash(Encoding.UTF8.GetBytes(NativeBinding(row)))||row.Multiplier!=(int)source.Before.Attribute("beforeCount"))return false;
            var known=originals.Where(value=>ReferenceEquals(value.FinalOutputSource,source)&&value.SourcePublished()&&value.ClientPurpose==purpose).ToArray();
            if(known.Length==1){owner=known[0];return true;}if(known.Length>1||originals.Count>=64)return false;
            string path=Path.Combine(source.Save,"RebirthProductiveEarnedUi","queueOwners",expectedOwner,operation.ToString("N")+".original");
            if(!RebirthProductiveEarnedArtifact.SafePath(path)||File.Exists(path+".tmp"))return false;
            long length=new FileInfo(path).Length;if(length<1||length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*length+65536),out memory))return false;
            byte[] image=File.ReadAllBytes(path);if(image.LongLength!=length||ProofHash(image)!=(string)node.Attribute("original"))return false;
            var original=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(image),RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
            if(original.Name!="nativeQueueOwnerOriginal"||(string)original.Attribute("operation")!=operation.ToString("N")||
                (string)original.Attribute("birth")!=source.Birth.ToString("N")||(string)original.Attribute("owner")!=expectedOwner||
                (string)original.Attribute("creation")!=expectedCreation||(int?)original.Attribute("deaths")!=expectedDeaths||
                (int?)original.Attribute("entity")!=row.StartingEntityId||(string)original.Attribute("world")!=(string)node.Attribute("world")||(string)original.Attribute("save")!=source.Save||
                ProofHash(Encoding.UTF8.GetBytes((string)original.Element("recipeBinding")))!=(string)node.Attribute("definition")||!source.Current()||source.Native.Current()!=true)return false;
            var value=new Witness{World=source.World,Station=station,Recipe=row.Recipe,Operation=operation,Birth=source.Birth,OriginalOwnerKey=expectedOwner,Creation=expectedCreation,
                Deaths=expectedDeaths,StartingEntityId=row.StartingEntityId,OriginalCount=row.Multiplier,RecipeBinding=(string)original.Element("recipeBinding"),NativeRecipeBinding=NativeBinding(row),
                Save=source.Save,WorldKey=(string)node.Attribute("world"),Path=path,Image=image,Published=true,FinalOutputSource=source,ClientPurpose=purpose,Retention=memory};memory=null;
            if(!value.SourcePublished()){value.Dispose();return false;}originals.Add(value);owner=value;return true;
        }
        catch{return false;}
        finally{memory?.Dispose();}
    }
}
