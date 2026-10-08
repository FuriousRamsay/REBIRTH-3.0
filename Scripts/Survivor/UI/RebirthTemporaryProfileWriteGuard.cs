using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

internal static class RebirthTemporaryProfileWriteGuard
{
    private static bool installed;
    internal static bool IsReady { get { return installed; } }
    [ThreadStatic] private static string ownedName;
    private sealed class Scope : IDisposable
    {
        private readonly string before;
        public Scope(string name) { before = ownedName; ownedName = name; }
        public void Dispose() { ownedName = before; }
    }
    internal static IDisposable OwnWrite(string name) { Install(); return new Scope(name); }
    internal static void Install()
    {
        if (installed) return;
        var harmony = new Harmony("rebirth.temporary-profile-write-ownership");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(SaveGuard));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(DeleteGuard));
        installed = true;
    }
    [HarmonyPatch]
    private static class SaveGuard
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (MethodInfo method in typeof(ProfileSDF).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                if (method.Name == nameof(ProfileSDF.SaveProfile) && method.GetParameters().Length > 0 && method.GetParameters()[0].ParameterType == typeof(string)) yield return method;
        }
        private static bool Prefix(string __0)
        {
            if (!RebirthTemporaryProfileOwnership.IsReservedName(__0)) return true;
            if (string.Equals(ownedName, __0, StringComparison.OrdinalIgnoreCase)) return true;
            if (!ProfileSDF.ProfileExists(__0))
            {
                Log.Warning("[REBIRTH Profiles] RBTemp_ is reserved; choose a different permanent Player Profile name.");
                return false;
            }
            // A public edit adopts the existing profile as permanent. Revoke automatic
            // deletion authority before the native write; failure must preserve the profile.
            RebirthNativePlayerProfileBridge.ForgetTemporaryOwnership(__0);
            return true;
        }
    }
    [HarmonyPatch(typeof(ProfileSDF), nameof(ProfileSDF.DeleteProfile))]
    private static class DeleteGuard
    {
        private static void Prefix(string __0) { RebirthNativePlayerProfileBridge.ForgetTemporaryOwnership(__0); }
    }
}
