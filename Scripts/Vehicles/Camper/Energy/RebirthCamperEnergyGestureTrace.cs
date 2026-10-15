using System;

// Actual gesture/admission boundaries only. No native item blobs, ownership
// tokens or per-frame inventory logs; a window opening has a finite budget.
internal static class RebirthCamperEnergyGestureTrace
{
    private const bool DebugLog=true;
    private static int remaining;
    internal static void Reset(){remaining=32;}
    internal static string Stack(ItemStack stack)=>"type="+(stack?.itemValue?.type??-1)+" count="+(stack?.count??-1);
    internal static void Write(string stage,string detail)
    {
        if(!DebugLog||remaining<=0||!ThreadManager.IsMainThread())return;
        remaining--;
        try
        {
            string clean=(detail??string.Empty).Replace('\r',' ').Replace('\n',' ');if(clean.Length>700)clean=clean.Substring(0,700);
            Log.Out("[REBIRTH EnergyGesture] frame="+UnityEngine.Time.frameCount+" stage="+stage+" "+clean);
        }
        catch{} // Diagnostics cannot change gesture admission.
    }
}
