using System;using UnityEngine;
public static class RebirthVehicleAssemblyIndex {public static bool TryGetEntity(EntityVehicle e,out RebirthVehicleAssembly a){a=null;return false;}public static void RegisterEntity(EntityVehicle e,RebirthVehicleAssembly a){}public static void CommitEntity(EntityVehicle e,RebirthVehicleAssembly a){}}
public static class RebirthVehicleEntityPersistence {public static bool TryReadAssemblyFromVehicleItem(EntityVehicle e,out RebirthVehicleAssembly a){a=null;return false;}public static bool WriteAssemblyToVehicleItem(EntityVehicle e,RebirthVehicleAssembly a)=>true;}
public static class RebirthVehicleInventoryTransactions {public static ItemStack CreateItemStack(RebirthInstalledVehiclePart p)=>null;public static RebirthInstalledVehiclePart FromItemStack(ItemStack s,object q)=>null;}
public static class RebirthNativeItemCodec {public static string Encode(ItemValue v)=>null;}
public static class RebirthCamperRadial {public static bool IsCamper(EntityVehicle e)=>true;}
public static class RebirthCamperModuleInteraction {public static bool Current(EntityVehicle e,EntityPlayerLocal p)=>false;}
public static class RebirthGameBridge {public static bool IsRunning=>true;}
public static class RebirthTraitGameplayModifierService {public static float GetVehicleFuelUseMultiplier(EntityVehicle e)=>1;}
