using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedRemoteClient
{
    private sealed class NativeOutput:IDisposable
    {
        internal Original Original;internal object NativeToken;internal bool Attempted,Admitted,AddReturned,TimerObserved,Returned,Header,Saved;
        internal RebirthProductiveEarnedNativeOutputPlan.Description Plan;internal byte[] Record;internal IDisposable Memory;
        internal ItemValue ProjectionItem;internal ItemStack[] ProjectionExtras;
        public void Dispose(){var held=Memory;Memory=null;Original=null;NativeToken=null;Plan=null;Record=null;ProjectionItem=null;ProjectionExtras=null;held?.Dispose();}
    }
    private static bool PumpNativeOutput(Original original,double now)
    {
        if(!original.Published&&original.Output==null)return false;
        original.NextQuery=now+1;
        if(PumpNativeTools(original))return true;
        if(PumpNativePending(original,now))return true;
        if(original.Output==null)
        {
            if(File.Exists(original.Path+".outputAttempt")||File.Exists(original.Path+".outputAttempt.tmp"))return true;
            if(!original.After()||!MatchesStage(original,"published",original.Nonce)||!HasNativeOutputCapacity(original,out long outputGraph)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*original.Image.Length+4L*RebirthProductiveEarnedNativeBody.MaximumBodyBytes+outputGraph+65536),out var held))return true;
            original.Output=new NativeOutput{Original=original,Memory=held,Plan=new RebirthProductiveEarnedNativeOutputPlan.Description{
                Operation=original.Frame.Operation,Birth=original.Frame.Birth,Nonce=original.Nonce,OriginalDigest=original.Digest,Binding="",Extras=""}};
            if(!Stage(original,"outputAttempt",original.Nonce))return true;
            original.Output.Attempted=true;
            var previous=executing;executing=original;
            try{RebirthRecyclerTransport.ExecuteEarnedOriginal(original.Actor,original.Station,()=>original.Station.HandleRecipeQueue(0));}
            finally{executing=previous;}
        }
        var output=original.Output;
        if(!output.Returned||output.Record==null||!CurrentNativeOutputOwner(original)||
            !Read(original.Path+".nativeOutput",RebirthProductiveEarnedRowProof.MaximumArtifactBytes).SequenceEqual(output.Record))return true;
        if(RebirthProductiveEarnedWire.TryReadReply(original.Frame.Operation,out var reply)&&reply.Nonce==original.Nonce&&reply.Digest==original.Digest&&
            reply.Birth==original.Frame.Birth&&reply.Owner==original.Owner&&reply.Creation==original.Creation&&reply.World==original.WorldKey&&
            reply.Deaths==original.Deaths&&reply.Position.Equals(original.Station.ToWorldPos()))
        {
            if(reply.State==RebirthProductiveEarnedWire.Status.NativeOutputHeader)output.Header=true;
            if(reply.State==RebirthProductiveEarnedWire.Status.NativeOutputSaved&&Stage(original,"nativeOutputSaved",original.Nonce)){output.Saved=true;original.Memory.RetainNativeTerminalAllocation();}
        }
        if(output.Header&&!output.Saved)
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var connection=manager?.connectionToServer?[0];
            if(manager?.IsClient==true&&!manager.IsServer&&RebirthLogisticsOriginalTransport.CanSendNative(connection)&&
                !File.Exists(original.Path+".nativeOutputUpload")&&Stage(original,"nativeOutputUpload",original.Nonce))
                manager.SendToServer(NetPackageManager.GetPackage<NetPackageTileEntity>().Setup(original.Station,StreamModeWrite.ToServer));
        }
        RebirthProductiveEarnedWire.Send(original.Actor,original.Station,RebirthProductiveEarnedWire.Kind.NativeOutputPlan,original.Frame.Operation,
            original.Nonce,output.Record,null);
        return true;
    }
    private static bool CurrentNativeOutputOwner(Original original)
    {
        using(var callbacks=original.Memory?.TryEnterCallbacks())return callbacks!=null&&original.Current()&&(original.ViewerToolsOnly||original.Output?.Returned==true)&&
            RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,EffectiveNativeOutput(original).Native,EffectiveNativeOutput(original).Offset);
    }
    private static bool LoadNativeOutputDescription(Original original,RebirthProductiveEarnedObservationBudget.Decoder decoder)
    {
        string path=original.Path+".nativeOutput";
        if(!File.Exists(path))return !File.Exists(path+".tmp");
        if(!original.Published||!MatchesSavedStageDescription(original,"outputAttempt",original.Nonce)||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*new FileInfo(path).Length+65536),out var held))return false;
        try
        {
            var record=Read(path,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
                if(decoder==null||!RebirthProductiveEarnedNativeOutputPlan.TryRead(record,original.Frame,original.Nonce,original.Digest,out var plan)||
                    !original.Identity()||!RebirthProductiveEarnedNativeOutputPlan.TryMeasureRetention(plan,record.Length,out long retainedBytes)||
                    !RebirthProductiveEarnedObservationBudget.TryResizeDetached(held,retainedBytes))return false;
                original.Output=new NativeOutput{Original=original,Attempted=true,Returned=true,AddReturned=true,TimerObserved=true,Plan=plan,Record=record,Memory=held,
                    Saved=MatchesSavedStageDescription(original,"nativeOutputSaved",original.Nonce)};if(original.Output.Saved)original.Memory?.RetainNativeTerminalAllocation();held=null;return true;
        }
        finally{held?.Dispose();}
    }
    private static bool HasNativeOutputCapacity(Original original,out long graph)
    {
        graph=0;
        using(var callbacks=original.Memory?.TryEnterCallbacks())
        {
            if(callbacks==null||!original.Current()||RebirthProductiveEarnedUiQueueOwner.Binding(original.Row)!=original.Frame.FullRecipeBinding||
                !RebirthRecipeOutputs.TryPlan(original.Recipe,1,out var extras,out _))return false;
            var item=new ItemValue(original.Recipe.itemValueType);
            if(item.ItemClass?.HasQuality==true)item=new ItemValue(original.Recipe.itemValueType,original.Row.Quality,original.Row.Quality);
            var primary=new ItemStack(item,original.Recipe.count);var additional=extras.ToArray();
            return RebirthProductiveEarnedNativeOutputPlan.TryMeasureItems(primary,additional,out graph)&&
                RebirthRecipeOutputIntegration.Predict(original.Station.Output,primary,-1,extras,out _,out _);
        }
    }
    internal static bool AllowsNativeOutput(TileEntityWorkstation station,RecipeQueueItem row)
    {
        var original=executing;var output=original?.Output;
        if(original==null||!ReferenceEquals(original.Station,station)||output?.Attempted!=true||output.TimerObserved||output.Returned||
            !original.Published||!MatchesStage(original,"published",original.Nonce)||!original.After()||!ReferenceEquals(row,original.Row))return false;
        object token=RebirthRecyclerNativeLoop.InvocationToken(station);if(token==null)return false;
        if(output.NativeToken!=null&&!ReferenceEquals(output.NativeToken,token))return false;
        output.NativeToken=token;output.Admitted=true;return true;
    }
    internal static object EnterNativeOutputAdd(TileEntityWorkstation station,int owner,ItemValue item,string name,string scrapped,int xp,int count,bool run)
    {
        var original=executing;var output=original?.Output;
        if(!run||output?.Admitted!=true||output.AddReturned||!ReferenceEquals(original.Station,station)||!original.Current()||
            !ReferenceEquals(output.NativeToken,RebirthRecyclerNativeLoop.InvocationToken(station))||!ReferenceEquals(station.Queue?.LastOrDefault(),original.Row)||
            original.Row.Multiplier!=original.Frame.Multiplier||owner!=original.Row.StartingEntityId||item?.type!=original.Recipe.itemValueType||
            name!=original.Recipe.GetName()||scrapped!=string.Empty||xp!=original.Recipe.craftExpGain||count!=original.Recipe.count||
            RebirthRecipeOutputs.DefinitionIdentity(original.Recipe)!=original.Frame.ExtrasDefinition)return null;
        if(RebirthRecipeOutputs.HasOutputs(original.Recipe)&&(!ReferenceEquals(output.ProjectionItem,item)||output.ProjectionExtras==null))return null;
        output.Plan.Primary=new ItemStack(item.Clone(),count);output.Plan.Additional=output.ProjectionExtras??Array.Empty<ItemStack>();return output;
    }
    internal static void ObserveNativeOutputProjection(TileEntityWorkstation station,ItemStack primary,System.Collections.Generic.IReadOnlyList<ItemStack> extras)
    {
        try
        {
            var original=executing;var output=original?.Output;
            if(output?.Admitted!=true||output.AddReturned||output.ProjectionExtras!=null||!ReferenceEquals(original.Station,station)||
                !ReferenceEquals(output.NativeToken,RebirthRecyclerNativeLoop.InvocationToken(station))||!original.Current()||primary?.itemValue==null||extras==null)return;
            output.ProjectionExtras=extras.Select(value=>value.Clone()).ToArray();output.ProjectionItem=primary.itemValue;
        }
        catch{/* Read-only observer cannot alter the original insertion. */}
    }
    internal static void ObserveNativeOutputAdd(TileEntityWorkstation station,bool run,object state)
    {
        if(state is not NativeOutput output||!run||!ReferenceEquals(output.Original,executing)||!output.Original.Current()||
            !ReferenceEquals(output.Original.Station,station)||!ReferenceEquals(output.NativeToken,RebirthRecyclerNativeLoop.InvocationToken(station)))return;
        output.AddReturned=true;
    }
    internal static void ObserveNativeOutputTimer(TileEntityWorkstation station,RecipeQueueItem row)
    {
        var original=executing;var output=original?.Output;
        if(output?.AddReturned==true&&ReferenceEquals(original.Station,station)&&ReferenceEquals(row,original.Row)&&row.Multiplier==original.Frame.Multiplier-1&&
            ReferenceEquals(output.NativeToken,RebirthRecyclerNativeLoop.InvocationToken(station)))output.TimerObserved=true;
    }
    internal static bool DeferNativeOutputSuccessor(TileEntityWorkstation station,RecipeQueueItem row,float before,float target)
    {
        var original=executing;var output=original?.Output;
        if(output?.TimerObserved!=true||!ReferenceEquals(original.Station,station)||!ReferenceEquals(output.NativeToken,RebirthRecyclerNativeLoop.InvocationToken(station))||row?.Recipe==null||row.Multiplier<=0)return false;
        bool same=ReferenceEquals(row,original.Row)&&row.Multiplier==original.Frame.Multiplier-1;
        if(!same&&target==before)return false;
        if(!original.Current()||row.CraftingTimeLeft!=before||float.IsNaN(target)||float.IsInfinity(target)||
            (same?target!=before+row.OneItemCraftTime:target>before))return true;
        var plan=output.Plan;int index=Array.IndexOf(station.Queue,row);
        if(plan.PendingIndex>=0&&(plan.PendingIndex!=index||plan.PendingBefore!=before||plan.PendingTarget!=target))return true;
        plan.PendingIndex=index;plan.PendingBefore=before;plan.PendingTarget=target;
        plan.Binding=RebirthProductiveEarnedUiQueueOwner.Binding(row);plan.Extras=RebirthRecipeOutputs.DefinitionIdentity(row.Recipe);return true;
    }
    internal static void ObserveNativeOutputReturned(TileEntityWorkstation station,bool returned)
    {
        try
        {
        var original=executing;var output=original?.Output;
        if(!returned||output?.TimerObserved!=true||!output.AddReturned||output.Returned||!ReferenceEquals(original.Station,station)||
            !ReferenceEquals(output.NativeToken,RebirthRecyclerNativeLoop.InvocationToken(station))||!original.Current())return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(station,out output.Plan.Native,out output.Plan.Offset)||
                !RebirthProductiveEarnedNativeOutputPlan.Validate(original.Frame,output.Plan))return;
            var record=RebirthProductiveEarnedNativeOutputPlan.Encode(output.Plan);
            output.ProjectionItem=null;output.ProjectionExtras=null;
            if(!RebirthProductiveEarnedNativeOutputPlan.TryMeasureRetention(output.Plan,record.Length,out long retainedBytes)||
                !RebirthProductiveEarnedObservationBudget.TryResizeDetached(output.Memory,retainedBytes))return;
            if(!WriteExact(original.Path+".nativeOutput",record,RebirthProductiveEarnedRowProof.MaximumArtifactBytes))return;
            output.Record=record;output.Returned=true;output.NativeToken=null;
        }
        }
        catch{/* The original native return/exception is preserved; this attempt stays held. */}
    }
}
