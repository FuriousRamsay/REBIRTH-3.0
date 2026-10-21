using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

#nullable disable

public static class RebirthFirePatchInstaller
{
    public const string HarmonyId = "rebirth.fire.3.1";

    private static readonly Harmony Harmony = new Harmony(HarmonyId);
    private static bool installed;
    private static int patchedMethods;
    private static int failedMethods;

    public static bool IsInstalled { get { return installed; } }
    public static int PatchedMethods { get { return patchedMethods; } }
    public static int FailedMethods { get { return failedMethods; } }

    public static string Install()
    {
        if (installed)
            return "[REBIRTH Fire] gameplay integration already installed patches=" + patchedMethods + " failures=" + failedMethods;

        patchedMethods = 0;
        failedMethods = 0;

        PatchPostfix(
            AccessTools.Method(
                typeof(Explosion),
                nameof(Explosion.AttackBlocks),
                new[] { typeof(int), typeof(ItemValue) }),
            AccessTools.Method(typeof(RebirthFireGameplayPatches), nameof(RebirthFireGameplayPatches.ExplosionAttackBlocksPostfix)));

        PatchPostfix(
            AccessTools.Method(
                typeof(Block),
                nameof(Block.OnEntityWalking),
                new[]
                {
                    typeof(WorldBase), typeof(int), typeof(int), typeof(int),
                    typeof(BlockValue), typeof(Entity)
                }),
            AccessTools.Method(typeof(RebirthFireGameplayPatches), nameof(RebirthFireGameplayPatches.BlockOnEntityWalkingPostfix)));

        PatchPostfix(
            AccessTools.Method(
                typeof(Chunk),
                nameof(Chunk.SetBlock),
                new[]
                {
                    typeof(WorldBase), typeof(int), typeof(int), typeof(int), typeof(BlockValue),
                    typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(int)
                }),
            AccessTools.Method(typeof(RebirthFireGameplayPatches), nameof(RebirthFireGameplayPatches.ChunkSetBlockPostfix)));


        PatchPrefix(
            AccessTools.Method(typeof(World), "AddFallingBlock", new[] { typeof(Vector3i), typeof(bool) }),
            AccessTools.Method(typeof(RebirthFireStabilityPatches), nameof(RebirthFireStabilityPatches.WorldAddFallingBlockPrefix)));

        try{RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthExtinguisherNativeShot.NativeEffects));}
        catch(Exception error){failedMethods++;Log.Error("[REBIRTH Fire] native extinguisher observer failed: "+error.Message);}
        try{RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthTorchNativeHit.NativeHit));}
        catch(Exception error){failedMethods++;Log.Error("[REBIRTH Fire] native torch observer failed: "+error.Message);}
        foreach(var type in new[]{typeof(RebirthFlamingProjectileNative.NativeEffects),typeof(RebirthFlamingProjectileNative.NativeFire),
            typeof(RebirthFlamingProjectileNative.NativeCollision),typeof(RebirthFlamingProjectileNative.NativeState)})
        {
            try{RebirthHarmonyBootstrap.PatchClassOnce(Harmony,type);}
            catch(Exception error){failedMethods++;Log.Error("[REBIRTH Fire] native flaming projectile observer failed: "+type.Name+": "+error.Message);}
        }
        installed = failedMethods == 0;
        return "[REBIRTH Fire] installed gameplay integration patches=" + patchedMethods + " failures=" + failedMethods;
    }

    private static string FormatMethodSignature(MethodInfo method)
    {
        if (method == null)
            return "<null>";
        ParameterInfo[] parameters = method.GetParameters();
        string[] names = new string[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
            names[i] = parameters[i].ParameterType.FullName;
        return method.DeclaringType.FullName + "." + method.Name + "(" + string.Join(",", names) + ")";
    }


    private static void PatchPrefix(MethodInfo original, MethodInfo prefix)
    {
        if (original == null || prefix == null)
        {
            failedMethods++;
            return;
        }
        try
        {
            if(HasOwnPatch(original,prefix,true)){patchedMethods++;return;}
            Harmony.Patch(original, prefix: new HarmonyMethod(prefix));
            patchedMethods++;
        }
        catch (Exception ex)
        {
            failedMethods++;
            Log.Error("[REBIRTH Fire] patch failed target=" + original.DeclaringType.Name + "." + original.Name
                + " error=" + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void PatchPostfix(MethodInfo original, MethodInfo postfix)
    {
        if (original == null || postfix == null)
        {
            failedMethods++;
            return;
        }

        try
        {
            if(HasOwnPatch(original,postfix,false)){patchedMethods++;return;}
            Harmony.Patch(original, postfix: new HarmonyMethod(postfix));
            patchedMethods++;
        }
        catch (Exception ex)
        {
            failedMethods++;
            Log.Error("[REBIRTH Fire] patch failed target=" + original.DeclaringType.Name + "." + original.Name
                + " error=" + ex.GetType().Name + ": " + ex.Message);
        }
    }
    private static bool HasOwnPatch(MethodInfo original,MethodInfo hook,bool prefix)
    {
        var info=HarmonyLib.Harmony.GetPatchInfo(original);
        if(info==null)return false;
        foreach(var patch in prefix?info.Prefixes:info.Postfixes)
            if(patch.owner==HarmonyId&&patch.PatchMethod==hook)return true;
        return false;
    }
}
