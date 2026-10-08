param([string]$ModRoot = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
[xml]$instruction = Get-Content -LiteralPath (Join-Path $ModRoot 'Config/_Survivor/theory_instruction.xml') -Raw
[xml]$catalogue = Get-Content -LiteralPath (Join-Path $ModRoot 'Config/_Survivor/theory_specialists.xml') -Raw
if ($instruction.DocumentElement.Name -ne 'survivor_theory_instruction' -or $catalogue.DocumentElement.Name -ne 'survivor_theory_specialists') { throw 'Unexpected Theory catalogue root' }
$required = @($instruction.SelectNodes('/survivor_theory_instruction/subjects/subject') | Where-Object { $_.GetAttribute('enabled') -ne 'false' } | ForEach-Object { $_.GetAttribute('skill_id') })
$profiles = @($catalogue.SelectNodes('/survivor_theory_specialists/specialist'))
[xml]$classDocument = Get-Content -LiteralPath (Join-Path $ModRoot 'Config/_NPC/entityclasses.xml') -Raw
[xml]$groupDocument = Get-Content -LiteralPath (Join-Path $ModRoot 'Config/_NPC/entitygroups.xml') -Raw
[xml]$rootGroups = Get-Content -LiteralPath (Join-Path $ModRoot 'Config/entitygroups.xml') -Raw
$invalidGroups = @()
if (-not $rootGroups.SelectSingleNode('/configs/include[@filename="_NPC/entitygroups.xml"]')) { $invalidGroups += 'missing root include' }
foreach ($profile in $profiles) {
    $profileId = $profile.GetAttribute('profile_id')
    if (-not $profileId.StartsWith('specialist.')) { $invalidGroups += $profileId; continue }
    $profession = $profileId.Substring('specialist.'.Length)
    $group = $groupDocument.SelectSingleNode('/configs/append[@xpath="/entitygroups"]/entitygroup[@name="npcSpecialist_'+$profession+'"]')
    if (-not $group -or $group.SelectNodes('e').Count -ne 2) { $invalidGroups += $profileId; continue }
    foreach ($gender in @('Man','Woman')) {
        $className = 'npcRebirthSpecialist'+$profession+$gender+'SDCS'
        $classNode = $classDocument.SelectSingleNode('//entity_class[@name="'+$className+'"]')
        if (-not $group.SelectSingleNode('e[@n="'+$className+'"]') -or -not $classNode -or
            -not $classNode.SelectSingleNode('property[@name="RebirthProfile" and @value="'+$profileId+'"]') -or
            $classNode.GetAttribute('extends') -ne ('npcRebirthPersistentSurvivor'+$gender+'SDCS')) { $invalidGroups += $className }
    }
}
$missing = @()
$coverage = @()
foreach ($subject in $required) {
    $instructors = @($profiles | Where-Object { @($_.SelectNodes('subject') | Where-Object { $_.GetAttribute('skill_id') -eq $subject }).Count -gt 0 } | ForEach-Object { $_.GetAttribute('profile_id') })
    if ($instructors.Count -eq 0) { $missing += $subject }
    $coverage += [pscustomobject]@{ subject=$subject; profiles=$instructors }
}
[pscustomobject]@{
    check='authored-specialist-subject-coverage'
    passed=($required.Count -eq 48 -and $missing.Count -eq 0 -and $invalidGroups.Count -eq 0)
    requiredSubjects=$required.Count
    authoredProfiles=$profiles.Count
    invalidGroups=$invalidGroups
    missingSubjects=$missing
    coverage=$coverage
    limitation='Checks authored XML subject coverage only. Does not prove profile registration, spawning, reachability, native lesson completion or networking.'
} | ConvertTo-Json -Depth 5
if ($required.Count -ne 48 -or $missing.Count -gt 0 -or $invalidGroups.Count -gt 0) { exit 2 }