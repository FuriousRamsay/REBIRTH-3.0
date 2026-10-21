using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    private sealed class NativeVersion {internal long Value,LastInvocation;}
    private static readonly ConditionalWeakTable<XUiC_RecipeStack,NativeVersion> nativeVersions=new ConditionalWeakTable<XUiC_RecipeStack,NativeVersion>();
    [ThreadStatic]private static XUiC_RecipeStack nativeUpdateCard;
    [ThreadStatic]private static long nativeUpdateId;
    private static long nextNativeUpdateId;
    internal static bool IsActualOwningUpdate(XUiC_RecipeStack card)=>card!=null&&ReferenceEquals(nativeUpdateCard,card)&&nativeUpdateId>0;
    internal struct OwningUpdateStamp
    {internal XUiC_RecipeStack Card,Previous;internal long Id,PreviousId;}
    internal static OwningUpdateStamp BeginOwningUpdateStamp(XUiC_RecipeStack card)
    {
        var stamp=new OwningUpdateStamp{Card=card,Id=++nextNativeUpdateId,Previous=nativeUpdateCard,PreviousId=nativeUpdateId};
        nativeUpdateCard=card;nativeUpdateId=stamp.Id;return stamp;
    }
    internal static long MarkOwningNativeUpdate(XUiC_RecipeStack card,bool ran)
    {
        if(ran&&card!=null&&ReferenceEquals(nativeUpdateCard,card)&&nativeUpdateId>0)ObserveUiOutputQueue(card);
        if(!ran||card==null||!ReferenceEquals(nativeUpdateCard,card)||nativeUpdateId<1||!nativeVersions.TryGetValue(card,out var version))return -1;
        if(version.LastInvocation!=nativeUpdateId){version.LastInvocation=nativeUpdateId;version.Value++;}
        var original=owningObservation;
        if(original!=null&&ReferenceEquals(original.Card,card)){original.RanUpdate=true;original.NativeVersionAfter=version.Value;}
        return version.Value;
    }
    internal static void EndOwningUpdateStamp(OwningUpdateStamp stamp)
    {if(ReferenceEquals(nativeUpdateCard,stamp.Card)&&nativeUpdateId==stamp.Id){nativeUpdateCard=stamp.Previous;nativeUpdateId=stamp.PreviousId;}}
    private static long OwningNativeVersion(XUiC_RecipeStack card)=>nativeVersions.GetValue(card,_=>new NativeVersion()).Value;
    internal static bool IsCurrentOwningNativeVersion(XUiC_RecipeStack card,long version)=>card!=null&&version>0&&nativeVersions.TryGetValue(card,out var current)&&current.Value==version;
    internal static bool IsCurrentOwningObservedVersion(XUiC_RecipeStack card,long version)=>card!=null&&version>=0&&nativeVersions.TryGetValue(card,out var current)&&current.Value==version;
    // Called only from the actual successful native Update observer while its
    // original scope is still active. This is positive no-physical-output at a
    // specific native full/cached-full branch, never absence-of-receipt proof.
    private static void TryFreezeOwningFullBoundary(OwningObservation original)
    {
        if(original.FullBoundary||original.Continued||original.Accepted||original.Updated||original.Taken||!original.RanUpdate||
            original.NativeVersionAfter<1||OwningNativeVersion(original.Card)!=original.NativeVersionAfter||!original.Current()||
            !original.RecipeMatches()||!original.Card.isInventoryFull||original.Card.GetRecipeCount()!=original.BeforeCount||
            original.Card.GetRecipeCraftingTimeLeft()>0||!CurrentBeforeImages(original))return;
        bool fresh=!original.Retry&&original.OutputStackRan&&!original.OutputStackReturned&&original.OutputStackCalls==1;
        bool cached=original.Retry&&!original.OutputStackRan&&original.OutputStackCalls==0;
        if(!fresh&&!cached||original.Card.outputItemValue==null||original.Card.outputItemValue.IsEmpty())return;
        var primary=OwningGrid(new[]{new ItemStack(original.Card.outputItemValue,original.Recipe.count)});
        if(!ExactPrimary(original,new ItemStack(original.Card.outputItemValue,original.Recipe.count),primary))return;
        if(!RebirthRemoteResourcePlacedStationQueue.TryDecode(original.UiQueueFull,out var beforeQueue))return;
        var actual=original.Window.craftingQueue.GetRecipesToCraft().Select(ActualCardRow).ToArray();
        if(actual.Length!=beforeQueue.Length)return;
        actual[original.Index].CraftingTimeLeft=beforeQueue[original.Index].CraftingTimeLeft;
        if(RebirthRemoteResourcePlacedStationQueue.Encode(actual)!=original.UiQueueFull||
            !RebirthProductiveEarnedNativeBody.TryCaptureCurrent(original.Station,out var nativeAfter,out int elapsed))return;
        if(!ChargeOwning(original,primary.Length+nativeAfter.Length))return;
        var evidence=OwningEvidenceHeader(original,"nativeOwningFullBoundary");
        evidence.Add(new XAttribute("nativeVersion",original.NativeVersionAfter),new XAttribute("freshCallback",fresh),new XAttribute("cachedCallback",cached),
            new XAttribute("nativeElapsedOffset",elapsed),new XAttribute("cachedTime",original.Card.GetRecipeCraftingTimeLeft()),
            new XAttribute("cachedCrafting",original.Card.IsCrafting),new XAttribute("cachedTotalTime",original.Card.totalCraftTimeLeft),new XElement("recipeFull",original.RecipeFull));
        AddOwningImages(evidence,new[]{"primaryNative","nativeBefore","nativeAfter","outputBefore","bagBefore","beltBefore","equipmentBefore"},
            new[]{primary,original.NativeBefore,nativeAfter,original.OutputBefore,original.BagBefore,original.BeltBefore,original.EquipmentBefore});
        original.FullBoundaryPrimary=primary;original.FullBoundaryEvidence=evidence;original.FullBoundary=true;
    }
    internal static bool TryContinueOwningCached(OwningObservation original,out OwningObservation successor)
    {
        successor=null;
        try
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||original==null||!original.FullBoundary||original.FullBoundaryEvidence==null||original.Continued||original.Accepted||
                    original.Updated||original.Taken||!original.Current()||!original.RecipeMatches()||!original.Card.isInventoryFull||
                    original.Card.GetRecipeCount()!=original.BeforeCount||original.NativeVersionAfter<1||
                    OwningNativeVersion(original.Card)!=original.NativeVersionAfter||original.Card.outputItemValue==null||
                    !OwningGrid(new[]{new ItemStack(original.Card.outputItemValue,original.Recipe.count)}).SequenceEqual(original.FullBoundaryPrimary))return false;
                if(!CanFitOwningCompletion(original.Card,true))return false; // Hold this positive boundary until actual native cached output can fit.
                var value=new OwningObservation(original.Card,true,original.Operation);
                if(original.RemoteOutputRestart!=null)
                {
                    if(original.RemoteOutputRestart.FinalCallbackSaved||!original.RemoteOutputRestart.BindingCurrent())
                    {value.Dispose();return false;}
                    value.RemoteOutputRestart=original.RemoteOutputRestart;
                }
                long bytes=value.RecipeNative.Length+2L*(value.RecipeFull.Length+value.UiQueueFull.Length+value.NativeQueueFull.Length)+value.NativeBefore.Length+
                    value.InputBefore.Length+value.FuelBefore.Length+value.ToolsBefore.Length+value.OutputBefore.Length+value.BagBefore.Length+value.BeltBefore.Length+value.ExtrasNative.Length+value.EquipmentBefore.Length;
                if(!value.Current()||!value.CachedPrimaryNative.SequenceEqual(original.FullBoundaryPrimary)||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12*bytes+65536),out value.Allocation)){value.Dispose();return false;}
                value.CachedPredecessor=new XElement(original.FullBoundaryEvidence);value.CachedPrevious=original;
                original.Continued=true;successor=value;return true;
            }
        }
        catch{return false;}
    }
}
