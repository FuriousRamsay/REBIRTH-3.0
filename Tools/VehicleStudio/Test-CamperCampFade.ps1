$ErrorActionPreference='Stop'
$source=Get-Content (Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperCampStationAppearance.cs') -Raw
$method=[regex]::Match($source,'internal static float Visibility\([\s\S]*?\n    \}').Value
if(!$method){throw 'Actual visibility method not found'}
$fixture=@'
using System;
public enum RebirthCamperEnergyMode { Travel, Deploying, Camp, Packing }
public static class Mathf {
 public static float Max(float a,float b)=>Math.Max(a,b);
 public static float Clamp01(float v)=>Math.Max(0,Math.Min(1,v));
}
public static class FadeFixture {
METHOD
 public static void Run() {
  foreach(float duration in new[]{1f,20f,120f}) {
   float previous=-1;
   for(int i=0;i<=100;i++) {
    float remaining=duration*(1-i/100f);
    float fade=Visibility(RebirthCamperEnergyMode.Deploying,remaining,duration,duration);
    if(fade<previous||Math.Abs(fade-i/100f)>.00001f)throw new Exception("Deployment not monotonic/timer proportional");
    if(Math.Abs(Visibility(RebirthCamperEnergyMode.Packing,remaining,duration,duration)-(1-fade))>.00001f)throw new Exception("Packing not reversed");
    previous=fade;
   }
  }
  if(Visibility(RebirthCamperEnergyMode.Travel,0,20,20)!=0||Visibility(RebirthCamperEnergyMode.Camp,20,20,20)!=1)throw new Exception("Endpoint failure");
  if(Visibility(RebirthCamperEnergyMode.Deploying,-10,0,0)!=1||Visibility(RebirthCamperEnergyMode.Packing,100,0,0)!=1)throw new Exception("Clamp failure");
 }
}
'@
Add-Type ($fixture.Replace('METHOD',$method))
[FadeFixture]::Run()
'PASS actual source visibility function: 303 timer samples, reverse packing, endpoints, clamping. Unity renderer, multiplayer and gameplay not exercised by this fixture.'
