# Survivor DPS, native progression patches and service-craft closure

2026-10-15 audit only. No production edits/build/tests/game/gameplay commands/git/shared report/ledger changes. All3 requested current files fully read in bounded contiguous ranges. Existing companion/crafting/medical/TheorySolo and core award findings remain attributed to their traces; this closes containing-file coverage rather than promoting every delegated call as verified.

## Full-read manifest

| Source | Lines | SHA256 |
|---|---:|---|
|Scripts/Survivor/Progression/RebirthWeaponSustainedDpsService.cs|1626|10F2329B8D3761A70B8B0849ABA7A0A3BC5938D40E3D2A02571F5E39E9A3D00C|
|Scripts/Survivor/Progression/RebirthSurvivorProgressionPatches.cs|599|8CD9C5C1D43268243BB9464B2A75E8D73C8EE8EEBC3785CF294493F4AE598D65|
|Scripts/Survivor/Progression/RebirthServiceCraftSkillService.cs|661|F458C74F3F3071D719CCA3A065AA167AA24B2DF8A1387E500426554B4E106160|

3 new unique C#2886 lines. DPS ranges1–210/211–420/421–625/626–835/836–1045/1046–1255/1256–1465/1466–1626. Patches1–200/201–400/401–599. ServiceCraft1–190/191–370/371–535/536–661. Hashes rechecked unchanged at close. Installer/bootstrap/persistence and native methods are targeted evidence only, no new full containing-file credit.

## Confirmed active combat queue has no installed frame settlement

DPS.QueueCombatProgress157+ aggregates by player entity ID and skill. FlushQueuedCombatProgress removes a batch under lock then calls TryAwardCombat, without requeuing failed awards. SettleQueuedCombatAtFrameEnd192+ would flush loaded players and owner publications. Its only runtime caller is a Harmony GameManager.Update postfix class RebirthCombatProgressionFrameSettlementPatch1620+.

`rg -uuu` across Scripts and current production C# finds no installation/reference to that patch type beyond its declaration, and no other caller of SettleQueuedCombatAtFrameEnd. ProgressionInstaller explicitly installs adjacent weapon/equipment profile patches and reload timing patches78–81 but omits frame settlement. RebirthHarmonyBootstrap uses targeted CreateClassProcessor only; EnsurePatched explicitly disables assembly PatchAll. Negative search includes ignored Debug; historical _Documentation/PerformanceAudit copies are not production installation. This is a real unreachable settlement path, not inferred from an attribute alone.

Other FlushAllQueuedCombatProgress callers are WeaponFamily world/game shutdown handlers191/196. RefreshHeldProfile, profile lookup and normal progression GameUpdate do not flush. Therefore source-installed ordinary combat/stealth learning accumulates without in-session settlement. Disconnect persistence saves character records but does not drain this separate queue. Shutdown reset clears PendingCombat; normal SurvivorInstaller registers persistence before progression/weapon-family, and persistence shutdown flushes/resets repository authority before later weapon-family flush can authorize awards. Parent should verify event invocation ordering as part of final lifecycle classification; even without that order, in-session missing settlement and no disconnect drain are confirmed. No gameplay measurement performed.

No correction applied. Installing the hook alone must not be called the whole fix without validating existing duplicate physical-event proof, successful original/native source admission, owner lifecycle and failure custody. The queue contains amounts, not physical event IDs or durable receipts.

## Queue custody and replay limits

PendingCombat key is entity ID only; it stores no original player reference, stable owner, creation/world token. Flush accepts the currently supplied player with that ID and removes work before award. World reset clears it; absent players are not traversed by FlushAll. This is concrete weak custody for delayed work. Entity-ID reuse/new creation or an old queue being applied later requires native lifecycle evidence before claiming cross-owner XP. Current native entity allocation typically supplies IDs, but this tranche did not prove every reuse boundary.

Finite positive input is checked before accumulation, but adding many finite amounts can overflow to infinity; core award later rejects nonfinite and the already-consumed batch is lost. Practical bounds on actual damage events make this malformed/extreme path, not a normal packet exploit. Locks protect maps/copy-clear; Unity/effect work occurs outside Gate. No arbitrary concurrent mutation thread is introduced by these sources; source references alone do not prove every caller is main-thread.

Core TryAwardCombat preserves existing authoritative eligibility/character snapshot and synchronous save semantics from prior full trace. This queue has no durable event receipt of its own. Failed nonreceipt save can leave accepted dirty character state and owner retry, whereas rejected eligibility/projection work is discarded. Native method duplication cannot be repaired merely by batching sums.

## DPS math and owner/server inputs

Combat calculation uses actual credited health loss/live sustained rate, caps equivalent seconds per hit and applies ordered <25/<50/<75/<90 bands. Unlike unified TrainingFramework's integer inclusive band gap, this DPS-specific band helper correctly covers fractional values. It validates finite damage/rate and final progress; currentSkill/config finite guards are weaker but invalid result returns0. World-work rate is creditedWork/attack interval, making creditedWork/rate equivalent to interval; quality/damage strength itself does not inflate work seconds.

Melee evaluates primary/power native effect damage/APM/stamina, selects highest calculated DPS and uses action delay fallback. Ranged includes pellets, live native action delay when held, magazine/ammo-per-shot, reload speed and reload downtime. Sustained rate uses cycle mean while attack/reload counts use a fixed window; those numbers describe different summaries and must not be interpreted as one exact simulated firing trace. No ammo is consumed by calculation.

Reload source order is measured neutral base, held animation heuristic, authored property/reflection, then five exact handgun-name fallbacks. Unresolved reload remains0 with a positive valid profile, so it assumes no downtime rather than refusing normalization; precise affected current content requires authored/native item verification. Historical3.2 comments/empirical prose are not installed b259 proof. Animation matching scores root/roles/names and longest tie clip; a clip name/length is not proof of actual native transition timing. Dedicated remote player may lack the owner's first-person animator/measured reload, yielding different fallback inputs; no universal numeric mismatch claimed without item witness.

Current held/display matching uses32-bit item signatures; display has a looser type/quality/seed/mod identity canonicalization, while combat compares fuller metadata/wear/ammo signature. RecentHeld retains last valid profile for grace after weapon switch, bounded map128 and full cache256. These are performance keys, not authenticated physical item/attack identity. No adversarial hash collision path was demonstrated. Delayed projectile after grace falls back to current nonheld preview effects rather than frozen original shot stats; attribution is approximate, not an original-shot receipt. Returned Profile is mutable reference, but no mutation caller was found here.

Cache signatures include item data, current effective skill, melee/reload CVars and reload multiplier; other changing buffs/equipment rely on periodic max age and installed invalidation. Reload animator/hierarchy metadata uses weak keys and1s refresh, avoiding retained hierarchy roots. InvalidateHeldProfile(player) clears the entire global cache, not just that player; performance scope only. Diagnostics default ReloadTimingDebug=false, bounded profile log keys; no profiling executed.

## Reload native callback ownership

Start/complete timing patches ARE explicitly installed. They select GameManager.World.GetPrimaryPlayer rather than the ItemActionData holding entity. __instance is only checked as ItemActionRanged and is not matched to the held action. Runtime start keys current primary held item; completion consumes that player's last start without checking current item's measurement key. This can contaminate primary reload measurement if another entity/action reload callback interleaves or a stale completion follows a switch. Exact native dispatch above these methods remains qualification; do not promote absence of actor matching into a proven normal multiplayer event.

Existing native ItemActionRanged.cs SHA256366B3A3C3E9C05B70B79115E890799D8C044A47204330B1CFC51723FB1B38964, targeted ReloadGun928+ and CompleteReload579+ read: native uses supplied actionData.invData.holdingEntity, conditionally notifies local reload start, and CompleteReload debits that entity bag/belt then updates its item Meta. REBIRTH timing only measures/cache-invalidates; it does not replace native ammo authority. Prefix can measure a ReloadGun call even if original does not begin a real local reload; completion validity is elapsed0.05..30s, not native ammo delta. Measured base is elapsed*start multiplier and stored process-only, never synchronized to server or persisted.

## Progression patch full integration and physical outcome boundaries

Combat prefix captures prior health/stealth and TheorySolo witness; postfix sends actual health loss into ordinary router after corpse exclusion. Solo receives __runOriginal while ordinary stats/router do not; zero actual loss normally yields no combat award. External interpatch changes to Health remain qualification, not proof of duplicate damage. This source does not deduplicate physical native hits; preserve prior physical-event evidence.

Craft AddCraftComplete excludes personal-output and Workstation OutputDepth owned paths, avoids duplicate cooking UI completion, preserves exact active Recipe variant where available, and delegates medical signature. Native queue Update/outputStack capability gates do not clear paid recipes. They are access/output guards, not award receipts. Equipment repair prefix captures source type/seed/amount/max and original starting actor; successful output postfix resolves actor then falls back to primary player if absent. It delegates ReportCompleted, whose server custody/native repair witness is separate pending source; fallback alone is not a confirmed steal path.

Harvest scope captures/restores original resource CVar, begins Phase8 output evidence, and finalizer completes only without native exception, aborts otherwise. It does not restore actual inventory on observer failure. Tree evidence compares original/post native block damage/type, limits work to requested/remaining and current nearby player; destruction settlement is deliberately no-op. Full Phase8 is next dependency.

Other-player medicine release policy refuses authored different-player medicine before native effects/consumption and opens no evidence scope. External target authority rechecked at release. Postfix requires original execution/current external scope before completing practice; finalizer closes scope. Self-use remains native-owner simulation. New self buff adapter captures exact old BuffValue/group, distinguishes new object from native refresh, checks current simulation owner/self-instigator/notstarted/nonremoved flags, stages immutable finite factor in item-effect scope, then consumes pending factor once on the exact buff-start event. Removal retires pending/quality/trauma flags and may restore a current verified sibling. These are real once-per-new-buff guards; they do not prove unrelated healing/LBD physical events exactly once. The medical admission/replication service implementations remain prior/other audit scope.

Loot specialized output observes prior Touched/loot list/opener then delegates; no new custody schema in wrapper. All explicit install references were located for relevant production patch classes; native installed metadata was not observed in a running game.

## Service/craft classification and rule inputs

Owned recipe primary skill comes from CraftingProgressionRegistry; fallback name/tag/area heuristics are external compatibility path. Repair ownership routes firearms->Gunsmithing, exact stock drone->DroneOperations, wearables->Tailoring, default Maintenance. Only drone field-service efficiency is knowledge gated; core Maintenance/Tailoring/Gunsmithing benefits remain skill-driven. Time/amount adapters use scoped effective skill and native queue data without creating items. Mechanics health multiplier and medical reserve/fracture/infection helpers change only existing outcomes; infection acceleration subtracts the same newly scheduled cure budget it front-loads.

TryGetPracticalSkillProgress remote branch uses unscoped GetOwnerStateSnapshot rather than scoped scalar API and lacks definition/mode/current-player checks. Callers include craft UI previews, QuickStack and self-medical practice admission; current world/current owner checks in those admission services remain essential. This is a concrete weaker helper contract, not independently proven unauthorized progression, and no corrected parallel client authoritative record should be added.

CraftTrainingRules loaded by CraftingProgressionRegistry45. Numeric XML values finite/ranged; duplicate materials/skills/recipes reject. It uses paid ingredient quantity resolver, ignores self-conversion/zero-material work, weights material-economic value, canonical authored time and difficulty, applies practice fade and associated capability recommendation. One cycle, not output count, is the training unit. BuildModel/Practice are calculation only; caller must establish actual output/payment.

DecodePreview admits bounded XML carrier <=8192 chars, <=12 ingredients/known item/count ranges and canonical output, and returns a recipe for UI expected-gain projection. Sole source caller is CraftingOutcomeViewModel269; no award/payment entry consumes this preview as authority. It should not be called a client-generated paid recipe witness. Malformed preview catches and returns null. This trace did not full-read all craft_training/capabilities XML; recipe registry/admission authority remains existing crafting evidence.

## Completion and next dependencies

Source closure complete for requested2886 lines. Main new confirmed issue is missing live frame settlement; secondary queue identity/failure and reload attribution concerns are explicitly conditional. No scenario run, native patch metadata observation, fault injection or test execution. Exact next requested full sources: Phase8WorldOutputTraining358, Phase9TechnicalTraining211, EquipmentRepairTraining remaining source/native receipt linkage, and any pending resource/native producers. Parent owns finding IDs and shared coverage/report integration.
