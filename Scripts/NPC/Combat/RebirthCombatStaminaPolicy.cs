/// <summary>Shared stamina threshold decisions; movement and counterattack remain actuator-owned.</summary>
public static class RebirthCombatStaminaPolicy
{
    private static bool Valid(float stamina,float maximum,float fraction)
    { return !float.IsNaN(stamina)&&!float.IsInfinity(stamina)&&!float.IsNaN(maximum)&&!float.IsInfinity(maximum)&&maximum>0f&&!float.IsNaN(fraction)&&!float.IsInfinity(fraction)&&fraction>=0f; }
    public static bool NeedsRecovery(float stamina,float maximum,float retreatFraction)
    { return Valid(stamina,maximum,retreatFraction)&&stamina<retreatFraction*maximum; }
    public static bool Recovered(float stamina,float maximum,float resumeFraction)
    { return Valid(stamina,maximum,resumeFraction)&&stamina>=resumeFraction*maximum; }
}