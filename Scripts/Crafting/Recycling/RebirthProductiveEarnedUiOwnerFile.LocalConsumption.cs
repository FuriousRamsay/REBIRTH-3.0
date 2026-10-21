using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiOwnerFile
{
    internal static bool IsInvokingLocalHostContribution(RebirthProductiveEarnedUiDeferredTransport.HostContribution contribution,
        TileEntityWorkstation station,EntityPlayerLocal actor)
        =>TryGetInvokingLocalOwner(station,actor,out var scope)&&scope.Members.Any(value=>
            ReferenceEquals(value.HostLocalOriginal?.Source.Original,contribution)&&value.Receipt?.CurrentAggregateMembership()==true);
    private static bool TryGetInvokingLocalOwner(TileEntityWorkstation station,EntityPlayerLocal actor,out LocalOwnerScope scope)
    {
        scope=localOwners.Values.SingleOrDefault(value=>!value.Closed&&value.Invoking&&ReferenceEquals(value.Station,station)&&ReferenceEquals(value.Actor,actor));
        return scope!=null&&scope.BeforePublished&&scope.BeforeSaved&&scope.Attempted&&scope.CurrentWindow();
    }
    // Called synchronously within the existing positive Check/GiveExp callback.
    // Neither a current getter nor a missing receipt can enter this method.
    private static void ObserveLocalOwnerConsumption(Consumption accepted)
    {
        if(!ReferenceEquals(consuming,accepted)||accepted.Unknown||!TryGetInvokingLocalOwner(accepted.Witness.Station,accepted.Actor,out var scope))return;
        try
        {
            if(scope.Consumed||accepted.Members.Length!=scope.Members.Length||scope.Members.Any(value=>!accepted.Members.Contains(value))||
                !XNode.DeepEquals(CaptureDeferredConsumedFrame(accepted.Members,false),scope.BeforeFrame))throw new InvalidDataException();
            var recipes=accepted.Members.Select(value=>value.Receipt.Receipt.RecipeName).Distinct(StringComparer.Ordinal).ToArray();
            if(!TryMeasureDeferredLiveFrame(scope.Actor,recipes,out int measured)||!scope.ReserveFrame(measured,true))throw new InvalidDataException();
            var after=CaptureDeferredConsumedFrame(accepted.Members,true);
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedNativeBody.TryRead(scope.NativeBefore,scope.BeforeOffset,out var native,out ulong elapsed))throw new InvalidDataException();
                var images=accepted.Members.Select(value=>ReceiptImage(value.Receipt.Receipt)).ToArray();
                var selected=native.Completions.Where(row=>images.Any(image=>image.SequenceEqual(ReceiptImage(row)))).ToArray();
                if(selected.Length!=accepted.Members.Select(value=>value.Receipt.Receipt).Distinct().Count())throw new InvalidDataException();
                native.Completions=native.Completions.Where(row=>!selected.Contains(row)).ToArray();
                var expected=RebirthProductiveEarnedNativeBody.Encode(native,elapsed,out int offset);
                if(!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(scope.Station,out var actual,out int actualOffset)||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(actual,actualOffset,expected,offset))throw new InvalidDataException();
                scope.NativeAfter=actual;scope.AfterOffset=actualOffset;
            }
            long bound=checked(measured+65536L+4*((scope.NativeBefore.Length+2L)/3)+4*((scope.NativeAfter.Length+2L)/3));
            using(var workspace=scope.EnterPublication(bound))
            {
                if(workspace==null)throw new InvalidDataException();
                var report=new XElement("localNativeOwnerConsumed",new XAttribute("nativeCheck",true),
                    new XAttribute("nativeGiveExpCount",accepted.Awarded.Count),new XAttribute("window",scope.WindowSession),
                    new XElement("members",accepted.Members.Select(value=>new XElement("member",new XAttribute("operation",value.Operation.ToString("N"))))),
                    new XElement("nativeBefore",new XAttribute("offset",scope.BeforeOffset),Convert.ToBase64String(scope.NativeBefore)),
                    new XElement("nativeAfter",new XAttribute("offset",scope.AfterOffset),Convert.ToBase64String(scope.NativeAfter)),new XElement("ownerAfter",after));
                if(!SaveLocalStage(scope,"consumed",report))throw new InvalidDataException();
            }
            scope.AfterFrame=after;scope.Consumed=true;
        }
        catch{scope.Unknown=true;/* Actual native effects remain attempted and cannot be replayed. */}
    }
    private static void InvokePreparedLocalOwner(LocalOwnerScope scope)
    {
        if(scope.Attempted||scope.Unknown||!scope.BeforePublished||!scope.BeforeSaved||!scope.CurrentWindow()||!scope.BodyCurrent(false))return;
        var recipes=scope.Members.Select(value=>value.Receipt.Receipt.RecipeName).Distinct(StringComparer.Ordinal).ToArray();
        if(!TryMeasureDeferredLiveFrame(scope.Actor,recipes,out int size)||!scope.ReserveFrame(size,true)||
            !XNode.DeepEquals(CaptureDeferredLiveFrame(scope.Actor,recipes),scope.BeforeFrame))return;
        if(!SaveLocalStage(scope,"attempted",new XElement("localNativeOwnerAttempt",new XAttribute("window",scope.WindowSession),
            new XAttribute("beforeNative",Hash(scope.NativeBefore)),new XAttribute("beforePds",Hash(scope.BeforeFile)))))return;
        scope.Attempted=true;scope.Invoking=true;
        try{scope.Station.CheckForCraftComplete(scope.Actor);if(!scope.Consumed)scope.Unknown=true;}
        catch{scope.Unknown=true;}
        finally{scope.Invoking=false;}
    }
}
