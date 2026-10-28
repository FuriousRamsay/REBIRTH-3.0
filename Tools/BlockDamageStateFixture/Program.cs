using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using HarmonyLib;

// Inert game parameter doubles; real installed Harmony builds/runs both wrappers.
public class WorldBase { }
public struct BlockValueRef { }
public class Block
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int Hit(WorldBase world, BlockValueRef pos, int unused, int damage, int actor)
    { return damage; }
}
public static class RebirthWorkmanshipSignatureService
{
    public static int BoolPostfixCalls, StringPostfixCalls;
    public static void BlockDamagePrefix(ref int __3, out bool __state) { __state = true; __3 *= 2; }
    public static void BlockDamagePostfix(bool __state)
    { if (!__state) throw new Exception("workmanship bool state lost"); BoolPostfixCalls++; }
    public static void EmptyHandBlockInspectPrefix(Block __instance, WorldBase __0, BlockValueRef __1, int __4, out string __state)
    { __state = "inspection:" + __4; }
    public static void EmptyHandBlockInspectPostfix(string __state)
    { if (__state != "inspection:7") throw new Exception("inspection string state lost"); StringPostfixCalls++; }
}
class Program
{
    [DllImport("kernel32.dll")] static extern void ExitProcess(uint exitCode);
    static int Finish()
    {
        // Unity's standalone Mono host faults in shutdown after detour use.
        // Tests have finished; terminate only this disposable offline process.
        if (Type.GetType("Mono.Runtime") != null) ExitProcess(0);
        return 0;
    }
    static int Native(string root, bool baseline)
    {
        root = Path.GetFullPath(root);
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
            string name = new AssemblyName(e.Name).Name + ".dll";
            foreach (string folder in new[] { root, Path.GetFullPath(Path.Combine(root, "../../7DaysToDie_Data/Managed")), Path.GetFullPath(Path.Combine(root, "../0_TFP_Harmony")) })
                if (File.Exists(Path.Combine(folder, name))) return Assembly.LoadFrom(Path.Combine(folder, name));
            return null;
        };
        var native = Assembly.LoadFrom(Path.GetFullPath(Path.Combine(root, "../../7DaysToDie_Data/Managed/Assembly-CSharp.dll")));
        var mod = Assembly.LoadFrom(Path.Combine(root, "RebirthUtils.dll"));
        var target = native.GetType("Block").GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(m => m.Name == "OnBlockDamaged" && m.GetParameters().Length == 9);
        var service = mod.GetType("RebirthWorkmanshipSignatureService");
        var inspect = mod.GetType("RebirthEmptyHandBlockInspectionPatch");
        var salvage = mod.GetType("RebirthSalvageAcceptedHitWitness");
        var h = new Harmony("rebirth.offline.native.blockdamage.state");
        if (!baseline) {
            var install = service.GetMethod("PatchBlockDamageMethods", BindingFlags.Static | BindingFlags.NonPublic);
            int count = (int)install.Invoke(null, new object[] { h });
            if (count < 1) throw new Exception("No native block hooks installed");
            Console.WriteLine("PASS: actual installed PatchBlockDamageMethods JIT-compiled all three production patch pairs/finalizer on " + count + " native/mod Block methods; no gameplay execution.");
            return Finish();
        }
        try {
            h.Patch(target, prefix: new HarmonyMethod(service, "BlockDamagePrefix"), postfix: new HarmonyMethod(service, "BlockDamagePostfix"));
            h.Patch(target, prefix: new HarmonyMethod(baseline ? service : inspect, baseline ? "EmptyHandBlockInspectPrefix" : "Prefix"),
                postfix: new HarmonyMethod(baseline ? service : inspect, baseline ? "EmptyHandBlockInspectPostfix" : "Postfix"));
            if (baseline) throw new Exception("Native baseline unexpectedly compiled");
            h.Patch(target, prefix: new HarmonyMethod(salvage, "Prefix"), postfix: new HarmonyMethod(salvage, "Postfix"), finalizer: new HarmonyMethod(salvage, "Finalizer"));
            Console.WriteLine("PASS: installed native Block.OnBlockDamaged JIT-compiled with all three production patch pairs and salvage finalizer; game not launched.");
            return Finish();
        } catch (HarmonyException ex) when (baseline) {
            if (!(ex.InnerException is InvalidProgramException)) throw;
            Console.WriteLine("EXPECTED NATIVE BASELINE InvalidProgramException: " + ex.InnerException.Message);
            return Finish();
        }
    }
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "native") return Native(args[1], args.Length > 2 && args[2] == "baseline");
        bool baseline = args.Length > 0 && args[0] == "baseline";
        var h = new Harmony("rebirth.offline.blockdamage.state");
        var target = typeof(Block).GetMethod("Hit");
        try
        {
            h.Patch(target, prefix: new HarmonyMethod(typeof(RebirthWorkmanshipSignatureService), "BlockDamagePrefix"),
                postfix: new HarmonyMethod(typeof(RebirthWorkmanshipSignatureService), "BlockDamagePostfix"));
            var inspector = baseline ? typeof(RebirthWorkmanshipSignatureService) : typeof(RebirthEmptyHandBlockInspectionPatch);
            h.Patch(target, prefix: new HarmonyMethod(inspector, baseline ? "EmptyHandBlockInspectPrefix" : "Prefix"),
                postfix: new HarmonyMethod(inspector, baseline ? "EmptyHandBlockInspectPostfix" : "Postfix"));
            if (baseline) throw new Exception("Baseline unexpectedly compiled; reproduction did not fail");
            for (int i = 1; i <= 20; i++)
                if (new Block().Hit(new WorldBase(), default, 0, i, 7) != i * 2) throw new Exception("damage altered");
            if (RebirthWorkmanshipSignatureService.BoolPostfixCalls != 20 || RebirthWorkmanshipSignatureService.StringPostfixCalls != 20)
                throw new Exception("hook count mismatch");
            Console.WriteLine("PASS: production inspector hooks compile and retain distinct bool/string state across 20 hits.");
            return Finish();
        }
        catch (HarmonyException ex) when (baseline)
        {
            Console.WriteLine("EXPECTED BASELINE FAILURE: " + ex);
            if (!(ex.InnerException is InvalidProgramException)) throw;
            return Finish();
        }
    }
}
