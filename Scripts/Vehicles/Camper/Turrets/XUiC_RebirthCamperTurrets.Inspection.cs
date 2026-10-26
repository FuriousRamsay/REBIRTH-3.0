using System;
using UnityEngine;

// Same persistent selected-item presentation and native stat/description resolution
// as XUiC_RebirthCamperModules.Inspect (storage maintenance).
public sealed partial class XUiC_RebirthCamperTurrets
{
    public void Inspect(XUiC_ItemStack slot)
    {
        var stack=slot?.ItemStack;
        var actions=GetChildById("camperTurretActions") as XUiC_ItemActionList;
        if(actions!=null)
        {
            actions.SetCraftingActionList(stack!=null&&!stack.IsEmpty()
                ?XUiC_ItemActionList.ItemActionListTypes.Item
                :XUiC_ItemActionList.ItemActionListTypes.None,slot);
            if(slot!=null&&stack!=null&&!stack.IsEmpty())RebirthEditorActions.Adapt(actions,slot);
        }
        Inspect(stack);
    }
    public void Inspect(ItemStack stack)
    {
        bool valid = stack != null && !stack.IsEmpty();
        var name = GetChildById("camperInspectName")?.ViewComponent as XUiV_Label;
        var description = GetChildById("camperInspectDescription")?.ViewComponent as XUiV_Label;
        var icon = GetChildById("camperInspectIcon")?.ViewComponent as XUiV_Sprite;
        var capacityLabel=GetChildById("camperInspectCapacity")?.ViewComponent as XUiV_Label;
        if(capacityLabel!=null)capacityLabel.Text=string.Empty;
        if (name != null) name.Text = valid ? Localization.Get(stack.itemValue.ItemClass.GetItemName()) : string.Empty;
        if (description != null)
        {
            description.Text = valid ? XUiC_RebirthCraftingItemContext.ResolveDescription(stack, xui) : string.Empty;
            // Same native/functional-container lookup as installation, using
            // the exact selected stack rather than the camper's total cargo.
            var single=valid?stack.Clone():null;if(single!=null)single.count=1;
            if(valid&&RebirthCamperStorageContainer.TryGetCapacity(single,out int selectedCapacity))
            {
                const string key="xuiRebirthCamperStorageContainerCapacityFormat";string format=Localization.Get(key);
                if(string.IsNullOrEmpty(format)||format==key)format="Container capacity: {0} slots";
                // Exact value color from xuiRebirthEstimatedStackSale; retain the
                // caption's purple and include the units in the yellow value.
                if(capacityLabel!=null)capacityLabel.Text="[B58CFF]"+string.Format(format.Replace("{0}","[-][D6C978]{0}"),selectedCapacity)+"[-]";
                else description.Text += "\n\n"+string.Format(format,selectedCapacity);
            }
        }
        if (icon != null) { icon.IsVisible = valid; if (valid) { icon.SpriteName = stack.itemValue.GetPropertyOverride(ItemClass.PropCustomIcon, stack.itemValue.ItemClass.GetIconName()); icon.Color = stack.itemValue.ItemClass.GetIconTint(stack.itemValue); } }
        var count=GetChildById("camperSelectedCount")?.ViewComponent as XUiV_Label;
        if(count!=null)count.Text=valid&&stack.count>1?stack.count.ToString():string.Empty;
        var durability=GetChildById("camperSelectedDurability");
        bool quality=valid&&stack.itemValue.Quality>0;
        bool durable=valid&&stack.itemValue.MaxUseTimes>0;
        if(durability?.ViewComponent!=null)durability.ViewComponent.IsVisible=quality||durable;
        var fill=durability?.GetChildById("fill")?.ViewComponent as XUiV_FilledSprite;
        if(fill!=null){fill.Fill=durable?UnityEngine.Mathf.Clamp01((stack.itemValue.MaxUseTimes-stack.itemValue.UseTimes)/(float)stack.itemValue.MaxUseTimes):1f;fill.Color=quality?(UnityEngine.Color)QualityInfo.GetQualityColor(stack.itemValue.Quality):UnityEngine.Color.white;}
        var number=durability?.GetChildById("qualityNumber")?.ViewComponent as XUiV_Label;
        if(number!=null)number.Text=quality?stack.itemValue.Quality.ToString():string.Empty;
        var remaining=GetChildById("camperSelectedDurabilityCount")?.ViewComponent as XUiV_Label;
        if(remaining!=null){remaining.IsVisible=false;remaining.Text=string.Empty;}
        var item=valid?stack.itemValue.ItemClass:null;
        var display=item==null?null:UIDisplayInfoManager.Current.GetDisplayStatsForTag(item.IsBlock()?Block.list[stack.itemValue.type].DisplayType:item.DisplayType);
        for(int index=0;index<7;index++)
        {
            var title=GetChildById("camperStatName"+index)?.ViewComponent as XUiV_Label;
            var value=GetChildById("camperStatValue"+index)?.ViewComponent as XUiV_Label;
            var stat=display!=null&&index<display.DisplayStats.Count?display.DisplayStats[index]:null;
            if(title!=null)title.Text=stat==null?string.Empty:stat.TitleOverride??UIDisplayInfoManager.Current.GetLocalizedName(stat.StatType);
            if(value!=null)value.Text=stat==null?string.Empty:RebirthItemStatColors.Format(RebirthItemStatColors.NativeValue(stack,xui.playerUI.entityPlayer,stat));
        }
    }
}
