$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path
$game='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die';$managed="$game/7DaysToDie_Data/Managed";$prod="$game/Mods/zzz_REBIRTH__3_0"
$out="$PSScriptRoot/out";New-Item -ItemType Directory -Force $out|Out-Null
# Reference-only snapshot. Production assembly is not loaded/executed or changed.
Add-Type -Path "$game/Mods/0_TFP_Harmony/Mono.Cecil.dll";$asm=[Mono.Cecil.AssemblyDefinition]::ReadAssembly("$prod/RebirthUtils.dll")
function Expose($type){
 if($type.DeclaringType){if($type.IsNestedAssembly-or$type.IsNestedFamilyOrAssembly){$type.IsNestedPublic=$true}}elseif($type.IsNotPublic){$type.IsPublic=$true}
 foreach($f in $type.Fields){if($f.IsAssembly-or$f.IsFamilyOrAssembly){$f.Attributes=($f.Attributes-band(-bnot[Mono.Cecil.FieldAttributes]::FieldAccessMask))-bor[Mono.Cecil.FieldAttributes]::Public}}
 foreach($m in $type.Methods){if($m.IsAssembly-or$m.IsFamilyOrAssembly){$m.Attributes=($m.Attributes-band(-bnot[Mono.Cecil.MethodAttributes]::MemberAccessMask))-bor[Mono.Cecil.MethodAttributes]::Public}}
 foreach($nested in $type.NestedTypes){Expose $nested}
}
try{foreach($type in $asm.MainModule.Types){if($type.Name-ne'<Module>'){Expose $type}};$asm.Write("$out/RebirthUtils.Api.dll")}finally{$asm.Dispose()}
[xml]$project=Get-Content "$prod/RebirthUtils.csproj";$refs=@()
foreach($r in $project.Project.ItemGroup.Reference){if($r.HintPath){$path=$r.HintPath.Replace('$(RebirthManagedRoot)',$managed).Replace('$(RebirthGameRoot)',$game);if(Test-Path -LiteralPath $path){$refs+='/reference:"'+$path+'"'}}}
foreach($name in @('System.dll','System.Core.dll')){$refs+='/reference:"'+(Join-Path $managed $name)+'"'}
$refs+='/reference:"'+$out+'/RebirthUtils.Api.dll"'
# The unchanged enum already exists in the metadata reference. Reuse its identity
# for cross-file calls; compile the entire changed file apart from that enum.
$compileSource=Get-Content "$root/Scripts/QuickStack/QuickStackCore.cs" -Raw
$compileSource=[regex]::Replace($compileSource,'(?s)public enum QuickStackCategory\s*\{.*?\}\s*','')
$compileSource|Set-Content "$out/QuickStackCore.Api.cs"
$files=@('Tools/QuickStackWeaponToolFixture/out/QuickStackCore.Api.cs')
$argsList=@('/nologo','/target:library','/nostdlib+','/langversion:latest','/nowarn:0436',('/out:"'+$out+'/QuickStack.Api.dll"'))+$refs+@($files|ForEach-Object {'"'+$root+'/'+$_+'"'})
$argsList|Set-Content "$out/compile.rsp"
& 'C:/Program Files/dotnet/dotnet.exe' 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll' ('@'+$out+'/compile.rsp') 2>&1|Tee-Object -FilePath "$out/compile.txt"
if($LASTEXITCODE-ne0){throw 'Weapon UI standalone API compile failed'}
'PASS complete QuickStackCore except unchanged enum (reference identity) compile against exact native game APIs and reference-only current cumulative mod metadata; no shared build or game execution'





