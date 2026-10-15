using System;
using HarmonyLib;

// Count the native accepted acquisition, not treatment/display stages or restored save buffs.
internal static class RebirthSurvivalHealthEpisodes
{
    internal static bool IsEpisode(string name)
    {
        switch(name)
        {
            case "buffLegBroken": case "buffArmBroken":
            case "buffLegSprained": case "buffArmSprained":
            case "buffInjuryConcussion": case "buffDysenteryMain":return true;
            default:return false;
        }
    }
    private static bool Active(EntityBuffs buffs,string name)
    {var value=buffs.GetBuff(name);return value!=null&&!value.Remove;}
    internal static bool Eligible(EntityBuffs buffs,string name)
    {
        if(!IsEpisode(name)||!(buffs?.parent is EntityPlayer player)||player.IsDead()
            ||player.world==null||player.world.IsRemote()
            ||!ReferenceEquals(GameManager.Instance?.World,player.world)
            ||!ReferenceEquals(player.world.GetEntity(player.entityId),player)
            ||RebirthCharacterCreationHoldService.IsHeld(player)||Active(buffs,name))return false;
        // Splint/cast stages retain the original fracture; their replacement is not a new break.
        if(name=="buffLegBroken"&&(Active(buffs,"buffLegSplinted")||Active(buffs,"buffLegCast")))return false;
        if(name=="buffArmBroken"&&(Active(buffs,"buffArmSplinted")||Active(buffs,"buffArmCast")))return false;
        RebirthWorldCharacterRecord character;
        return RebirthWorldCharacterService.TryGet(player,out character)&&character!=null&&character.IsComplete;
    }
    internal static void Applied(EntityBuffs buffs,string name)
    {
        var player=buffs?.parent as EntityPlayer;
        if(player==null||player.world==null||player.world.IsRemote()||!Active(buffs,name))return;
        var record=RebirthStatisticsRepository.GetOrCreate(player);if(record==null)return;
        long count;record.HealthEpisodes.TryGetValue(name,out count);
        if(count==long.MaxValue)return;
        record.HealthEpisodes[name]=count+1;record.Touch("health-episode:"+name);
    }
}

// Patch only the implementation overload: the shorter native overload forwards to this one.
[HarmonyPatch(typeof(EntityBuffs),nameof(EntityBuffs.AddBuff),new Type[]{typeof(string),typeof(Vector3i),typeof(int),typeof(bool),typeof(bool),typeof(float)})]
internal static class RebirthSurvivalHealthEpisodePatch
{
    static void Prefix(EntityBuffs __instance,string __0,out bool __state)
    {__state=RebirthSurvivalHealthEpisodes.Eligible(__instance,__0);}
    static void Postfix(EntityBuffs __instance,string __0,bool __state,bool __runOriginal,EntityBuffs.BuffStatus __result)
    {if(__state&&__runOriginal&&__result==EntityBuffs.BuffStatus.Added)RebirthSurvivalHealthEpisodes.Applied(__instance,__0);}
}
