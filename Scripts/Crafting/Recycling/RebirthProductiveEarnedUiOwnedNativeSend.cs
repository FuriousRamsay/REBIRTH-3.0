using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

// The native sync calls SetModified before ResetTickTime. Its automatic packet
// must not escape a staged purpose before that purpose's authenticated header.
// Only this actual synchronous invocation is suppressed; it is never replayed.
internal static class RebirthProductiveEarnedUiOwnedNativeSend
{
    private sealed class Intent
    {
        internal object Source;internal bool Before;internal TileEntityWorkstation Station;
        internal RebirthProductiveEarnedUiQueueOwner.ActualPendingAdd Add;
        internal byte[] Expected;internal int Offset;internal ulong Clock,First,Last;internal bool SetupRan,Valid;
    }
    private static readonly ConditionalWeakTable<NetPackageTileEntity,Intent> originals=new ConditionalWeakTable<NetPackageTileEntity,Intent>();
    internal static void BeginSetup(NetPackageTileEntity packet,TileEntity tile,StreamModeWrite mode)
    {
        Forget(packet);if(!ThreadManager.IsMainThread()||mode!=StreamModeWrite.ToServer||tile is not TileEntityWorkstation station)return;
        bool before=RebirthProductiveEarnedUiOriginal.TryGetOwnedAutomaticSync(station,out var source,out var expected,out int offset);
        if(!before&&!RebirthProductiveEarnedUiCoordinator.TryGetOwnedAutomaticSync(station,out source,out expected,out offset))
        {
            // AddCraftComplete calls SetModified inside the original, before
            // the positive receipt postfix exists. This is custody only:
            // suppress this exact UIO call's automatic packet, never mint a
            // successful contribution or resend its body from this context.
            if(!RebirthProductiveEarnedUiQueueOwner.TryGetActualPendingAdd(station,out var add)||add.UiOriginal==null||!add.World.IsRemote())return;
            originals.Add(packet,new Intent{Add=add,Station=station,Clock=station.lastTickTime,First=GameTimer.Instance.ticks});return;
        }
        originals.Add(packet,new Intent{Source=source,Before=before,Station=station,Expected=expected,Offset=offset,Clock=station.lastTickTime,First=GameTimer.Instance.ticks});
    }
    internal static void ObserveSetup(NetPackageTileEntity packet,bool run)
    {
        if(!originals.TryGetValue(packet,out var value))return;
        value.SetupRan=run;value.Last=GameTimer.Instance.ticks;
        try
        {
            if(!run||!Executing(value)||value.Expected==null||value.Offset<0||value.Offset>value.Expected.Length-8||
                packet.ms==null||packet.ms.Length!=value.Expected.Length||packet.handle!=value.Station.lockHandleWaitingFor||
                !packet.teWorldPos.Equals(value.Station.ToWorldPos())||packet.teBlockId!=value.Station.blockValue.type||value.Station.lastTickTime!=value.Clock||
                value.First<value.Clock||value.Last<value.First)return;
            ulong elapsed=0;
            lock(packet.ms)
            {
                long prior=packet.ms.Position;
                try
                {
                    packet.ms.Position=0;
                    for(int i=0;i<value.Expected.Length;i++)
                    {
                        int next=packet.ms.ReadByte();if(next<0)return;
                        if(i>=value.Offset&&i<value.Offset+8)elapsed|=(ulong)(byte)next<<(8*(i-value.Offset));
                        else if(next!=value.Expected[i])return;
                    }
                }
                finally{packet.ms.Position=prior;}
            }
            value.Valid=elapsed>=value.First-value.Clock&&elapsed<=value.Last-value.Clock;
        }
        catch{value.Valid=false;}
    }
    private static bool Executing(Intent value)=>value.Add!=null?value.Add.Current()&&ReferenceEquals(value.Add.Station,value.Station):value.Before?RebirthProductiveEarnedUiOriginal.IsOwnedAutomaticSync(value.Source,value.Station):
        RebirthProductiveEarnedUiCoordinator.IsOwnedAutomaticSync(value.Source,value.Station);
    internal static void Forget(NetPackage packet){if(packet is NetPackageTileEntity tile)originals.Remove(tile);}
    internal static bool TrySuppress(NetPackage packet,bool run)
    {
        if(!run||packet is not NetPackageTileEntity native||!originals.TryGetValue(native,out var value)||!Executing(value))return false;
        byte handle=native.handle;bool valid=value.Valid&&value.SetupRan&&native.inSendQueuesCount==0&&handle!=byte.MaxValue&&handle==value.Station.lockHandleWaitingFor;
        // No queue reference was registered: this exact pooled packet remains
        // ours to free. Preserve native handleCounter/lockHandleWaitingFor so
        // the later freshly-created purpose upload receives its real echo.
        originals.Remove(native);
        if(value.Add==null)
        {
            if(value.Before)RebirthProductiveEarnedUiOriginal.NoteOwnedAutomaticSync(value.Source,handle,valid);
            else RebirthProductiveEarnedUiCoordinator.NoteOwnedAutomaticSync(value.Source,handle,valid);
        }
        if(native.inSendQueuesCount==0)NetPackageManager.FreePackage(native);
        return true;
    }
    [HarmonyPatch(typeof(ConnectionManager),nameof(ConnectionManager.SendToServer),new[]{typeof(NetPackage),typeof(bool)})]
    internal static class ClientSend
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(NetPackage _package,bool _flush,bool __runOriginal)=>!TrySuppress(_package,__runOriginal)&&
            !RebirthRecyclerPlacedSettlement.TrySuppressRecyclerNativeSend(_package,__runOriginal)&&
            !RebirthWorkstationCompletionPipeline.TryHoldPendingNativeAdd(_package,_flush,__runOriginal);
    }
}

