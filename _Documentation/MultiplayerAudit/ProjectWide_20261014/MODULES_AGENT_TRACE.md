# Modules registration and concrete adapter trace — 2026-10-15

Audit-only source evidence. No production edits, build, tests, game control or Git operations. Project operating directives read; prior NPC/trader traces and existing audit finding context consulted. This is a scoped tranche, NOT whole-directory certification. Registry descriptions are not evidence of installation.

## Registration versus runtime

`RebirthModuleManifestRegistry` joins module, patch, toggle contract and shadow-state declarations for reports. `RebirthModuleRegistry` in Performance/Architecture is a declarative registry (partial read153–380); `RebirthPatchRegistry` explicitly records future ownership rather than installing Harmony. ToggleRegistry is a read-only plan. ToggleStateRegistry writes only process-memory shadow state and returns explicit no-gameplay-change text. Its setters do not patch/unpatch, persist, replicate or consult gameplay authority. These are harmless reporting state, not feature switches. Shallow snapshots expose declaration objects but no concrete gameplay consumer was found in this boundary.

Movement `OnUpdateNoBehavior`, item stats `OnModifyValueNoBehavior` and paint `OnRenderFaceNoBehavior` have declaration-only references in the complete Scripts symbol search. Their contexts/reports, and caves stage/rule registry, are planned scaffolds. No missing gameplay implementation finding is assigned: real movement/paint/caves behavior may live elsewhere. The DEBUG pathing registry is likewise declarative even where report text says behavior migrated; the actual smoothing installer below supplies that behavior. Release benchmark and ProtectCrate trace facades are inert substitutes, not persistence implementations.

Concrete install routes inspected: `RebirthFresh_Init` calls registry and hybrid-path installer steps; `RebirthFreshAdvancedFarmingModApi.InitMod` calls AlwaysStagger then HeadshotOnly before farming registration; `RebirthBossEventModApi.InitMod` installs DamageAdapter and reward access, optional DEBUG health trace and legacy health refill, then registers boss lifecycle events. The independent `RebirthHybridPathSmoothingBootstrap : IModApi` also calls its installer. These are explicit production installation paths; manifest membership is not one. Native IModApi discovery and targeted Harmony bootstrap are covered by existing audit evidence. No PatchAll fallback should be inferred.

## Combat adapter contracts

DamagePipeline contains a live scalar adapter beneath largely planned declarations: the High-priority EntityAlive.ProcessDamageResponseLocal prefix resolves the response source entity ID, asks BossEventRuntimePolicy for a multiplier and modifies Strength only. Invalid/nonpositive multipliers and strengths exit; rounded long result clamps1..intMax. Installer sets installed only after PatchClassOnce succeeds. Its runtime boss-policy dictionary, lifecycle, and remote damage ownership remain dependencies of the unread BossEvents body; this tranche does not certify damage replication or claim double application. The broad scaffold report saying no damage behavior is stale reporting text in the presence of this adapter.

AlwaysStagger explicitly rejects nonserver execution (where a connection manager exists), nonenemy/animal targets, Internal/Buff sources, zero strength, fatal hits, and mode-ineligible body hits. Zombie BloodLoss Buff normalization clears response stun/pain flags before the server eligibility gate; it is response normalization rather than health mutation. damageEntityLocal postfix marks pain/cancels pending melee impact. ProcessDamageResponse prefix temporarily changes pain resistance/hit counters; postfix and finalizer restore idempotently. Both class patches install before the module flag commits, so the generic MP013 early-flag defect is NOT established here. Earlier successful per-class patches can remain after second-class failure, while retry uses shared PatchClassOnce protection (see existing MP017/bootstrap evidence).

HeadshotOnly tracks nested ProcessDamageResponseLocal scope with ThreadStatic depth and finalizer decrement. It resolves attacking player, active HeadShotOnly passive, zombie/nonanimal/noheadshot exclusions; immutable attack-source item identifies the junk-turret exemption. Heat is exempt. Helmet head scale0.85 and body ranged0.05/melee0.025 adjust integer Strength and clear Fatal if adjusted strength cannot kill. These run at the native local-response hook, not a custom authoritative damage packet. Exact native source excerpt shows native HeadShotOnly enforcement includes a separate IsHeadshotOnly property branch as well as the passive lookup; filtering the passive does not erase the property branch. No defect is inferred from that without a conflicting gameplay requirement.

MutatedSpit is installed by Config/items.xml on ammoProjectileZombieMutated/onProjectileImpact. The action requires server, world, shooter and canSpawn tag; suppresses Blood Moon; makes1-in6 roll; counts only live exact minion class within horizontal40m in an80x30x80 bounds; caps12 then creates/spawns at impact+0.35y. No custom persistence/cache/packet exists in this action. Native projectile event firing, configured shooter tags/class inheritance and native entity save/spawn publication were NOT fully traced here, so end-to-end topology remains open.

## Qualified integration issues

**MOD-A: Headshot transpiler can report installed after failing its required rewrite.** At HeadshotOnly lines145–191 it searches EffectManager.GetValue with a HeadShotOnly constant in preceding48 IL instructions, inserts the filter on the first candidate, but on zero matches only logs Error and returns unchanged IL. The installer then commits installed=true and logs success. Prefix/finalizer can therefore install while the native all-or-nothing passive branch survives, defeating intended fractional body-hit exceptions. This is a confirmed failure-handling branch, not a claim that the current target IL misses. Existing MP004 partial-install/false-success family overlaps; attach this concrete adapter example rather than count a wholly independent systemic defect. Validate rewrite cardinality and propagate a failed required rewrite through existing targeted installer handling; do not introduce global PatchAll.

**MOD-B: Hybrid installer failure is swallowed at the shared step boundary.** Lines1433–1457 catch any PatchClassOnce exception, retain installed=false and return normally. A caller RunStepOnce can therefore mark its named step successful without a patch. Independent IModApi bootstrap provides another attempt, so permanent absence is conditional on both attempts failing; not a present runtime failure. Same existing MP004/MP013 family, not a duplicate new global finding.

**MOD-C: DEBUG capture lifetime hook is declared but not installed.** `RebirthHybridPathCaptureLifetimePatch` on GameManager.Update is declaration-only across Scripts, and the explicit installer patches only RebirthHybridPathSmoothingPatch. Thus its UpdateCaptureLifetime pump is absent in targeted mode. CaptureDecision independently checks expiry when the selected entity next reaches its movement hook; expiry is therefore delayed when that entity stops updating/disappears, not universally broken. Release excludes this class entirely. Capture command637-line implementation was not fully read, so this is bounded hook-reachability evidence, not certification of the full capture workflow.

## Scope limits / unfinished large modules

Hybrid smoothing actual prefix has server gate around176 and process-memory entity/world state; first165 and last1350+ plus selected capture branches were read. Its1491-line path selection, clearance, world-reset/entity retention and native ownership integration require a separate full trace. No pathing authority defect is claimed from this partial read.

BossEvents9070 lines: only installer/bootstrap section8209–8280 and directly referenced snippets were read in this tranche. ProtectCrate1513 and ProtectCratePersistence526 were not read. Debug capture command637 and ProtectCrate debug files not fully read. They must remain PENDING/PARTIAL in coverage. Boss reward access, health reset/refill, progress persistence, crate ownership/network and repeated world lifecycle are explicitly not closed. No test or running-game evidence is supplied.

## Full-read manifest

Paths below were actually read completely (some in contiguous chunks); SHA256 identifies the source snapshot. Hashing alone is not reading. Large partial dependencies are excluded from this full-read list.

- FULL `Scripts/Modules/RebirthModuleManifestRegistry.cs` (203 lines), SHA256 `78676D0468EF1B99339F3BEAD15EC246E8691685281FC8536DBAB11C890CB484`
- FULL `Scripts/Modules/Combat/Damage/RebirthDamagePipeline.cs` (472 lines), SHA256 `51CA7F0390F0B18079DA80F41F037A9F4170E5A04931625CF65F27256557C0A6`
- FULL `Scripts/Modules/Combat/HeadshotOnly/RebirthHeadshotOnlyBehavior.cs` (247 lines), SHA256 `61B87DCD3B3DAAB9E4E2A35028E95FCE3A1F32AA6D58DAE612BB31E121038E8F`
- FULL `Scripts/Modules/Combat/MutatedSpit/MinEventActionRebirthMutatedSpitImpact.cs` (83 lines), SHA256 `99FF6E9D512F8C6B259B70E53C3DDD381EB7A0BC59DAD7C61CBBB71A399B7B3A`
- FULL `Scripts/Modules/Combat/Stagger/RebirthAlwaysStaggerPolicy.cs` (229 lines), SHA256 `0163176A4EFBF496B6B5224D8F64188C124A7BDFB6490520522CFFDFF516326D`
- FULL `Scripts/Modules/Items/Stats/RebirthItemStatPipeline.cs` (320 lines), SHA256 `BF6C13F2D072A5BAEADC3B887971802125E048FF01C6E2DAB6625B42428D68B1`
- FULL `Scripts/Modules/Paint/RebirthPaintPipeline.cs` (327 lines), SHA256 `078A626B1FCFC79391A40E20D16D248412118B11C7DAFF4FA33B73FC8DBF2929`
- FULL `Scripts/Modules/Player/Movement/RebirthPlayerMovementPipeline.cs` (262 lines), SHA256 `D67926C7F106A500022347632509CDD4D9075DA373C4268183F31402496E2889`
- FULL `Scripts/Modules/World/Caves/RebirthCavePipeline.cs` (276 lines), SHA256 `A4C1F815A1F465F3C903A64ED584E82DF3C4001BBF73B76931FEC2E644072DC0`
- FULL `Scripts/Modules/Pathing/RebirthHybridPathPerformanceBenchmark.Release.cs` (20 lines), SHA256 `BB24E6ABE02C6C2E1ADB2D5ECAA0EA74EE6F56FFF363809CA80DD9081F9EB836`
- FULL `Scripts/Modules/ProtectCrate/RebirthProtectCrateTrace.Release.cs` (15 lines), SHA256 `296C45F87F22508AACAA76CF6B4562C6FA177A1A629EE18F90284D09F1D971C3`
- FULL `Scripts/Modules/Pathing/Debug/RebirthPathingPipeline.cs` (278 lines), SHA256 `5078961F5F882130CC5686A72D672E97077E84C6140D381D9BD66AC9FC5EB8CF`
- FULL `Scripts/Modules/Pathing/Debug/ConsoleCmdRebirthPathFresh.cs` (108 lines), SHA256 `97BA47BECE0F7230799FBDAD96671A676FED3CF245A8AC2E4948A1C4A906422E`
- FULL `Scripts/Patching/Core/RebirthPatchRegistry.cs` (151 lines), SHA256 `A81A6EE67D5B67BB5BFF3B7077DD90DF3F77F63AFC7764B0C3218EB5D463FB32`
- FULL `Scripts/Toggles/RebirthModuleToggleRegistry.cs` (408 lines), SHA256 `842AB45CE755B27C119E08A03711A0931324CD2569BD527746719C9115491491`
- FULL `Scripts/Toggles/RebirthModuleToggleStateRegistry.cs` (241 lines), SHA256 `B9898E660F202AB6EF06E5DCB1A4FA30143703DFEF6403EDF919A9A9F6B4863F`
- PARTIAL `Scripts/Modules/BossEvents/RebirthBossEvents.cs`, SHA256 `78FE36EAA5EDED2A629ECA8D692F7463819B9696BA39AE999DCB73F740C6B2B6`
- PARTIAL `Scripts/Modules/Pathing/RebirthHybridPathSmoothing.cs`, SHA256 `147D8ACD9CFE9BE053730724B51851EC2F1F84DD1270511CF9D8D88E7C0DCA50`
- PARTIAL `Scripts/Performance/Architecture/RebirthPerformanceArchitecture.cs`, SHA256 `18E0C777029FD777874D38717140CCB4491045157622DB44A1E1B47AFCEE0E9B`
- PARTIAL `Scripts/Patching/Core/RebirthHarmonyBootstrap.cs`, SHA256 `7B27D3EA1AFB9403FE8B9FF7811D776C7DBB1C6E62C800A3C22FCFEA1B799E94`
- PARTIAL `Scripts/AdvancedFarming/RebirthFreshAdvancedFarmingModApi.cs`, SHA256 `7B212F8E90C4B8D95BBC41391A422E9E4AF70D481A0888DBA971CF925A0EFED1`
- PARTIAL `Scripts/Bootstrap/RebirthFresh_Init.cs`, SHA256 `002E8D028A646C7063C5C18FFA9D1882988BB906BF9447B5658EC38E8E9CBA95`
- PARTIAL `Config/items.xml`, SHA256 `61E45859B9B9CB895AD9DDAF374071FEEF8BA8EC0D7E71EA2AD0FF35D0C50581`

