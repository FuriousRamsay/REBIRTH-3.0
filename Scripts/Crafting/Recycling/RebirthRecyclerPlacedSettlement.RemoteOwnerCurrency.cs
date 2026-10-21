internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed partial class RemoteHostOriginal
    {
        // No station/item mutation permission. A fully saved terminal may
        // outlive later legitimate native queue work while the last genuine
        // owner autosave crosses its independently fenced receive/file head.
        internal bool AutomaticOwnerCurrent()=>Current()||FinancialReleased&&OwnerCurrent()&&NativeLineageCurrent()&&Access?.Ready()==true&&
            Terminal!=null&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(TerminalPath,Terminal);
    }
}
