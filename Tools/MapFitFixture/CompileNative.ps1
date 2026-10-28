$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path
$game='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die';$managed="$game/7DaysToDie_Data/Managed"
$out=Join-Path $PSScriptRoot 'out';New-Item -ItemType Directory -Force $out|Out-Null
@'
internal static class RebirthHarmonyBootstrap { internal static void PatchClassOnce(HarmonyLib.Harmony h,System.Type t){} }
public static class RebirthCraftingNavigationService { public enum Destination { Map } }
public class XUiC_RebirthScreenChrome:XUiController { protected virtual RebirthCraftingNavigationService.Destination Destination=>default; }
public class XUiC_RebirthPurgePoiPopup:XUiController {public void Show(object o){} }
internal static class RebirthPoiMapPresentation {internal static object Record(NavObject o)=>o;internal static bool Owns(NavObject o)=>o!=null;}
internal static class RebirthSandboxOptionManager {internal static Options Current=new Options();internal sealed class Options {internal bool IsPurge=true;} }
'@|Set-Content "$out/FeatureBoundaries.cs"
$purge=Get-Content "$root/Scripts/Purge/Network/RebirthPoiMapPresentation.cs" -Raw
$start=$purge.IndexOf('[HarmonyPatch(typeof(XUiC_MapArea), "UpdateWaypointSelection")]');$end=$purge.IndexOf('[HarmonyPatch(typeof(XUiC_MapArea), nameof(XUiC_MapArea.Update))]',$start)
('using HarmonyLib;'+$purge.Substring($start,$end-$start))|Set-Content "$out/ActualPurgeHoverHook.cs"
[xml]$project=Get-Content "$root/RebirthUtils.csproj";$refs=@()
foreach($r in $project.Project.ItemGroup.Reference){if($r.HintPath){$path=$r.HintPath.Replace('$(RebirthManagedRoot)',$managed).Replace('$(RebirthGameRoot)',$game);if(Test-Path -LiteralPath $path){$refs+='/reference:"'+$path+'"'}}}
foreach($name in @('System.dll','System.Core.dll')){$refs+='/reference:"'+(Join-Path $managed $name)+'"'}
$compilerArgs=@('/nologo','/target:library','/nostdlib+','/langversion:latest',('/out:"'+(Join-Path $out 'NativePatch.dll')+'"'))+$refs+@(('"'+(Join-Path $root 'Scripts/UI/RebirthMapOverview.cs')+'"'),('"'+(Join-Path $root 'Scripts/UI/XUiC_RebirthMapArea.cs')+'"'),('"'+(Join-Path $root 'Scripts/UI/XUiC_RebirthMapZoomSlider.cs')+'"'),('"'+(Join-Path $root 'Scripts/UI/RebirthMapPresentation.cs')+'"'),('"'+(Join-Path $out 'ActualPurgeHoverHook.cs')+'"'),('"'+(Join-Path $out 'FeatureBoundaries.cs')+'"'))
$rsp=Join-Path $out 'compile.rsp';$compilerArgs|Set-Content $rsp
& 'C:/Program Files/dotnet/dotnet.exe' 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll' ('@'+$rsp) 2>&1|Tee-Object -FilePath (Join-Path $out 'compile.txt')
if($LASTEXITCODE -ne 0){throw "Native patch API compile failed: $LASTEXITCODE"}
'PASS complete actual map controller, overview, slider, presentation and actual Purge hover hook compile against installed native Unity/Harmony API; unrelated chrome/bootstrap and Purge data boundaries stubbed; no shared build or game execution'


