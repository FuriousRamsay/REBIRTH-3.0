#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperLampCode=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperLampIntensity.cs'))
$camperLampCode+=@'
public static class CamperLampFixture{
 static void Check(float value,float expected,string name){if(value!=expected)throw new System.Exception(name);}
 public static void Run(){
 Check(RebirthCamperLampIntensity.Calculate(false,false,0,false),0,"all lamps off");
 Check(RebirthCamperLampIntensity.Calculate(false,false,0,true),.12f,"dim running lamps");
 Check(RebirthCamperLampIntensity.Calculate(false,true,2,false),.65f,"braking bright without headlights");
 Check(RebirthCamperLampIntensity.Calculate(false,false,-.051f,false),.65f,"actual reverse bright without headlights");
 Check(RebirthCamperLampIntensity.Calculate(false,false,-.05f,false),0,"reverse threshold");
 Check(RebirthCamperLampIntensity.Calculate(false,false,2,false),0,"forward input does not imply reverse");
 Check(RebirthCamperLampIntensity.Calculate(true,true,-2,true),0,"broken component off");
 Check(RebirthCamperLampIntensity.Calculate(false,true,float.NaN,true),0,"invalid native velocity off");
 Check(RebirthCamperLampIntensity.Calculate(false,false,float.PositiveInfinity,true),0,"infinite native velocity off");
 Check(RebirthCamperLampIntensity.Marker(true),.08f,"engine marker on"); Check(RebirthCamperLampIntensity.Marker(false),0,"engine marker off");
 Check(RebirthCamperLampIntensity.PickupRed(false,false,-2,false),0,"reverse does not light red brake lenses");
 Check(RebirthCamperLampIntensity.PickupRed(false,true,-2,false),.65f,"braking and reversing retains red brake channel");
 Check(RebirthCamperLampIntensity.PickupRed(false,false,-2,true),.12f,"reverse retains dim red tail channel");
 Check(RebirthCamperLampIntensity.PickupReverse(false,-2),.65f,"reverse uses clear channel");
 Check(RebirthCamperLampIntensity.PickupReverse(false,-.05f),0,"clear reverse threshold");
 Check(RebirthCamperLampIntensity.PickupReverse(true,-2),0,"broken clear channel off");
 Check(RebirthCamperLampIntensity.PickupReverse(false,float.NaN),0,"invalid clear channel off");
 int red=0,white=0;
 foreach(string prefix in new[]{"Rear lamp lens segment","Fine lens flute"}){
 int max=prefix=="Fine lens flute"?23:5;
 for(int n=0;n<=max;n++){
 bool reverse;string name=n==0?prefix:prefix+"."+n.ToString("000");
 if(!RebirthCamperLampIntensity.PickupLens(name,out reverse))throw new System.Exception("Missing approved lens "+name);
 if(reverse)white++;else red++;
 }}
 if(red!=20||white!=10||RebirthCamperLampIntensity.PickupLens("Tailgate folded outer shell",out _))throw new System.Exception("Approved lens classification");
 System.Console.WriteLine("PASS actual native rear-lamp state mapping. Unity emitter/lens appearance remains to be verified; game not launched.");}}
'@
Add-Type -TypeDefinition $camperLampCode
[CamperLampFixture]::Run()


