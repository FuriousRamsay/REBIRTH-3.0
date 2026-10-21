using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed partial class CachedPhysicalSource
    {
        internal readonly RetainedCachedClientBinding retainedPrefix;
        private XElement retainedPrepare;
        private CachedPhysicalSource(RetainedCachedClientBinding binding,IDisposable memory,IDisposable lease)
        {
            Grant=binding.Grant;Operation=Grant.Operation;Action=binding.Action;Path=binding.ActionPath;
            retainedPrefix=binding;retainedPrepare=binding.CopyPrepare();allocation=memory;retention=lease;
            Before=Grant.CopyBefore();Full=Grant.CopyFull();NativeVersion=Grant.NativeVersion;
        }
        internal static CachedPhysicalSource FromRetainedPrefix(RetainedCachedClientBinding binding,RebirthRecipeOutputIntegration.RemoteOutputColdClient owner)
        {
            IDisposable memory=null,lease=null;CachedPhysicalSource result=null;
            try
            {
                if(binding?.HostPrefixRestored!=true||owner?.BeforeBindingCurrent()!=true||owner.FinalCallbackSaved&&(!binding.HostFinalOwnerRestored||!owner.QueueOnlyAuthorized)||
                    !ReferenceEquals(owner.RetainedCached,binding)||!binding.Grant.CurrentWindow()||!binding.Grant.NativeCurrent())return null;
                string generation=(string)owner.Request.Attribute("generation"),path=binding.ActionPath;
                long prepareLength=RebirthWorkstationCompletionPublication.PublishedLength(path+".prepared"),readyLength=RebirthWorkstationCompletionPublication.PublishedLength(path+".ready."+generation);
                if(prepareLength<1||readyLength<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(
                    checked(16*(binding.Grant.MeasureImage()+prepareLength+readyLength)+65536),out memory)||(lease=binding.Grant.Retain())==null)return null;
                result=new CachedPhysicalSource(binding,memory,lease);memory=lease=null;
                result.requestImage=binding.CopyActionImage();
                var prepared=RebirthWorkstationCompletionPublication.ReadPrimary(path+".prepared");
                var ready=RebirthWorkstationCompletionPublication.ReadPrimary(path+".ready."+generation);
                var first=prepared?.Element("cachedPhysicalPrepared")?.Element("ack");var last=ready?.Element("cachedPhysicalReady")?.Element("ack");
                foreach(var record in new[]{prepared,ready})
                    if(record?.Name!="workstationCompletionIntent"||(string)record.Attribute("operation")!=result.Operation.ToString("N")||
                        (string)record.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(result.World)||
                        !XNode.DeepEquals(record.Element("before"),new XElement("before",new XElement(result.Before))))return null;
                foreach(var ack in new[]{first,last})
                    if((string)ack?.Attribute("ticket")!=result.Grant.Ticket.ToString("N")||(string)ack.Attribute("action")!=result.Action.ToString("N")||
                        (string)ack.Attribute("digest")!=result.Grant.Original)return null;
                if((string)first.Attribute("stage")!="cachedPhysicalPrepared"||(string)last.Attribute("stage")!="cachedPhysicalReady"||
                    (string)last.Attribute("generation")!=generation)return null;
                result.preparedImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path+".prepared");
                result.readyPath=path+".ready."+generation;result.readyImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(result.readyPath);
                if(!result.Prepared||!result.Ready||!result.Current())return null;
                var owned=result;result=null;return owned;
            }
            catch{return null;}
            finally{result?.Dispose();lease?.Dispose();memory?.Dispose();}
        }
        internal bool RestoreRetainedCardMetadata(RebirthRecipeOutputIntegration.RemoteOutputColdClient owner)
        {
            if(retainedPrefix?.HostPrefixRestored!=true||owner?.BindingCurrent()!=true||!ReferenceEquals(owner.RetainedCached,retainedPrefix)||
                !Current()||Frame!=null||!Grant.CurrentWindow()||!Grant.NativeCurrent()||!owner.TryPrimary(out var primary))return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||Grid(new[]{new ItemStack(primary,Card.GetRecipe().count)})!=(string)Full.Element("primaryNative"))return false;
                float time=(float)Full.Attribute("cachedTime"),total=(float)Full.Attribute("cachedTotalTime");
                if(float.IsNaN(time)||float.IsInfinity(time)||time>0||float.IsNaN(total)||float.IsInfinity(total))return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(World)),new XElement("before",new XElement(Before)),
                    new XElement("retainedCachedCardMetadata",new XAttribute("action",Action.ToString("N")),new XAttribute("ticket",Grant.Ticket.ToString("N")),
                        new XAttribute("localOperation",owner.LocalOperation.ToString("N")),new XAttribute("original",owner.OriginalHash),new XAttribute("prefix",owner.Prefix)));
                string path=owner.FramePath+".retained-card";
                if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||
                    !Current()||!Grant.CurrentWindow()||!Grant.NativeCurrent())return false;
                // Only the already-accepted primary's UI representation is
                // restored. The saved output prefix skips its native setter.
                Card.outputItemValue=primary;Card.isInventoryFull=true;Card.craftingTimeLeft=time;Card.totalCraftTimeLeft=total;
                Card.isCrafting=(bool)Full.Attribute("cachedCrafting");installed=true;return true;
            }
        }
        internal bool BeginRetainedPrefix(out Frame frame)
        {
            frame=Frame;
            if(retainedPrefix?.HostPrefixRestored!=true||!Current()||!Ready||frame?.Observation.RemoteOutputRestart?.BindingCurrent()!=true||
                frame.Observation.RanUpdate||!frame.Attempted||!nativeViewerClients.TryGetValue(frame,out var viewer)||
                (frame.Observation.RemoteOutputRestart.FinalCallbackSaved?viewer.After?.Saved!=true||!retainedPrefix.HostFinalOwnerRestored:
                    viewer.Prefix?.Stage.Saved!=true)||
                !CanResumeAdditionalOutputPrefix(Card,frame.Observation.RemoteOutputRestart.FinalCallbackSaved?frame.Observation.RemoteOutputRestart.Prefix:viewer.Prefix.Prefix.Count)||
                !RebirthRecipeOutputIntegration.ResumeOwningObservation(frame.Observation))return false;
            current=frame;return true;
        }
    }
}
