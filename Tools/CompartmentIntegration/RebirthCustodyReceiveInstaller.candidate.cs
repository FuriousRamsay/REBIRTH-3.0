using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;

// Staged integration candidate; no production bootstrap calls this installer yet.
internal static class RebirthCustodyReceiveInstaller
{
    internal const string Owner = "rebirth.custody.receive.3_2b10";
    private static readonly object Sync = new object();
    private static readonly Harmony Patcher = new Harmony(Owner);
    private static bool failed;

    internal static bool HasCoverage()
    {
        lock (Sync)
        {
            if (failed) return false;
            var target = ResolveTarget();
            var transform = AccessTools.Method(typeof(RebirthCustodyReceiveInstaller), "Transpiler");
            var info = target == null ? null : Harmony.GetPatchInfo(target);
            return info != null && transform != null && info.Transpilers.Count == 1 && info.ILManipulators.Count == 0 && info.Transpilers.Any(p =>
                p.owner == Owner && p.PatchMethod == transform);
        }
    }

    internal static void Install()
    {
        lock (Sync)
        {
            if (failed) throw new InvalidOperationException("Custody receive installation previously failed.");
            MethodInfo target = null;
            bool attemptedPatch = false;
            try
            {
                if (HasCoverage()) return;
                target = ResolveTarget();
                var transform = AccessTools.Method(typeof(RebirthCustodyReceiveInstaller), "Transpiler");
                if (target == null || transform == null)
                    throw new InvalidOperationException("Exact custody receive target/transform unavailable.");
                var existing = Harmony.GetPatchInfo(target);
                if (existing != null && (existing.Transpilers.Count != 0 || existing.ILManipulators.Count != 0))
                    throw new InvalidOperationException("Receive instruction-transform composition requires explicit review.");
                attemptedPatch = true;
                Patcher.Patch(target, transpiler: new HarmonyMethod(transform));
                if (!HasCoverage()) throw new InvalidOperationException("Custody receive patch coverage absent.");
            }
            catch (Exception installationFailure)
            {
                failed = true;
                if (attemptedPatch && target != null)
                {
                    try { Patcher.Unpatch(target, HarmonyPatchType.Transpiler, Owner); }
                    catch (Exception cleanupFailure)
                    {
                        throw new AggregateException("Installation and owner cleanup failed; coverage remains latched off.", installationFailure, cleanupFailure);
                    }
                }
                throw;
            }
        }
    }
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        return FarmingSessionReceiveTransform.Transform(instructions, __originalMethod);
    }
    private static MethodInfo ResolveTarget()
    {
        var methods = typeof(ConnectionManager).GetMethods(BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic).Where(m => m.Name == "ProcessPackages").ToArray();
        if (methods.Length != 1) return null;
        var method = methods[0];
        var parameters = method.GetParameters();
        if (method.IsStatic || method.ReturnType != typeof(void) || parameters.Length != 3 ||
            parameters[0].ParameterType != typeof(INetConnection) || parameters[1].ParameterType != typeof(NetPackageDirection) ||
            parameters[2].ParameterType != typeof(ClientInfo) ||
            method.Module.ModuleVersionId.ToString("D") != "1a9a4203-3d95-4c90-b094-8926dec1ee9c") return null;
        var body = method.GetMethodBody();
        if (body == null) return null;
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(body.GetILAsByteArray())).Replace("-", "") ==
                "7F0BFAAD493946941D3BE1D48AB0C7DDE8ED526DEC715CF429C6849BF04DAFD2" ? method : null;
    }
}




