using System;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

// The native input grid owns staging, drag, split, shift and station persistence.
// Only the authoritative runtime consumes that staged input and publishes grouped jobs.
[Preserve]
public sealed class XUiC_RebirthRecyclerInputGrid : XUiC_WorkstationInputGrid
{
    private float nextPoll;
    public string GateReason { get; private set; }
    public bool Accepts(ItemStack stack,out string reason)
    {
        reason="xuiNoScrapOutputsDefined";
        var station=WorkstationData?.TileEntity;
        if(!RebirthRecyclerRuntime.CanAccept(xui.playerUI.entityPlayer,station,stack,out reason))
        {if(reason=="xuiScrapToolRequired")reason="xuiRebirthRecyclingToolsRequired";return false;}
        var slots=GetSlots();
        if(slots!=null && slots.Length>0 && !slots[0].IsEmpty() && !slots[0].CanStackWith(stack))
        {reason="xuiRebirthRecyclingInputOccupied";return false;}
        reason=null;return true;
    }
    public override bool AddItem(ItemClass item,ItemStack stack)
    {return Accepts(stack,out _) && base.AddItem(item,stack);}
    public override void UpdateBackend(ItemStack[] stacks)
    {
        var original=WorkstationData?.TileEntity?.Input;
        if(original==null || stacks==null)return;
        var merged=ItemStack.CreateArray(original.Length);
        for(int i=0;i<original.Length;i++)merged[i]=original[i].Clone();
        if(stacks.Length>0 && merged.Length>0)merged[0]=stacks[0].Clone();
        base.UpdateBackend(merged);
    }
    public override void OnOpen()
    {
        base.OnOpen();
        SetSlots(WorkstationData.TileEntity.Input);
        nextPoll=0;
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        if(windowGroup?.isShowing!=true || Time.unscaledTime<nextPoll)return;
        nextPoll=Time.unscaledTime+.25f;
        var station=WorkstationData?.TileEntity;
        if(!RebirthRecyclerRuntime.Supports(station))return;
        var input=station.Input;
        if(input==null || input.Length==0 || input[0]==null || input[0].IsEmpty()) { GateReason=null;return; }
        var actor=xui.playerUI.entityPlayer;
        if(!RebirthRecyclerRuntime.CanQueue(actor,station,0,input[0].count,out var reason)) { GateReason=reason;return; }
        GateReason=null;
        if(RebirthRecyclerRuntime.TryQueue(actor,station,0,input[0].count,Guid.NewGuid(),
            ()=>windowGroup?.isShowing==true && ReferenceEquals(WorkstationData?.TileEntity,station),out reason))
        {
            // Refresh from authoritative state; never write the pre-queue grid back.
            SetSlots(station.Input);
            windowGroup.Controller.SetAllChildrenDirty();
        }
        else GateReason=reason;
    }
}

[Preserve]
public sealed class XUiC_RebirthRecyclerPreview : XUiController
{
    private float nextPoll;
    private string yields="", gate="";
    public override void Update(float dt)
    {
        base.Update(dt);
        if(windowGroup?.isShowing!=true || Time.unscaledTime<nextPoll)return;
        nextPoll=Time.unscaledTime+.25f;
        var station=(windowGroup.Controller as XUiC_WorkstationWindowGroup)?.WorkstationData?.TileEntity;
        var input=station?.Input;
        var stack=input!=null && input.Length>0 ? input[0] : null;
        if(stack==null || stack.IsEmpty())stack=xui.DragAndDropWindow?.CurrentStack;
        string next="", why=GetChildByType<XUiC_RebirthRecyclerInputGrid>()?.GateReason;
        if(stack!=null && !stack.IsEmpty() && station!=null)
        {
            if(RebirthRecyclerRules.TryResolve(stack.itemValue.type,station.block.GetBlockName(),out var rule,out var reason))
            {
                var text=new StringBuilder();
                foreach(var output in rule.Outputs)
                {
                    var item=output.Stack.itemValue.ItemClass;
                    text.Append((long)output.Stack.count*stack.count).Append(" × ").Append(Localization.Get(item.GetItemName()))
                        .Append(" (").Append((output.TimeSeconds*stack.count).ToString("0.##")).Append("s)\n");
                }
                next=text.ToString();
                if(!RebirthRecyclerRuntime.CanAccept(xui.playerUI.entityPlayer,station,stack,out var admissionReason))why=admissionReason;
                if(why=="xuiScrapToolRequired")
                {
                    RebirthRecyclerRules.ToolsHealthy(station.Tools,rule,out var missing,out _);
                    var names=new System.Collections.Generic.List<string>();
                    foreach(var name in missing)names.Add(Localization.Get(name));
                    why=string.Format(Localization.Get("xuiScrapToolRequired"),string.Join(", ",names));
                }
            }
            else why=reason; // Preserve unavailable-reference versus no matching rule.
        }
        string translated=string.IsNullOrEmpty(why)?Localization.Get("xuiRebirthRecyclingDrop"):Localization.Get(why);
        if(next!=yields || translated!=gate){yields=next;gate=translated;RefreshBindings();}
    }
    public override bool GetBindingValueInternal(ref string value,string bindingName)
    {
        if(bindingName=="recycler_yield"){value=yields;return true;}
        if(bindingName=="recycler_gate"){value=gate;return true;}
        return base.GetBindingValueInternal(ref value,bindingName);
    }
}

[HarmonyLib.HarmonyPatch(typeof(XUiC_WorkstationInputGrid), nameof(XUiC_WorkstationInputGrid.AcceptsMaterial))]
internal static class RebirthRecyclerShiftAdmission
{
    private static bool Prefix(XUiC_WorkstationInputGrid __instance,ref bool __result)
    {
        if(!(__instance is XUiC_RebirthRecyclerInputGrid))return true;
        __result=true;return false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(XUiC_RebirthStationWorkspace), nameof(XUiC_RebirthStationWorkspace.OnOpen))]
internal static class RebirthRecyclerToolPresentation
{
    private static void Postfix(XUiC_RebirthStationWorkspace __instance)
    {
        var station=__instance.WorkstationData?.TileEntity;
        if(!RebirthRecyclerRuntime.Supports(station))return;
        var grid=__instance.GetChildByType<XUiC_WorkstationToolGrid>();
        if(grid==null)return;
        string names;
        switch(station.block.GetBlockName())
        {
            case "SonjaRecyclerStation":names="carBattery,FuriousRamsayHammerPliers,FuriousRamsayScrewdriver";break;
            case "SonjaAmmoRecyclerStation":names="FuriousRamsayPliers";break;
            case "WorkbenchShredder001_FR":names="FuriousRamsayPropaneTank";break;
            default:names="FuriousRamsayArmorCrafting,FuriousRamsayScrewdriver";break;
        }
        grid.requiredTools=names;
        var tools=names.Split(',');var cells=grid.GetItemStackControllers();
        for(int i=0;i<cells.Length;i++)if(cells[i] is XUiC_RequiredItemStack cell)
        {
            if(i<tools.Length && ItemClass.GetItemClass(tools[i]) is ItemClass item)cell.SetAllowedItemClassSingle(item);
            else cell.ClearAllowedItemClasses();
            cell.RequiredType=XUiC_RequiredItemStack.RequiredTypes.ItemClass;cell.RequiredItemOnly=true;cell.IsDirty=true;
        }
        new RebirthPersonalCraftingLayoutService(__instance.Presentation).Apply(true);
        var title=__instance.GetChildById("stationName")?.ViewComponent as XUiV_Label;
        if(title!=null)title.Text=Localization.Get(station.block.GetBlockName());
        var icon=__instance.GetChildById("stationIcon")?.ViewComponent as XUiV_Sprite;
        if(icon!=null)icon.SpriteName=ItemClass.GetItem(station.block.GetBlockName()).ItemClass?.GetIconName()??"workbench";
    }
}

[Preserve]
public sealed class RebirthRecyclerContextAction : BaseItemActionEntry
{
    private readonly XUiC_ItemStack source;
    private readonly XUiC_RebirthRecyclerInputGrid grid;
    public RebirthRecyclerContextAction(XUiC_ItemStack source,XUiC_RebirthRecyclerInputGrid grid)
        :base(source,"lblContextActionScrap","ui_game_symbol_scrap",GamepadShortCut.DPadRight)
    {this.source=source;this.grid=grid;SoundName="ui_craft_start";}
    public override void RefreshEnabled(){Enabled=grid.Accepts(source.ItemStack,out _);}
    public override void OnActivated(){if(grid.Accepts(source.ItemStack,out _))source.HandleMoveToPreferredLocation();}
    public override void OnDisabledActivate(){if(!grid.Accepts(source.ItemStack,out var reason))GameManager.ShowTooltip(source.xui.playerUI.entityPlayer,Localization.Get(reason));}
}

[HarmonyLib.HarmonyPatch(typeof(XUiC_ItemActionList), nameof(XUiC_ItemActionList.SetCraftingActionList))]
internal static class RebirthRecyclerContextActions
{
    private static void Postfix(XUiC_ItemActionList __instance,XUiC_ItemActionList.ItemActionListTypes _actionListType,XUiController itemController)
    {
        if(_actionListType!=XUiC_ItemActionList.ItemActionListTypes.Item)return;
        var owner=XUiC_RebirthStationWorkspace.ActiveInstance;
        var grid=owner?.GetChildByType<XUiC_RebirthRecyclerInputGrid>();
        if(grid==null || !ReferenceEquals(owner.xui,__instance.xui) || !(itemController is XUiC_ItemStack source) || source.ItemStack==null || source.ItemStack.IsEmpty())return;
        if(!RebirthRecyclerRules.TryResolve(source.ItemStack.itemValue.type,owner.WorkstationData.TileEntity.block.GetBlockName(),out _))return;
        __instance.itemActionEntries.RemoveAll(entry=>entry is ItemActionEntryScrap);
        __instance.itemActionEntries.Add(new RebirthRecyclerContextAction(source,grid));__instance.IsDirty=true;
    }
}

[Preserve]
public sealed class XUiC_RebirthRecyclerInputSlot : XUiC_ItemStack
{
    public override void Update(float dt)
    {
        var cursor=xui.DragAndDropWindow?.CurrentStack;
        var grid=GetParentByType<XUiC_RebirthRecyclerInputGrid>();
        bool accept=cursor==null || cursor.IsEmpty() || grid==null || grid.Accepts(cursor,out _);
        ViewComponent.EventOnPress=accept;
        base.Update(dt);
    }
}

[HarmonyLib.HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.HandleMoveToPreferredLocation))]
internal static class RebirthRecyclerPreferredRouting
{
    private static bool Prefix(XUiC_ItemStack __instance)
    {
        var owner=XUiC_RebirthStationWorkspace.ActiveInstance;
        var grid=owner?.GetChildByType<XUiC_RebirthRecyclerInputGrid>();
        if(grid==null || !ReferenceEquals(__instance.xui,owner.xui) ||
            __instance.StackLocation!=XUiC_ItemStack.StackLocationTypes.Backpack && __instance.StackLocation!=XUiC_ItemStack.StackLocationTypes.ToolBelt)return true;
        var tools=owner.GetChildByType<XUiC_WorkstationToolGrid>();
        if(tools!=null)foreach(var cell in tools.GetItemStackControllers())
            if(cell is XUiC_RequiredItemStack required && required.allowedItemClasses.Contains(__instance.ItemStack?.itemValue?.ItemClass))return true;
        if(grid.Accepts(__instance.ItemStack,out var reason))return true;
        GameManager.ShowTooltip(__instance.xui.playerUI.entityPlayer,Localization.Get(reason));return false;
    }
}

