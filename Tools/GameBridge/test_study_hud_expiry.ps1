$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Network/RebirthStudyHudNetPackage.cs')
$start=$source.IndexOf('public static class RebirthStudyHudClientState')
$end=$source.IndexOf('public static class RebirthStudyHudNetworkService',$start)
if($start -lt 0 -or $end -le $start){throw 'Client state extraction failed'}
$fixture=Get-Content -Raw (Join-Path $PSScriptRoot 'test_study_hud_expiry_fixture.cs')
Add-Type -TypeDefinition $fixture.Replace('// CLIENT_STATE',$source.Substring($start,$end-$start))
[StudyExpiryChecks]::Run()
'PASS actual client state expiry, duplicate refusal, fresh recovery, clear and clock rewind; Unity time/math doubled'