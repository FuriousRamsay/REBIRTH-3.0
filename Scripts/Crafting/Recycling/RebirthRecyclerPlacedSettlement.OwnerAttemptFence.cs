using System;
using System.IO;
using System.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // A native loaded checkpoint grants its own proved prefix, never proof
    // that an entered but unsaved next setter did not run. This is a refusal
    // fence only; missing files do not issue any recovery capability.
    private static bool HasUnresolvedOwnerSlotAttempt(Original original,int completed)
    {
        if(original==null||completed<0)return true;
        if(completed>=original.Slots.Length)return false;
        try
        {
            string root=RebirthWorkstationCompletionOutbox.Root("recyclerPlaced");
            return Directory.EnumerateFiles(root,original.Operation.ToString("N")+"*.slot."+completed+".attempted.xml*").Any();
        }
        catch{return true;}
    }
}
