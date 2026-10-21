using System;

internal static partial class RebirthProductiveEarnedCoordinator
{
    private sealed class NativeTerminalOuterTick
    {
        internal TileEntityWorkstation Station;internal World World;internal NativeTerminalOuterTick Previous;
        internal Attempt Returned;internal object NativeOutput;internal bool InnerReturned,Ambiguous;
        internal Attempt Earned;internal object EarnedInvocation;internal bool EarnedAmbiguous;
    }
    [ThreadStatic]private static NativeTerminalOuterTick nativeTerminalOuterTick;
    internal static object EnterNativeTerminalOuterTick(TileEntityWorkstation station,bool ran)
    {
        if(!ran||!Enabled||station==null||station is RebirthCamperStationState||GameManager.Instance?.World?.IsRemote()!=false)return null;
        var scope=new NativeTerminalOuterTick{Station=station,World=GameManager.Instance.World,Previous=nativeTerminalOuterTick};
        nativeTerminalOuterTick=scope;return scope;
    }
    private static bool DeferNativeTerminalToOuterTick(TileEntityWorkstation station,Attempt original,object nativeOutput)
    {
        var scope=nativeTerminalOuterTick;
        if(scope==null||!ReferenceEquals(scope.Station,station)||!ReferenceEquals(scope.World,GameManager.Instance?.World))return false;
        // Called only after the actual inner invocation and its exact positive
        // output token were verified by ObserveNativeReturned. The outer scope
        // cannot mint a token or invoke HandleRecipeQueue itself.
        if(scope.InnerReturned&&(!ReferenceEquals(scope.Returned,original)||!ReferenceEquals(scope.NativeOutput,nativeOutput)))scope.Ambiguous=true;
        scope.Returned=original;scope.NativeOutput=nativeOutput;scope.InnerReturned=true;return true;
    }
    internal static void ObserveNativeTerminalOuterTickReturned(TileEntityWorkstation station,bool ran,object state)
    {
        ObserveInitialNativeSuffixReturned(station,ran,state);
        if(!ran||state is not NativeTerminalOuterTick scope||!ReferenceEquals(nativeTerminalOuterTick,scope)||!ReferenceEquals(scope.Station,station)||
            !ReferenceEquals(scope.World,GameManager.Instance?.World)||!scope.InnerReturned||scope.Ambiguous||scope.Returned==null||scope.NativeOutput==null||
            !retained.TryGetValue(station,out var current)||!ReferenceEquals(current,scope.Returned)||!current.OutputTimerObserved||
            !ReferenceEquals(current.NativeOutput,scope.NativeOutput))return;
        CaptureReturnedNativeTerminal(current);
    }
    private static void ObserveInitialNativeApplied(Attempt original)
    {
        var scope=nativeTerminalOuterTick;
        if(scope==null||original?.Artifact==null||!ReferenceEquals(scope.Station,original.Station)||
            !ReferenceEquals(scope.World,GameManager.Instance?.World)||!RebirthRecyclerNativeLoop.IsCurrentInvocation(original.Station)||
            !RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,original.Frame.AfterNative,original.Frame.AfterElapsedOffset))return;
        var token=RebirthRecyclerNativeLoop.InvocationToken(original.Station);if(token==null)return;
        if(scope.Earned!=null&&(!ReferenceEquals(scope.Earned,original)||!ReferenceEquals(scope.EarnedInvocation,token)))scope.EarnedAmbiguous=true;
        scope.Earned=original;scope.EarnedInvocation=token;
        original.NativeTickCustody=true;original.NativeSuffixRequired=true;
    }
    internal static void EndNativeTerminalOuterTick(object state)
    {if(state is NativeTerminalOuterTick scope&&ReferenceEquals(nativeTerminalOuterTick,scope))nativeTerminalOuterTick=scope.Previous;}
}
