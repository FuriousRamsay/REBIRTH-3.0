using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // Metadata/resource successor of an already saved native owner terminal.
    // This token grants no native write, primary restoration or owner effect.
    internal sealed class CachedPhysicalPublication:IDisposable
    {
        internal readonly CachedHostOriginal Source;
        internal readonly NativeHostOriginal NativeSource;
        internal readonly RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication Previous;
        internal readonly Guid Operation;
        internal readonly World World;
        internal readonly TileEntityWorkstation Station;
        internal readonly string Path;
        internal readonly long NativeReadFence;
        private CachedCustody root;
        private byte[] image;
        private IDisposable retention;
        private readonly RebirthWorkstationCompletionOriginal original;
        internal CachedCustody Root=>root;
        internal bool IsSameOwner=>root?.Original.Element("cachedOriginal")?.Element("nativeOwnerLedger")==null;
        internal string NativeDigest=>root==null?null:RebirthProductiveEarnedUiDeferredTransport.CachedNativeDigest(root.Native,root.Offset);
        internal byte[] Body=>root?.Native;
        internal int Offset=>root?.Offset??-1;
        private CachedPhysicalPublication(CachedCustody cache,RebirthWorkstationCompletionOriginal delivered,string path,byte[] receipt,IDisposable lease,long fence)
        {
            root=cache;Source=cache.HostOriginal;NativeSource=cache.RemoteSource;Previous=cache.OwnerTerminal;Operation=cache.Operation;
            World=cache.World;Station=cache.Station;original=delivered;Path=path;image=receipt;retention=lease;NativeReadFence=fence;
        }
        internal bool CurrentSavedSource()=>retention!=null&&root?.Active==true&&root.Physical&&root.SavedCurrent()&&
            root.PrimaryCurrent()&&root.WorldGuid==RebirthWorkstationCompletionOutbox.CurrentGuid(GameManager.Instance?.World)&&
            root.Save==System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())&&
            (IsSameOwner?MatchesSameOwner():HasSavedCachedOwnerEvent(root)&&ReferenceEquals(root.OwnerTerminal,Previous)&&
                (Previous==null||Previous.HasSavedCachedIdentity(root)))&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,image)&&
            RebirthWorkstationCompletionOutbox.TryReadSaved(original,out _);
        internal long MeasureImage()=>image?.LongLength??0;
        internal byte[] CopyImage()=>CurrentSavedSource()?(byte[])image.Clone():null;
        private bool MatchesSameOwner()=>root.Original.Element("cachedOriginal")?.Element("remoteCachedOwnerEffect") is XElement effect&&
            root.ReserveRetirementWorkspace(checked(16*RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(effect)+65536))&&
            RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.MatchesSavedCachedBoundaryOwner(effect.Elements().SingleOrDefault());
        public void Dispose(){root=null;image=null;retention?.Dispose();retention=null;}
        internal static bool TryIssue(CachedCustody cache,out CachedPhysicalPublication proof)
        {
            proof=null;
            if(!HasSavedCachedOwnerEvent(cache)||
                !TryReadSavedCachedPhysical(cache,out var original,out var physical)||
                !RebirthWorkstationCompletionOutbox.TryReadSaved(original,out _))return false;
            if(cache.PhysicalPublication!=null)
            {proof=cache.PhysicalPublication;return proof.CurrentSavedSource();}
            long bound=checked(65536+cache.SavedImage.LongLength+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(physical));
            if(!cache.ReserveRetirementWorkspace(checked(16*bound)))return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",cache.Operation.ToString("N")),new XAttribute("world",cache.WorldGuid),
                new XElement(cache.Original.Element("before")),
                new XElement("cachedPhysicalPublication",new XAttribute("version",1),new XAttribute("original",HistoricalHash(cache.Image)),
                    new XAttribute("ownerTerminal",cache.OwnerTerminal?.Path??""),new XAttribute("ownerSaved",cache.OwnerSavedPath??""),
                    new XAttribute("ownerSavedDigest",cache.OwnerSavedImage==null?"":RebirthWorkstationCompletionOriginal.Hash(cache.OwnerSavedImage)),
                    new XAttribute("physicalPath",cache.Path+".physical"),new XAttribute("physicalDigest",RebirthWorkstationCompletionOriginal.Hash(physical.ToString(SaveOptions.DisableFormatting))),
                    new XAttribute("nativeSaved",cache.SavedPath),new XAttribute("nativeSavedDigest",HistoricalHash(cache.SavedImage)),
                    new XAttribute("originalDelivery",original.Digest)));
            string path=cache.Path+".physical-publication";
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!cache.NativeCurrent()||
                !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return false;
            var image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(image==null)return false;
            if(!RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(cache.World,cache.Station,out var fence))return false;
            var lease=cache.RetainResource();if(lease==null)return false;
            var token=new CachedPhysicalPublication(cache,original,path,image,lease,fence);
            if(!token.CurrentSavedSource()){token.Dispose();return false;}
            cache.PhysicalPublication=token;proof=token;return true;
        }
    }
}
