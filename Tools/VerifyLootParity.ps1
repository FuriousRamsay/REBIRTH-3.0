$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
[xml]$loot=Get-Content -LiteralPath "$root\..\..\Data\Config\loot.xml" -Raw
$seen=@{}
function Add-Declarations([string]$path) {
    $path=[IO.Path]::GetFullPath($path)
    if($seen.ContainsKey($path)){return}
    $seen[$path]=$true
    [xml]$document=Get-Content -LiteralPath $path -Raw
    foreach($node in $document.SelectNodes('//lootcontainer')) {
        if(!$loot.SelectSingleNode("/lootcontainers/lootcontainer[@name='$($node.name)']")) {
            $null=$loot.DocumentElement.AppendChild($loot.ImportNode($node,$true))
        }
    }
    foreach($include in $document.SelectNodes('//include[@filename]')) {Add-Declarations (Join-Path (Split-Path -Parent $path) $include.filename)}
}
Add-Declarations "$root\Config\loot.xml"
[xml]$patch=Get-Content -LiteralPath "$root\Config\_Survivor\loot_sizes_26.xml" -Raw
$checked=0
foreach($op in $patch.configs.setattribute) {
    $targets=@($loot.SelectNodes($op.xpath))
    if($targets.Count -ne 1){throw "Target not unique: $($op.xpath)"}
    $parts=$op.InnerText.Split(',')
    if($parts.Count -ne 2 -or [int]$parts[0] -lt 1 -or [int]$parts[1] -lt 1){throw 'Invalid dimensions'}
    $targets[0].SetAttribute('size',$op.InnerText);$checked++
}
foreach($expected in @(@{Name='dumpster';Size='8,6'},@{Name='reinforcedChestT1';Size='6,5'},@{Name='rebirthBrownPalletLoot';Size='6,3'},@{Name='rebirthWineBarrelJunk';Size='6,3'})) {
    if($loot.SelectSingleNode("/lootcontainers/lootcontainer[@name='$($expected.Name)']").size -ne $expected.Size){throw "Parity failed: $($expected.Name)"}
}
"PASS $checked unique native/current-custom targets and valid dimensions; dumpster48/T1crate30/brown18/decorative-wine18. Declaration+size replay only; no game/save/load test."
