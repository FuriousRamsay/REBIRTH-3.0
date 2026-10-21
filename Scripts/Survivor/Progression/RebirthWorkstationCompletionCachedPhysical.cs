using System;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private static void ObserveCachedPhysicalAccepted(NativeSync native)
    {
        var frame=native.Frame;
        if(frame==null||frame.Observation.World.IsRemote()||frame.ConfirmedOriginal==null||!frame.NativeAccepted||
            !cachedCustodies.TryGetValue(frame.Observation.Operation,out var source)||!ReferenceEquals(source.Frame,frame)||!source.SourceCurrent())return;
        try
        {
            byte[] body;int offset;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(source.Station,out body,out offset))return;
                long size=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(frame.Intent)+2L*body.Length+65536;
                if(!source.Grow(checked(64L*1024*1024+8*size)))return;
            }
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldGuid),
                new XElement(source.Original.Element("before")),new XElement("actualCachedPhysical",new XAttribute("original",HistoricalHash(source.Image)),
                    new XAttribute("elapsedOffset",offset),new XElement("confirmedOriginal",frame.ConfirmedOriginal.Write()),
                    new XElement(frame.Intent.Element("tileAccepted")),new XElement("native",Convert.ToBase64String(body))));
            string path=source.Path+".physical-attempt";
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return;
            source.PhysicalObserved=true;source.Native=body;source.Offset=offset;source.Saved=false;
            source.Watch?.Dispose();source.Watch=null;
        }
        catch{}
    }
}
