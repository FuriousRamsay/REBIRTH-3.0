using System;
using System.Linq;

internal static partial class RebirthProductiveEarnedCoordinator
{
    [ThreadStatic] private static Attempt paidNativeZeroHandler;
    internal static bool IsExecutingPaidNativeZeroCompletion(TileEntityWorkstation station)
        =>IsExecutingMobilePaidZeroCompletion(station)||RebirthProductiveEarnedRemoteHost.IsNativeHandoffTick(station)&&paidNativeZeroHandler?.NativeTickCustody==true&&
            ReferenceEquals(paidNativeZeroHandler.Station,station)&&retained.TryGetValue(station,out var original)&&
            ReferenceEquals(original,paidNativeZeroHandler)&&!original.OutputTimerObserved&&original.Artifact?.MatchesAfter(station)==true&&
            (original.RemoteSavedEarned!=null?original.RemoteSavedEarned():EffectivePaidNativeAfterCurrent(original));
    private static bool IsNativeSettlementInvocation(Attempt original)=>original?.NativeAdoptionReleaseReady?.Invoke()!=false&&
        (RebirthRecyclerNativeLoop.IsCurrentInvocation(original?.Station)||
        original?.NativeTickCustody==true&&RebirthProductiveEarnedRemoteHost.IsNativeHandoffTick(original.Station));
    internal static bool IsAdoptedNativeChildPreparation(TileEntityWorkstation station,RebirthProductiveEarnedRowProof.Frame frame)
        =>RebirthProductiveEarnedRemoteHost.IsNativeHandoffTick(station)&&retained.TryGetValue(station,out var parent)&&parent.NativeTickCustody&&
            ReferenceEquals(parent.EarnedChild?.Frame,frame)&&parent.EarnedChild.CurrentBefore();
    internal static bool IsAdoptedNativeChildPublication(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared artifact)
        =>RebirthProductiveEarnedRemoteHost.IsNativeHandoffTick(station)&&retained.TryGetValue(station,out var parent)&&parent.NativeTickCustody&&
            parent.EarnedChild?.Returned==true&&ReferenceEquals(parent.EarnedChild.Next?.Artifact,artifact)&&artifact.MatchesAfter(station);
    internal static void RestoreSavedNativeAtTick(TileEntityWorkstation station)
    {
        if(!Enabled||!RebirthProductiveEarnedRemoteHost.IsNativeHandoffTick(station)||retained.ContainsKey(station)||
            !RebirthProductiveEarnedSavedNativeSource.TryObserved(station,out var source)||!source.PrimaryCurrent()||
            !source.CurrentIdentity(GameManager.Instance?.World,station,source.Frame.Birth,source.Frame.Operation)||
            !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source.Native,source.NativeOffset))return;
        RetireReplacedNativeGraph(station,source);
        if(source.Terminal)
        {if(!source.PendingNextWork)TryRetireObservedNativeTerminal(station);else TryRestorePendingNativeTerminal(station);return;}
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!RebirthProductiveEarnedArtifact.TryReadRemoteObservedOriginal(station,source.Frame.Birth,source.Frame.Operation,out var artifact,out var image)||
                !decoder.Admit(source.Frame,image.Length))return;
            var candidate=new Attempt{Station=station,Artifact=artifact,Frame=source.Frame};
            if(!EffectiveNativeAfter(candidate,out var earned,out int earnedOffset)||
                !RebirthProductiveEarnedNativeBody.SameExceptElapsed(source.Native,source.NativeOffset,earned,earnedOffset))return;
            artifact.LoadedSource=source;
            AdoptRemoteSavedEarnedChild(station,artifact,source.Frame,()=>source.PrimaryCurrent()&&
                source.CurrentIdentity(GameManager.Instance?.World,station,source.Frame.Birth,source.Frame.Operation),2048);
        }
    }
    internal static bool HoldsAdoptedNativeTick(TileEntityWorkstation station)=>Enabled&&retained.TryGetValue(station,out var original)&&original.NativeTickCustody;
    internal static void PumpAdoptedNativeTick(TileEntityWorkstation station)
    {
        if(!Enabled||!RebirthProductiveEarnedRemoteHost.IsNativeHandoffTick(station)||!RebirthProductiveEarnedRemoteHost.CanRunNativeHandoffSettlement(station)||
            !retained.TryGetValue(station,out var original)||!original.NativeTickCustody)return;
        if(original.NativeAdoptionReleaseReady?.Invoke()==false)return;
        if(original.OutputTimerObserved){TryFinishNativeTerminal(original);return;}
        // Execute only the original's genuine native completion handler. Its
        // own Harmony invocation and AllowsCompletion gate issue the token;
        // the outer tick scope cannot issue or substitute that output token.
        if(paidNativeZeroHandler==null&&original.Artifact?.MatchesAfter(station)==true&&
            (original.RemoteSavedEarned!=null?original.RemoteSavedEarned():EffectivePaidNativeAfterCurrent(original)))
        {
            paidNativeZeroHandler=original;
            try{station.HandleRecipeQueue(0f);}
            finally{paidNativeZeroHandler=null;}
        }
    }
    private static bool EffectivePaidNativeAfterCurrent(Attempt original)
    {
        using(var decoder=original.Observation?.Memory?.TryEnterCallbacks()??RebirthProductiveEarnedObservationBudget.TryEnterCandidateDecoder())
            return decoder!=null&&EffectiveNativeAfter(original,out var body,out int offset)&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,body,offset);
    }
    internal static bool AdoptRemoteSavedEarnedChild(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared artifact,
        RebirthProductiveEarnedRowProof.Frame frame,Func<bool> savedSourceCurrent,long sourceBytes,Func<bool> releaseReady=null)
    {
        if(!Enabled||GameManager.Instance?.World?.IsRemote()!=false||savedSourceCurrent?.Invoke()!=true||
            artifact?.MatchesAfter(station)!=true||frame.RowIndex<0||frame.RowIndex>=station.Queue.Length||sourceBytes<0)return false;
        ObserveWorld();
        if(retained.TryGetValue(station,out var known))return known.Frame?.Operation==frame.Operation&&known.RemoteSavedEarned!=null;
        var row=station.Queue[frame.RowIndex];
        if(retained.Count>=4||row?.Recipe==null||row.Multiplier!=frame.Multiplier||row.CraftingTimeLeft>0)return false;
        var value=new Attempt{Station=station,Row=row,Recipe=row.Recipe,Multiplier=frame.Multiplier,Frame=frame,Artifact=artifact,
            NativeTickCustody=true,NativeAdoptionReleaseReady=releaseReady};
        value.RemoteSavedEarned=()=>savedSourceCurrent()&&EffectivePaidNativeAfterCurrent(value);
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*artifact.ImageLength+sourceBytes),out value.Retention))return false;
        if(!savedSourceCurrent()||!artifact.MatchesAfter(station)){value.Retention.Dispose();return false;}
        retained.Add(station,value);return true;
    }
    // The caller is the actual native UpdateTick observer and carries a
    // positively saved original output. This transfers pending custody only;
    // it cannot insert output or manufacture a HandleRecipeQueue invocation.
    internal static bool AdoptRemoteSavedNativeTerminal(TileEntityWorkstation station,
        RebirthProductiveEarnedArtifact.Prepared artifact,RebirthProductiveEarnedRowProof.Frame frame,
        RebirthProductiveEarnedNativeOutputPlan.Description plan,Func<bool> savedSourceCurrent,long sourceBytes,bool toolsRebased,Func<bool> releaseReady)
    {
        if(!Enabled||GameManager.Instance?.World?.IsRemote()!=false||savedSourceCurrent?.Invoke()!=true||
            artifact?.Current()!=true||!artifact.IsTarget(station)||plan==null||plan.PendingIndex<0)return false;
        ObserveWorld();
        if(retained.TryGetValue(station,out var known))return known.Frame?.Operation==frame.Operation&&known.Terminal?.RemoteSavedSource!=null;
        if(retained.Count>=4||station.Queue==null||plan.PendingIndex!=station.Queue.Length-1)return false;
        var row=station.Queue[plan.PendingIndex];
        if(row?.Recipe==null||row.Multiplier<=0||row.CraftingTimeLeft!=plan.PendingBefore||
            RebirthProductiveEarnedUiQueueOwner.Binding(row)!=plan.Binding||RebirthRecipeOutputs.DefinitionIdentity(row.Recipe)!=plan.Extras)return false;
        Attempt value=null;
        try
        {
            value=new Attempt{Station=station,Row=row,Recipe=row.Recipe,Multiplier=frame.Multiplier,Frame=frame,Artifact=artifact,
                NativeTickCustody=true,NativeAdoptionReleaseReady=releaseReady,
                OutputTimerObserved=true,PendingTimerObserved=true,PendingTimerIndex=plan.PendingIndex,
                PendingTimerBefore=plan.PendingBefore,PendingTimerTarget=plan.PendingTarget,PendingRecipeBinding=plan.Binding,PendingExtras=plan.Extras};
            if(sourceBytes<0||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*artifact.ImageLength+2L*(plan.Binding.Length+plan.Extras.Length)+128+sourceBytes),out value.Retention))return false;
            value.Terminal=new NativeTerminal{Original=value,World=GameManager.Instance.World,Body=(byte[])plan.Native.Clone(),ElapsedOffset=plan.Offset,
                PendingNextWork=true,SavedPending=true,RemoteSavedSource=savedSourceCurrent,RemoteToolsRebased=toolsRebased};
            value.Terminal.Memory=RebirthProductiveEarnedObservationBudget.TryOpen(artifact,frame);
            if(value.Terminal.Memory==null)return false;
            value.Terminal.Allocation=value.Terminal.Memory.TryObserveNative(value.Terminal.Body,value.Terminal.ElapsedOffset);
            if(value.Terminal.Allocation==null||!value.Terminal.Current())return false;
            ParkSavedPendingTerminal(value.Terminal);retained.Add(station,value);value=null;return true;
        }
        catch{return false;}
        finally{if(value!=null){value.Terminal?.Dispose();value.Retention?.Dispose();}}
    }
}
