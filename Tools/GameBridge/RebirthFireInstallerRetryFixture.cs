using System;
using System.Reflection;
using HarmonyLib;
// PREPARED, NOT RUN. Isolated offline process only, after cumulative coding.
internal static class RebirthFireInstallerRetryFixture
{
    private static void Target(){}
    public static void Prefix(){}
    internal static void Verify()
    {
        var target=typeof(RebirthFireInstallerRetryFixture).GetMethod("Target",BindingFlags.Static|BindingFlags.NonPublic);
        var hook=typeof(RebirthFireInstallerRetryFixture).GetMethod("Prefix");
        var patch=typeof(RebirthFirePatchInstaller).GetMethod("PatchPrefix",BindingFlags.Static|BindingFlags.NonPublic);
        var owner=new Harmony(RebirthFirePatchInstaller.HarmonyId);
        try
        {
            patch.Invoke(null,new object[]{target,hook});patch.Invoke(null,new object[]{target,hook});
            int count=0;foreach(var value in Harmony.GetPatchInfo(target).Prefixes)if(value.owner==RebirthFirePatchInstaller.HarmonyId&&value.PatchMethod==hook)count++;
            if(count!=1)throw new Exception("Installer retry duplicated successful fire hook.");
            var bootstrap=typeof(Rebirth.Bootstrap.RebirthFreshInit);
            var run=bootstrap.GetMethod("RunStepOnce",BindingFlags.Static|BindingFlags.NonPublic);
            string key="fixture-fire-retry-"+Guid.NewGuid().ToString("N");int attempts=0;
            try{run.Invoke(null,new object[]{key,(Action)(()=>{attempts++;throw new InvalidOperationException("injected installer refusal");})});throw new Exception("Failure swallowed.");}
            catch(TargetInvocationException ex){if(!(ex.InnerException is InvalidOperationException))throw;}
            run.Invoke(null,new object[]{key,(Action)(()=>attempts++)});
            run.Invoke(null,new object[]{key,(Action)(()=>attempts++)});
            if(attempts!=2)throw new Exception("Failed bootstrap step was not retried exactly once.");
        }
        finally{owner.Unpatch(target,hook);}
    }
}
