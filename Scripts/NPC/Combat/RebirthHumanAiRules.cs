// Engine-independent historical restrictions, shared by EAI and source fixtures.
internal static class RebirthHumanAiRules
{
    internal static float RetreatDistance(bool targetIsPlayer)=>targetIsPlayer?5f:4f;
    internal static bool CanRetreat(bool owned,bool following)=>!owned||following;
    internal static float AcquisitionDistance(bool offline,bool fullControlFollow,float hunting,float fullControl)=>offline?3f:fullControlFollow?fullControl:hunting;
    internal static bool InsideAcquisition(float distance,float ownerDistance,float range,bool bypass,bool eventPlayer)
        =>!float.IsNaN(distance)&&!float.IsInfinity(distance)&&distance>=0f&&
            (eventPlayer&&distance<=120f||bypass&&distance<=50f||distance<range||ownerDistance>=0f&&ownerDistance<range+5f);
    internal static bool TracksElevatedTarget(bool melee,bool noMeleeTracking,float heightDifference)=>!melee||!noMeleeTracking||heightDifference<=4f;
}
