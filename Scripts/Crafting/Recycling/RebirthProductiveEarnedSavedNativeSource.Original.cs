using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal static partial class RebirthProductiveEarnedSavedNativeSource
{
    // This locator is issued solely inside real validated original preparation,
    // before any productive setter. It locates a future real native load; it is
    // never a saved-After receipt, earned permission or replay permit.
    internal static bool PublishOriginalLocation(RebirthProductiveEarnedArtifact.Prepared artifact,
        TileEntityWorkstation station,RebirthProductiveEarnedRowProof.Frame frame)
    {
        try
        {
            var world=GameManager.Instance?.World;var chunk=station?.GetChunk();
            if(artifact?.Current()!=true||chunk==null||station is RebirthCamperStationState||world?.IsRemote()!=false||
                !ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station)||
                !Guid.TryParse(RebirthWorkstationCompletionOutbox.CurrentGuid(world),out var guid)||guid==Guid.Empty||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,frame.BeforeNative,frame.BeforeElapsedOffset))return false;
            byte kind=(byte)(RebirthProductiveEarnedCoordinator.TryNativeMarker(station,out _)?1:0);
            byte[] record;
            using(var buffer=new MemoryStream(105))using(var writer=new BinaryWriter(buffer))using(var sha=SHA256.Create())
            {
                writer.Write(0x314F4552u);writer.Write(kind); // REO1, initial vs predecessor-owned original.
                writer.Write(frame.Birth.ToByteArray());writer.Write(frame.Operation.ToByteArray());writer.Write(guid.ToByteArray());
                writer.Write(chunk.X);writer.Write(chunk.Z);var p=station.localChunkPos;writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);
                writer.Write(sha.ComputeHash(artifact.CopyImage()));writer.Flush();record=buffer.ToArray();
            }
            return record.Length==105&&RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(artifact.OriginalPath+".native-location",record)&&artifact.Current();
        }
        catch{return false;}
    }
    internal static bool RegisterPreparedOriginal(string original,string save)
    {
        Evidence value=null;
        try
        {
            if(!ThreadManager.IsMainThread())return false;save=Path.GetFullPath(save);
            lock(cachedLoadGate)
            {
                var same=cachedLoadRequests.Where(request=>string.Equals(request.Directory,save,RebirthStationCompletionOriginalScope.PathComparison)&&
                    (string.Equals(request.Custody.Path,original,RebirthStationCompletionOriginalScope.PathComparison)||
                     string.Equals(request.Custody.Path,original+".earned-load",RebirthStationCompletionOriginalScope.PathComparison))).ToArray();
                if(same.Length!=0)return same.Length==1&&same[0].Custody.PrimaryCurrent();
                if(cachedLoadRequests.Count>=4)return false;
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterCandidateDecoder())
                {if(decoder==null||!TryReadOriginalLocation(original,save,out value))return false;}
                if(!value.CanEnterNativeLoad())return false;
                cachedLoadRequests.Add(value.Load);value=null;return true;
            }
        }
        catch{return false;}
        finally{value?.Dispose();}
    }
    private static bool TryReadOriginalLocation(string original,string save,out Evidence source)
    {
        source=null;Evidence value=null;
        try
        {
            if(!original.EndsWith(".rew",StringComparison.Ordinal)||!RebirthProductiveEarnedArtifact.SafePath(original)||
                File.Exists(original+".tmp")||File.Exists(original+".native-location.tmp"))return false;
            byte[] location=Read(original+".native-location",105);
            if(location.Length!=105)return false;
            Guid birth,operation,guid;int x,z,px,py,pz;byte[] digest;
            using(var stream=new MemoryStream(location,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadUInt32()!=0x314F4552u||reader.ReadByte()!=0)return false; // Child requires its full predecessor protocol.
                birth=new Guid(reader.ReadBytes(16));operation=new Guid(reader.ReadBytes(16));guid=new Guid(reader.ReadBytes(16));
                x=reader.ReadInt32();z=reader.ReadInt32();px=reader.ReadInt32();py=reader.ReadInt32();pz=reader.ReadInt32();digest=reader.ReadBytes(32);
                if(birth==Guid.Empty||operation==Guid.Empty||guid==Guid.Empty||stream.Position!=stream.Length)return false;
            }
            string expected=Path.Combine(save,"RebirthProductiveEarned",birth.ToString("N"),operation.ToString("N")+".rew");
            if(!string.Equals(Path.GetFullPath(original),expected,RebirthStationCompletionOriginalScope.PathComparison))return false;
            long length=new FileInfo(original).Length;
            if(length<100||length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*length+65536),out var retention))return false;
            value=new Evidence(retention){Path=original,Directory=save,WorldGuid=guid.ToString("D"),
                MarkerKey="RebirthProductiveEarnedV1:"+px+":"+py+":"+pz};
            value.Image=Read(original,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
            using(var sha=SHA256.Create())if(!sha.ComputeHash(value.Image).SequenceEqual(digest))return false;
            value.Frame=RebirthProductiveEarnedRowProof.Decode(value.Image);var frame=value.Frame;
            using(var sha=SHA256.Create())
                if(frame.World!=Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(guid.ToString("N")))))return false;
            if(!frame.HasNativeBodies||!frame.HasRecipeDefinition||frame.Birth!=birth||frame.Operation!=operation||
                !TryInitialEarnedBody(original,value.Image,frame,out var earned,out int earnedOffset,out var suffixDependency)||
                !RebirthProductiveEarnedNativeBody.TryMeasure(earned,earnedOffset,out long graph,out long scratch)||
                !RebirthProductiveEarnedObservationBudget.TryResizeDetached(retention,
                    checked(8L*length+4L*earned.Length+3*graph+scratch+65536))||
                !RebirthProductiveEarnedRowProof.TryDecodeNative(frame,out var before,out _,out _,out _)||
                frame.WorkedSeconds!=(before.CraftingTimeLeft>0?before.CraftingTimeLeft:before.OneItemCraftTime)||
                !RebirthProductiveEarnedNativeBody.TryRead(earned,earnedOffset,out var native,out _)||
                native.LocalPosition.x!=px||native.LocalPosition.y!=py||native.LocalPosition.z!=pz)return false;
            value.Original=value.Image;value.Marker=RebirthProductiveEarnedRowProof.Marker(frame,value.Image);
            value.Native=earned;value.NativeOffset=earnedOffset;
            value.Dependencies=suffixDependency==null?new[]{NativeChildDependencyFor(original+".native-location",location)}:
                new[]{NativeChildDependencyFor(original+".native-location",location),suffixDependency};
            value.Load=new CachedLoadRequest{Custody=value,Directory=save,StartupOrigin=original,X=x,Z=z,Snapshot=Array.Empty<byte>(),
                Tile=new byte[checked(value.Native.Length+10)],PredictedNative=true,MaximumGraph=graph,MaximumScratch=scratch};
            if(!value.PrimaryCurrent())return false;source=value;value=null;return true;
        }
        catch{return false;}
        finally{value?.Dispose();}
    }
}
