using System;

// Same native rows and presentation policy as Storage; no alternate item-stat calculation.
internal static class RebirthBackpackSectionStats
{
    internal static bool Render(XUiController owner,ItemStack stack,string prefix)
    {
        bool has=stack!=null&&!stack.IsEmpty();
        System.Collections.Generic.List<RebirthConsumableItemPresentation.Row> rows=null;
        bool consumable=has&&RebirthConsumableItemPresentation.TryBuild(stack,null,owner.xui,out rows,true);
        if(!has)rows=null;
        var display=has&&!consumable?UIDisplayInfoManager.Current.GetDisplayStatsForTag(stack.itemValue.ItemClass.DisplayType):null;
        bool hasStats=consumable?rows.Count>0:display?.DisplayStats?.Count>0;
        RebirthWeaponSustainedDpsService.Profile weapon=null;
        if(has)RebirthWeaponSustainedDpsService.TryGetDisplayProfile(owner.xui?.playerUI?.entityPlayer,stack.itemValue,out weapon);
        for(int i=0;i<7;i++)
        {
            string title="",value="";
            if(consumable&&i<rows.Count){title=rows[i].Title;value=rows[i].Value;}
            else if(display!=null&&i<display.DisplayStats.Count)
            {
                var stat=display.DisplayStats[i];title=stat.TitleOverride??UIDisplayInfoManager.Current.GetLocalizedName(stat.StatType);
                value=RebirthItemStatColors.Format(XUiM_ItemStack.GetStatItemValueTextWithModInfo(stack,owner.xui.playerUI.entityPlayer,stat));
            }
            if(RebirthWeaponDetailRows.TryGet(owner.xui,stack,null,i,out var weaponTitle,out var weaponText)){title=weaponTitle;value=weaponText;}
            (owner.GetChildById(prefix+"StatName"+i)?.ViewComponent as XUiV_Label)?.SetTextImmediately(title);
            (owner.GetChildById(prefix+"StatValue"+i)?.ViewComponent as XUiV_Label)?.SetTextImmediately(value);
        }
        return hasStats;
    }
}