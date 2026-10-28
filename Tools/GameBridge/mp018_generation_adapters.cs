// AUTHORED_NOT_RUN. Existing repository schema/save/file/native identity are adapters.
internal static class RebirthSurvivorMode {internal static bool Enabled=true;internal static bool IsEnabledForCurrentWorld()=>Enabled;}
internal static class RebirthConsumptionOwnerPublication {internal static bool TryCaptureCreation(EntityPlayer player,out string creation){creation=player.Creation;return true;}}
internal static class RebirthHydrationGenerationClient {internal static bool TryGet(EntityPlayerLocal player,out string generation){generation=null;return false;}}
internal static class RebirthNpcMetabolismCustody {internal static bool Held;internal static bool HoldsPlayer(EntityPlayer player)=>Held;}
internal static class RebirthHydrationOwnerHold {internal static bool HoldsMetabolismOwner(EntityPlayerLocal player)=>false;}
internal sealed class NativePlatformId {internal string CombinedString;}
internal sealed partial class GameManager {internal NativePlatformId getPersistentPlayerID(object unused)=>new NativePlatformId{CombinedString="account"};}
public static partial class RebirthMetabolismStateRepository {internal static RebirthMetabolismState GetOrCreate(EntityPlayer player)=>State;}
