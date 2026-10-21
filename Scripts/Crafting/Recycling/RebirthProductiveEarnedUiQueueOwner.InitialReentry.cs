using System;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    private static bool TryIssueInitialQueueOwner(TileEntityWorkstation station,RecipeQueueItem row)
    {
        if(!installed||station==null||row?.Recipe==null||!OrdinaryInitial(row))return false;
        InitialNativeOriginal source;
        lock(initialLoadGate)
        {var matches=initialNativeOriginals.Where(value=>ReferenceEquals(value.Tile,station)&&ReferenceEquals(value.Row,row)&&value.OriginalRowCurrent()).ToArray();if(matches.Length!=1)return false;source=matches[0];}
        IDisposable memory=null;Witness witness=null;
        try
        {
            if(!source.OriginalRowCurrent()||originals.Count>=64)return false;
            long length=new FileInfo(source.OriginalPath).Length;
            if(length<1||length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*length+8L*source.Native.TileLength+65536),out memory))return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return false;
                byte[] bytes=File.ReadAllBytes(source.OriginalPath);
                if(bytes.LongLength!=length||InitialHash(bytes)!=source.OriginalDigest)return false;
                var node=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(bytes),RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
                if(node.Name!="nativeQueueOwnerOriginal"||node.Attributes().Count()!=8||node.Elements().Count()!=3||
                    !node.Elements().Select(value=>value.Name.ToString()).SequenceEqual(new[]{"recipeBinding","beforeNative","acceptedNative"})||
                    (string)node.Attribute("operation")!=source.Reference.Source.ToString("N")||(string)node.Attribute("birth")!=source.Reference.Birth||
                    (string)node.Attribute("owner")!=source.Owner||(string)node.Attribute("creation")!=source.Creation||(int?)node.Attribute("entity")!=source.Entity||
                    (int?)node.Attribute("deaths")!=source.Deaths||(string)node.Attribute("world")!=source.WorldKey||(string)node.Attribute("save")!=source.Save||
                    row.StartingEntityId!=source.Entity||NativeBinding(row)!=source.RowBinding)return false;
                string snapshotPath=source.ReceiptPath+".snapshot";
                if(InitialFileHash(snapshotPath,source.Native.SnapshotLength,8)!=source.Native.SnapshotDigest)return false;
                var tile=new byte[source.Native.TileLength];
                using(var stream=new FileStream(snapshotPath,FileMode.Open,FileAccess.Read,FileShare.Read))
                {stream.Position=source.Native.Offset;int read=0;while(read<tile.Length){int count=stream.Read(tile,read,tile.Length-read);if(count<1)return false;read+=count;}}
                if(InitialHash(tile)!=source.Native.TileDigest||!RebirthRemoteResourcePlacedStationNativeReader.TryRead(tile,out var saved))return false;
                byte[] expected=RebirthProductiveEarnedNativeBody.Encode(saved,0,out int elapsed);
                byte[] accepted=Convert.FromBase64String(node.Element("acceptedNative").Value);
                if(!RebirthProductiveEarnedNativeBody.TryRead(accepted,elapsed,out var acceptedImage,out _)||source.Slot<0||source.Slot>=acceptedImage.Queue.Length||
                    acceptedImage.Queue[source.Slot]?.Recipe==null||acceptedImage.Queue[source.Slot].Multiplier!=source.OriginalCount||
                    NativeBinding(acceptedImage.Queue[source.Slot])!=source.RowBinding||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(accepted,elapsed,expected,elapsed)||!source.OriginalRowCurrent())return false;
                witness=new Witness{World=source.World,Station=station,Recipe=row.Recipe,OriginalOwnerKey=source.Owner,Creation=source.Creation,StartingEntityId=source.Entity,
                    OriginalCount=source.OriginalCount,Deaths=source.Deaths,RecipeBinding=Source4DefinitionBinding(row),NativeRecipeBinding=source.RowBinding,Save=source.Save,WorldKey=source.WorldKey,
                    Operation=source.Reference.Source,Birth=Guid.ParseExact(source.Reference.Birth,"N"),Path=source.OriginalPath,Image=bytes,Retention=memory,InitialLoaded=source,Published=true};
                memory=null;if(!witness.SourcePublished())return false;originals.Add(witness);witness=null;return true;
            }
        }
        catch{return false;}
        finally{witness?.Dispose();memory?.Dispose();}
    }
}
