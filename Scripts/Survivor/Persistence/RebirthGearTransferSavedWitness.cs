using System.Xml.Linq;

// Used only on a validated current final-file record. This comparison does not
// load files, authorize the sender, debit inventory or prove an owner receipt.
public static class RebirthGearTransferSavedWitness
{
    public static bool Matches(RebirthWorldSupportState saved, string savedCreation,
        RebirthGearTransferState expected, RebirthGearTransferPhase phase)
    {
        if (saved == null || expected == null || saved.PendingGearTransfer == null ||
            saved.PendingMusicTransfer != null || saved.PendingLibraryTransfer != null ||
            saved.GearTransferPhase != phase ||
            !RebirthGearTransferJournal.Matches(saved, expected, savedCreation)) return false;
        return XNode.DeepEquals(saved.PendingGearTransfer.ToXml(), expected.ToXml());
    }
}