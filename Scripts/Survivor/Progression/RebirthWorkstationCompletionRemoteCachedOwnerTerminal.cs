using System;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal static bool ObserveRemoteCachedOwnerTerminal(NativeHostOriginal original,RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication proof)
    {
        if(original==null||!cachedCustodies.TryGetValue(original.Operation,out var cache)||!cache.Active||
            (bool?)cache.Original.Element("cachedOriginal")?.Attribute("remoteCreator")!=true)return true;
        if(proof?.IncludesRemoteNativeOriginal(original)!=true||!ReferenceEquals(cache.World,original.World)||
            !ReferenceEquals(cache.Station,original.Station)||!cache.SourceCurrent())return false;
        try
        {
            long measured=checked(proof.GroupEvidenceBound+2L*proof.Body.Length+65536);
            if(!cache.ReserveRetirementWorkspace(checked(16*measured)))return false;
            // The cache's measured retained lease covers these additional
            // copies; the caller already owns its group publication workspace.
            {
                var terminal=new XElement("workstationCompletionIntent",new XAttribute("operation",cache.Operation.ToString("N")),new XAttribute("world",cache.WorldGuid),
                    new XElement(cache.Original.Element("before")),new XElement("cachedOwnerTerminal",new XAttribute("original",HistoricalHash(cache.Image)),
                        new XAttribute("nonce",original.Nonce.ToString("N")),proof.CopyGroupEvidence()));
                string terminalPath=cache.Path+".owner-terminal";
                if(!RebirthWorkstationCompletionPublication.Write(terminalPath,terminal)||!proof.Current())return false;
                string terminalImage=RebirthWorkstationCompletionPublication.ReadEvidence(terminalPath);if(terminalImage==null)return false;
                var saved=new XElement("workstationCompletionIntent",new XAttribute("operation",cache.Operation.ToString("N")),new XAttribute("world",cache.WorldGuid),
                    new XElement(cache.Original.Element("before")),new XElement("cachedOwnerAlreadySaved",new XAttribute("ownerProof",terminalPath),
                        new XAttribute("elapsedOffset",proof.Offset),new XElement("proof",terminalImage),new XElement("native",Convert.ToBase64String(proof.Body))));
                string path=cache.Path+".owner-saved";
                if(!RebirthWorkstationCompletionPublication.Write(path,saved)||!proof.Current())return false;
                string image=RebirthWorkstationCompletionPublication.ReadEvidence(path);if(image==null||!proof.TryGetCachedOwnerReadFence(out var ownerReadFence))return false;
                var body=(byte[])proof.Body.Clone();
                cache.OwnerSavedPath=path;cache.OwnerSavedImage=image;cache.Native=body;cache.Offset=proof.Offset;
                cache.OwnerReadFence=ownerReadFence;cache.HasOwnerReadFence=true;if(cache.Load?.Loaded!=null)cache.Load.Loaded.ReadFence=ownerReadFence;
                cache.OwnerTerminal=proof;
                if(cachedPhysicalActions.TryGetValue(cache.Operation,out var action)&&ReferenceEquals(action.Issue?.Cache,cache)&&
                    action.Issue.RecordedIdentity()&&!action.PhysicalAccepted)action.ObserveOwnerTerminalRead(ownerReadFence);
                cache.Saved=false;cache.Watch?.Dispose();cache.Watch=null;
                cache.TrySave();cache.Chunk.isModified=true;cache.Station.setModified();return true;
            }
        }
        catch{return false;}
    }
}
