using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiOwnerFile
{
    private sealed class LocalCachedSync
    {internal LocalOwnerScope Scope;internal RebirthWorkstationCompletionPipeline.NativeAddOriginal Source;internal RebirthProductiveEarnedUiCoordinator.Original UiSource;internal Guid Operation=>Source!=null?Source.Operation:UiSource.Binding.Operation;internal byte[] Before,After;internal int BeforeOffset,AfterOffset;internal bool Prepared,Observed;internal string OwnerEvent;}
    private static LocalCachedSync QualifyLocalCachedSync(XUiC_WorkstationWindowGroup group,RebirthWorkstationCompletionPipeline.NativeAddOriginal source=null)
    {
        if(!ThreadManager.IsMainThread()||group==null)return null;
        var matches=localOwners.Values.Where(value=>!value.Closed&&!value.Unknown&&!value.HandoffAccepted&&value.CurrentWindow()&&
            ReferenceEquals(value.Window,group)&&value.Terminal?.CurrentOwnerEvent()==true).ToArray();
        if(matches.Length!=1)return null;var scope=matches[0];
        var originals=scope.Members.Select(value=>value.NativeOriginal??value.HostLocalOriginal?.Source.Original.CachedOriginal?.LocalCachedNativeOriginal)
            .Where(value=>value!=null&&(source==null||ReferenceEquals(value,source))).Distinct().ToArray();
        LocalCachedSync result=null;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null)return null;
            foreach(var original in originals)
            {
                if(!RebirthWorkstationCompletionPipeline.TryQualifyLocalCachedSync(original,group,scope.NativeAfter,scope.AfterOffset,
                    out var before,out var after,out int offset,out int afterOffset))continue;
                if(result!=null)return null;
                result=new LocalCachedSync{Scope=scope,Source=original,Before=before,After=after,BeforeOffset=offset,AfterOffset=afterOffset,OwnerEvent=scope.Terminal.Path};
            }
            if(source==null)foreach(var original in scope.Members.Where(value=>value.Original!=null).Select(value=>value.Original).Distinct())
            {
                if(!RebirthProductiveEarnedUiCoordinator.TryQualifyLocalCachedUiSync(original,group,scope.NativeAfter,scope.AfterOffset,
                    out var before,out var after,out int offset,out int afterOffset))continue;
                if(result!=null)return null;
                result=new LocalCachedSync{Scope=scope,UiSource=original,Before=before,After=after,BeforeOffset=offset,AfterOffset=afterOffset,OwnerEvent=scope.Terminal.Path};
            }
        }
        return result;
    }
    internal static bool CanSyncLocalCachedReceipt(XUiC_WorkstationWindowGroup group)=>QualifyLocalCachedSync(group)!=null;
    internal static object CaptureLocalCachedUiSync(XUiC_WorkstationWindowGroup group,RebirthProductiveEarnedUiCoordinator.Original source)
    {
        try{var state=QualifyLocalCachedSync(group);if(state==null||!ReferenceEquals(state.UiSource,source))return null;
            state.Prepared=SaveLocalCachedPreparation(state);return state;}catch{return null;}
    }
    internal static bool EnterLocalCachedUiSync(XUiC_WorkstationWindowGroup group,RebirthProductiveEarnedUiCoordinator.Original source,out object state)
    {
        state=null;
        if(!localOwners.Values.Any(value=>!value.Closed&&!value.Unknown&&!value.HandoffAccepted&&ReferenceEquals(value.Window,group)&&
            value.Members.Any(member=>ReferenceEquals(member.Original,source))))return true;
        state=CaptureLocalCachedUiSync(group,source);return state is LocalCachedSync captured&&captured.Prepared;
    }
    internal static void ObserveLocalCachedUiSync(XUiC_WorkstationWindowGroup group,RebirthProductiveEarnedUiCoordinator.Original source,object state)
    {if(state is LocalCachedSync value&&ReferenceEquals(value.UiSource,source))ObserveLocalCachedSync(group,null,state);}
    internal static object CaptureLocalCachedSync(XUiC_WorkstationWindowGroup group,RebirthWorkstationCompletionPipeline.NativeAddOriginal source)
    {
        try
        {
            var state=QualifyLocalCachedSync(group,source);if(state==null)return null;
            state.Prepared=SaveLocalCachedPreparation(state);
            return state;
        }
        catch{return null;}
    }
    internal static void ObserveLocalCachedSync(XUiC_WorkstationWindowGroup group,RebirthWorkstationCompletionPipeline.NativeAddOriginal source,object state)
    {
        if(state is not LocalCachedSync accepted||!accepted.Prepared||!ReferenceEquals(accepted.Source,source)||!accepted.Scope.CurrentWindow()||
            !ReferenceEquals(group,accepted.Scope.Window)||accepted.Scope.Terminal?.CurrentOwnerEvent()!=true)return;
        try
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(accepted.Scope.Station,accepted.After,accepted.AfterOffset))return;
            accepted.Observed=true;accepted.Scope.CachedPending=accepted;
            PumpLocalCachedSync(accepted.Scope);
        }
        catch{/* Native insertion/sync is never replayed from missing acceptance proof. */}
    }
    private static bool SaveLocalCachedPreparation(LocalCachedSync state)
    {
        long bound=65536L+4*((state.Before.Length+2L)/3)+4*((state.After.Length+2L)/3);
        using(var workspace=state.Scope.EnterPublication(bound))return workspace!=null&&
            SaveLocalStage(state.Scope,"cached."+state.Operation.ToString("N"),new XElement("localCachedOwnerTransition",
                new XAttribute("operation",state.Operation.ToString("N")),new XAttribute("ownerEvent",state.OwnerEvent),
                new XElement("before",new XAttribute("offset",state.BeforeOffset),Convert.ToBase64String(state.Before)),
                new XElement("after",new XAttribute("offset",state.AfterOffset),Convert.ToBase64String(state.After))));
    }
    private static void PumpLocalCachedSync(LocalOwnerScope scope)
    {
        if(scope.CachedPending is not LocalCachedSync accepted||!accepted.Observed||scope.Unknown||!scope.Current()||scope.Terminal?.CurrentOwnerEvent()!=true)return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(scope.Station,accepted.After,accepted.AfterOffset))return;
        if(!accepted.Prepared)accepted.Prepared=SaveLocalCachedPreparation(accepted);
        if(!accepted.Prepared||!SaveLocalStage(scope,"cachedAccepted."+accepted.Operation.ToString("N"),new XElement("localCachedOwnerAccepted",
            new XAttribute("operation",accepted.Operation.ToString("N")),new XAttribute("nativeAfter",Hash(accepted.After)))))return;
        scope.NativeAfter=accepted.After;scope.AfterOffset=accepted.AfterOffset;scope.CachedOperation=accepted.Operation;
        scope.Watch?.Dispose();scope.Watch=null;scope.Terminal=null;scope.TerminalAccepted=false;scope.CachedPending=null;
    }
    internal static bool CanAdvanceLocalCachedReceipt(XUiC_RecipeStack card)
    {
        if(!ThreadManager.IsMainThread()||card==null)return false;
        var scopes=localOwners.Values.Where(value=>!value.Closed&&!value.Unknown&&!value.HandoffAccepted&&value.CurrentWindow()&&
            value.Terminal?.CurrentOwnerEvent()==true&&value.Members.Any(member=>member.NativeOriginal?.IsKnownCachedCard(card)==true||
                member.HostLocalOriginal?.Source.Original.CachedOriginal?.LocalCachedNativeOriginal?.IsKnownCachedCard(card)==true||
                member.Original!=null&&(RebirthProductiveEarnedUiCoordinator.IsKnownLocalCachedUiCard(member.Original,card)||
                    RebirthWorkstationCompletionPipeline.IsKnownLocalCachedUiCard(member.Original,card)))).ToArray();
        // The ordinary MP015 prefix still must acquire the exact same original
        // positive FullBoundary through TryContinueOwningCached. This predicate
        // supplies no resume, output setter or payment permission itself.
        return scopes.Length==1;
    }
    internal static bool ReleaseLocalOwnerQueueAuthority(RebirthProductiveEarnedUiCoordinator.SavedLocalQueueSuccessor successor)
    {
        if(!ThreadManager.IsMainThread()||successor?.Current()!=true)return false;
        var scope=successor.PreviousScope;
        if(scope==null||scope.Closed||!scope.TerminalAccepted||!scope.HandoffAccepted||!ReferenceEquals(scope.Terminal,successor.PreviousProof)||
            !ReferenceEquals(scope.World,successor.World)||!ReferenceEquals(scope.Station,successor.Station)||
            !localOwners.TryGetValue(scope.Operation,out var retained)||!ReferenceEquals(retained,scope))return false;
        // Positive saved successor owns the native transition. No old body
        // equality is consulted after that intentional actual queue sync.
        if(scope.Members.Any(value=>value.NativeOriginal!=null&&!value.OwnerTerminalSaved))return false;
        if(!RebirthWorkstationCompletionPipeline.RetireSucceededCachedPrimaries(successor))return false;
        if(!RebirthProductiveEarnedUiDeferredTransport.ReleaseLocalHostReceiptResources(scope,successor.Current))return false;
        foreach(var member in scope.Members.Where(value=>value.HostLocalOriginal!=null||value.NativeOriginal!=null).ToArray())
        {var receipt=member.Receipt;member.Dispose();receipt?.Dispose();}
        localOwners.Remove(scope.Operation);scope.Dispose();return true;
    }
    internal static bool ReleaseEmptyLocalOwnerQueueAuthority(RebirthProductiveEarnedUiCoordinator.SavedEmptyLocalQueue successor)
    {
        if(!ThreadManager.IsMainThread()||successor?.Current()!=true)return false;
        var scope=successor.PreviousScope;
        if(scope==null||scope.Closed||!scope.TerminalAccepted||!scope.HandoffAccepted||!ReferenceEquals(scope.Terminal,successor.PreviousProof)||
            !ReferenceEquals(scope.World,successor.World)||!ReferenceEquals(scope.Station,successor.Station)||
            !localOwners.TryGetValue(scope.Operation,out var retained)||!ReferenceEquals(retained,scope)||
            scope.Members.Any(value=>value.NativeOriginal!=null&&!value.OwnerTerminalSaved||
                value.Original!=null&&(!value.Original.Delivered||!value.Original.TerminalSaved)))return false;
        // The private issuer proves the actual empty nochange sync. This is
        // resource retirement only; original durable event/attempt records
        // remain and no future row inherits this owner's source.
        if(!RebirthWorkstationCompletionPipeline.RetireEmptyCachedPrimaries(successor))return false;
        if(!RebirthProductiveEarnedUiDeferredTransport.ReleaseLocalHostReceiptResources(scope,successor.Current))return false;
        foreach(var member in scope.Members.Where(value=>value.HostLocalOriginal!=null||value.NativeOriginal!=null).ToArray())
        {var receipt=member.Receipt;member.Dispose();receipt?.Dispose();}
        localOwners.Remove(scope.Operation);scope.Dispose();return true;
    }
}
