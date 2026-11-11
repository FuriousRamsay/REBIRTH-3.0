[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SaveDirectory,
    [Parameter(Mandatory=$true)][ValidateSet('BaseGame','Rebirth')][string]$Progression,
    [ValidateSet('Navezgane','Pregen06k01','Pregen06k02','Pregen08k01','Pregen08k02','West Xuyofu Territory')][string]$World='Navezgane',
    [switch]$RequirePurge,
    [string]$FixtureManifest=(Join-Path $PSScriptRoot 'combine-fixtures.json')
)
$ErrorActionPreference='Stop'
$save=(Resolve-Path -LiteralPath $SaveDirectory).Path
$name=Split-Path -Leaf $save
if ($name -notmatch '^Codex[A-Za-z0-9_-]+$') { throw 'Use the exact prepared disposable Codex save.' }
if ((Split-Path -Leaf (Split-Path -Parent $save)) -ne $World) { throw 'Native save parent did not match the installed world fixture.' }
$optionsFile=Join-Path $save 'RebirthSandboxOptions.xml'
$recipesFile=Join-Path $save 'ConfigsDump/recipes.xml'
[xml]$options=Get-Content -LiteralPath $optionsFile -Raw
$code=$options.DocumentElement.GetAttribute('code')
if ($code -cnotmatch '^RBX(?:[A-Z]{3})*$') { throw 'This fixture requires an actual current RBX authoritative saved snapshot.' }
$entries=@(
    for ($i=3;$i+2 -lt $code.Length;$i+=3) { $code.Substring($i,3) }
)
$progressionOptions=@($entries|Where-Object { $_.StartsWith('CJ',[StringComparison]::Ordinal) })
$themeOptions=@($entries|Where-Object { $_.StartsWith('CT',[StringComparison]::Ordinal) })
if ($progressionOptions.Count -gt 1 -or $themeOptions.Count -gt 1 -or
    @($progressionOptions|Where-Object {$_ -cnotin @('CJA','CJB')}).Count -or
    @($themeOptions|Where-Object {$_ -cnotin @('CTA','CTB')}).Count) { throw 'Invalid or ambiguous native progression/theme snapshot.' }
$actual=if ($progressionOptions -ccontains 'CJA') { 'BaseGame' } else { 'Rebirth' }
$purge=$themeOptions -ccontains 'CTB'
if ($actual -cne $Progression -or ($RequirePurge -and !$purge)) { throw 'Saved progression or Purge theme did not match the fixture.' }
[xml]$recipes=Get-Content -LiteralPath $recipesFile -Raw
$rows=@($recipes.SelectNodes('/recipes/recipe[@name="combineStation"]'))
$expectedCount=if ($actual -eq 'BaseGame') {1} else {0}
if ($rows.Count -ne $expectedCount) { throw "Wrong effective stock recipe count. Expected $expectedCount, observed $($rows.Count)." }
if ($expectedCount) {
    $recipe=$rows[0]
    $iron=@($recipe.SelectNodes('ingredient[@name="resourceScrapIron"]'))
    $wood=@($recipe.SelectNodes('ingredient[@name="resourceWood"]'))
    if ($recipe.GetAttribute('count') -ne '1' -or $recipe.HasAttribute('craft_area') -or
        @($recipe.SelectNodes('ingredient')).Count -ne 2 -or $iron.Count -ne 1 -or $wood.Count -ne 1 -or
        $iron[0].GetAttribute('count') -ne '50' -or $wood[0].GetAttribute('count') -ne '100') {
        throw 'Base effective recipe was present but did not match the exact installed stock definition.'
    }
}
$fixture=(Get-Content -LiteralPath $FixtureManifest -Raw|ConvertFrom-Json).worlds.$World
if (!$fixture.targets -or !$fixture.prefabsXmlSha256) { throw 'Expected authored station manifest for this installed world.' }
[pscustomobject]@{
    status='PREFLIGHT';saveName=$name;world=$World;playerProgression=$actual;purge=$purge
    effectiveRecipeCount=$rows.Count;effectiveRecipeSha256=(Get-FileHash -LiteralPath $recipesFile).Hash
    snapshotSha256=(Get-FileHash -LiteralPath $optionsFile).Hash;prefabsXmlSha256=$fixture.prefabsXmlSha256
    expectedFreshNativeParent='air'
    targets=$fixture.targets
} | ConvertTo-Json -Depth 8
