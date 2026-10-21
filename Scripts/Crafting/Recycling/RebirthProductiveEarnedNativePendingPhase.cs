using System;

internal static partial class RebirthProductiveEarnedCoordinator
{
    private static bool ContinueSavedNativePending(Attempt original)
    {
        var terminal=original?.Terminal;
        if(terminal==null||!terminal.PendingNextWork||terminal.SavedOrigin==null&&!terminal.SavedPending)return false;
        try
        {
            if(!IsNativeSettlementInvocation(original)||!terminal.Current())return false;
            if(terminal.Allocation==null)
            {
                terminal.Allocation=terminal.Memory.TryObserveNative(terminal.Body,terminal.ElapsedOffset);
                if(terminal.Allocation==null||!terminal.Current())return false;
            }
            return ContinueSavedNativeSuccessor(original);
        }
        finally
        {
            // The durable pending predecessor keeps its original body, debt,
            // source, account and attempted successor custody. Holding its old
            // observation phase while waiting would exclude native owner read
            // and other stations. A successor owns its own phase separately.
            if(ReferenceEquals(original.Terminal,terminal))ParkSavedPendingTerminal(terminal);
        }
    }
    private static void ParkSavedPendingTerminal(NativeTerminal terminal)
    {
        if(terminal==null||!terminal.PendingNextWork||terminal.SavedOrigin==null&&!terminal.SavedPending)return;
        var watch=terminal.Watch;terminal.Watch=null;var allocation=terminal.Allocation;terminal.Allocation=null;
        try{watch?.Dispose();}finally{allocation?.Dispose();}
    }
}
