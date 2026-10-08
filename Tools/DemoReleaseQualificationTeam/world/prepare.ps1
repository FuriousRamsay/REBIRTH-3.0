$ErrorActionPreference='Stop'
$out=Join-Path $PSScriptRoot 'scenarios'
New-Item -ItemType Directory -Force $out | Out-Null
function WriteScenario($file,$name,$steps){[ordered]@{name=$name;stopOnFailure=$true;steps=$steps}|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $out "$file.json") -Encoding utf8}
function Act($cmd,$arg){@{action=@{command=$cmd;args=$arg}}}
$note='PREPARED, NOT RUN. Only after explicit native authorization, disposable TestProfile4 baseline, guard ON, windows closed, free inventory, clear level ground. OPERATOR MUST VERIFY toolbelt slot1 EMPTY before give; move any prior exact stack through native inventory and record custody, never overwrite. Fixture22,45,1019 must be preverified disposable and empty: setblock overwrites it. No cleanup removes live items. Retain failed fixture for diagnosis; reclaim through real input. Never use real saves. Multiplayer execution requires independently prepared host/peer fixtures.'
$prep=@(@{_note=$note},(Act 'cleararea' @{radius=40}),(Act 'restore' @{}),@{surroundings=@{}})
$families=@('Hero','Shamway','LabEquipment','Bookstore','CarParts','ShotgunMessiah','WorkingStiffs','SavageCountry','MoPowerElectronics')
foreach($f in $families){
 $steps=$prep+@((Act 'setblock' @{x=22;y=45;z=1019;name="cntShippingCrate$f"}),(Act 'give' @{item='ItemsWeaponsCrowbar001_FR';quality=1;toolbelt=1}),(Act 'select' @{slot=1}),(Act 'walkto' @{x=22.5;z=1017.3;radius=0.4}),@{surroundings=@{}},(Act 'lookat' @{x=22;y=45;z=1019;block=1}),@{expectTarget="cntShippingCrate$f"},(Act 'press' @{action='Primary';seconds=0.12}),@{wait=1.2},(Act 'lookat' @{x=22;y=45;z=1019;block=1}),@{expectTarget="cntLootCrate$f"},@{screenshot="crowbar_$f"},@{expectNoErrors=$true})
 WriteScenario "crowbar_$f" "Crowbar $f native downgrade postimage" $steps
}
$plots=@('farmPlotBlock','farmPlotBlockRaised','farmPlotBlockCornerRound','farmPlotBlockPlayer','farmPlotBlockPlayerRaised','farmPlotBlockPlayerCornerRound')
foreach($p in $plots){
 $steps=$prep+@((Act 'setblock' @{x=22;y=45;z=1019;name=$p}),(Act 'give' @{item='meleeToolShovelT2SteelShovel';quality=6;toolbelt=1}),(Act 'select' @{slot=1}),(Act 'walkto' @{x=22.5;z=1017.3;radius=0.4}),@{surroundings=@{}},(Act 'lookat' @{x=22;y=45;z=1019;block=1}),@{expectTarget=$p},@{markItem='farmPlotBlockVariantHelper'})
 for($i=0;$i -lt 12;$i++){$steps+=@((Act 'press' @{action='Primary';seconds=0.12}),@{wait=1.0})}
 $steps+=@(@{expectItemDelta=@{name='farmPlotBlockVariantHelper';op='==';value=1}},@{screenshot="plot_$p"},@{expectNoErrors=$true})
 WriteScenario "plot_$p" "Native digging $p returns exactly one usable plot selector" $steps
}
foreach($mode in @('melee','ranged')){
 $s=Get-Content (Join-Path $PSScriptRoot "../../GameBridge/tests/combat_$mode.json") -Raw|ConvertFrom-Json
 $steps=@(@{_note='PREPARED NOT RUN. Disposable flat area, guard ON. OPERATOR MUST VERIFY toolbelt slot1 EMPTY for melee and slot2 EMPTY for ranged before give; reclaim existing exact items through native inventory and record custody. This is combat outcome regression only; it does NOT assert bleed or animator stand-up. Inspect fight trace, kill credit and synchronized remote animation separately.'})+@($s.steps|Where-Object {-not $_._note})
 $steps+=@(@{expectLivingEntities=@{contains='zombie';radius=40;op='==';value=0}},@{expectNoErrors=$true})
 WriteScenario "combat_$mode" "Native $mode combat survival and no nearby living zombie" $steps
}
$steps=$prep+@((Act 'setblock' @{x=22;y=45;z=1019;name='plantedPotato3Harvest'}),(Act 'give' @{item='meleeToolAxeT1StoneAxe';quality=1;toolbelt=1}),(Act 'select' @{slot=1}),(Act 'walkto' @{x=22.5;z=1017.3;radius=0.4}),@{surroundings=@{}},(Act 'lookat' @{x=22;y=45;z=1019;block=1}),@{expectTarget='plantedPotato3Harvest'},@{markItem='foodCropPotato'},(Act 'press' @{action='Primary';seconds=3}),@{expectItemDelta=@{name='foodCropPotato';op='>';value=0}},@{screenshot='unknown_origin_potato_harvest'},@{expectNoErrors=$true})
WriteScenario 'unknown_origin_potato_harvest' 'Unknown-origin mature potato remains harvestable; origin/UI manual evidence pending' $steps
foreach($dish in @(@{key='water';item='drinkJarBoiledWater';text='Boiled Water';category='filter2'},@{key='redtea';item='drinkJarRedTea';text='Red Tea';category='filter2'},@{key='potato';item='foodBakedPotato';text='Baked Potato';category='filter1'})){
 $steps=@(@{_note='PREPARED NOT RUN. Disposable TestProfile4 campfire14,45,995. OPERATOR PRECONDITIONS: empty queue/grid, known exact recipe, matching cooking pot/grill installed, fuel ON with enough time, exactly one full ingredient set in backpack, batch ONE selected. Empty input grid has no leftover or surplus stacks; do not preload ingredients. No substitutions. Resolved recipe quantities must equal authored one-per-input below; record UI before running if runtime modifiers differ. No synthetic unlock/progression counted. Record pre/post full inventory and grid metadata for custody. Screenshot manual review required. Existing stack counts preserved using output baseline.'},(Act 'cleararea' @{radius=40}),(Act 'restore' @{}),@{surroundings=@{}},@{markItem=$dish.item},(Act 'walkto' @{x=14.5;z=993.3;radius=0.7}),@{surroundings=@{}},(Act 'activate' @{x=14;y=45;z=995;block=1}),@{expectWindowOpen='workstation_campfire'},(Act 'click' @{id=$dish.category;window='workstation_campfire'}),@{expectUi=@{text=$dish.text;window='workstation_campfire'}},(Act 'click' @{text=$dish.text;window='workstation_campfire'}),(Act 'click' @{id='pullIngredients';window='workstation_campfire'}),@{screenshot="campfire_$($dish.key)_ready"},(Act 'click' @{id='cook';window='workstation_campfire'}),@{waitForUi=@{text='READY TO TAKE';window='workstation_campfire';timeout=120}},(Act 'click' @{id='rebirthCookingTake';window='workstation_campfire'}),(Act 'key' @{name='Escape'}),@{expectItemDelta=@{name=$dish.item;op='==';value=1}},@{expectNoErrors=$true})
 $inputs=switch($dish.key){'water'{@('drinkJarRiverWater')};'redtea'{@('resourceCropChrysanthemumPlant','drinkJarBoiledWater')};'potato'{@('foodCropPotato')}}
 $marks=@($inputs|ForEach-Object{@{markItem=$_}})
 $debits=@($inputs|ForEach-Object{@{expectItemDelta=@{name=$_;op='==';value=-1}}})
 # Baselines after restore and before pull; final carried debit after Take and Escape includes transfers.
 $steps=@($steps[0..4])+$marks+@($steps[5..($steps.Count-1)])
 $steps=@($steps[0..($steps.Count-2)])+$debits+@($steps[-1])
 WriteScenario "campfire_$($dish.key)" "Campfire $($dish.text) category selection cook and exactly one output" $steps
}
Get-ChildItem $out -Filter '*.json'|ForEach-Object{$j=Get-Content $_.FullName -Raw|ConvertFrom-Json;if(-not $j.steps){throw 'Empty scenario'};foreach($s in $j.steps){if(@($s.PSObject.Properties).Count -ne 1){throw 'Invalid step'}}}
Write-Output 'Prepared21 JSON scenarios; parsed only, no bridge execution.'




