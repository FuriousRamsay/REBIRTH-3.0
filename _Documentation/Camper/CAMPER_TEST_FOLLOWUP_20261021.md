# Camper test follow-up â€” October21,2026

Source cumulative project: existing REBIRTH3.0 workspace at C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0. Target: same project for7 Days to Die3.0. The owner supplied new game screenshots after testing the earlier installed DLL5EF30A3D... and bundle669013F8.... This document records the additive corrections, not a claim that the earlier delivery passed gameplay.

## Requirements retained

1. Close both inside rear tailgate/bedside corner openings; retain outside approved shape, plate, dye and blackfloor.
2. Bring pipe/grille/ladder material detail up to the armor reference without changing the armor or geometry.
3. Correct improperly black plow faces under angled illumination.
4. Defense CCTV caption is only CCTV; selected-item/backpack backgrounds match established neutral windows.
5. Audible item-placement confirmation for successful Defense and Energy inventory actions, including installed turrets.
6. Mounted camper equipment uses internal power without player relay links; external blocks still require relay wiring.
7. Assault label/icon centered vertically; distinct target icon for Sniper.
8. Entering driver seat in Camp or deployment must not start the engine. Deployment and packing take8seconds each.
9. Fix installed-body driving spin/launch regression; truck-only placement was previously confirmed grounded by the owner.
10. Restore visible usable external relays at the owner's retained placement: oneeachside above battery-bank position, halfnative scale, seated against camper skin.

Three agents own physical/optional-part coverage and UI/sound, asset corners/texture integration, and plow/power/relay work, with parent integration and review. No agent launches or controls the game.

## Driving physics finding

The previous stationary body-hull repair omitted solids outside the cosmetic BodyModule, especially the detached model-root selection collider. The earlier driven offline test also excluded that shape, so it did not qualify driving safety. A repeated proxy-INCLUDED full-modifier driven test with torque, steering and visual-model following reproduces old-policy upward movement (peakY7.140589 from.5), while full-owned pair filtering remains near baseline (peakY.5000055). The test uses Unity14vs21 collision enabled; the exact running-game layer matrix has not been inspected. Thus an omitted owned-contact path is demonstrated, while full native-game behavior still needs owner verification.

RebirthCamperOwnCollision inventories all owned nontrigger solids and filters each against only its own Rigidbody compound, excluding occupants/other actors. It refreshes on model replacement, native driver/modifier changes, turret binding and bounded fallback. No native wheel, suspension, world collision, foreign actor/vehicle collision or Travel constraints are changed. See DRIVEN_PHYSICS_PROXY_REVIEW_20261021.md and CAMPER_BODY_PHYSICS_20261021.md.

## Engine and timing implementation

RebirthCamperCampEngine is a prefix on the exact native VPEngine.startEngine method. It reuses the established observed-mode driving guard: known Deploying/Camp/Packing refuse engine startup BEFORE sound/running state and native start effects; Travel, unrelated vehicles and absent mode observation retain native behavior. Native seat/camera admission remains unchanged. Registration is adjacent existing driving-input patches in ModulePatchInstaller.

Config/RebirthCamperEnergy.xml deploySeconds/packSeconds are8; RebirthCamperEnergyConfiguration defaults are also8. Existing authoritative transition clocks and HUD duration consume those values. Saved state format and identifiers remain unchanged.

Validation performed so far: exact native VPEngine source reviewed;18 production-prefix admission assertions with explicit mode-observation boundary double; existing actual-source Energy fixture256 assertions passes after default/config change. Manual driver entry, native startup sound and exact eight-second HUD transitions are not represented as tested.

## Installed delivery

The final DLL/PDB and approved brown candidate bundle are installed in the cumulative project; installed hashes match the staged artifacts. A backup is retained under E:/_Haven/shared/REBIRTHBackups/CamperTestFollowup_20261021.

## Integrated implementation details

Defense now uses only CCTV in both shipped localization tables and the source fragment. The selected-item and backpack fills reuse64,64,64,255 from the existing vehicle windows. Allfour Assault/Sniper controls have matchingcenter pivots atY−14 in28px rows; Sniper selects the verified native hard-target icon. The native placement-sound path runs after committed inventory publication and deduplicates operation IDs/retired owner holds for Defense and Energy, including remotely acknowledged transfers. Queries, rejected operations, refreshes and toggles do not generate item-placement audio. See UI_COMMITTED_ITEM_FEEDBACK_20261021.md.

Mounted turret consumers no longer depend on player-created relay links. They remain subject to real installed/enabled/manual-control demand, custody, source output and funded native energy accounting. Existing external circuit Connected checks remain in place. Power-warning messages now explain enabling a charged camper source without telling the player to wire an installed turret.26 linked production power assertions pass, including a throwing TurretConnected boundary proving builtin power does not consult wire links. See RemainingFixes_20261020/MOUNTED_INTERNAL_POWER_20261021.md.

Relay creation is retried at most oncepersecond when anchors, readable skin or native assets are not ready. Only a completed pair with valid geometry and endpoints is cached; failed temporary groups are removed. Native relay roots are activated, and the group follows body-installed visibility in bothTravel andCamp. Scale remains.5, two mounts above battery-bank position with plates seated2mm outside skin. The production-source/actual-Unity-transform lifecycle fixture passes11 checks using explicit native-loader/prefab boundary doubles. This verifies loading/retry/group behavior, not actual wire edits in a running game. See RemainingFixes_20261020/RELAY_VISIBILITY_RETRY_20261021.md.

The asset candidate adds two closed60triangle rear corner jambs, with allweldededges manifold;378ray checks cover the old openings. Original visible meshes/physics/black floor preserved. Pipe/grille/ladder detail uses the approved brown armor reference maps at matching physical scale on only those parts; the rejected alternate-steel preview is superseded by the final brown reference correction below. Armor plates and original truck materials remain unchanged. Plow AO is rebaked from actual geometry at1024pixels with32hemisphere rays percoveredtexel, preserving true cavity darkness and allotherPBR maps. Exposed bracketAO0 became.832 rather than suppressing lighting or brightening albedo. Parent visually inspected bothcorner renders, rear ladder/pipe detail and plow afterrender. See PICKUP_INNER_REAR_CORNERS_20261021.md and companion asset audit documents.


## Final brown reference correction

The initial blue-gray SalvagedSteel detail candidate was rejected by the owner and never installed. The final pipe/ladder/support finish instead clones the actual approved plates material and uses unchanged paint-free brown pixels from its albedo, normal, metal/smooth and AO maps. No arbitrary color correction or sharpening is applied. Physical UV density is retained and the crop is scaled accordingly. The same-camera preview was inspected against the visible original brown grille frame and armor plate. All corner, plow-AO and runtime corrections remain in the versioned candidate.

Original installed wheel vs candidate compiled wheel comparison reports identical triangle positions/winding, UVs, normals and material descriptors across the8 readable distance wheel LODs; measured errors are zero. Four non-readable base meshes require the separate final read-only mesh-data qualification recorded in the bundle receipt. The first source-vs-build byte-hash gate encountered non-readable compiled base meshes and therefore did not qualify those buffers, so the installed-to-built geometric comparison is the controlling evidence. The final brown bundle is reopened and compared again before installation.


## Final verification and traceability

Delivery type: changed-files package; no full cumulative archive requested. Source and target are the current REBIRTH 3.0 workspace named above. See TestFollowup_20261021/installation.json for installed hashes. DLL build: 19 warnings, 0 errors. Final compiled bundle physics comparison: PASS, 181 components, 31 MeshColliders, maximum physical vertex delta zero, all wheel suspension/rigidbody/primitive settings identical. The earlier JSON guard compared source serialization to compiled references; the compiled-to-compiled floor-ramp JSON is identical.

Wheel qualification: original source wheel assets were not edited; eight readable compiled distance LODs matched numerically. Four non-readable base wheel buffers remain unqualified numerically: Unity native MeshData.GetVertices crashed the inspection process. This is an inspection-tool limitation, not a passing test. No agent in-game validation was performed.

| Requirement | Implementation / members | Persistence, XML, UI, localization, networking | Validation | Result |
|---|---|---|---|---|
| Close rear corner openings, retain black floor | RebirthPickupRearCornerRepair; candidate prefab jamb meshes | Bundle only; no collider or save changes | Closed mesh edges, 378 gap rays, inspected renders | PASS |
| Brown detailed pipes/grilles/ladders | RebirthCamperSteelSurfaceDetail; RebirthCamperBrownMetalMatch | Candidate material/UV assets; original armor retained | Brown preview inspected against original frame; geometry preserved | PASS |
| Plow black surfaces | RebirthCamperPloughAORepair | Scoped AO map/material clone; original other maps retained | Geometry rays and before/after render | PASS |
| CCTV, backgrounds, centered modes/target icon | camper_turret_windows.xml, localization tables, XUiC_RebirthCamperTurrets | Native UI bindings and existing localization keys retained | 8 UI static checks; atlas icon checked | PASS |
| Placement sounds | RebirthCamperCommittedItemSound, TurretAccess, EnergyOwnerHold | Successful committed local/remote operations only; no persistent schema change | Actual commit paths reviewed; no live audio test | PASS |
| Built-in power independent of wires | RelayWiring.Power TurretLoad/TurretPowered | Existing authoritative energy accounting and external links retained | 26 production-source assertions | PASS |
| Engine off while deployed/deploying; 8 seconds | CampEngine.Prefix, EnergyModel, RebirthCamperEnergy.xml, installer | Existing observed modes and transition sync retained | 18 engine assertions; 256 Energy assertions | PASS |
| Camper-equipped driving stability | OwnCollision.Apply, driven/parts hooks, Appearance, turret invalidation | Own-compound contact filtering only; no saved IDs/native wheel settings changed | 8 static checks, driven offline reproduction and new-policy comparison; compiled physics receipt | PASS |
| Visible external relays | RelayVisuals.Attach/retry and regular Appearance attachment | Existing endpoints/network wiring retained; half scale, above bank | 11 production lifecycle assertions with native boundary doubles | PASS |
| Existing interaction/impact behavior | NativeTargets tag preservation | Existing targeting/native strong-impact tags | 63 fixture assertions | PASS |

## REBIRTH FEATURE COMPLETION CONFIRMATION

Feature: Camper test follow-up corrections
Source cumulative project: current zzz_REBIRTH__3_0 workspace
Target project/version: REBIRTH 3.0 / 7 Days to Die 3.0

Design requirements reviewed: PASS
Relevant existing documentation reviewed: PASS
Accepted architecture preserved: PASS
Code requirements: PASS
XML requirements: PASS
UI requirements: PASS
Localization requirements: PASS
Images/assets requirements: PASS
Serialization/persistence requirements: PASS (existing formats retained)
Networking/synchronization requirements: PASS (existing authoritative commit paths retained)
Documentation requirements: PASS
Packaging requirements: PASS (archive verified against per-file SHA256 manifest)

Compilation: Performed
In-game validation: Not performed

COMPLETION VERDICT: COMPLETE

Basis for verdict: All requested implementation and asset corrections are integrated and installed. Offline checks and compilation are recorded above with their limits; owner gameplay validation remains separate. The four non-readable base-wheel numeric inspection is explicitly unqualified.
