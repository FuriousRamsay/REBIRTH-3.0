using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
// PREPARED, NOT RUN. Actual Harmony metadata; separate offline process only.
internal static class RebirthHarmonyRollbackFixture
{
    private const string A="rebirth.fixture.rollback.a",B="rebirth.fixture.rollback.b",C="rebirth.fixture.rollback.c";
    private static void One(){} private static void Two(){}
    [HarmonyPatch] private static class Owned
    {
        private static IEnumerable<MethodBase> TargetMethods(){yield return Target("One");yield return Target("Two");}
        [HarmonyPrefix,HarmonyPriority(Priority.High)] private static void Prefix(){}
    }
    [HarmonyPatch] private static class Other
    {
        private static MethodBase TargetMethod()=>Target("One");
        [HarmonyPostfix] private static void Postfix(){}
    }
    [HarmonyPatch] private static class FailedShared
    {
        internal static bool AllowSecond;
        private static IEnumerable<MethodBase> TargetMethods(){yield return Target("One");yield return Target("Two");}
        [HarmonyPrefix] private static void Prefix(){}
        [HarmonyTranspiler] private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,MethodBase original)
        {if(original==Target("Two")&&!AllowSecond)throw new InvalidOperationException("Fixture patch failure");return instructions;}
    }
    internal static void VerifyFailedSharedHook()
    {
        // Separate offline process; do not patch fixture targets in a live game.
        var a=new Harmony(A+".failure");var c=new Harmony(C+".failure");
        try
        {
            var hook=typeof(FailedShared).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic);
            a.Patch(Target("One"),prefix:new HarmonyMethod(hook));
            c.Patch(Target("One"),prefix:new HarmonyMethod(hook));
            Patch foreign=null;foreach(var patch in Harmony.GetPatchInfo(Target("One")).Prefixes)if(patch.owner==c.Id)foreign=patch;
            bool failed=false;try{RebirthHarmonyBootstrap.PatchClassOnce(a,typeof(FailedShared));}catch{failed=true;}
            if(!failed||Count(Target("One"),c.Id,true)!=1)throw new Exception("Failed class removed foreign use of shared hook.");
            bool same=false;foreach(var patch in Harmony.GetPatchInfo(Target("One")).Prefixes)if(ReferenceEquals(patch,foreign))same=true;
            if(!same)throw new Exception("Rollback replaced foreign patch object/order metadata.");
            int retained=Count(Target("One"),a.Id,true);failed=false;
            try{RebirthHarmonyBootstrap.PatchClassOnce(a,typeof(FailedShared));}catch{failed=true;}
            if(!failed||Count(Target("One"),c.Id,true)!=1||Count(Target("One"),a.Id,true)!=retained)
                throw new Exception("Ambiguous failed class replay changed retained hooks.");
            FailedShared.AllowSecond=true;
            RebirthHarmonyBootstrap.PatchClassOnce(a,typeof(FailedShared));
            if(Count(Target("One"),a.Id,true)!=1||Count(Target("Two"),a.Id,true)!=1||Count(Target("One"),c.Id,true)!=1)
                throw new Exception("Successful retry duplicated a pre-existing hook.");
        }
        finally{a.UnpatchSelf();c.UnpatchSelf();}
    }
    private static MethodInfo Target(string name)=>typeof(RebirthHarmonyRollbackFixture).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic);
    private static int Count(MethodBase target,string owner,bool prefix)
    {var info=Harmony.GetPatchInfo(target);if(info==null)return 0;int count=0;foreach(var p in prefix?info.Prefixes:info.Postfixes)if(p.owner==owner)count++;return count;}
    internal static void Verify()
    {
        var a=new Harmony(A);var b=new Harmony(B);var c=new Harmony(C);
        try
        {
            RebirthHarmonyBootstrap.PatchClassOnce(a,typeof(Owned));RebirthHarmonyBootstrap.PatchClassOnce(b,typeof(Other));
            var shared=typeof(Owned).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic);
            c.Patch(Target("One"),prefix:new HarmonyMethod(shared));
            a.Unpatch(Target("One"),HarmonyPatchType.All,A);
            RebirthHarmonyBootstrap.PatchClassOnce(a,typeof(Owned));
            if(Count(Target("One"),A,true)!=1||Count(Target("Two"),A,true)!=1||Count(Target("One"),B,false)!=1||Count(Target("One"),C,true)!=1)
                throw new Exception("Partial repair duplicated hooks or removed another owner.");
            a.UnpatchSelf();RebirthHarmonyBootstrap.PatchClassOnce(a,typeof(Owned));
            if(Count(Target("One"),A,true)!=1||Count(Target("Two"),A,true)!=1||Count(Target("One"),C,true)!=1)
                throw new Exception("Full rollback was falsely cached or foreign shared method removed.");
            RebirthHarmonyBootstrap.PatchClassOnce(a,typeof(Owned));
            if(Count(Target("One"),A,true)!=1)throw new Exception("Repeated repair duplicated hook.");
        }
        finally{a.UnpatchSelf();b.UnpatchSelf();c.UnpatchSelf();}
    }
}
