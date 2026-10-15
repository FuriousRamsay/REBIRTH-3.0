using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

internal static class RebirthHordeNightStatisticsRuntime
{
    private static ConditionalWeakTable<EntityAlive,object> credited=new ConditionalWeakTable<EntityAlive,object>();
    private static readonly string[] VariantSuffixes={"Infernal","Charged","Radiated","Feral"};
    internal static void Reset(){credited=new ConditionalWeakTable<EntityAlive,object>();}
    internal static RebirthHordeNightStatistics Observe(EntityPlayer player,RebirthStatisticsRecord record)
    {
        var component=player?.world?.aiDirector?.BloodMoonComponent;
        if(component==null||!component.BloodMoonActive||component.bmDay<1)
        {
            if(record.ActiveHordeNight!=null){record.ActiveHordeNight.Completed=true;record.ActiveHordeNight=null;record.Touch("horde-night-completed");}
            return null;
        }
        int day=component.bmDay;
        if(record.ActiveHordeNight!=null&&record.ActiveHordeNight.WorldDay!=day){record.ActiveHordeNight.Completed=true;record.ActiveHordeNight=null;record.Touch("horde-night-completed");}
        RebirthHordeNightStatistics current;
        if(record.HordeNightIndex.TryGetValue(day,out current))
        {
            if(current.Completed){current.Completed=false;record.Touch("horde-night-resumed");}
            record.ActiveHordeNight=current;return current;
        }
        current=new RebirthHordeNightStatistics{WorldDay=day};record.AddHordeNight(current);
        record.HordeNightTotals.NightsParticipated=Increment(record.HordeNightTotals.NightsParticipated);
        record.Touch("horde-night-participated");return current;
    }
    internal static void Death(EntityPlayer player,RebirthStatisticsRecord record)
    {
        var night=Observe(player,record);if(night==null)return;
        night.Deaths=Increment(night.Deaths);record.HordeNightTotals.Deaths=Increment(record.HordeNightTotals.Deaths);
        record.Touch("horde-night-death");
    }
    internal static void Kill(EntityPlayer player,RebirthStatisticsRecord record,EntityAlive victim,DamageResponse response,DamageSource source,string family)
    {
        var night=Observe(player,record);if(night==null||victim==null)return;
        object existing;if(credited.TryGetValue(victim,out existing))return;credited.Add(victim,new object());
        var totals=record.HordeNightTotals;night.Kills=Increment(night.Kills);totals.Kills=Increment(totals.Kills);
        // Explosion masks may include multiple body parts; only an exact head hit
        // qualifies as a headshot, matching the native 2.6 counter semantics.
        bool head=response.HitBodyPart==EnumBodyPartHit.Head;
        if(head){night.HeadshotKills=Increment(night.HeadshotKills);totals.HeadshotKills=Increment(totals.HeadshotKills);}
        if(head&&response.Dismember){night.HeadDecapitations=Increment(night.HeadDecapitations);totals.HeadDecapitations=Increment(totals.HeadDecapitations);}
        string item=source?.AttackingItem?.ItemClass?.GetItemName();
        switch(item)
        {
            case "thrownAmmoMolotovCocktail":family="molotovs";break;
            case "thrownAmmoPipeBomb":family="pipe_bombs";break;
            case "thrownGrenade":case "thrownGrenadeContact":family="grenades";break;
        }
        if(string.IsNullOrEmpty(family))family=source?.BuffClass!=null&&source.AttackingItem==null?"indirect_damage":"other";
        Add(night.WeaponCategories,family);Add(totals.WeaponCategories,family);
        // The original Horde total admitted all victims. Only zombie victims
        // belong in the archetype and variant rankings.
        if(victim is EntityZombie zombie)
        {
        EntityClass type;
        string name=EntityClass.list.TryGetValue(zombie.entityClass,out type)?type?.entityClassName:null;
        if(string.IsNullOrEmpty(name))name="unknown";
        // Authored no-minion clones are the same zombie archetype/variant.
        // Remove the clone suffix before classifying Feral/Radiated, otherwise
        // zombieMutatedRadiatedNoMinion incorrectly appears as a normal type.
        if(name.EndsWith("NoMinion",StringComparison.OrdinalIgnoreCase))
            name=name.Substring(0,name.Length-"NoMinion".Length);
        string variant="normal";
        foreach(var suffix in VariantSuffixes)
            if(name.EndsWith(suffix,StringComparison.OrdinalIgnoreCase)){name=name.Substring(0,name.Length-suffix.Length);variant=suffix.ToLowerInvariant();break;}
        Add(night.ZombieTypes,name);Add(totals.ZombieTypes,name);Add(night.ZombieVariants,variant);Add(totals.ZombieVariants,variant);
        }
        if(night.Kills>totals.BestNightKills){totals.BestNightKills=night.Kills;totals.BestNightWorldDay=night.WorldDay;}
        record.Touch("horde-night-kill");
        RebirthHordeNightHud.Publish(player,record,night);
    }
    private static long Increment(long value)=>value==long.MaxValue?value:value+1;
    private static void Add(Dictionary<string,long> counts,string key)
    {long value;counts.TryGetValue(key,out value);if(counts.ContainsKey(key)||counts.Count<4096)counts[key]=Increment(value);}
}
