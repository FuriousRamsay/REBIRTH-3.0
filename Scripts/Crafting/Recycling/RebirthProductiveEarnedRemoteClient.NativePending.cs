using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedRemoteClient
{
    private sealed class NativePending:IDisposable
    {
        internal RebirthProductiveEarnedNativePendingPlan.Description Plan;internal byte[] Record;internal IDisposable Memory;
        internal Guid ChildNonce;internal bool Header,Entered,Returned,Saved;
        public void Dispose(){Memory?.Dispose();Memory=null;Plan=null;Record=null;}
    }
    private static bool PumpNativePending(Original original,double now)
    {
        if(original.Output?.Saved!=true||original.Output.Plan.PendingIndex<0)return false;
        if(original.Pending==null&&File.Exists(original.Path+".nativePending"))
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                if(decoder==null||!LoadNativePendingDescription(original,decoder))return true;
        }
        if(original.Pending==null)
        {
            if(!CurrentNativeOutputOwner(original)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(24L*original.Image.Length+8L*RebirthProductiveEarnedNativeBody.MaximumBodyBytes+65536),out var memory))return true;
            try
            {
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(decoder==null||!RebirthProductiveEarnedNativePendingPlan.TryCreate(original.Station,original.Frame,original.Nonce,original.Digest,EffectiveNativeOutput(original),out var plan))return true;
                    byte[] record=RebirthProductiveEarnedNativePendingPlan.Encode(plan);
                    if(!RebirthProductiveEarnedNativePendingPlan.TryMeasureRetention(plan,record.Length,out long retainedBytes)||
                        !RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,retainedBytes)||!WriteExact(original.Path+".nativePending",record,RebirthProductiveEarnedRowProof.MaximumArtifactBytes))return true;
                    original.Pending=new NativePending{Plan=plan,Record=record,Memory=memory};memory=null;
                }
            }
            finally{memory?.Dispose();}
        }
        var value=original.Pending;
        if(!original.Current()||!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(original.Path+".nativePending",value.Record,RebirthProductiveEarnedRowProof.MaximumArtifactBytes))return true;
        if(RebirthProductiveEarnedWire.TryReadReply(original.Frame.Operation,out var reply)&&reply.Nonce==original.Nonce&&reply.Digest==original.Digest&&
            reply.Birth==original.Frame.Birth&&reply.Owner==original.Owner&&reply.Creation==original.Creation&&reply.World==original.WorldKey&&reply.Deaths==original.Deaths&&reply.Position.Equals(original.Station.ToWorldPos()))
        {
            if(reply.State==RebirthProductiveEarnedWire.Status.NativePendingHeader||reply.State==RebirthProductiveEarnedWire.Status.NativePendingSaved)
            {
                if(value.Plan.Child!=null)
                {
                    string[] fields=(reply.QueueOwner??"").Split(':');
                    if(fields.Length!=2||!Guid.TryParseExact(fields[0],"N",out var childNonce)||childNonce==Guid.Empty||fields[1]!=Hash(value.Plan.ChildImage)||
                        value.ChildNonce!=Guid.Empty&&value.ChildNonce!=childNonce)return true;
                    if(!WriteExact(original.Path+".nativeChildNonce",System.Text.Encoding.ASCII.GetBytes(childNonce.ToString("N")),64))return true;
                    value.ChildNonce=childNonce;
                }
                value.Header=true;
                if(reply.State==RebirthProductiveEarnedWire.Status.NativePendingSaved&&value.Returned&&
                    MatchesSavedStageDescription(original,"nativePendingUpload",original.Nonce)&&Stage(original,"nativePendingSaved",original.Nonce))value.Saved=true;
            }
        }
        if(value.Header&&!value.Entered&&!File.Exists(original.Path+".nativePendingAttempt")&&!File.Exists(original.Path+".nativePendingAttempt.tmp")&&CurrentNativeOutputOwner(original))
        {
            value.Entered=true;
            if(!Stage(original,"nativePendingAttempt",original.Nonce))return true;
            var previous=executing;executing=original;
            try{RebirthRecyclerTransport.ExecuteEarnedOriginal(original.Actor,original.Station,()=>original.Station.HandleRecipeQueue(0));}
            finally{executing=previous;}
            using(var callbacks=original.Memory.TryEnterCallbacks())
                if(callbacks!=null&&value.Returned&&RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,value.Plan.After,value.Plan.AfterOffset))
                    Stage(original,"nativePendingReturned",original.Nonce);
        }
        if(value.Returned&&!value.Saved&&MatchesSavedStageDescription(original,"nativePendingReturned",original.Nonce)&&
            !File.Exists(original.Path+".nativePendingUpload")&&!File.Exists(original.Path+".nativePendingUpload.tmp"))
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var connection=manager?.connectionToServer?[0];
            using(var callbacks=original.Memory.TryEnterCallbacks())
                if(callbacks!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,value.Plan.After,value.Plan.AfterOffset)&&
                    manager?.IsClient==true&&!manager.IsServer&&RebirthLogisticsOriginalTransport.CanSendNative(connection)&&Stage(original,"nativePendingUpload",original.Nonce))
                    manager.SendToServer(NetPackageManager.GetPackage<NetPackageTileEntity>().Setup(original.Station,StreamModeWrite.ToServer));
        }
        RebirthProductiveEarnedWire.Send(original.Actor,original.Station,RebirthProductiveEarnedWire.Kind.NativePendingPlan,original.Frame.Operation,original.Nonce,value.Record,null);
        return true;
    }
    internal static void ApplyNativePendingOnEnter(TileEntityWorkstation station)
    {
        var original=executing;var value=original?.Pending;
        if(value?.Header!=true||!value.Entered||value.Returned||!ReferenceEquals(original.Station,station)||!RebirthRecyclerNativeLoop.IsCurrentInvocation(station))return;
        using(var callbacks=original.Memory.TryEnterCallbacks())
        {
            if(callbacks==null||!CurrentNativeOutputOwner(original)||!MatchesStage(original,"nativePendingAttempt",original.Nonce)||
                !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(original.Path+".nativePending",value.Record,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,value.Plan.Before,value.Plan.BeforeOffset))return;
            // Exactly one entered native invocation owns these original setters.
            // The WAL was published before entering; an unknown attempt never retries.
            station.Tools=value.Plan.Tools;station.Queue[value.Plan.Index].CraftingTimeLeft=value.Plan.AfterTimer;station.setModified();
            value.Returned=RebirthProductiveEarnedNativeBody.MatchesCurrent(station,value.Plan.After,value.Plan.AfterOffset);
        }
    }
    private static bool LoadNativePendingDescription(Original original,RebirthProductiveEarnedObservationBudget.Decoder decoder)
    {
        string path=original.Path+".nativePending";
        if(!File.Exists(path))return !File.Exists(path+".tmp");
        if(original.Output?.Saved!=true||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*new FileInfo(path).Length+65536),out var held))return false;
        try
        {
            var bytes=Read(path,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
            if(decoder==null||!RebirthProductiveEarnedNativePendingPlan.TryRead(bytes,original.Frame,original.Nonce,original.Digest,EffectiveNativeOutput(original),original.Station.block,out var plan)||
                !RebirthProductiveEarnedNativePendingPlan.TryMeasureRetention(plan,bytes.Length,out long retainedBytes)||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(held,retainedBytes))return false;
            var value=new NativePending{Plan=plan,Record=bytes,Memory=held,Entered=MatchesSavedStageDescription(original,"nativePendingAttempt",original.Nonce),
                Returned=MatchesSavedStageDescription(original,"nativePendingReturned",original.Nonce),Saved=MatchesSavedStageDescription(original,"nativePendingSaved",original.Nonce)};
            if(plan.Child!=null&&File.Exists(original.Path+".nativeChildNonce"))
            {if(!Guid.TryParseExact(System.Text.Encoding.ASCII.GetString(Read(original.Path+".nativeChildNonce",64)),"N",out value.ChildNonce)||value.ChildNonce==Guid.Empty)return false;}
            if(value.Returned&&!value.Entered||value.Saved&&(!value.Returned||!MatchesSavedStageDescription(original,"nativePendingUpload",original.Nonce)))return false;
            original.Pending=value;held=null;return true;
        }
        finally{held?.Dispose();}
    }
}
