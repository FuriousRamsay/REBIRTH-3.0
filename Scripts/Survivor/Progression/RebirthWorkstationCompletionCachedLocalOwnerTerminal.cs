using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed partial class CachedHostOriginal
    {
        private RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication localOwnerTerminal;
        private string localOwnerTerminalPath,localOwnerTerminalImage;
        internal NativeAddOriginal LocalCachedNativeOriginal=>source?.Frame?.NativeOriginal;
        internal bool HasSavedLocalOwnerTerminal(RebirthProductiveEarnedUiOwnerFile.LocalOwnerScope scope)=>
            scope!=null&&ReferenceEquals(localOwnerTerminal?.Scope,scope)&&OwnerTerminalSaved&&
            CachedCustody.EvidenceCurrent(localOwnerTerminalPath,localOwnerTerminalImage)&&source?.OwnerSavedImage!=null&&
            CachedCustody.EvidenceCurrent(source.OwnerSavedPath,source.OwnerSavedImage)&&scope.Members.Any(member=>
                member.HostLocalOriginal?.Source.IsLoadedCache==true&&ReferenceEquals(member.HostLocalOriginal.Source.Original.CachedOriginal,this));
        // The local Check, PDS and native tile publication are issued by the
        // existing local owner scope. Historical cached metadata cannot issue it.
        internal bool TrySaveLocalOwnerTerminal(RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication proof)
        {
            if(proof?.IncludesLoadedCachedOriginal(this)!=true||!SourceCurrentWithoutRows()||
                !ReferenceEquals(proof.World,World)||!ReferenceEquals(proof.Station,Station)||
                !RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(World,Station,out long fence))return false;
            if(ReferenceEquals(localOwnerTerminal,proof))return
                CachedCustody.EvidenceCurrent(localOwnerTerminalPath,localOwnerTerminalImage)&&
                source.OwnerSavedImage!=null&&CachedCustody.EvidenceCurrent(source.OwnerSavedPath,source.OwnerSavedImage);
            if(localOwnerTerminal!=null&&(!ReferenceEquals(localOwnerTerminal.Scope,proof.Scope)||proof.Scope.CachedOperation!=Operation))return false;
            long bound=checked(8L*proof.Body.Length+MeasureMembership()+65536);
            if(!source.ReserveRetirementWorkspace(checked(8*bound)))return false;
            using(var workspace=proof.Scope.EnterPublication(bound))
            {
                if(workspace==null)return false;
                var terminal=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",source.WorldGuid),
                    new XElement(source.Original.Element("before")),new XElement("cachedLocalOwnerTerminal",
                        new XAttribute("original",SourceHash),new XAttribute("nonce",Nonce.ToString("N")),
                        new XAttribute("group",proof.Operation.ToString("N")),new XAttribute("ownerTerminal",proof.Path),
                        new XAttribute("owner",proof.Scope.Owner),new XAttribute("creation",proof.Scope.Creation),
                        new XAttribute("deaths",proof.Scope.Deaths),new XAttribute("nativeAfter",CachedFileHash(proof.Body))));
                string terminalPath=source.Path+".local-owner-terminal"+(proof.Scope.CachedOperation==Guid.Empty?"":".physical");
                if(!RebirthWorkstationCompletionPublication.Write(terminalPath,terminal)||!proof.Current())return false;
                string terminalImage=RebirthWorkstationCompletionPublication.ReadEvidence(terminalPath);if(terminalImage==null)return false;
                var saved=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",source.WorldGuid),
                    new XElement(source.Original.Element("before")),new XElement("cachedOwnerAlreadySaved",new XAttribute("ownerProof",terminalPath),
                        new XAttribute("elapsedOffset",proof.Offset),new XElement("proof",terminalImage),new XElement("native",Convert.ToBase64String(proof.Body))));
                string savedPath=source.Path+".owner-saved";
                if(!RebirthWorkstationCompletionPublication.Write(savedPath,saved)||!proof.Current()||
                    !RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(World,Station,out long afterFence)||afterFence!=fence)return false;
                string savedImage=RebirthWorkstationCompletionPublication.ReadEvidence(savedPath);if(savedImage==null)return false;
                source.OwnerSavedPath=savedPath;source.OwnerSavedImage=savedImage;
                source.Native=(byte[])proof.Body.Clone();source.Offset=proof.Offset;source.OwnerReadFence=fence;source.HasOwnerReadFence=true;
                if(loaded!=null)loaded.ReadFence=fence;
                localOwnerTerminal=proof;localOwnerTerminalPath=terminalPath;localOwnerTerminalImage=terminalImage;OwnerTerminalSaved=true;
                // The physical cache is still an independent original. The
                // actual physical callback/save retires it through its existing
                // loaded path; local progression publication does not do so.
                source.Saved=false;source.Watch?.Dispose();source.Watch=null;
                source.TrySave();source.Chunk.isModified=true;source.Station.setModified();return true;
            }
        }
    }
}
