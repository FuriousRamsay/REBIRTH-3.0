using System;
using System.Collections.Generic;

// Base practical-skill gains. Player learning modifiers are applied once by SkillAwardService.
public static class RebirthDifficultyPractice
{
    public static readonly float[] Levels = { 0,10,20,30,50,75,99 };
    private static readonly Dictionary<string,float[]> Crafts = new Dictionary<string,float[]>(StringComparer.OrdinalIgnoreCase)
    {
        {"medicalBandage",new[]{.015f,.015f,.0075f,0,0,0,0}},
        {"medicalFirstAidBandage",new[]{.040f,.040f,.040f,.0267f,0,0,0}},
        {"medicalFirstAidKit",new[]{.120f,.120f,.120f,.120f,.096f,.036f,.024f}},
        {"medicalAloeCream",new[]{.010f,.0075f,.0025f,0,0,0,0}},
        {"medicalSplint",new[]{.020f,.020f,.0133f,.0067f,0,0,0}},
        {"medicalPlasterCast",new[]{.055f,.055f,.055f,.055f,.0275f,0,0}},
        {"drugHerbalAntibiotics",new[]{.060f,.060f,.060f,.060f,.0343f,0,0}},
        {"drugAntibiotics",new[]{.140f,.140f,.140f,.140f,.140f,.070f,.035f}},
        {"drugSteroids",new[]{.100f,.100f,.100f,.100f,.100f,.0375f,.015f}},
        {"drugFortBites",new[]{.080f,.080f,.080f,.080f,.070f,.020f,.008f}},
        {"drugRecog",new[]{.090f,.090f,.090f,.090f,.0788f,.0225f,.009f}}
    };
    private static readonly Dictionary<string,float[]> Chemistry = new Dictionary<string,float[]>(StringComparer.OrdinalIgnoreCase)
    {
        {"resourceGlue",new[]{.020f,.010f,0}}, {"resourceGunPowder",new[]{.015f,.008f,0}},
        {"resourceOil",new[]{.020f,.015f,.005f}}, {"ammoGasCan",new[]{.002f,.001f,0}},
        {"chemistryStation",new[]{.080f,.050f,.020f}}
    };
    private static readonly Dictionary<string,float[]> Treatments = new Dictionary<string,float[]>(StringComparer.OrdinalIgnoreCase)
    {
        {"medicalBandage",new[]{.010f,.0075f,.005f,.0025f,0,0,0}},
        {"medicalFirstAidBandage",new[]{.030f,.027f,.024f,.021f,.015f,.0075f,.003f}},
        {"medicalFirstAidKit",new[]{.100f,.090f,.080f,.070f,.050f,.025f,.010f}},
        {"medicalAloeCream",new[]{.010f,.009f,.008f,.007f,.005f,.0025f,.001f}},
        {"medicalSplint",new[]{.020f,.0167f,.0133f,.010f,.0033f,0,0}},
        {"medicalPlasterCast",new[]{.050f,.045f,.040f,.035f,.025f,.0125f,.005f}},
        {"drugHerbalAntibiotics",new[]{.020f,.018f,.016f,.014f,.010f,.005f,.002f}},
        {"drugAntibiotics",new[]{.050f,.045f,.040f,.035f,.025f,.0125f,.005f}},
        {"drugSteroids",new[]{.010f,.008f,.006f,.004f,0,0,0}},
        {"drugVitamins",new[]{.005f,.0033f,.0017f,0,0,0,0}},
        {"drugPainkillers",new[]{.040f,.036f,.032f,.028f,.020f,.010f,.004f}},
        {"foodHoney",new[]{.010f,.009f,.008f,.007f,.005f,.0025f,.001f}}
    };
    public static IEnumerable<string> TreatmentItems
    {
        get { foreach(string id in Treatments.Keys) yield return id; yield return "resourceSewingKit"; }
    }
    // Sewing uses the existing bandage practice curve; crafting remains separate.
    private static string TreatmentProfile(string id)=>id=="resourceSewingKit"?"medicalBandage":id;
    public static bool HasTreatment(string id)=>id!=null && Treatments.ContainsKey(TreatmentProfile(id));
    public static float Treatment(string id,float level)=>id!=null && Treatments.TryGetValue(TreatmentProfile(id),out var curve)?Interpolate(Levels,curve,level):0f;
    public static bool TryCraft(string id,string skill,float level,int outputs,out float gain)
    {
        gain=0;
        if(skill=="skill.medicine" && Crafts.TryGetValue(id??"",out var medical)){gain=Interpolate(Levels,medical,level);return true;}
        if(skill=="skill.chemistry" && Chemistry.TryGetValue(id??"",out var chemistry))
        {
            // One completed recipe cycle is one process-training event. Recipe output stack size
            // is product packaging, not additional chemical work, and must not multiply Skill gain.
            gain=Interpolate(new float[]{0,30,75,99},new[]{chemistry[0],chemistry[1],chemistry[2],chemistry[2]*.4f},level);return true;
        }
        return false;
    }
    public static float Interpolate(float[] levels,float[] values,float level)
    {
        if(float.IsNaN(level)||float.IsInfinity(level))return 0;
        if(level<=levels[0])return values[0];
        for(int i=1;i<levels.Length;i++)if(level<=levels[i])return values[i-1]+(values[i]-values[i-1])*(level-levels[i-1])/(levels[i]-levels[i-1]);
        return values[values.Length-1];
    }
    public static float Cooking(float level,int ingredients,float seconds)
    {
        int variety=Math.Max(1,Math.Min(6,ingredients));
        float raw=Math.Min(.12f,.008f+(variety-1)*.018f+Math.Min(120,Math.Max(0,seconds))*.00015f);
        float difficulty=Math.Min(75,5+(variety-1)*12+Math.Min(120,Math.Max(0,seconds))*.08f);
        return raw*Math.Max(variety<=2?0f:.1f,1-Math.Max(0,level-difficulty)/30f);
    }
}
