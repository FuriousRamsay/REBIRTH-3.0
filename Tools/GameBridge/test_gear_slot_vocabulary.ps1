$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot '../..'
$source=Get-Content -Raw (Join-Path $root 'Scripts/Survivor/Domain/RebirthGearTransferState.cs')
$start=$source.IndexOf('    internal static bool IsSupportedSlot(');$end=$source.IndexOf('    public bool HasRecoveryAttempts',$start)
if($start -lt 0 -or $end -le $start){throw 'Production slot policy missing'}
$method=$source.Substring($start,$end-$start).Replace('internal static bool IsSupportedSlot','public static bool IsSupportedSlot')
Add-Type -TypeDefinition ('public static class GearSlotVocabularyChecks {'+$method+'}')
[xml]$profiles=Get-Content -Raw (Join-Path $root 'Config/_Survivor/support_profiles.xml')
foreach($profile in $profiles.SelectNodes("//support_profile[@kind='survivor_gear']")){
 if(-not [GearSlotVocabularyChecks]::IsSupportedSlot($profile.GetAttribute('gear_slot_id'))){throw ('Authored slot rejected: '+$profile.GetAttribute('id'))}
}
foreach($slot in @('backpack','belt','support','walkman','outerwear_lining')){
 if(-not [GearSlotVocabularyChecks]::IsSupportedSlot($slot)){throw "Existing slot rejected: $slot"}
}
foreach($slot in @($null,'','unknown','outerwear_lining ','Outerwear_Lining')){
 if([GearSlotVocabularyChecks]::IsSupportedSlot($slot)){throw "Unknown slot admitted: $slot"}
}
Write-Output 'PASS actual slot policy against authored gear; transfer/cold native recovery NOT covered.'
