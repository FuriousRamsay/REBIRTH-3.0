using System;
using System.Collections.Generic;

// Shared selected-item stat policy used by container and companion inspectors.
// Native display definitions, consumable rows, comparison and weapon rows remain
// the authority; a window only chooses where the resulting rows are painted.
internal static class RebirthSelectedItemRows
{
    internal static void Build(XUi xui,ItemStack stack,ItemStack comparison,ItemStack nativeComparison,int count,string[] names,string[] values)
    {
        if(names==null||values==null||names.Length<count||values.Length<count)throw new ArgumentException("Selected item row buffers");
        List<RebirthConsumableItemPresentation.Row> consumableRows;
        bool consumable=RebirthConsumableItemPresentation.TryBuild(stack,comparison,xui,out consumableRows,true);
        var display=!consumable&&stack!=null&&!stack.IsEmpty()
            ?UIDisplayInfoManager.Current.GetDisplayStatsForTag(stack.itemValue.ItemClass.DisplayType):null;
        int nativeIndex=0;
        bool nativeCompare=display!=null&&nativeComparison!=null&&!nativeComparison.IsEmpty()&&
            XUiM_ItemStack.CanCompare(stack.itemValue.ItemClass,nativeComparison.itemValue.ItemClass);
        bool weapon=!consumable&&RebirthWeaponDetailRows.TryGet(xui,stack,nativeCompare?nativeComparison:null,0,out _,out _);
        for(int i=0;i<count;i++)
        {
            string titleText="",valueText="";
            if(weapon)RebirthWeaponDetailRows.TryGet(xui,stack,nativeCompare?nativeComparison:null,i,out titleText,out valueText);
            else if(consumable)
            {
                if(i<consumableRows.Count){titleText=consumableRows[i].Title;valueText=consumableRows[i].Value;}
            }
            else if(display!=null)
            {
                while(nativeIndex<display.DisplayStats.Count)
                {
                    var stat=display.DisplayStats[nativeIndex++];
                    if(!RebirthConsumableItemPresentation.ShouldShowNativeStat(stat,stack.itemValue,
                        nativeCompare?nativeComparison.itemValue:null,xui))continue;
                    titleText=stat.TitleOverride??UIDisplayInfoManager.Current.GetLocalizedName(stat.StatType);
                    valueText=nativeCompare
                        ?XUiM_ItemStack.GetStatItemValueTextWithCompareInfo(stack.itemValue,nativeComparison.itemValue,xui.playerUI.entityPlayer,stat)
                        :RebirthItemStatColors.NativeValue(stack,xui.playerUI.entityPlayer,stat);
                    valueText=RebirthItemStatColors.Format(valueText);
                    break;
                }
            }
            names[i]=titleText;values[i]=valueText;
        }
    }
}

