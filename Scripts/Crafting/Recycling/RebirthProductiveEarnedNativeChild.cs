using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

internal static partial class RebirthProductiveEarnedCoordinator
{
    internal sealed class NativeEarnedChild:IDisposable
    {
        internal Attempt Parent;
        internal RecipeQueueItem Row;
        internal Recipe Recipe;
        internal RebirthProductiveEarnedRowProof.Frame Frame;
        internal ItemStack[] Tools;
        internal byte[] Image,Link;
        internal RebirthProductiveEarnedArtifact.Prepared Artifact;
        internal IDisposable Retention;
        internal bool Closed,Entered,Returned;
        internal Attempt Next;
        internal bool CurrentBefore()
        {
            try
            {
                using(var callbacks=Parent?.Terminal?.Memory?.TryEnterCallbacks())
                return callbacks!=null&&!Closed&&Parent?.Terminal?.Current()==true&&Parent.PartialSuccessor==null&&
                    !Parent.PendingTimerAmbiguous&&Parent.PendingTimerObserved&&
                    ReferenceEquals(Parent.Station.Queue?.LastOrDefault(),Row)&&ReferenceEquals(Row.Recipe,Recipe)&&
                    Row.Multiplier==Frame.Multiplier&&RebirthProductiveEarnedUiQueueOwner.Binding(Row)==Frame.FullRecipeBinding&&
                    RebirthRecipeOutputs.DefinitionIdentity(Recipe)==Frame.ExtrasDefinition&&
                    RebirthProductiveEarnedRowProof.CaptureRow(Row).SequenceEqual(Frame.BeforeRow)&&
                    RebirthProductiveEarnedArtifact.SameTools(Parent.Station.Tools,Frame.BeforeTools);
            }
            catch{return false;}
        }
        public void Dispose()
        {
            if(Closed)return;
            // Observation disposal can retain a retryable native save request.
            // Keep that exact Next and its charged retention until it returns;
            // closing first would make the remaining cleanup unreachable.
            Next?.Observation?.Dispose();
            var retention=Retention;Retention=null;Closed=true;
            Next=null;Parent=null;Row=null;Recipe=null;Frame=null;Tools=null;Image=Link=null;Artifact=null;
            retention?.Dispose();
        }
    }
    // Called only after the predecessor's full native terminal and historical
    // receipt are durable. This creates the exact child's immutable original;
    // it changes no marker, tools, timer, output, receipt or multiplier.
    private static bool TryPrepareEarnedNativeChild(Attempt parent)
    {
        if(parent?.Terminal?.Current()!=true||parent.PartialSuccessor!=null||parent.PendingTimerAmbiguous||
            !parent.PendingTimerObserved||!IsNativeSettlementInvocation(parent))return false;
        if(parent.EarnedChild==null&&parent.Terminal.SavedOrigin!=null&&
            (File.Exists(parent.Artifact.OriginalPath+".earned-child")||File.Exists(parent.Artifact.OriginalPath+".earned-child.tmp")))
            return TryRestoreStagedNativeChild(parent);
        try
        {
            using(var callbacks=parent.Terminal.Memory.TryEnterCallbacks())
            {
                if(callbacks==null||!RebirthProductiveEarnedNativeBody.TryMeasure(parent.Terminal.Body,parent.Terminal.ElapsedOffset,
                    out long graph,out long scratch)||!parent.Terminal.Memory.TryReserveAllocation(
                        checked(8L*(parent.Artifact.ImageLength+parent.Terminal.Body.Length+262144)+2*graph+scratch),out var workspace))return false;
                using(workspace)
                {
                    var child=parent.EarnedChild;
                    if(child==null)
                    {
                        var station=parent.Station;int index=parent.PendingTimerIndex;
                        if(index<0||index!=station.Queue.Length-1)return false;
                        var row=station.Queue[index];
                        if(row?.Recipe==null||row.Multiplier<=0||row.RepairItem!=null||row.AmountToRepair!=0||
                            !MatchesPendingDefinition(parent,row)||
                            RebirthRecyclerRuntime.IsMarked(row.Recipe)||RebirthCookingHeat.Managed(row.Recipe)||
                            row.CraftingTimeLeft!=parent.PendingTimerBefore||
                            !TryEarnedNativeChildWork(parent.PendingTimerBefore,parent.PendingTimerTarget,row.OneItemCraftTime,
                                out float work,out float target)||
                            RebirthProductiveToolWork.Limit(station.Tools,row.Recipe,station.block,work)!=work)return false;
                        var tools=RebirthProductiveToolWork.Wear(station.Tools,row.Recipe,station.block,work);
                        if(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(station.Tools,262144,349528,out var beforeTools)||
                            !RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(tools,262144,349528,out var afterTools))return false;
                        var afterRow=new RecipeQueueItem{Recipe=row.Recipe,Multiplier=row.Multiplier,CraftingTimeLeft=target,
                            IsCrafting=row.IsCrafting,Quality=row.Quality,StartingEntityId=row.StartingEntityId,OneItemCraftTime=row.OneItemCraftTime,
                            RepairItem=row.RepairItem,AmountToRepair=row.AmountToRepair};
                        var frame=new RebirthProductiveEarnedRowProof.Frame{Birth=parent.Artifact.CustodyBirth,Operation=Guid.NewGuid(),
                            World=parent.Frame.World,RowIndex=index,Multiplier=row.Multiplier,WorkedSeconds=work,
                            BeforeRow=RebirthProductiveEarnedRowProof.CaptureRow(row),AfterRow=RebirthProductiveEarnedRowProof.CaptureRow(afterRow),
                            BeforeTools=Convert.FromBase64String(beforeTools),AfterTools=Convert.FromBase64String(afterTools),
                            FullRecipeBinding=RebirthProductiveEarnedUiQueueOwner.Binding(row),ExtrasDefinition=RebirthRecipeOutputs.DefinitionIdentity(row.Recipe)};
                        if(!RebirthProductiveEarnedNativeBody.TryCapturePlan(station,tools,index,target,
                            out frame.BeforeNative,out frame.AfterNative,out frame.BeforeElapsedOffset,out frame.AfterElapsedOffset)||
                            !RebirthProductiveEarnedNativeBody.SameExceptElapsed(frame.BeforeNative,frame.BeforeElapsedOffset,
                                parent.Terminal.Body,parent.Terminal.ElapsedOffset))return false;
                        var image=RebirthProductiveEarnedRowProof.Encode(frame);
                        if(!RebirthProductiveEarnedObservationBudget.TryMeasureTools(frame.AfterTools,out long toolGraph,out _)||
                            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*image.Length+toolGraph+1024),out var retained))return false;
                        child=new NativeEarnedChild{Parent=parent,Row=row,Recipe=row.Recipe,Frame=frame,Tools=tools,Image=image,Retention=retained};
                        // Keep the exact ID/frame even if artifact publication
                        // refuses. A later invocation retries this original.
                        parent.EarnedChild=child;
                    }
                    if(!child.CurrentBefore())return false;
                    if(child.Artifact==null)
                    {if(!RebirthProductiveEarnedArtifact.TryPrepare(parent.Station,child.Frame,out var artifact))return false;child.Artifact=artifact;}
                    if(child.Artifact.Current()!=true||!child.CurrentBefore())return false;
                    if(child.Link==null)
                    {
                        using(var buffer=new MemoryStream())using(var writer=new BinaryWriter(buffer))using(var sha=SHA256.Create())
                        {
                            writer.Write(0x31434552u); // REC1: immutable predecessor → exact child-original linkage.
                            writer.Write(parent.Artifact.Birth.ToByteArray());writer.Write(parent.Artifact.Operation.ToByteArray());
                            writer.Write(child.Artifact.Birth.ToByteArray());writer.Write(child.Artifact.Operation.ToByteArray());
                            writer.Write(sha.ComputeHash(parent.Artifact.CopyImage()));writer.Write(sha.ComputeHash(child.Image));
                            writer.Write(RebirthProductiveEarnedNativeBody.ComparisonDigest(parent.Terminal.Body,parent.Terminal.ElapsedOffset));
                            writer.Flush();child.Link=buffer.ToArray();
                        }
                    }
                    return RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(parent.Artifact.OriginalPath+".earned-child",child.Link)&&
                        child.CurrentBefore()&&child.Artifact.Current();
                }
            }
        }
        catch{return false;}
    }
    internal static bool TryEarnedNativeChildWork(float before,float target,float unit,out float work,out float normalized)
    {
        work=0;normalized=target;
        if(float.IsNaN(before)||float.IsInfinity(before)||float.IsNaN(target)||float.IsInfinity(target)||
            float.IsNaN(unit)||float.IsInfinity(unit)||unit<=0||target>0)return false;
        if(before<0){if(target!=before+unit)return false;work=unit;}
        else{if(before<=0||target>=before)return false;work=before;}
        if(target==0)normalized=-float.Epsilon;
        return work>0&&!float.IsNaN(work)&&!float.IsInfinity(work);
    }
}
