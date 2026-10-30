#requires -Version 7.0
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$game='C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die'
$prod=Join-Path $game 'Mods/zzz_REBIRTH__3_0'
$relative='Scripts/Modules/BossEvents/RebirthBossNativePublication.cs'
$helper='Scripts/Modules/BossEvents/RebirthBossPublicationCapture.cs'
$baseline='F81831DCDC35B7154CEE152A9E2FECD0F7096BBB5FFE1384028D3015CA2EC4F2'
if((Get-FileHash (Join-Path $prod $relative)).Hash -ne $baseline){throw 'Production publication baseline changed.'}
$run=Join-Path 'E:\_Haven\shared\REBIRTHTemp' ('sub7-boss-pool-'+[guid]::NewGuid())
New-Item -ItemType Directory $run,(Join-Path $run 'empty-feed') -Force|Out-Null
$docs=Join-Path $repo '_Documentation/Purge'
function Extract([string]$source,[string]$marker) {
    $start=$source.IndexOf($marker,[StringComparison]::Ordinal);if($start -lt 0){throw ('Missing source '+$marker)}
    $brace=$source.IndexOf('{',$start);$semi=$source.IndexOf(';',$start);$arrow=$source.IndexOf('=>',$start)
    if($arrow -ge $start -and $arrow -lt $brace -and $semi -lt $brace){return $source.Substring($start,$semi-$start+1)}
    $depth=0;$end=$brace
    do{if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}while($depth -gt 0)
    return $source.Substring($start,$end-$start)
}
$before=@()
$inputPaths=@(
    (Join-Path $prod $relative),(Join-Path $repo $relative),(Join-Path $repo $helper),
    (Join-Path $prod 'Scripts/Survivor/Persistence/RebirthStationSnapshotEvidence.cs'),
    (Join-Path $prod 'Scripts/Survivor/Persistence/RebirthStationPublicationEvidence.cs'),
    (Join-Path $prod 'Scripts/Survivor/Persistence/RebirthStationSnapshotEvidence.Entity.cs'),
    (Join-Path $prod 'Scripts/Survivor/Persistence/RebirthStationSnapshotEvidencePatches.cs'),
    (Join-Path $prod 'Scripts/Survivor/Persistence/RebirthStationRegionPayload.cs'),
    (Join-Path $game '7DaysToDie_Data/Managed/Assembly-CSharp.dll'),
    (Join-Path $prod 'Scripts/QuickStack/RebirthLogisticsSnapshotReservation.cs'),
    (Join-Path $prod 'Scripts/Modules/BossEvents/RebirthBossEvents.cs')
)
foreach($path in $inputPaths){$before+=[pscustomobject]@{path=$path;sha256=(Get-FileHash $path).Hash}}
$source=[IO.File]::ReadAllText((Join-Path $repo $relative))
$old=[IO.File]::ReadAllText((Join-Path $prod $relative))
$boss=[IO.File]::ReadAllText((Join-Path $prod 'Scripts/Modules/BossEvents/RebirthBossEvents.cs'))
$budget=[IO.File]::ReadAllText((Join-Path $prod 'Scripts/QuickStack/RebirthLogisticsSnapshotReservation.cs'))
$model='using System;using System.Collections.Generic;using UnityEngine;'+(Extract $boss 'public sealed class RebirthBossSpawnOriginal')+
 'public sealed class RebirthBossEventInstance{public Guid EventId=Guid.NewGuid();public ProgressionSnapshot Progression=new ProgressionSnapshot();public List<RebirthBossSpawnOriginal> SpawnOriginals=new List<RebirthBossSpawnOriginal>();}public sealed class ProgressionSnapshot{public string OwnerStableId="owner";}'+
 'public static class RebirthBossEventRegistry{public static Dictionary<Guid,RebirthBossEventInstance> Events=new Dictionary<Guid,RebirthBossEventInstance>();'+(Extract $boss 'public static bool TryGet(Guid')+'}'
[IO.File]::WriteAllText((Join-Path $run 'Models.cs'),$model)
[IO.File]::WriteAllText((Join-Path $run 'Budget.cs'), 'using System;'+(Extract $budget 'public interface IRebirthStationSnapshotByteBudget'))
$methods=[Collections.Generic.List[string]]::new()
foreach($marker in @('internal static string Key(','internal static byte[] Stamp(','internal static bool TryPublishedEntity(','internal static bool NativeBirth(','private static bool Flag(','private static void Channel(','private static void Skip(','private static int StringLength(')){
    $method=Extract $source $marker
    if($method -cne (Extract $old $marker)){throw ('Exact entity parser/stamp was changed: '+$marker)}
    if($marker -like '*TryPublishedEntity*') {
        $core=$method.Replace('bool TryPublishedEntity(','bool ParseOriginal(')
        $methods.Add($core)
        $methods.Add('internal static int Parses;internal static bool TryPublishedEntity(byte[] bytes,int x,int z,RebirthBossSpawnOriginal o,string key,byte[] stamp,out string proof,bool absent=false,bool death=false){Parses++;return ParseOriginal(bytes,x,z,o,key,stamp,out proof,absent,death);}')
    }else{$methods.Add($method)}
}
$start=$old.IndexOf('if(RebirthStationSnapshotEvidence.HasPublished(item.Watch)&&')
if($start -lt 0){throw 'Expected exact old polling condition absent.'}
$open=$start+2;$end=$open;$depth=0
do{if($old[$end] -eq '('){$depth++};if($old[$end] -eq ')'){$depth--};$end++}while($depth -gt 0)
$condition=$old.Substring($open+1,$end-$open-2).Replace('item.Watch','watch').Replace('item.Chunk','chunk').Replace('item.Original','original').Replace('item.Event','encounter').Replace('item.Death','death')
$methods.Add('internal static bool BaselinePoll(RebirthStationSnapshotEvidence.Request watch,Chunk chunk,World world,RebirthBossEventInstance encounter,RebirthBossSpawnOriginal original,bool death=false){return '+$condition+';}')
[IO.File]::WriteAllText((Join-Path $run 'Parser.cs'),'using System;using System.IO;using System.Text;using System.Linq;internal static class RebirthBossNativePublication{'+($methods -join [Environment]::NewLine)+'}')
$retirement='using System;using System.Collections.Generic;internal static class RetirementFixture{'+
 (Extract $source 'private sealed class Bound ')+(Extract $source 'private sealed class Removing ')+
 'private static readonly Dictionary<Entity,Bound> bound=new Dictionary<Entity,Bound>();private static readonly Dictionary<Guid,Removing> removing=new Dictionary<Guid,Removing>();private static World scope;private static string directory;private static float nextUpdate;private static int reservedFloor;private static bool idsIndexed;'+
 (Extract $source 'private static void StopWatch(Bound')+(Extract $source 'private static void StopWatch(Removing')+(Extract $source 'private static void RetireObservations()')+
 'internal static void Own(RebirthBossPublicationCapture a,RebirthStationSnapshotEvidence.Request wa,Chunk ca,RebirthBossPublicationCapture b,RebirthStationSnapshotEvidence.Request wb,Chunk cb){bound[new Entity()]=new Bound{Capture=a,Watch=wa,Chunk=ca};removing[Guid.NewGuid()]=new Removing{Capture=b,Watch=wb,Chunk=cb};}internal static void Retire()=>RetireObservations();}'
[IO.File]::WriteAllText((Join-Path $run 'Retirement.cs'),$retirement)
Copy-Item (Join-Path $repo $relative) (Join-Path $run 'FullPublicationSource.cs.txt')
Copy-Item (Join-Path $repo $helper) (Join-Path $run 'Capture.cs')
Copy-Item (Join-Path $prod 'Scripts/Survivor/Persistence/RebirthStationSnapshotEvidence.cs') (Join-Path $run 'Shared.cs')
Copy-Item (Join-Path $PSScriptRoot 'NativeDoubles.cs'),(Join-Path $PSScriptRoot 'SharedUnregisteredEntity.cs'),(Join-Path $PSScriptRoot 'Program.cs') $run
Add-Content (Join-Path $run 'NativeDoubles.cs') 'namespace UnityEngine{public struct Vector3{public float x,y,z;}}'
$props='<TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><Nullable>disable</Nullable><RestoreSources>empty-feed</RestoreSources><NuGetAudit>false</NuGetAudit>'
function Project($name,$files,$refs,$kind='Exe') {
    [IO.File]::WriteAllText((Join-Path $run ($name+'.csproj')),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'+$props+'<OutputType>'+$kind+'</OutputType><AssemblyName>'+$name+'</AssemblyName></PropertyGroup><ItemGroup><Compile Include="'+$files+'"/></ItemGroup><ItemGroup>'+($refs -join [Environment]::NewLine)+'</ItemGroup></Project>')
}
function Build($name){
    $output=& dotnet build (Join-Path $run ($name+'.csproj')) --nologo 2>&1
    $output|Set-Content (Join-Path $docs ('SUB7_BOSS_POOL_'+$name+'_BUILD_20261029.txt'))
    if($LASTEXITCODE -ne 0){$output|Write-Output;throw ('Isolated fixture/API compile failed: '+$name)}
}
Project 'fixture' 'Capture.cs;Shared.cs;SharedUnregisteredEntity.cs;Models.cs;Budget.cs;Parser.cs;Retirement.cs;NativeDoubles.cs;Program.cs' @()
Build 'fixture'
$output=& dotnet (Join-Path $run 'bin/Debug/net9.0/fixture.dll') 2>&1;$code=$LASTEXITCODE
$output|Set-Content (Join-Path $docs 'SUB7_BOSS_POOL_FIXED_20261029.txt')
if($code -ne 0){$output|Write-Output;throw 'Actual-source pooled publication fixture failed.'}
$negative=& dotnet (Join-Path $run 'bin/Debug/net9.0/fixture.dll') --baseline 2>&1;$code=$LASTEXITCODE
$negative|Set-Content (Join-Path $docs 'SUB7_BOSS_POOL_BASELINE_NEGATIVE_20261029.txt')
if($code -ne 1 -or ($negative -join ' ') -notmatch 'FAIL next-tick baseline keeps original published proof'){throw 'Missing expected old polling failure.'}
$refs=Get-ChildItem (Join-Path $game '7DaysToDie_Data/Managed') -Filter *.dll|Where-Object {$_.Name -notmatch '^(System[.]|System$|Microsoft[.]|mscorlib|netstandard)'}|ForEach-Object{
    '<Reference Include="'+$_.BaseName+'"><HintPath>'+[Security.SecurityElement]::Escape($_.FullName)+'</HintPath><Private>false</Private></Reference>'
}
[IO.File]::WriteAllText((Join-Path $run 'HookApi.cs'),'internal static class HookApi{internal static void Register(){ModEvents.WorldShuttingDown.RegisterHandler(RetireWorld);ModEvents.GameShutdown.RegisterHandler(RetireGame);}private static void RetireWorld(ref ModEvents.SWorldShuttingDownData data){}private static void RetireGame(ref ModEvents.SGameShutdownData data){}}')
Project 'api' 'Capture.cs;Shared.cs;SharedUnregisteredEntity.cs;Models.cs;Budget.cs;Parser.cs;HookApi.cs' $refs 'Library'
Build 'api'
# Syntax-parse the complete integration file without building/installing a mod.
$dotnetRoot=Split-Path (Get-Command dotnet).Source
$sdk=Get-ChildItem (Join-Path $dotnetRoot 'sdk') -Directory|Where-Object {$_.Name -like '9.*'}|Sort-Object Name|Select-Object -Last 1
Add-Type -Path (Join-Path $sdk.FullName 'Roslyn/bincore/Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $sdk.FullName 'Roslyn/bincore/Microsoft.CodeAnalysis.CSharp.dll')
$tree=[Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($source)
if(@($tree.GetDiagnostics()|Where-Object Severity -eq Error).Count){throw 'Full publication source has syntax errors.'}
if(($source.Split('new RebirthBossPublicationCapture(').Count-1) -ne 2 -or $source.Contains('RebirthStationSnapshotEvidence.HasPublished(item.Watch)') -or $source.Contains('RebirthStationSnapshotEvidence.TryCopy(item.Watch')){throw 'Publication callers not fully migrated.'}
foreach($input in $before){if((Get-FileHash $input.path).Hash -ne $input.sha256){throw 'Exact input changed during verification.'}}
$before|ConvertTo-Json -Depth 4|Set-Content (Join-Path $docs 'SUB7_BOSS_POOL_SOURCE_GUARDS_20261029.json')
$run|Set-Content (Join-Path $docs 'SUB7_BOSS_POOL_SNAPSHOT_20261029.txt')
$output|Select-Object -Last 3
'Baseline negative exit1; new capture/core/parser API compiled; full integration syntax and both callsites checked.'
