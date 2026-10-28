// AUTHORED_NOT_RUN. Metabolism row/WAL phase/native type are adapters;
// actual private recovery custody/native owner proof/hold are embedded.
internal sealed class ItemValue {internal string Data;}
internal sealed class RebirthMetabolismState
{
    internal string SurvivorCreationId;internal ItemStack HydrationSlotItem=new ItemStack();
    internal RebirthHydrationTransferOriginal HydrationTransferReceipt;
}
internal static class RebirthNativeItemCodec {internal static string Encode(ItemValue item)=>item.Data;}
internal static class RebirthMetabolismStateRepository
{
    internal static RebirthMetabolismState State;
    internal static bool TryGet(EntityPlayerLocal player,out RebirthMetabolismState state){state=State;return state!=null;}
}
internal static class RebirthHydrationTransferJournal
{
    internal static bool Attempted=true;
    internal static bool HasAttempted(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>Attempted;
}
