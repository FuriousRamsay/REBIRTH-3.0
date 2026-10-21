using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private static readonly Dictionary<Guid,SameOwnerStationPublication> sameOwnerNative=new Dictionary<Guid,SameOwnerStationPublication>();
    // This source is deliberately separate from receipt-producing Add paths.
    // Its constructor is reached only inside the actual accepted native event.
    internal sealed class SameOwnerStationPublication:IDisposable
    {
        internal readonly RebirthWorkstationCompletionPipeline.NativeHostOriginal Source;
        internal readonly XElement Physical;
        private RebirthWorkstationCompletionPipeline.NativeViewerEffectAdmission effect;
        private RebirthRemoteResourceNativePublication.Watch watch;
        private IDisposable terminalMemory;
        private byte[] terminal;private string path;
        internal int Phase=>effect.Phase;
        internal byte[] Body=>effect.Body;internal int Offset=>effect.Offset;
        internal string TerminalPath=>path;
        internal long TerminalLength=>terminal?.LongLength??0;
        internal byte[] CopyTerminal()=>TrySeal()?(byte[])terminal.Clone():null;
        private bool TerminalMetadataCurrent()=>terminal!=null&&RebirthProductiveEarnedArtifact.FileMatches(path,terminal)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Source.Path,Source.Image);
        private SameOwnerStationPublication(RebirthWorkstationCompletionPipeline.RemoteNativeEvent actual)
        {Source=actual.Source;effect=actual.SameOwnerEffect;Physical=actual.Contribution.Element("physicalDelivery") is XElement physical?new XElement(physical):null;}
        private bool CurrentBody()=>Source.SourceCurrent()&&effect.CurrentSavedSource()&&
            sameOwnerNative.TryGetValue(Source.Operation,out var current)&&ReferenceEquals(current,this)&&
            RebirthProductiveEarnedNativeBody.MatchesCurrent(Source.Station,Body,Offset);
        private void Begin()
        {
            if(Phase!=3||!CurrentBody()||!RebirthRemoteResourceNativeBinding.TryResolve(Source.World,
                RemoteResourceIdentity.Workstation(Source.Station.ToWorldPos()),RemoteResourceSourceKind.WorkstationOutput,
                Source.CustodyBirth.ToString("N"),out var binding))return;
            watch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,CurrentBody,Source.Memory);
        }
        internal bool TrySeal()
        {
            if(Phase!=3)return false;
            if(terminal!=null)return TerminalMetadataCurrent();
            if(!Source.SourceCurrent())return false;
            if(watch==null||!CurrentBody()||!watch.TryReadPublishedTileDescriptor(out var native,out long offset,out int length,out string digest)||!CurrentBody())return false;
            long measured=effect.MeasureEvidence();
            if(!Source.Memory.TryReserveAllocation(checked(8*measured+65536),out var workspace))return false;
            try
            {
                if(workspace==null)return false;
                var evidence=effect.CopyEvidence();if(evidence==null)return false;
                path=Source.Path+".same-owner-terminal";
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Source.Operation.ToString("N")),
                    new XAttribute("world",Source.WorldGuid),new XElement("before",new XElement("nativeSameOwnerSaved",
                    new XAttribute("version",1),new XAttribute("nonce",Source.Nonce.ToString("N")),new XAttribute("source",Source.SourceHash),
                    new XAttribute("birth",Source.Birth.ToString("N")),new XAttribute("custodyBirth",Source.CustodyBirth.ToString("N")),
                    new XAttribute("native",HydrationHash(Body)),new XAttribute("nativePath",native.Path),new XAttribute("payload",native.PayloadDigest),
                    new XAttribute("offset",offset),new XAttribute("length",length),new XAttribute("tile",digest),evidence)));
                if(!RebirthWorkstationCompletionPublication.Write(path,record))return false;
                terminal=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
                if(terminal==null||!RebirthProductiveEarnedArtifact.FileMatches(path,terminal))return false;
                terminalMemory=workspace;workspace=null;return true;
            }
            finally{workspace?.Dispose();}
        }
        public void Dispose(){watch?.Dispose();watch=null;effect?.Dispose();effect=null;terminal=null;terminalMemory?.Dispose();terminalMemory=null;}
        internal static bool Observe(RebirthWorkstationCompletionPipeline.RemoteNativeEvent actual)
        {
            if(actual?.SameOwnerEffect?.MatchesAccepted(actual)!=true)return false;
            var source=actual.Source;var candidate=new SameOwnerStationPublication(actual);
            sameOwnerNative.TryGetValue(source.Operation,out var previous);
            sameOwnerNative[source.Operation]=candidate;previous?.Dispose();candidate.Begin();return true;
        }
    }
    internal static bool TryAdmitRemoteNativeEvent(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,
        XElement contribution,byte[] proposed,int elapsedOffset,out HostContribution entry,
        out RebirthWorkstationCompletionPipeline.NativeViewerEffectAdmission effect)
    {
        entry=null;effect=null;
        if(contribution?.Name!="sameOwnerEffect")return TryAdmitRemoteNativeEvent(source,contribution,proposed,elapsedOffset,out entry);
        if(source?.CurrentViewer()!=true||!source.BeforeSaved||!source.AdmissionCurrent())return false;
        int phase=(int?)contribution.Attribute("phase")??0;
        sameOwnerNative.TryGetValue(source.Operation,out var previous);
        if(previous!=null&&!ReferenceEquals(previous.Source,source))return false;
        if(phase==2||phase==4)
        {if(source.HasRemoteOutputFinalQueuePrefix||previous!=null&&previous.Phase!=4)return false;}
        else if(phase!=3||previous==null&&!source.HasRemoteOutputFinalQueuePrefix||previous!=null&&(previous.Phase!=2||
            !XNode.DeepEquals(previous.Physical,contribution.Element("physicalDelivery"))))return false;
        byte[] prior=previous?.Body??source.RemoteOutputFinalQueuePrior??source.NativeBefore;
        int offset=previous?.Offset??(source.HasRemoteOutputFinalQueuePrefix?source.RemoteOutputFinalQueuePriorOffset:source.BeforeOffset);
        return NativeViewerPds.TryValidateSameOwner(source,contribution,prior,offset,proposed,elapsedOffset,phase,out effect);
    }
    private static bool TrySealSameOwnerNativeStation(RebirthWorkstationCompletionPipeline.NativeHostOriginal source)=>
        source!=null&&(sameOwnerNative.TryGetValue(source.Operation,out var publication)&&ReferenceEquals(publication.Source,source)&&
        publication.TrySeal()&&source.TryReleaseStationWorkHold(publication)||
        TryGetSavedCachedSameOwnerTerminal(source,out var cached)&&source.TryReleaseStationWorkHold(cached));
    internal static bool TryGetSavedSameOwnerTerminal(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,out SameOwnerStationPublication publication)
    {
        publication=null;
        if(!ThreadManager.IsMainThread()||source==null||!sameOwnerNative.TryGetValue(source.Operation,out var retained)||
            !ReferenceEquals(retained.Source,source)||!retained.TrySeal())return false;
        publication=retained;return true;
    }
    internal static bool RetireSavedSameOwnerResources(RebirthWorkstationCompletionPipeline.SameOwnerReceived proof)
    {
        if(proof?.Current()!=true||!TryGetSavedSameOwnerTerminal(proof.Source,out var publication))return false;
        sameOwnerNative.Remove(proof.Source.Operation);publication.Dispose();proof.Source.Dispose();return true;
    }
    internal static void RetireSameOwnerObservationWorld(World current)
    {
        if(!ThreadManager.IsMainThread())return;
        foreach(var pair in sameOwnerNative.Where(value=>!ReferenceEquals(value.Value.Source.World,current)).ToArray())
        {sameOwnerNative.Remove(pair.Key);pair.Value.Dispose();}
    }
}

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed partial class NativeHostOriginal
    {
        internal bool TryReleaseStationWorkHold(RebirthProductiveEarnedUiDeferredTransport.SameOwnerStationPublication proof)
        {
            if(proof==null||!ReferenceEquals(proof.Source,this)||proof.Phase!=3||!proof.TrySeal())return false;
            stationWorkReleased=true;return true;
        }
    }
}
