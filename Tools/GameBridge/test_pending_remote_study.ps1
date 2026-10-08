$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Progression/RebirthPendingRemoteStudy.cs')
$start=$source.IndexOf('internal static class RebirthPendingRemoteStudy')
if($start -lt 0){throw 'Actual class missing'}
$fixture=Get-Content -Raw (Join-Path $PSScriptRoot 'test_pending_remote_study_fixture.cs')
Add-Type -TypeDefinition $fixture.Replace('// ACTUAL_PENDING_CLASS',$source.Substring($start))
[PendingStudyChecks]::Run()
'PASS actual pending study scheduler with native time/world/inventory/study/network doubles'