using System;

internal static partial class RebirthProductiveEarnedCoordinator
{
    internal struct NativeTerminalOuterTick
    {
        internal TileEntityWorkstation Station;internal World World;internal ulong Token;
        internal Attempt Returned;internal object NativeOutput;internal bool InnerReturned,Ambiguous;
        internal Attempt Earned;internal object EarnedInvocation;internal bool EarnedAmbiguous;
    }

    internal readonly struct NativeTerminalTickState
    {
        internal readonly NativeTerminalOuterTick Previous;
        internal readonly ulong Token;
        internal NativeTerminalTickState(NativeTerminalOuterTick previous,ulong token)
        { Previous=previous; Token=token; }
    }

    [ThreadStatic]private static NativeTerminalOuterTick nativeTerminalOuterTick;
    [ThreadStatic]private static ulong nextNativeTerminalTickToken;
    internal static NativeTerminalTickState EnterNativeTerminalOuterTick(TileEntityWorkstation station,bool ran)
    {
        if(!ran||!Enabled||station==null||station is RebirthCamperStationState||GameManager.Instance?.World?.IsRemote()!=false)return default;
        ulong token=unchecked(++nextNativeTerminalTickToken);
        if(token==0)token=++nextNativeTerminalTickToken;
        var state=new NativeTerminalTickState(nativeTerminalOuterTick,token);
        // An original can be admitted inside this tick, before any retained entry exists.
        nativeTerminalOuterTick=new NativeTerminalOuterTick{Station=station,World=GameManager.Instance.World,Token=token};
        return state;
    }

    private static bool NativeTerminalTickCurrent(NativeTerminalTickState state)
        =>state.Token!=0&&nativeTerminalOuterTick.Token==state.Token;
    private static bool DeferNativeTerminalToOuterTick(TileEntityWorkstation station,Attempt original,object nativeOutput)
    {
        ref var scope=ref nativeTerminalOuterTick;
        if(scope.Token==0||!ReferenceEquals(scope.Station,station)||!ReferenceEquals(scope.World,GameManager.Instance?.World))return false;
        // Called only after the actual inner invocation and its exact positive
        // output token were verified by ObserveNativeReturned. The outer scope
        // cannot mint a token or invoke HandleRecipeQueue itself.
        if(scope.InnerReturned&&(!ReferenceEquals(scope.Returned,original)||!ReferenceEquals(scope.NativeOutput,nativeOutput)))scope.Ambiguous=true;
        scope.Returned=original;scope.NativeOutput=nativeOutput;scope.InnerReturned=true;return true;
    }
    internal static void ObserveNativeTerminalOuterTickReturned(TileEntityWorkstation station,bool ran,NativeTerminalTickState state)
    {
        ObserveInitialNativeSuffixReturned(station,ran,state);
        var scope=nativeTerminalOuterTick;
        if(!ran||!NativeTerminalTickCurrent(state)||!ReferenceEquals(scope.Station,station)||
            !ReferenceEquals(scope.World,GameManager.Instance?.World)||!scope.InnerReturned||scope.Ambiguous||scope.Returned==null||scope.NativeOutput==null||
            !retained.TryGetValue(station,out var current)||!ReferenceEquals(current,scope.Returned)||!current.OutputTimerObserved||
            !ReferenceEquals(current.NativeOutput,scope.NativeOutput))return;
        CaptureReturnedNativeTerminal(current);
    }
    private static void ObserveInitialNativeApplied(Attempt original)
    {
        var entered=nativeTerminalOuterTick;
        if(entered.Token==0||original?.Artifact==null||!ReferenceEquals(entered.Station,original.Station)||
            !ReferenceEquals(entered.World,GameManager.Instance?.World)||!RebirthRecyclerNativeLoop.IsCurrentInvocation(original.Station)||
            !RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,original.Frame.AfterNative,original.Frame.AfterElapsedOffset))return;
        var token=RebirthRecyclerNativeLoop.InvocationToken(original.Station);
        ref var scope=ref nativeTerminalOuterTick;
        if(token==null||scope.Token!=entered.Token)return;
        if(scope.Earned!=null&&(!ReferenceEquals(scope.Earned,original)||!ReferenceEquals(scope.EarnedInvocation,token)))scope.EarnedAmbiguous=true;
        scope.Earned=original;scope.EarnedInvocation=token;
        original.NativeTickCustody=true;original.NativeSuffixRequired=true;
    }
    internal static void EndNativeTerminalOuterTick(NativeTerminalTickState state)
    {if(NativeTerminalTickCurrent(state))nativeTerminalOuterTick=state.Previous;}
}
