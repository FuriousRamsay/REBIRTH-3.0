using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    internal static bool AcceptsSavedSource4=>installed;
    internal static bool FitsSavedSource4NativeFrames(byte[] before,byte[] after)
    {
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!RebirthRemoteResourcePlacedStationNativeReader.TryRead(before,out var first)||
                !RebirthRemoteResourcePlacedStationNativeReader.TryRead(after,out var last))return false;
            var a=RebirthProductiveEarnedNativeBody.Encode(first,0,out int ao);var b=RebirthProductiveEarnedNativeBody.Encode(last,0,out int bo);
            return RebirthProductiveEarnedNativeBody.TryRead(a,ao,out _,out _)&&RebirthProductiveEarnedNativeBody.TryRead(b,bo,out _,out _);
        }
    }
    private static string Source4DefinitionBinding(RecipeQueueItem row)
    {
        // Native read omits runtime recipe authoring fields. Ownership binds
        // its exact native projection; the full authored recipe remains in
        // the authenticated placed original and source4 Before/After receipt.
        var projected=RebirthCookingRemoteAdmissionBackend.ReadQueue(NativeBinding(row));
        if(projected.Length!=1||projected[0]?.Recipe==null)throw new InvalidDataException();
        return Binding(projected[0]);
    }
    internal static bool MatchesSourceDefinition(Witness source,RecipeQueueItem row)=>source?.Source4Image!=null||source?.InitialLoaded!=null?
        source.NativeRecipeBinding==NativeBinding(row)&&source.RecipeBinding==Source4DefinitionBinding(row):source?.RecipeBinding==Binding(row);
    private static XElement Source4Receipt(Guid source,string binding,string proof,string owner,string creation,int deaths,string world,string birth,
        RecipeQueueItem row,int count,byte[] before,byte[] after)=>
        new XElement("source4QueueOwner",new XAttribute("version",1),new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),
            new XAttribute("proof",proof),new XAttribute("owner",owner),new XAttribute("creation",creation),new XAttribute("deaths",deaths),new XAttribute("world",world),
            new XAttribute("birth",birth),new XAttribute("entity",row.StartingEntityId),new XAttribute("count",count),
            new XElement("nativeRow",NativeBinding(row)),new XElement("before",Convert.ToBase64String(before)),new XElement("after",Convert.ToBase64String(after)));
    internal static bool AdoptSavedSource4(RebirthRemoteResourcePlacedStationOriginal.SavedEarnedOwner source)
    {
        if(!installed||source?.Current()!=true||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||
            source.World==null||source.World.IsRemote()||source.Row.StartingEntityId!=source.EntityId||
            (source.Actor!=null&&(!RebirthStablePlayerIdentity.TryResolveServerEntity(source.Actor,out var owner)||owner.StorageKey!=source.Owner||
                source.Actor.Died!=source.Deaths||source.Creation!=RebirthCamperCharacterAccess.CurrentCreation(source.Actor)))||
            !RebirthLogisticsEndpointIdentity.TryGetIncarnation(source.World,source.Station,out var birth)||birth!=source.Birth||source.Row.Recipe.IsScrap||
            RebirthCookingHeat.Managed(source.Row.Recipe)||RebirthRecyclerRuntime.IsMarked(source.Row.Recipe)||RebirthStationGridQueue.IsMarked(source.Row.Recipe))return false;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(48L*1024*1024,out var memory))return false;
        using var retained=memory;
        byte[] before=source.Before(),after=source.After();if(before==null||after==null||!source.Current())return false;
        // Retain exact typed source/native publication provenance before the
        // ordinary issuer can expose this row to its existing completion path.
        var record=Source4Receipt(source.Source,source.Binding,source.Proof,source.Owner,source.Creation,source.Deaths,source.WorldKey,source.Birth,source.Row,source.OriginalCount,before,after);
        byte[] bytes=Encoding.UTF8.GetBytes(record.ToString(SaveOptions.DisableFormatting));
        if(bytes.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;
        string path=Path.Combine(GameIO.GetSaveGameDir(),"RebirthProductiveEarnedUi","queueOwners",source.Owner,source.Source.ToString("N")+".source4");
        if(!source.Current()||!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(path,bytes)||!source.Current())return false;
        // The source receipt keeps its exact Persistency image. The existing
        // issuer's beforeNative field uses the detached ToServer grammar;
        // converting that grammar does not assert a native AddRecipe callback.
        if(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(before,out var nativeBefore))return false;
        byte[] queueBefore=RebirthProductiveEarnedNativeBody.Encode(nativeBefore,0,out int elapsedOffset);
        if(!RebirthProductiveEarnedNativeBody.TryRead(queueBefore,elapsedOffset,out _,out _))return false;
        if(TryRestoreSavedSource4(source,path,bytes,queueBefore,out bool existing))return source.Current();
        if(existing)return false;
        return Issue(source.Actor,source.Station,source.Row,source.Owner,source.Creation,source.Deaths,queueBefore,source.Source,path,bytes,source)&&source.Current();
    }
    // Historical identity publication only. This never mints a live row
    // witness; native reentry still requires the private actual-load producer.
    internal static bool HasSavedSource4Identity(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourcePlacedStationArtifactReader.Image artifact,string proof)
    {
        if(!installed||root==null||artifact==null||proof==null||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(48L*1024*1024,out var memory))return false;
        using(memory)
        {
            try
            {
                var full=artifact.Copy();var plan=full.Element("plan");var saved=full.Element("savedBefore");int slot=(int)plan.Attribute("slot");
                if(!RebirthRemoteResourcePlacedStationQueue.TryDecode(plan.Element("queue").Value,out var rows)||slot<0||slot>=rows.Length||rows[slot]?.Recipe==null)return false;
                var row=rows[slot];byte[] snapshot=Convert.FromBase64String(saved.Element("snapshot").Value),after=Convert.FromBase64String(plan.Element("afterTile").Value);
                long offset=(long)saved.Attribute("tileOffset");int length=(int)saved.Attribute("tileLength");if(offset<8||length<1||offset>snapshot.LongLength-length)return false;
                var before=new byte[length];Buffer.BlockCopy(snapshot,(int)offset,before,0,length);
                byte[] receipt=Encoding.UTF8.GetBytes(Source4Receipt(root.Operation,root.Binding,proof,root.Owner,root.Creation,artifact.Original.Deaths,root.World,artifact.Birth,row,row.Multiplier,before,after).ToString(SaveOptions.DisableFormatting));
                string save=Path.GetFullPath(GameIO.GetSaveGameDir()),prefix=Path.Combine(save,"RebirthProductiveEarnedUi","queueOwners",root.Owner,root.Operation.ToString("N"));
                if(receipt.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||!RebirthProductiveEarnedArtifact.FileMatches(prefix+".source4",receipt)||
                    !RebirthRemoteResourcePlacedStationNativeReader.TryRead(before,out var first)||!RebirthRemoteResourcePlacedStationNativeReader.TryRead(after,out var last))return false;
                byte[] beforeNative=RebirthProductiveEarnedNativeBody.Encode(first,0,out _),accepted=RebirthProductiveEarnedNativeBody.Encode(last,0,out _);
                var original=new XElement("nativeQueueOwnerOriginal",new XAttribute("operation",root.Operation.ToString("N")),new XAttribute("birth",artifact.Birth),
                    new XAttribute("owner",root.Owner),new XAttribute("creation",root.Creation),new XAttribute("entity",row.StartingEntityId),new XAttribute("deaths",artifact.Original.Deaths),
                    new XAttribute("world",RebirthProductiveEarnedArtifact.WorldIdentity(GameManager.Instance.World)),new XAttribute("save",save),
                    new XElement("recipeBinding",Source4DefinitionBinding(row)),new XElement("beforeNative",Convert.ToBase64String(beforeNative)),new XElement("acceptedNative",Convert.ToBase64String(accepted)));
                original.SetAttributeValue("source4",ProofHash(receipt));byte[] bytes=Encoding.UTF8.GetBytes(original.ToString(SaveOptions.DisableFormatting));
                return bytes.Length<=RebirthProductiveEarnedRowProof.MaximumArtifactBytes&&RebirthProductiveEarnedArtifact.FileMatches(prefix+".original",bytes);
            }
            catch{return false;}
        }
    }
    private static bool TryRestoreSavedSource4(RebirthRemoteResourcePlacedStationOriginal.SavedEarnedOwner source,string receiptPath,byte[] receipt,byte[] before,out bool existing)
    {
        existing=false;
        string save=Path.GetFullPath(GameIO.GetSaveGameDir()),path=Path.Combine(save,"RebirthProductiveEarnedUi","queueOwners",source.Owner,source.Source.ToString("N")+".original");
        if(!RebirthProductiveEarnedArtifact.SafePath(path))return false;
        if(!File.Exists(path))return false;existing=true;
        ObserveWorld();var matches=originals.Where(value=>ReferenceEquals(value.Station,source.Station)&&value.Matches(source.Row)).ToArray();
        if(matches.Length!=0)return matches.Length==1&&matches[0].Operation==source.Source&&matches[0].Source4Image!=null&&matches[0].Source4Image.SequenceEqual(receipt)&&matches[0].SourcePublished();
        long length=new FileInfo(path).Length;
        if(length<1||length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||originals.Count>=64||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*length+receipt.LongLength+65536),out var memory))return false;
        Witness result=null;
        try
        {
            byte[] bytes=File.ReadAllBytes(path);if(bytes.LongLength!=length)return false;
            var node=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(bytes),RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
            if(node.Name!="nativeQueueOwnerOriginal"||node.Attributes().Count()!=9||node.Elements().Count()!=3||
                !node.Elements().Select(value=>value.Name.ToString()).SequenceEqual(new[]{"recipeBinding","beforeNative","acceptedNative"})||
                (string)node.Attribute("operation")!=source.Source.ToString("N")||(string)node.Attribute("birth")!=source.Birth||
                (string)node.Attribute("owner")!=source.Owner||(string)node.Attribute("creation")!=source.Creation||
                (int?)node.Attribute("entity")!=source.Row.StartingEntityId||(int?)node.Attribute("deaths")!=source.Deaths||
                (string)node.Attribute("world")!=RebirthProductiveEarnedArtifact.WorldIdentity(source.World)||(string)node.Attribute("save")!=save||
                (string)node.Attribute("source4")!=ProofHash(receipt)||node.Element("recipeBinding").Value!=Source4DefinitionBinding(source.Row)||
                !Convert.FromBase64String(node.Element("beforeNative").Value).SequenceEqual(before)||!source.Current())return false;
            // Rehydration requires the private actual saved-source producer.
            // Parse and qualify the old native envelope; never recapture a new
            // elapsed value and overwrite an earlier immutable owner original.
            byte[] accepted=Convert.FromBase64String(node.Element("acceptedNative").Value);
            if(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(source.After(),out var image))return false;
            byte[] expected=RebirthProductiveEarnedNativeBody.Encode(image,0,out int offset);
            if(!RebirthProductiveEarnedNativeBody.TryRead(accepted,offset,out _,out _)||
                !RebirthProductiveEarnedNativeBody.ComparisonDigest(accepted,offset).SequenceEqual(RebirthProductiveEarnedNativeBody.ComparisonDigest(expected,offset)))return false;
            result=new Witness{World=source.World,Station=source.Station,Recipe=source.Row.Recipe,OriginalOwnerKey=source.Owner,Creation=source.Creation,
                StartingEntityId=source.EntityId,OriginalCount=source.OriginalCount,Deaths=source.Deaths,RecipeBinding=Source4DefinitionBinding(source.Row),NativeRecipeBinding=NativeBinding(source.Row),
                Save=save,WorldKey=RebirthProductiveEarnedArtifact.WorldIdentity(source.World),Operation=source.Source,Birth=Guid.ParseExact(source.Birth,"N"),
                Path=path,Image=bytes,Source4Path=receiptPath,Source4Image=(byte[])receipt.Clone(),Retention=memory,Published=true};
            memory=null;
            if(!source.Current()||!result.SourcePublished())return false;
            originals.Add(result);RecordRemoteIssuedSource(result);result=null;return true;
        }
        catch{return false;}
        finally{result?.Dispose();memory?.Dispose();}
    }
}
