# Camper camp fade and interaction handoff — 2026-10-14

## Scope and ownership

Separate taller camper candidate, based on the preserved approved source and corrected side-mounted power trial. Original installed camper remains unchanged. Vehicle Studio owns geometry, materials and presentation. Backlog owns authoritative interaction, multiplayer synchronization, workstation UI and coordinated installation.

## Files

- Unity project: `E:/_Haven/shared/REBIRTHVehicleStudio_Camper`
- Final candidate source: `Assets/REBIRTHCamperTallIntegrated_20261014/rebirthCamper80s.prefab`
- Bundle: `E:/_Haven/shared/REBIRTHTemp/CamperTallIntegrated_20261014/Bundle/rebirth_camper80s.unity3d`
- Exact target paths: `E:/_Haven/shared/REBIRTHTemp/CamperTallIntegrated_20261014/interaction-targets.tsv`
- Build and reopened verification receipts: same output directory.
- Runtime: `Scripts/Vehicles/Camper/RebirthCamperCampStationAppearance.cs`
- Lightweight observation: `Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyRuntime.cs`
- Authoring: `Tools/VehicleStudio/UnitySources/RebirthCamperCampFadeBuild.cs` and `RebirthCamperCampFade.shader`.

## Presentation contract

| Mode | Equipment and stair visuals | Door | Interaction targets / walk-in collision |
|---|---|---|---|
| Travel | Hidden | Closed | Disabled / original travel hulls restored |
| Deploying | Remaining timer maps to 0 → 1; smoothed between authoritative samples | Closed | Disabled |
| Camp | Fully visible | Opens on entering Camp; manual presentation override allowed | Enabled / authored walk-in shell |
| Packing | Remaining timer maps to 1 → 0 | Closes when leaving Camp | Disabled / travel hulls restored |

Guardrails stay visible in all modes. All 16 crafting stations, 3 power units and stair visuals use `REBIRTH/CamperCampFade`, property `_CampVisibility`. Fine world-space dither maintains opaque depth and shader shadow coverage, avoiding alpha sorting holes. It is a visual fade, not physically transparent glass. Original material maps are copied to separate candidate materials. Texture maximum is 2K.

`TryObservePresentation(vehicle, out mode, out revision, out range, out remaining)` observes existing authoritative/synchronized energy state without cloning inventories. Existing `TryObserveMode` callers keep their original signature and meaning. No new authority or timer is introduced. Client visual smoothing does not extrapolate a paused timer. Completion snaps to the authoritative endpoint.

`RebirthCamperCampStationAppearance.SetDoorOpen(vehicle, bool)` is an internal **presentation-only** hook. It returns false outside Camp. It must be called on the main thread after server validation and on observers after replication. Mode transitions reset the door; manual state is not saved or networked by this hook. Door collider follows the hinge. Do not rotate the pivot independently each frame: use this hook.

## Interaction targets

Each target is a built-in `BoxCollider`, `isTrigger=true`, Default layer 0, name `REBIRTHInteraction_<id>`. Trigger is fitted to its model bounds and disabled until Camp. No custom MonoBehaviour dependency is embedded in the bundle. These are interaction-ready hit targets; no crafting action is claimed to be connected yet.

IDs: `forge`, `campfire`, `workbench`, `cement_mixer`, `iron_oven`, `circular_saw`, `outfit_designer`, `recycling_station`, `shredder`, `ammo_recycler`, `gas_stovetop`, `mortar_pestle`, `toolbox`, `chemistry_station`, `distiller`, `research_table`, `battery_bank`, `generator`, `solar_bank`, `rear_door`.

Use the existing REBIRTH station/module and localization definitions to map IDs. In particular, the ammo recycler also crafts ammunition; do not implement it as recycling only. Query triggers explicitly (`QueryTriggerInteraction.Collide`), identify the owning camper ancestor, apply reach/line-of-sight checks, and resolve the specific target instead of the broad vehicle collider. Default layer alone must not bypass walls or interaction authorization. Roof targets may need the existing camper station selection interface for reachable use from ground level; adding hit targets does not create roof access.

Server must recheck Camp mode, ownership/access and revision at interaction execution, handle busy/queue custody using existing services, and deny stale interactions when packing starts. Do not create replacement tile entities or duplicate stored inventories just to open a station UI. Power target interactions should reuse energy management source identity, not infer it from the render object name.

## Validation boundary

- Actual runtime source compiled to isolated E-drive DLL output; installed DLL not replaced. Existing project warnings remain.
- `Test-CamperCampFade.ps1` exercises the actual visibility method with 303 timer samples, reverse packing, endpoints and clamp cases.
- Unity bundle verification is recorded separately in `verify-receipt.txt`; screenshots are Unity previews, not game screenshots.
- `Tools/GameBridge/tests/camper_fade_deferred.json` is an observation scenario, NOT RUN. Its screenshots require visual judgment; successful capture is not a rendering assertion.
- No gameplay launch, movement, native station interaction, save/load, multiplayer or manual-door replication validation has been performed for this change.
- Installation must be coordinated with backlog, game closed, originals backed up, and bundle/runtime source kept in sync. Do not install the isolated test DLL blindly: backlog must build its current cumulative source.

## Prompt for the backlog session

Implement camper station and rear-door interactions using `_Documentation/VehicleStudio/CAMPER_CAMP_FADE_HANDOFF_20261014.md` and its exact target manifest. Vehicle Studio has authored a separate taller camper candidate with 16 crafting stations, 3 power units, camp-only trigger targets and timer-driven presentation. Connect each `REBIRTHInteraction_<id>` to the existing corresponding REBIRTH module/UI, including ammo crafting at the ammo recycler. Validate Camp state, authority, access, reach, line of sight and queue custody; disable interactions during Deploying/Packing/Travel. Add authoritative replicated/persisted manual rear-door open/close using the presentation `SetDoorOpen` hook, preserving automatic opening only when the camp timer completes and closing when packing begins. Preserve permanent guardrails, Disco/native finishes, seating and physics. Review source changes before building your cumulative DLL. Coordinate matching candidate bundle installation only with the game closed and preserve the original version. Do not launch/control the game until explicitly authorized. Report source/build/install status separately from gameplay validation.

## Completion matrix

| Requirement | Evidence/status |
|---|---|
| Timer fade source and door completion gate | Implemented; compiled; pure timer fixture passed |
| Per-station and door targets | Authored 19 station/power + 1 door; manifest emitted |
| Bundle and source preservation | Isolated build; installed original SHA256 `25516F90949FEFDBE263D48834041233015A04A4F129DF187AE95928E71ABFEB` |
| Reopened bundle and render check | See final verification receipt; not equivalent to gameplay |
| Actual crafting/power/door interaction | Backlog implementation requested; not connected by this asset task |
| Installation and game validation | Not performed |

Final verification: bundle built and reopened successfully; all five preview images inspected. Bundle SHA256: 960336342D4D94113E854EA614C3B458DDF3A014211CFC34007ECAF82ACDDC8D. Size: 527933508 bytes. Installed asset and DLL hashes unchanged. Compile: 0 errors, 19 existing warnings. No game launched or tested.

