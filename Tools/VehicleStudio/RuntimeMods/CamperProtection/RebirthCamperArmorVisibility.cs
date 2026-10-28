using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
// Derived from native health; no new save state, network messages, or Update polling.
internal static class RebirthCamperArmorVisibility
{
 // Armor adds two thirds of the armored maximum (8k chassis +16k armor).
 // Authored stages end at5%; map that final stage to the chassis reserve,
 // preserving their balanced plate/grille order and exact repair reversal.
 internal static bool Visible(int health,int maximum,int threshold)=>maximum>0&&
  ((long)health*3-maximum)*95>(long)maximum*2*Math.Max(0,threshold-5);
 internal sealed class Range {internal int Threshold,Start,Count;}
 internal sealed class Surface {internal Mesh Mesh;internal int[] Original;internal Range[] Ranges;internal ulong Mask=ulong.MaxValue;}
 internal sealed class Binding {public Binding(){} internal Transform Root;internal Surface[] Surfaces;}
 static readonly ConditionalWeakTable<EntityVehicle,Binding> Bindings=new ConditionalWeakTable<EntityVehicle,Binding>();
 internal static void Release(EntityVehicle entity)
 {
  if(!Bindings.TryGetValue(entity,out var binding))return;
  if(binding.Surfaces!=null)foreach(var surface in binding.Surfaces)if(surface.Mesh)UnityEngine.Object.Destroy(surface.Mesh);
  Bindings.Remove(entity);
 }
 internal static void Apply(Vehicle vehicle)
 {
  if(!RebirthCamperModProtection.IsCamper(vehicle)||!vehicle.entity)return;
  var entity=vehicle.entity;var root=entity.ModelTransform;if(!root)return;
  var binding=Bindings.GetOrCreateValue(entity);
  if(binding.Root!=root||binding.Surfaces==null)
  {
   if(binding.Surfaces!=null)foreach(var surface in binding.Surfaces)if(surface.Mesh)UnityEngine.Object.Destroy(surface.Mesh);
   var surfaces=new List<Surface>();
   foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
   {
    if(!filter.name.StartsWith("REBIRTHArmorSurface_",StringComparison.Ordinal))continue;
    var ranges=new List<Range>();
    foreach(Transform child in filter.transform)
    {
     var fields=child.name.Split('_');
     if(fields.Length==5&&fields[0]=="ArmorRange"&&int.TryParse(fields[2],out int threshold)&&int.TryParse(fields[3],out int start)&&int.TryParse(fields[4],out int count))
      ranges.Add(new Range{Threshold=threshold,Start=start,Count=count});
    }
    if(ranges.Count==0)continue;
    var mesh=filter.mesh;
    surfaces.Add(new Surface{Mesh=mesh,Original=mesh.triangles,Ranges=ranges.ToArray()});
   }
   if(surfaces.Count==0)return;
   binding.Root=root;binding.Surfaces=surfaces.ToArray();
  }
  int health=entity.Health,maximum=(int)entity.Stats.Health.BaseMax;
  foreach(var surface in binding.Surfaces)
  {
   ulong mask=0;int count=0;
   for(int i=0;i<surface.Ranges.Length;i++)if(Visible(health,maximum,surface.Ranges[i].Threshold)){mask|=1UL<<i;count+=surface.Ranges[i].Count;}
   if(mask==surface.Mask)continue;
   var indices=new int[count];int offset=0;
   for(int i=0;i<surface.Ranges.Length;i++)if((mask&(1UL<<i))!=0){var range=surface.Ranges[i];Array.Copy(surface.Original,range.Start,indices,offset,range.Count);offset+=range.Count;}
   surface.Mesh.SetTriangles(indices,0,false);surface.Mask=mask;
  }
 }
}
[HarmonyPatch(typeof(Vehicle),nameof(Vehicle.FireEvent),new Type[]{typeof(Vehicle.Event)})]
internal static class RebirthCamperArmorHealthVisuals
{
 static void Postfix(Vehicle __instance,Vehicle.Event _event){if(_event==Vehicle.Event.HealthChanged)RebirthCamperArmorVisibility.Apply(__instance);}
}
[HarmonyPatch(typeof(Vehicle),nameof(Vehicle.CalcMods))]
internal static class RebirthCamperArmorModVisuals
{
 [HarmonyPriority(Priority.Last)] static void Postfix(Vehicle __instance)=>RebirthCamperArmorVisibility.Apply(__instance);
}
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.PostInit))]
internal static class RebirthCamperArmorLoadVisuals
{
 static void Postfix(EntityVehicle __instance)=>RebirthCamperArmorVisibility.Apply(__instance.vehicle);
}
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.ReadSyncData))]
internal static class RebirthCamperArmorSyncVisuals
{
 static void Postfix(EntityVehicle __instance)=>RebirthCamperArmorVisibility.Apply(__instance.vehicle);
}
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.OnEntityUnload))]
internal static class RebirthCamperArmorUnloadVisuals
{
 static void Prefix(EntityVehicle __instance)=>RebirthCamperArmorVisibility.Release(__instance);
}

