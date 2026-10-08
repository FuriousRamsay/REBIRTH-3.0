using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

// 3.2 composite safes use feature locks and replace the locked block on success.
// Keep the legacy adapter for older block implementations. Register on the server's
// accepted lock callback, not on a client request or the appearance of a timer UI.
[HarmonyPatch]
internal static class RebirthCompositeLockpickStartPatch
{
    // Current installed LockManager calls this only after it has assigned the exact native lock owner.
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(TEFeatureAbs), "OnLockResponseServer");
    private static void Postfix(TEFeatureAbs __instance, int __0, PooledBinaryWriter __1, ushort __2, bool __runOriginal)
    {
        if(!__runOriginal || !(__instance is TEFeatureLockPickable feature) || !ThreadManager.IsMainThread())return;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world=GameManager.Instance?.World;
        if(connection==null||!connection.IsServer||world==null||world.IsRemote()||feature.Parent==null)return;
        var manager=LockManager.Instance;
        if(manager==null||feature.IsSharedLock(__2)||!manager.singleLocks.TryGetByValue(new LockManager.LockEntry(feature,__2),out var originalOwner)||originalOwner!=__0)return;
        var position=feature.ToWorldPos();
        if(!ReferenceEquals(world.GetTileEntity(position),feature.Parent))return;
        EntityPlayer player=world.GetEntity(__0) as EntityPlayer;
        float baseTime=feature.lockPickTime,completion=feature.unlockCompletion;
        BlockValue downgrade=feature.lockpickDowngradeBlock;
        if(player==null||!ReferenceEquals(player.world,world)||player.IsDead()||downgrade.isair||
            float.IsNaN(baseTime)||float.IsInfinity(baseTime)||baseTime<=0||float.IsNaN(completion)||float.IsInfinity(completion)||completion>=1)return;
        float effective=EffectManager.GetValue(PassiveEffects.LockPickTime,player.inventory?.holdingItemItemValue,baseTime,player);
        if(float.IsNaN(effective)||float.IsInfinity(effective)||effective<=0)return;
        RebirthSkillWaveAService.RegisterServerLockpickAttempt(player,position,baseTime,effective,effective*(1f-Mathf.Clamp01(completion)),downgrade.type);
        RebirthTheorySoloLockpickService.Grant(feature,player,__2,baseTime,effective,effective*(1f-Mathf.Clamp01(completion)));
    }
}
[HarmonyPatch]
internal static class RebirthCompositeLockpickSuccessPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(TEFeatureLockPickable), "EventData_Event");
    private static void Prefix(TEFeatureLockPickable __instance, TimerEventData __0, out RebirthTheorySoloLockpickService.Transition __state)
    { __state=RebirthTheorySoloLockpickService.BeforeLocal(__instance,__0); }
    private static Exception Finalizer(Exception __exception,RebirthTheorySoloLockpickService.Transition __state)
    { RebirthTheorySoloLockpickService.Unknown(__state,__exception);return __exception; }
    private static void Postfix(TEFeatureLockPickable __instance, TimerEventData __0, RebirthTheorySoloLockpickService.Transition __state, bool __runOriginal)
    {
        RebirthTheorySoloLockpickService.After(__state,__0,__runOriginal);
        if (__runOriginal && __0?.Data is EntityPlayerLocal player)
            RebirthSkillWaveAService.ReportClientLockpickSuccess(player, __instance.ToWorldPos());
    }
}

[HarmonyPatch(typeof(TEFeatureAbs), nameof(TEFeatureAbs.OnUnlockedServer))]
internal static class RebirthTheorySoloLockpickReleasePatch
{
    private static void Postfix(TEFeatureAbs __instance,int __0,ushort __1,bool __runOriginal)
    { RebirthTheorySoloLockpickService.Release(__instance,__0,__1,__runOriginal); }
}

[HarmonyPatch(typeof(NetPackageSetBlock), nameof(NetPackageSetBlock.ProcessPackage))]
internal static class RebirthTheorySoloLockpickBlockTransitionPatch
{
    private static void Prefix(NetPackageSetBlock __instance,World __0,GameManager __1,out RebirthTheorySoloLockpickService.Transition __state)
    { __state=RebirthTheorySoloLockpickService.BeforeRemote(__instance,__0,__1); }
    private static void Postfix(NetPackageSetBlock __instance,RebirthTheorySoloLockpickService.Transition __state,bool __runOriginal)
    { RebirthTheorySoloLockpickService.After(__state,__instance,__runOriginal); }
    private static Exception Finalizer(Exception __exception,RebirthTheorySoloLockpickService.Transition __state)
    { RebirthTheorySoloLockpickService.Unknown(__state,__exception);return __exception; }
}
[HarmonyPatch(typeof(GameManager), nameof(GameManager.ChangeBlocks))]
internal static class RebirthTheorySoloLockpickCommitPatch
{
    private static void Prefix(GameManager __instance,PlatformUserIdentifierAbs __0,System.Collections.Generic.List<BlockChangeInfo> __1,out RebirthTheorySoloLockpickService.Transition __state)
    { __state=RebirthTheorySoloLockpickService.BeforeCommit(__instance,__0,__1); }
    private static void Postfix(RebirthTheorySoloLockpickService.Transition __state,bool __runOriginal)
    { RebirthTheorySoloLockpickService.AfterCommit(__state,__runOriginal); }
    private static Exception Finalizer(Exception __exception,RebirthTheorySoloLockpickService.Transition __state)
    { RebirthTheorySoloLockpickService.Unknown(__state,__exception);return __exception; }
}
[HarmonyPatch(typeof(TEFeatureLockPickable), nameof(TEFeatureLockPickable.ShowUI))]
internal static class RebirthTheorySoloLockpickShowUiPatch
{
    private static void Prefix(TEFeatureLockPickable __instance,bool __0,out RebirthTheorySoloLockpickService.TimerSetup __state)
    { __state=RebirthTheorySoloLockpickService.BeforeShowUi(__instance,__0); }
    private static void Postfix(RebirthTheorySoloLockpickService.TimerSetup __state,bool __runOriginal)
    { RebirthTheorySoloLockpickService.AfterShowUi(__state,__runOriginal); }
    private static Exception Finalizer(Exception __exception,RebirthTheorySoloLockpickService.TimerSetup __state)
    { RebirthTheorySoloLockpickService.UnknownShowUi(__state,__exception);return __exception; }
}

[HarmonyPatch(typeof(XUiC_Timer), nameof(XUiC_Timer.setTimer))]
internal static class RebirthTheorySoloLockpickTimerPatch
{
    private static void Postfix(XUiC_Timer __instance,float __0,TimerEventData __1,bool __runOriginal)
    { RebirthTheorySoloLockpickService.CaptureTimer(__instance,__1,__0,__runOriginal); }
}