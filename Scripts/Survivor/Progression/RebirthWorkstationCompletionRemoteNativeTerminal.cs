using System;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed partial class NativeHostOriginal
    {
        private byte[] ownerTerminal;private string ownerTerminalPath;private IDisposable ownerTerminalAllocation;
        private void ReleaseRemoteOwnerTerminal(){ownerTerminal=null;ownerTerminalAllocation?.Dispose();ownerTerminalAllocation=null;}
        internal bool RemoteOwnerTerminalSaved=>SourceCurrent()&&ownerTerminal!=null&&
            RebirthProductiveEarnedArtifact.FileMatches(ownerTerminalPath,ownerTerminal);
        internal bool RemoteOwnerTerminalHistoricalSaved=>ownerTerminal!=null&&
            RebirthProductiveEarnedArtifact.FileMatches(ownerTerminalPath,ownerTerminal)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,Image);
        internal bool SaveRemoteOwnerTerminal(RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication terminal)
        {
            if(terminal?.IncludesRemoteNativeOriginal(this)!=true||!SourceCurrent())return false;
            long bound=checked(terminal.GroupEvidenceBound+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(Before)+65536);
            using(var workspace=terminal.EnterGroupPublication(bound))
            {
                if(workspace==null)return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                    new XAttribute("world",WorldGuid),new XElement("before",new XElement(Before)),
                    new XElement("remoteNativeOwnerTerminal",new XAttribute("nonce",Nonce.ToString("N")),new XAttribute("original",SourceHash),terminal.CopyGroupEvidence()));
                string path=Path+".owner-terminal";
                if(!RebirthWorkstationCompletionPublication.Write(path,record))return false;
                // Charge retained bytes before the transient publication workspace
                // leaves. The original owns this evidence until its real release.
                if(ownerTerminalAllocation==null&&!Memory.TryReserveAllocation(checked(4*bound+65536L),out ownerTerminalAllocation))return false;
                var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(bytes==null)return false;
                ownerTerminal=bytes;ownerTerminalPath=path;return RemoteOwnerTerminalSaved&&ObserveRemoteCachedOwnerTerminal(this,terminal);
            }
        }
    }
}
