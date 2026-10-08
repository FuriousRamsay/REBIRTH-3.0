$ErrorActionPreference='Stop'
$fixture=Get-Content -Raw (Join-Path $PSScriptRoot 'test_music_owner_receipt_fixture.cs')
$scope=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs')
$state=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Domain/RebirthMusicTransferState.cs')
$owner=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Support/RebirthMusicOwnerTransfer.cs')
function Actual([string]$source,[string]$marker){$i=$source.IndexOf($marker);if($i -lt 0){throw "Missing actual class $marker"};$source.Substring($i)}
$code='using System.Globalization;using System.Xml.Linq;'+$fixture.Replace('// SCOPE',(Actual $scope 'public static class RebirthSurvivorRequestScope')).Replace('// STATE',(Actual $state 'public sealed class RebirthMusicTransferState')).Replace('// OWNER',(Actual $owner 'public enum RebirthMusicOwnerTransferResult'))
Add-Type -TypeDefinition $code
[MusicOwnerChecks]::Run()
'PASS actual music owner Apply: malformed receipts, GUID/legacy scope, duplicate deposit/withdrawal, rejection, full destination. Native inventory/item/CVar adapters doubled.'