using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

#nullable disable

/// <summary>
/// Compatibility bridge for the existing targeted Harmony installers.
///
/// The attempted assembly-wide PatchAll migration was rolled back because the
/// current assembly still contains option-gated, diagnostic, runtime-toggle and
/// version-sensitive patch classes that are not safe to install globally.
/// </summary>
public static class RebirthHarmonyBootstrap
{
    private static readonly object Sync = new object();
    private static readonly HashSet<Type> PatchedTypes = new HashSet<Type>();
    private sealed class InstalledPatch { internal MethodBase Original; internal MethodInfo Method; internal string Owner; internal HarmonyPatchType Kind; internal int Priority; internal string[] Before,After; internal Patch NativePatch; }
    private static readonly Dictionary<Type,List<InstalledPatch>> Installed = new Dictionary<Type,List<InstalledPatch>>();
    private static readonly HashSet<Type> RetainedFailedClasses=new HashSet<Type>();

    private static bool initialized;
    private static string lastError = string.Empty;
    private static string lastPatchClass = string.Empty;
    private static int patchedClassCount;

    public static bool IsApplyingPatchAll { get { return false; } }
    public static bool IsPatchAllComplete { get { return false; } }
    public static string LastError { get { return lastError; } }
    public static string LastPatchClass { get { return lastPatchClass; } }
    public static int ProductionPatchClassCount { get { return patchedClassCount; } }
    public static int ManualPatchClassCount { get { return 0; } }

    public static string BuildStatus()
    {
        return "[REBIRTH Harmony] mode=targeted-installers"
            + " patchAllComplete=False"
            + " patchedClasses=" + patchedClassCount
            + " lastPatchClass=" + (string.IsNullOrEmpty(lastPatchClass) ? "none" : lastPatchClass)
            + " lastError=" + (string.IsNullOrEmpty(lastError) ? "none" : lastError);
    }

    public static void EnsurePatched()
    {
        if (initialized)
            return;

        lock (Sync)
        {
            if (initialized)
                return;

            initialized = true;
            lastError = string.Empty;
            if (RebirthLogSettings.HarmonyPatchLoggingEnabled) Log.Out("[REBIRTH Harmony] Targeted installer mode active; assembly-wide PatchAll is disabled.");
        }
    }

    /// <summary>
    /// Installs one explicitly owned patch class and prevents the same class from
    /// being installed twice through different feature bootstraps.
    /// </summary>
    public static bool PatchClassOnce(Harmony harmony, Type patchType)
    {
        if (harmony == null)
            throw new ArgumentNullException(nameof(harmony));
        if (patchType == null)
            throw new ArgumentNullException(nameof(patchType));

        EnsurePatched();

        lock (Sync)
        {
            if(RetainedFailedClasses.Contains(patchType))
                throw new InvalidOperationException("Failed patch class retains a shared hook; automatic class replay refused: "+patchType.FullName);
            if (PatchedTypes.Contains(patchType))
            {
                if(Installed.TryGetValue(patchType,out var expected)&&StillInstalled(expected))return false;
                // A feature may have rolled back its Harmony owner after this
                // class succeeded. Cache membership is not installation proof.
                if(expected!=null&&expected.Count>0)
                {
                    // Restore only missing exact hooks. Unpatch(MethodInfo)
                    // would also remove another owner's use of that method.
                    try
                    {
                        foreach(var item in expected)if(!StillInstalled(new List<InstalledPatch>{item}))Restore(item);
                        if(!StillInstalled(expected))throw new InvalidOperationException("Targeted patch restoration is incomplete: "+patchType.FullName);
                        lastError=string.Empty;return true;
                    }
                    catch(Exception error)
                    {lastPatchClass=patchType.FullName;lastError=lastPatchClass+": "+error.GetType().Name+": "+error.Message;throw;}
                }
                PatchedTypes.Remove(patchType);Installed.Remove(patchType);
                patchedClassCount=Math.Max(0,patchedClassCount-1);
            }

            var beforeAttempt=Capture(patchType,harmony.Id);
            try
            {
                lastPatchClass = patchType.FullName ?? patchType.Name;
                { if (RebirthLogSettings.HarmonyPatchLoggingEnabled) RebirthLogSettings.TraceHarmonyPatch("BEGIN harmony=" + harmony.Id + " class=" + lastPatchClass); }
                harmony.CreateClassProcessor(patchType).Patch();
                RemoveDuplicateHooks(harmony,patchType,beforeAttempt);
                Installed[patchType]=Capture(patchType,harmony.Id);
                PatchedTypes.Add(patchType);
                patchedClassCount++;
                lastError = string.Empty;
                { if (RebirthLogSettings.HarmonyPatchLoggingEnabled) RebirthLogSettings.TraceHarmonyPatch("PASS harmony=" + harmony.Id + " class=" + lastPatchClass); }
                return true;
            }
            catch (Exception ex)
            {
                var added=new List<InstalledPatch>();
                foreach(var partial in Capture(patchType,harmony.Id))
                    if(!beforeAttempt.Exists(prior=>ReferenceEquals(prior.NativePatch,partial.NativePatch)))added.Add(partial);
                var originals=new HashSet<MethodBase>();
                foreach(var partial in added)originals.Add(partial.Original);
                foreach(var original in originals)
                    try{RemoveAttempt(harmony,original,added);}catch{RetainedFailedClasses.Add(patchType);}
                Installed.Remove(patchType);
                lastError = patchType.FullName + ": " + ex.GetType().Name + ": " + ex.Message;
                Log.Error("[REBIRTH Harmony][PatchClass] FAIL harmony=" + harmony.Id + " class=" + lastPatchClass);
                Log.Error("[REBIRTH Harmony][PatchClass] exception=" + ex.ToString());
                Exception inner = ex.InnerException;
                int depth = 0;
                while (inner != null && depth < 8)
                {
                    Log.Error("[REBIRTH Harmony][PatchClass] inner[" + depth + "]=" + inner.ToString());
                    inner = inner.InnerException;
                    depth++;
                }
                throw;
            }
        }
    }

    private static List<InstalledPatch> Capture(Type type,string owner)
    {
        var result=new List<InstalledPatch>();
        foreach(var original in new List<MethodBase>(Harmony.GetAllPatchedMethods()))
        {
            var info=Harmony.GetPatchInfo(original);if(info==null)continue;
            Add(result,original,info.Prefixes,type,owner,HarmonyPatchType.Prefix);Add(result,original,info.Postfixes,type,owner,HarmonyPatchType.Postfix);
            Add(result,original,info.Transpilers,type,owner,HarmonyPatchType.Transpiler);Add(result,original,info.Finalizers,type,owner,HarmonyPatchType.Finalizer);
        }
        return result;
    }
    private static void Add(List<InstalledPatch> result,MethodBase original,IEnumerable<Patch> patches,Type type,string owner,HarmonyPatchType kind)
    {
        foreach(var patch in patches)
            if(patch.owner==owner&&patch.PatchMethod?.DeclaringType==type)
                result.Add(new InstalledPatch{Original=original,Method=patch.PatchMethod,Owner=owner,Kind=kind,Priority=patch.priority,Before=(string[])(patch.before??Array.Empty<string>()).Clone(),After=(string[])(patch.after??Array.Empty<string>()).Clone(),NativePatch=patch});
    }
    private static readonly Lazy<object> NativePatchGate=new Lazy<object>(()=>
    {
        // This uses the inspected HarmonyX metadata API, not a guessed layout.
        using(var input=System.IO.File.OpenRead(typeof(Harmony).Assembly.Location))
        using(var sha=System.Security.Cryptography.SHA256.Create())
            if(BitConverter.ToString(sha.ComputeHash(input)).Replace("-","")!="C349E1A3FD13FA5A9FACC9805A5E160161B14489F46F6BDD38202B8E124F78DF")
                throw new InvalidOperationException("Selective rollback requires the qualified Harmony assembly.");
        var field=typeof(PatchProcessor).GetField("locker",BindingFlags.Static|BindingFlags.NonPublic);
        if(field==null||field.FieldType!=typeof(object))throw new InvalidOperationException("Native Harmony patch gate unavailable.");
        return field.GetValue(null)??throw new InvalidOperationException("Native Harmony patch gate unavailable.");
    });
    private static void RemoveDuplicateHooks(Harmony owner,Type type,List<InstalledPatch> before)
    {
        var keep=new List<InstalledPatch>(before);var duplicates=new List<InstalledPatch>();
        foreach(var item in Capture(type,owner.Id))
        {
            if(keep.Exists(prior=>ReferenceEquals(prior.NativePatch,item.NativePatch)))continue;
            if(keep.Exists(prior=>prior.Original==item.Original&&prior.Owner==item.Owner&&prior.Kind==item.Kind&&prior.Method==item.Method))duplicates.Add(item);
            else keep.Add(item);
        }
        var targets=new HashSet<MethodBase>();foreach(var duplicate in duplicates)targets.Add(duplicate.Original);
        foreach(var target in targets)RemoveAttempt(owner,target,duplicates);
    }
    private static Patch[] WithoutAttempt(Patch[] source,MethodBase original,List<InstalledPatch> added)
    {
        var kept=new List<Patch>();
        foreach(var patch in source)
            if(!added.Exists(item=>item.Original==original&&ReferenceEquals(item.NativePatch,patch)))kept.Add(patch);
        return kept.ToArray();
    }
    private static void RemoveAttempt(Harmony owner,MethodBase original,List<InstalledPatch> added)
    {
        lock(NativePatchGate.Value)
        {
            var info=HarmonyLib.Public.Patching.PatchManager.GetPatchInfo(original);
            if(info==null)return;
            var prefixes=info.prefixes;var postfixes=info.postfixes;var transpilers=info.transpilers;var finalizers=info.finalizers;
            try
            {
                info.prefixes=WithoutAttempt(prefixes,original,added);info.postfixes=WithoutAttempt(postfixes,original,added);
                info.transpilers=WithoutAttempt(transpilers,original,added);info.finalizers=WithoutAttempt(finalizers,original,added);
                // Empty processor adds no hooks; native code recompiles exactly
                // these retained Patch objects under the same reentrant gate.
                new PatchProcessor(owner,original).Patch();
            }
            catch(Exception rollbackError)
            {
                info.prefixes=prefixes;info.postfixes=postfixes;info.transpilers=transpilers;info.finalizers=finalizers;
                try{new PatchProcessor(owner,original).Patch();}
                catch(Exception restoreError){throw new AggregateException("Patch rollback and wrapper restoration failed; class retained.",rollbackError,restoreError);}
                throw;
            }
        }
    }
    private static void Restore(InstalledPatch item)
    {
        var hook=new HarmonyMethod(item.Method){priority=item.Priority,before=item.Before,after=item.After};
        var owner=new Harmony(item.Owner);
        switch(item.Kind)
        {
            case HarmonyPatchType.Prefix:owner.Patch(item.Original,prefix:hook);break;
            case HarmonyPatchType.Postfix:owner.Patch(item.Original,postfix:hook);break;
            case HarmonyPatchType.Transpiler:owner.Patch(item.Original,transpiler:hook);break;
            case HarmonyPatchType.Finalizer:owner.Patch(item.Original,finalizer:hook);break;
            default:throw new InvalidOperationException("Unsupported targeted hook kind.");
        }
    }
    private static bool StillInstalled(List<InstalledPatch> expected)
    {
        foreach(var item in expected)
        {
            var info=Harmony.GetPatchInfo(item.Original);if(info==null)return false;
            bool found=false;
            IEnumerable<Patch> group=item.Kind==HarmonyPatchType.Prefix?info.Prefixes:item.Kind==HarmonyPatchType.Postfix?info.Postfixes:
                item.Kind==HarmonyPatchType.Transpiler?info.Transpilers:info.Finalizers;
            foreach(var patch in group)if(patch.owner==item.Owner&&patch.PatchMethod==item.Method)found=true;
            if(!found)return false;
        }
        return true;
    }
}

/// <summary>
/// Retained temporarily so v15-tagged manual patch classes continue to compile.
/// It no longer participates in an assembly-wide PatchAll operation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RebirthManualHarmonyPatchAttribute : Attribute
{
}
