# Camper requested corrections and battery charging

Delivery type: changed-files package. Source cumulative project: existing REBIRTH 3.0 workspace, `C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0`. Target: that same project for 7 Days to Die 3.0 b259. Work started October 20 and continued October 21, 2026, America/Toronto. No full cumulative project is included.

## Scope and retained approval

The owner approved the final truck render with a black floor, dyed side panels/corners, original wheels, original license artwork and approved bumper/tailgate. That installed bundle is preserved byte-for-byte. This follow-up fixes the remaining reported spawn, interaction, relay, radial and station-overview issues. The owner then explicitly approved both engine-driven and surplus-daytime-solar battery charging.

The implementation was divided among agents for spawn/lifecycle, physical interactions/wiring, UI/charging, with parent integration and independent reviews. The owner performs gameplay testing; no agent launched or controlled the game during this work.

## Corrections

### Initial truck placement and world entry

The starter truck previously began preparation from `PlayerSpawnedInWorld`, which is too late to keep its appearance behind native loading. The native world-readiness gate now queues and waits for truck publication/model readiness before the player enters. Character creation still completes through its existing post-spawn path; world camera/control remain covered until the native driver attachment is confirmed. The progress popup yields to the character chooser/creator so it cannot block its own dependency. Remote authority/client readiness is actor/world scoped; the server must observe the actual client spawn event before final handoff.

Placement checks test entire rotated voxel intersections, multiblock child cells and dynamic occupancy. They reject obstacles and retry suitable road candidates; no wrecks, blocks or other entities are deleted. Existing journal stages and no-duplicate semantics remain intact. The new behavior applies to initial Purge starts and unfinished starter handoffs, rather than relocating already completed saved trucks.

See `SAFE_PURGE_TRUCK_START_20261020.md` for native lifecycle evidence, source members, exact checks and manual qualification.

### Camper parts and wire relays

Relay placement previously used inactive collider/renderer world bounds, which can be empty, and mounted against broad collision hulls rather than the visible skin. It now derives dimensions from authored local geometry, probes the exact camper mesh and seats the relay mounting plate two millimetres outside that skin. Its target covers the relay geometry rather than a small tip box.

Physical and wire target selection ignore only the exact native model-root selection proxy. Real structural colliders, closed doors, walls and other vehicles still occlude selection. The existing 21 authored target IDs and Camp/installation/permission rules remain. The existing authoritative wire edit and native circuit adapter carry admitted connections to camper source spending and downstream native devices.

The installed bundle was reopened in Unity without rebuilding it. Its `camper_body` mesh is readable, and the production surface algorithm hits both actual side walls at the intended mount height. See `Energy/PHYSICAL_RELAY_CORRECTIONS_20261020.md` and `RemainingFixes_20261020/relay-bundle-check.txt`.

### Radial menu

The native game caches its activation-command array. The previous body-dependent registration was therefore never reevaluated after inspecting a bare pickup. A scoped cache check now clears stale arrays when the native body modifier or energy registration changes. Native registration/reordering and existing permission guards rebuild the menu. Stable states and unrelated vehicles retain their caches. See `RADIAL_MODIFIER_REFRESH_20261020.md`.

### Crafting-station overview

All four summary icon/text pairs use equal gaps; Installed stations uses the native workbench icon, Working has a native hammer icon, and the nonexistent Queue icon was replaced by the native clock. All twenty cards center Fuel/Queue/Output text and icons, and center the Use Crafting Station caption on its button. The main background matches the neutral surface used by existing camper windows. Native UILabel code confirms the center pivot centers text itself, not only its bounding rectangle. See `STATION_OVERVIEW_UI_FIXES_20261020.md`.

### Engine and solar charging

Charging runs after active consumers have been supplied and uses actual native bank battery ItemValues. Engine charging requires the native running-engine state, a driver and fuel (respecting the existing no-fuel-consumption option); no speed threshold is imposed. The configurable alternator output defaults to 50 W and is bounded to 0–300 W. Solar contributes only its remaining daylight generation budget after device spending. Each enabled battery bank retains its native acceptance limit; full or disabled banks do not receive charge. Generator and battery discharge are not used as charging inputs.

Charging uses the native 0.16-second power cadence. Temporary timing state is reset across custody holds/unloads so offline or unresolved intervals cannot become a later charging windfall. Changed battery stacks flow through the existing energy state encode/revision/persist/sync path. No new saved battery format is introduced. See `Energy/BATTERY_CHARGING_IMPLEMENTATION_20261021.md` for native conversion evidence, configured limits and tests. Supported vanilla/REBIRTH banks use native `ChargePerInput=1`; custom banks with another conversion are safely refused rather than permitting the native conversion to overdraw its offered funding.

## Requirements traceability

All implementation file paths below are relative to the project root. The delivery manifest lists every changed/new file with its SHA-256 hash.

| Requirement | Implementation files / members | Persistence, XML, UI, localization and network impact | Validation | Result |
|---|---|---|---|---|
| Preserve approved model and black floor | Existing `Resources/rebirth_camper80s.unity3d` | No bundle edit | SHA-256 compared to approved installed version; Unity read-only open | PASS |
| No truck/wreck/block overlap | `Scripts/Purge/Runtime/RebirthPurgeStartFootprint.cs`, `IntersectsVoxel`; `RebirthPurgeTruckStart.Clear` | No destructive world changes | 28,813 geometry/handoff assertions; child/entity source checks | PASS |
| Prepare before world entry; safely attach afterward | `RebirthPurgeTruckStart.BeforeWorldReady`, `Queue`, `Update`, `PresentStart`; `RebirthPurgeStartHandoff.CanRun`; readiness packet file | Existing journal retained; new bounded readiness projection only | Native lifecycle review; 17 static contracts; remote event-witness regression | PASS, live behavior unverified |
| Interact with authored parts and relays | `RebirthCamperPhysicalTargets`, `RebirthCamperWireTarget`, `RebirthCamperRelayVisuals`, `RebirthCamperRelayGeometry` | Existing target IDs, permissions and link metadata retained | 48 production-source fixture checks; installed-bundle skin rays | PASS |
| Connected blocks use camper sources | Existing relay authority and energy circuit adapter; corrected authoritative reach | Existing native downstream circuit and save/sync paths | End-to-end source trace; focus fixtures | PASS, actual game electricity unverified |
| Radial changes with body modifier | `RebirthCamperRadialCache.NeedsRebuild`, `RebirthCamperRadialRefresh.Prefix`; installer registration | Native modifier remains sole state; no schema/UI changes | 25 production policy/prefix checks; independent review | PASS |
| Screen spacing, icons, centering and neutral background | `Config/_Vehicles/camper_station_picker_windows.xml` | Existing bindings/localized labels/controllers retained | Twenty-card geometry, native atlas inventory, native text-pivot evidence | PASS, screenshot qualification unperformed |
| Engine and surplus solar charge native bank | `RebirthCamperBatteryCharging.Apply`, `EngineRunning`; energy runtime/configuration/unload hooks | Optional XML rate; native stack persistence and authority reused | 256 actual-source energy assertions, exact native charge method body, independent review, full DLL build | PASS |

## Compatibility, maintenance and validation limits

The approved native vehicle rig, wheels, materials, UVs, plate, mesh and bundle remain the already approved version. The new relay fitting is runtime presentation. Existing body-removal admission, module custody, station processing, native bank formats and wire metadata layouts remain. No localization keys are added; the loading cover reuses existing localized text. Existing remote edit authentication remains in place, with narrow new world/actor checks for starter readiness.

Apply the updated DLL and XML on all participating modded peers and restart. Existing saves retain their native modifier/bank items and starter journal. Source-only correctness, stubbed physics hits and successful compilation do not establish actual native input, visual timing, network behavior or gameplay success. Manual scenarios are supplied for those follow-ups, including a fresh disposable Purge start, obstruction cases, creator completion, host/remote joins, body modifier roundtrip, all visible installed parts, actual wire connections, charging, save/reload and station screen screenshots.

Backups: `E:/_Haven/shared/REBIRTHBackups/CamperRemainingFixes_20261020` (previous DLL/PDB). UI backup reconstruction provenance is separately disclosed in the UI report.

The final combined build was performed successfully to `E:/_Haven/shared/REBIRTHStaged/CamperCorrections_20261021/Build/`: 19 warnings, zero errors. Exact log: `RemainingFixes_20261020/build.log`. The game had been reopened and locked the live DLL, so staging was used to avoid an incomplete DLL replacement. The owner subsequently closed the game and authorized installation. The final DLL and PDB are now installed in the mod root and match the staged build byte-for-byte. The installed approved model hash is unchanged. Exact deployment hashes and time are in `RemainingFixes_20261020/installation.json`.

## REBIRTH FEATURE COMPLETION CONFIRMATION

Feature: remaining camper corrections and approved engine/solar charging
Source cumulative project: current REBIRTH 3.0 workspace named above
Target project/version: REBIRTH 3.0 / 7 Days to Die 3.0 b259

Design requirements reviewed: PASS
Relevant existing documentation reviewed: PASS
Accepted architecture preserved: PASS
Code requirements: PASS
XML requirements: PASS
UI requirements: PASS
Localization requirements: N/A (existing keys retained)
Images/assets requirements: PASS (approved model retained)
Serialization/persistence requirements: PASS (existing formats retained)
Networking/synchronization requirements: PASS (native formats retained; bounded new readiness packets reviewed)
Documentation requirements: PASS
Packaging requirements: PASS (all archive entries verified by SHA-256)

Compilation: Performed, final combined build 19 warnings / zero errors
In-game validation: Not performed

COMPLETION VERDICT: COMPLETE

Basis for verdict: all requested correction and charging paths are implemented, compiled and installed, with the approved model preserved. Documentation, scenarios and the changed-files package are present; installed DLL/PDB match the final build and every archive entry is hash-verified. No required implementation surface is omitted. Gameplay validation is separately disclosed and is not being represented as performed.
