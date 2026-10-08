using System;
using System.Globalization;
// Shared seven-row selected-item policy, preserving native secondary rows and comparison formatting.
internal static class RebirthWeaponDetailRows
{
    private static string Number(float value)=>value.ToString("0.#",CultureInfo.InvariantCulture);
    private static string Pair(float normal,float power)=>Number(normal)+" ("+Number(power)+")";
    internal static bool TryGet(XUi ui,ItemStack stack,ItemStack comparison,int index,out string title,out string value)
    {
        title=value="";
        if(stack==null||stack.IsEmpty()||index<0||index>=7||
            !RebirthWeaponSustainedDpsService.TryGetDisplayProfile(ui?.playerUI?.entityPlayer,stack.itemValue,out var profile)||profile?.Valid!=true)return false;
        var rows=UIDisplayInfoManager.Current.GetDisplayStatsForTag(stack.itemValue.ItemClass.DisplayType)?.DisplayStats;
        int visible=-1,source=-1;
        if(rows!=null)for(int i=0;i<rows.Count;i++)
        {
            var stat=rows[i];
            // The native localized title remains authoritative; compare its localization key's value.
            string label=stat.TitleOverride??UIDisplayInfoManager.Current.GetLocalizedName(stat.StatType);
            if(profile.Melee&&(string.Equals(label,Localization.Get("statPowerAttack"),StringComparison.Ordinal)||
                label.IndexOf("Power Attack Damage",StringComparison.OrdinalIgnoreCase)>=0))continue;
            if(++visible==index){source=i;break;}
        }
        if(source<0)return true;
        var entry=rows[source];
        title=entry.TitleOverride??UIDisplayInfoManager.Current.GetLocalizedName(entry.StatType);
        bool compare=comparison!=null&&!comparison.IsEmpty()&&XUiM_ItemStack.CanCompare(stack.itemValue.ItemClass,comparison.itemValue.ItemClass);
        value=compare?XUiM_ItemStack.GetStatItemValueTextWithCompareInfo(stack.itemValue,comparison.itemValue,ui.playerUI.entityPlayer,entry):
            XUiM_ItemStack.GetStatItemValueTextWithModInfo(stack,ui.playerUI.entityPlayer,entry);
        if(index==0){title=profile.Melee?"Melee DPS":"Ranged DPS";value=profile.Melee?Pair(profile.NormalAttackDps,profile.PowerAttackDps):Number(profile.SustainedDps);}
        else if(profile.Melee&&entry.StatType.ToString()=="BlockDamage")value=Pair(profile.NormalBlockDamagePerAttack,profile.PowerBlockDamagePerAttack);
        else if(profile.Melee&&entry.StatType.ToString()=="StaminaLoss")value=Pair(profile.NormalStaminaCost,profile.PowerStaminaCost);
        value=RebirthItemStatColors.Format(value);return true;
    }
}