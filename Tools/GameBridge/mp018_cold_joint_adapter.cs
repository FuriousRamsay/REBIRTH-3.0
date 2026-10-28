// AUTHORED_NOT_RUN. Native types/schema/writer and journal path/group remain
// adapters. Actual cold source qualification, owner proof and closure compose.
internal sealed class ItemValue {internal string Data;}
internal sealed class RebirthMetabolismState
{
    internal string SurvivorCreationId;internal int Revision;
    internal ItemStack HydrationSlotItem=new ItemStack();
    internal RebirthHydrationTransferOriginal HydrationTransferReceipt;
}
internal static class RebirthNativeItemCodec {internal static string Encode(ItemValue value)=>value.Data;}
public static partial class RebirthMetabolismStateRepository
{
    private static Dictionary<string,RebirthMetabolismState> ReadCandidate(string path)=>DecodeStore(XDocument.Load(path));
    internal static bool PublishColdFixtureSource()=>TrySave("fixture-seed-committed-source",out _);
}
internal static partial class RebirthHydrationTransferJournal
{
    internal static bool HasApplied(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {var journal=Open(original,false);var record=journal?.Read(original.Operation);return Matches(record,original)&&record.State==RebirthCamperTransferJournal.Phase.Applied;}
}
