# Generates a preserving native observation scenario OR verifies captured target JSON. No bridge calls.
[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][ValidateSet('cntPillCaseClosedRebirthPlayer','cntPillCaseEmptyRebirthPlayer')][string]$BlockName,
 [Parameter(Mandatory=$true)][int]$X,
 [Parameter(Mandatory=$true)][int]$Y,
 [Parameter(Mandatory=$true)][int]$Z,
 [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$CarriedItem,
 [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$StackFingerprint,
 [Parameter(Mandatory=$true)][ValidateRange(1,2147483647)][int]$CarriedCount,
 [string]$OutputPath,
 [string]$TargetEvidence
)
$ErrorActionPreference='Stop'
foreach($field in @('CarriedItem','StackFingerprint')){if([string]::IsNullOrWhiteSpace((Get-Variable $field -ValueOnly))){throw "$field must be nonblank"}}
if($PSBoundParameters.ContainsKey('TargetEvidence')){
 if([string]::IsNullOrWhiteSpace($TargetEvidence)){throw 'TargetEvidence must be nonblank'}
 $r=Get-Content -LiteralPath $TargetEvidence -Raw|ConvertFrom-Json
 if($r.ok -isnot [bool] -or $r.ok -ne $true){throw 'Target evidence must be a successful bridge response'}
 if($null -eq $r.target -or $r.target.hit -isnot [bool] -or $r.target.hit -ne $true -or $null -eq $r.target.block){throw 'Missing real hit/block target evidence'}
 $b=$r.target.block
 if($b.name -cne $BlockName -or $b.label -cne 'Medical Cabinet'){throw 'Exact generated block or neutral English Medical Cabinet label differs (Empty prohibited)'}
 if($b.position -isnot [array]){throw 'Position must be a JSON array'}
 $pos=@($b.position)
 foreach($coord in $pos){if(($coord -isnot [int] -and $coord -isnot [long] -and $coord -isnot [double] -and $coord -isnot [decimal]) -or [double]::IsNaN([double]$coord) -or [double]::IsInfinity([double]$coord) -or [Math]::Truncate([double]$coord) -ne [double]$coord){throw 'Position coordinates must be finite numeric integers'}}
 if($pos.Count -ne 3 -or $pos[0] -ne $X -or $pos[1] -ne $Y -or $pos[2] -ne $Z){throw 'Target coordinates differ from prepared owned cabinet'}
 if([string]::IsNullOrWhiteSpace($b.activationText)){throw 'Missing world activation prompt; inspect screenshot and custom naming separately'}
 'PASS exact world target block/coordinates/neutral English label; does not prove loot title or custom prompt'
 return
}
if([string]::IsNullOrWhiteSpace($OutputPath) -or [IO.Path]::GetExtension($OutputPath) -ine '.json'){throw 'OutputPath needs .json suffix'}
$root=[IO.Path]::GetFullPath($PSScriptRoot)+[IO.Path]::DirectorySeparatorChar
$target=[IO.Path]::GetFullPath($OutputPath)
if(-not $target.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'Output must remain within owned ui directory'}
$steps=@(
 @{expectItem=@{name=$CarriedItem;min=$CarriedCount;max=$CarriedCount}},
 @{markItem=$CarriedItem},
 @{lookat=@{x=$X;y=$Y;z=$Z;block=1}},
 @{target=@{}},
 @{expectTarget=$BlockName},
 @{expectTarget='Medical Cabinet'},
 @{screenshot='owned_pillcase_nonempty_world_title'},
 @{activate=@{x=$X;y=$Y;z=$Z;block=1}},
 @{wait=1},
 @{uitree=@{}},
 @{screenshot='owned_pillcase_nonempty_loot_title'},
 @{expectItemDelta=@{name=$CarriedItem;op='==';value=0}},
 @{expectNoErrors=$true},
 @{key=@{name='Escape'}}
)
@{name="Prepared owned nonempty $BlockName naming";stopOnFailure=$true;_note="NOT RUN. Disposable English client, preverified already placed owned unnamed nonempty $BlockName at $X,$Y,$Z in interaction range, menus closed, cursor empty, exact carried $CarriedItem count $CarriedCount fingerprint $StackFingerprint recorded externally and distinct from existing container stack. No grants/transfer/replacement occurs; existing container contents must be fingerprinted before/after externally. Captured target step004-target.json requires this script TargetEvidence mode to enforce exact neutral label; runner expectTarget substring cannot exclude Empty. Native activation opens existing loot, but no exact loot title selector was verified; inspect tree/title and screenshot manually. Carried count preservation does not prove metadata or container ownership. Root controls save/reload/reconnect followups.";steps=$steps}|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $target -Encoding utf8



