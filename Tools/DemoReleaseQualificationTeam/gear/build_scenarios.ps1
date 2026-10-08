# Offline generator. Produces executable GameBridge JSON; never contacts game.
$dest = $PSScriptRoot
function A($command,$parameters) { return @{action=@{command=$command;args=$parameters}} }
function SaveScenario($name,$note,$steps) {
 $data=@{name=$name;stopOnFailure=$true;steps=@(@{_note=$note})+$steps+@(@{expectNoErrors=$true})}
 $data | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $dest "$name.json") -Encoding utf8
}
$pre='UNEXECUTED. Explicit human game authorization. Disposable CodexTest only, completed Survivor, empty cursor, native window already open, visible source/destination, cleararea radius40 then restore, guard remains ON. Never overwrite items, delete gear or reset saves. '
foreach($pair in @(@('rebirthGearDaypack','Backpack'),@('rebirthGearUtilityBelt','Belt'))) {
 $item=$pair[0];$slot=$pair[1]
 SaveScenario "gear_$slot`_shift_roundtrip" ($pre+'Corresponding equipped slot empty; exactly one carried test gear; attribute eligibility satisfied; room to return. Input success and inventory delta are insufficient for full equip/custody proof: capture actual inventory state before/equipped/returned and use snapshot verifier. Failure leaves custody untouched for operator recovery.') @(
 @{expectWindowOpen='rebirthSurvivorCharacter'},@{markItem=$item},
 (A 'click' @{item=$item;shift=1}),@{wait=3},
 @{expectItemDelta=@{name=$item;op='==';value=-1}},
 (A 'uitree' @{window='rebirthSurvivorCharacter'}),@{screenshot="$slot`_equipped"},
 (A 'click' @{id="survivorGearInput$slot";shift=1}),@{wait=3},
 @{expectItemDelta=@{name=$item;op='==';value=0}},
 (A 'uitree' @{window='rebirthSurvivorCharacter'}),@{screenshot="$slot`_returned"})
}
foreach($pair in @(@('theory','rebirthBackpackLibrary','noteDuke01',6),@('sell','rebirthBackpackSellStash','resourceWood',10))) {
 $p=$pair[0];$window=$pair[1];$item=$pair[2];$cap=$pair[3]
 SaveScenario "$p`_deposit_withdraw" ($pre+'Daypack equipped, section empty, exactly one test item in backpack slot0 and no other matching stack. Section opened by native toolbar. No fixture grants or slot overwrites. Theory approved6 per tier, Sale10 per tier. No study/consumption. Deposit then withdraw exact one item; snapshot metadata checks remain external.') @(
 @{expectWindowOpen=$window},@{markItem=$item},
 @{expectUi=@{id="$p`Capacity";text="0 / $cap";window=$window}},
 (A 'click' @{id="$p`Available0"}),(A 'click' @{id="$p`Deposit"}),@{wait=4},
 @{expectItemDelta=@{name=$item;op='==';value=-1}},
 @{expectUi=@{id="$p`Capacity";text="1 / $cap";window=$window}},
 @{screenshot="$p`_stored"},(A 'click' @{id="$p`Choose0"}),(A 'click' @{id="$p`Withdraw"}),@{wait=4},
 @{expectItemDelta=@{name=$item;op='==';value=0}},
 @{expectUi=@{id="$p`Capacity";text="0 / $cap";window=$window}},
 @{screenshot="$p`_returned"},(A 'click' @{id="$p`Close"}))
}
SaveScenario 'theory_reject_nonlearning' ($pre+'Empty Theory, Daypack equipped, one resourceWood in bag slot0; native section open. Checks refused deposit and stable count, requires no localized refusal text.') @(
 @{expectWindowOpen='rebirthBackpackLibrary'},@{markItem='resourceWood'},
 (A 'click' @{id='theoryAvailable0'}),(A 'click' @{id='theoryDeposit'}),@{wait=3},
 @{expectItemDelta=@{name='resourceWood';op='==';value=0}},
 @{expectUi=@{id='theoryCapacity';text='0 / 6';window='rebirthBackpackLibrary'}},
 @{screenshot='theory_refused_wood'},(A 'click' @{id='theoryClose'}))

# Additional native routes using the same offline generator helpers.
foreach($pair in @(@('rebirthGearDaypack','Backpack'),@('rebirthGearUtilityBelt','Belt'))) {
 $item=$pair[0];$slot=$pair[1]
 SaveScenario "gear_$slot`_drag_roundtrip" ($pre+'Gear slot empty, exactly one matching item in bag slot0, slot0 visible in Character. Gear requirements met; no other items move into slot0 during test. Snapshot verifier required for equip identity/capacity; serializer evidence still pending.') @(
 @{expectWindowOpen='rebirthSurvivorCharacter'},@{markItem=$item},
 (A 'drag' @{'from.item'=$item;'to.id'="survivorGearInput$slot"}),@{wait=3},
 @{expectItemDelta=@{name=$item;op='==';value=-1}},(A 'uitree' @{window='rebirthSurvivorCharacter'}),
 (A 'drag' @{'from.id'="survivorGearInput$slot";'to.id'='characterBagSlot0'}),@{wait=3},
 @{expectItemDelta=@{name=$item;op='==';value=0}},(A 'uitree' @{window='rebirthSurvivorCharacter'}))
 SaveScenario "gear_$slot`_inventory_equip" ($pre+'Native inventory crafting window open; one carried matching gear, gear slot empty, eligibility met. Click source then explicit localized English Equip. This qualifies UI cleanup route only when paired with native captured-state identity/capacity/metadata verification. Do not count -1 alone as success; item remains equipped after scenario.') @(
 @{markItem=$item},(A 'click' @{item=$item}),@{expectUi=@{text='Equip'}},
 (A 'click' @{text='Equip'}),@{wait=4},
 @{expectItemDelta=@{name=$item;op='==';value=-1}},(A 'uitree' @{window='rebirthSurvivorCharacter'}),
 @{screenshot="$slot`_inventory_equip"})
}

