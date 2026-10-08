using System;
using System.Text;

#nullable disable

public static class RebirthNpcProgressionAcip02Qualification
{
    public static string Run()
    {
        RebirthNpcProgressionModifierService.EnsureInitialized();var id=RebirthNpcStableId.NewId();
        Award(id,RebirthNpcProgressionModifierService.ConstitutionTrack,4000,"constitution");
        Award(id,RebirthNpcProgressionModifierService.StrengthTrack,4000,"strength");
        Award(id,RebirthNpcProgressionModifierService.DexterityTrack,4000,"dexterity");
        var use=RebirthNpcWeaponProgressionService.Commit(new RebirthNpcCombatProgressionEvent{EventId=Guid.NewGuid(),NpcId=id,Specialty=RebirthNpcWeaponSpecialty.Rifles,BaseDamage=100,Hit=true,ReloadCompleted=true,AmmoConsumed=1,SourceId="qualification"});
        var m=RebirthNpcProgressionModifierService.Evaluate(id,RebirthNpcWeaponSpecialty.Rifles);
        bool pass=m.MaximumHealthMultiplier>1f&&m.PhysicalDamageMultiplier>1f&&m.MovementSpeedMultiplier>1f&&m.DodgeChance>0f&&use.Accepted&&use.ScaledDamage>100&&use.AccuracyMultiplier>=1f&&use.ReloadSpeedMultiplier>=1f;
        return "[REBIRTH NPC ACIP-02 Qualification] result="+(pass?"PASS":"FAIL")+" constitution="+m.ConstitutionLevel+" strength="+m.StrengthLevel+" dexterity="+m.DexterityLevel+" weapon="+m.WeaponLevel+" damage="+use.ScaledDamage;
    }
    private static void Award(RebirthNpcStableId id,string track,long xp,string source){RebirthNpcProgressionService.Award(new RebirthNpcProgressionAwardRequest{AwardId=Guid.NewGuid(),NpcId=id,ProgressionId=track,RequestedXp=xp,SourceType="qualification",SourceId=source,CreatedUtcTicks=DateTime.UtcNow.Ticks,IsAdministrative=true});}
}
