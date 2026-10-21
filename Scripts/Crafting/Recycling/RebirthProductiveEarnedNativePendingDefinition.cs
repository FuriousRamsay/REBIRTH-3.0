using System;
using System.IO;
using System.Text;

internal static partial class RebirthProductiveEarnedCoordinator
{
    private static bool CapturePendingDefinition(Attempt original,RecipeQueueItem row)
    {
        try
        {
            if(!original.OutputTimerObserved||original.NativeOutput==null||!ReferenceEquals(original.NativeOutput,RebirthRecyclerNativeLoop.InvocationToken(original.Station)))return false;
            string binding=RebirthProductiveEarnedUiQueueOwner.Binding(row),extras=RebirthRecipeOutputs.DefinitionIdentity(row.Recipe);
            if(string.IsNullOrEmpty(binding)||string.IsNullOrEmpty(extras)||Encoding.UTF8.GetByteCount(binding)>RebirthProductiveEarnedRowProof.MaximumFrameBytes||
                Encoding.UTF8.GetByteCount(extras)>RebirthProductiveEarnedRowProof.MaximumFrameBytes)return false;
            if(original.PendingRecipeBinding!=null||original.PendingExtras!=null)
                return original.PendingRecipeBinding==binding&&original.PendingExtras==extras;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(2L*(binding.Length+extras.Length)+128),out var retained))return false;
            original.PendingRecipeBinding=binding;original.PendingExtras=extras;original.PendingDefinitionRetention=retained;return true;
        }
        catch{return false;}
    }
    private static bool MatchesPendingDefinition(Attempt original,RecipeQueueItem row)
        =>!string.IsNullOrEmpty(original.PendingRecipeBinding)&&!string.IsNullOrEmpty(original.PendingExtras)&&
            RebirthProductiveEarnedUiQueueOwner.Binding(row)==original.PendingRecipeBinding&&
            RebirthRecipeOutputs.DefinitionIdentity(row.Recipe)==original.PendingExtras;
    private static bool PublishPendingDefinition(Attempt original)
    {
        try
        {
            if(original.Terminal?.Current()!=true||original.PendingTimerAmbiguous||!original.PendingTimerObserved||
                string.IsNullOrEmpty(original.PendingRecipeBinding)||string.IsNullOrEmpty(original.PendingExtras))return false;
            using(var callback=original.Terminal.Memory.TryEnterCallbacks())
            {
                if(callback==null||!original.Terminal.Memory.TryReserveAllocation(checked(8L*(original.PendingRecipeBinding.Length+original.PendingExtras.Length)+1024),out var workspace))return false;
                using(workspace)
                using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true)))
                {
                    writer.Write(0x31445052u); // RPD1, captured actual successor definition and terminal-native lineage.
                    writer.Write(original.Artifact.Birth.ToByteArray());writer.Write(original.Artifact.Operation.ToByteArray());
                    writer.Write(original.PendingTimerIndex);writer.Write(original.PendingTimerBefore);writer.Write(original.PendingTimerTarget);
                    writer.Write(original.PendingRecipeBinding);writer.Write(original.PendingExtras);
                    writer.Write(RebirthProductiveEarnedNativeBody.ComparisonDigest(original.Terminal.Body,original.Terminal.ElapsedOffset));writer.Flush();
                    if(stream.Length>2L*RebirthProductiveEarnedRowProof.MaximumFrameBytes+128)return false;
                    return RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(original.Artifact.OriginalPath+".pending-definition",stream.ToArray())&&original.Terminal.Current();
                }
            }
        }
        catch{return false;}
    }
}
