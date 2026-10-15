using System;
using System.Globalization;
using UnityEngine;

// Presentation only: native synchronized CVars carry the owner's supply counter.
// They never authorize rewards or replace the persisted account.
internal static class RebirthPurgeHudProgress
{
    private static double next;private static int cursor;private static World owner;
    private static World displayWorld;
    private static Guid displayWorldId;
    private static System.Collections.Generic.IReadOnlyDictionary<string,RebirthPurgeObjectiveProgress.BiomeProgress> displayProgress;
    internal static void Reset(){owner=null;initializedWorld=null;displayWorld=null;displayProgress=null;displayWorldId=Guid.Empty;next=0;cursor=0;}
    internal static bool Visible(EntityPlayerLocal player)=>player!=null&&
        RebirthPurgeReleasePolicy.Enabled&&RebirthSandboxOptionManager.Current.IsPurge&&
        !RebirthHordeNightHud.Visible(player)&&!SkyManager.IsBloodMoonVisible()&&GameManager.Instance?.World?.aiDirector?.BloodMoonComponent?.BloodMoonActive!=true;
    private static World initializedWorld;
    internal static void InitializeLoadedWorld()
    {
        var world=GameManager.Instance?.World;
        if(world==null||world.IsRemote()||ReferenceEquals(initializedWorld,world)||
            !RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge)return;
        RebirthPoiWorldStore store;
        if(!RebirthPoiWorldLifecycle.Instance.TryGetStore(out store))return;
        RebirthPurgePoiCensus.Instance.InitializeLoadedWorld();
        RebirthPurgeObjectiveProgress.Instance.InitializeLoadedWorld();
        if(CurrentLocalProgress()!=null)initializedWorld=world;
    }
    internal static void Pulse()
    {
        var world=GameManager.Instance?.World;
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||world==null||world.IsRemote())
        {if(owner!=null)Reset();return;}
        if(!ReferenceEquals(owner,world)){owner=world;next=0;cursor=0;}
        if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+1;
        var players=world.aiDirector?.GetComponent<AIDirectorPlayerManagementComponent>()?.trackedPlayers.list;
        var accounts=RebirthPurgeSupplyService.Published;var rules=RebirthPurgeSupplyPolicy.Current;
        if(players==null)return;
        for(int n=0,limit=Math.Min(16,players.Count);n<limit;n++)
        {
            if(cursor>=players.Count)cursor=0;var player=players[cursor++].Player;if(player==null)continue;
            var key=RebirthPurgeKillContributor.Identify(player,world);
            if(accounts==null||rules==null||key==null){Set(player,"_rbPurgeSupplyKnown",0);continue;}
            RebirthPurgeSupplyAccount account;accounts.TryGetValue(key,out account);
            int target=rules.Target(account?.EarnedDrops??0,RebirthPurgeSupplyEquipment.Tier(player));
            long balance=account==null?0:account.ObservedCredits-account.SpentCredits;
            Set(player,"_rbPurgeSupplyTarget",target);
            Set(player,"_rbPurgeSupplyRemaining",Math.Max(0,target-balance));
            Set(player,"_rbPurgeSupplyDeliveredAny",account!=null&&account.DeliveredDrops>0?1:0);
            Set(player,"_rbPurgeSupplyKnown",1);
        }
    }
    private static void Set(EntityPlayer player,string key,float value)
    {if(!player.Buffs.HasCustomVar(key)||player.Buffs.GetCustomVar(key)!=value)player.Buffs.SetCustomVar(key,value,true);}
    internal static string BiomeColor(EntityPlayerLocal player)
    {
        var biome=player==null?null:GameManager.Instance?.World?.GetBiome((int)player.position.x,(int)player.position.z)?.m_sBiomeName;
        switch(biome)
        {
            case "pine_forest":return "58,105,70,255";
            case "desert":return "184,181,140,255";
            case "snow":return "128,128,128,255";
            case "wasteland":return "152,166,121,255";
            case "burnt_forest":return "56,56,56,255";
            default:return "0,0,0,255";
        }
    }
    internal static string BiomeTextColor(EntityPlayerLocal player)
    {
        var biome=player==null?null:GameManager.Instance?.World?.GetBiome((int)player.position.x,(int)player.position.z)?.m_sBiomeName;
        // Preserve the legacy biome hues, lifted for readable text on black.
        switch(biome)
        {
            case "pine_forest":return "128,201,144,255";
            case "desert":return "216,212,166,255";
            case "snow":return "227,227,227,255";
            case "wasteland":return "184,201,146,255";
            case "burnt_forest":return "186,186,186,255";
            default:return "225,225,225,255";
        }
    }
    internal static string BiomeFill(EntityPlayerLocal player)
    {
        var world=GameManager.Instance?.World;
        var biome=player==null?null:world?.GetBiome((int)player.position.x,(int)player.position.z)?.m_sBiomeName;
        if(biome==null)return "0";
        int cleared,eligible,mode=RebirthPurgeObjectivePolicy.ProgressionMode;
        if(world.IsRemote())
        {
            var frame=RebirthPoiMapSync.LocalObjectives;RebirthPurgeObjectiveFrame.Biome value;
            if(!CurrentFrame(frame)||!frame.Biomes.TryGetValue(biome,out value))return "0";
            mode=frame.ProgressionMode;cleared=RebirthPurgeProgression.Units(value.Cleared,value.ClearedByTier,mode);eligible=RebirthPurgeProgression.Units(value.Eligible,value.EligibleByTier,mode);
        }
        else
        {
            var data=CurrentLocalProgress();RebirthPurgeObjectiveProgress.BiomeProgress value;
            if(data==null||!data.TryGetValue(biome,out value))return "0";
            cleared=RebirthPurgeProgression.Units(value.Cleared,value.ClearedByTier,mode);eligible=RebirthPurgeProgression.Units(value.Eligible,value.EligibleByTier,mode);
        }
        return (eligible<=0?0d:Math.Min(1d,Math.Max(0d,(double)cleared/eligible))).ToString(CultureInfo.InvariantCulture);
    }
    internal static string Text(EntityPlayerLocal player)
    {
        if(player==null)return Localization.Get("xuiRebirthPurgeObjectivePending");
        string cleared="—",total="—",percentage="—";var world=GameManager.Instance?.World;
        var biome=world?.GetBiome((int)player.position.x,(int)player.position.z)?.m_sBiomeName;
        if(biome!=null)
        {
            if(world.IsRemote())
            {
                var frame=RebirthPoiMapSync.LocalObjectives;RebirthPurgeObjectiveFrame.Biome value;
                if(CurrentFrame(frame)&&frame.Biomes.TryGetValue(biome,out value)){cleared=value.Cleared.ToString();total=value.Eligible.ToString();percentage=Percentage(RebirthPurgeProgression.Units(value.Cleared,value.ClearedByTier,world.IsRemote()?RebirthPoiMapSync.LocalObjectives.ProgressionMode:RebirthPurgeObjectivePolicy.ProgressionMode),RebirthPurgeProgression.Units(value.Eligible,value.EligibleByTier,world.IsRemote()?RebirthPoiMapSync.LocalObjectives.ProgressionMode:RebirthPurgeObjectivePolicy.ProgressionMode));}
            }
            else
            {
                var data=CurrentLocalProgress();RebirthPurgeObjectiveProgress.BiomeProgress value;
                if(data!=null&&data.TryGetValue(biome,out value)){cleared=value.Cleared.ToString();total=value.Eligible.ToString();percentage=Percentage(RebirthPurgeProgression.Units(value.Cleared,value.ClearedByTier,world.IsRemote()?RebirthPoiMapSync.LocalObjectives.ProgressionMode:RebirthPurgeObjectivePolicy.ProgressionMode),RebirthPurgeProgression.Units(value.Eligible,value.EligibleByTier,world.IsRemote()?RebirthPoiMapSync.LocalObjectives.ProgressionMode:RebirthPurgeObjectivePolicy.ProgressionMode));}
            }
        }
        bool known=player.Buffs.GetCustomVar("_rbPurgeSupplyKnown")==1;
        string progress=string.Format(Localization.Get("xuiRebirthPurgeHudProgress"),cleared,total,
            known?player.Buffs.GetCustomVar("_rbPurgeSupplyRemaining").ToString("0"):"—",
            known?player.Buffs.GetCustomVar("_rbPurgeSupplyTarget").ToString("0"):"—",percentage,RebirthPurgeScanner.LifetimeText(player));
        return progress;
    }
    private static bool CurrentFrame(RebirthPurgeObjectiveFrame frame)
        =>frame!=null&&frame.Known&&frame.World==RebirthPoiMapSync.LocalWorld&&frame.Session==RebirthPoiMapSync.LocalSession;
    private static System.Collections.Generic.IReadOnlyDictionary<string,RebirthPurgeObjectiveProgress.BiomeProgress> CurrentLocalProgress()
    {
        var progress=RebirthPurgeObjectiveProgress.Instance;
        RebirthPoiWorldStore store;
        var world=GameManager.Instance?.World;
        if(!RebirthPoiWorldLifecycle.Instance.TryGetStore(out store)||store.Published==null||!store.Published.Binding.IsCurrent)return null;
        if(progress.Published!=null&&progress.WorldId==store.Published.WorldId&&progress.Revision<=store.Published.Revision)
        {
            displayWorld=world;displayWorldId=progress.WorldId;displayProgress=progress.Published;
            return displayProgress;
        }
        // Presentation only: retain validated numbers while the same world's
        // census is rebuilt. Reward services still require their live snapshot.
        return ReferenceEquals(displayWorld,world)&&displayWorldId==store.Published.WorldId?displayProgress:null;
    }
    internal static void BiomeSummary(EntityPlayerLocal player,out string biomeName,out string percentage,out string counts)
    {
        biomeName=percentage=counts="—";
        var world=GameManager.Instance?.World;
        if(player==null||world==null)return;
        var biome=world.GetBiome((int)player.position.x,(int)player.position.z)?.m_sBiomeName;
        if(biome==null)return;
        biomeName=Localization.Get("biome_"+biome);
        int cleared,eligible,mode=RebirthPurgeObjectivePolicy.ProgressionMode;
        if(world.IsRemote())
        {
            var frame=RebirthPoiMapSync.LocalObjectives;RebirthPurgeObjectiveFrame.Biome value;
            if(!CurrentFrame(frame)||!frame.Biomes.TryGetValue(biome,out value))return;
            mode=frame.ProgressionMode;cleared=RebirthPurgeProgression.Units(value.Cleared,value.ClearedByTier,mode);eligible=RebirthPurgeProgression.Units(value.Eligible,value.EligibleByTier,mode);
        }
        else
        {
            var data=CurrentLocalProgress();RebirthPurgeObjectiveProgress.BiomeProgress value;
            if(data==null||!data.TryGetValue(biome,out value))return;
            cleared=RebirthPurgeProgression.Units(value.Cleared,value.ClearedByTier,mode);eligible=RebirthPurgeProgression.Units(value.Eligible,value.EligibleByTier,mode);
        }
        percentage=Percentage(cleared,eligible);
        counts=RebirthPurgeProgression.Text(cleared,mode)+"/"+RebirthPurgeProgression.Text(eligible,mode)+(mode==1?" "+Localization.Get("xuiRebirthPurgePoints"):string.Empty);
    }
    internal static bool HasBiomeSummary(EntityPlayerLocal player)
    {
        string biome,percentage,counts;
        BiomeSummary(player,out biome,out percentage,out counts);
        return counts!="—";
    }
    private static string Percentage(int cleared,int eligible)
    {return eligible<=0?"—":Math.Min(100d,100d*cleared/eligible).ToString("0.#",CultureInfo.InvariantCulture)+"%";}
}
