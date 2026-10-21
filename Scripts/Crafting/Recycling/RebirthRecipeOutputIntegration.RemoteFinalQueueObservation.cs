using System;
using System.Linq;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    internal sealed partial class RemoteOutputColdClient
    {
        internal bool ObserveSavedFinalQueue(OwningObservation observed)
        {
            if(!FinalCallbackSaved||!CallbackReturned||!QueueOnlyAuthorized||!BindingCurrent()||
                !ReferenceEquals(observed?.RemoteOutputRestart,this)||!ReferenceEquals(observed.Card,Card)||
                observed.Accepted||observed.RemoteFinalQueueObserved||observed.Updated||observed.Taken||
                !observed.RanUpdate||observed.NativeVersionAfter<1||OwningNativeVersion(Card)!=observed.NativeVersionAfter||
                !observed.Current(true)||!observed.RecipeMatches()||Card.GetRecipeCount()!=observed.BeforeCount-1||
                !pendingUiOutputs.TryGetValue(Card,out var pending)||!ReferenceEquals(pending.Source?.RemoteCold,this)||
                !pending.NativeCallbackEntered||!pending.NativeContinuationEntered||!pending.Returned||pending.Source.OutputPrefix!=Prefix||
                !StationSideImages(observed))return false;
            var saved=request.Element("physicalDelivery")?.Elements().SingleOrDefault();
            if(saved?.Name!="nativeOwningDelivery"||(string)saved.Attribute("operation")!=Operation.ToString("N")||
                (int?)saved.Attribute("beforeCount")!=observed.BeforeCount||(int?)saved.Attribute("afterCount")!=Card.GetRecipeCount()||
                !Enum.TryParse((string)saved.Attribute("sink"),out OwningSink sink)||(sink!=OwningSink.Inventory&&sink!=OwningSink.Output))return false;
            byte[] output=Convert.FromBase64String(saved.Element("outputAfter").Value),bag=Convert.FromBase64String(saved.Element("bagAfter").Value),
                belt=Convert.FromBase64String(saved.Element("beltAfter").Value),equipment=Convert.FromBase64String(saved.Element("equipmentAfter").Value);
            if(!SameOwningGrid(output,observed.Grid==null?observed.Station.Output:observed.Grid.GetSlots())||
                !SameOwningGrid(bag,observed.Viewer.bag.ItemGrid.items)||!SameOwningGrid(belt,observed.Viewer.inventory.ItemGrid.items)||
                !equipment.SequenceEqual(OwningEquipmentNative(observed.Viewer))||
                !ChargeOwning(observed,checked(output.Length+bag.Length+belt.Length+equipment.Length)))return false;
            observed.OutputAfter=output;observed.BagAfter=bag;observed.BeltAfter=belt;observed.EquipmentAfter=equipment;
            observed.AcceptedSink=sink;observed.AfterCount=Card.GetRecipeCount();observed.RemoteFinalQueueObserved=true;observed.Updated=true;
            return true;
        }
        internal bool CopySavedFinalQueueDelivery(OwningObservation observed,out XElement evidence)
        {
            evidence=null;
            if(!FinalCallbackSaved||!CallbackReturned||!QueueOnlyAuthorized||!BindingCurrent()||
                !ReferenceEquals(observed?.RemoteOutputRestart,this)||!observed.RemoteFinalQueueObserved||!observed.RanUpdate||
                !observed.Updated||!observed.Current(true)||!observed.RecipeMatches()||observed.NativeVersionAfter<1||
                OwningNativeVersion(observed.Card)!=observed.NativeVersionAfter||!CurrentAcceptedImages(observed))return false;
            var saved=request.Element("physicalDelivery")?.Elements().SingleOrDefault();
            if(saved==null||(int?)saved.Attribute("afterCount")!=observed.AfterCount)return false;
            evidence=new XElement(saved);return true;
        }
    }
}
