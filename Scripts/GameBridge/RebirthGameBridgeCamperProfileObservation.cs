using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HarmonyLib;
using UnityEngine;

// Read-only physical inventory/UI bindings. Never opens a window or invokes an
// input callback. Paths mirror the existing bridge visible-tree traversal.
public sealed class ConsoleCmdRebirthCamperProfileObservation : ConsoleCmdAbstract
{
    // Current cooking controller exposes no selected-recipe getter. Read only
    // its exact inspected field; resolve once, never alter preparation/selection.
    private static readonly AccessTools.FieldRef<XUiC_RebirthCookingWorkspace,Recipe> cookingSelection
        = AccessTools.FieldRefAccess<XUiC_RebirthCookingWorkspace,Recipe>("selected");
    public override string[] getCommands()=>new[]{"rbcamperregprofile"};
    public override string getDescription()=>"Observe visible camper qualification slots and physical source identities.";
    public override string getHelp()=>"rbcamperregprofile (local CodexTest Bridge only).";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        var world=GameManager.Instance?.World;
        var player=world?.GetPrimaryPlayer();
        if(args.Count!=0||sender.RemoteClientInfo!=null||player==null||world.IsRemote()
            ||!RebirthGameBridge.IsRunning||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")
        {SdtdConsole.Instance.Output("*** ERROR "+getHelp());return;}
        try
        {
            var ui=LocalPlayerUI.GetUIForPlayer(player);
            if(ui?.xui?.WindowGroups==null||player.bag?.ItemGrid?.items==null)
                throw new InvalidOperationException("native inventory/UI unavailable");
            var sources=new JArray();var visible=new JArray();var selected=new JArray();var vehicleFrames=new JArray();
            var contextSources=new JArray();
            var context=RebirthContextNavigationService.Session;
            if(context?.Ui==ui.xui&&!context.Suspended)
            {
                var cells=context.GetSlots(false);
                if(cells!=null)for(int i=0;i<cells.Length;i++)
                    if(cells[i]!=null&&!cells[i].IsEmpty())contextSources.Add(Source(cells[i],i,"container"));
            }
            var bag=player.bag.ItemGrid.items;
            for(int i=0;i<bag.Length;i++)
                if(bag[i]!=null&&!bag[i].IsEmpty())sources.Add(Source(bag[i],i));
            foreach(var group in ui.xui.WindowGroups)
                if(group!=null&&group.Controller!=null
                    &&(group.isShowing||ui.windowManager.IsWindowOpen(group.Id)))
                    Walk(group.Controller,group.Id,player,bag,visible,selected,vehicleFrames);
            SdtdConsole.Instance.Output(new JObject{["schema"]=1,["observationOnly"]=true,
                ["playerId"]=player.entityId,["paused"]=GameManager.Instance.IsPaused(),
                ["modalOpen"]=ui.windowManager.IsModalWindowOpen(),["inputActive"]=ui.windowManager.IsInputActive(),
                ["categoryChoiceCount"]=Enum.GetValues(typeof(QuickStackCategory)).Length+1,
                ["sources"]=sources,["contextSources"]=contextSources,["visibleSlots"]=visible,["selectedRecipes"]=selected,
                ["vehicleFrames"]=vehicleFrames}.ToString(Formatting.None));
        }
        catch(Exception error){SdtdConsole.Instance.Output("*** ERROR profile observation: "+error.Message);}
    }
    private static JObject Source(ItemStack stack,int index,string location="backpack")
    {
        var bytes=RebirthResourceItemFingerprint.Capture(stack.itemValue);
        if(bytes==null)throw new InvalidOperationException("native item identity unavailable");
        string name=stack.itemValue.ItemClass.GetItemName();
        return new JObject{["location"]=location,["slot"]=index,["name"]=name,["count"]=stack.count,
            ["payload"]=Convert.ToBase64String(bytes),
            ["category"]=QuickStackCategoryRegistry.Get(stack.itemValue.type).ToString(),
            ["hasQuality"]=stack.itemValue.HasQuality,["quality"]=stack.itemValue.Quality,
            ["plainStation"]=(name=="campfire"||name=="workbench")&&stack.count==1
                &&(stack.itemValue.Metadata==null||stack.itemValue.Metadata.Count==0)
                &&!stack.itemValue.HasMods()};
    }
    private static void Walk(XUiController controller,string path,EntityPlayerLocal player,ItemStack[] bag,JArray result,JArray selected,JArray vehicleFrames)
    {
        if(controller==null)return;
        var view=controller.ViewComponent;
        if(view!=null&&(!view.IsVisible||view.UiTransform==null||!view.UiTransform.gameObject.activeInHierarchy))return;
        if(controller is XUiC_RebirthCraftingRecipeDetails details&&details.SelectedRecipe!=null)
            selected.Add(new JObject{["path"]=path,["recipe"]=details.SelectedRecipe.GetName(),["tier"]=details.SelectedCraftingTier});
        if(controller is XUiC_RebirthCookingWorkspace cooking&&cooking.IsCookingOpen)
            selected.Add(new JObject{["path"]=path,["recipe"]=cookingSelection(cooking)?.GetName(),["tier"]=0});
        if(controller is XUiC_VehicleFrameWindow frame)
        {
            var group=frame.WindowGroup?.Controller as XUiC_VehicleWindowGroup;
            vehicleFrames.Add(new JObject{["path"]=path,["controller"]=frame.GetType().Name,
                ["vehicleEntityId"]=frame.Vehicle?.entityId,["windowGroupVehicleEntityId"]=group?.CurrentVehicleEntity?.entityId});
        }
        if(controller is XUiC_BasePartStack part)
        {
            if(result.Count>=1024)throw new InvalidOperationException("visible slot observation exceeds bound");
            var group=part.WindowGroup?.Controller as XUiC_VehicleWindowGroup;
            var owningFrame=group?.GetChildByType<XUiC_VehicleFrameWindow>();
            result.Add(new JObject{["path"]=path,["controller"]=part.GetType().Name,
                ["role"]=part is XUiC_RebirthCamperModuleSlot?"module":part is XUiC_ItemCosmeticStack?"cosmetic":
                    part is XUiC_VehiclePartStack?"vehicle-part":part is XUiC_ItemPartStack?"item-part":"base-part",
                ["slotNumber"]=part.SlotNumber,["slotType"]=part.SlotType,["stackLocation"]=part.StackLocation.ToString(),
                ["empty"]=part.ItemStack==null||part.ItemStack.IsEmpty(),["stack"]=PartStack(part.ItemStack),
                ["frameVehicleEntityId"]=owningFrame?.Vehicle?.entityId,
                ["windowGroupVehicleEntityId"]=group?.CurrentVehicleEntity?.entityId});
        }
        else if(controller is XUiC_ItemStack slot)
        {
            if(result.Count>=1024)throw new InvalidOperationException("visible slot observation exceeds bound");
            var record=new JObject{["path"]=path,["controller"]=slot.GetType().Name,["slotNumber"]=slot.SlotNumber,
                ["empty"]=slot.ItemStack==null||slot.ItemStack.IsEmpty()};
            record["geometry"]=Geometry(slot);
            record["dragCursor"]=slot.IsDragAndDrop;
            if(slot.ItemStack!=null&&!slot.ItemStack.IsEmpty())record["stack"]=Source(slot.ItemStack,slot.SlotNumber,"presentation");
            if(slot is XUiC_RebirthContextSlot proxy)
            {
                record["contextBackpack"]=proxy.IsBackpack;
                record["presented"]=proxy.IsPresented;
                record["intersectsViewport"]=proxy.Grid?.IntersectsViewport(proxy.SlotNumber)==true;
            }
            record["role"]=slot is XUiC_RebirthCookingSlot?"ingredient":
                slot.GetParentByType<XUiC_WorkstationFuelGrid>()!=null?"fuel":
                slot.GetParentByType<XUiC_WorkstationToolGrid>()!=null?"tool":
                slot.GetParentByType<XUiC_WorkstationOutputGrid>()!=null?"output":
                slot.GetParentByType<XUiC_WorkstationInputGrid>()!=null?"input":"other";
            if(slot.StackLocation==XUiC_ItemStack.StackLocationTypes.Backpack&&slot.SlotNumber>=0&&slot.SlotNumber<bag.Length
                &&ReferenceEquals(slot.xui?.playerUI?.entityPlayer,player))
            {
                var context=slot as XUiC_RebirthContextSlot;
                var scroll=slot.GetParentByType<XUiC_RebirthCraftingInventoryScroll>();
                bool presented=(context==null||context.IsBackpack&&context.IsPresented)
                    &&(scroll==null||scroll.IsSlotVisible(slot.SlotNumber));
                var physical=bag[slot.SlotNumber];
                var shown=slot.ItemStack;
                if(presented&&physical!=null&&!physical.IsEmpty()&&shown!=null&&!shown.IsEmpty()
                    &&physical.count==shown.count&&RebirthResourceItemFingerprint.Matches(shown.itemValue,
                        RebirthResourceItemFingerprint.Capture(physical.itemValue)))
                    record["source"]=Source(physical,slot.SlotNumber);
            }
            if(controller is XUiC_RequiredItemStack)
            {
                var accepts=new JArray();
                // These are current workspace-authored tool names, not guesses
                // from localized labels. CanSwap is admission only, no input.
                foreach(string name in new[]{"toolCookingPot","FuriousRamsayHammerPliers","FuriousRamsayScrewdriver"})
                {
                    var item=ItemClass.GetItemClass(name);
                    if(item!=null&&slot.CanSwap(new ItemStack(new ItemValue(item.Id),1)))accepts.Add(name);
                }
                record["acceptsTools"]=accepts;
            }
            result.Add(record);
        }
        var children=controller.Children;if(children==null)return;
        var ids=new Dictionary<string,int>();
        foreach(var child in children)
        {
            string id=child?.ViewComponent?.ID;
            if(!string.IsNullOrEmpty(id)){int count;ids.TryGetValue(id,out count);ids[id]=count+1;}
        }
        for(int i=0;i<children.Count;i++)
        {
            var child=children[i];string id=child?.ViewComponent?.ID;
            string segment=!string.IsNullOrEmpty(id)&&ids[id]==1?id:(id??"")+"#"+i;
            Walk(child,path+"/"+segment,player,bag,result,selected,vehicleFrames);
        }
    }
    private static JObject PartStack(ItemStack stack)
    {
        if(stack==null)return null;
        if(stack.itemValue==null)throw new InvalidOperationException("native part ItemStack value unavailable");
        // The resource fingerprint intentionally rejects empty values. Part
        // observation also needs genuine native empty-slot evidence; use its
        // same exact ItemValue writer without that admission-only empty gate.
        byte[] bytes;
        using(var stream=new System.IO.MemoryStream())
        using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(stream);ItemValue.Write(stack.itemValue,writer);writer.Flush();bytes=stream.ToArray();}
        return new JObject{["count"]=stack.count,["type"]=stack.itemValue.type,
            ["name"]=stack.itemValue.ItemClass?.GetItemName(),["seed"]=stack.itemValue.Seed,["meta"]=stack.itemValue.Meta,
            ["quality"]=stack.itemValue.Quality,["payload"]=Convert.ToBase64String(bytes)};
    }
    private static JObject Geometry(XUiC_ItemStack slot)
    {
        var rails=new JArray();
        foreach(var child in slot.Children)
            if(child.ViewComponent?.ID=="durability"||child.ViewComponent?.ID=="durabilityBackground")
                rails.Add(ViewGeometry(child.ViewComponent));
        return new JObject{["root"]=ViewGeometry(slot.ViewComponent),
            ["icon"]=ViewGeometry(slot.GetChildById("itemIcon")?.ViewComponent),
            ["count"]=ViewGeometry(slot.GetChildById("stackValue")?.ViewComponent),["rails"]=rails};
    }
    private static JObject ViewGeometry(XUiView view)
    {
        if(view==null)return null;
        var scale=view.UiTransform?.localScale??Vector3.one;
        // Pivot belongs to the native widget views, not the XUiView base.
        string pivot=view is XUiV_Sprite sprite?sprite.Pivot.ToString():
            view is XUiV_Label pivotLabel?pivotLabel.Pivot.ToString():null;
        var result=new JObject{["id"]=view.ID,["kind"]=view.GetType().Name,["visible"]=view.IsVisible,
            ["size"]=new JArray(view.Size.x,view.Size.y),["position"]=new JArray(view.Position.x,view.Position.y),
            ["pivot"]=pivot,["scale"]=new JArray(scale.x,scale.y,scale.z)};
        if(view is XUiV_Label label){result["fontSize"]=label.FontSize;result["text"]=label.Text;result["alignment"]=label.Alignment.ToString();}
        var camera=view.Controller?.xui?.playerUI?.uiCamera?.cachedCamera;
        if(camera!=null&&view.UiTransform!=null)
        {
            Bounds bounds=NGUIMath.CalculateAbsoluteWidgetBounds(view.UiTransform);
            if(bounds.size.sqrMagnitude>0f)
            {
                Vector3 a=camera.WorldToScreenPoint(bounds.min),b=camera.WorldToScreenPoint(bounds.max);
                result["rect"]=new JArray(Mathf.Min(a.x,b.x),Screen.height-Mathf.Max(a.y,b.y),Mathf.Abs(b.x-a.x),Mathf.Abs(b.y-a.y));
            }
        }
        return result;
    }
}
