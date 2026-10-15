# Camper final source gap cohort

Audit-only2026-10-15. Eleven previously uncredited current full source bodies,419 lines, hashes below. Reconciled PROJECT_REMAINING_SOURCE_RECONCILIATION and narrative traces: CookingQueueAdmission44 was already explicitly FULL in COOKING_AGENT_TRACE; PatchInstaller47 FULL in CAMPER_PHYSICAL_AGENT_TRACE. Both reread as support, excluded from new unique coverage. No production/build/game/tests/Git. Existing physical/station/transfer traces retain their authority qualifications.

## Installer, native and authored reachability

Fresh_Init52 explicitly invokes CamperPatchInstaller; its per-step installed integer patches InitialDye, CosmeticMigration, TerrainLog, RearLights and BedPose, preserving successful classes on later failure. Current ModulePatchInstaller explicitly patches CargoSize/CargoSlots, PortablePlacementGuard and ModulePreferredTransfer before registration. These are active production classes outside the diagnostics-only conditional GameName check. PatternAppearance is called by CamperRearLights25; LampIntensity by CamperRearLights38/41 and standard RearLights37. Dedicated-server/render gating belongs existing caller trace, not helper name.

XML excerpts camper_items4/6 enable RebirthCamperRandomInitialDye and canHaveCosmetic/vehicle pattern tag; camper_pattern_item_modifiers4 defines modVehiclePatternDisco with matching installable tag/cosmetic type and no initial random chance. CraftingStation resolves actual native ItemClassBlock→BlockWorkstation→CraftingManager.WorkstationData; descriptor uses native workstation_<block> fallback, configured override, forge_nosmelting when native DisableSmelter, and valid RebirthCraftingStation recipe alias. Descriptor grants no access and creates no tile entity. Actual caller WindowSession.OpenCore/server PlayerSource previously fully reviewed; native input template is server-derived there, not client arbitrary window.

Read-only installed Assembly-CSharp excerpts verified ItemValue native constructor allocates one cosmetic slot from CosmeticItemTags; Clone constructs new ItemValue but then overwrites cosmeticMods with cloned original array and Seed with original. ReadData restores saved cosmetic count as null when0. Native BasePartStack.HandleMoveToPreferredLocation is nonvirtual; validates CanRemove then directly AddItemToBackpack or toolbelt, empties UI slot and invokes slot callbacks. The installed ModulePreferredTransfer prefix recognizes only XUiC_RebirthCamperModuleSlot and routes TransferPreferred, suppressing this unsafe clone-native fallback; other basepart slots unaffected. Original transfer authority/custody is in prior management traces, not supplied by prefix alone. Native assembly SHA256 reused FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705.

## Cargo serialization and physical storage

CargoSize both peers derive exact synchronized module metadata and identity; legacy missingModules leaves native dimensions. Missing/empty identity, malformed manifest or failed restoration throws rather than resizing/erasing. Cache keyed both encoded module string and identity; capacity from restored storage, one row. CargoSlots SetBag scopes exact CurrentVehicle Camper and ReferenceEquals(vehicle.bag,offered); bounds native ItemGrid capacity, grows item controllers only when insufficient by native item_stack repeater parse/AutoBindComponents/Init/AutoBindEvents then recollects controllers. Postfix updates rows using actual columns. It neither clones inventory nor debits it. Native sort/locking remain BagContainer. Dynamic creation exception can leave partially initialized view children; no retry rollback implemented, but native parse failure was not induced and no item loss inferred. Existing cells are retained for subsequent larger/smaller bags; native SetBag binding governs excess-cell visibility, not independently fully decompiled here.

ModuleItemCandidate.Prepare requires game thread/exact Camper item, valid nonnull existing metadata with byte entry limit, matching storage/station identities. Captures both manifests into RVM1+vehicleGUID+lengthframes, clones item and sets module metadata. TryRestore requires trusted identity and exact Camper, validates magic/GUID/complete nonempty frames/no trailing bytes, restores both into temporaries before assigning outputs. No live vehicle/inventory mutation. Base64 decode happens before any explicit aggregate decoded-size limit in this helper; downstream native metadata/transport and installation codecs limit normal sources. Do not assert remotely exploitable allocation absent a reachable oversized input topology.

Actual consumers CargoSize, ModuleRuntime constructor and CampStationAppearance restore; ModuleRuntime.Persist excerpt clones updated item, sets identity, prepares candidate, then writes only metadata into current native item and SendSyncData flags. It deliberately avoids full SetItemValue reinitializing fuel/health/seats. Transfer coordinator owns original item/cargo commit and saved proof, already audited; helper alone is neither atomic commit nor trusted sender validation. Source item retained on decode refusal. Persist caches revision only after sync call; no new save guarantee claimed from helper.

## Cosmetics/rendering and portable placement

InitialDye exact authored Camper item/property, game-thread constructor only, preserves any occupied cosmetic, requires exactly one slot, deterministically chooses first available palette dye from seed. Native Clone overwrites temporary constructor cosmetic; native load restores serialized arrays then CosmeticMigration only creates a missing empty slot, never grants/rerolls dye. This behavior runs for new items on any native constructor caller; it is new-item default, not server permission to modify owned existing dye. Migration ReadData can run at native load boundaries and has no inventory transfer or global cosmetic change.

Pattern scans native updated cosmetic slot for exact modVehiclePatternDisco and returns none/disco; no custom persisted selector or network request. PatternAppearance weak per-entity cache finds two authored renderers, resets if model reference changes, sets only _PatternEnabled MaterialPropertyBlock while preserving existing property block. Missing material/property is tolerated. Same-model child/material replacement is not detected once pattern unchanged; no runtime asset replacement proved. It does not alter dye/shared material/physics. LampIntensity pure finite-state mapping: broken/nonfinite velocity0; braking/reverse.65; headlight.12; marker engine.08; values do not set native driving or brake authority.

PortablePlacementGuard checks presence of portable workstation metadata key before native ItemActionPlaceAsBlock ExecuteAction for both press/release, including malformed payload. This prevents converting stored station state into an empty terrain tile entity and delayed native debit. Only release callback shows local install prompt. Any item bearing that key is denied terrain placement; legitimate install uses separate current transfer validation. No parsing of untrusted metadata is needed to deny; no place-block call or inventory mutation occurs here. Exact native placement debit body remains prior portable custody evidence, not newly decompiled in this small helper tranche.

No new confirmed gameplay defect promoted. Coverage is eleven files; the remainder of Vehicles is not declared complete. No visual/native play, exceptional parse failure, full native BagContainer teardown or payload stress test executed.

## Current full-read manifest

| Source | Lines | SHA256 |
|---|---:|---|
| Scripts/Vehicles/Camper/RebirthCamperCargoSize.cs | 44 | DF2DCE0A4956E8297550739808C716E488D9E13CDB4CEB3054ADCE04DF7885A3 |
| Scripts/Vehicles/Camper/RebirthCamperCargoSlots.cs | 57 | B9F9D6DBB40F88A04A24748D596C9E6F2FA262570C717A55CC5E975D6E5E366E |
| Scripts/Vehicles/Camper/RebirthCamperCosmeticMigration.cs | 21 | B09ED1F2A173AE941A631855C332672449C3ED0746BEEBFF84FE3358CEC626C1 |
| Scripts/Vehicles/Camper/RebirthCamperCraftingStation.cs | 59 | 6144137F09354A3779144811685A35F438629E381D698943F26A41E99101BB1A |
| Scripts/Vehicles/Camper/RebirthCamperInitialDye.cs | 41 | 521A527AA997DA42D40D0BC7F597389A762190DFBD54E6D843543A30CF9BF3CE |
| Scripts/Vehicles/Camper/RebirthCamperLampIntensity.cs | 10 | D34A54E1707E5DB0DBA3084FB05ABD20599F126F52153D3295A98A0D94873D7E |
| Scripts/Vehicles/Camper/RebirthCamperModuleItemCandidate.cs | 86 | D2743CC8EAEDF1F9C25A8D1EA3C8A8849DEAF8F22D68D13BAB4A7D7718D96188 |
| Scripts/Vehicles/Camper/RebirthCamperModulePreferredTransfer.cs | 15 | 6E73363C93A6FC02B4CE0EC4AFFAC6394B3314273DD0304846A6F947CCA10CFA |
| Scripts/Vehicles/Camper/RebirthCamperPattern.cs | 24 | 764DB5D02DF6EFD79E3A7755BDDECFEDBCB5DA60AF3E882E599105861AA955F0 |
| Scripts/Vehicles/Camper/RebirthCamperPatternAppearance.cs | 42 | F985128858655B0E4BEA44164FC08A34549CE2C1AE2D9304DFF8CCC2FE73694C |
| Scripts/Vehicles/Camper/RebirthCamperPortablePlacementGuard.cs | 20 | 02F0F8676EBB34C0D4BBE9F7F1A1000BEBD95104E70E8B538CAB47A187C65F23 |

Verified manifest sum: 419 lines. Hashes identify current bodies after literal reads; supplemental callers/native/XML excerpts do not imply full containing-file review.


