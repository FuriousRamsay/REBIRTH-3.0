using System;
using HarmonyLib;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    // Actual target Chunk.read consumes the TE type, then InstantiateFromRead
    // constructs with the actual load chunk, invokes read, and returns before
    // OnReadComplete/dictionary insertion. A read on an existing equal TE is
    // therefore insufficient to identify this reconstruction.
    [HarmonyPatch(typeof(TileEntity),nameof(TileEntity.InstantiateFromRead),new[]{typeof(PooledBinaryReader),typeof(global::StreamModeRead),
        typeof(TileEntityType),typeof(Chunk),typeof(int[]),typeof(Func<int,int,int,BlockValue>)})]
    private static class CancellationInstantiate
    {
        private static void Prefix(PooledBinaryReader _br,global::StreamModeRead _eStreamMode,TileEntityType _type,Chunk _chunk,bool __runOriginal,out object __state)
        {
            __state=null;CancellationCachedPurposes purposes=null;
            try{var cached=RebirthWorkstationCompletionPipeline.EnterCachedNativeInstantiate(_br,_eStreamMode,_type,_chunk,__runOriginal);if(cached!=null)__state=purposes=new CancellationCachedPurposes{Cached=cached};}catch{/* Independent readonly purpose. */}
            try{var recycler=RebirthRecyclerPlacedSettlement.EnterRecyclerNativeInstantiate(_br,_eStreamMode,_type,_chunk,__runOriginal);if(recycler!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.Recycler=recycler;}}catch{}
            try{var earned=RebirthProductiveEarnedSavedNativeSource.EnterCachedNativeInstantiate(_br,_eStreamMode,_type,_chunk,__runOriginal);if(earned!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.Earned=earned;}}catch{}
            try{var action=RemoteResourceTransactions.EnterActionPaymentTargetInstantiate(_br,_eStreamMode,_type,_chunk,__runOriginal);if(action!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.ActionPayment=action;}}catch{}
            try{var source4=EnterSource4NativeInstantiate(_br,_eStreamMode,_type,_chunk,__runOriginal);if(source4!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.Source4=source4;}}catch{}
            try{var initialQueue=RebirthProductiveEarnedUiQueueOwner.EnterInitialNativeInstantiate(_br,_eStreamMode,_type,_chunk,__runOriginal);if(initialQueue!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.InitialQueue=initialQueue;}}catch{}
            try{var recipeOutput=RebirthRecipeOutputIntegration.EnterRecipeOutputNativeInstantiate(_br,_eStreamMode,_type,_chunk,__runOriginal);if(recipeOutput!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.RecipeOutput=recipeOutput;}}catch{}
            try
            {
                var loading=cancellationLoading;if(!__runOriginal||loading==null||!loading.Loaded||loading.Instantiating||loading.Instantiated||
                    _eStreamMode!=global::StreamModeRead.Persistency||_type!=TileEntityType.Workstation||!ReferenceEquals(_br,loading.Reader)||_chunk==null)return;
                lock(cancellationLoadGate)
                {
                    if(!loading.Original.Active||_br.BaseStream.Position!=loading.Original.Offset-8)return;
                    loading.FactoryChunk=_chunk;loading.Instantiating=true;
                    if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.Cancellation=loading;
                }
            }catch{/* Factory behavior remains entirely native. */}
        }
        private static void Postfix(TileEntity __result,PooledBinaryReader _br,Chunk _chunk,bool __runOriginal,object __state)
        {
            try
            {
                if(!__runOriginal||(__state as CancellationCachedPurposes)?.Cancellation is not CancellationLoading loading||!ReferenceEquals(cancellationLoading,loading)||!loading.Instantiating)return;
                lock(cancellationLoadGate)
                    if(loading.Original.Active&&loading.Reads==1&&loading.ReadReturned&&ReferenceEquals(loading.Reader,_br)&&
                        ReferenceEquals(loading.FactoryChunk,_chunk)&&ReferenceEquals(loading.Tile,__result)&&ReferenceEquals(__result?.GetChunk(),_chunk)&&
                        _br.BaseStream.Position==loading.Original.Offset-8+loading.Original.Tile.Length&&BindCancellationHostCustody(loading))loading.Instantiated=true;
            }catch{/* Neither an equal row nor a skipped factory is lineage. */}
            finally{if(__state is CancellationCachedPurposes purposes){if(purposes.Cached!=null)try{RebirthWorkstationCompletionPipeline.ObserveCachedNativeInstantiate(__result,_br,_chunk,__runOriginal,purposes.Cached);}catch{/* B retains unknown. */}
                if(purposes.Recycler!=null)try{RebirthRecyclerPlacedSettlement.ObserveRecyclerNativeInstantiate(__result,_br,_chunk,__runOriginal,purposes.Recycler);}catch{}
                if(purposes.Earned!=null)try{RebirthProductiveEarnedSavedNativeSource.ObserveCachedNativeInstantiate(__result,_br,_chunk,__runOriginal,purposes.Earned);}catch{}
                if(purposes.Source4!=null)try{ObserveSource4NativeInstantiate(__result,_br,_chunk,__runOriginal,purposes.Source4);}catch{}
                if(purposes.InitialQueue!=null)try{RebirthProductiveEarnedUiQueueOwner.ObserveInitialNativeInstantiate(__result,_br,_chunk,__runOriginal,purposes.InitialQueue);}catch{}
                if(purposes.RecipeOutput!=null)try{RebirthRecipeOutputIntegration.ObserveRecipeOutputNativeInstantiate(__result,_br,_chunk,__runOriginal,purposes.RecipeOutput);}catch{}
                if(purposes.ActionPayment!=null)try{RemoteResourceTransactions.ObserveActionPaymentTargetInstantiate(__result,_br,_chunk,__runOriginal,purposes.ActionPayment);}catch{}}}
        }
        private static Exception Finalizer(Exception __exception,object __state)
        {
            var purposes=__state as CancellationCachedPurposes;
            if(purposes?.Cancellation is CancellationLoading loading)
            {loading.Instantiating=false;if(__exception!=null)loading.Instantiated=false;}
            if(purposes?.Cached!=null)try{RebirthWorkstationCompletionPipeline.EndCachedNativeInstantiate(purposes.Cached,__exception);}catch{/* Preserve native result/exception. */}
            if(purposes?.Recycler!=null)try{RebirthRecyclerPlacedSettlement.EndRecyclerNativeInstantiate(purposes.Recycler,__exception);}catch{}
            if(purposes?.Earned!=null)try{RebirthProductiveEarnedSavedNativeSource.EndCachedNativeInstantiate(purposes.Earned,__exception);}catch{}
            if(purposes?.ActionPayment!=null)try{RemoteResourceTransactions.EndActionPaymentTargetInstantiate(purposes.ActionPayment,__exception);}catch{}
            if(purposes?.Source4!=null)try{EndSource4NativeInstantiate(purposes.Source4,__exception);}catch{}
            if(purposes?.InitialQueue!=null)try{RebirthProductiveEarnedUiQueueOwner.EndInitialNativeInstantiate(purposes.InitialQueue,__exception);}catch{}
            if(purposes?.RecipeOutput!=null)try{RebirthRecipeOutputIntegration.EndRecipeOutputNativeInstantiate(purposes.RecipeOutput,__exception);}catch{}
            if(purposes!=null){purposes.Cached=null;purposes.Cancellation=null;purposes.Recycler=null;purposes.Earned=null;purposes.ActionPayment=null;purposes.Source4=null;purposes.InitialQueue=null;purposes.RecipeOutput=null;}
            return __exception;
        }
    }
}
