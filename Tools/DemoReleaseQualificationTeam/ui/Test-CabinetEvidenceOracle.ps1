# Offline cabinet evidence oracle checks; no bridge/native calls.
$ErrorActionPreference='Stop'
$dir=Join-Path $PSScriptRoot 'offline-fixtures'
New-Item -ItemType Directory -Force $dir|Out-Null
$p=Join-Path $PSScriptRoot 'New-CabinetNamingScenario.ps1'
$evidence=Join-Path $dir 'cabinet_synthetic_target.json'
$argsBase=@{BlockName='cntPillCaseClosedRebirthPlayer';X=10;Y=20;Z=30;CarriedItem='SYNTHETIC_ONLY';StackFingerprint='SYNTHETIC_ONLY';CarriedCount=1;TargetEvidence=$evidence}
$valid='{"ok":true,"target":{"hit":true,"block":{"name":"cntPillCaseClosedRebirthPlayer","label":"Medical Cabinet","position":[10,20,30],"activationText":"SYNTHETIC prompt"}}}'
$valid|Set-Content $evidence -Encoding utf8
& $p @argsBase|Out-Null
$checks=@('valid synthetic exact wrapper')
$cases=@(
 @{label='failed wrapper';json=$valid.Replace('"ok":true','"ok":false')},
 @{label='missing ok';json=$valid.Replace('"ok":true,','')},
 @{label='string ok';json=$valid.Replace('"ok":true','"ok":"true"')},
 @{label='nonarray position';json=$valid.Replace('[10,20,30]','"10,20,30"')},
 @{label='string coordinates';json=$valid.Replace('[10,20,30]','["10",20,30]')},
 @{label='null coordinates';json=$valid.Replace('[10,20,30]','[null,20,30]')},
 @{label='fractional coordinates';json=$valid.Replace('[10,20,30]','[10.5,20,30]')},
 @{label='wrong coordinates';json=$valid.Replace('[10,20,30]','[11,20,30]')},
 @{label='short position';json=$valid.Replace('[10,20,30]','[10,20]')},
 @{label='empty title';json=$valid.Replace('Medical Cabinet','Medical Cabinet Empty')},
 @{label='no hit';json=$valid.Replace('"hit":true','"hit":false')}
)
foreach($case in $cases){$case.json|Set-Content $evidence -Encoding utf8;$rejected=$false;try{& $p @argsBase|Out-Null}catch{$rejected=$true};if(-not $rejected){throw "Accepted $($case.label)"};$checks+=$case.label}
$valid|Set-Content $evidence -Encoding utf8
foreach($emptyPath in @('',' ')){$argsCase=@{};foreach($k in $argsBase.Keys){$argsCase[$k]=$argsBase[$k]};$argsCase.TargetEvidence=$emptyPath;$rejected=$false;try{& $p @argsCase|Out-Null}catch{$rejected=$true};if(-not $rejected){throw 'Accepted blank evidence path'};$checks+='blank evidence path'}
@{status='PASS';nativeExecuted=$false;checks=$checks;warning='All evidence synthetic; no native result'}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $dir 'cabinet_oracle_results.json') -Encoding utf8
$checks|ForEach-Object {"PASS $_"}
