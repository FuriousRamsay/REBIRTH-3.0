// Default-closed switches behind RebirthNpcStorageOrganizationWork.Sort/Merge/PartialCustodyReady. They exist only so the
// single deferred profiler batch can open a gate for its own run (a bridge/console hook sets them); nothing in the product
// sets them, they are never persisted, and every field is false at load, so Parse/AssignSort refuse the modes by default.
internal static class RebirthNpcB09GateOverride
{
    internal static bool SortOpen, MergeOpen, PartialOpen;
    internal static string Report()=>"[REBIRTH NPC B09 gates] sort="+SortOpen+" merge="+MergeOpen+" partial="+PartialOpen;
}
