param([string]$NativeConfig='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Config')
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$count=0
function Check($ok,$label){if(-not $ok){throw "FAIL $label"};$script:count++;Write-Output "PASS $label"}
[xml]$base=Get-Content -LiteralPath "$NativeConfig/blocks.xml" -Raw
[xml]$materials=Get-Content -LiteralPath "$NativeConfig/materials.xml" -Raw
[xml]$patch=Get-Content -LiteralPath "$root/Config/_Crowbar/blocks.xml" -Raw
[xml]$items=Get-Content -LiteralPath "$root/Config/_Survivor/starter_legacy_items.xml" -Raw
[xml]$entry=Get-Content -LiteralPath "$root/Config/blocks.xml" -Raw
[xml]$conversion=Get-Content -LiteralPath "$root/Config/_LootConversions/blocks.xml" -Raw
$blocks=@{};foreach($b in $base.blocks.block){$blocks[$b.name]=$b}
function Props($name){$b=$blocks[$name];$p=@{};$extends=$b.SelectSingleNode("property[@name='Extends']");if($extends){$parent=Props $extends.value;foreach($k in $parent.Keys){if($k -notin ($extends.param1 -split ',')){$p[$k]=$parent[$k]}}};foreach($v in $b.SelectNodes('property[@name]')){if($v.name -ne 'Extends'){$p[$v.name]=$v.value}};return $p}
$family=@('cntShippingCrateHero','cntShippingCrateShamway','cntShippingCrateLabEquipment','cntShippingCrateBookstore','cntShippingCrateCarParts','cntShippingCrateShotgunMessiah','cntShippingCrateWorkingStiffs','cntShippingCrateSavageCountry','cntShippingCrateMoPowerElectronics','cntShippingCrateConstructionSupplies')
$tagged=@{}
foreach($a in $patch.configs.append){foreach($b in $base.SelectNodes($a.xpath)){foreach($p in $a.property){$tagged[$b.name]=[string]$p.value}}}
function Tags($name){if($tagged.ContainsKey($name)){return $tagged[$name]};$e=$blocks[$name].SelectSingleNode("property[@name='Extends']");if($e -and 'Tags' -notin ($e.param1 -split ',')){return Tags $e.value};return (Props $name)['Tags']}
foreach($name in $family){$p=Props $name;Check ($p.Material -eq 'Mwood_regular') "$name wood material";Check (-not $p.ContainsKey('Stage2Health')) "$name no stage2";Check (-not $p.ContainsKey('PassThroughDamage')) "$name no overflow pass-through";Check ((Tags $name) -eq $(if($name -eq 'cntShippingCrateConstructionSupplies'){''}else{'rbCrowbarCover'})) "$name precise tag scope"}
foreach($b in $base.blocks.block){if($b.name -like 'cntLootCrate*'){Check ((Tags $b.name) -notmatch 'rbCrowbarCover') "$($b.name) opened loot excluded"}}
Check ($conversion.SelectNodes("//append[contains(@xpath,'cntShippingCrateConstructionSupplies')]/property[@name='Class' and @value='CompositeTileEntity']").Count -eq 1) 'actual construction storage conversion acknowledged'
$wood=$materials.SelectSingleNode("/materials/material[@id='Mwood_regular']/property[@name='MaxDamage']")
Check ([int]$wood.value -eq 225) 'actual native cover maximum damage225'
$item=$items.SelectSingleNode("//item[@name='ItemsWeaponsCrowbar001_FR']")
$effects=$item.SelectNodes("effect_group/passive_effect[@name='BlockDamage']")
Check ($effects.Where({$_.operation -eq 'base_set' -and $_.value -eq '11'}).Count -eq 1) 'existing base damage11 retained'
foreach($pair in @(@('safes,hardenedSafe,buriedTreasure,timecharge','1000'),@('police','250'),@('rbCrowbarCover','250'))){Check ($effects.Where({$_.operation -eq 'base_add' -and $_.tags -eq $pair[0] -and $_.value -eq $pair[1]}).Count -eq 1) "one exact bonus $($pair[0])"}
Check ((11+250) -gt [int]$wood.value) 'finite cover bonus exceeds default native cover health'
Check ($entry.configs.include.Where({$_.filename -eq '_Crowbar/blocks.xml'}).Count -eq 1) 'single include'
$includes=@($entry.configs.include.filename);Check ([array]::IndexOf($includes,'_Crowbar/blocks.xml') -eq ([array]::IndexOf($includes,'_LootConversions/blocks.xml')+1)) 'cover exclusion follows storage conversion'
$native=Get-Content "$PSScriptRoot/native/Block.cs" -Raw
Check ($native.Contains('(num3 > 0 && EnablePassThroughDamage) || _bBypassMaxDamage')) 'actual native overflow gated'
Check ($native.Contains('public bool EnablePassThroughDamage;')) 'native passthrough default false'
Check ($native.Contains('DestroyedResult destroyedResult = OnBlockDestroyedBy(')) 'original destruction callback remains authority'
$attack=Get-Content "$PSScriptRoot/native/ItemActionAttack.cs" -Raw
Check ($attack.Contains('EffectManager.GetValue(PassiveEffects.BlockDamage, _itemValue, damageBlock')) 'actual source item effect query'
Check ($attack.Contains('_blockValue.Block.Tags')) 'actual target block tag query'
Write-Output "$count PASS; static actual XML/native source qualification only; no gameplay assertion"
