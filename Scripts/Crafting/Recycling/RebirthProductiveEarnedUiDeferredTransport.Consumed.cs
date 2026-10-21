using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    // The issuer is the actual final-run native Check/GiveExp observer. The
    // live frame getter and a missing receipt cannot mint this event.
    internal static void ObserveImportedConsumption(RebirthProductiveEarnedUiOwnerFile.DeferredConsumptionEvent observed)
    {
        if(observed?.Current()!=true)return;
        var members=observed.Members;var original=members.FirstOrDefault()?.ImportedOriginal?.Original;
        if(original==null)return;
        try
        {
            if(!original.InvokingCheck||!original.BeforeReady||!original.Attempted||original.Consumed||original.Unknown||
                !original.CurrentSource()||members.Length!=original.Members.Count||
                members.Any(value=>!ReferenceEquals(value.ImportedOriginal?.Original,original))||
                original.Members.Any(value=>!members.Any(member=>ReferenceEquals(member,value.Witness))))throw new InvalidDataException();
            var recipes=members.Select(value=>value.Receipt.Receipt.RecipeName).Distinct(StringComparer.Ordinal).ToArray();
            if(!RebirthProductiveEarnedUiOwnerFile.TryMeasureDeferredLiveFrame(original.Actor,recipes,out int measured)||
                !original.ReserveOwnerFrame(measured,true))throw new InvalidDataException();
            var frame=RebirthProductiveEarnedUiOwnerFile.CaptureDeferredConsumedFrame(members,true);
            var before=RebirthProductiveEarnedUiOwnerFile.CaptureDeferredConsumedFrame(members,false);
            if(!XNode.DeepEquals(before,original.BeforeFrame)||!observed.Current())throw new InvalidDataException();
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedNativeBody.TryRead(original.NativeBefore,original.NativeBeforeOffset,out var native,out ulong elapsed))throw new InvalidDataException();
                var selected=native.Completions.Where(row=>original.Members.Any(member=>HydrationHash(Row(row))==HydrationHash(Row(member.Receipt)))).ToArray();
                if(selected.Length!=original.Members.Select(value=>value.Receipt).Distinct().Count())throw new InvalidDataException();
                native.Completions=native.Completions.Where(row=>!selected.Any(value=>ReferenceEquals(value,row))).ToArray();
                var expected=RebirthProductiveEarnedNativeBody.Encode(native,elapsed,out int offset);
                if(!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(original.Station,out var actual,out int actualOffset)||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(actual,actualOffset,expected,offset))throw new InvalidDataException();
                original.NativeAfter=actual;original.NativeAfterOffset=actualOffset;
            }
            long reportBound=checked((long)measured+65536+4*((original.NativeBefore.Length+2L)/3)+4*((original.NativeAfter.Length+2L)/3));
            if(!original.ReserveOwnerFrame(reportBound,true))throw new InvalidDataException();
            var report=new XElement("deferredNativeConsumption",new XAttribute("version",1),new XAttribute("group",original.Group.ToString("N")),
                new XAttribute("world",original.WorldKey),new XAttribute("owner",original.Owner),new XAttribute("creation",original.Creation),
                new XAttribute("entity",original.Actor.entityId),new XAttribute("deaths",original.Deaths),new XAttribute("window",original.WindowVersion),
                new XAttribute("beforeDigest",original.BeforeDigest),new XAttribute("nativeCheck",true),
                new XAttribute("nativeGiveExpCount",members.Select(value=>value.Receipt.Receipt).Distinct().Count()),
                new XElement("membership",original.Header.Elements("aggregate").Select(value=>new XElement(value))),
                new XElement("nativeBefore",new XAttribute("offset",original.NativeBeforeOffset),Convert.ToBase64String(original.NativeBefore)),
                new XElement("nativeAfter",new XAttribute("offset",original.NativeAfterOffset),Convert.ToBase64String(original.NativeAfter)),
                new XElement("ownerAfter",new XElement(frame)));
            if(!WriteWithPublicationWorkspace(original,original.Path+".consumed.xml",reportBound+65536,()=>
                new XElement("workstationCompletionIntent",new XAttribute("operation",original.Group.ToString("N")),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(original.World)),new XElement("before",new XElement(report)))))throw new InvalidDataException();
            original.AfterFrame=frame;original.ConsumedReport=report;original.Consumed=true;
        }
        catch{original.Unknown=true;/* Actual native effects are retained; never rearmed. */}
    }
}
