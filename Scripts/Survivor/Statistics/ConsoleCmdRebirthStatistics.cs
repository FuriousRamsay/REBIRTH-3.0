using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

#nullable disable

public sealed class ConsoleCmdRebirthStatistics : ConsoleCmdAbstract
{
    public override string[] getCommands(){return new[]{"rbstats","rebirthstats"};}
    public override string getDescription(){return "Inspect, export, or explicitly reset REBIRTH player statistics.";}
    public override string getHelp(){return "rbstats status | export | reset confirm";}
    public override void Execute(List<string> p,CommandSenderInfo senderInfo)
    {
        EntityPlayer player=ResolvePlayer(senderInfo);if(player==null){Log.Warning("[REBIRTH Statistics] player unavailable.");return;}
        string cmd=p!=null&&p.Count>0?(p[0]??string.Empty).ToLowerInvariant():"status";
        if(cmd=="reset")
        {
            if(p==null||p.Count<2||!string.Equals(p[1],"confirm",StringComparison.OrdinalIgnoreCase)){Log.Out("Statistics reset is destructive for this world/player. Use: rbstats reset confirm");return;}
            string message;bool ok=RebirthStatisticsRepository.ResetPlayer(player,out message);Log.Out("[REBIRTH Statistics] reset="+ok+" "+message);if(ok)RebirthStatisticsService.SendSnapshot(player,0L,true,"debug-reset");return;
        }
        RebirthStatisticsRecord record=RebirthStatisticsRepository.GetOrCreate(player);if(record==null){Log.Warning("[REBIRTH Statistics] authoritative record unavailable.");return;}
        RebirthStatisticsSnapshot s=RebirthStatisticsService.BuildSnapshot(player,record);
        if(cmd=="export")
        {
            string root=RebirthStatisticsRepository.RootDirectory;if(string.IsNullOrEmpty(root)){Log.Warning("[REBIRTH Statistics] storage root unavailable.");return;}Directory.CreateDirectory(root);string path=Path.Combine(root,"statistics_export_"+record.StablePlayerKey+".csv");File.WriteAllText(path,BuildCsv(s));Log.Out("[REBIRTH Statistics] exported "+path);return;
        }
        Log.Out("[REBIRTH Statistics] epoch="+s.Epoch+" revision="+s.Revision+" active="+s.ActivePlaySeconds.ToString("0",CultureInfo.InvariantCulture)+"s life="+s.CurrentLifeSeconds.ToString("0",CultureInfo.InvariantCulture)+"s days="+s.DaysSurvived+" distance="+s.DistanceMeters.ToString("0.0",CultureInfo.InvariantCulture)+"m zombies="+s.ZombiesKilled+" deaths="+s.Deaths+" crafted="+s.ItemsCrafted+" resources="+s.ResourcesGathered+" knowledge="+s.KnowledgeDiscovered+" skillProgress="+s.SkillProgressGained.ToString("0.0",CultureInfo.InvariantCulture)+" locations="+s.LocationsDiscovered+" biomes="+s.BiomesVisited+" traders="+s.TradersVisited);
    }
    private static string BuildCsv(RebirthStatisticsSnapshot s)
    {
        return "metric,value\nactive_play_seconds,"+s.ActivePlaySeconds.ToString("R",CultureInfo.InvariantCulture)+"\ncurrent_life_seconds,"+s.CurrentLifeSeconds.ToString("R",CultureInfo.InvariantCulture)+"\nlongest_life_seconds,"+s.LongestLifeSeconds.ToString("R",CultureInfo.InvariantCulture)+"\ndays_survived,"+s.DaysSurvived+"\ndistance_meters,"+s.DistanceMeters.ToString("R",CultureInfo.InvariantCulture)+"\non_foot_meters,"+s.OnFootMeters.ToString("R",CultureInfo.InvariantCulture)+"\nzombies_killed,"+s.ZombiesKilled+"\nplayer_kills,"+s.PlayerKills+"\ndeaths,"+s.Deaths+"\nanimals_killed,"+s.AnimalsKilled+"\nheadshot_kills,"+s.HeadshotKills+"\nitems_crafted,"+s.ItemsCrafted+"\nresources_gathered,"+s.ResourcesGathered+"\nknowledge_discovered,"+s.KnowledgeDiscovered+"\nskill_progress_gained,"+s.SkillProgressGained.ToString("R",CultureInfo.InvariantCulture)+"\npois_cleared,"+s.PoisCleared+"\nlocations_discovered,"+s.LocationsDiscovered+"\ntraders_visited,"+s.TradersVisited+"\nbiomes_visited,"+s.BiomesVisited+"\ndamage_dealt,"+s.DamageDealt.ToString("R",CultureInfo.InvariantCulture)+"\ndamage_taken,"+s.DamageTaken.ToString("R",CultureInfo.InvariantCulture)+"\n";
    }
    private static EntityPlayer ResolvePlayer(CommandSenderInfo senderInfo){World world=GameManager.Instance!=null?GameManager.Instance.World:null;if(world==null)return null;if(senderInfo.RemoteClientInfo!=null){EntityPlayer remote=world.GetEntity(senderInfo.RemoteClientInfo.entityId) as EntityPlayer;if(remote!=null)return remote;}return world.GetPrimaryPlayer() as EntityPlayer;}
}
