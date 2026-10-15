# NPC/Survivor behavioral XML closure — 2026-10-15

Audit only. Config/_NPC/dogs.xml214 and dog_blocks.xml221 read fully literally, including every breed/stat/property/token; Config/npc.xml6 also complete. Prior DOG_RUNTIME_REMAINDER claimed parent XML evidence without an explicit local full-read manifest, so this records current actual reads rather than relying on that claim. Root entityclasses/blocks includes, root buffs dog nodes, hand item, native healing buffs, loot/nav/dialog nodes are scoped literal reads only. No whole large root XML credit, no build/test/game/production change. Prior NPC_SPAWN full _NPC/entityclasses172 and Survivor companions full tamed_wild_animals192/special_panthers22 are reused as provenance, not new reads.

## Active include and class chain

Root Config/entityclasses.xml includes _Fire first, _NPC/entityclasses then _NPC/dogs before later zombie edits. Root blocks includes GameBridge/Fire/Farming/BlockPickup/LootConversions/Crowbar/Trader/bed economy, then _NPC/dog_blocks before Vehicles and later station changes. This explicitly reaches ordinary dog content; do not generalize to tamed_wild_animals/special_panthers, whose missing include qualification remains prior trace. Root buffs chooses _Rebirth or _Base by character_progression, includes Fire, then appends ordinary dog buffs unconditionally. _Rebirth/buffs includes Survivor starter legacy buffs; Base wrapper is empty. No later current Config override of medicalRegHealthAmount/medRegHealthIncSpeed or .NoHealingManager was located. Final installed merged table and native XML loader/include execution were not observed in a running game.

FuriousRamsayNPCDog extends native animalTemplateHostile with Class EntityRebirthDogCompanion, RebirthUtils and profile companion.dog, whiteriver faction, animal/ally/dog/NPC tags, native Standard mesh/AvatarAnimalController/FuriousRamsayAnimalDog physics. It advertises one-meat hire and companion NPCID, but actual modern acquisition/cap/ownership is reviewed C# service, not an XML permission. LootListAlive is rebirthDogCompanionStorage and death list empty. Native ApproachAndAttackTarget executes selected target; AITarget slots are empty, with custom server runtime owning selection. Hand item is FuriousRamsayMeleeHandDog. Base max health500/regen.5/resist10+20/landmine immunity; kill event adds $varNumKills, consumed by five-second authoritative progression reconciliation.

All eight exact children read: german_shepherd, pitbull, labrador, husky, golden_retriever, doberman, dalmatian, bull_terrier. Each supplies exact breed ID/localization/icon, source mesh, SpawnBlock and RootMotion true. Shepherd and BullTerrier explicitly expose Menu; others inherit None. Shared level1–10 HealthMax additions20..200 and DamageModifier .1,.2,.4,.8,1.2,1.6,2,2.4,2.8,3.2 depend on server projected $FR_NPC_Level. Difficulty0–5 health percentages-.5,-.25,0,.5,1,1.5 and damage.2,.1,0,-.1,-.2,-.3 depend on _difficulty. Literal tables were fully read, not inferred from array count. Native effect evaluation, loaded model asset contents and effective inherited base-game stats remain qualification boundaries.

## Deployment token and custody bindings

Dog block base extends woodNoUpgradeMaster, model/collide sight/no stability/fall drop0. Eight fresh token definitions explicitly use RebirthDogDeployBlock, breed ID, preview mesh, 1x1x2, simple rotations, stack50, Creative Player and not sellable. Config legacy ec/buff/pc/leader strings remain compatibility metadata; actual PlaceBlock uses breed registry and authenticated server request, not evaluation of those strings. Eight picked-up tokens extend corresponding fresh token and change Creative None/stack1, retaining exact class/breed/preview. This matches source exact placed-name/breed checks and durable capsule nonce validation. XML does not implement idempotence, owner debit or migration; those are source/native contracts recorded in NPC_DOG_COMPLETE.

Root hand item extends meleeHandAnimalWolf and authors HoldType35, Action0 sphere1/delay2/range2/entity30/block60. Its native action class is inherited, not explicitly REBIRTH custom melee. Exact inherited melee hand/effect merge was not newly full-read. Loot node count0/5x2/open_time0/destroy_on_close false/cobweb0 is an empty native ten-slot shell; canonical persistence remains DogInventoryService, with storage lock/mirror methods separately reviewed. No XML owner receipt or remote inventory settlement should be inferred from shell size.

## Exact healing, pickup and stat restoration behavior

Root buffs83–149 were read literally. RawMeatHealTrigger ignore-stack/.1sec adds medicalRegHealthAmount45, sets medRegHealthIncSpeed3, adds buffProcessConsumables and removes bleeding. C# TickHealing checks deficit-minus-reserve>=140 and absence buffHealHealth, consumes one canonical meat then sets different legacy $medicRegHealthIncreaseSpeed4 before adding this trigger. That legacy key does not drive the native buff read here. Native Data/Config/buffs8496–8600 full two-buff structures show buffProcessConsumables adds buffHealHealth for positive reserve; buffHealHealth update_rate1, while .NoHealingManager=0, adds @medRegHealthIncSpeed and subtracts reserve2 each update. Display uses reserve/2*speed, so45 reserve is nominal67.5 healing units (discrete23 positive-reserve updates can add69 before cleanup), not a declared140 HP treatment. Native food-health .5 branch and health clamp may also affect actual total. No running-game healing quantity measured; this is authored/source mismatch, not authorization to rebalance historical dog design.

RebirthDogBedrollHeal is a separate2sec HealthChangeOT.25 buff, but runtime awaiting-respawn path actually adds buffBedrollAOEEffect: replace1sec/onStart Health+3, gated by current bed proximity in C#. AdjustNPCStats ignore3sec sets Health=@$tempHealth onFinish. This explains source ordering requirement to seed tempHealth before adding the buff; ghost repair now does so. delayPickup ignore3sec sets $delayedPickup1 onStart and0 onFinish; lifecycle rejects while buff or var active. StandStill2sec persists through death; pause/resume effects remove opposite buff; persistent aggregate combat axes are authority, empty mode buffs are compatibility projection.

Five resistance buffs and legacy effect-marker buffs are literal-read scoped nodes; resistance200 tags map fire/shock/heat combinations while marker buffs themselves contain no executable damage effect. Applying them depends on separately reviewed resistance service/canonical inventory, not a user-controlled XML ownership grant.

## Interaction and owner-local presentation nodes

Entire npc.xml defines companion identity with whiteriver/Like/empty voice/trader100/dialog FuriousRamsayCompanion. dialogs107–122 defines start Follow/Stay/Respawn/Pickup/Inventory/AdminRemove/Rename/Stats; GuardHere exists but is deliberately omitted from start. Requirements use exact DialogRequirementRebirthDogOwner/Lifecycle/PickupReady/OrderNot/InventoryEnabled/Admin classes. Action dispatcher in previously full DogDialogCompat routes Follow/Stay/Guard/Pickup/Report through CompanionService requests, Inventory/Stats/Rename to owner UI; AdminRemove direct local service requires authoritative world/admin. Hide requirements are presentation; services enforce authority. Remote dialog AdminRemove direct call cannot itself satisfy authoritative world, a limitation already distinct from packet authorization.

Nav class FuriousRamsayCompanionNav has unlimited map/compass100/clamped/distance dog sprites and no InParty requirement; owner-local NavigationMarkerService supplies visibility. Panther's distinct nav InParty requirement is not applied to dogs. Physics initial collider excerpt confirms named body/bone/tag bindings but whole physics file/assets were not fully reviewed. No visual alignment or mesh/runtime validation claimed.

## Hashes and exact coverage labels

Hashes identify current containing files; scoped hashes do not imply full-body coverage.

```csv
file,sha256,coverageMethod
Config/_NPC/dogs.xml,EEE8134DDB41095F086CAC456C0A68B41DC878AF49213794231B001361DBDD5D,FULL_LITERAL_XML_214_LINES
Config/_NPC/dog_blocks.xml,6DBDD349C99C51530CC37C3A629EE6CAF4282EB2F1E7EA982B73C528214591DB,FULL_LITERAL_XML_221_LINES
Config/npc.xml,D14566BA174CA145B8E35090592C28248537314872C9147B51A30C8218537BE3,FULL_LITERAL_XML_6_LINES
Config/buffs.xml,EF517DE6ED46BED20879DA5B8D9D183A5BBEDB44290ADCF771B6BAA599F397AF,SCOPED_LITERAL_LINES_1_153_DOG_NODES_83_149
Config/items.xml,61E45859B9B9CB895AD9DDAF374071FEEF8BA8EC0D7E71EA2AD0FF35D0C50581,SCOPED_LITERAL_HAND_ITEM_102_AND_FOLLOWING_INCLUDES
Config/loot.xml,27B536438F2BACDC3CAE04BC9049D1463BABAB67C82C96E3146ED25D795D5BC9,SCOPED_LITERAL_DOG_STORAGE_NODE_103
Config/nav_objects.xml,EAFCD2E2A65FC32F7718F9F3F360FCD266617FA0AA4E48BE5EB7ECA2B6673BAB,SCOPED_LITERAL_DOG_NAV_NODE_86
Config/dialogs.xml,7C387D902ACACDD7CC68CCC5351D21E6298642F70C32CFD64878B779FE411E8D,SCOPED_LITERAL_COMPANION_DIALOG_107_TO_END
Config/blocks.xml,40FC94CDF4D7A2FB99E13455156AE7BF8A0218BD31253FF9028ECD1A9309B7BD,SCOPED_LITERAL_INITIAL_INCLUDE_ORDER
Config/entityclasses.xml,87E3FD5F54DD02CEAF981E5F188A72993720C6E63A38CBE1E9181C437DF0308B,SCOPED_LITERAL_INITIAL_INCLUDE_ORDER
../../Data/Config/buffs.xml,08345E0D43AA76F542FC55E3055855E51BFCCD0E59D698DDF54F6D3D6886824A,SCOPED_LITERAL_NATIVE_PROCESS_CONSUMABLES_AND_HEAL_HEALTH_8496_8600
```
