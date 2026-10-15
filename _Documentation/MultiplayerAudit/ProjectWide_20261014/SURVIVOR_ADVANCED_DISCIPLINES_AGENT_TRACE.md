# Advanced disciplines Rage and registry audit

2026-10-15 audit-only current REBIRTH3.0. No production/build/tests/game/commands/git/shared ledger/report changes. Other agent owns Beastmaster/WildAffinity; BlackMagic519 and companion disciplines reused earlier SURVIVOR_COMPANIONS full manifest matching9E0B052F652080E39D3FB712A377B980183F30108F364D0B8A2A7B54414EB70F, no new credit. Searches rg -uuu.

## Full source manifest

| File | Lines | SHA256 |
|---|---:|---|
| Scripts/Survivor/Progression/AdvancedDisciplines/RebirthRageService.cs |172|700F8D7D53124B4D977EA9DB33A2FC479D954A409FAA713AEE8147B42A293CDB|
| Scripts/Survivor/Progression/AdvancedDisciplines/RebirthRageCapsuleService.cs |17|5B24B631D0F11B7A53E5987C9C4EBAB710EE385DEA7F7637EBFA5F43965F775D|
| Scripts/Survivor/Progression/AdvancedDisciplines/ItemActionUseRageCapsuleRebirth.cs |43|CBD6C6202C3B34217FAB32A9E644FB9C373779342113F34FA4D50B3BBC03CB7F|
| Scripts/Survivor/Progression/AdvancedDisciplines/RebirthAdvancedDisciplineRegistry.cs |273|DCD515C13CF45B7636239795D74E04434199E9D71C3327C957224C56E0553618|

Four full505 lines; Rage award middle94–127 and registry125–190 reread after truncated combined outputs. Targeted SupportNetPackages request32–90, WeaponFamily46–105/155–217 (already full earlier), Config/_Survivor/advanced_disciplines root/tunables first28 and item5099–5111 exact native item binding. No whole XML/supportpacket credit.

## Active release and authority

ProgressionInstaller.Load advanced registry, Install Rage during normal release. Config root grants_enabled=true, Rebirthonly; Rage tunables current finite12second duration/15basic+5offensive+10blood Energy. Item rebirthRageCapsule extends drugRecog and binds custom ItemAction class in Action0. EventRouter melee eligible physical credited damage calls OnMeaningfulMeleeDamage; debug tier activation separate guarded command path other agent owns.

Service requires nonremote server world, enabled Survivor, live nonheld complete Berserker record and normalized creation. Mastery breadth6families>=20/depth3>=35; trial25 meaningful melee hits after prerequisites; final acquisition calls central CanAcquireFromRecord with proposed trial then adds acquired discipline/trial/basic Knowledge, RageValue>=1 and saves. Saves are not checked before owner publication or return: failed disk writes retain dirty in-memory grants, existing repository failure family, no exact rollback promised. Active hit queues Rage XP using actual attacking item valid melee DPS profile; first activating hit no retroactive XP. Queue settlement inherits previously confirmed missing normal-frame installer in DPS trace; this affects Rage too, not separate duplicate defect.

Tiers basic1/controlled25/offensive50/blood80 plus corresponding Knowledge; blood requires visibleBloodMoon. Already-active activation does not early-return: repeated capsule request can repay/refresh; no packet operation receipt, so reliable application duplication would consume again if actual item remains. Server Support request ValidEntityIdForSender and exact creation bind before dispatch, so stale character request rejected. No arbitrary client cost/rawXP accepted.

## Confirmed saved-marker/process-timer divergence

Rage Active dictionary stores playerentityID+creation+Until realtime and tier; server IsActive validates timer/current character. GameStarting and WorldShuttingDown only clear dictionary. Native marker $rbRageActive and $rbRageTier stored via SetCustomVar; OnUpdate considers only existing Active keys and returns immediately when none. rg -uuu all Scripts/Config finds no reset/scrub of those two marker names outside SetMarkers. On disconnected actor update can remove key without player to clear; fresh process emptykeys likewise no retirement.

Exact native EntityBuffs.Write complete named method654–692 in DescriptionBindingsInspect/EntityBuffs.cs (SHA9B73A8AB0CCC95FB844422A673CCDB94F4A0DFA04D9EDB3D35B6A60816E75D27) read: persisted nonzero CVars included unless leading dot; $ markers are included. Prior full named Read694–737 restores via SetCustomVar. Thus a native saved active marker can outlive process-only12second timer. No generic native CVar timer exists here.

Remote IsActive branch accepts marker>0, decodes tier, lacks timer/creation/record/primary-owner expiry. Actual active consumer WeaponFamily.OnGameUpdate remote branch SyncNativePassiveEffects(primary) every interval, calls Rage.GetStaminaDelta/GetSpeedDelta for melee and writes passive effect CVars. Therefore after native save/reconnect preserving active markers while authoritative Active absent, remote owner can derive Rage modifiers while server IsActive=false and empty OnUpdate never retires marker. Source-confirmed topology/persistence inconsistency, runtime untested; do not claim measured permanentbuff duration or exact native save race reproduction. SP/listen server branch ignores stale markers so distinction matters. Starting another valid Rage and letting it expire provides a clear path; it does not repair automatic resume by itself.

Server additionally syncs passive CVars for remote replica, but uses IsActive dictionary; client locally recomputes using retained Rage markers. This is contradictory authority input, not a demonstrated alternative expiry mechanism. Current markers remain saved unless SetMarkers(null) runs for that tracked player. Death within tracked session removes on next update; death/load after untracked state remains outside scan.

## Capsule payment and remote inventory boundary

Custom action disables native Consume/animation. ExecuteInstant dispatch returns true regardless service result; feedback result conveys failure. StopHolding cancels pending native eating state. Local host calls service, remote primary uses authenticated SupportAction UseRageCapsule with type+seed+creation. Service requires Berserker+Offensive Knowledge, live owner, no library resource reservation, finds first matching native server belt/bag type+seed (not exact full item), validates actual capsule class. Calls TryActivate FIRST: subtracts Energy and SaveIfDirty without checking success, publishes active/markers, then decrements matched capsule and writes slot.

No prepared immutable item receipt/expected revision/owner ACK/correction payload in this route; result only success+message. Native remote inventory ownership/upload boundary already verified earlier Repair trace: NetPackagePlayerInventory updates Sender.latestPlayerData streams, not live owning client grids. This service mutates server mirrored stack without direct owner native removal protocol. Consequently actual remote capsule debit must be qualified against another inventory application consumer; none found in this capsule chain. Do not describe server SetSlot as proven owner item consumption. Strong candidate same native support remote custody family, not independent proven arbitrary itemmint. Exact live mirror freshness, owner reconciliation after request and persistent physical debit remains unresolved; preserve native accounting without redesign.

TryActivate persists metabolism Energy before item debit but continues if save fails. Crash can separate Energy/capsule/active duration; no transaction/receipt coupling. No error compensation if postactivation inventory write throws; actual runtime throw not observed. Host immediate item decrement is concrete native mutation; no server resource scan charged arbitrary clientchosen amount.

## Registry contract

Registry clears then parses authored root/mode, grants flag, anti-farm/capacity/exclusions and immutable definitions. Required3discipline IDs/hard infected animal class/tag exclusions validated; duplicate discipline IDs throw. Requirements carry kind/id/minimum/planned. Evaluation requires enabled Survivor, known definition, grantsenabled and no planned deferrals; skill/knowledge/discipline/action/accomplishment/trial/melee breadth-depth checked. CanAcquire clones existing sets and adds current proposed trial only, then uses central evaluator; no public acquisition mutation in registry.

Parsing float minimum/tunables uses TryParse without finite bounds. NaN minimum can make s.Value<minimum false; finite current config means no current hostile packet path. Rage tunables publicstatic and malformed XML can produce nonfinite durations/costs/deltas; SourcePath is localauthoring, no network-controlled edit. Bounds helper counters normalized positive but registry doesn't validate unknown requirementkind at load (runtime fails closed). Lazy EnsureReady retriesLoad on failure; partially loaded static definitions not ready, no valid grant published by ready flag. Native target hard exclusions used prior companion chain; not re-audited here. Vectors report arithmetic/authoring checks only, not executed and not runtime proof.

## Findings and limits

Confirmed saved Rage markers can diverge from process timer on remote consumer after lifecycle restore; parent severity/ID. Missing DPS settlement reachability inherited, no duplicate count. Capsule remote native custody/payment failure is explicit candidate requiring actual owner inventory application proof, not asserted exploit. No production change or runtime qualification. Other advanced companion bodies explicitly excluded/reused current hashes.
