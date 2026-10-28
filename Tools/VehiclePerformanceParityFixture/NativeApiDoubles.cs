public class RebirthVehiclePerformanceSnapshot {public bool CanOperate;public float AccelerationMultiplier=1,TorqueMultiplier=1,VelocityMultiplier=1,ReverseVelocityMultiplier=1,TurboVelocityMultiplier=1,BrakingMultiplier=1,HandlingMultiplier=1,HillClimbMultiplier=1,ResponseMultiplier=1,FuelUseMultiplier=1,TurboFuelUseMultiplier=1;}
public class RebirthVehicleAssembly {public RebirthVehiclePerformanceSnapshot Performance;}
public static class RebirthVehicleAssemblyIndex {public static bool TryGetEntity(EntityVehicle e,out RebirthVehicleAssembly a){a=null;return false;}}
public static class RebirthTraitGameplayModifierService {public static float GetVehicleFuelUseMultiplier(EntityVehicle e)=>1;}
