using System;
using System.Reflection;
// PREPARED, NOT RUN. Actual stage helper, isolated offline fixture process.
internal static class RebirthSurvivorInstallStageFixture
{
    internal static void Verify()
    {
        var method=typeof(RebirthSurvivorPersistenceLifecycle).GetMethod("RegisterStage",BindingFlags.Static|BindingFlags.NonPublic);
        int calls=0;object[] failed={false,(Action)(()=>{calls++;throw new InvalidOperationException("injected registration failure");})};
        try{method.Invoke(null,failed);throw new Exception("Registration failure swallowed.");}
        catch(TargetInvocationException error){if(!(error.InnerException is InvalidOperationException))throw;}
        if((bool)failed[0])throw new Exception("Failed registration recorded complete.");
        object[] retry={false,(Action)(()=>calls++)};method.Invoke(null,retry);
        if(!(bool)retry[0])throw new Exception("Successful registration not recorded.");
        method.Invoke(null,retry);if(calls!=2)throw new Exception("Completed subscription repeated.");
    }
}
