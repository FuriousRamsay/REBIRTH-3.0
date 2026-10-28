// AUTHORED_NOT_RUN. Native mapping/send and packet envelope are adapters here.
internal sealed class NetPackagePlayerData {}
internal sealed partial class GameManager {internal int NativePdsRequests;internal void TriggerSendOfLocalPlayerDataFile(float delay){NativePdsRequests++;}}
internal sealed class NetPackageRebirthHydrationAcknowledgment {internal NetPackageRebirthHydrationAcknowledgment Setup(EntityPlayerLocal player,Guid id,string digest){if(!RebirthHydrationRemoteClientTerminal.TryGetAcknowledgment(player,id,digest,out _))throw new InvalidOperationException("missing terminal");return this;}}
internal static class NetPackageManager {internal static bool Available=true;internal static Action DuringMapping;internal static int GetPackageId(Type type){DuringMapping?.Invoke();if(!Available)throw new InvalidOperationException("unmapped acknowledgment");return 1;}internal static T GetPackage<T>() where T:new()=>new T();}
internal sealed partial class ConnectionManager {internal static int Sends;internal static Action DuringSend;internal void SendToServer(NetPackageRebirthHydrationAcknowledgment packet){Sends++;DuringSend?.Invoke();}}
