using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private sealed class CachedPeerRead
    {
        internal CachedCustody Root;internal byte[] Head;internal int Epoch;
    }
    private static CachedPeerRead[] CaptureCachedPeerReads(CachedPhysicalHostAction action,byte[] before,int offset)
    {
        return cachedCustodies.Values.Where(value=>!ReferenceEquals(value,action.Issue.Cache)&&value.Active&&!value.Physical&&!value.PhysicalObserved&&
            ReferenceEquals(value.World,action.World)&&ReferenceEquals(value.Station,action.Station)&&value.SourceCurrent()&&
            CurrentCachedCreatorFence(value,action.World)&&value.Native!=null&&
            RebirthProductiveEarnedNativeBody.SameExceptElapsed(value.Native,value.Offset,before,offset))
            .Select(value=>new CachedPeerRead{Root=value,Head=value.Native,Epoch=value.Epoch}).ToArray();
    }
    private static void ObserveCachedPeerReads(CachedPhysicalRead read)
    {
        if(!ReferenceEquals(executingCachedPhysical,read)||!read.ReadCompleted||read.Reads!=1||read.Peers==null)return;
        foreach(var prior in read.Peers)
        {
            var root=prior.Root;
            if(!root.Active||root.Physical||root.PhysicalObserved||root.Epoch!=prior.Epoch||!ReferenceEquals(root.Native,prior.Head)||root.Epoch>=64||!root.SourceCurrent())continue;
            long bound=checked(65536+2L*read.Body.Length+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(root.Original));
            if(!root.ReserveRetirementWorkspace(checked(16*bound)))continue;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",root.Operation.ToString("N")),new XAttribute("world",root.WorldGuid),
                new XElement(root.Original.Element("before")),new XElement("actualCachedPeerNative",new XAttribute("version",1),
                    new XAttribute("original",HistoricalHash(root.Image)),new XAttribute("source",read.Action.Operation.ToString("N")),
                    new XAttribute("dispatch",read.Dispatch.ToString("N")),new XAttribute("phase",read.Phase),new XAttribute("readFence",read.ReadFence),
                    new XAttribute("before",HistoricalHash(prior.Head)),new XAttribute("epoch",root.Epoch+1),new XAttribute("offset",read.Offset),
                    new XElement("native",Convert.ToBase64String(read.Body))));
            string path=root.Path+".peer-native."+(root.Epoch+1);
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))continue;
            string evidence=RebirthWorkstationCompletionPublication.ReadEvidence(path);if(evidence==null)continue;
            root.Watch?.Dispose();root.Watch=null;root.Native=(byte[])read.Body.Clone();root.Offset=read.Offset;root.Saved=false;root.Epoch++;
            root.HasOwnerReadFence=true;root.OwnerReadFence=read.ReadFence;root.SpacePath=path;root.SpaceImage=evidence;
            if(cachedPhysicalActions.TryGetValue(root.Operation,out var action))action.ObservePeerNativeRead(read.ReadFence);
            root.TrySave();root.Chunk.isModified=true;
        }
    }
    private static bool HasUnpreparedCachedPeers(CachedPhysicalHostAction action)=>cachedCustodies.Values.Any(value=>value.Active&&!value.Physical&&
        !ReferenceEquals(value,action.Issue.Cache)&&ReferenceEquals(value.World,action.World)&&ReferenceEquals(value.Station,action.Station)&&
        value.CreatorIssue!=null&&value.CreatorIssue.Group==action.Issue.Group&&ReferenceEquals(value.CreatorIssue.Sender,action.Sender)&&
        ReferenceEquals(value.CreatorIssue.Connection,action.Issue.Connection)&&!cachedPhysicalActions.ContainsKey(value.Operation));
    internal sealed partial class CachedPhysicalHostAction
    {
        internal void ObservePeerNativeRead(long fence){ReadFence=fence;BeforeGeneration=null;readyImage=null;OwnerRenewed=true;}
    }
}
