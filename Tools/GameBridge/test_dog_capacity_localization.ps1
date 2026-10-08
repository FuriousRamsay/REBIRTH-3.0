$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/NPC/Dog/RebirthDogLifecycleService.cs')
$rows=Import-Csv (Join-Path $PSScriptRoot '../../Config/Localization.csv')
$match=[regex]::Match($source,'if \(dogs >= globalCap\)\s*\{\s*reason = Localization.Get\(RebirthSurvivorMode.IsEnabledForCurrentWorld\(\)\s*\? "([^"]+)" : "([^"]+)"\);\s*return false;')
if(!$match.Success){throw 'Shared cap denial no longer uses capacity mode predicate'}
$rebirth=@($rows|Where-Object Key -eq $match.Groups[1].Value)
$native=@($rows|Where-Object Key -eq $match.Groups[2].Value)
if($rebirth.Count -ne 1 -or $native.Count -ne 1){throw 'Missing/duplicate dog denial localization'}
if($rebirth[0].english -match 'Charismatic|perk|Increase'){throw 'REBIRTH denial promises legacy or unsupported upgrade'}
if($native[0].english -notmatch 'Increase Charismatic Nature'){throw 'Base Game hint lost'}
if($source -notmatch 'if \(RebirthSurvivorMode.IsEnabledForCurrentWorld\(\)\)\s*return RebirthAdvancedDisciplineRegistry.BaseDogCapacity;'){throw 'REBIRTH authored capacity changed'}
if($source -notmatch 'return charismaticNature \+ 1;'){throw 'Base Game capacity changed'}
if(([regex]::Matches($source,'!CanAcquire\(player, out reason\)')).Count -lt 2){throw 'Hiring/deployment gate coverage changed'}
if(([regex]::Matches($source,'!RebirthDogLifecycleService.CanAcquire\(player, out (reason|preflightReason)\)')).Count -lt 2){throw 'Placement preflight gate coverage changed'}
Write-Output 'PASS source/localization contract: shared hiring/deployment/preflight cap denial uses current mode, REBIRTH no perk promise, Base Game retains perk hint, limits unchanged. No game validation.'
