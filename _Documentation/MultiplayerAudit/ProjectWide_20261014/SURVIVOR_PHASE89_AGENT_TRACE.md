# Survivor Phase 8/9 physical output, repairs and training

2026-10-15 audit only; no production edits/build/tests/game/gameplay commands/git/shared ledger/report changes. Prior skill/DPS/crafting/vehicles/native traces reused. This file closes three current source bodies and precise native method dependencies; delegated full systems are not counted again.

## Full-read manifest

| Source | Lines | SHA256 |
|---|---:|---|
|Scripts/Survivor/Progression/RebirthPhase8WorldOutputTrainingService.cs|358|FE7D84F5CAD47040B8216BF1A8CCF29AC894E210DAC6CEBF5776E11E7A85D6C5|
|Scripts/Survivor/Progression/RebirthPhase9TechnicalTrainingService.cs|211|DFBD9F2D8D4B22B48162AC023981C32A120236EE6D460F09EE3BD77D0C5B7C4F|
|Scripts/Survivor/Progression/RebirthEquipmentRepairTrainingService.cs|371|73DD26BBE58C5AC8F952032863D5586F3374AA43CF62BD3A01EA2F7DD73933EB|

3 new unique C#940 lines, fully read bounded Phase8 first185/rest173, Technical211, Equipment first190/rest181. Installer, vehicle/electrical/farming callers and native containing files are PARTIAL named-body evidence only. All negative source caller conclusions use rg -uuu including Debug.

Read-only existing inspector extracted GameUtils to E:/_Haven/shared/REBIRTHTemp/SurvivorPhase89Native20261015/GameUtils.cs SHA2566DDFFF60BFB555EB56FDBC98AD332A29401AD7CF2C698C5FC70E6183BF6B7A29. FULL named methods HarvestOnAttack1274–1417 and collectHarvestedItem1420–1461; unrelated source unreviewed, no full-class credit. Existing XUiC_RecipeStack.cs SHA2567414ECF33B9EB334C8B88F4317556B5A8FEC988A1D6B655AF69C1FC88C049C4E: full outputStack458–646 body read across segments, containing class not fully read here. Installed assemblyFCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705. No inspector build or gameplay execution. HAV3N coordination was checked in preceding read-only native tranche; expired notice did not prohibit light reads.

## Confirmed remote native harvesting topology gap

Phase8.BeginHarvest only opens a scope on a server/nonremote world for a player action. Exact native GameUtils.HarvestOnAttack immediately returns unless holdingEntity is EntityPlayerLocal. Normal remote owner native harvesting therefore executes locally where REBIRTH BeginHarvest refuses remote World; its server replica is EntityPlayer and cannot execute native collection. The installed collected-output postfix cannot see successful remote output with an active authoritative scope through this path. This is concrete incompatibility between native owner execution and server-only observer admission, not merely an absent IsServer guard.

Consequences are bounded: vanilla Phase8 salvage/carcass/native crop output-based learning has no normal remote bridge here; AdvancedFarming's custom server harvest is a different path and is NOT ruled out. Current server tree-damage integration likewise provides a distinct logging witness. Mining continuous scope computes attackDetails damage and can award on exception-free scope completion without observed output; if a server-side caller invokes HarvestOnAttack for a remote EntityPlayer, its prefix could open scope even though original immediately returns. Exact upstream caller dispatch is still required before claiming a false remote continuous award. Do not infer that every remote action is replayed server-side.

Native collectHarvestedItem delivers via owner's LocalPlayerUI inventory, drops any remainder, gives native harvesting XP and returns bool. REBIRTH collection observation compares bag+belt before/after actual item count, not requested _count or native bool. Full bag delivery to world drop therefore yields no observed inventory delta for that route, despite a real output drop. This is a source-defined training witness omission, not item loss or duplicate award. No parallel server inventory mutation should be added to fix remote progression.

## Native deferred equipment repair loses its training observation

ProgressionPatches equipment Prefix captures only AmountToRepair>0; Postfix reports only outputStack==true. Exact native outputStack computes repaired clone, clamps wear to zero and sets amountToRepair=0 at510 BEFORE attempting destination inventory delivery. A full inventory can then return false after581+. That attempt produces no completion report. A later retry delivers the already-computed output with AmountToRepair0; prefix captures nothing, and postfix again rejects. Thus actual successful delayed repair output loses the training notification. This is the same native deferred-completion family as existing MP-015, now proven for this exact repair bridge; parent chooses integration rather than duplicating an identifier.

Host/listen ReportCompleted directly routes captured requested AmountToRepair/MaxUseTimes. It does not recompute actual wear delta after native clamping/degradation. Potential overcredit needs an actual upstream queue allowing amount>wear; this tranche did not prove that upstream bound. Remote report sends item type/seed/receipt only, not numeric restored work, so this conditional host numerical issue is not a client arbitrary-XP request.

## Equipment remote witness, receipt and original identity

EquipmentTraining installed by ProgressionInstaller21 and repair output patch117. Remote packet is ToServer, rejects null/remote world, authenticates playerId and resolves current player. Completion receives type+ushort seed and nonzero client receipt. Current world authority and mode are checked; no explicit current creation token is bound in the packet/runtime request.

Server samples live bag+belt every0.25s, keeping8 changes per type+seed for600s. Pending reports bounded32/player and expire30s; consumed receipt cache expires600s. It searches newest prior higher wear, clamps restored delta/max, replaces history with the current state BEFORE router award and consumes the receipt even if award is unavailable. This prevents straightforward repeated notice paying against the same retained condition transition, but intentionally sacrifices retry of rejected award work. No durable receipt or outcome ledger survives world reset.

Item identity is player entity ID+type+16-bit seed; quality/mods/slot/full serialized original are not part of matching. Histories for multiple same-key physical items can mix; current lookup returns first matching bag then belt item. This is a concrete weaker original identity predicate, not proof ordinary item seed collisions are frequent or maliciously selectable. Do not call it exact native original custody solely because the comment does.

Remote freshness remains the known native architecture dependency: existing NPC_TRADE/METABOLISM traces show native owner inventory upload updates latestPlayerData rather than necessarily mutating the server replica grids sampled here. Exact decrease on a stale server grid is not proof of current owning-client repair, and legitimate repaired owner output may never appear in this live-grid witness. No measured remote rejection/duplication is claimed. No custom packet restores or mutates physical inventory in this service; it observes and awards only.

## Phase 8 work formulas and native scope

Load reads finite/ranged tuning, exactly24 carcass rows and generated33 crop table count. Runtime historical loadStatus text is hardcoded (.25 etc), not independent proof authored tuning matches. Current qualification must use loaded data. [ThreadStatic] activeScope is one slot; Begin clears it rather than stacking nested scopes. Exact ActionData/player references correlate collection callbacks. Nested/reentrant HarvestOnAttack would overwrite the outer scope, losing rather than proving its work; no concrete native reentrancy observed.

Continuous mining/logging credited work comes from attackDetails.damageGiven/Max/Total with remaining-health bound. Trees deliberately skip that generic path; installed TreeDamageEvidence reads real original/after world block, caps native requested/remaining work, requires nearby player/logging tool and routes AwardTreeDamage. Scope closure's 'successful' means no exception, not original returned bool/committed world state, so continuous non-tree physical success depends on native attackDetails provenance. Phase8 itself does not generate native damage.

Salvage requires positive observed weighted output and expected authored drops, caps to target work fraction; bonus yield cannot multiply beyond that cap. Carcass requires dead recognized animal/tool, positive actual output and work, scales against original base butcher units/dead HP. Inventory delta count includes type totals and does not prove which exact ItemValue copy yielded them, but callback scope narrows ordinary synchronous collection. No replay IDs across repeated physical native calls are supplied.

Native/player crop route only recognizes suffix3HarvestPlayer and positive output (fixed gain independent of bonus yield), not every item in the33-count table. AdvancedFarming call sites RebirthUtilities372/397 validate seed stage/current block, commit replacement/air first, verify actual state, deliver every built stack, then ask router with captured output count. This targeted caller evidence is stronger than nominal harvest intent; delivery faults after world commit remain existing farming custody audit, not newly resolved here.

## Phase 9 actual caller custody and persistence

Technical service is a converter, not native outcome origin. Load requires202 component rows and3 electrical kinds with finite complexity, duplicate keys rejected. Availability string is stored but not itself a runtime enable gate. Mechanics install/replace parses block:/entity:content:slot and table complexity, repair uses actual health-restored fraction, hotwire positive-success flag. Electrical/lockpick use authored job/base time, bounded assistance/discrete cap then core server eligibility. Missing physical outcome checks inside these conversion functions do not authorize untrusted callers by themselves.

`rg -uuu` resolves Mechanics callers to VehicleRestorationSystem after successful published assembly mutation/compensation boundary. Repair1644 computes (entity.Health-before)/maximum after native change and CommitEntity; install/replace1505/1783 occurs after Applied outcome. Hotwire1585 is a distinct conversion success. Parent's full vehicle audit owns detailed payment/native mutation. Electrical sole caller InfrastructureWorkService358 preflights finite skill/config, requires meaningful published configuration change, consumes parts, installs record/dirty and sets tile modified, then awards. Award therefore follows accepted in-memory resource/state commit but precedes separate electrical durable storage completion; existing persistence/failed-custody issues remain. No false durable atomicity claim from the 'committed' label.

Lockpick sole production route WaveA handle validates exact native attempt/time/block incarnation/unlock before consuming attempt and calling converter; existing composite native trace governs acceptance, legacy unavailable adapters not used as proof. Technical functions use ordinary migrated evidence with active-time minimum interval, no durable original receipt. They reject nonfinite final raw before award. These rate guards are not once-per-original physical transaction guarantees. A failed award can be forgotten after the caller consumes its work token; no rollback/repeat of paid effects is justified by training failure.

Core SkillAwardService persists skill+attribute/anti-repeat within character snapshot; these services do not introduce a new progression file. Equipment histories/receipts and Phase8 thread scope reset process-only. Reconnect/restart reset witness availability, not persisted skill. Existing MP-036 final dirty-state reset and core receipt rules apply without a duplicate finding claim.

## Outcome and exact remaining work

Full source scope complete940 lines. Two primary findings for parent review: remote native Phase8 harvesting admission mismatch; deferred output retry loses repair completion/training (MP-015 family). Native physical replay is not declared solved by these scopes/cooldowns/receipt caches. Remaining exact proof is upstream server/owner attack invocation plus current live-grid refresh, original item identity and storage-failure/reconnect qualification. No tests/fault scenarios/native patch metadata observed. Parent owns IDs/ledger/report integration.
