# Creates a native quest claim scenario for a recorded disposable fixture; does not invoke the bridge.
[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$QuestId,
 [Parameter(Mandatory=$true)][string]$RewardPath,
 [Parameter(Mandatory=$true)][string]$AcceptPath,
 [Parameter(Mandatory=$true)][string]$PayoutLedger,
 [Parameter(Mandatory=$true)][string]$OutputPath
)
$ErrorActionPreference='Stop'
foreach($field in @('QuestId','RewardPath','AcceptPath','PayoutLedger','OutputPath')){
 if([string]::IsNullOrWhiteSpace((Get-Variable -Name $field -ValueOnly))){throw "$field cannot be blank"}
}
if([IO.Path]::GetExtension($OutputPath) -ine '.json'){throw 'OutputPath must have .json suffix'}
$ownerRoot=[IO.Path]::GetFullPath($PSScriptRoot)+[IO.Path]::DirectorySeparatorChar
$target=[IO.Path]::GetFullPath($OutputPath)
if(-not $target.StartsWith($ownerRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'OutputPath must be inside this ui artifact directory'}
$rawLedger=Get-Content -LiteralPath $PayoutLedger -Raw
if(-not $rawLedger.TrimStart().StartsWith('[')){throw 'Ledger must be a JSON array'}
$ledger=@($rawLedger | ConvertFrom-Json)
if($ledger.Count -eq 0){throw 'Supply all mandatory and selected inventory payouts; empty ledger prohibited'}
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($row in $ledger){
 if($null -eq $row -or $row -isnot [System.Management.Automation.PSCustomObject]){throw 'Every ledger row must be an object'}
 $fields=@($row.PSObject.Properties.Name)
 if($fields.Count -ne 2 -or $fields -notcontains 'name' -or $fields -notcontains 'delta'){throw 'Every ledger row needs exactly name and delta'}
 if($row.name -isnot [string] -or [string]::IsNullOrWhiteSpace($row.name) -or $row.name -ne $row.name.Trim()){throw 'Each name must be a nonblank trimmed string'}
 if(-not $seen.Add($row.name)){throw 'Duplicate payout name: aggregate mandatory and chosen payouts by name'}
 if($row.delta -isnot [int] -and $row.delta -isnot [long] -and $row.delta -isnot [double] -and $row.delta -isnot [decimal]){throw 'Delta must be a numeric positive integer'}
 $amount=[double]$row.delta
 if([double]::IsNaN($amount) -or [double]::IsInfinity($amount) -or $amount -le 0 -or $amount -gt [int]::MaxValue -or [Math]::Truncate($amount) -ne $amount){throw 'Delta must be a positive integer within Int32 range'}
}
$steps=@(@{expectWindowOpen='questTurnIn'})
foreach($row in $ledger){$steps+=@{markItem=$row.name}}
$steps+=@{click=@{path=$RewardPath}}
$steps+=@{expectUi=@{id='questItemInfo'}}
$steps+=@{screenshot='quest_selected_before_deliberate_claim'}
$steps+=@{click=@{path=$AcceptPath}}
$steps+=@{wait=1}
foreach($row in $ledger){$steps+=@{expectItemDelta=@{name=$row.name;op='==';value=[double]$row.delta}}}
$steps+=@{expectNoUi=@{id='rebirthQuestTurnInRoot'}}
$steps+=@{expectNoErrors=$true}
@{name="Native quest exact inventory payout $QuestId";stopOnFailure=$true;_note="NOT RUN. Recorded disposable fixture for quest $QuestId; native questTurnIn already opened, reward path uniquely identifies chosen offer, AcceptPath targets native claim. Ensure all payouts fit inventory; full-bag overflow is separate acceptance. Ledger must include aggregate chosen plus mandatory payouts. Operator must verify journal exact identity and single claim, metadata and no unexpected reward types externally. Does not force-open UI or change quest state.";steps=$steps}|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $target -Encoding utf8

