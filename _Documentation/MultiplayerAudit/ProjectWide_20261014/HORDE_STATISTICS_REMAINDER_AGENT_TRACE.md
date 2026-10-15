# Horde statistics remainder and injury replication boundary

2026-10-15 audit-only current REBIRTH3.0. No production/build/tests/game/commands/git/shared ledger/report changes. No interaction with externally launched game. Sources and native extracted text only. Reused STATISTICS trace service/runtime/owner direction evidence and installed native SHA256 FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705.

## Full current source manifest

| File | Lines | SHA256 |
|---|---:|---|
| Scripts/Survivor/Statistics/RebirthHordeNightStatisticsModels.cs |62|8952FB5D76C7835966D39305096BA720182ECD82B923DFF07DEDD32B409C3106|
| Scripts/Survivor/Statistics/RebirthHordeNightStatisticsProjection.cs |135|2A8C3CAD4AED538D34FB409A9A905439B56BCFF5E0A85FA99DF75BB10C7B60D0|
| Scripts/Survivor/Statistics/RebirthHordeNightStatisticsPersistence.cs |80|494238A6554BDA7D1C5B038038CF5342151180E57BADF0EB0BF3F0FCA50588D5|
| Scripts/Survivor/Statistics/RebirthHordeNightHud.cs |46|A2A416D1CBFE0783B670D468F0AF297FC928641A097F54A20C57CB924517BD66|

Four full323 lines. rg -uuu callers: StatisticsService BuildSnapshot→Lifetime/History; repository Serialize/TryLoad→Write/Read; StatisticsNetwork version4+ writes/reads; runtime Kill and server1s tick call HUD.Publish; CompassWindow196–202 binds Visible/Count/AfterDawn, TargetBar95 adjusts layout, PurgeHud hides while Horde visible. All actual release code, no DEBUG condition on these bodies. Prior service/runtime full sources not counted again.

## Durable model and format

Per-night WorldDay identifies native BloodMoon evening, retains Completed plus kills/headshots/decaps/deaths and case-insensitive category/type/variant maps. Lifetime independent totals do not derive from sent page. Both clone all maps. XML nested hordeNights version1 inside authoritative statistics record, absent section compatible old-save defaults. Writer all nights sortedday plus lifetime and sorted maps; no truncation of history. Reader strict nonnegative long/int bounds, unique positivedays, valid booleans and <=1unfinished, decaps<=heads<=kills, every map present, key1..256/unique/max4096. Saturating sums over all history must equal lifetime counts and nights count; bestNight points actual matching maximum or zero reference. No arbitrary stale page overwrites full authority.

No history count cap: intentional full lifetime consistency permits growing nights, clone/XML size increases with long saves; no measured performance issue. Map admission cap runtime4096 aligns reader. No exact sum check category maps vs kills/type classification; they are descriptive breakdown, no reward. Native evening day logic/weak per-victim dedup already full STATISTICS trace. Invalid read aborts detached record; repository backup/quarantine policy applies.

Projection bounded top64 ranking, page8 summaries descending, selected night separately top64 maps. History clamps requested page based totalcount before multiplication, so int.MaxValue page request cannot overflow unboundedSkip. Reader checks nonnegativepage/count andpage range, count<=8 andtotal, descending unique positive WorldDays, nonnegative counter invariants. Lifetime decoder checks bestkill<=total and head ordering. It does not prove ranking sum matches truncated totals; truncated rankings deliberately insufficient to reconstruct lifetime. Estimated lengths approximate diagnostics; native actual serializer transport still authoritative. No prepared item/native publication receipt in these display structures.

## HUD projection and owner distinction

Server Publish selects active night, else today/previous, sends changed native synchronized CVars Known/Day/Kills/Heads. These are float presentation, not canonical counts; above2^24 exact integer precision can be lost, extreme display-only bound. HUD Visible requires enabled BloodMoon frequency and visible/active native state, or dawn-halfhour knownpreviousnight; Count checks exact expected eveningday based22h, otherwise emdash. Locale invariantN0. AfterDawn layout uses same visibility and native active flags. No gameplay damage/award depends on these CVars. Owner native SET/replica ordering exact proof is prior network work; no ownerACK or durable count implied by successful SetCustomVar.

## Actual native injury replica boundary

Read existing native DescriptionBindingsInspect/NetPackageEntityStatsBuff.cs complete class (small native dependency), SHA256 36044387FAC6C4D1B7BD172BFF0183E9AC8B6E3A102788CAFBC179BF51B3BF14. Setup serializes EntityBuffs.Write(netSynctrue), packet unreliable, ProcessPackage applies Buffs.Read only entity.isEntityRemote, server forwards same bytes excluding actor. EntityBuffs.cs SHA256 9B73A8AB0CCC95FB844422A673CCDB94F4A0DFA04D9EDB3D35B6A60816E75D27: FULL named Read694–737 read, plus AddBuff overload165–185 targeted; no whole EntityBuffs credit. Read clears ActiveBuffs, creates BuffValue.Read then directly ActiveBuffs.Add and Stats.EntityBuffAdded, reads CVars through SetCustomVar. It never invokes EntityBuffs.AddBuff. Therefore the installed HealthEpisode AddBuff hook does not count a condition solely received/restored through this native snapshot. This is an exact dispatch boundary, not proof every remote injury always lacks a server AddBuff event.

HealthEpisode hook explicitly excludes world.IsRemote, so owning-client local acquisition by itself does not create authoritative episode count. If an injury is acquired only on native owner and reaches server solely through StatsBuff, counter remains unincremented. Establish which actual damage/injury generators use owner-only acquisition before promoting broad missing remote injury finding. Server-origin AddBuff and native forwarded AddBuff path may count independently; no claim all fractures/conditions omitted. Native player health ownership (prior Condition trace) is preserved, not replaced by server clinical simulation. Partial EntityAlive.DamageEntity entry read insufficient to settle all producers, no full native combat credit.

## Assessment and dependencies

No new confirmed active physical gameplay/custody defect. Horde formats/history/ownerHUD now fully read. Confirmed native Buffs.Read bypasses AddBuff acquisition observer; actual per-injury generator topology remains targeted next work. Existing failedsave reset MP036/release startup issues parent-owned, not duplicated. Native source extraction reused, no inspector/build/game run. Next useful dependency: native critical-hit AddBuff producer and player damage dispatch to exact owner/server path; remaining actual Survivor active ResourceField/TraitGameplay/WildAffinity service bodies remain pending parent assignment.
