public static partial class RebirthNpcEconomyLogisticsService
{
    internal static bool SavedAdoptionEmpty()
    {lock(Sync)return Endpoints.Count==0&&Checkpoints.Count==0&&Replay.Count==0&&ReplayOrder.Count==0;}
}
public static partial class RebirthNpcCombatEmergencyService
{
    internal static bool SavedAdoptionEmpty()
    {lock(Sync)return States.Count==0&&Orders.Count==0&&FinalizedDeathTransactions.Count==0;}
}
public static partial class RebirthNpcSettlementSimulation
{
    internal static bool SavedAdoptionEmpty()
    {
        lock(Sync)return Needs.Count==0&&Professions.Count==0&&Work.Count==0&&Resources.Count==0&&ResourceReservations.Count==0&&
            AvailableByPriority.Count==0&&AvailableQueued.Count==0&&ClaimedWorkByNpc.Count==0&&BusyNpcs.Count==0&&
            ClaimExpiryByDue.Count==0&&TerminalWorkOrderHistory.Count==0;
    }
}
