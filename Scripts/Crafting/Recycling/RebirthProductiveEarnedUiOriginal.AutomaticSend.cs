using System;

internal static partial class RebirthProductiveEarnedUiOriginal
{
    internal static bool IsExecutingOwnedSync(XUiC_WorkstationWindowGroup window)=>executingBeforeFlush!=null&&ReferenceEquals(executingBeforeFlush.Binding.Window,window);
    internal static bool TryGetOwnedAutomaticSync(TileEntityWorkstation station,out object source,out byte[] expected,out int offset)
    {
        source=null;expected=null;offset=-1;var value=executingBeforeFlush;
        if(value==null||!value.Entered||value.Closed||value.Binding.World?.IsRemote()!=true||!ReferenceEquals(value.Binding.Station,station))return false;
        source=value;expected=value.After;offset=value.AfterOffset;return true;
    }
    internal static bool IsOwnedAutomaticSync(object source,TileEntityWorkstation station)=>source is BeforeFlush value&&ReferenceEquals(value,executingBeforeFlush)&&
        value.Entered&&!value.Closed&&value.Binding.World.IsRemote()&&ReferenceEquals(value.Binding.Station,station);
    internal static void NoteOwnedAutomaticSync(object source,byte handle,bool valid)
    {if(source is BeforeFlush value&&ReferenceEquals(value,executingBeforeFlush)){value.AutomaticSends++;value.AutomaticHandle=handle;value.AutomaticInvalid|=!valid;}}
    private static bool AutomaticSyncObserved(BeforeFlush value)=>!value.Binding.World.IsRemote()||value.AutomaticSends==1&&!value.AutomaticInvalid;
    internal static bool TryGetOwnedExplicitHandle(BeforeFlush value,out byte handle)
    {handle=byte.MaxValue;if(value?.IsObserved!=true||value.AutomaticSends!=1||value.AutomaticInvalid||value.Binding.Station.lockHandleWaitingFor!=value.AutomaticHandle)return false;handle=value.AutomaticHandle;return true;}
}
