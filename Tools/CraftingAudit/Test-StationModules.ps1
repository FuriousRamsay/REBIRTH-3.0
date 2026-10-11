$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
[xml]$doc=Get-Content "$root/../../Data/Config/blocks.xml" -Raw
[xml]$patch=Get-Content "$root/Config/_Workstations/blocks.xml" -Raw
foreach($op in $patch.DocumentElement.ChildNodes){
 if($op.NodeType -ne 'Element'){continue}
 $nodes=@($doc.SelectNodes($op.GetAttribute('xpath')))
 switch($op.Name){
 'remove' {foreach($n in $nodes){$null=$n.ParentNode.RemoveChild($n)}}
 'append' {foreach($n in $nodes){foreach($child in $op.ChildNodes){$null=$n.AppendChild($doc.ImportNode($child,$true))}}}
 'set' {foreach($n in $nodes){$n.InnerText=$op.InnerText}}
 default {throw "Unsupported operation $($op.Name)"}
 }
}
$rows=foreach($b in $doc.SelectNodes('/blocks/block[property[@class="Workstation"]]')){
 $modules=$b.SelectSingleNode('property[@class="Workstation"]/property[@name="Modules"]')
 if($modules){[pscustomobject]@{Station=$b.GetAttribute('name');Modules=$modules.GetAttribute('value')}}
}
$rows|Export-Csv "$root/_Documentation/CraftingAudit_20261010/STATION_MODULE_COMPOSITION.csv" -NoTypeInformation
foreach($name in @('workbench','cementMixer','chemistryStation','WorkbenchResearchTable001_FR','SonjaAmmoRecyclerStation')){
 $row=$rows|Where-Object Station -eq $name
 if(!$row -or 'tools' -notin $row.Modules.Split(',')){throw "Missing tools: $name"}
 "PASS $name $($row.Modules)"
}
'Coverage: native blocks plus final Workstations patch, not all root config branches.'
