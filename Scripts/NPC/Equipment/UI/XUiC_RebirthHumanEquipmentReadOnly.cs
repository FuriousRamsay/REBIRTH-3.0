using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine.Scripting;

// Pre-hire inspection uses the same native ItemStack view as the other REBIRTH
// inventories, but never exposes a mutation controller or inventory gesture.
[Preserve]
public sealed class XUiC_RebirthHumanEquipmentReadOnly : XUiController
{
    private static readonly HashSet<XUiC_RebirthHumanEquipmentReadOnly> active = new HashSet<XUiC_RebirthHumanEquipmentReadOnly>();
    private readonly ItemStack[] gear = new ItemStack[11];
    private readonly ItemStack[] bag = new ItemStack[169];
    private RebirthNpcStableId stable;
    private int entity = -1, capacity, scrollRow;
    private bool equipment, loaded, pending;
    private float retryAt;
    private string token;
    private XUiV_Label summary,selectedName,selectedCount,selectedSummary,selectedDescription;
    private XUiV_Sprite selectedIcon;
    private XUiC_RebirthReadonlyItemSlot selectedNative;
    private readonly XUiV_Label[] statNames=new XUiV_Label[7],statValues=new XUiV_Label[7];

    public override void Init()
    {
        base.Init();
        summary = GetChildById("hireNativeSummary")?.ViewComponent as XUiV_Label;
        selectedName=GetChildById("hireNativeSelectedName")?.ViewComponent as XUiV_Label;
        selectedCount=GetChildById("hireNativeSelectedCount")?.ViewComponent as XUiV_Label;
        selectedSummary=GetChildById("hireNativeSelectedSummary")?.ViewComponent as XUiV_Label;
        selectedDescription=GetChildById("hireNativeSelectedDescription")?.ViewComponent as XUiV_Label;
        selectedIcon=GetChildById("hireNativeSelectedIcon")?.ViewComponent as XUiV_Sprite;
        selectedNative=GetChildById("hireNativeSelectedMirror") as XUiC_RebirthReadonlyItemSlot;
        for(int i=0;i<statNames.Length;i++)
        {statNames[i]=GetChildById("hireNativeStatName"+i)?.ViewComponent as XUiV_Label;
            statValues[i]=GetChildById("hireNativeStatValue"+i)?.ViewComponent as XUiV_Label;}
        var grid=GetChildById("hireNativeGrid");
        if(grid!=null)grid.OnScroll+=(sender,delta)=>
        {
            if(equipment)return;
            scrollRow=Math.Max(0,Math.Min(Math.Max(0,(capacity-1)/13-8),scrollRow+(delta<0?1:-1)));
            Render();
        };
        for(int i=0;i<11;i++)
        {
            int index=i;var slot=GetChildById("hireGearSlot"+i);
            if(slot!=null)
            {
                slot.OnPress+=(sender,button)=>{if(button==0||button==-1)Inspect(gear[index],slot.GetChildById("hireGearStack"+index) as XUiC_ItemStack);};
                slot.OnHover+=(sender,over)=>RebirthCharacterItemStatsTooltip.Hover(slot.GetChildById("hireGearStack"+index) as XUiC_ItemStack,over);
            }
        }
        for(int i=0;i<117;i++)
        {
            int index=i;var slot=GetChildById("hireInventorySlot"+i);
            if(slot!=null)slot.OnPress+=(sender,button)=>
            {
                if(button!=0&&button!=-1)return;
                int physical=scrollRow*13+index;
                if(physical>=0&&physical<capacity)Inspect(bag[physical],slot.GetChildById("hireInventoryStack"+index) as XUiC_ItemStack);
            };
            if(slot!=null)slot.OnHover+=(sender,over)=>RebirthCharacterItemStatsTooltip.Hover(slot.GetChildById("hireInventoryStack"+index) as XUiC_ItemStack,over);
        }
        for(int i=0;i<gear.Length;i++)gear[i]=ItemStack.Empty.Clone();
        for(int i=0;i<bag.Length;i++)bag[i]=ItemStack.Empty.Clone();
        active.Add(this);
    }

    public void PresentHire(RebirthNpcStableId id,int entityId,bool equipmentTab)
    {
        active.Add(this);
        bool changed=entityId!=entity||!id.Equals(stable)||equipment!=equipmentTab;
        stable=id;entity=entityId;equipment=equipmentTab;
        if(changed){loaded=false;pending=false;scrollRow=0;retryAt=0f;}
        // A lost reply or a rejected/undecodable snapshot is retried only after a pause, never every render.
        if(pending&&UnityEngine.Time.unscaledTime>=retryAt)pending=false;
        if(entityId<0||pending||loaded||UnityEngine.Time.unscaledTime<retryAt)return;
        foreach(var other in active)
        {
            if(ReferenceEquals(other,this)||other.entity!=entity||!other.stable.Equals(stable))continue;
            if(other.loaded)
            {
                Array.Copy(other.gear,gear,gear.Length);Array.Copy(other.bag,bag,bag.Length);
                capacity=other.capacity;loaded=true;Render();return;
            }
            if(other.pending){token=other.token;pending=true;retryAt=other.retryAt;return;}
        }
        var player=xui?.playerUI?.entityPlayer as EntityPlayerLocal;
        var npc=player?.world?.GetEntity(entityId) as EntityRebirthHumanoidNPC;
        if(player==null||npc==null||npc.RebirthRuntimeState?.StableId.Equals(id)!=true)return;
        token=Guid.NewGuid().ToString("N");pending=true;retryAt=UnityEngine.Time.unscaledTime+4f;
        if(!player.world.IsRemote())Receive(player.entityId,token,RebirthHumanEquipment.InspectHire(player,npc).ToString());
        else SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
            NetPackageManager.GetPackage<NetPackageRebirthHumanEquipmentRequest>()
                .Setup(player.entityId,entityId,3,-1,-1,token,"",id.ToString()));
    }

    public static void Receive(int player,string request,string payload)
    {
        foreach(var view in active.ToArray())
        {
            if(view?.xui?.playerUI?.entityPlayer?.entityId!=player||view.token!=request)continue;
            try{view.Accept(XElement.Parse(payload));}
            catch(Exception e){Log.Warning("[REBIRTH Hire Inventory] "+e.GetType().Name);view.pending=false;}
        }
    }

    private void Accept(XElement root)
    {
        pending=false;
        if(root.Attribute("error")!=null||(int?)root.Attribute("readonly")!=1||
            (int?)root.Attribute("entity")!=entity||(string)root.Attribute("stable")!=stable.ToString())return;
        foreach(var slot in root.Elements("slot"))
        {
            int index=(int)slot.Attribute("id");
            if(index==RebirthHumanEquipment.LockpicksSlot)index=10;
            if(index<0||index>=gear.Length)return;
            gear[index]=RebirthHumanEquipment.Decode(slot.Value);
        }
        foreach(var cell in root.Element("storage")?.Elements("cell")??Enumerable.Empty<XElement>())
        {
            int index=(int)cell.Attribute("id");
            if(index<0||index>=bag.Length)return;
            bag[index]=RebirthHumanEquipment.Decode(cell.Value);
        }
        capacity=Math.Max(0,Math.Min(bag.Length,(int?)root.Attribute("capacity")??0));
        loaded=true;
        Render();
    }

    // Read by the hire window for the Inventory tab label; valid once a snapshot has loaded.
    internal bool SnapshotLoaded=>loaded;
    internal int SnapshotCapacity=>capacity;
    internal int SnapshotUsed{get{int n=0;for(int i=0;i<capacity&&i<bag.Length;i++)if(bag[i]!=null&&!bag[i].IsEmpty())n++;return n;}}

    private void SetLegacyPane(bool on)
    {
        var pane=GetChildById("hireNativeSelected")?.ViewComponent;
        if(pane!=null&&pane.IsVisible!=on)pane.IsVisible=on;
    }
    private void Render()
    {
        if(!loaded)return;
        SetLegacyPane(!(windowGroup?.Controller?.GetChildByType<XUiC_ItemInfoWindow>()!=null&&RebirthHireInfoActionsPatch.Healthy));
        if(summary!=null)summary.Text=equipment?"":string.Format(Localization.Get("xuiRbNpcEquipmentStorageSummary"),
            bag.Take(capacity).Count(stack=>stack!=null&&!stack.IsEmpty()),capacity);
        if(equipment)
        {
            for(int i=0;i<gear.Length;i++)
            {
                var control=GetChildById("hireGearSlot"+i);
                var native=control?.GetChildById("hireGearStack"+i) as XUiC_RebirthReadonlyItemSlot;
                native?.Show(gear[i]);
            }
        }
        else for(int i=0;i<117;i++)
        {
            var control=GetChildById("hireInventorySlot"+i);
            int physical=scrollRow*13+i;
            if(control?.ViewComponent!=null)control.ViewComponent.IsVisible=physical<capacity;
            var native=control?.GetChildById("hireInventoryStack"+i) as XUiC_RebirthReadonlyItemSlot;
            native?.Show(physical<capacity?bag[physical]:ItemStack.Empty);
        }
    }

    private void Inspect(ItemStack stack,XUiC_ItemStack source)
    {
        // The standard itemInfoPanel is hosted in the hire window group; its actions are suppressed for hire stacks.
        var item=windowGroup?.Controller?.GetChildByType<XUiC_ItemInfoWindow>();
        bool shared=item!=null&&RebirthHireInfoActionsPatch.Healthy;
        SetLegacyPane(!shared);
        if(stack==null||stack.IsEmpty())
        {
            if(shared){item.ClearSelectedStacks();if(item.ViewComponent!=null)item.ViewComponent.IsVisible=false;}
            return;
        }
        if(shared){if(source!=null)item.SetItemStack(source,true);return;}
        if(item!=null&&source!=null)item.SetItemStack(source,true);
        selectedNative?.Show(stack,source);
        if(selectedIcon!=null)
        {
            var value=stack.itemValue;var itemClass=value.ItemClassOrMissing;
            selectedIcon.SpriteName=value.GetPropertyOverride(ItemClass.PropCustomIcon,itemClass.GetIconName());
            selectedIcon.Color=itemClass.GetIconTint(value);
        }
        if(selectedName!=null)selectedName.Text=Localization.Get(stack.itemValue.ItemClass.GetItemName());
        if(selectedCount!=null)selectedCount.Text=stack.count.ToString();
        if(selectedSummary!=null)selectedSummary.Text=XUiC_RebirthCraftingItemContext.BuildSummary(stack);
        if(selectedDescription!=null)selectedDescription.Text=XUiC_RebirthCraftingItemContext.ResolveDescription(stack,xui);
        string[] names=new string[statNames.Length],values=new string[statValues.Length];
        RebirthSelectedItemRows.Build(xui,stack,null,null,names.Length,names,values);
        for(int i=0;i<names.Length;i++)
        {if(statNames[i]!=null)statNames[i].Text=names[i];if(statValues[i]!=null)statValues[i].Text=values[i];}
        RebirthSelectedDurability.Render(this,"hireNativeSelectedDurability",source??selectedNative);
    }

    public override void OnClose()
    {
        active.Remove(this);
        // A reopened hire popup must request a fresh snapshot; a reply still in flight is dropped by token.
        loaded=false;pending=false;entity=-1;token=null;retryAt=0f;scrollRow=0;
        base.OnClose();
    }
}
