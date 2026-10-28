#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'RuntimeDiagnostics/CamperTrackFixture.cs'))
$start=$source.IndexOf('        internal static bool FootprintsIntersect')
$end=$source.IndexOf('        public override void Execute', $start)
if($start -lt 0 -or $end -le $start){throw 'Actual pad admission methods missing.'}
$methods=$source.Substring($start,$end-$start)
$fixture=@'
using System;
public struct Vector3i{public int x,y,z;public Vector3i(int x,int y,int z){this.x=x;this.y=y;this.z=z;}}
public static class PadAdmissionFixture{
static void Check(bool value,string message){if(!value)throw new Exception(message);}
public static void Run(){
 for(int i=1;i<=8;i++)Check(IsVerifiedEmber("emberPileDeco"+i,true,false),"exact native decoration admitted");
 foreach(var name in new[]{"emberPileDeco9","emberPileLightPOI1","emberPileDeco1Clone","wreckedCar","terrPlayerBuilt"})Check(!IsVerifiedEmber(name,true,false),"unknown name refused");
 Check(!IsVerifiedEmber("emberPileDeco8",false,false)&&!IsVerifiedEmber("emberPileDeco8",true,true),"runtime decoration and absence of TE required");
 foreach(var name in new[]{"rock01","rock02","oreLeadBoulder","plantShrubDead","mushroomBiome4","mushroomRadiated02","mushroomRadiated04","cinderBlocks01","cinderBlocks03","scrapMetalPile","driftwood2","hubcapNoMine","burntSedan01","abandonedPickupTruck01Purple"}){
   Check(IsVerifiedFieldClutter(name,true,false),"exact inspected field clutter admitted");
   Check(!IsVerifiedFieldClutter(name,false,false)&&!IsVerifiedFieldClutter(name,true,true),"native runtime flag and all TE preservation");
 }
 Check(IsVerifiedFieldClutter("rubblePileBricksSmall",false,false)&&!IsVerifiedFieldClutter("rubblePileBricksSmall",true,true),"exact nonTE native rubble exception");
 foreach(var name in new[]{"rock03","oreIronBoulder","plantedMushroom3Harvest","cntMinivan01Grey","FuriousRamsayBirdNest","FuriousRamsayBirdNestEggs","FuriousRamsayBirdNestFeathers","burntSedan01Clone","playerSafe","rubblePileBricks"})Check(!IsVerifiedFieldClutter(name,true,false),"nonallowlisted field object refused");
 Check(FootprintsIntersect(0,0,40,80,new Vector3i(-5,200,0),new Vector3i(2,1,1)),"4m margin and full prefab footprint regardless height");
 Check(!FootprintsIntersect(0,0,40,80,new Vector3i(-5,0,0),new Vector3i(1,1,1)),"edge outside margin");
 Check(FootprintsIntersect(0,0,40,80,new Vector3i(-10,0,-10),new Vector3i(100,2,100)),"enclosing POI refused");
 bool rejected=false;try{FootprintsIntersect(0,0,40,80,new Vector3i(0,0,0),new Vector3i(0,1,1));}catch{rejected=true;}Check(rejected,"invalid native bounds fail closed");
 Console.WriteLine("PASS actual pad admission methods: exact native ember/clutter whitelist and runtime flags, every TE preserved, unknown/player crop/container refusal, full prefab and4m margin, invalid bounds. No gameplay proof.");
}
'@
Add-Type -TypeDefinition ($fixture + $methods + '}')
[PadAdmissionFixture]::Run()
