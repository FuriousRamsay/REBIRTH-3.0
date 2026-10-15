# Final current source reconciliation — 2026-10-15

Audit-only static inventory and evidence reconciliation. No production changes, build/MSBuild evaluation, tests, game, profiler, endpoints, commands or Git actions. Only this unique trace and owned PROJECT_REMAINING_SOURCE_RECONCILIATION.csv updated; shared report/ledger and external SeatPose sources preserved.

## Exact snapshot and inclusion limits

Current root `rg --files -uuu -g '*.cs'` discovery (SDK bin/obj/.git excluded), filtered by actual unconditional Compile Remove globs from current RebirthUtils.csproj, yields **1805 source files**, all under Scripts. Zero added/removed against preceding CSV and zero outside-Scripts included candidates. Project SHA256 F02362773008D03BBBFFFE2F4FBD857A4C7E055D8FF9109BA39C2DC80EA6B617. No actual explicit Compile Include, EnableDefaultCompileItems override, Import or DefaultItemExcludes source modification found; the initial comment mentions the SDK rule but is not an item directive.

This is the statically inferred SDK source set, not a compiler result, installed DLL inventory, target framework compatibility pass or native release qualification. Current non-Debug ItemGroup removes Scripts/**/Debug/**: 92 files excluded, leaving **1713 non-Debug source candidates**. Debug has REBIRTH_DEBUG; default qualification adds PURGE_QUALIFICATION when configured; non-Debug adds REBIRTH_RELEASE; optional Debug instrumentation adds REBIRTH_DIAGNOSTIC_HARMONY. Per-file preprocessor and runtime installer/release gates retain their individual trace limitations. Union full-source coverage does not prove every build branch is functional, release-enabled or exercised.

## Evidence reconciliation

Integrated 79 exact current path/hash entries from explicit literal full-body manifests in CAMPER_REMAINING_HALF_A_AGENT_TRACE.md, CAMPER_REMAINING_HALF_B_AGENT_TRACE.md, CAMPER_FINAL_UI_AGENT_TRACE.md, CAMPER_GAP_FINAL_NEXT_AGENT_TRACE.md, CAMPER_PARENT_EVIDENCE_TRANSPORT_CLOSURE_AGENT_TRACE.md and CAMPER_PARENT_EVIDENCE_ENERGY_CLOSURE_AGENT_TRACE.md. First path/hash binding was parsed with wrapped hash whitespace normalized; current hash matched all79. These resolve the60 previously unmatched Camper rows; other19 strengthen already-covered rows. Source body coverage belongs to those agents' literal reads, not this manifest parsing. Parent energy7/current station provenance and other earlier Camper cohorts remain inherited current matching evidence from the preceding snapshot.

All17 original semantic-only Features registries were literally closed by SEMANTIC_SOURCE_LITERAL_CLOSURE_AGENT_TRACE.md (8) and SEMANTIC_SOURCE_FINAL_CLOSURE_AGENT_TRACE.md (9). Their prior grouped semantic provenance remains explicitly historical. They are fixed reports/declarations, not execution/enforcement/test results.

Final classifications: TRACE_FULL_READ1649 + CANONICAL_FULL_READ24 + LEDGER_FULL_READ111 + NEW_LITERAL_FULL_READ21 = **1805**. **Zero unmatched, zero semantic-only, zero changed/stale, zero older-reference-only rows** after the final all1805 hash recheck. Canonical/ledger categories retain prior evidence quality and qualifications rather than being silently relabeled fresh independent literal reads. Hash-only mentions were not promoted. Snapshot freshness is limited to this read/recheck; later external changes reopen affected rows.

## External current changes reread literally

Three SeatPose sources changed externally since PROJECT_COVERAGE_RECONCILIATION_NEXT_AGENT_TRACE.md. All current lines were displayed and read literally without truncation, and expected hashes matched again afterward. No source edits made. Profile/Bones/Rig/Camera and other editor sources remain unchanged in this snapshot and reuse prior evidence.

| Source | Lines | SHA256 |
|---|---:|---|
| Scripts/Vehicles/Debug/SeatPoseEditor/ConsoleCmdSeatPoseEditor.cs | 67 | 7D75905D85D4B4950ADE42237EAFD31C2AF8AFBA0B991F463229E5D890E1825E |
| Scripts/Vehicles/Debug/SeatPoseEditor/SeatPosePanel.cs | 188 | 4E847E163CA5B91A83A22F3E2E4CA4FD1A14AD39E3E9F7AC84E2AAE58DEF32A3 |
| Scripts/Vehicles/Debug/SeatPoseEditor/SeatPoseSession.cs | 143 | 5F43462A16F10E430D7FF51FC05D290961809BE5CFEA993074692F15622DC411 |

Current console adds ready-status and handles attached same-vehicle seat transfer through native AttachEntityToSelf, actual seat anchor, local root/model positioning and SendSyncData(1). Initial command still excludes dedicated/remote sender/remote world, checks living local actor, parked live vehicle within10m and native lock permission. Reopened seat route retains original occupancy/native attach checks; no new network-authority claim is inferred from diagnostic host-local functionality.

Panel current GUI preserves ExitGUIException, balances BeginScrollView with finally, balances vector horizontal rows with finally, and supports a freeze inspection callback. Numeric explicit Set remains finite invariant validation; profile edits validate, apply and commit history with prior serialized rollback on failure. This is literal code review, not GUI visual validation.

Session now captures original Animator speeds, exposes readiness through Valid/current panel and uses freeze callback to set0 or captured speed. Close restores each captured surviving Animator speed through guarded cleanup. Existing current-world/native attachment/seat/speed checks, late-update local model offsets, camera/rig disposal and captured input/cursor/view restoration remain. Exact native rendering/attachment geometry, deferred Unity cleanup timing, saved profile adoption and play behavior remain unqualified. These sources are under Debug and are excluded from non-Debug compilation by the actual csproj rule.

No new finding promoted. Existing custody, lifecycle, source-release and native authority findings are not resolved merely because whole-source inventory coverage is complete. No build/game/multiplayer or fault-injection PASS is claimed.

Checkpoint recorded: 2026-10-15 16:40:59 UTC. Hash recheck completed immediately before this timestamp. External edits after this snapshot are pending checkpoint recheck, not retroactive coverage claims.

- Scripts/Vehicles/Debug/SeatPoseEditor/ConsoleCmdSeatPoseEditor.cs observed LastWriteTimeUtc=2026-10-15 16:28:56.0661739 UTC
- Scripts/Vehicles/Debug/SeatPoseEditor/SeatPosePanel.cs observed LastWriteTimeUtc=2026-10-15 16:29:52.3768485 UTC
- Scripts/Vehicles/Debug/SeatPoseEditor/SeatPoseSession.cs observed LastWriteTimeUtc=2026-10-15 16:28:54.9100514 UTC
