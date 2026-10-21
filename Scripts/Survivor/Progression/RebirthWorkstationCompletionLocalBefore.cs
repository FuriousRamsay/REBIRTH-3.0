using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed class LocalNativeBefore:IDisposable
    {
        internal readonly Frame Frame;internal readonly long Session;
        private RebirthProductiveEarnedObservationBudget memory;
        private RebirthRemoteResourceNativePublication.Watch watch;
        private IDisposable retained;private byte[] native,seal,accountImage;private int offset;private XElement candidate;
        private readonly string path;internal bool Saved;
        internal string SavedPath=>Saved?path:null;
        internal string SavedDigest=>Saved&&seal!=null?HistoricalHash(seal):null;
        internal bool TryCopyActualSaved(out string savedPath,out byte[] savedImage,out byte[] budgetImage)
        {
            savedPath=null;savedImage=budgetImage=null;if(!Saved||!Current()||seal==null||accountImage==null)return false;
            savedPath=path;savedImage=(byte[])seal.Clone();budgetImage=(byte[])accountImage.Clone();return true;
        }
        private LocalNativeBefore(Frame frame,long session){Frame=frame;Session=session;path=frame.Path+".native-before."+frame.Intent.Elements("cachedContinuation").Count()+".saved";}
        internal bool IdentityCurrent()=>!Frame.Closed&&!Frame.Observation.World.IsRemote()&&!Frame.Observation.RanUpdate&&
            ReferenceEquals(Frame.LocalBefore,this)&&Frame.EarlierOwner?.Current()==true&&Frame.EarlierOwner.SourcePublished()&&
            Frame.Observation.Current()&&Frame.Observation.RecipeMatches()&&
            RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(Frame.Observation.Window,out var session)&&session==Session;
        internal bool Current()
        {
            if(!IdentityCurrent()||native==null||memory==null)return false;
            using(var callbacks=memory.TryEnterCallbacks())return callbacks!=null&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(Frame.Observation.Station,native,offset)&&
                (!Saved||RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,seal));
        }
        internal bool MatchesClosedNative()
        {
            if(native==null||memory==null)return false;
            using(var callbacks=memory.TryEnterCallbacks())return callbacks!=null&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(Frame.Observation.Station,native,offset);
        }
        internal static LocalNativeBefore Begin(Frame frame)
        {
            if(frame.Observation.World.IsRemote()||frame.EarlierOwner?.Current()!=true||
                !RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(frame.Observation.Window,out var session))return null;
            LocalNativeBefore result=null;
            long measure=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(ActiveBefore(frame.Intent))+65536;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*measure),out var workspace))return null;
            using(workspace)try
            {
                var before=ActiveBefore(frame.Intent);
                var primary=new XElement("workstationCompletionIntent",new XAttribute("operation",frame.Observation.Operation.ToString("N")),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(frame.Observation.World)),new XElement("before",new XElement(before)));
                string seed=frame.Path+".native-before."+frame.Intent.Elements("cachedContinuation").Count()+".original";
                if(!RebirthWorkstationCompletionPublication.Write(seed,primary))return null;
                var image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(seed);if(image==null)return null;
                result=new LocalNativeBefore(frame,session);
                result.accountImage=image;
                result.memory=RebirthProductiveEarnedObservationBudget.TryOpenNativeCompletion(frame.Observation.World,frame.EarlierOwner.CurrentCustodyBirth,
                    frame.Observation.Operation,image);if(result.memory==null){result.Dispose();return null;}
                if(!result.memory.TryReserveAllocation(checked(16*measure),out result.retained)){result.Dispose();return null;}
                result.native=Convert.FromBase64String((string)before.Element("nativeBefore"));result.offset=(int)before.Attribute("nativeElapsedOffset");
                if(result.native.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes){result.Dispose();return null;}
                return result;
            }
            catch{result?.Dispose();return null;}
        }
        internal bool TrySeal()
        {
            if(!Current())return false;if(Saved)return true;
            if(watch==null&&candidate==null)
            {
                if(!RebirthRemoteResourceNativeBinding.TryResolve(Frame.Observation.World,
                    RemoteResourceIdentity.Workstation(Frame.Observation.Station.ToWorldPos()),RemoteResourceSourceKind.WorkstationOutput,
                    Frame.EarlierOwner.CurrentCustodyBirth.ToString("N"),out var binding))return false;
                watch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,Current,memory);
            }
            if(candidate==null)
            {
                if(watch==null||!watch.TryReadPublishedTileDescriptor(out var published,out long at,out int length,out string digest)||!Current())return false;
                candidate=new XElement("workstationCompletionIntent",new XAttribute("operation",Frame.Observation.Operation.ToString("N")),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(Frame.Observation.World)),
                    new XElement("before",new XElement(ActiveBefore(Frame.Intent))),
                    new XElement("localNativeBeforeSaved",new XAttribute("session",Session),new XAttribute("path",published.Path),
                        new XAttribute("payload",published.PayloadDigest),new XAttribute("offset",at),new XAttribute("length",length),new XAttribute("tile",digest)));
            }
            if(!RebirthWorkstationCompletionPublication.Write(path,candidate))return false;
            seal=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(seal==null||!Current())return false;
            Saved=true;watch?.Dispose();watch=null;return Current();
        }
        public void Dispose(){watch?.Dispose();watch=null;native=seal=accountImage=null;candidate=null;retained?.Dispose();retained=null;memory?.Dispose();memory=null;}
    }
    private static bool TryPrepareLocalNativeBefore(Frame frame)
    {
        if(frame.LocalBefore==null)frame.LocalBefore=LocalNativeBefore.Begin(frame);
        if(frame.LocalBefore?.TrySeal()!=true)return false;
        frame.NativeBefore=NativeBeforePublication.FromLocalSaved(frame);
        return frame.NativeBefore!=null&&frame.NativeBefore.Current()&&RetireSameOwnerCachedWithNativeBefore(frame.NativeBefore)&&
            RebirthProductiveEarnedUiCoordinator.AcceptNativeBeforePublication(frame.NativeBefore);
    }
    private static bool CanTickLocalNativeBefore(TileEntityWorkstation station)=>!frames.Values.Any(frame=>frame.LocalBefore!=null&&
        !frame.Closed&&!frame.Observation.RanUpdate&&ReferenceEquals(frame.Observation.World,GameManager.Instance?.World)&&
        ReferenceEquals(frame.Observation.Station,station));
}
