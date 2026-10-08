# Exercise the actual runner helpers without loading its dispatch or contacting the game.
$ErrorActionPreference = 'Stop'
$tokens = $null; $parseErrors = $null
$source = Join-Path $PSScriptRoot 'gamebridge.ps1'
$ast = [Management.Automation.Language.Parser]::ParseFile($source, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Runner contains syntax errors' }
foreach ($name in @('Assert-TestResponseField', 'Get-ItemCount', 'Test-Compare')) {
    $definition = $ast.Find({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name }, $true)
    if ($null -eq $definition) { throw "Missing helper: $name" }
    Invoke-Expression $definition.Extent.Text
}
$checks = 0
foreach ($bad in @(
    $null,
    [pscustomobject]@{Status=0;Json=[pscustomobject]@{buffs=@()}},
    [pscustomobject]@{Status=302;Json=[pscustomobject]@{buffs=@()}},
    [pscustomobject]@{Status=500;Json=[pscustomobject]@{buffs=@()}},
    [pscustomobject]@{Status=200;Json=[pscustomobject]@{}},
    [pscustomobject]@{Status=200;Json=[pscustomobject]@{ok=$false;buffs=@()}},
    [pscustomobject]@{Status=200;Json=[pscustomobject]@{buffs=$null}}
)) {
    $rejected = $false
    try { Assert-TestResponseField $bad 'buffs' } catch { $rejected = $true }
    if (-not $rejected) { throw 'Invalid response accepted' }
    $checks++
}
Assert-TestResponseField ([pscustomobject]@{Status=200;Json=[pscustomobject]@{buffs=@()}}) 'buffs'; $checks++
Assert-TestResponseField ([pscustomobject]@{Status=200;Json=[pscustomobject]@{count=0}}) 'count'; $checks++
foreach ($bad in @($null,[pscustomobject]@{},[pscustomobject]@{toolbelt=@()},
    [pscustomobject]@{toolbelt=$null;backpack=@()},
    [pscustomobject]@{toolbelt=@();backpack=$null},
    [pscustomobject]@{ok=$false;toolbelt=@();backpack=@()})) {
    $rejected = $false
    try { Get-ItemCount $bad 'wood' | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Incomplete inventory accepted' }
    $checks++
}
if ((Get-ItemCount ([pscustomobject]@{toolbelt=@();backpack=@()}) 'wood') -ne 0) { throw 'Empty count incorrect' }; $checks++
$inventory = [pscustomobject]@{toolbelt=@([pscustomobject]@{name='wood';count=2});backpack=@([pscustomobject]@{name='wood';count=3})}
if ((Get-ItemCount $inventory 'wood') -ne 5) { throw 'Combined count incorrect' }; $checks++
# Execute the real skill switch bodies with a stubbed transport. Do not copy the
# implementation here: a regression in the runner must fail this test as well.
$runner = $ast.Find({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Invoke-TestFile' }, $true)
$stepSwitch = $runner.Find({ param($n) $n -is [Management.Automation.Language.SwitchStatementAst] }, $true)
$skillBodies = @{}
foreach ($clause in $stepSwitch.Clauses) {
    $kind = $clause.Item1.Value
    if ($kind -in @('expectSkill','markSkill','expectSkillGain')) {
        $skillBodies[$kind] = [scriptblock]::Create($clause.Item2.Extent.Text.Trim().Substring(1).TrimEnd().TrimEnd('}'))
    }
}
if ($skillBodies.Count -ne 3) { throw 'Skill dispatch bodies not found' }
function Invoke-Bridge { return $script:stubReply }
function Test-SkillStep($kind, $payload, $argument, $shouldPass, $status = 200) {
    $script:stubReply = [pscustomobject]@{Status=$status;Json=$payload}
    $arg = $argument; $ok = $true; $skillMarks = @{Cooking=1.0}
    try { . $skillBodies[$kind] } catch { $ok = $false }
    if ($ok -ne $shouldPass) { throw "Unexpected $kind result: expected $shouldPass, got $ok" }
}
$nativeArg = [pscustomobject]@{name='perkCooking';op='==';value=0}
Test-SkillStep 'expectSkill' ([pscustomobject]@{}) $nativeArg $false; $checks++
Test-SkillStep 'expectSkill' ([pscustomobject]@{skills=[pscustomobject]@{}}) $nativeArg $true; $checks++
Test-SkillStep 'expectSkill' ([pscustomobject]@{skills=[pscustomobject]@{}}) $nativeArg $false 500; $checks++
$incomplete = [pscustomobject]@{rebirth=[pscustomobject]@{Skills=@([pscustomobject]@{Id='Cooking';Value=1})}}
Test-SkillStep 'markSkill' $incomplete 'Cooking' $false; $checks++
$gainArg = [pscustomobject]@{skill='Cooking';op='>';value=0}
Test-SkillStep 'expectSkillGain' $incomplete $gainArg $false; $checks++
$complete = [pscustomobject]@{rebirth=[pscustomobject]@{Skills=@([pscustomobject]@{Id='Cooking';Value=1;Progress=0.25})}}
Test-SkillStep 'markSkill' $complete 'Cooking' $true; $checks++
Test-SkillStep 'expectSkillGain' $complete $gainArg $true; $checks++
Write-Output "PASS: $checks offline assertion checks. No game or bridge contacted."
