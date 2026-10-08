using HarmonyLib;
using System;
using System.Collections.Generic;

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
            if (PatchedTypes.Contains(patchType))
                return false;

            try
            {
                lastPatchClass = patchType.FullName ?? patchType.Name;
                { if (RebirthLogSettings.HarmonyPatchLoggingEnabled) RebirthLogSettings.TraceHarmonyPatch("BEGIN harmony=" + harmony.Id + " class=" + lastPatchClass); }
                harmony.CreateClassProcessor(patchType).Patch();
                PatchedTypes.Add(patchType);
                patchedClassCount++;
                lastError = string.Empty;
                { if (RebirthLogSettings.HarmonyPatchLoggingEnabled) RebirthLogSettings.TraceHarmonyPatch("PASS harmony=" + harmony.Id + " class=" + lastPatchClass); }
                return true;
            }
            catch (Exception ex)
            {
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
}

/// <summary>
/// Retained temporarily so v15-tagged manual patch classes continue to compile.
/// It no longer participates in an assembly-wide PatchAll operation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RebirthManualHarmonyPatchAttribute : Attribute
{
}
