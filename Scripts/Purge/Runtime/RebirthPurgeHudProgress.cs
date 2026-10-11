using System;
using UnityEngine;

// Presentation only: native synchronized CVars carry the owner's supply counter.
// They never authorize rewards or replace the persisted account.
internal static class RebirthPurgeHudProgress
{
    private static double next;private static int cursor;private static World owner;
    internal static void Pulse()
    {
        var world=GameManager.Instance?.World;
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||world==null||world.IsRemote())return;
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
            int target=rules.Target(account?.EarnedDrops??0);
            long balance=account==null?0:account.ObservedCredits-account.SpentCredits;
            Set(player,"_rbPurgeSupplyTarget",target);
            Set(player,"_rbPurgeSupplyRemaining",Math.Max(0,target-balance));
            Set(player,"_rbPurgeSupplyKnown",1);
        }
    }
    private static void Set(EntityPlayer player,string key,float value)
    {if(!player.Buffs.HasCustomVar(key)||player.Buffs.GetCustomVar(key)!=value)player.Buffs.SetCustomVar(key,value,true);}
    internal static string Text(EntityPlayerLocal player)
    {
        if(player==null)return Localization.Get("xuiRebirthPurgeObjectivePending");
        string cleared="—",total="—";var world=GameManager.Instance?.World;
        var biome=world?.GetBiome((int)player.position.x,(int)player.position.z)?.m_sBiomeName;
        if(biome!=null)
        {
            if(world.IsRemote())
            {
                var frame=RebirthPoiMapSync.LocalObjectives;RebirthPurgeObjectiveFrame.Biome value;
                if(frame!=null&&frame.Known&&frame.Biomes.TryGetValue(biome,out value)){cleared=value.Cleared.ToString();total=value.Eligible.ToString();}
            }
            else
            {
                var data=RebirthPurgeObjectiveProgress.Instance.Published;RebirthPurgeObjectiveProgress.BiomeProgress value;
                if(data!=null&&data.TryGetValue(biome,out value)){cleared=value.Cleared.ToString();total=value.Eligible.ToString();}
            }
        }
        bool known=player.Buffs.GetCustomVar("_rbPurgeSupplyKnown")==1;
        return string.Format(Localization.Get("xuiRebirthPurgeHudProgress"),cleared,total,
            known?player.Buffs.GetCustomVar("_rbPurgeSupplyRemaining").ToString("0"):"—",
            known?player.Buffs.GetCustomVar("_rbPurgeSupplyTarget").ToString("0"):"—");
    }
}
