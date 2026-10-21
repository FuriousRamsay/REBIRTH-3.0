using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    private static bool PublishRemotePaidParentLocator(Context parent,Context child)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write(0x31505052u);writer.Write(parent.HeaderPath);writer.Write(Digest(parent.Image));writer.Write(Digest(child.Image));writer.Flush();
            return RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(child.Artifact.OriginalPath+".remote-paid-parent",stream.ToArray());
        }
    }
    private static bool TryReadRemotePaidParent(string original,byte[] image,RebirthProductiveEarnedRowProof.Frame frame,
        TileEntityWorkstation station,out Context parent,out Early child)
    {
        parent=null;child=null;
        try
        {
            byte[] locator=ReadNativeOutputMetadata(original+".remote-paid-parent",2048);string header,parentDigest;
            using(var stream=new MemoryStream(locator,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadUInt32()!=0x31505052u)return false;
                header=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,1024);parentDigest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
                if(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Digest(image)||stream.Position!=stream.Length||!Safe(header))return false;
            }
            string save=Path.GetFullPath(GameIO.GetSaveGameDir()),remote=Path.Combine(save,"RebirthProductiveEarned","remote")+Path.DirectorySeparatorChar;
            if(!Path.GetFullPath(header).StartsWith(remote,StringComparison.OrdinalIgnoreCase))return false;
            var hold=ReadEarly(header.Substring(0,header.Length-7)+".hold");
            string childHeader=Path.Combine(Path.GetDirectoryName(header),frame.Operation.ToString("N")+".header");
            child=ReadEarly(childHeader.Substring(0,childHeader.Length-7)+".hold");
            if(hold.Operation==frame.Operation||hold.World!=frame.World||!hold.Position.Equals(station.ToWorldPos())||
                child.Operation!=frame.Operation||child.Birth!=frame.Birth||child.World!=hold.World||child.Owner!=hold.Owner||child.Creation!=hold.Creation||
                child.Deaths!=hold.Deaths||!child.Position.Equals(hold.Position)||!TryClosedEarly(hold,out var closed)||closed.Kind!=2||closed.OriginalDigest!=parentDigest||
                !Manifest(child.Manifest,out _,out _,out _,out _,out var childDigest)||childDigest!=Digest(image)||
                !TryReadHeader(Read(childHeader),child.Owner,child.Creation,child.Deaths,station,frame,childDigest,out var childNonce)||childNonce!=child.Nonce)return false;
            string parentOriginal=Path.Combine(save,"RebirthProductiveEarned",hold.Birth.ToString("N"),hold.Operation.ToString("N")+".rew");
            byte[] parentImage=ReadNativeOutputMetadata(parentOriginal,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
            if(Digest(parentImage)!=parentDigest)return false;
            var description=new RebirthProductiveEarnedNativePendingPlan.Description{Birth=hold.Birth,Operation=hold.Operation,Child=frame,ChildImage=image,
                Before=frame.BeforeNative,BeforeOffset=frame.BeforeElapsedOffset};
            byte[] link=RebirthProductiveEarnedNativePendingPlan.ChildLink(parentImage,description);
            if(!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(parentOriginal+".earned-child",link)||
                !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(parentOriginal+".earned-child.invoking",link))return false;
            parent=new Context{World=GameManager.Instance.World,Station=station,HeaderPath=header,Image=parentImage,Operation=hold.Operation,
                Owner=hold.Owner,Creation=hold.Creation,Deaths=hold.Deaths,WorldKey=hold.World};
            return true;
        }
        catch{return false;}
    }
    private static bool TryAdoptRemoteSavedEarnedOriginal(Context source,RebirthProductiveEarnedSavedNativeSource.Evidence loaded)
    {
        IDisposable workspace=null;RebirthProductiveEarnedObservationBudget.Decoder decoder=null;
        try
        {
            if(OriginalStillOwnsNativeCard(source)||!IsNativeHandoffTick(source.Station)||source.Artifact?.Current()!=true||
                File.Exists(source.HeaderPath+".nativeOutput")||File.Exists(source.HeaderPath+".nativeOutput.tmp")||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*source.Image.Length+4L*RebirthProductiveEarnedRowProof.MaximumArtifactBytes+65536),out workspace))return false;
            if(loaded==null&&(decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())==null)return false;
            var frame=RebirthProductiveEarnedRowProof.Decode(source.Image);
            if(decoder!=null&&!decoder.Admit(frame,source.Image.Length))return false;
            if(!source.Artifact.MatchesAfter(source.Station)||!RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,frame.AfterNative,frame.AfterElapsedOffset))return false;
            if(loaded==null)
            {if(!source.Accepted||!source.AfterPublished||!ValidateAfterReceipt(source,ReadBeforeReceipt(source.HeaderPath+".afterSaved")))return false;}
            else if(loaded.Terminal||!loaded.PrimaryCurrent()||!loaded.Original.SequenceEqual(source.Image)||
                !loaded.CurrentIdentity(source.World,source.Station,frame.Birth,frame.Operation))return false;
            if(!TryReadRemotePaidParent(source.Artifact.OriginalPath,source.Image,frame,source.Station,out var parent,out var child))return false;
            byte[] image=source.Image,header=source.Header;var artifact=source.Artifact;string path=source.HeaderPath;
            Func<bool> current=()=>artifact.Current()&&RebirthProductiveEarnedArtifact.FileMatches(artifact.OriginalPath,image)&&
                RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(path,header);
            if(!RebirthProductiveEarnedCoordinator.AdoptRemoteSavedEarnedChild(source.Station,artifact,frame,current,checked((long)image.Length+header.Length+4096),
                ()=>TryNativeInheritedCustody(child))||!PublishNativeInheritedCustody(parent,frame,image,false))return false;
            DisposeAdoptedRemoteRoot(source);pending.Remove(source.Operation);early.Remove(source.Operation);coldEarly.RemoveAll(value=>value.Operation==source.Operation);
            RefreshEarlyAfterNativeClosure();return true;
        }
        catch{return false;}
        finally{decoder?.Dispose();workspace?.Dispose();}
    }
}
