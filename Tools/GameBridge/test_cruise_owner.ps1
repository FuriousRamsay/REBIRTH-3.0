$ErrorActionPreference='Stop'
$source=(Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Input/RebirthNativeControls.cs')).Replace("`r`n","`n")
function Section([string]$start,[string]$end){
 $a=$source.IndexOf($start);if($a -lt 0){throw "Missing production start: $start"}
 $b=$source.IndexOf($end,$a);if($b -le $a){throw "Missing production end: $end"}
 return $source.Substring($a,$b-$a)
}
$fields=Section '    private static int cruiseState;' '    internal static void ResetQuickStackHotkeyTraceHeartbeat()'
$toggle=Section '    private static bool TryConsumeCruiseToggle(' '    private static bool CanDispatchOnFootAction('
$move=Section '    internal static void MoveByAttachedEntityPostfix(' "`n}`n`n[HarmonyPatch"
$fixture=Get-Content -Raw (Join-Path $PSScriptRoot 'test_cruise_owner_fixture.cs')
Add-Type -TypeDefinition ($fixture.Replace('// PRODUCTION_FIELDS',$fields).Replace('// PRODUCTION_TOGGLE',$toggle).Replace('// PRODUCTION_MOVE',$move))
[CruiseOwnerChecks]::Run()
