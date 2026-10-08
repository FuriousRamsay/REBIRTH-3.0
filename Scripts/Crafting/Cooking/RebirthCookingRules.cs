using System;

/// <summary>Bounded cooking tuning shared by preview and queued output. Skill never gates an attempt.</summary>
public static class RebirthCookingRules
{
    public const float BookMultiplier = 1.2f;
    public const float TimeMultiplier = .8f;
    public static float Proficiency(float skill) => Math.Max(0, Math.Min(100, skill)) / 100f;
    public static float NutritionRetention(float skill) => .9f + .1f * Proficiency(skill);
    public static float ComfortMultiplier(float skill) => .75f + .25f * Proficiency(skill);
    public static string Quality(float skill) => skill < 25 ? "Rough" : skill < 75 ? "Standard" : "Well prepared";
    public static float TechniqueComfort(string effect, bool compatibleHerb) => effect == "Q" || effect == "H" && compatibleHerb ? 1f : 0f;
    public static float TechniqueTime(string effect) => effect == "T" ? TimeMultiplier : 1f;
    public static float Nutrition(bool improvised, float inputsPerServing, float authored, float originalInputs, float skill)
    {
        if(improvised)return Math.Max(0,inputsPerServing)*(1.05f+.05f*Proficiency(skill));
        float adjusted=Math.Max(0,authored)*(originalInputs>0?Math.Max(.9f,Math.Min(1.1f,inputsPerServing/originalInputs)):1);
        return adjusted*NutritionRetention(skill);
    }
    public static float Comfort(float authored,float skill,bool seasoned,string technique)
        => (authored>0?authored*ComfortMultiplier(skill):authored)+(seasoned?2:0)+TechniqueComfort(technique,seasoned);
}
