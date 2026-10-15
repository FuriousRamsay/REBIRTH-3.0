# Camper installed equipment model visibility — 2026-10-14

Source: current cumulative REBIRTH workspace after physical-target/fade integration, originally checkpoint 7ecb720. Target: current installed game and matching authored taller camper bundle. User requirement: only models corresponding to stations actually installed in crafting management should appear. Delivery type: parent-coordinated changed-files package. No game interaction occurred during the user's current test.

## Existing state paths reviewed

The authoritative RebirthCamperModuleRuntime owns Stations; TryGetExisting reads that existing owner without constructing a new owner or mutating vehicle state. StationInstallations.Revision advances for installation/removal. Each occupied slot supplies InstalledItem; CraftingStation.TryGetPresentation exposes its actual InstalledBlockName, shared with existing physical station resolution.

RemoteStationClient.InstalledForNavigation returns the snapshot of an active station UI session. It is unavailable while looking at a camper in the world, so it cannot serve world-model presentation. Existing native vehicle item metadata carries RebirthCamperIdentity and RebirthCamperModules. ModuleItemCandidate.TryRestore decodes a detached snapshot of those exact module manifests; it does not adopt or mutate the live owner. The appearance controller uses this existing metadata outside UI sessions, with cached frame/identity values. This is cosmetic observation only and grants no interaction authority.

EnergyRuntime.TryObservePresentation exposes current/synchronized energy revision and mode without constructing a full source snapshot. EnergyRuntime.Observe supplies actual installed Source.Kind values. Existing sources distinguish Generator, Battery and Solar independently of whether they currently produce power.

## Implementation

CampStationAppearance binds each of the 19 equipment targets' immediate parent model, as specified by CAMPER_CAMP_INTERACTION_TARGETS_20261014.tsv. The binding additionally requires the model to be an immediate child of the corresponding roof/interior/power group, preventing an unexpected target hierarchy from hiding the whole camper or an entire group. Rear-door targets are excluded from equipment selection.

The sixteen crafting models appear only when an actual installed item's exact block identity matches the existing physical-target mapping. One existing map in PhysicalInteraction.TryGetStationBlockName serves both routing and appearance; no separate duplicate station catalogue is introduced. Multiple installations of one supported type would still yield one authored visual model. Missing or unsupported installations do not create model or station substitutes.

The three power models independently follow actual installed energy Source.Kind. A source that is installed but switched off, empty or unpowered remains visible. A removed/absent source's model and physical target disappear. This extends the same installation rule to power equipment; it does not treat power equipment as crafting management slots.

Groups remain containers for the existing fade. Per-model active state now additionally requires that equipment to be installed. Installed models retain Deploying/Packing opacity and Camp visibility; Travel hides them. Stair visuals retain their existing transition presentation. Door, permanent guardrails, authored walk-in shell, travel hull replacement and manual door replication remain governed by their prior rules. Interaction targets require Camp and active hierarchy, so a hidden absent model's target is disabled too.

Installation observation is limited to once per 0.25 seconds. Live station type discovery runs only when the live station collection/revision changes. Native metadata is decoded only when its frame or identity changes; unchanged frames are retained. Missing/invalid metadata clears cosmetic station observations until a valid new frame arrives, preserving source recovery bytes. Full energy snapshots are acquired only when observed energy revision changes. Neither full module nor energy snapshots are acquired every rendered/physics frame. Renderer opacity continues updating through the existing hooks.

## Requirement traceability

| Requirement | Implementation | Persistence/XML/UI/localization/networking impact | Static review |
|---|---|---|---|
| Only installed crafting models appear | CampStationAppearance.Equipment, ObserveInstallations, Update; PhysicalInteraction.TryGetStationBlockName | Reads existing installed types; no payload, item, UI or save changes | PASS |
| React to installation/removal locally | Existing Stations reference + Revision observed at bounded cadence | Existing authoritative owner retained | PASS |
| Remote world models follow synced installation state | Cached RebirthCamperModules/Identity decoding | Existing native observer metadata only; no new packets | PASS |
| Hide absent targets | Update target enabled gate and per-model active hierarchy | Existing admission validation preserved | PASS |
| Preserve transition fade and door/physics behavior | Existing Visibility/MoveTowards, group fade, unchanged door/collider mode gates | Cosmetic per-model gate only | PASS |
| Installed power sources select their models | ObserveInstallations energy revision and Source.Kind | Existing energy state only | PASS |

Files changed: Scripts/Vehicles/Camper/RebirthCamperCampStationAppearance.cs; Scripts/Vehicles/Camper/RebirthCamperPhysicalInteraction.cs; this documentation. Earlier physical-prompt/fade corrections remain in the same production files and are documented separately.

Limitations: observer visibility follows normal metadata synchronization latency plus at most the 0.25-second observation interval; it is not an immediate network acknowledgement. Missing/invalid observer module data hides equipment instead of inventing installations. Authored models exist only for the supplied sixteen crafting types and three power kinds; this work does not generate artwork for other supported management installations. Existing native metadata is used cosmetically and cannot authorize station use.

Compilation: not performed. Tests: not performed. In-game validation: not performed. No launch/control, build, install, git operation or package generation was performed by this subtask. Parent owns final cumulative build, packaging and completion confirmation.
