using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedArtifact
{
    internal sealed partial class Prepared
    {
        private byte[] mobileOriginHash,mobileSuffixHash;
        internal bool BindMobileDependency(bool suffix,byte[] bytes)
        {
            byte[] digest=RebirthProductiveEarnedMobileOrigin.Digest(bytes);
            var existing=suffix?mobileSuffixHash:mobileOriginHash;
            if(existing!=null)return existing.SequenceEqual(digest);
            if(suffix)mobileSuffixHash=digest;else mobileOriginHash=digest;return true;
        }
        private bool MobileDependenciesCurrent()
        {
            return MobileDependencyCurrent(".mobile-origin",mobileOriginHash)&&MobileDependencyCurrent(".mobile-initial-suffix",mobileSuffixHash);
        }
        private bool MobileDependencyCurrent(string suffix,byte[] expected)
        {
            if(expected==null)return true;
            string source=path+suffix;
            if(!SafePath(source)||File.Exists(source+".tmp")||!File.Exists(source)||
                new FileInfo(source).Length>RebirthProductiveEarnedMobileOrigin.MaximumBytes)return false;
            using(var stream=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var sha=System.Security.Cryptography.SHA256.Create())return sha.ComputeHash(stream).SequenceEqual(expected);
        }
    }
    private static bool TryLoadObservedMobile(RebirthCamperStationState station,out Prepared prepared)
    {
        prepared=null;
        try
        {
            if(station?.BoundVehicle==null||
                !RebirthCookingMillingPublication.TryObserveNativeLoadedVehicle(station.BoundVehicle,out var loaded)||
                !loaded.TryCopyStationBody(station.InstallationId,out var archived))return false;
            var runtime=RebirthCamperModuleRuntime.For(station.BoundVehicle);
            var installation=runtime.Stations.Find(station.InstallationId);
            if(!ReferenceEquals(installation?.State,station))return false;
            var saved=RebirthCamperStationSnapshot.Restore(station.InstallationId,installation.InstalledItem,archived);
            byte[] marker=saved.CopyProductiveEarnedMarker();
            if(!saved.ProductiveEarnedV5||!RebirthProductiveEarnedRowProof.TryReadMarker(marker,out var birth,out var operation)||
                birth!=station.InstallationId||!TryReadPreparedIdentity(station,birth,operation,out var original,out _))return false;
            var frame=original.ReadFrame();
            if(!frame.HasMobileOrigin||!original.MatchesMarker(marker)||
                !station.CopyProductiveEarnedMarker().SequenceEqual(marker)||
                File.Exists(original.OriginalPath+".mobile-output-invoking")||File.Exists(original.OriginalPath+".mobile-output-invoking.tmp")||
                !File.Exists(original.OriginalPath+".mobile-initial-suffix")||
                !RebirthProductiveEarnedMobileOrigin.TryRead(original,frame,out var source)||source.Vehicle!=runtime.Identity||
                !RebirthProductiveEarnedMobileOrigin.TryEffectiveAfter(original,source,out var after)||
                !RebirthProductiveEarnedMobileOrigin.Canonical(saved,installation.InstalledItem).SequenceEqual(after)||
                !loaded.OriginalCurrent())return false;
            // This capability originates only at the actual native file -> ECD
            // -> factory -> returned EntityVehicle.Read installed source. Later
            // legitimate saves use our own full-body publication predicates.
            original.observedRemoteIdentity=()=>loaded.Current()&&
                NativeCurrent(GameManager.Instance?.World,station,birth)&&
                RebirthCamperModuleRuntime.For(station.BoundVehicle).Identity==source.Vehicle;
            if(!original.MatchesAfter(station))return false;prepared=original;return true;
        }
        catch{return false;}
    }
}
