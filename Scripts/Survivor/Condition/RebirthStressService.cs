using System;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

public static class RebirthStressService
{
    static bool installed;
    public static void Install()
    {
        if(installed)return;installed=true;
        new Harmony("rebirth.stress").Patch(AccessTools.Method(typeof(EntityAlive),"DamageEntity",new[]{typeof(DamageSource),typeof(int),typeof(bool),typeof(float)}),postfix:new HarmonyMethod(typeof(RebirthStressService),nameof(Damaged)));
    }
    static float Modifier(RebirthWorldCharacterRecord record,string target,float value)
    {
        foreach(string id in record.Origin.TraitIds)
        {
            if(!RebirthSurvivorDefinitionRegistry.TryGetTrait(id,out var trait)||
                !RebirthSurvivorDefinitionRegistry.TryGetModifier(trait.ModifierId,out var profile))continue;
            foreach(var component in profile.Components)
                if(component.Phase=="runtime"&&component.Target==target&&
                    float.TryParse(component.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out float amount))
                    value=component.Operation=="multiply"?value*amount:value+amount;
        }
        return value;
    }
    static float Multiplier(RebirthWorldCharacterRecord r)=>Modifier(r,"stress.gain",1);
    static void Damaged(EntityAlive __instance,DamageSource _damageSource,int __result)
    {
        var p=__instance as EntityPlayer;
        if(__result<=0||p==null||p.world.IsRemote()||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        if(!(p.world.GetEntity(_damageSource.getEntityId()) is EntityAlive attacker)||attacker==p)return;
        if(RebirthWorldCharacterService.TryGet(p,out var r)&&r.IsComplete){r.Condition.Stress.Hit(Multiplier(r));RebirthWorldCharacterService.MarkDirty(r,"stress-hit");}
    }
    public static void Drink(EntityPlayer p,string item,float fraction)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()||!RebirthWorldCharacterService.TryGet(p,out var r))return;
        if(item=="rebirthCookingFoodN37")r.Condition.Stress.Drink(fraction);
        else if(item=="rebirthCookingFoodN38")
        {
            if(!r.Support.Entries.TryGetValue("support.calming_tea",out var support))r.Support.Entries["support.calming_tea"]=support=new RebirthTraitSupportRuntimeState{SupportProfileId="support.calming_tea"};
            support.PositiveRemainingActiveSeconds=Math.Min(600,support.PositiveRemainingActiveSeconds+600*Mathf.Clamp01(fraction));support.Stacks=1;
        }
        else return;
        RebirthWorldCharacterService.MarkDirty(r,"calming-infusion");
    }
    public static void Tick(EntityPlayer p,RebirthWorldCharacterRecord r,float dt)
    {
        var s=r.Condition.Stress;
        if(p.IsDead()){r.Condition.Stress=s=new RebirthStressState();Sync(p,s);return;}
        bool threat=false,unseen=false;
        foreach(var entity in p.world.Entities.list)
        {
            var enemy=entity as EntityAlive;
            if(enemy==null||enemy==p||enemy.IsDead()||Vector3.SqrMagnitude(enemy.position-p.position)>225)continue;
            bool hostile=enemy.GetAttackTarget()==p||enemy.GetRevengeTarget()==p;
            if(enemy.EntityClass.bIsEnemyEntity&&enemy.moveDirection!=Vector3.zero&&Vector3.SqrMagnitude(enemy.position-p.position)<=144&&!p.CanSee(enemy))unseen=true;
            if(!hostile)continue;
            threat=true;
            if(!p.CanSee(enemy))unseen=true;
        }
        var pos=new Vector3i(p.position);
        bool trader=p.world.IsWithinTraderArea(pos);
        var persistent=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(p.entityId);
        var claim=persistent==null?EnumLandClaimOwner.None:p.world.GetLandClaimOwner(pos,persistent);
        bool camp=claim==EnumLandClaimOwner.Self||claim.ToString()=="Ally"||RebirthWorkstationSecurityService.IsStressCamp(p);
        bool sheltered=!threat&&(camp||trader);
        s.Location=threat?"In Danger":trader?"Trader Compound":camp?"Established Camp":"Exploring";
        p.world.GetSunAndBlockColors(new Vector3i(p.position+Vector3.up),out byte sky,out byte fixedLight);
        bool dark=sky<4&&fixedLight<4;
        s.Tick(dt,threat,p.Stats.Health.Value<p.Stats.Health.ModifiedMax*.25f,dark,p.world.IsDark(),sheltered,unseen,Modifier(r,"stress.anxiety",0)>0,Modifier(r,"stress.darkness",0)>0,Multiplier(r),
            p.Stats.Food.Value/Math.Max(1,p.Stats.Food.ModifiedMax),
            p.Stats.Water.Value/Math.Max(1,p.Stats.Water.ModifiedMax));
        Sync(p,s);
    }
    static void Sync(EntityPlayer p,RebirthStressState s)
    {
        p.Buffs.SetCustomVar("rebirthStress",s.Value,true);
        p.Buffs.SetCustomVar("rebirthStressLocation",s.Location=="In Danger"?3:s.Location=="Trader Compound"?2:s.Location=="Established Camp"?1:0,true);
        string desired=s.Value>=80?"buffRebirthStressHigh":s.Value>=60?"buffRebirthStressShaken":"";
        foreach(string buff in new[]{"buffRebirthStressHigh","buffRebirthStressShaken"})
            if(buff==desired){if(!p.Buffs.HasBuff(buff))p.Buffs.AddBuff(buff);}else if(p.Buffs.HasBuff(buff))p.Buffs.RemoveBuff(buff);
    }
}
