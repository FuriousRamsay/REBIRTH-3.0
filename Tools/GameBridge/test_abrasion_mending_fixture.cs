using System;
public class EntityAlive { public Buffs Buffs=new Buffs(); }
public class EntityPlayer:EntityAlive { public float Skill; public bool HasSkill=true; }
public class Buffs
{
    public bool Treated; public float Multiplier; public int Writes;
    public bool HasBuff(string id){return Treated;}
    public float GetCustomVar(string id){return Multiplier;}
    public void SetCustomVar(string id,float value){Multiplier=value;Writes++;}
}
public static class Mathf { public static float Clamp(float x,float min,float max){return Math.Max(min,Math.Min(max,x));} }
public static class RebirthProgressionRuntimeConfig
{
    public static float MedicineFractureHealingNegative=-.15f,MedicineFractureHealingPositive=.20f;
}
public static class Test
{
    static EntityPlayer LookupOwner;
    static bool TryGetSkillValue(EntityPlayer owner,string skill,out float value)
    { LookupOwner=owner;value=owner.Skill;return owner.HasSkill; }
// PRODUCTION_CLASS
    static void Check(float actual,float expected){if(Math.Abs(actual-expected)>.0001f)throw new Exception("Abrasion scaling mismatch");}
    public static void Main()
    {
        Check(ComputeNewAbrasionMultiplier(true,false,true,2f,0f),2f);
        Check(ComputeNewAbrasionMultiplier(true,false,true,2f,.20f),2.4f);
        Check(ComputeNewAbrasionMultiplier(true,false,true,2f,-.15f),1.7f);
        Check(ComputeNewAbrasionMultiplier(true,true,true,2.4f,.20f),2.4f);
        Check(ComputeNewAbrasionMultiplier(false,false,true,2f,.20f),2f);
        Check(ComputeNewAbrasionMultiplier(true,false,false,2f,.20f),2f);
        Check(ComputeNewAbrasionMultiplier(true,false,true,0f,.20f),0f);
        Check(ComputeNewAbrasionMultiplier(true,false,true,2f,float.NaN),2f);
        Check(ComputeNewAbrasionMultiplier(true,false,true,2f,-5f),.2f);
        var healer=new EntityPlayer{Skill=100};
        var patient=new EntityPlayer{Skill=-50}; patient.Buffs.Treated=true;patient.Buffs.Multiplier=2;
        AdjustNewAbrasionTreatment(healer,patient,true,false);
        Check(patient.Buffs.Multiplier,2.4f); if(LookupOwner!=healer)throw new Exception("Wrong skill owner");
        patient.Skill=100; patient.Buffs.Multiplier=2; healer.Skill=-50;
        AdjustNewAbrasionTreatment(healer,patient,true,false);Check(patient.Buffs.Multiplier,1.7f);
        int writes=patient.Buffs.Writes;
        AdjustNewAbrasionTreatment(healer,patient,true,true);Check(patient.Buffs.Multiplier,1.7f);
        if(patient.Buffs.Writes!=writes)throw new Exception("Repeated write");
        patient.Buffs.Multiplier=2;healer.HasSkill=false;
        AdjustNewAbrasionTreatment(healer,patient,true,false);Check(patient.Buffs.Multiplier,2);
        healer.HasSkill=true;patient.Buffs.Treated=false;
        AdjustNewAbrasionTreatment(healer,patient,true,false);Check(patient.Buffs.Multiplier,2);
        AdjustNewAbrasionTreatment(null,patient,true,false);
        AdjustNewAbrasionTreatment(healer,null,true,false);
        Console.WriteLine("PASS actual abrasion arithmetic: skill endpoints, new treatment, no compounding, failed treatment");
    }
}

