# Camper requirement traceability — final local qualification

All listed implementation requirements pass within the stated evidence scope. Packaging completion is a separate parent-owned final check. Final runtime build: `bf7894a`, zero build errors; later `572d3ee` changes test scripts/docs. Final gameplay error review: zero errors.

Audio: native event paths were exercised; no audible playback recording/listening proof. Native-mode counterpart, remote peers and exhaustive reload/all-angle tests are scope limitations, not newly imposed requirements.

## 1. Radial commands refresh when camper modifier is installed or removed

**PASS — bounded local gameplay + source/offline.**

Files: `Scripts/Vehicles/Camper/RebirthCamperRadialCache.cs`, `Scripts/Vehicles/Camper/RebirthCamperRadial.cs`.

Members: `NeedsRebuild`, `Prefix`.

Persistence / XML / UI / localization / networking impact: Existing native activation cache; no save-schema change.

Validation: Source-linked cache fixture; native radial used repeatedly after modifier/mode changes.

## 2. Fresh camper start is prepared before entering world

**PASS — source/offline + owner observation.**

Files: `Scripts/Purge/Runtime/RebirthPurgeTruckStart.cs`, `Scripts/Purge/Runtime/RebirthPurgeStartFootprint.cs`, `Scripts/Purge/Runtime/RebirthPurgeForestRoad.cs`, `Scripts/Purge/Network/NetPackageRebirthPurgeStartReadiness.cs`.

Members: `RebirthPurgeTruckStart`, `RebirthPurgeStartFootprint`, `RebirthPurgeForestRoad`.

Persistence / XML / UI / localization / networking impact: Startup readiness and existing network handshake; existing saves not reset.

Validation: Native startup sequence review, offline readiness/physics checks; owner confirmed truck-only initial grounded spawn.

## 3. Preserve original camper, wheels, underbody and right rear tire; add only missing pickup section

**PASS — offline asset preservation + visual approval.**

Files: `Resources/rebirth_camper80s.unity3d`, `Tools/VehicleStudio/UnitySources/RebirthPickupRearCornerRepair.cs`.

Members: `rebirthCamper80s prefab`, `RebirthPickupRearCornerRepair.Run`.

Persistence / XML / UI / localization / networking impact: Asset-only additions; original physics and wheel assets preserved.

Validation: Compiled semantic comparison PASS 181 components including 31 mesh colliders; 8 readable wheel LODs position/UV/normal delta0. Four nonreadable base-wheel buffers not numerically decoded; source assets untouched.

## 4. Approved tailgate shape smooth and aligned; tight rear bumper follows truck and front finish

**PASS — owner approval + offline GPU review.**

Files: `Resources/rebirth_camper80s.unity3d`.

Members: `pickup tailgate and rear bumper meshes/materials`.

Persistence / XML / UI / localization / networking impact: Approved geometry; original bumper finish contract.

Validation: Owner approved clay shape; scoped Unity renders and semantic preservation checks.

## 5. Original plate retained and readable; rear lamps seated inward without protruding parts

**PASS — offline rendered/source review.**

Files: `Resources/rebirth_camper80s.unity3d`, `Scripts/Vehicles/Camper/RebirthCamperRearLights.cs`.

Members: `original plate UV`, `rear lens mesh placement`.

Persistence / XML / UI / localization / networking impact: Existing plate identity retained; no save/localization changes.

Validation: Original Texas plate readable in reviewed renders; lamp housing and original round bezels retained.

## 6. Tailgate, sides and formed corners follow dye; floor remains black

**PASS — offline GPU + bounded visual gameplay.**

Files: `Scripts/Vehicles/Camper/RebirthCamperPickupPaint.cs`, `Scripts/Vehicles/Camper/RebirthCamperPatternAppearance.cs`, `Resources/rebirth_camper80s.unity3d`.

Members: `RebirthCamperPickupPaint.Apply`, `PickupOnlyPaint subtree`.

Persistence / XML / UI / localization / networking impact: Scoped material-property tint; existing dye item metadata. Black floor excluded.

Validation: Red/blue/undyed GPU comparisons; floor material unchanged; parent viewed final patterned camper screenshot.

## 7. Close both tall rear inner-bed corner holes without welding shut gate seam

**PASS — offline geometry + GPU.**

Files: `Tools/VehicleStudio/UnitySources/RebirthPickupRearCornerRepair.cs`, `Resources/rebirth_camper80s.unity3d`.

Members: `RebirthPickupRearCornerRepair.Run`.

Persistence / XML / UI / localization / networking impact: Two closed dyed jamb meshes; no new physical colliders.

Validation: Exactly2 closed capped jambs, welded-edge manifold checks;378/378 rays blocked; both inside-corner before/after GPU views reviewed.

## 8. Pickup addition hidden when camper body added and restored when removed

**PASS — bounded local gameplay + source/offline.**

Files: `Resources/rebirth_camper80s.unity3d`, `Scripts/Vehicles/Camper/RebirthCamperPatternAppearance.cs`.

Members: `pickup/body visibility application`.

Persistence / XML / UI / localization / networking impact: Existing modifier state; no save-schema change.

Validation: Native modifier installation/removal and body-retained pickup/re-place; existing visibility source review.

## 9. No camper-installed bounce, spin or sinking; preserve usable hood and ladders

**PASS — bounded local gameplay + source/offline.**

Files: `Scripts/Vehicles/Camper/RebirthCamperOwnCollision.cs`, `Scripts/Vehicles/Camper/RebirthCamperRearLadders.cs`.

Members: `Apply`, `Invalidate`, `BindHoodSupports`.

Persistence / XML / UI / localization / networking impact: Own actor collider pair exclusion and native physical access; no visible model retune.

Validation: Offline driven proxy-included comparisons; local forward16.41m, steering31.02m, reverse/dismount; Travel/Camp access14steps each; pickup234 to235 retained body, six equal parked poses and zero velocity.

## 10. Pipes, grilles and ladders match approved brown armor detail; plow faces retain proper shading

**PASS — offline GPU + bounded front gameplay visual.**

Files: `Tools/VehicleStudio/UnitySources/RebirthCamperBrownMetalMatch.cs`, `Tools/VehicleStudio/UnitySources/RebirthCamperPloughAORepair.cs`, `Resources/rebirth_camper80s.unity3d`.

Members: `BrownMetalMatch.Run`, `PloughAORepair.Run`.

Persistence / XML / UI / localization / networking impact: Scoped maps/UV/material clones and AO; original armor/geometry unchanged.

Validation: Same-light brown-reference preview accepted; offline geometry AO/GPU checks; parent viewed actual front brown plow and full camper pattern. Rear supports not exhaustively photographed.

## 11. Station overview icon gaps, station/working icons, metric/button vertical centers and native-like background

**PASS — source/static XML qualification.**

Files: `Config/_Vehicles/camper_station_picker_windows.xml`.

Members: `workshopSummary`, `workshopMetricIcon0/1`, `stationFuel/Queue/Output0..19`, `stationUseCaption`, `camperOverviewBackground`.

Persistence / XML / UI / localization / networking impact: XML-only; localized IDs/input callbacks retained; no persistence/network changes.

Validation: Actual XML geometry checks all20cards, native atlas alpha inventory, UILabel pivot text-placement code and sibling background comparison PASS. No fresh night gameplay screenshot.

## 12. Relay endpoints usable by native wire tool and power external electrical blocks

**PASS — bounded local gameplay + source/offline.**

Files: `Scripts/Vehicles/Camper/Energy/RebirthCamperRelayWiring.Input.cs`, `Scripts/Vehicles/Camper/Energy/RebirthCamperRelayWiring.Authority.cs`, `Scripts/Vehicles/Camper/Energy/RebirthCamperRelayWiring.Power.cs`.

Members: `relay wiring input/authority/power routes`.

Persistence / XML / UI / localization / networking impact: Existing authoritative link protocol and saved links maintained.

Validation: Real wire selection/link: external spotlight powered5W with1link; settled unlink0links/0supply/device off.

## 13. Wire selection uses native hanging wire and connect sounds, no invented success toast

**PASS — bounded local gameplay + source/offline.**

Files: `Scripts/Vehicles/Camper/Energy/RebirthCamperWireFeedback.cs`, `Scripts/Vehicles/Camper/Energy/RebirthCamperRelayWiring.Input.cs`.

Members: `native WireNode feedback lifecycle`.

Persistence / XML / UI / localization / networking impact: Client-only preview/audio; no new link save/network schema.

Validation: wire-live PASS preview, same-relay cancel, tool-swap cleanup; native wire_mesh/WireNode/live/dead event paths exercised. Audio not listened/recorded.

## 14. Relay half-size placement centered in stripe and fade follows stations only during deployment

**PASS — bounded local gameplay + source/offline.**

Files: `Scripts/Vehicles/Camper/Energy/RebirthCamperRelayGeometry.cs`, `Scripts/Vehicles/Camper/Energy/RebirthCamperRelayVisuals.cs`.

Members: `relay geometry and visual Apply lifecycle`.

Persistence / XML / UI / localization / networking impact: Visual-only; endpoint activation follows Camp.

Validation: 20numeric opacity samples and transition endpoint counts; original geometry/lifecycle fixture.

## 15. Slow driving recharges native battery cells; parked engine-off does not

**PASS — bounded local gameplay + source/offline.**

Files: `Scripts/Vehicles/Camper/Energy/RebirthCamperBatteryCharging.cs`, `Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyRuntime.cs`, `Config/RebirthCamperEnergy.xml`.

Members: `EngineRunning`, `native battery charging tick`.

Persistence / XML / UI / localization / networking impact: Bounded configured alternator; existing ItemValue charge persistence/publication; no save schema.

Validation: charging-final-bf7894a PASS actual slow movement with62UseTimes charge gain; engine-off isolation unchanged.

## 16. Daytime solar cell production funds loads first then charges batteries with surplus

**PASS — bounded local gameplay + source/offline.**

Files: `Scripts/Vehicles/Camper/Energy/RebirthCamperBatteryCharging.cs`, `Scripts/Vehicles/Camper/Energy/RebirthCamperEnergySourcePlan.cs`, `Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyRuntime.cs`.

Members: `AvailableSolar`, `DebitSolar`, `source budget and native charging`.

Persistence / XML / UI / localization / networking impact: Native cell quality/acceptance ceilings; shared budget prevents double spend; existing source sync.

Validation: Full charging driver PASS69UseTimes solar gain, night isolation unchanged; actual internal turret shot externalRelayLinkCount0. Not exact solar calibration.

## 17. Camp/Deploying driver cannot start engine or consume fuel

**PASS — bounded local gameplay + source/offline.**

Files: `Scripts/Vehicles/Camper/Energy/RebirthCamperCampEngine.cs`, `Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyDrivingInput.cs`.

Members: `Camp engine/input guards`.

Persistence / XML / UI / localization / networking impact: Native engine guarded by mode; existing mode networking.

Validation: Deploying10samples and Camp driverseat: both native running flagsfalse, fuel37.52 unchanged. No historical oneshot audio proof.

## 18. Suppress Duke note pin only REBIRTH progression

**PASS — bounded local gameplay + source/offline.**

Files: `Scripts/UI/RebirthDukeNoteNotification.cs`, `Scripts/UI/RebirthToolbeltCapacity.cs`.

Members: `MinEventActionPinToolbeltMessage.Execute prefix`, `SanitizePlayerStartup`.

Persistence / XML / UI / localization / networking impact: Exact buffDukeNoteToolTip; existing progression policy, no localization/network schema changes.

Validation: Source fixture29 assertions; REBIRTH actual native buff/UI absence PASS. Native-mode passthrough offline only.

## 19. CCTV shows complete local player head and restores prior player view

**PASS — bounded local gameplay + source/offline.**

Files: `Scripts/Vehicles/Camper/Turrets/RebirthCamperCctvPlayerView.cs`.

Members: `native first-person save/restore lifecycle`.

Persistence / XML / UI / localization / networking impact: Temporary native view state, no persistence/global layer mutation.

Validation: Four-camera claim/release PASS; complete heads actually viewed in corner0 and2 boundary captures; fixture8.

## 20. Remove distracting CCTV manual-shot smoke, preserve sound/damage/tracers and automatic effects

**PASS — bounded local gameplay + source/offline.**

Files: `Scripts/Vehicles/Camper/Turrets/RebirthCamperTurretManualShot.cs`.

Members: `manual-shot context and native Fire restoration`.

Persistence / XML / UI / localization / networking impact: Manual targetId<0 only; scoped native effect suppression, native sound event retained.

Validation: Source fixture13; successful manual damage/ammo shots and reviewed faint beam/no prominent wedge. Native sound path exercised, not audibly recorded.

## 21. Manual single-ray CCTV shots accurate; brief clicks not lost or latched

**PASS — bounded local gameplay + source/offline.**

Files: `Scripts/Vehicles/Camper/Turrets/RebirthCamperTurretManualShot.cs`, `Scripts/Vehicles/Camper/Turrets/RebirthCamperManualPress.cs`, `Scripts/Vehicles/Camper/Turrets/RebirthCamperManualInputCadence.cs`, `Scripts/GameBridge/RebirthBridgePulseClock.cs`.

Members: `getSpread manual context`, `manual press/cadence`, `first native injection clock`.

Persistence / XML / UI / localization / networking impact: Single-ray only; shotgun/automatic semantics retained; existing server control route.

Validation: headshot-sniper-bf7894a-243 PASS10/10, one round and19health each, positive native input witness. Native hit region not measured. AI freeze verified before and cleared after.

## 22. Use native beam without distracting camera wedge and maximize safe four-corner aim

**PASS — bounded local gameplay + source/offline.**

Files: `Scripts/Vehicles/Camper/Turrets/RebirthCamperTurretLaser.cs`, `Scripts/Vehicles/Camper/Turrets/RebirthCamperLaserNearField.cs`, `Scripts/Vehicles/Camper/Turrets/RebirthCamperLaserCameraClip.cs`.

Members: `native laser presentation and camera near-field clipping`.

Persistence / XML / UI / localization / networking impact: Camera-scoped presentation; existing turret authority/range.

Validation: All4bounded sweeps PASS;16actual boundary images viewed, no camera-own barrel across center. Peripheral hardware/world foliage remains; unloaded corners do not independently prove firing.

## 23. Turret OFF yellow / ACTIVE green; centered mode icon/caption and CCTV labels

**PASS — bounded local gameplay + source/offline.**

Files: `Scripts/Crafting/Cooking/XUiC_RebirthCookingAction.cs`.

Members: `StateFillColor`, `turret actionFill and caption bindings`.

Persistence / XML / UI / localization / networking impact: Optional action fill override; existing localized labels/protocol preserved.

Validation: Four native toggle roundtrips; actual all4greenACTIVE and all4yellowOFF PNGs viewed; ASSAULT/SNIPER/CCTV caption images viewed centered/unclipped.

## 24. Reverse rear lighting illuminates ground while keeping original lamp identity/color

**PASS — bounded local gameplay + source/offline.**

Files: `Scripts/Vehicles/Camper/RebirthCamperLampIntensity.cs`, `Scripts/Vehicles/Camper/RebirthCamperRearLights.cs`.

Members: `native rear lamp intensity policy`.

Persistence / XML / UI / localization / networking impact: Runtime light profile only, original lamp meshes retained.

Validation: reverse-paved-fueled-final-245 PASS actual fuel39.76, native reverse-2.969;3.25intensity/6m/60deg; reset.12/4m/48deg. Actual brightREDground PNG viewed; no all-angle lighting claim.
