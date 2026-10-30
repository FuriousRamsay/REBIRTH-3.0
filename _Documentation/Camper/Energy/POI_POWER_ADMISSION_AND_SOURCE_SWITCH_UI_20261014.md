# Camper POI power admission and source switch UI — 2026-10-14

Source: current cumulative REBIRTH workspace, including installed-equipment appearance changes. Target: current installed game and REBIRTH Energy UI. Delivery type: parent-coordinated changed-files package. Coding only while the user tests; no game interaction, build or tests performed.

## Confirmed cause and required POI containment

SetPoiLights previously checked authority, owner, Camp, POI range and supported lights, but did not check usable power before setting PoiOn. Consumers intentionally assign zero active demand when no installed source is both switched on and Status.Available. Consequently an empty generator could accept the lighting request, display the OFF action, and still supply/meter zero watts. IsOn describes the requested lighting state; it does not prove lights are currently powered.

The latest user requirement replaces the former 20m proximity rule. NearestPoi now selects a POI only when the camper position lies inside its horizontal bounding-box footprint (minimum inclusive, maximum exclusive), with no added vertical constraint. It retains the existing exclusions for prefabs below 8m in either horizontal dimension and the prior tie order for overlapping containing POIs. Selection, authoritative admission, world consumers and direct observations share this selector. Remote lease projections additionally require that the currently containing POI matches the exact projected target; stale metadata from the old proximity rule cannot make an outside camper eligible.

Config/RebirthCamperEnergy.xml still contains poiRange="20" and the parser/lease retain that numeric field for compatibility with existing configuration and lease payloads. It no longer expands POI eligibility or grants a distance allowance. Ordinary camper equipment range="10" and defenseRange="12" retain their existing meanings. No runtime game query was performed to assert loaded configuration.

## Prospective load and authoritative admission

RebirthCamperEnergyPoiSnapshot.RequiredWatts is a cosmetic observation of all eligible supported POI lights' prospective load. It is separate from DemandWatts, which retains its existing meaning of currently powered/metered POI demand. Powered-light prospective demand uses the existing owner/workmanship savings with node.RequiredPower, matching the consumer path. Ordinary lights use the configured OrdinaryPoiLightWatts (currently 1). Existing claim, eligible native leaf, POI, world-light and trader exclusions are unchanged.

DeviceAdapter.CanSupplyPoiLights is shared by UI dispatch/button state and authoritative SetPoiLights. It requires current Camp, an in-range supported POI, a known prospective requirement, and enough usable output after non-POI current demand. Capacity sums MaxOutput only for installed sources that are switched on and Status.Available. CurrentPower and AvailableWatts represent transient stored native buffers and may be zero before a fueled generator's first demand tick, so those values are not mistaken for a missing generator's rated output. Source status already considers required components, fuel/charge and sunlight.

ON is refused before changing requested state when capacity is absent/insufficient. OFF retains existing authority/owner/Camp checks and remains possible after fuel/components/power disappear or no lights are currently supported. This does not remove installed sources, invent fuel, change native accounting, or replace world lighting behavior. Existing requested ON remains an OFF action even after a later supply loss, permitting cancellation.

## Observation wire compatibility

All production Energy observation writers/readers were reviewed: EnergyAccess.Refresh/local presentation uses the direct snapshot; the server reply writer and client receiver use EnergySnapshotWire.Write/Read. API signatures remain unchanged. Offline EstimateAndWireCases consumers were read but not run or edited.

The Energy observation writer now emits version 5, appending RequiredWatts to the existing optional POI observation. The reader explicitly accepts versions 2, 3, 4 and 5. Versions 2–4 leave RequiredWatts at -1 (unknown); UI refuses a new ON request instead of interpreting zero as a free load. OFF remains available when requested ON is observed. Version 5 validates the field is >= -1 and continues rejecting trailing bytes. The writer validates the same sentinel bound. Existing source-position/capacity and runtime-estimate version handling is unchanged. Old mod binaries do not understand v5, so multiplayer peers need the matching current mod build. Existing Energy save/lease payloads and mutation packet layouts are unchanged; the separate cosmetic native-item observation below is additive.

## Radial parity without opening management

The native radial ON action uses DeviceAdapter.CanTurnPoiLightsOn and the same CanSupplyPoiLights capacity gate as Energy UI/server admission. OFF bypasses the capacity gate. The remote radial requires a valid prospective-load observation independent of an open Energy UI; it never infers full POI demand from the supported-light count.

The server publishes the separate native item metadata key RebirthCamperPoiPowerV1 during its existing one-second POI observation. Its bounded Base64 binary observation contains version 1, world GUID, camper identity GUID, exact POI target identity, prospective watts and other active load. The native flag-4 observer sync is sent only when these bytes change. Existing persisted lease layout/version is untouched. The extra cosmetic key may travel through native item save/pickup metadata, but it is recomputed and is not restoration or authorization state.

Remote radial checks the observation's world, vehicle and exact currently contained POI against current identity/lease projection and uses the current synchronized source ratings/status for capacity. Missing, malformed, foreign-world/vehicle or mismatched-target observations fail closed for ON. The server still rechecks current authoritative power and full normal mutation admission, irrespective of client observation content. The local radial gate caches only for 0.25 seconds, scoped to the exact World object, world GUID, vehicle identity, POI and observation frame; it is invalidated by changed observations/identity. This also bounds snapshot/parse work while the radial is open. Release removes the cosmetic key and local gate; new world/entity objects and GUID checks prevent reusing a prior connection's gate. Observer updates remain subject to ordinary native synchronization latency.

## UI behavior

The existing two-line POI status label localizes PrefabName through Localization.Get and marks only the location name light purple ([D0A0FF]). It continues showing actual metered demand. If supply is unavailable/insufficient it additionally shows prospective watts required and available, or a waiting explanation for an older observation with unknown requirements. The POI ON button and dispatch path use the same gate as the server. The OFF button remains usable after loss of supply.

The Travel-mode screenshot follow-up corrects the previous misleading fallback to “No eligible nearby POI lighting.” ObservePoi now identifies only the containing POI name/footprint before the Camp gate; it still returns before enumerating powered nodes or ordinary lights outside Camp. RequiredWatts remains unknown (-1), and no zero count is presented as an assessment. In Travel the label shows the localized contained POI plus an Enter Camp instruction. Deploying/Packing show a neutral requirement that assessment/power occur in Camp. An outside-POI message is distinct from either mode restriction. Once in Camp, zero actual supported lights produces the explicit assessed no-eligible message; positive counts retain actual metered demand plus required/available supply if insufficient. Missing observations use the existing waiting status. No new wire fields are needed. TryObservePoiEligibility retains its Camp-only eligibility meaning for compass/radial callers; cheap name observation outside Camp does not grant eligibility or admission.

Source cards and selected-source status explain missing engines, fuel, batteries, battery charge, solar cells or sunlight instead of showing only an unexplained Empty/Disabled value. Source toggle captions use Turn Generator On/Off, Turn Battery Bank On/Off or Turn Solar Bank On/Off. They retain Enabled command semantics and do not perform Remove. Existing source slots, refueling and source/component transfer paths are untouched.

Localization is supplied by the parent task. Required keys: xuiRebirthCamperEnergyTurnSourceOnFormat; TurnSourceOffFormat; Generator; BatteryBank; SolarBank; NoEngines; NoFuel; NoBatteries; NoCharge; NoSolarCells; NoSunlight; SourceOff; PoiPowerNeededFormat; PoiPowerUnknown (all with the xuiRebirthCamperEnergy prefix). No localization file was edited by this subtask.

Travel-status follow-up additionally requires xuiRebirthCamperEnergyPoiEnterCamp, PoiTransition, PoiOutside and PoiNoEligibleFormat (same prefix). PoiTransition should describe the Camp requirement without promising Packing will automatically enter Camp.

## Requirement traceability

| Requirement | Implementation members | Persistence/XML/UI/localization/networking impact | Static review |
|---|---|---|---|
| Refuse unsupported POI ON | DeviceAdapter.ObservePoi, CanSupplyPoiLights, SetPoiLights; XUiC Energy.Send/Paint | Existing authoritative mutation; prospective observation only | PASS |
| Allow OFF after power loss | SetPoiLights and UI Send/Paint ON-only gate | No state/persistence redesign | PASS |
| Explain empty/no-power sources | XUiC Energy.SourceStatusText, PoiStatusText | Existing labels + parent localization keys | PASS |
| Localized light-purple POI name | PoiStatusText | Existing label markup | PASS |
| Clear source switch captions | Paint energyPowerLabel | Enabled semantics retained; parent localization | PASS |
| Remote parity | SnapshotWire v5; v2–4 unknown requirement | Explicit observation version only; no persisted schema change | PASS |
| Preserve native world-light/trader and Camp constraints | Existing eligibility/ownership checks remain | No global lighting changes | PASS |
| Require camper inside POI footprint | DeviceAdapter.NearestPoi, ContainedProjection, TryObservePoiOn | Legacy poiRange field retained only for payload/config compatibility | PASS |
| Match radial ON capacity gate locally/remotely | RadialPermission.Prefix; CanTurnPoiLightsOn, PublishPoiPowerObservation | Separate cosmetic observation key; normal server admission retained | PASS |
| Distinguish mode restriction from absent lights/power | ObservePoi cheap location before Camp gate; UI PoiStatusText branches | Existing observation schema; parent localization only; no outside-Camp light scans | PASS |

Files changed cumulatively for this issue: Scripts/Vehicles/Camper/Energy/XUiC_RebirthCamperEnergy.cs; RebirthCamperEnergyDeviceAdapter.cs; RebirthCamperEnergySnapshotWire.cs; Scripts/Vehicles/Camper/RebirthCamperRadial.cs; Config/RebirthCamperEnergy.xml (comments only); this document. The containment/radial follow-up touches only DeviceAdapter, Radial, configuration comments and this document.

The Travel-status follow-up touches only DeviceAdapter, Energy UI and this document. Relevant source review also confirmed XUiC_RebirthCompassWindow already limits its eligibility observation to Camp.

Compilation: not performed. Tests: not performed. In-game validation: not performed. Source/API and all production wire callers statically reviewed. UI rendering, native output behavior and multiplayer round trips remain unvalidated. Final localization integration, cumulative conformance/build, packaging and completion confirmation belong to the parent task.
