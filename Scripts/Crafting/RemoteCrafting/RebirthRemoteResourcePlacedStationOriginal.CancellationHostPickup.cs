using System;
using HarmonyLib;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    internal static void InstallActionPaymentPickupObserver(Harmony harmony)=>RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationHostPickup));
    // The native timer may have been started before custody. Its completion
    // gives/drops the block item BEFORE SetBlockRPC, so removal alone is late.
    [HarmonyPatch(typeof(Block),nameof(Block.TakeItemWithTimerDone),new[]{typeof(TimerEventData)})]
    private static class CancellationHostPickup
    {
        private static bool Prefix(TimerEventData _timerData,bool __runOriginal,System.Reflection.MethodBase __originalMethod,out object __state)
        {
            __state=null;if(!__runOriginal||!ThreadManager.IsMainThread())return true;
            if(_timerData?.Data is not object[] values||values.Length!=3||values[1] is not Vector3i position)return true;
            if(!RemoteResourceTransactions.AllowActionBlockEscape(GameManager.Instance?.World,position,"native-block-pickup-timer-completion"))return false;
            if(!ProtocolActive)return true;
            return EnterCancellationBlockEscape("take",GameManager.Instance?.World,position,__originalMethod,true,out __state);
        }
        private static void Postfix(bool __runOriginal,object __state)=>ObserveCancellationBlockEscape(__runOriginal,__state);
        private static Exception Finalizer(Exception __exception,object __state){ObserveCancellationBlockEscape(false,__state);return __exception;}
    }
}
