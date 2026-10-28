$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../..").Path
$game='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die';$managed="$game/7DaysToDie_Data/Managed"
$out=Join-Path $PSScriptRoot 'out';New-Item -ItemType Directory -Force $out|Out-Null
@'
internal static class RebirthCamperRadial { internal static bool IsCamper(EntityVehicle value)=>false; }
internal static class RebirthCamperBodyAttachment { internal static bool HasBody(EntityVehicle value)=>false; }
internal enum RebirthCamperEnergyMode { Travel, Camp }
internal static class RebirthCamperEnergyRuntime { internal static bool TryObservePresentation(EntityVehicle v,out RebirthCamperEnergyMode m,out long r,out float x,out float y){m=default;r=0;x=0;y=0;return false;} }
'@ | Set-Content "$out/FeatureBoundaries.cs"
[xml]$project=Get-Content "$root/RebirthUtils.csproj";$refs=@()
foreach($r in $project.Project.ItemGroup.Reference){if($r.HintPath){$path=$r.HintPath.Replace('$(RebirthManagedRoot)',$managed).Replace('$(RebirthGameRoot)',$game);if(Test-Path -LiteralPath $path){$refs+='/reference:"'+$path+'"'}}}
foreach($name in @('System.dll','System.Core.dll')){$refs+='/reference:"'+(Join-Path $managed $name)+'"'}
$argsList=@('/nologo','/target:library','/nostdlib+','/langversion:latest',('/out:"'+(Join-Path $out 'NativeCollision.dll')+'"'))+$refs+@(('"'+(Join-Path $root 'Scripts/Vehicles/Camper/RebirthCamperOwnCollision.cs')+'"'),('"'+(Join-Path $out 'FeatureBoundaries.cs')+'"'))
$argsList|Set-Content "$out/compile.rsp"
& 'C:/Program Files/dotnet/dotnet.exe' 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll' ('@'+(Join-Path $out 'compile.rsp')) 2>&1|Tee-Object -FilePath "$out/compile.txt"
if($LASTEXITCODE-ne0){throw 'Native ABI compilation failed'}
'PASS complete actual collision source compiled against installed native Unity/Harmony API; custom feature boundary stubs only; no game execution/shared build'
