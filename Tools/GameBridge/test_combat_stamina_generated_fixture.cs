/// <summary>Shared stamina threshold decisions; movement and counterattack remain actuator-owned.</summary>
public static class RebirthCombatStaminaPolicy
{
    private static bool Valid(float stamina,float maximum,float fraction)
    { return !float.IsNaN(stamina)&&!float.IsInfinity(stamina)&&!float.IsNaN(maximum)&&!float.IsInfinity(maximum)&&maximum>0f&&!float.IsNaN(fraction)&&!float.IsInfinity(fraction)&&fraction>=0f; }
    public static bool NeedsRecovery(float stamina,float maximum,float retreatFraction)
    { return Valid(stamina,maximum,retreatFraction)&&stamina<retreatFraction*maximum; }
    public static bool Recovered(float stamina,float maximum,float resumeFraction)
    { return Valid(stamina,maximum,resumeFraction)&&stamina>=resumeFraction*maximum; }
}public static class CombatStaminaFixture {
private static void Check(bool pass,string name){if(!pass)throw new System.Exception(name);}
public static string Run(){
Check(RebirthCombatStaminaPolicy.NeedsRecovery(32,100,0.33f),"Below retreat");
Check(!RebirthCombatStaminaPolicy.NeedsRecovery(33,100,0.33f),"Exact retreat does not begin");
Check(!RebirthCombatStaminaPolicy.Recovered(74,100,0.75f),"Below resume");
Check(RebirthCombatStaminaPolicy.Recovered(75,100,0.75f),"Exact resume");
Check(RebirthCombatStaminaPolicy.NeedsRecovery(-1,100,0.33f),"Negative stamina depletion");
Check(!RebirthCombatStaminaPolicy.NeedsRecovery(float.NaN,100,0.33f),"Nonfinite stamina");
Check(!RebirthCombatStaminaPolicy.Recovered(75,0,0.75f),"Zero maximum");
Check(!RebirthCombatStaminaPolicy.Recovered(75,float.PositiveInfinity,0.75f),"Nonfinite maximum");
Check(!RebirthCombatStaminaPolicy.NeedsRecovery(32,100,-1),"Invalid fraction");
Check(RebirthCombatStaminaPolicy.NeedsRecovery(10,20,2.2f),"Bridge power-mix absolute threshold preserved");
return "PASS: actual shared stamina policy,10 threshold/malformed/bridge power-mix cases.";
}
}