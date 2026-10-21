using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed partial class CachedIdentitySuccessor:IDisposable
    {
        internal readonly World World;internal readonly TileEntityWorkstation Station;internal readonly Guid CustodyBirth;
        internal readonly string Path,Save,WorldGuid;internal byte[] Image;private IDisposable memory;private readonly long fence;
        private CachedIdentitySuccessor(RebirthProductiveEarnedUiCoordinator.SavedLocalQueueSuccessor proof,string path,byte[] image,IDisposable allocation,long version)
            :this(proof.World,proof.Station,proof.CustodyBirth,path,image,allocation,version){}
        private CachedIdentitySuccessor(World world,TileEntityWorkstation station,Guid birth,string path,byte[] image,IDisposable allocation,long version)
        {World=world;Station=station;CustodyBirth=birth;Path=path;Image=image;memory=allocation;fence=version;
         Save=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir());WorldGuid=RebirthWorkstationCompletionOutbox.CurrentGuid(World);}
        internal bool SourceCurrent()=>memory!=null&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&
            ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())==Save&&
            RebirthWorkstationCompletionOutbox.CurrentGuid(World)==WorldGuid&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Station,out var birth)&&birth==CustodyBirth.ToString("N")&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,Image);
        internal bool RowCurrent()=>SourceCurrent()&&RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(World,Station,out var current)&&current==fence;
        public void Dispose(){Image=null;memory?.Dispose();memory=null;}
        internal static CachedIdentitySuccessor FromSavedSuccessor(RebirthProductiveEarnedUiCoordinator.SavedLocalQueueSuccessor proof,
            RebirthProductiveEarnedUiQueueOwner.Witness owner,string path)
        {
            if(proof?.Current()!=true||!ReferenceEquals(proof.EarlierOwner,owner)||proof.CustodyBirth==Guid.Empty||
                !RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(proof.World,proof.Station,out var version)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(256*1024,out var allocation))return null;
            try
            {
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",proof.SuccessorOperation.ToString("N")),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(proof.World)),new XElement("cachedIdentitySuccessor",
                        new XAttribute("historicalBirth",owner.Birth.ToString("N")),new XAttribute("custodyBirth",proof.CustodyBirth.ToString("N")),
                        new XAttribute("ownerPurpose",RebirthProductiveEarnedUiQueueOwner.Export(owner)),new XAttribute("previousGroup",proof.Group.ToString("N")),
                        new XAttribute("handoffPath",proof.Path),new XElement("handoff",Convert.ToBase64String(proof.CopyImage()))));
                if(RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(record)>24*1024||
                    !RebirthWorkstationCompletionPublication.Write(path,record)||!proof.Current())return null;
                var image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(image==null||image.Length>24*1024)return null;
                var result=new CachedIdentitySuccessor(proof,path,image,allocation,version);allocation=null;return result;
            }
            finally{allocation?.Dispose();}
        }
    }
    internal static bool RetireSucceededCachedPrimaries(RebirthProductiveEarnedUiCoordinator.SavedLocalQueueSuccessor proof)
    {
        if(proof?.Current()!=true)return false;
        var values=cachedCustodies.Values.Where(value=>ReferenceEquals(value.World,proof.World)&&ReferenceEquals(value.Station,proof.Station)).ToArray();
        if(values.Length==0)return true;
        var replacements=new Dictionary<RebirthProductiveEarnedUiQueueOwner.Witness,CachedIdentitySuccessor>();
        try
        {
            foreach(var value in values)
            {
                var owner=value.Frame?.EarlierOwner;
                if(!value.Physical||!value.Saved||owner==null||!ReferenceEquals(owner,proof.EarlierOwner)||
                    !(proof.PreviousScope.Members.Any(member=>ReferenceEquals(member.NativeOriginal,value.Frame.NativeOriginal)&&member.OwnerTerminalSaved)||
                        value.HostOriginal?.HasSavedLocalOwnerTerminal(proof.PreviousScope)==true))return false;
                if(!TryPublishCachedSettledNotice(value))return false;
                if(!replacements.ContainsKey(owner))
                {
                    var compact=CachedIdentitySuccessor.FromSavedSuccessor(proof,owner,value.Path+".identity."+proof.SuccessorOperation.ToString("N"));
                    if(compact==null)return false;replacements.Add(owner,compact);
                }
                long physicalLength=RebirthWorkstationCompletionPublication.PublishedLength(value.Path+".physical");
                if(physicalLength<1||!value.ReserveRetirementWorkspace(checked(8*(physicalLength+proof.MeasureImage()+65536))))return false;
                var physical=RebirthWorkstationCompletionPublication.ReadPrimary(value.Path+".physical");if(physical==null)return false;
                var compactSource=replacements[owner];
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",value.Operation.ToString("N")),new XAttribute("world",value.WorldGuid),
                    new XElement(value.Original.Element("before")),new XElement("cachedIdentityRetired",new XAttribute("original",HistoricalHash(value.Image)),
                        new XAttribute("successor",compactSource.Path),new XElement("identity",Convert.ToBase64String(compactSource.Image)),new XElement("physical",physical)));
                if(!RebirthWorkstationCompletionPublication.Write(value.Path+".identity-retired",record)||
                    !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(value.Path+".identity-retired"))||!proof.Current())return false;
            }
            foreach(var pair in replacements)
            {
                var owner=pair.Key;var old=owner.Retention;
                owner.CachedIdentity=pair.Value;owner.CachedSource=null;owner.Path=pair.Value.Path;owner.Image=pair.Value.Image;owner.Retention=pair.Value;old?.Dispose();
            }
            replacements.Clear();
            foreach(var value in values){cachedCustodies.Remove(value.Operation);value.Frame?.CachedReentry?.Dispose();value.Dispose();}
            return true;
        }
        finally{foreach(var compact in replacements.Values)compact.Dispose();}
    }
    private static bool IsSucceededCacheRetired(CachedCustody value)
    {
        try
        {
            long retiredLength=RebirthWorkstationCompletionPublication.PublishedLength(value.Path+".identity-retired"),physicalLength=RebirthWorkstationCompletionPublication.PublishedLength(value.Path+".physical");
            if(retiredLength<1||physicalLength<1||!value.ReserveRetirementWorkspace(checked(16*(retiredLength+physicalLength)+65536)))return false;
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(value.Path+".identity-retired");
            if(record==null||!XNode.DeepEquals(record.Element("before"),value.Original.Element("before"))||
                (string)record.Attribute("operation")!=value.Operation.ToString("N")||(string)record.Attribute("world")!=value.WorldGuid)return false;
            var retired=record.Element("cachedIdentityRetired");
            return retired!=null&&(string)retired.Attribute("original")==HistoricalHash(value.Image)&&
                XNode.DeepEquals(retired.Element("physical")?.Elements().SingleOrDefault(),RebirthWorkstationCompletionPublication.ReadPrimary(value.Path+".physical"))&&
                RebirthWorkstationCompletionPublication.MatchesPublishedBytes((string)retired.Attribute("successor"),Convert.FromBase64String((string)retired.Element("identity")));
        }
        catch{return false;}
    }
}
