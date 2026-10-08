using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable disable

public enum RebirthNpcWeaponSpecialty : byte { Unarmed=0, Blades=1, Clubs=2, Spears=3, Archery=4, Pistols=5, Shotguns=6, Rifles=7, AutomaticWeapons=8, HeavyWeapons=9 }

public sealed class RebirthNpcProgressionModifierSnapshot
{
    public RebirthNpcStableId NpcId;
    public int ConstitutionLevel, StrengthLevel, DexterityLevel, WeaponLevel;
    public float MaximumHealthMultiplier, PhysicalDamageMultiplier, CarryMultiplier;
    public float MovementSpeedMultiplier, ResponsivenessMultiplier, DodgeChance;
    public float WeaponDamageMultiplier, AccuracyMultiplier, ReloadSpeedMultiplier;
    public string WeaponTrack;
}

public static class RebirthNpcProgressionModifierService
{
    public const string ConstitutionTrack="attribute.constitution";
    public const string StrengthTrack="attribute.strength";
    public const string DexterityTrack="attribute.dexterity";
    private static readonly object InitSync=new object();
    private static bool definitionsInitialized;
    private static long evaluations, combatEvaluations, aiEvaluations;

    public static void EnsureInitialized()
    {
        if(definitionsInitialized)return;
        lock(InitSync)
        {
            if(definitionsInitialized)return;
            RebirthNpcProgressionService.RegisterDefinition(new RebirthNpcProgressionDefinition(ConstitutionTrack,RebirthNpcProgressionTrackKind.Attribute));
            RebirthNpcProgressionService.RegisterDefinition(new RebirthNpcProgressionDefinition(StrengthTrack,RebirthNpcProgressionTrackKind.Attribute));
            RebirthNpcProgressionService.RegisterDefinition(new RebirthNpcProgressionDefinition(DexterityTrack,RebirthNpcProgressionTrackKind.Attribute));
            foreach(RebirthNpcWeaponSpecialty specialty in Enum.GetValues(typeof(RebirthNpcWeaponSpecialty)))
                RebirthNpcProgressionService.RegisterDefinition(new RebirthNpcProgressionDefinition(WeaponTrack(specialty),RebirthNpcProgressionTrackKind.WeaponSpecialty));
            definitionsInitialized=true;
        }
    }

    public static string WeaponTrack(RebirthNpcWeaponSpecialty specialty){return "weapon."+specialty.ToString().ToLowerInvariant();}

    public static RebirthNpcProgressionModifierSnapshot Evaluate(RebirthNpcStableId npcId,RebirthNpcWeaponSpecialty specialty)
    {
        EnsureInitialized(); Interlocked.Increment(ref evaluations);
        int c=Level(npcId,ConstitutionTrack),s=Level(npcId,StrengthTrack),d=Level(npcId,DexterityTrack),w=Level(npcId,WeaponTrack(specialty));
        float cn=c-1, st=s-1, dx=d-1, wp=w-1;
        return new RebirthNpcProgressionModifierSnapshot{
            NpcId=npcId,ConstitutionLevel=c,StrengthLevel=s,DexterityLevel=d,WeaponLevel=w,WeaponTrack=WeaponTrack(specialty),
            MaximumHealthMultiplier=1f+cn*0.025f,
            PhysicalDamageMultiplier=1f+st*0.02f,
            CarryMultiplier=1f+st*0.025f,
            MovementSpeedMultiplier=1f+dx*0.006f,
            ResponsivenessMultiplier=1f+dx*0.015f,
            DodgeChance=Math.Min(0.19f,dx*0.01f),
            WeaponDamageMultiplier=1f+wp*0.02f,
            AccuracyMultiplier=1f+wp*0.015f+dx*0.005f,
            ReloadSpeedMultiplier=1f+wp*0.0125f+dx*0.005f};
    }

    public static int ScaleMaximumHealth(RebirthNpcStableId id,int baseHealth){var m=Evaluate(id,RebirthNpcWeaponSpecialty.Unarmed);return Math.Max(1,(int)Math.Round(baseHealth*m.MaximumHealthMultiplier));}
    public static int ScalePhysicalDamage(RebirthNpcStableId id,RebirthNpcWeaponSpecialty specialty,int baseDamage){Interlocked.Increment(ref combatEvaluations);var m=Evaluate(id,specialty);return Math.Max(1,(int)Math.Round(baseDamage*m.PhysicalDamageMultiplier*m.WeaponDamageMultiplier));}
    public static float ScaleMoveSpeed(RebirthNpcStableId id,float baseSpeed){Interlocked.Increment(ref aiEvaluations);return Math.Max(0f,baseSpeed*Evaluate(id,RebirthNpcWeaponSpecialty.Unarmed).MovementSpeedMultiplier);}
    public static float GetResponsiveness(RebirthNpcStableId id){Interlocked.Increment(ref aiEvaluations);return Evaluate(id,RebirthNpcWeaponSpecialty.Unarmed).ResponsivenessMultiplier;}
    public static bool RollDodge(RebirthNpcStableId id,int deterministicRoll0To9999){Interlocked.Increment(ref combatEvaluations);int roll=Math.Max(0,Math.Min(9999,deterministicRoll0To9999));return roll < (int)(Evaluate(id,RebirthNpcWeaponSpecialty.Unarmed).DodgeChance*10000f);}
    public static string GetReport(){return "[REBIRTH NPC ACIP-02 Modifiers] evaluations="+Interlocked.Read(ref evaluations)+" combat="+Interlocked.Read(ref combatEvaluations)+" ai="+Interlocked.Read(ref aiEvaluations);}
    private static int Level(RebirthNpcStableId id,string track){RebirthNpcProgressionView v;return RebirthNpcProgressionService.TryGetView(id,track,out v)?v.Level:1;}
}

public sealed class RebirthNpcCombatProgressionEvent
{
    public Guid EventId; public RebirthNpcStableId NpcId; public RebirthNpcWeaponSpecialty Specialty;
    public int BaseDamage; public bool Hit; public bool ReloadCompleted; public int AmmoConsumed; public string SourceId;
}

public sealed class RebirthNpcWeaponUseResult
{
    public bool Accepted; public string Code; public int ScaledDamage; public float AccuracyMultiplier; public float ReloadSpeedMultiplier; public int AmmoConsumed; public long Revision;
}

public static class RebirthNpcWeaponProgressionService
{
    private static readonly object Sync=new object();
    private static readonly HashSet<Guid> CommittedEvents=new HashSet<Guid>();
    private static readonly Dictionary<RebirthNpcStableId,long> Revisions=new Dictionary<RebirthNpcStableId,long>();
    private static long accepted,rejected,duplicates,xpAwards;

    public static RebirthNpcWeaponUseResult Commit(RebirthNpcCombatProgressionEvent e)
    {
        if(!IsServer())return Reject("not-authority");
        if(e==null||e.EventId==Guid.Empty||e.NpcId.IsEmpty||string.IsNullOrWhiteSpace(e.SourceId)||e.BaseDamage<0||e.AmmoConsumed<0)return Reject("invalid-request");
        lock(Sync){if(!CommittedEvents.Add(e.EventId)){Interlocked.Increment(ref duplicates);return Reject("duplicate-event");}
            long revision;Revisions.TryGetValue(e.NpcId,out revision);revision++;Revisions[e.NpcId]=revision;
            var m=RebirthNpcProgressionModifierService.Evaluate(e.NpcId,e.Specialty);
            int scaled=e.Hit?Math.Max(1,(int)Math.Round(e.BaseDamage*m.PhysicalDamageMultiplier*m.WeaponDamageMultiplier)):0;
            long xp=e.Hit?Math.Max(1,Math.Min(250,e.BaseDamage)):1;if(e.ReloadCompleted)xp+=5;
            var award=RebirthNpcProgressionService.Award(new RebirthNpcProgressionAwardRequest{AwardId=e.EventId,NpcId=e.NpcId,ProgressionId=RebirthNpcProgressionModifierService.WeaponTrack(e.Specialty),RequestedXp=xp,SourceType="combat.weapon",SourceId=e.SourceId,CreatedUtcTicks=DateTime.UtcNow.Ticks});
            if(award.Accepted)Interlocked.Increment(ref xpAwards);Interlocked.Increment(ref accepted);
            return new RebirthNpcWeaponUseResult{Accepted=true,Code="accepted",ScaledDamage=scaled,AccuracyMultiplier=m.AccuracyMultiplier,ReloadSpeedMultiplier=m.ReloadSpeedMultiplier,AmmoConsumed=e.AmmoConsumed,Revision=revision};}
    }
    public static void ResetForWorldChange(){lock(Sync){CommittedEvents.Clear();Revisions.Clear();}}
    public static string GetReport(){lock(Sync)return "[REBIRTH NPC ACIP-02 Weapon Use] actors="+Revisions.Count+" events="+CommittedEvents.Count+" accepted="+accepted+" rejected="+rejected+" duplicates="+duplicates+" xpAwards="+xpAwards;}
    private static RebirthNpcWeaponUseResult Reject(string code){Interlocked.Increment(ref rejected);return new RebirthNpcWeaponUseResult{Accepted=false,Code=code};}
    private static bool IsServer(){ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;return c==null||c.IsServer;}
}
