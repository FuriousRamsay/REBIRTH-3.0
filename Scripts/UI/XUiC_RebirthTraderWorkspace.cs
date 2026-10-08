using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthTraderWorkspace : XUiC_TraderWindowGroup
{
    public XUiC_ItemInfoWindow Details { get; private set; }
    private XUiC_InfoWindow empty;
    private XUiV_Label wallet;
    private readonly RebirthWindowHudScope hud = new RebirthWindowHudScope();
    private float nextRefresh;
    private int lastMoney = -1;
    private RebirthTraderSaleStash stashSales;
    private XUiController stashSell;
    private XUiV_Label stashTotal;
    private XUiView stashCoin;
    private float nextStashRequest;
    public override void Init()
    {
        base.Init();
        Details=GetChildById("traderItemInfo") as XUiC_ItemInfoWindow;
        empty=GetChildById("traderEmpty") as XUiC_InfoWindow;
        Details.emptyInfoWindow=empty;
        GetChildByType<XUiC_TraderItemList>().InfoWindow=Details;
        wallet=GetChildById("traderWallet")?.ViewComponent as XUiV_Label;
        stashSales=new RebirthTraderSaleStash(this);
        stashCoin=GetChildById("sellStashCoin")?.ViewComponent;
        stashSell=GetChildById("sellBackpackAll");stashTotal=GetChildById("sellBackpackTotal")?.ViewComponent as XUiV_Label;
        if(stashSell!=null)stashSell.OnPress+=(sender,button)=>{if(button==0||button==-1)stashSales.Start();};
    }
    public override void OnOpen()
    {
        Details.emptyInfoWindow=empty;
        base.OnOpen();
        GetChildByType<XUiC_RebirthCraftingTopTabs>()?.SetActiveDestination((RebirthCraftingNavigationService.Destination)(-1));
        Details.ViewComponent.IsVisible=false;
        empty.ViewComponent.IsVisible=true;
        nextRefresh=0;lastMoney=-1;
        xui.playerUI.windowManager.Open("toolbelt",false);
        xui.playerUI.windowManager.Open("dragAndDrop",false);
        hud.Maintain(xui);
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        if(!windowGroup.isShowing || Time.realtimeSinceStartup<nextRefresh)return;
        nextRefresh=Time.realtimeSinceStartup+0.25f;
        hud.Maintain(xui);
        stashSales.Advance();
        var player=xui.playerUI?.entityPlayer;
        if(player?.world!=null&&Time.realtimeSinceStartup>=nextStashRequest){nextStashRequest=Time.realtimeSinceStartup+1f;RebirthBackpackSellStashClientViews.Request(player.world,player.entityId);}
        RebirthBackpackSellStashView stash=null;
        if(player?.world!=null)RebirthBackpackSellStashClientViews.TryGet(player.world,player.entityId,out stash);
        bool equipped=RebirthBackpackSectionProjectionPolicy.Matches(player,stash)&&stash.Capacity>0;
        if(stashCoin!=null)stashCoin.IsVisible=equipped;
        var heading=GetChildById("sellBackpackHeading")?.ViewComponent;if(heading!=null)heading.IsVisible=equipped;
        if(stashTotal!=null){stashTotal.IsVisible=equipped;if(equipped)stashTotal.SetTextImmediately(RebirthBackpackSaleQuote.Format(xui,stash));}
        if(stashSell?.ViewComponent!=null){stashSell.ViewComponent.IsVisible=equipped;stashSell.ViewComponent.Enabled=equipped&&!stash.TransferPending&&!stashSales.Running&&stash.OccupiedSlots>0;}
        int money=RebirthSkillWaveABuyPatch.CurrencyCount(xui);
        if(wallet!=null && money!=lastMoney){lastMoney=money;wallet.Text=string.Format(Localization.Get("rebirthTraderWallet"),money);}
    }
    public override void OnClose(){stashSales?.Cancel();hud.Restore();base.OnClose();}
}

[Preserve]
public sealed class XUiC_RebirthTraderDetails : XUiC_ItemInfoWindow
{
    private XUiController quantitySource;
    private int quantityItemType = -1;
    public override void OnOpen()
    {
        quantitySource = null;
        quantityItemType = -1;
        base.OnOpen();
    }
    public bool IsNewQuantitySelection(XUiController source, ItemStack stack)
    {
        int type = stack?.itemValue?.type ?? -1;
        bool changed = quantitySource != source || quantityItemType != type;
        quantitySource = source;
        quantityItemType = type;
        return changed;
    }
    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        if(bindingName=="rebirthtradertransactiontotal")
        {
            value=string.Empty;
            if(itemStack==null||itemStack.IsEmpty()||BuySellCounter==null)return true;
            bool sellable=itemStack.itemValue.ItemClass.IsBlock()
                ?Block.list[itemStack.itemValue.type].SellableToTrader
                :itemStack.itemValue.ItemClass.SellableToTrader;
            if(!isBuying&&!sellable){value=Localization.Get("xuiNoSellPrice");return true;}
            int count=Math.Max(0,BuySellCounter.Count);
            int total=isBuying
                ?(useCustomMarkup
                    ?XUiM_Trader.GetBuyPrice(xui,itemStack.itemValue,count,itemStack.itemValue.ItemClass,selectedTraderItemStack?.SlotIndex??-1)
                    :XUiM_Trader.GetBuyPrice(xui,itemStack.itemValue,count,itemStack.itemValue.ItemClass))
                :XUiM_Trader.GetSellPrice(xui,itemStack.itemValue,count,itemStack.itemValue.ItemClass);
            value=!isBuying&&total<=0?Localization.Get("xuiNoSellPrice"):itemcostFormatter.Format(total);
            return true;
        }
        if (bindingName == "rebirthtraderhasmods")
        {
            bool hasMods = false;
            var mods = itemStack?.itemValue?.modifications;
            if (mods != null) foreach (var mod in mods) if (mod != null && !mod.IsEmpty()) { hasMods = true; break; }
            var cosmetics = itemStack?.itemValue?.cosmeticMods;
            if (cosmetics != null) foreach (var mod in cosmetics) if (mod != null && !mod.IsEmpty()) { hasMods = true; break; }
            value = (hasMods && CompareStack.IsEmpty()).ToString();
            return true;
        }
        if (bindingName == "rebirthtradertotallabel")
        {
            value = Localization.Get(isBuying ? "rebirthTraderBuyTotal" : "rebirthTraderSellTotal");
            return true;
        }
        return base.GetBindingValueInternal(ref value, bindingName);
    }
}

// A new sell selection defaults to its maximum tradable amount; purchases retain
// the minimum bundle. Keep manual quantity choices while inspecting the same selection.
[HarmonyPatch(typeof(XUiC_ItemInfoWindow), nameof(XUiC_ItemInfoWindow.SetInfo))]
internal static class RebirthTraderQuantitySelectionPatch
{
    private static void Prefix(XUiC_ItemInfoWindow __instance, ItemStack stack, XUiController controller, out bool __state)
    {
        __state = false;
        if (!(__instance is XUiC_RebirthTraderDetails details)) return;
        bool changed = details.IsNewQuantitySelection(controller, stack);
        __state = changed || details.SetMaxCountOnDirty;
        details.SetMaxCountOnDirty = false;
    }
    private static void Postfix(XUiC_ItemInfoWindow __instance, bool __state)
    {
        if (!__state || !(__instance is XUiC_RebirthTraderDetails)) return;
        var counter = __instance.BuySellCounter;
        if (counter == null) return;
        counter.SetCount(__instance.isBuying
            ? Math.Min(Math.Max(1, counter.Step), Math.Max(0, counter.MaxCount))
            : Math.Max(0, counter.MaxCount));
        if (counter.Count > 0) counter.ForceTextRefresh();
        __instance.RefreshBindings();
    }
}

[Preserve]
public sealed class XUiC_RebirthTraderQuantity : XUiC_Counter
{
    public override void Init()
    {
        base.Init();
        GetChildById("countMin").OnPress += OnMinimum;
    }
    private void OnMinimum(XUiController sender, int mouseButton)
    {
        SetCount(Math.Min(Math.Max(1, Step), Math.Max(0, MaxCount)));
        if (Count > 0) ForceTextRefresh();
    }
}

[Preserve]
public sealed class XUiC_RebirthTraderStockEntry : XUiC_TraderItemEntry
{
    public override void Update(float dt)
    {
        base.Update(dt);
        var color = IsSelected ? new Color32(255,255,255,255) : isHovered ? new Color32(55,55,64,255) : new Color32(25,25,32,220);
        if (background != null && !background.Color.Equals(color)) background.Color = color;
    }
}

[Preserve]
public sealed class XUiC_RebirthTraderActionEntry : XUiC_ItemActionEntry
{
    public override void Update(float dt)
    {
        base.Update(dt);
        bool visible = ItemActionEntry != null;
        if (ViewComponent.IsVisible != visible) ViewComponent.IsVisible = visible;
        if (background != null && !isOver && background.SpriteName != "menu_empty2px") background.SpriteName = "menu_empty2px";
        if (keyboardButton != null)
        {
            int buttonWidth = ViewComponent.Size.x;
            keyboardButton.Position = new Vector2i(buttonWidth - 48, keyboardButton.Position.y);
            keyboardButton.Size = new Vector2i(32, keyboardButton.Size.y);
            keyboardButton.TryUpdatePosition();
            string text = keyboardButton.Text;
            if (!string.IsNullOrEmpty(text) && text[0] == '<') keyboardButton.Text = text.Trim('<','>');
        }
    }
}

[Preserve]
public sealed class XUiC_RebirthTraderSurface : XUiController
{
    private Vector2i lastScreen=new Vector2i(-1,-1);
    public override void OnOpen(){base.OnOpen();lastScreen=new Vector2i(-1,-1);Layout();}
    public override void Update(float dt){base.Update(dt);Layout();}
    private void Layout()
    {
        var screen=xui.GetXUiScreenSize();
        if(screen.x==lastScreen.x && screen.y==lastScreen.y)return;
        lastScreen=screen;
        Vector2i p,s;RebirthScreenLayout.GetScreenBounds(xui,out p,out s);
        Set(this,p,s);Set(GetChildById("traderOuterFrame"),Vector2i.zero,s);
        Set(GetChildById("rebirthCraftingTopZone"),new Vector2i(8,-8),new Vector2i(s.x-16,52));
        Set(GetChildById("rebirthCraftingTopBackground"),Vector2i.zero,new Vector2i(s.x-16,52));
        Set(GetChildById("rebirthCraftingTopFrame"),Vector2i.zero,new Vector2i(s.x-16,52));
        RebirthPersonalCraftingLayoutService.ApplySharedTopLayout(this,s.x-16);
        float scale=Math.Min((s.x-16)/1856f,Math.Max(1,s.y-78)/849f);
        var body=GetChildById("traderBody");
        Set(body,new Vector2i((s.x-Mathf.RoundToInt(1856*scale))/2,-70),new Vector2i(1856,849));
        body.ViewComponent.UiTransform.localScale=new Vector3(scale,scale,1);
    }
    private static void Set(XUiController c,Vector2i p,Vector2i s){if(c?.ViewComponent==null)return;c.ViewComponent.Position=p;c.ViewComponent.Size=s;c.ViewComponent.TryUpdatePosition();}
}

// Native list repopulation resolves the global item-info window. Scope that binding to this workspace.
[HarmonyPatch(typeof(XUiC_TraderItemList), nameof(XUiC_TraderItemList.SetItems))]
internal static class RebirthTraderWorkspaceStockPatch
{
    private static void Postfix(XUiC_TraderItemList __instance)
    {
        var owner=__instance.windowGroup?.Controller as XUiC_RebirthTraderWorkspace;
        if(owner?.Details==null)return;
        __instance.InfoWindow=owner.Details;
        foreach(var row in __instance.entryList)row.InfoWindow=owner.Details;
    }
}

[Preserve]
public sealed class XUiC_RebirthTraderInventorySlot : XUiC_RebirthCraftingInventorySlot
{
    private XUiC_RebirthTraderWorkspace owner;
    public override void Init(){base.Init();owner=windowGroup.Controller as XUiC_RebirthTraderWorkspace;}
    public override void Update(float dt){Bind();base.Update(dt);}
    private void Bind(){if(owner?.Details!=null)InfoWindow=owner.Details;}
    public override void updateItemInfoWindow(XUiC_ItemStack slot){Bind();InfoWindow?.SetItemStack(slot,true);}
    public override void HandleClickComplete(){Bind();base.HandleClickComplete();}
}
