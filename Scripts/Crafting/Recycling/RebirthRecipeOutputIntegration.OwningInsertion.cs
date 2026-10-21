using System;
using System.Collections.Generic;
using System.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    private sealed class GridInsertion
    {internal OwningObservation Original;internal XUiC_WorkstationOutputGrid Grid;internal byte[] Expected,Primary;internal bool Argument;}
    private sealed class InventoryInsertion
    {
        internal OwningObservation Original;internal XUiM_PlayerInventory Inventory;internal InventoryInsertion Parent;
        internal byte[] Incoming,BeforeBag,BeforeBelt,AfterBag,AfterBelt;internal byte[][] ExpectedItems;
        internal string[] BeforeEquipment,AfterEquipment;
        internal readonly List<InventoryInsertion> Children=new List<InventoryInsertion>();internal bool Accepted,NativeRan;
    }
    [ThreadStatic]private static InventoryInsertion inventoryInsertion;
    internal static byte[] OwningEquipmentNative(EntityPlayer player)
    {
        using(var stream=new System.IO.MemoryStream())
        using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            writer.SetBaseStream(stream);player.equipment.Write(writer,StreamModeWrite.Persistency);writer.Flush();
            if(stream.Length>4*1024*1024)throw new System.IO.InvalidDataException();
            return stream.ToArray();
        }
    }
    private static bool SameOwningEquipment(OwningObservation original)=>
        original.EquipmentBefore!=null&&original.EquipmentBefore.SequenceEqual(OwningEquipmentNative(original.Viewer));
    private static string[] OwningEquipment(EntityPlayerLocal player)
    {
        var result=new string[player.equipment.GetSlotCount()];
        for(int i=0;i<result.Length;i++)result[i]=RebirthNativeItemCodec.Encode(player.equipment.GetSlotItem(i));
        return result;
    }
    private static bool SameOwningGrid(byte[] image,ItemStack[] slots)=>image!=null&&OwningGrid(slots).SequenceEqual(image);
    private static bool StationSideImages(OwningObservation o)=>
        (o.Window.inputWindow==null?o.InputBefore.Length==0:SameOwningGrid(o.InputBefore,o.Window.inputWindow.GetSlots()))&&
        (o.Window.fuelWindow==null?o.FuelBefore.Length==0:SameOwningGrid(o.FuelBefore,o.Window.fuelWindow.GetSlots()))&&
        (o.Window.toolWindow==null?o.ToolsBefore.Length==0:SameOwningGrid(o.ToolsBefore,o.Window.toolWindow.GetSlots()));
    private static bool CurrentBeforeImages(OwningObservation o)=>StationSideImages(o)&&
        SameOwningGrid(o.OutputBefore,o.Grid==null?o.Station.Output:o.Grid.GetSlots())&&
        SameOwningGrid(o.BagBefore,o.Viewer.bag.ItemGrid.items)&&SameOwningGrid(o.BeltBefore,o.Viewer.inventory.ItemGrid.items)&&SameOwningEquipment(o);
    private static bool CurrentAcceptedImages(OwningObservation o)=>StationSideImages(o)&&
        SameOwningGrid(o.OutputAfter,o.Grid==null?o.Station.Output:o.Grid.GetSlots())&&
        SameOwningGrid(o.BagAfter,o.Viewer.bag.ItemGrid.items)&&SameOwningGrid(o.BeltAfter,o.Viewer.inventory.ItemGrid.items)&&
        o.EquipmentAfter!=null&&o.EquipmentAfter.SequenceEqual(OwningEquipmentNative(o.Viewer));
    private static bool ChargeOwning(OwningObservation original,long bytes)
    {
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12*bytes+4096),out var allocation))return false;
        original.Charges.Add(allocation);return true;
    }
    private static bool BindActualOwningExtras(OwningObservation original)
    {
        if(original.ExtrasBound)return true;
        var scope=ui;
        if(scope==null||!ReferenceEquals(scope.Card,original.Card)||scope.Outputs==null||!original.RecipeMatches())return false;
        var bytes=OwningGrid(scope.Outputs.ToArray());if(!ChargeOwning(original,bytes.Length))return false;
        original.ExtrasNative=bytes;original.ExtrasBound=true;return true;
    }
    private static bool ExactPrimary(OwningObservation o,ItemStack primary,byte[] bytes)
        =>primary?.itemValue!=null&&primary.count==o.Recipe.count&&primary.itemValue.type==o.Recipe.itemValueType&&
            primary.itemValue.ItemClass!=null&&primary.itemValue.Quality==(primary.itemValue.ItemClass.HasQuality?o.OutputQuality:0)&&
            (!o.Retry||o.CachedPrimaryNative.SequenceEqual(bytes));
    internal static object BeginOwningGridInsert(XUiC_WorkstationOutputGrid grid,bool runOriginal)
    {
        try
        {
            var original=owningObservation;
            if(!runOriginal||original==null||original.Accepted||!ReferenceEquals(original.Grid,grid))return null;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!original.Current()||!original.RecipeMatches()||!BindActualOwningExtras(original)||!StationSideImages(original)||!SameOwningEquipment(original)||
                    !SameOwningGrid(original.BagBefore,original.Viewer.bag.ItemGrid.items)||!SameOwningGrid(original.BeltBefore,original.Viewer.inventory.ItemGrid.items))return null;
                var primary=new ItemStack(original.Card.outputItemValue,original.Recipe.count);var image=OwningGrid(new[]{primary});
                if(!ExactPrimary(original,primary,image)||!PredictOwningOutput(RebirthCamperInventorySnapshot.Restore(original.OutputBefore),primary,
                    RebirthCamperInventorySnapshot.Restore(original.ExtrasNative),original.Retry,out var after))return null;
                var expected=OwningGrid(after);if(!ChargeOwning(original,expected.Length+image.Length))return null;
                return new GridInsertion{Original=original,Grid=grid,Expected=expected,Primary=image};
            }
        }
        catch{return null;}
    }
    internal static void BindOwningGridArgument(object state,ItemStack[] cells)
    {
        try
        {
            if(state is not GridInsertion insertion)return;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                if(decoder!=null)insertion.Argument=SameOwningGrid(insertion.Expected,cells);
        }
        catch{/* Native caller remains unaffected; no accepted evidence. */}
    }
    internal static void ObserveOwningGridInsert(XUiC_WorkstationOutputGrid grid,bool runOriginal,object state)
    {
        try
        {
            if(!runOriginal||state is not GridInsertion insertion||!insertion.Argument)return;
            var original=insertion.Original;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!ReferenceEquals(owningObservation,original)||original.Accepted||!original.Current()||!original.RecipeMatches()||
                    !ReferenceEquals(grid,insertion.Grid)||!SameOwningEquipment(original)||!SameOwningGrid(insertion.Expected,grid.GetSlots())||
                    !SameOwningGrid(insertion.Expected,original.Station.Output)||!StationSideImages(original)||
                    !SameOwningGrid(original.BagBefore,original.Viewer.bag.ItemGrid.items)||!SameOwningGrid(original.BeltBefore,original.Viewer.inventory.ItemGrid.items))return;
                original.PrimaryNative=insertion.Primary;original.OutputAfter=insertion.Expected;
                original.BagAfter=original.BagBefore;original.BeltAfter=original.BeltBefore;original.EquipmentAfter=original.EquipmentBefore;original.OriginalAccepted=true;
                original.AcceptedSink=OwningSink.Output;original.Accepted=true;
            }
        }
        catch{/* Exact native event/postimage missing; retain unknown. */}
    }
    internal static object BeginOwningInventoryInsert(XUiM_PlayerInventory inventory,ItemStack primary,bool runOriginal)
    {
        try
        {
            var original=owningObservation;var parent=inventoryInsertion;
            if(!runOriginal||original==null||original.Accepted||!ReferenceEquals(original.Card.xui.PlayerInventory,inventory)||
                primary?.itemValue==null||primary.count<1||parent!=null&&!ReferenceEquals(parent.Original,original))return null;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!original.Current()||!original.RecipeMatches()||!BindActualOwningExtras(original)||!StationSideImages(original))return null;
                var incoming=OwningGrid(new[]{primary});
                if(parent==null&&(!ExactPrimary(original,primary,incoming)||!CurrentBeforeImages(original)))return null;
                var extras=parent==null?RebirthCamperInventorySnapshot.Restore(original.ExtrasNative):Array.Empty<ItemStack>();
                if(!PlanInventory(original.Card,primary,extras,out var afterBag,out var afterBelt,out var afterEquipment))return null;
                var node=new InventoryInsertion{Original=original,Inventory=inventory,Parent=parent,Incoming=incoming,
                    BeforeBag=OwningGrid(original.Viewer.bag.ItemGrid.items),BeforeBelt=OwningGrid(original.Viewer.inventory.ItemGrid.items),
                    AfterBag=OwningGrid(afterBag),AfterBelt=OwningGrid(afterBelt),
                    BeforeEquipment=OwningEquipment(original.Viewer),AfterEquipment=afterEquipment.Select(RebirthNativeItemCodec.Encode).ToArray(),
                    ExpectedItems=new[]{primary}.Concat(extras).Select(value=>OwningGrid(new[]{value})).ToArray()};
                long bytes=incoming.Length+node.BeforeBag.Length+node.BeforeBelt.Length+node.AfterBag.Length+node.AfterBelt.Length+node.ExpectedItems.Sum(value=>(long)value.Length);
                bytes=checked(bytes+node.BeforeEquipment.Sum(value=>2L*value.Length)+node.AfterEquipment.Sum(value=>2L*value.Length));
                if(!ChargeOwning(original,bytes))return null;
                parent?.Children.Add(node);inventoryInsertion=node;return node;
            }
        }
        catch{return null;}
    }
    internal static void ObserveOwningInventoryInsert(XUiM_PlayerInventory inventory,bool runOriginal,bool result,object state)
    {
        try
        {
            if(!result||state is not InventoryInsertion node||!ReferenceEquals(inventoryInsertion,node)||!ReferenceEquals(node.Inventory,inventory))return;
            var original=node.Original;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!ReferenceEquals(owningObservation,original)||!original.Current()||!original.RecipeMatches()||!StationSideImages(original)||
                    !SameOwningGrid(node.AfterBag,original.Viewer.bag.ItemGrid.items)||!SameOwningGrid(node.AfterBelt,original.Viewer.inventory.ItemGrid.items)||
                    !node.AfterEquipment.SequenceEqual(OwningEquipment(original.Viewer)))return;
                bool native=runOriginal&&node.Children.Count==0;
                bool children=!runOriginal&&node.Children.Count==node.ExpectedItems.Length;
                if(children)for(int i=0;i<node.Children.Count;i++)
                {
                    var child=node.Children[i];
                    if(!child.Accepted||!child.NativeRan||!child.Incoming.SequenceEqual(node.ExpectedItems[i])||
                        !child.BeforeBag.SequenceEqual(i==0?node.BeforeBag:node.Children[i-1].AfterBag)||
                        !child.BeforeBelt.SequenceEqual(i==0?node.BeforeBelt:node.Children[i-1].AfterBelt)||
                        !child.BeforeEquipment.SequenceEqual(i==0?node.BeforeEquipment:node.Children[i-1].AfterEquipment)){children=false;break;}
                }
                if(!native&&!children)return;node.NativeRan=native;node.Accepted=true;
                if(node.Parent!=null)return;
                var equipmentAfter=OwningEquipmentNative(original.Viewer);
                if(!ChargeOwning(original,equipmentAfter.Length))return;
                if(!SameOwningGrid(original.OutputBefore,original.Grid==null?original.Station.Output:original.Grid.GetSlots()))return;
                original.PrimaryNative=node.Incoming;original.OutputAfter=original.OutputBefore;original.BagAfter=node.AfterBag;original.BeltAfter=node.AfterBelt;
                original.EquipmentAfter=equipmentAfter;
                original.OriginalAccepted=native;original.ChildrenAccepted=children;original.Replacement=children;
                original.AcceptedSink=OwningSink.Inventory;original.Accepted=true;
            }
            RebirthWorkstationCompletionPipeline.ObserveNativeInventoryInsertion(original);
        }
        catch{/* A successful setter without complete proof stays unknown. */}
    }
    internal static void EndOwningInventoryInsert(object state)
    {if(state is InventoryInsertion node&&ReferenceEquals(inventoryInsertion,node))inventoryInsertion=node.Parent;}
}
