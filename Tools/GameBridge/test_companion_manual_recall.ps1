$ErrorActionPreference='Stop'
$source=(Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Companions/RebirthCompanionService.cs')).Replace("`r`n","`n")
$start=$source.IndexOf('    private static bool HasTeleportTarget(')
$end=$source.IndexOf('    private static void ProcessTeleportFollowers(',$start)
if($start -lt 0 -or $end -le $start){throw 'Production recall admission method missing'}
$method=$source.Substring($start,$end-$start).Replace('private static bool HasTeleportTarget','public static bool HasTeleportTarget')
$fixture=Get-Content -Raw (Join-Path $PSScriptRoot 'test_companion_manual_recall_fixture.cs')
Add-Type -TypeDefinition $fixture.Replace('// PRODUCTION_METHOD',$method)
[ManualRecallChecks]::Run()
