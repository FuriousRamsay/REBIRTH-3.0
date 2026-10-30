param(
    [ValidateSet('BuildAndReview','ReviewOnly','Icons','PloughSurface','Cutouts','CombinedCutouts','HoodAudit')][string]$Kind='BuildAndReview',
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Label='current',
    [string]$Ids='MidnightCartographer,TheHungryRoad,DeadZoneRunner,ShotgunMessiah,JensClinic,FuriousRamsaySignature',
    [switch]$ApprovedOnly,
    [switch]$IsolatedOutput,
    [switch]$OnlyReviewedIcons,
    [switch]$SkipIcons,
    [switch]$SwitchRegression,
    [switch]$ConceptLayers,
    [switch]$HoodMaster,
    [switch]$ApprovedUndead,
    [switch]$PickupReview,
    [switch]$ShadowReview,
    [string]$DiagnosticRawDirectory,
    [switch]$TailgateNormalDiagnostic,
    [ValidateSet(0,1,2)][int]$DiagnosticLayoutVersion=0
)
$ErrorActionPreference='Stop'
$wrapRoot=$PSScriptRoot
$modRoot=[IO.Path]::GetFullPath((Join-Path $wrapRoot '../../..'))
if($Ids -notmatch '^[A-Za-z0-9,]+$'){throw 'Only stable catalogue IDs are accepted.'}
$incoming='E:/_Haven/shared/coordination/HAV3N_to_codex.txt'
if(Test-Path $incoming){
    $request=Get-Content -Raw $incoming
    if($request -match '(?m)^STATUS:\s*(QUIET|CLOSE_GAME)\s*$'){
        $status=$Matches[1]
        if($request -match '(?m)^UNTIL:\s*(\d{4}-\d{2}-\d{2} \d{2}:\d{2})\s*$'){
            $until=[datetime]::ParseExact($Matches[1],'yyyy-MM-dd HH:mm',[Globalization.CultureInfo]::InvariantCulture)
            $now=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([datetime]::UtcNow,'Eastern Standard Time')
            if($until -gt $now){throw "Active HAV3N coordination request $status until $until; no heavy job started."}
        }
    }
}
$project=Join-Path $wrapRoot '_UnityProject'
& (Join-Path $modRoot 'Tools/AssetBundle/Stage-CrunchPackaging.ps1') -ProjectPath $project
$active=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"|Where-Object {$_.CommandLine -like "*$project*" -or $_.CommandLine -like "*$($project.Replace('\','/'))*"})
if($active.Count -gt 0){throw "Existing isolated Unity job still running: $($active.ProcessId -join ',')."}
$catalog=Get-Content -Raw (Join-Path $modRoot 'Resources/CamperWraps/catalog.json')|ConvertFrom-Json
$iconIds=$catalog.wraps.id -join ','
$addon=Join-Path $modRoot 'Resources/CamperWraps/undead-chill-catalog.json'
if(Test-Path $addon){$iconIds+=','+((Get-Content -Raw $addon|ConvertFrom-Json).wraps.id -join ',')}
$build=Join-Path $wrapRoot '_Build'
$runtime=Join-Path $wrapRoot '_Runtime'
if($IsolatedOutput){$build=Join-Path $wrapRoot "_Build-$Label";$runtime=Join-Path $wrapRoot "_Runtime-$Label"}
$log=Join-Path $wrapRoot ("_Build-$Label.log")
$native='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Resources/rebirth_camper80s.unity3d'
$method=if($Kind -eq 'HoodAudit'){'RebirthHoodSurfaceAudit.Run'}elseif($Kind -eq 'ReviewOnly'){'RebirthWrapCollectionReview.Run'}elseif($Kind -eq 'Cutouts' -or $Kind -eq 'CombinedCutouts'){'RebirthNativeCutoutTemplates.Run'}elseif($Kind -eq 'PloughSurface'){'RebirthPloughSurfaceReview.Run'}elseif($Kind -eq 'Icons'){'RebirthWrapCollectionIcons.Run'}else{'RebirthWrapCollectionBuild.BuildAndReview'}
$arguments=@('-batchmode','-quit','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod',$method,
    '-wrapOutput',('"'+$build+'"'),'-wrapRuntimeRoot',('"'+$runtime+'"'),
    '-wrapReviewOutput',('"'+(Join-Path $wrapRoot ("_Review-$Label"))+'"'),'-wrapVehicleBundle',('"'+$native+'"'),'-wrapIds',$Ids,
    '-wrapIconOutput',('"'+(Join-Path $modRoot 'UIAtlases/ItemIconAtlas')+'"'),'-wrapIconBundle',('"'+(Join-Path $build 'rebirth_camper_wraps.unity3d')+'"'),
    '-wrapIconIds',$iconIds,'-logFile',('"'+$log+'"'))
if($ApprovedUndead){
    if($Ids.Contains(',')){throw 'Dedicated bundle jobs require exactly one wrap ID.'}
    $specialBundles=@{
        MidnightCartographer='rebirth_midnight_cartographer.unity3d'
        CaptStress='rebirth_captstress.unity3d'
        GenmaSaotome='rebirth_genma_saotome.unity3d'
        UndeadChillAfterHours='rebirth_undead_chill_after_hours.unity3d'
        UndeadChill='rebirth_undead_chill.unity3d'
    }
    $dedicatedName=if($specialBundles.ContainsKey($Ids)){$specialBundles[$Ids]}else{'rebirth_wrap_'+$Ids.ToLowerInvariant()+'.unity3d'}
    $bundleIndex=[Array]::IndexOf($arguments,'-wrapIconBundle')
    if($bundleIndex -ge 0){$arguments[$bundleIndex+1]='"'+(Join-Path $build $dedicatedName)+'"'}
}
if($SkipIcons){$i=[Array]::IndexOf($arguments,'-wrapIconOutput');$arguments=@($arguments[0..($i-1)])+@('-logFile',('"'+$log+'"'))}
if($ApprovedUndead){$arguments+=@('-wrapApprovedUndead')}
if($PickupReview){$arguments+=@('-wrapPickupReview')}
if($ShadowReview){$arguments+=@('-wrapShadowReview')}
if($DiagnosticRawDirectory){
    if($Kind -ne 'ReviewOnly' -or !$IsolatedOutput -or !$SkipIcons){throw 'Raw diagnostics require ReviewOnly, IsolatedOutput and SkipIcons.'}
    $arguments+=@('-wrapDiagnosticRawDirectory',('"'+(Resolve-Path -LiteralPath $DiagnosticRawDirectory).Path+'"'))
}
if($DiagnosticLayoutVersion){
    if(!$DiagnosticRawDirectory){throw 'Layout override requires isolated raw diagnostic review.'}
    $arguments+=@('-wrapDiagnosticLayoutVersion',"$DiagnosticLayoutVersion")
}
if($TailgateNormalDiagnostic){
    if($Kind -ne 'ReviewOnly' -or !$IsolatedOutput -or !$SkipIcons -or !$DiagnosticRawDirectory){throw 'Normal diagnostics require an isolated raw review with icons disabled.'}
    $arguments+=@('-wrapTailgateNormalDiagnostic')
}
if($HoodMaster){$arguments+=@("-wrapHoodMaster")}
if($ConceptLayers){$arguments+=@("-wrapConceptLayers")}
if($SwitchRegression){$arguments+=@("-wrapSwitchRegression")}
if($Kind -eq 'CombinedCutouts'){$arguments+=@('-wrapCombinedCutouts')}
if($ApprovedOnly){$arguments+=@('-wrapBuildIds',$Ids)}
if($OnlyReviewedIcons){$arguments+=@('-wrapIconFilter',$Ids)}
$inputFiles=@(
    (Join-Path $wrapRoot 'ConceptProjection/UndeadChill-approved.png'),
    (Join-Path $wrapRoot 'ConceptProjection/UndeadChill-layout.json'),
    (Join-Path $wrapRoot 'concept_projection.py'),
    (Join-Path $wrapRoot 'prepare_shader.py')
)
$inputFiles+=@('fiveview_projection.py','native_concept_layers.py','prepare_unity.py','ConceptProjection/native-layer-manifest.json' | ForEach-Object {Join-Path $wrapRoot $_})
$inputFiles+=@(Get-ChildItem -LiteralPath (Join-Path $wrapRoot 'ConceptProjection') -Filter '*layers-v1.png' | ForEach-Object {$_.FullName})
$inputFiles+=@(Join-Path $wrapRoot 'ConceptProjection/UndeadChill-hood-clean-v1.png')
$inputFiles+=@(Get-ChildItem -LiteralPath (Join-Path $project 'Assets/Editor') -Filter '*.cs'|ForEach-Object {$_.FullName})
$inputFiles+=@(Get-ChildItem -LiteralPath (Join-Path $project 'Assets/REBIRTHCamperWrapCollection/Shaders') -Filter '*.shader'|ForEach-Object {$_.FullName})
$inputHashes=@{}
foreach($inputFile in $inputFiles){if(Test-Path -LiteralPath $inputFile){$inputHashes[$inputFile.Substring($modRoot.Length+1).Replace('\','/')]=(Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash}}
$process=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2/Editor/Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
$receipt=[ordered]@{pid=$process.Id;kind=$Kind;label=$Label;project=$project;log=$log;reviewIds=$Ids;vehicleBundle=$native;vehicleBundleSHA256=(Get-FileHash -LiteralPath $native -Algorithm SHA256).Hash;startedUtc=[datetime]::UtcNow.ToString('o');inputs=$inputHashes}
New-Item -ItemType Directory -Force (Join-Path $wrapRoot '_Jobs')|Out-Null
$receipt|ConvertTo-Json|Set-Content -Encoding UTF8 (Join-Path $wrapRoot "_Jobs/$Label.json")
$receipt|ConvertTo-Json -Compress
