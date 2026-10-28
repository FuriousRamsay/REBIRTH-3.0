// AUTHORED_NOT_RUN. Native package mapping and player upload scheduling adapters.
internal sealed class NetPackagePlayerData {}
internal static class NetPackageManager {internal static bool Available=true;internal static Action DuringMapping;internal static int GetPackageId(Type type){DuringMapping?.Invoke();if(!Available)throw new InvalidOperationException("native mapping unavailable");return 1;}}
internal sealed partial class GameManager {internal static int Uploads;internal static Action DuringUpload;internal void TriggerSendOfLocalPlayerDataFile(float delay){Uploads++;DuringUpload?.Invoke();}}
