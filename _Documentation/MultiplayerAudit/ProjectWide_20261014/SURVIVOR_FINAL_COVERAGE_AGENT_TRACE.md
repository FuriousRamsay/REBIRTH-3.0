# Survivor final coverage tranche — 2026-10-15

Audit only; no production/build/test/game/Git. Eleven new FULL literal HudTracking C# bodies below. Four other HudTracking files (Models/PreferenceService/Store/Providers) reuse canonical prior explicit evidence, not new reads. No XML full-file coverage claimed from binding excerpts. No claim of complete Survivor runtime acceptance.

## Selection reconciliation

Current rg source inventory reports552 Survivor C# paths. Canonical453 evidence rows plus subsequent explicit FULL section/CSV path+hash rows yield45 additional matching rows in a conservative parser,498 unique recorded paths;55 current candidates were written to E:/_Haven/shared/REBIRTHTemp/SurvivorFinalCoverageCandidates20261015.txt before this new11-file cohort. Inventory is selection only. Parser omissions remain possible where prior evidence uses another format; these55 are not asserted genuinely unread. Canonical four narrative-without-historic-hash qualifications remain unchanged. Project Debug configuration includes Survivor source; Release excludes Debug directories, so source totals must not be called Release totals. Current source count differs from earlier553 and parent must reconcile current compile inventory separately. No source edit made by this agent.

## Runtime ownership and binding

Config/XUi_InGame/windows.xml1187/1194 condition character_progression('Rebirth') appends BuffLayout owner and TrackedProgressionHud under HUDLeftStatBars; manager6148 and stress25655 are literal controller bindings. These preserve XUi controller loading rather than explicit C# construction. XML snippets are targeted, not full content coverage. BuffLayout.Init installs compact layout Harmony patches via PatchClassOnce; native updateEntries prefix suppresses competing layout only in Rebirth mode, Update postfix applies geometry once/frame. Buff content/timer/click semantics remain native. No network/authoritative progression or inventory mutation in this cohort.

Manager668 binds up to272 tracked/208 available physical controls, available list is viewport over sorted logical results, manual additions capped15. It mutates client preference service, not owner progression. Drag snapshots entry identity plus ContextEpoch and preference Revision, rejects stale drag, then rechecks saved model identity before MoveGrid. Console input guard wraps all wired presses. Init unwires old events; close/cleanup cancels drag, releases modal lease and unsubscribes; close attempts preference SaveNow. Failed preference writes are logged/debounced by prior service evidence, not native inventory loss.

Manager caches owner header/snapshot, uses preference/text/provider revisions for invalidation and avoids provider resolution while closed. It lacks an explicit ContextEpoch comparison for cachedOwner (unlike live tracker); actual client-state reset/header behavior remains the dependency before asserting stale cross-world values. No observed cross-session defect claimed. Navigation lease dynamically patches actual cursor/base concrete SetNavigationLockView methods, records every setter revision and restores previous view only if its exact token remains owner. It does not overwrite another dialog's navigation token. Initially unknown native lock restores null rather than an inferred previous native state; whether an unobserved preexisting lock occurs is unqualified. Modal underlying-view restoration requires still-owned token and unchanged hidden property.

TrackedHud412 uses authoritative existing owner snapshot/provider resolution, never requests awards. ContextEpoch change clears cached owner/header, pulses and rendered rows; display requires current Rebirth mode and HasCharacter snapshot, enabled preferences. Mode/screen/provider/text/preference/header invalidation is throttled at.25s; modal/personal crafting suppression hides and reports zero footprint. Close/cleanup unsubscribe and hide. Rendering clamps15 cells, three columns, reserves overflow cell, preserves preferences rather than deleting hidden selections. Previous pulse map prunes unselected IDs. Skill gain math handles integer rollover, rejects nonfinite/nonpositive deltas; rendering pulses any source-revision value change, so a decrease can pulse color while positive delta text remains empty (delta column intentionally disabled). This is visual behavior, not an award.

Metrics234 owns capacity envelope/footprint with lock and callbacks outside lock, listener exceptions isolated. Music/purge reserve invalidation shares height contract; measured screen/scale/buff pixels determine capacity, fallback reference math clamps0..15. Tracker physical rows ceil(cells/3); compact buffs use pairs. ClearMeasuredGeometry resets measured/footprint state, music/purge reserves retain their separate owners. BuffLayout154 subscribes footprint, refreshes.1s and reports current reserved pixels/crouch; it calculates but does not apply yOffset in Rebirth path because actual compact positioning belongs Update postfix. ResetNativeOffset restores zero on close. No visual screenshot/resolution certification.

CompactBuffLayout269 stores per-list/per-item state in weak tables, captures geometry before ownership and restores only properties still equal to its written values when mode changes. It retains native non-buff notifications (null Buff), hides finished/paused/invalid/remove entries, suppresses modal/personal-crafting HUD, and uses bounded hierarchy discovery. It moves sneak/stress cards and native notification colliders/icons/bars/text; no buff definition/removal/click mutation. Stress70 reads local player CVars, handles world/player/root rebinding, hides dead/modal, projects tier/place tooltips; no CVar writes. Current native notification structure is a runtime dependency, not assumed compatible from names alone.

Three vector helpers (33/81/31 lines) have no outside production callers by rg -uuu Scripts. They check pure geometry/render arithmetic and detached preference parse/order/future-schema behavior, never live native layouts. No harness executed. Foundation delegates prior Store/Model implementations, not new coverage.

No newly demonstrated gameplay defect. Remaining candidate files, compile-condition classification and parent/other-agent ongoing coverage must be reconciled before claiming total coverage.

## FULL literal current source manifest

The first combined read truncated compact tail/metrics middle; compact156–269, metrics1–60/60–94/91–234 and navigation69 were reread explicitly. Manager1–230/231–460/461–668 and tracker1–170/171–412 complete. All eleven actual bodies fully read; hashes do not substitute for reads.

```csv
"file","sha256","coverageMethod","lines"
"Scripts/Survivor/HudTracking/RebirthCompactBuffLayout.cs","1CCED3C523DB714978AEB87AA1FCB7C04AA7CFC3318812564E39454CF0B4D206","FULL literal source body read; no execution","269"
"Scripts/Survivor/HudTracking/RebirthHudBuffLayoutVectorHarness.cs","52825C17EF50D5E3D053B3EB508F09ED31D8097FA11904EC15F7F94CBB55A3ED","FULL literal source body read; no execution","33"
"Scripts/Survivor/HudTracking/RebirthHudNavigationLease.cs","2CEA6782F5DE3F9017226918BF23F9EB59091232A41B85FF137748329B099F72","FULL literal source body read; no execution","69"
"Scripts/Survivor/HudTracking/RebirthHudTrackingFoundationVectorHarness.cs","279D7989E3A7CCD40654B70C32B60BAE417F91BA8E2DEBC883726A48D198C095","FULL literal source body read; no execution","81"
"Scripts/Survivor/HudTracking/RebirthHudTrackingLayoutMetrics.cs","A3045D45545272FAAC7A9AA33C282A952CE092D5D232F59F0201FA1C90105BBE","FULL literal source body read; no execution","234"
"Scripts/Survivor/HudTracking/RebirthHudTrackingRenderMath.cs","E81DC04DD737FD5336AFF87805DF570E173B070CF573B1F3530F860CF9CF6899","FULL literal source body read; no execution","50"
"Scripts/Survivor/HudTracking/RebirthHudTrackingRenderVectorHarness.cs","58081BD5E27AAAC4CE88DF9547A6C7EBA4539EA004C1584BC245A692C6EBF0BD","FULL literal source body read; no execution","31"
"Scripts/Survivor/HudTracking/XUiC_RebirthHudBuffLayout.cs","6C798EF234E004967DFF8B03C7F96BF4A8A8263996F1F2773230184014A98D8E","FULL literal source body read; no execution","154"
"Scripts/Survivor/HudTracking/XUiC_RebirthHudTrackingManager.cs","6C85F105F44F92860D108B4CF41AF18FC14E61C10BEADB7E71C84B6466034201","FULL literal source body read; no execution","668"
"Scripts/Survivor/HudTracking/XUiC_RebirthStressHud.cs","48AA8E9DB58E76BE5DBE5408A7E4A8889AC277EDFE593BBED8A72ECCFA743365","FULL literal source body read; no execution","70"
"Scripts/Survivor/HudTracking/XUiC_RebirthTrackedProgressionHud.cs","7A6228B6004E035297D8B8FBFB955496A130287985811317BE192E459A263CE7","FULL literal source body read; no execution","412"
```
