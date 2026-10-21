using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal static void ObserveCachedOwnerClose(RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication terminal,byte[] body,int offset,string path,byte[] accepted)
    {
        if(!RebirthProductiveEarnedUiDeferredTransport.IsAcceptingCachedOwnerClose(terminal)||terminal==null||body==null||accepted==null)return;
        foreach(var root in cachedCustodies.Values.Where(value=>value.Active&&!value.Physical&&ReferenceEquals(value.OwnerTerminal,terminal)).ToArray())
        {
            try
            {
                if(root.Epoch>=64||!root.SourceCurrent()||!terminal.HasSavedCachedIdentity(root)||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(root.Native,root.Offset,body,offset)||
                    !RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(root.World,root.Station,out var fence)||
                    !root.ReserveRetirementWorkspace(checked(16L*(body.Length+accepted.Length+65536))))continue;
                string receiptPath=root.Path+".owner-close."+(root.Epoch+1);
                var receipt=new XElement("workstationCompletionIntent",new XAttribute("operation",root.Operation.ToString("N")),new XAttribute("world",root.WorldGuid),
                    new XElement(root.Original.Element("before")),new XElement("actualCachedOwnerClose",new XAttribute("original",HistoricalHash(root.Image)),
                        new XAttribute("group",terminal.RecipientGroup.ToString("N")),new XAttribute("path",path),new XAttribute("accepted",HistoricalHash(accepted)),
                        new XAttribute("readFence",fence),new XAttribute("epoch",root.Epoch+1),new XAttribute("attempted",root.RestoreAttempted)));
                if(!RebirthWorkstationCompletionPublication.Write(receiptPath,receipt)||!XNode.DeepEquals(receipt,RebirthWorkstationCompletionPublication.ReadPrimary(receiptPath)))continue;
                if(root.RestoreAttempted)
                {
                    var guard=new XElement("workstationCompletionIntent",new XAttribute("operation",root.Operation.ToString("N")),new XAttribute("world",root.WorldGuid),
                        new XElement(root.Original.Element("before")),new XElement("cachedOwnerCloseUnknownAttempt",new XAttribute("original",HistoricalHash(root.Image)),
                            new XAttribute("epoch",root.Epoch+1),new XAttribute("close",receiptPath)));
                    if(!RebirthWorkstationCompletionPublication.Write(root.Path+".remote-action."+(root.Epoch+1),guard))continue;
                }
                var image=RebirthWorkstationCompletionPublication.ReadEvidence(receiptPath);if(image==null)continue;
                root.Native=(byte[])body.Clone();root.Offset=offset;root.Epoch++;root.OwnerReadFence=fence;root.HasOwnerReadFence=true;
                if(root.Load?.Loaded!=null)root.Load.Loaded.ReadFence=fence;
                root.SpacePath=receiptPath;root.SpaceImage=image;root.Saved=false;root.Watch?.Dispose();root.Watch=null;
                root.TrySave();root.Chunk.isModified=true;root.Station.setModified();
            }
            catch{/* Unknown close/read evidence never renews a cached action. */}
        }
    }
}
