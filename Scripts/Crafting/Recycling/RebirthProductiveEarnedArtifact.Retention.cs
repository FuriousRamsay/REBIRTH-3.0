using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedArtifact
{
    // File-retention capability only. No native mutation, output, reward,
    // cancellation or saved marker-absence authority is carried by this token.
    internal sealed class CompletionRetention
    {
        private readonly Prepared original;private readonly Guid custody;
        private bool consumed;
        internal CompletionRetention(Prepared value,Guid incarnation){original=value;custody=incarnation;}
        internal bool TryRetain()
        {
            if(consumed)return false;consumed=true;return original.RetainAfterMarkerRemoval(custody);
        }
    }
    internal sealed partial class Prepared
    {
        internal CompletionRetention CaptureCompletionRetention(byte[] expectedMarker)
        {
            try
            {
                if(station is RebirthCamperStationState||!Current()||expectedMarker==null||
                    !RebirthProductiveEarnedRowProof.MatchesMarker(expectedMarker,image)||
                    !RebirthProductiveEarnedCoordinator.TryNativeMarker(station,out var actual)||!actual.SequenceEqual(expectedMarker))return null;
                return new CompletionRetention(this,CustodyBirth);
            }
            catch{return null;}
        }
        internal bool RetainAfterMarkerRemoval(Guid custody)
        {
            try
            {
                // LoadedSource.CurrentIdentity intentionally requires the old
                // marker. Revalidate the token's pinned native object instead,
                // after the caller's exact clear has returned successfully.
                if(!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,world)||world.IsRemote()||
                    Path.GetFullPath(GameIO.GetSaveGameDir())!=save||WorldIdentity(world)!=worldIdentity||
                    station is RebirthCamperStationState||!NativeCurrent(world,station,custody)||
                    RebirthProductiveEarnedCoordinator.TryNativeMarker(station,out _)||
                    !path.EndsWith(".rew",StringComparison.Ordinal)||!FileMatches(path,image))return false;
                string directory=Path.GetDirectoryName(path);
                if(Path.GetFileName(directory)=="retained")return true;
                string archiveDirectory=Path.Combine(directory,"retained"),archive=Path.Combine(archiveDirectory,Path.GetFileName(path));
                if(!SafePath(archiveDirectory)||!SafePath(archive)||File.Exists(archive+".tmp"))return false;
                Directory.CreateDirectory(archiveDirectory);
                if(!SafePath(archiveDirectory))return false;
                if(File.Exists(archive))return FileMatches(archive,image); // Preserve both, including conflicts.
                if(!FileMatches(path,image)||RebirthProductiveEarnedCoordinator.TryNativeMarker(station,out _)||
                    !NativeCurrent(world,station,custody))return false;
                File.Move(path,archive);return FileMatches(archive,image);
            }
            catch{return false;} // No file is deleted or replaced on refusal.
        }
    }
}
