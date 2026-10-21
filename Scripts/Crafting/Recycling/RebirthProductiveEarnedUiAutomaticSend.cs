using System;
using System.Linq;

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    internal static bool CanSyncOwnedOriginal(XUiC_WorkstationWindowGroup group)
    {
        if(group==null||!originals.Values.Any(value=>ReferenceEquals(value.Binding.Window,group)&&!value.TerminalSaved))return true;
        return RebirthProductiveEarnedUiOriginal.IsExecutingOwnedSync(group)||terminalSync!=null&&ReferenceEquals(terminalSync.Binding.Window,group)||
            handoffSync!=null&&ReferenceEquals(handoffSync.Binding.Window,group);
    }
    internal static bool TryGetOwnedAutomaticSync(TileEntityWorkstation station,out object source,out byte[] expected,out int offset)
    {
        source=null;expected=null;offset=-1;
        if(terminalSync is Original original&&original.Binding.World.IsRemote()&&ReferenceEquals(original.Binding.Station,station)&&original.TerminalSyncAttempted)
        {source=original;expected=original.TerminalBody;offset=original.TerminalOffset;return true;}
        if(handoffSync is QueueHandoff handoff&&handoff.Binding.World.IsRemote()&&ReferenceEquals(handoff.Binding.Station,station)&&handoff.Entered)
        {source=handoff;expected=handoff.After;offset=handoff.AfterOffset;return true;}
        return false;
    }
    internal static bool IsOwnedAutomaticSync(object source,TileEntityWorkstation station)=>
        source is Original original&&ReferenceEquals(terminalSync,original)&&original.Binding.World.IsRemote()&&ReferenceEquals(original.Binding.Station,station)||
        source is QueueHandoff handoff&&ReferenceEquals(handoffSync,handoff)&&handoff.Entered&&handoff.Binding.World.IsRemote()&&ReferenceEquals(handoff.Binding.Station,station);
    internal static void NoteOwnedAutomaticSync(object source,byte handle,bool valid)
    {
        if(source is Original original&&ReferenceEquals(terminalSync,original)){original.TerminalAutomaticSends++;original.TerminalAutomaticHandle=handle;original.TerminalAutomaticInvalid|=!valid;}
        if(source is QueueHandoff handoff&&ReferenceEquals(handoffSync,handoff)){handoff.AutomaticSends++;handoff.AutomaticHandle=handle;handoff.AutomaticInvalid|=!valid;}
    }
    private static bool TryGetTerminalExplicitHandle(Original original,out byte handle)
    {handle=original.TerminalAutomaticHandle;return original.TerminalSynced&&original.TerminalAutomaticSends==1&&!original.TerminalAutomaticInvalid&&original.Binding.Station.lockHandleWaitingFor==handle;}
    private static bool TryGetHandoffExplicitHandle(QueueHandoff handoff,out byte handle)
    {handle=handoff.AutomaticHandle;return handoff.Observed&&handoff.AutomaticSends==1&&!handoff.AutomaticInvalid&&handoff.Binding.Station.lockHandleWaitingFor==handle;}
}
