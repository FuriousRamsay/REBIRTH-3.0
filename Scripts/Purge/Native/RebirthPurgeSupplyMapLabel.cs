using System;

// Decorate the native marker before HandleNavObject constructs its native packet.
// Entity ownership remains in the existing crate stamp; this is presentation only.
internal static class RebirthPurgeSupplyMapLabel
{
    internal static void AfterRegister(Entity __1,NavObject __result,bool __runOriginal)
    {
        var crate=__1 as EntitySupplyCrate;
        RebirthPurgeSupplyCrateStamp stamp;
        if(!__runOriginal||__result==null||crate==null||
           !RebirthPurgeSupplyCrateSerialization.TryReadProjection(crate,out stamp))return;
        string owner=RebirthPurgeDiscoveryNotification.CleanDisplayName(crate.spawnByName);
        if(string.IsNullOrEmpty(owner))return;
        SetName(__result,owner);
        RebirthPurgeSupplyMarkerLabels.Capture(crate,owner);
    }
    internal static void SetName(NavObject nav,string owner)
    {nav.name=owner;nav.usingLocalizationId=false;nav.localizedName=null;}
}
