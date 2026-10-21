using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private sealed class SelectedNativeSource:IDisposable
    {
        internal NetPackageTileEntity Packet;internal World World;internal TileEntityWorkstation Station;
        internal byte[] Body;internal byte Handle;internal int Block,ElapsedOffset=-1;internal Vector3i Position;internal IDisposable Allocation;
        internal ulong SetupClock,SetupBeforeTick,SetupAfterTick;
        internal bool Candidate()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&World?.IsRemote()==false&&
            ReferenceEquals(World.GetTileEntity(Position),Station)&&World.GetBlock(Position).type==Block&&Packet.handle==Handle&&
            Packet.teBlockId==Block&&Packet.teWorldPos.Equals(Position);
        internal bool Current()
        {
            if(!ThreadManager.IsMainThread()||Body==null||!ReferenceEquals(GameManager.Instance?.World,World)||World.IsRemote()||
                !ReferenceEquals(World.GetTileEntity(Position),Station)||World.GetBlock(Position).type!=Block||Packet.handle!=Handle||
                Packet.teBlockId!=Block||!Packet.teWorldPos.Equals(Position)||Packet.ms==null||Packet.ms.Length!=Body.Length)return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return false;
                lock(Packet.ms)
                {
                    long previous=Packet.ms.Position;
                    try{Packet.ms.Position=0;for(int index=0;index<Body.Length;index++)if(Packet.ms.ReadByte()!=Body[index])return false;}
                    finally{Packet.ms.Position=previous;}
                }
                ulong tick=GameTimer.Instance.ticks;
                if(ElapsedOffset<0||SetupBeforeTick<SetupClock||SetupAfterTick<SetupBeforeTick||tick<SetupAfterTick||Station.lastTickTime!=SetupClock)return false;
                ulong elapsed=BitConverter.ToUInt64(Body,ElapsedOffset);
                return elapsed>=SetupBeforeTick-SetupClock&&elapsed<=SetupAfterTick-SetupClock&&
                    RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,Body,ElapsedOffset);
            }
        }
        public void Dispose(){Body=null;Allocation?.Dispose();Allocation=null;}
    }
    private static readonly ConditionalWeakTable<NetPackageTileEntity,SelectedNativeSource> selectedNativeSources=new ConditionalWeakTable<NetPackageTileEntity,SelectedNativeSource>();
    // Actual Setup identity is sufficient only to HOLD a known hydration when
    // the full-byte observation budget is busy. It cannot mint a grant.
    internal static bool TryGetKnownNativeCandidate(NetPackageTileEntity native,out World world,out TileEntityWorkstation station)
    {
        world=null;station=null;
        try
        {
            if(native==null||!selectedNativeSources.TryGetValue(native,out var source)||!source.Candidate())return false;
            world=source.World;station=source.Station;return true;
        }
        catch{return false;}
    }
    internal static bool TryGetSelectedNativeSource(NetPackageTileEntity native,out World world,out TileEntityWorkstation station)
    {
        world=null;station=null;
        try
        {
            if(native==null||!selectedNativeSources.TryGetValue(native,out var source)||!source.Current())return false;
            world=source.World;station=source.Station;return true;
        }
        catch{return false;}
    }
    [HarmonyPatch(typeof(NetPackageTileEntity),nameof(NetPackageTileEntity.Setup),new[]{typeof(TileEntity),typeof(StreamModeWrite),typeof(byte)})]
    private static class NativeSetupSource
    {
        private sealed class SetupClock {internal TileEntityWorkstation Station;internal ulong Clock,Tick;}
        private static void Prefix(NetPackageTileEntity __instance,TileEntity _te,StreamModeWrite _eStreamMode,out SetupClock __state)
        {
            __state=null;
            RebirthProductiveEarnedUiOwnedNativeSend.BeginSetup(__instance,_te,_eStreamMode);
            RebirthRecyclerPlacedSettlement.BeginRecyclerNativeSetup(__instance,_te,_eStreamMode);
            RebirthProductiveEarnedUiRemote.BeginQueueResponseSetup(__instance,_te,_eStreamMode);
            RebirthWorkstationCompletionPipeline.BeginRemoteNativeSetup(__instance,_te,_eStreamMode);
            BeginRemoteCloseEchoSetup(__instance,_te,_eStreamMode);
            RebirthProductiveEarnedRemoteHost.BeginOriginalEchoSetup(__instance,_te,_eStreamMode);
            BeginCachedPhysicalEchoSetup(__instance,_te,_eStreamMode);
            if(selectedNativeSources.TryGetValue(__instance,out var prior)){selectedNativeSources.Remove(__instance);prior.Dispose();}
            if(ThreadManager.IsMainThread()&&_eStreamMode==StreamModeWrite.ToClient&&_te is TileEntityWorkstation station)
                __state=new SetupClock{Station=station,Clock=station.lastTickTime,Tick=GameTimer.Instance.ticks};
        }
        private static void Postfix(NetPackageTileEntity __instance,TileEntity _te,StreamModeWrite _eStreamMode,bool __runOriginal,SetupClock __state)
        {
            try{RebirthProductiveEarnedRemoteClient.ObserveRemoteToolsSetup(__instance,_te,_eStreamMode,__runOriginal);}catch{}
            RebirthCookingMillingPublication.ObserveNativeSetup(__instance,_te,_eStreamMode,__runOriginal);
            RebirthProductiveEarnedUiOwnedNativeSend.ObserveSetup(__instance,__runOriginal);
            RebirthRecyclerPlacedSettlement.ObserveRecyclerNativeSetup(__instance,__runOriginal);
            RebirthProductiveEarnedUiRemote.ObserveQueueResponseSetup(__instance,__runOriginal);
            RebirthWorkstationCompletionPipeline.ObserveRemoteNativeSetup(__instance,__runOriginal);
            ObserveRemoteCloseEchoSetup(__instance,__runOriginal);
            RebirthProductiveEarnedRemoteHost.ObserveOriginalEchoSetup(__instance,__runOriginal);
            ObserveCachedPhysicalEchoSetup(__instance,__runOriginal);
            ObserveRemoteRecipientCloseSetup(__instance,_te,_eStreamMode,__runOriginal);
            RebirthProductiveEarnedUiQueueOwner.ObserveRemoteNativeSetup(__instance,_te,_eStreamMode,__runOriginal);
            if(!__runOriginal||!ThreadManager.IsMainThread()||_eStreamMode!=StreamModeWrite.ToClient||_te is not TileEntityWorkstation station)return;
            try
            {
                var world=GameManager.Instance?.World;
                if(world?.IsRemote()!=false||!ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station))return;
                ObserveCachedHostSource(world,station);
                if(!host.Values.Any(known=>!known.Closed&&ReferenceEquals(known.World,world)&&ReferenceEquals(known.Station,station))&&
                    !RebirthProductiveEarnedUiQueueOwner.HasNativeHydrationCandidate(world,station)&&
                    !RebirthWorkstationCompletionPipeline.HasNativeReceiptHydrationCandidate(world,station,null,null)&&
                    !RebirthWorkstationCompletionPipeline.HasCachedPrimaryHydrationCandidate(world,station)&&
                    !RebirthRecyclerPlacedSettlement.HasRecyclerReentryCandidate(world,station,null,null)&&
                    !HasActualAuthorityNativeAdd(world,station))return;
                var value=new SelectedNativeSource{Packet=__instance,World=world,Station=station,Handle=__instance.handle,Block=__instance.teBlockId,Position=__instance.teWorldPos,
                    SetupClock=__state?.Clock??0,SetupBeforeTick=__state?.Tick??0,SetupAfterTick=GameTimer.Instance.ticks};
                selectedNativeSources.Add(__instance,value);
                if(__instance.ms==null||__instance.ms.Length<1||__instance.ms.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes)return;
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(decoder==null||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*__instance.ms.Length+65536),out var allocation))return;
                    value.Allocation=allocation;value.Body=new byte[(int)__instance.ms.Length];
                    lock(__instance.ms)
                    {
                        long previous=__instance.ms.Position;
                        try{__instance.ms.Position=0;int copied=0;while(copied<value.Body.Length){int read=__instance.ms.Read(value.Body,copied,value.Body.Length-copied);if(read<1)throw new EndOfStreamException();copied+=read;}}
                        finally{__instance.ms.Position=previous;}
                    }
                    if(__state==null||!ReferenceEquals(__state.Station,station)||station.lastTickTime!=__state.Clock||
                        !RebirthProductiveEarnedNativeBody.TryCaptureCurrent(station,out var current,out int offset)||
                        !RebirthProductiveEarnedNativeBody.SameExceptElapsed(value.Body,offset,current,offset))
                    {value.Body=null;value.Allocation.Dispose();value.Allocation=null;return;}
                    value.ElapsedOffset=offset;
                }
                if(!value.Current()){value.Body=null;value.Allocation?.Dispose();value.Allocation=null;}
            }
            catch{/* The actual setup tombstone grants only a known-source hold; no full-byte permission is inferred. */}
        }
    }
    [HarmonyPatch(typeof(NetPackageManager),nameof(NetPackageManager.FreePackage))]
    private static class NativeSourceFree
    {
        private static bool Prefix(NetPackage _package)
        {
            if(IsHeldAuthorityNativeRequest(_package))return false;
            RebirthProductiveEarnedUiOwnedNativeSend.Forget(_package);
            RebirthRecyclerPlacedSettlement.ForgetRecyclerNativeSetup(_package);
            RebirthProductiveEarnedUiRemote.ForgetQueueResponseSetup(_package);
            RebirthWorkstationCompletionPipeline.ForgetRemoteNativeSetup(_package);
            if(_package is NetPackageTileEntity closePacket)closeEchoSetups.Remove(closePacket);
            if(_package is NetPackageTileEntity cachedPacket)cachedEchoSetups.Remove(cachedPacket);
            RebirthProductiveEarnedUiQueueOwner.ForgetRemoteNativeSetup(_package);
            if(_package is NetPackageTileEntity native&&selectedNativeSources.TryGetValue(native,out var source))
            {selectedNativeSources.Remove(native);source.Dispose();}
            ForgetReceivedConnection(_package);
            return true;
        }
    }
}


