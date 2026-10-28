// AUTHORED_NOT_RUN. Native rejection packet/send envelope adapter.
internal sealed class NetPackageRebirthHydrationPreparedRejection {internal NetPackageRebirthHydrationPreparedRejection Setup(RebirthHydrationTransferJournal.SavedRejection rejection){if(!rejection.Current())throw new InvalidOperationException("missing saved rejection");return this;}}
internal sealed partial class ConnectionManager {internal void SendPackage(NetPackageRebirthHydrationPreparedRejection packet,int _attachedToEntityId){Sends++;DuringSend?.Invoke();}}
