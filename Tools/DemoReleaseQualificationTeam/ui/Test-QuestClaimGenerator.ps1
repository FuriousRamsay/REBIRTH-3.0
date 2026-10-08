# Offline generator validation only. Never invokes gamebridge or native APIs.
$ErrorActionPreference='Stop'
$dir=Join-Path $PSScriptRoot 'offline-fixtures'
New-Item -ItemType Directory -Force $dir|Out-Null
$generator=Join-Path $PSScriptRoot 'New-QuestClaimScenario.ps1'
$ledger=Join-Path $dir 'ledger.json'
$output=Join-Path $dir 'synthetic_claim.json'
$base=@{QuestId='SYNTHETIC_NOT_A_REAL_QUEST';RewardPath='SYNTHETIC/reward0';AcceptPath='SYNTHETIC/btnAccept';PayoutLedger=$ledger;OutputPath=$output}
'[ { "name": "syntheticRewardA", "delta": 2 }, { "name": "syntheticRewardB", "delta": 1 } ]'|Set-Content $ledger -Encoding utf8
& $generator @base
$j=Get-Content $output -Raw|ConvertFrom-Json
if($j.steps.Count -ne 12){throw "Unexpected step count $($j.steps.Count)"}
foreach($step in $j.steps){if(@($step.PSObject.Properties).Count -ne 1){throw 'Generated step does not have exactly one key'}}
if(@($j.steps|Where-Object {$_.markItem}).Count -ne 2 -or @($j.steps|Where-Object {$_.expectItemDelta}).Count -ne 2){throw 'Missing payout baselines/assertions'}
$cases=@(
 @{label='duplicate';json='[{"name":"a","delta":1},{"name":"A","delta":2}]'},
 @{label='fractional';json='[{"name":"a","delta":1.5}]'},
 @{label='empty ledger';json='[]'},
 @{label='partial malformed';json='[{"name":"a","delta":1},{"name":"b"}]'},
 @{label='string delta';json='[{"name":"a","delta":"1"}]'},
 @{label='null row';json='[{"name":"a","delta":1},null]'},
 @{label='not array';json='{"name":"a","delta":1}'}
)
$passed=@('positive generated12single-keysteps,2baselines,2exactdeltas')
foreach($case in $cases){
 $case.json|Set-Content $ledger -Encoding utf8
 $argsCase=@{};foreach($key in $base.Keys){$argsCase[$key]=$base[$key]};$argsCase.OutputPath=Join-Path $dir 'rejected.json'
 $rejected=$false;try{& $generator @argsCase}catch{$rejected=$true}
 if(-not $rejected){throw "Accepted invalid $($case.label)"};if(Test-Path $argsCase.OutputPath){throw 'Invalid input created an output file'}
 $passed+=$case.label
}
'[{"name":"a","delta":1}]'|Set-Content $ledger -Encoding utf8
foreach($field in @('QuestId','RewardPath','AcceptPath')){
 $argsCase=@{};foreach($key in $base.Keys){$argsCase[$key]=$base[$key]};$argsCase[$field]=' ';$argsCase.OutputPath=Join-Path $dir 'rejected.json'
 $rejected=$false;try{& $generator @argsCase}catch{$rejected=$true};if(-not $rejected){throw "Accepted blank $field"};$passed+="blank $field"
}
foreach($field in @('QuestId','RewardPath','AcceptPath')){
 $argsCase=@{};foreach($key in $base.Keys){$argsCase[$key]=$base[$key]};$argsCase[$field]='';$argsCase.OutputPath=Join-Path $dir 'rejected.json'
 $rejected=$false;try{& $generator @argsCase}catch{$rejected=$true};if(-not $rejected){throw "Accepted empty $field"};$passed+="empty $field"
}
$argsCase=@{};foreach($key in $base.Keys){$argsCase[$key]=$base[$key]};$argsCase.OutputPath=Join-Path $dir 'rejected.txt'
$rejected=$false;try{& $generator @argsCase}catch{$rejected=$true};if(-not $rejected){throw 'Accepted nonjson output'};$passed+='nonjson output'
@{status='PASS';nativeExecuted=$false;checks=$passed;generatedScenario='synthetic_claim.json';warning='SYNTHETIC ONLY: do not run against game'}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $dir 'results.json') -Encoding utf8
$passed|ForEach-Object {"PASS $_"}


