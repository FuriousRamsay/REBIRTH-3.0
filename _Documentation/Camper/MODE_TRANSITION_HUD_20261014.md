# Camper transition progress without an open UI

Source: current cumulative REBIRTH camper workspace (parent checkpoint 7ecb720), existing native XUi HUD patterns and installed camper energy implementation. Target: current camper test build. This is a coding-only contribution; parent owns compilation, gameplay/scenarios and packaging.

Read: project operating directives; existing camper energy requirements/runtime/configuration; XUiC_RebirthPurgeProgressHud, XUiC_RebirthMusicHud and XUiC_RebirthVehicleHudStatBar lifecycle/binding/vehicle-selection patterns; current windowToolbelt and toolTipPopups geometry; native toolbelt window-group registration in Config/XUi_InGame/xui.xml. Defining a new standalone window would require another window-group registration, so the new controller is an always-updating rect appended inside the already registered native windowToolbelt.

## Implementation

Scripts/Vehicles/Camper/XUiC_RebirthCamperTransitionHud.cs presents an owned camper's observed Deploying/Packing state while the player is alive in the current world and within the existing eight-metre interaction range. Selection prefers the shortcut-requested vehicle, then the retained transition, then native current Vehicle selection, then existing native look-target resolution. There is no global entity scan. ModeInput.Request calls Track only after existing permission/current state observation succeeds. Tracking supplies a candidate reference, not permission or a predicted transition. The HUD remains hidden until TryObservePresentation returns an actual positive finite authoritative remaining value in Deploying or Packing.

The controller retains a valid transitioning vehicle when the player looks away, and remembers the native Energy session's current vehicle while its UI is open so an accepted UI-started transition can become visible when that window closes. Every presentation observation revalidates live world/player/vehicle identity, proximity, owner and mode. Dead/removed/replaced/remote stale entities cannot continue a locally extrapolated countdown. Close/Cleanup hide the card; Cleanup clears the corresponding static shortcut reference.

Caption uses Mathf.CeilToInt(observedRemaining). Fill is Clamp01(1 - observedRemaining/configuredPhaseDuration), using existing DeploySeconds/PackSeconds. Both current Config/RebirthCamperEnergy.xml and configuration defaults specify the owner's approved 20-second phases. The HUD does not modify these values. It polls presentation at five Hz and refreshes bindings only when visibility, caption or actual observed fill changes. It never subtracts elapsed real/game time from a snapshot, predicts completion, smooths a paused custody timer or fabricates timer progress between server snapshots. Thus remote updates can advance the fill in native snapshot steps, and actual custody pauses remain visible as pauses.

Cursor windows, modal windows and console input immediately hide the card; closing them rereads state. Ordinary always-open HUD windows do not suppress the HUD. The mode UI and all gameplay authority remain unchanged.

Config/XUi_InGame/windows.xml appends only the new controller rect to windowToolbelt, preserving existing vehicle gauge gray tracks. The 360x44 card is horizontally centered in the existing 1240-wide toolbelt and sits above the existing message popup area. It has a gray background track, teal horizontal filled progress and the whole-second phase caption. Final in-game positioning/overlap and readability were not visually validated by this subtask.

## Localization

No new localization keys are needed. The caption reuses verified existing keys:

- xuiRebirthEnergyHudDeploying: DEPLOYING {0}s
- xuiRebirthEnergyHudPacking: PACKING {0}s

Localization.txt was read to confirm those keys and was not edited by this subtask.

## Requirement traceability

| Requirement | Implementation/member | Other impact | Validation performed | Result |
|---|---|---|---|---|
| Progress with no UI open | XUiC_RebirthCamperTransitionHud.Update/Select; windows.xml appended rect | HUD presentation only, existing registered toolbelt | Existing HUD/window-group source inspection | Implemented; gameplay unverified |
| Actual 20-second transition state | Observe, Update | Existing runtime/configuration only | Config and TryObservePresentation/RequestMode/Tick read | Implemented |
| Ceil whole seconds, no guessed timer | Update, campertransitioncaption/campertransitionfill bindings | Reuses phase localization; no new timers or packets | Source inspection of direct remaining calculation | Implemented |
| Hide under UI and preserve transition target | Update, Track, Cleanup; ModeInput.Request | No window opened, no authority/state mutation | Guards and lifecycle source review | Implemented; gameplay unverified |
| Preserve gray gauge tracks | windows.xml single scoped append | Fuel/condition track nodes unchanged by this contribution | Current source readback | Implemented |

No persistence, save migration, XML mode configuration, networking, gameplay option or new localization change is introduced. The existing server-owned timer and native sync remain the only source of truth. Full feature delivery conformance, archives, hashes, compilation and requested in-game checks remain parent-task responsibilities.

Compilation: Not performed by this subtask.
In-game validation: Not performed by this subtask.
