using UnityEngine;

// Shared by runtime presentation and the Unity bundle verification. The native
// modifier remains the only source of truth; this helper owns no saved state.
internal static class RebirthCamperBodyVisibility
{
    internal const string PickupBodywork = "REBIRTHPickupOnlyBodywork";
    internal const string PickupPaint = "REBIRTHPickupOnlyPaint";

    internal static void Apply(bool hasCamperBody, GameObject camperBody, GameObject pickupBodywork, GameObject pickupPaint)
    {
        Set(camperBody, hasCamperBody);
        Set(pickupBodywork, !hasCamperBody);
        Set(pickupPaint, !hasCamperBody);
    }

    private static void Set(GameObject target, bool active)
    {
        if (target && target.activeSelf != active) target.SetActive(active);
    }
}
