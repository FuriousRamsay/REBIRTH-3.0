using HarmonyLib;
// Feature assembly entry point; no changes to the currently installed cumulative DLL.
public sealed class RebirthCamperProtectionModApi : IModApi
{
 public void InitMod(Mod mod)
 {
  var harmony=new Harmony("rebirth.camper.modprotection");
  foreach(var type in new[]{typeof(RebirthCamperArmorDurability),typeof(RebirthCamperArmorCapacity),typeof(RebirthCamperZombieDamageScope),typeof(RebirthCamperZombieDamageReduction),typeof(RebirthCamperZombiePloughImpact),typeof(RebirthCamperArmorHealthVisuals),typeof(RebirthCamperArmorModVisuals),typeof(RebirthCamperArmorLoadVisuals),typeof(RebirthCamperArmorSyncVisuals),typeof(RebirthCamperArmorUnloadVisuals)})
   harmony.CreateClassProcessor(type).Patch();
  UnityEngine.Debug.Log("[REBIRTH Camper] Native armor capacity, progressive damage visuals and zombie/plough protection installed.");
 }
}



