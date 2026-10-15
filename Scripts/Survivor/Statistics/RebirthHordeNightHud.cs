using System;
using System.Globalization;

// Native synchronized CVars are owner presentation only; the Statistics record owns history.
internal static class RebirthHordeNightHud
{
    internal static void Publish(EntityPlayer player,RebirthStatisticsRecord record,RebirthHordeNightStatistics active)
    {
        if(player?.Buffs==null||player.world==null||player.world.IsRemote())return;
        var night=active;
        if(night==null)
        {
            int today=GameUtils.WorldTimeToDays(player.world.worldTime);
            if(!record.HordeNightIndex.TryGetValue(today,out night))record.HordeNightIndex.TryGetValue(today-1,out night);
        }
        Set(player,"_rbHordeStatsKnown",night==null?0f:1f);
        Set(player,"_rbHordeStatsDay",night?.WorldDay??0);
        Set(player,"_rbHordeStatsKills",night?.Kills??0);
        Set(player,"_rbHordeStatsHeads",night?.HeadshotKills??0);
    }
    private static void Set(EntityPlayer player,string name,float value)
    {if(!player.Buffs.HasCustomVar(name)||player.Buffs.GetCustomVar(name)!=value)player.Buffs.SetCustomVar(name,value,true);}
    internal static bool Visible(EntityPlayerLocal player)
    {
        var world=player?.world;
        if(world==null||GamePrefs.GetInt(EnumGamePrefs.BloodMoonFrequency)<=0)return false;
        if(SkyManager.IsBloodMoonVisible()||world.aiDirector?.BloodMoonComponent?.BloodMoonActive==true)return true;
        var time=GameUtils.WorldTimeToElements(world.worldTime);
        int dawn=22-GamePrefs.GetInt(EnumGamePrefs.DayLightLength);
        return time.Item2==dawn&&time.Item3<30&&player.Buffs!=null
            &&player.Buffs.GetCustomVar("_rbHordeStatsKnown")==1f
            &&player.Buffs.GetCustomVar("_rbHordeStatsDay")==time.Item1-1;
    }
    internal static string Count(EntityPlayerLocal player,bool heads)
    {
        if(player?.Buffs==null||player.Buffs.GetCustomVar("_rbHordeStatsKnown")!=1f)return "—";
        var time=GameUtils.WorldTimeToElements(player.world.worldTime);
        int today=GameUtils.WorldTimeToDays(player.world.worldTime);
        float day=player.Buffs.GetCustomVar("_rbHordeStatsDay");
        int expectedNight=time.Item2>=22?today:today-1;
        if(day!=expectedNight)return "—";
        return player.Buffs.GetCustomVar(heads?"_rbHordeStatsHeads":"_rbHordeStatsKills").ToString("N0",CultureInfo.InvariantCulture);
    }
    internal static bool AfterDawn(EntityPlayerLocal player)=>Visible(player)&&!SkyManager.IsBloodMoonVisible()
        &&player?.world?.aiDirector?.BloodMoonComponent?.BloodMoonActive!=true;
}
