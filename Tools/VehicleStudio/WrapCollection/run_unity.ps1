param(
    [ValidateSet('BuildAndReview','Icons')][string]$Kind='BuildAndReview',
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Label='current',
    [string]$Ids='MidnightCartographer,TheHungryRoad,DeadZoneRunner,ShotgunMessiah,JensClinic,FuriousRamsaySignature',
    [switch]$ApprovedOnly
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
$active=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"|Where-Object {$_.CommandLine -like "*$project*" -or $_.CommandLine -like "*$($project.Replace('\','/'))*"})
if($active.Count -gt 0){throw "Existing isolated Unity job still running: $($active.ProcessId -join ',')."}
$catalog=Get-Content -Raw (Join-Path $modRoot 'Resources/CamperWraps/catalog.json')|ConvertFrom-Json
$iconIds=$catalog.wraps.id -join ','
$build=Join-Path $wrapRoot '_Build'
$log=Join-Path $wrapRoot ("_Build-$Label.log")
$native='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Resources/rebirth_camper80s.unity3d'
$method=if($Kind -eq 'Icons'){'RebirthWrapCollectionIcons.Run'}else{'RebirthWrapCollectionBuild.BuildAndReview'}
$arguments=@('-batchmode','-quit','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod',$method,
    '-wrapOutput',('"'+$build+'"'),'-wrapRuntimeRoot',('"'+(Join-Path $wrapRoot '_Runtime')+'"'),
    '-wrapReviewOutput',('"'+(Join-Path $wrapRoot ("_Review-$Label"))+'"'),'-wrapVehicleBundle',('"'+$native+'"'),'-wrapIds',$Ids,
    '-wrapIconOutput',('"'+(Join-Path $modRoot 'UIAtlases/ItemIconAtlas')+'"'),'-wrapIconBundle',('"'+(Join-Path $build 'rebirth_camper_wraps.unity3d')+'"'),
    '-wrapIconIds',$iconIds,'-logFile',('"'+$log+'"'))
if($ApprovedOnly){$arguments+=@('-wrapBuildIds',$Ids)}
$process=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2/Editor/Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
$receipt=[ordered]@{pid=$process.Id;kind=$Kind;label=$Label;project=$project;log=$log;reviewIds=$Ids;vehicleBundle=$native;vehicleBundleSHA256=(Get-FileHash -LiteralPath $native -Algorithm SHA256).Hash;startedUtc=[datetime]::UtcNow.ToString('o')}
New-Item -ItemType Directory -Force (Join-Path $wrapRoot '_Jobs')|Out-Null
$receipt|ConvertTo-Json|Set-Content -Encoding UTF8 (Join-Path $wrapRoot "_Jobs/$Label.json")
$receipt|ConvertTo-Json -Compress
