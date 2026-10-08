using System;

/// <summary>Native four-metre external injury treatment boundary, rechecked at use.</summary>
public static class RebirthExternalTreatmentTargetPolicy
{
    public static bool IsExternalTreatment(string itemName)
    {
        switch(itemName)
        {
            case "medicalBandage": case "medicalFirstAidBandage": case "medicalFirstAidKit":
            case "medicalAloeCream": case "medicalSplint": case "medicalPlasterCast": case "resourceSewingKit":
                return true;
            default: return false;
        }
    }

    public static bool Allows(bool actorAlive, bool patientAlive, bool patientIsPlayer,
        bool sameWorld, bool sameEntity, float distanceSquared)
    {
        return actorAlive && patientAlive && patientIsPlayer && sameWorld && !sameEntity &&
            !float.IsNaN(distanceSquared) && !float.IsInfinity(distanceSquared) &&
            distanceSquared >= 0f && distanceSquared <= 16f;
    }
}
