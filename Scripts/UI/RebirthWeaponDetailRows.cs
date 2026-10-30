using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

// Seven-slot selected summaries and complete hover cards share meanings/values;
// the hover card is free to request more rows than the summary's fixed geometry.
internal static class RebirthWeaponDetailRows
{
    internal const int MaximumRows=16;
    private sealed class FrameRows
    {
        internal int Frame=-1,Stamp,OtherStamp;
        internal ItemValue Item,Other;
        internal EntityPlayer Player;
        internal List<Row> Rows;
        internal bool Valid;
    }
    private static readonly ConditionalWeakTable<XUi,FrameRows> Frames=new ConditionalWeakTable<XUi,FrameRows>();
    internal static int ItemStateStamp(ItemStack stack)=>Stamp(stack);
    private static int Stamp(ItemStack stack)
    {
        var item=stack?.itemValue;if(item==null)return 0;
        unchecked
        {
            int hash=item.type;hash=hash*31+item.Quality;hash=hash*31+item.Meta;hash=hash*31+item.Seed;
            hash=hash*31+item.UseTimes.GetHashCode();hash=hash*31+item.MaxDurabilityModifier.GetHashCode();hash=hash*31+stack.count;
            hash=hash*31+(EntityPlayerLocal.PermaDegrationOn?1:0);
            hash=ModStamp(item.modifications,hash);hash=ModStamp(item.cosmeticMods,hash);
            return hash;
        }
    }
    private static int ModStamp(ItemValue[] mods,int hash)
    {
        unchecked{if(mods!=null)foreach(var mod in mods)if(mod!=null)
        {hash=hash*31+mod.type;hash=hash*31+mod.Quality;hash=hash*31+mod.Meta;hash=hash*31+mod.Seed;hash=hash*31+mod.UseTimes.GetHashCode();}return hash;}
    }
    internal sealed class Row {internal string Key,Title,Value,SelectedValue="";internal float Number=float.NaN,OtherNumber=float.NaN;}
    private static string Number(float value,string format="0.#")=>value.ToString(format,CultureInfo.InvariantCulture);
    internal static string CompareNumber(float current,float baseline,bool negativePreferred=false,string suffix="",string format="0.#")
    {
        string text=RebirthItemStatColors.Format(Number(current,format)+suffix);
        if(float.IsNaN(baseline)||Mathf.Abs(current-baseline)<.001f)return text;
        float delta=current-baseline;bool better=negativePreferred?delta<0:delta>0;
        return text+(better?"[8FD18F]":"[CC6B64]")+" ("+(delta>0?"+":"")+Number(delta,"0.##")+suffix+")[-]";
    }
    private static float Other(RebirthWeaponSustainedDpsService.Profile p,Func<RebirthWeaponSustainedDpsService.Profile,float> read)=>p==null?float.NaN:read(p);
    private static string Pair(float normal,float power,float otherNormal,float otherPower,bool paired,bool negative=false)
        =>CompareNumber(normal,otherNormal,negative)+(paired?" ("+CompareNumber(power,otherPower,negative)+")":"");
    private static string Boost(string text,ItemValue item,params PassiveEffects[] effects)
    {
        foreach(var effect in effects)if(item.GetStatPercent(effect,true)!=1f)return RebirthItemStatColors.Format("[sp=ui_stat]")+text;
        return text;
    }
    internal static float Condition(ItemValue item)=>item==null||item.MaxUseTimes<=0?1f:Mathf.Clamp01(item.PercentUsesLeft);
    internal static float Wear(ItemValue item)=>ItemActionAttack.DegradationModifier(100f,Condition(item))-100f;
    private static float Block(float damage,float condition)=>ItemActionAttack.DegradationModifier(damage,condition);
    private static int Priority(DisplayInfoEntry stat)
    {
        switch(stat.StatType){case PassiveEffects.BlockDamage:return 0;case PassiveEffects.StaminaLoss:return 1;
            case PassiveEffects.AttacksPerMinute:return 2;case PassiveEffects.DamageFalloffRange:return 3;
            case PassiveEffects.MagazineSize:return 4;case PassiveEffects.RoundsPerMinute:return 5;case PassiveEffects.DegradationMax:return 6;default:return 7;}
    }
    internal static bool TryBuild(XUi ui,ItemStack stack,ItemStack comparison,out List<Row> result)
    {
        result=null;
        if(stack==null||stack.IsEmpty()||!RebirthWeaponSustainedDpsService.TryGetDisplayProfile(ui?.playerUI?.entityPlayer,stack.itemValue,out var p)||p?.Valid!=true)return false;
        result=new List<Row>();var item=stack.itemValue;var player=ui?.playerUI?.entityPlayer;
        bool compare=comparison!=null&&!comparison.IsEmpty()&&XUiM_ItemStack.CanCompare(item.ItemClass,comparison.itemValue.ItemClass);
        // Snapshot native condition once per item for this synchronous projection only.
        bool durable=item.MaxUseTimes>0,otherDurable=compare&&comparison.itemValue.MaxUseTimes>0;
        float condition=durable?Condition(item):1f,otherCondition=otherDurable?Condition(comparison.itemValue):1f;
        float wear=ItemActionAttack.DegradationModifier(100f,condition)-100f,otherWear=ItemActionAttack.DegradationModifier(100f,otherCondition)-100f;
        RebirthWeaponSustainedDpsService.Profile other=null;
        if(compare&&!RebirthWeaponSustainedDpsService.TryGetDisplayProfile(player,comparison.itemValue,out other))other=null;
        string dps=p.Melee?Pair(p.NormalAttackDps,p.PowerAttackDps,Other(other,x=>x.NormalAttackDps),Other(other?.HasPowerAttack==true?other:null,x=>x.PowerAttackDps),p.HasPowerAttack)
            :CompareNumber(p.SustainedDps,Other(other,x=>x.SustainedDps));
        result.Add(new Row{Key="dps",Title=Localization.Get(p.Melee?"xuiRebirthWeaponMeleeDps":p.Ranged?"xuiRebirthWeaponRangedDps":"xuiRebirthWeaponExplosiveDps"),Value=p.Ranged?Boost(dps,item,PassiveEffects.EntityDamage,PassiveEffects.RoundsPerMinute,PassiveEffects.MagazineSize,PassiveEffects.ReloadSpeedMultiplier,PassiveEffects.RoundRayCount):Boost(dps,item,PassiveEffects.EntityDamage,PassiveEffects.AttacksPerMinute)});
        if(durable)
        {
            result.Add(new Row{Key="condition",Title=Localization.Get("xuiRebirthWeaponCondition"),Number=condition*100f,OtherNumber=otherDurable?otherCondition*100f:float.NaN,Value=CompareNumber(condition*100f,otherDurable?otherCondition*100f:float.NaN,false,"%")});
            result.Add(new Row{Key="wear",Title=Localization.Get("xuiRebirthWeaponBlockWear"),Number=wear,OtherNumber=otherDurable?otherWear:float.NaN,Value=CompareNumber(wear,otherDurable?otherWear:float.NaN,false,"%")});
        }
        if(p.Ranged)
            result.Add(new Row{Key="reload-time",Title=Localization.Get("xuiRebirthWeaponReloadTime"),Value=p.ReloadSeconds>0?Boost(CompareNumber(p.ReloadSeconds,Other(other,x=>x.ReloadSeconds>0?x.ReloadSeconds:float.NaN),true," s","0.##"),item,PassiveEffects.ReloadSpeedMultiplier):"—"});
        var display=UIDisplayInfoManager.Current.GetDisplayStatsForTag(item.ItemClass.DisplayType)?.DisplayStats;
        if(display==null)return true;
        var otherDisplay=compare?UIDisplayInfoManager.Current.GetDisplayStatsForTag(comparison.itemValue.ItemClass.DisplayType)?.DisplayStats:null;
        foreach(var stat in display.Where(s=>s!=null).OrderBy(Priority))
        {
            if(stat.StatType==PassiveEffects.EntityDamage)continue;
            string title=stat.TitleOverride??UIDisplayInfoManager.Current.GetLocalizedName(stat.StatType)??stat.CustomName??stat.StatType.ToString();
            if(title.IndexOf("DPS",StringComparison.OrdinalIgnoreCase)>=0||title.IndexOf("Damage Per Second",StringComparison.OrdinalIgnoreCase)>=0)continue;
            if(!RebirthConsumableItemPresentation.ShouldShowNativeStat(stat,item,compare?comparison.itemValue:null,ui))continue;
            bool nativeCompare=compare&&otherDisplay?.Any(otherStat=>otherStat!=null&&otherStat.StatType==stat.StatType&&string.Equals(otherStat.CustomName,stat.CustomName,StringComparison.Ordinal))==true;
            string text;
            if(p.Melee&&stat.StatType==PassiveEffects.BlockDamage)
                text=Boost(Pair(Block(p.NormalBlockDamagePerAttack,condition),Block(p.PowerBlockDamagePerAttack,condition),Other(other,x=>Block(x.NormalBlockDamagePerAttack,otherCondition)),Other(other?.HasPowerAttack==true?other:null,x=>Block(x.PowerBlockDamagePerAttack,otherCondition)),p.HasPowerAttack),item,PassiveEffects.BlockDamage);
            else if(p.Melee&&stat.StatType==PassiveEffects.StaminaLoss)
                text=Boost(Pair(p.NormalStaminaCost,p.PowerStaminaCost,Other(other,x=>x.NormalStaminaCost),Other(other?.HasPowerAttack==true?other:null,x=>x.PowerStaminaCost),p.HasPowerAttack,true),item,PassiveEffects.StaminaLoss);
            else text=nativeCompare?RebirthItemStatColors.FormatComparison(RebirthItemStatColors.WithBonus(XUiM_ItemStack.GetStatItemValueTextWithCompareInfo(item,comparison.itemValue,player,stat,flipCompare:true),item,stat))
                :RebirthItemStatColors.Format(RebirthItemStatColors.NativeValue(stack,player,stat));
            result.Add(new Row{Key="native:"+stat.StatType+":"+stat.CustomName,Title=title,Value=text});
        }
        // Retain per-attack damage on the complete card after the compact summary.
        result.Add(new Row{Key="attack-damage",Title=Localization.Get("statEntityDamage"),Value=p.Melee
            ?Boost(Pair(p.NormalDamagePerAttack,p.PowerDamagePerAttack,Other(other,x=>x.NormalDamagePerAttack),Other(other?.HasPowerAttack==true?other:null,x=>x.PowerDamagePerAttack),p.HasPowerAttack),item,PassiveEffects.EntityDamage)
            :Boost(CompareNumber(p.DamagePerAttack*p.ProjectilesPerAttack,Other(other,x=>x.DamagePerAttack*x.ProjectilesPerAttack)),item,PassiveEffects.EntityDamage,PassiveEffects.RoundRayCount)});
        return true;
    }
    internal static bool TryBuildComparison(XUi ui,ItemStack stack,ItemStack selected,out List<Row> rows)
    {
        if(!TryBuild(ui,stack,selected,out rows))return false;
        if(selected==null||selected.IsEmpty()||!XUiM_ItemStack.CanCompare(stack.itemValue.ItemClass,selected.itemValue.ItemClass)||!TryBuild(ui,selected,null,out var other))return true;
        foreach(var row in rows)row.SelectedValue=FindRow(other,row.Key)?.Value??"";
        foreach(var row in other)if(FindRow(rows,row.Key)==null)rows.Add(new Row{Key=row.Key,Title=row.Title,Value="",SelectedValue=row.Value});
        return true;
    }
    private static Row FindRow(List<Row> rows,string key)
    {
        for(int i=0;i<rows.Count;i++)if(rows[i].Key==key)return rows[i];
        return null;
    }
    private static string CompactPercent(Row row)
    {
        string text=RebirthItemStatColors.Format(Number(row.Number)+"%");
        if(float.IsNaN(row.OtherNumber)||Mathf.Abs(row.Number-row.OtherNumber)<.001f)return text;
        return text+(row.Number>row.OtherNumber?"[8FD18F] ▲[-]":"[CC6B64] ▼[-]");
    }
    internal static bool TryGet(XUi ui,ItemStack stack,ItemStack comparison,int index,out string title,out string value)
    {
        title=value="";
        if(index<0||index>=MaximumRows)return false;
        List<Row> rows;
        if(ui==null){if(!TryBuild(ui,stack,comparison,out rows))return false;}
        else
        {
            var item=stack?.itemValue;var other=comparison?.itemValue;var player=ui.playerUI?.entityPlayer;
            var frame=Frames.GetValue(ui,_=>new FrameRows());int stamp=Stamp(stack),otherStamp=Stamp(comparison);
            // One UI-frame row plan keeps title/value bindings consistent. It is
            // never retained into the next player-condition/effect refresh.
            if(frame.Frame!=Time.frameCount||!ReferenceEquals(frame.Item,item)||!ReferenceEquals(frame.Other,other)
                ||!ReferenceEquals(frame.Player,player)||frame.Stamp!=stamp||frame.OtherStamp!=otherStamp)
            {
                frame.Frame=Time.frameCount;frame.Item=item;frame.Other=other;frame.Player=player;frame.Stamp=stamp;frame.OtherStamp=otherStamp;
                frame.Valid=TryBuild(ui,stack,comparison,out frame.Rows);
            }            if(!frame.Valid)return false;rows=frame.Rows;
        }
        // Keep the fixed seven-slot summaries useful without dropping native
        // max durability: condition and native block wear share one compact row.
        // The full hover card keeps both separate, with numeric deltas.
        bool compact=rows.Count>2&&rows[1].Key=="condition"&&rows[2].Key=="wear";
        if(compact&&index==1){title=Localization.Get("xuiRebirthWeaponConditionWear");value=CompactPercent(rows[1])+" / "+CompactPercent(rows[2]);return true;}
        int source=compact&&index>=2?index+1:index;
        if(source<rows.Count){title=rows[source].Title;value=rows[source].Value;}return true;
    }
}











