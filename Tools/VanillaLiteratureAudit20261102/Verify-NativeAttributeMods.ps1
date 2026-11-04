param([Parameter(Mandatory=$true)][string]$Dump,[Parameter(Mandatory=$true)][string]$Report)
$ErrorActionPreference='Stop'
$chips=@('modArmorPerception','modArmorStrength','modArmorFortitude','modArmorAgility','modArmorIntellect')
$rows=@()
foreach($category in @('loot','traders','quests','items')){
 $path=Join-Path $Dump ($category+'.xml');[xml]$doc=Get-Content -LiteralPath $path
 $bad=@()
 if($category -eq 'quests'){$bad=@($doc.SelectNodes('//reward[@id]')|Where-Object {$chips -contains $_.id})}
 elseif($category -eq 'items'){$bad=@($doc.SelectNodes('//property[@name="Create_item" or @name="Random_item"]')|Where-Object {$tokens=@($_.value -split ',');@($tokens|Where-Object {$chips -contains $_}).Count -gt 0})}
 else{$bad=@($doc.SelectNodes('//item[@name]')|Where-Object {$chips -contains $_.name})}
 if($bad.Count){throw "Native $category still has $($bad.Count) attribute-chip routes"}
 $rows+=@{category=$category;forbidden=0;SHA256=(Get-FileHash -LiteralPath $path).Hash}
}
@{result='PASS_NATIVE_CONFIG';dump=$Dump;checks=$rows;scope='Actual regenerated native configuration, no transforms; owned existing items preserved.'}|ConvertTo-Json -Depth 7|Set-Content -LiteralPath $Report
Get-Content -LiteralPath $Report -Raw
