# Camper body modifier, dye, rear lighting and targeting follow-up — October 21, 2026

Source cumulative project: existing REBIRTH3.0 mod workspace at C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0. Target: same cumulative project for7 Days to Die3.0. Changed-files delivery only; no full cumulative archive.

## User observations and scope

The owner explicitly clarified that the latest truck-only initial spawn was correctly grounded. Bouncing and sinking began when the camper body modifier was added, and continued after picking up/placing the body-equipped vehicle. This is distinct from the earlier airborne-start report. The camper-only physical regression must be fixed at collision ownership, rather than attributed to the initial spawn or corrected by retuning the working truck-only rig.

Additional reports: new white pickup bed panels/tailgate do not follow dye; pickup-only rear brake/reverse lights do not illuminate; camper right-side exterior/rear door lack ordinary native interaction. Camp mode's stopped/leave-seat refusal appeared while the vehicle was physically unstable. Its stability guard is retained; the physics defect must be corrected rather than bypassing that guard.

The approved bodywork geometry, wheel meshes/UVs/materials, original plate and black bed floor are preserved. No agent launched or controlled the game. Parallel agents investigated physics, native targeting/lighting and dye, with parent integration.

## Confirmed physical defect and correction

The earlier CamperHull belonged to Physics, sharing the native vehicle Rigidbody. OwnerCorrections.BodyRoots moved it under the cosmetic camper module. Activating the modifier then activated an independent solid overlapping the dynamic CabHull by1.155345m. Twenty-two detached rear ladder capsules also intersect the restored hull.

Runtime Appearance now restores CamperHull to the original Physics compound with world pose unchanged, explicitly enables it only for body-installed travel, and suppresses only own-vehicle pairs between detached body solids and that vehicle's compound. Their external collision remains intact. No suspension, mass, wheel or truck-only geometry tuning is introduced.

The controlled Unity bodyON test reproduced peakheight3.737m, speed9.013m/s and approximately21m drift. Restoring hull ownership alone left6.35m drift; restoring ownership plus own-pair filtering returned the result to the truck-only baseline, approximately0.00006m drift. See BODY_MODIFIER_PHYSICS_REGRESSION_20261021.md and BodyPhysicsAudit_20261021 receipts for conditions and limits. This reproduces a camper-only physical defect; it does not reproduce the earlier158m launch exactly.

## Native targeting

Native vehicle selection requires E_Vehicle and CollisionCallForward resolving to the owning EntityVehicle. Init wires Physics descendants, while the cosmetic/deployed shell had no such contract. RebirthCamperNativeTargets binds all owned solid colliders, including inactive Travel/Camp shapes, without changing their layers, geometry or enabled state. Trigger equipment targets, wheel colliders and nested actors are excluded. Existing special-part occlusion/permissions are preserved. The production-source fixture passes57 checks. See RemainingFixes_20261020/NATIVE_BODY_TARGETING_20261021.md.

## Remaining integration

Dye and rear-light corrections are being validated. Final source list, build/install receipts, package manifest and completion assessment will be recorded before delivery. No live gameplay pass is claimed.

## Final integration and validation

The added pickup paint now receives the native byte-range dye directly through a scoped renderer property block, including recovery from stale shared materials or cleared blocks. Only REBIRTHPickupOnlyPaint is bound; the floor, wheels, original parts, plate and bumper are excluded. The shader and installed asset bundle were not changed. Both source and installed-bundle GPU checks show the original shader already supports the correct colors; the exact native runtime ordering that left the additions white was not reproduced. Explicit synchronization removes reliance on one-time material inheritance. Production helper tests:97 assertions. Installed-mesh red/blue/undyed GPU checks retain the identical black floor. See PICKUP_DYE_SYNCHRONIZATION_20261021.md.

Pickup rear lenses inherited a legacy diffuse-only material and had no controller binding. The correction clones the existing native emissive lamp material per model, binds all30 approved lens/flute renderers, and provides separate red running/brake and white reversing channels with six segment lights. Camper-only beams follow the visible camper lens, and repeated headlight-part binding reuses owned assets. No lens geometry or UV changes. Native lamp-state checks pass; GPU checks on actual installed red and clear lens meshes confirm the appropriate emission increases. See RemainingFixes_20261020/PICKUP_REAR_LIGHTS_20261021.md. Full in-game beam appearance remains unverified.

Combined Debug build:19 warnings,0 errors. Parent repeated62 interaction/actual-geometry checks,97 paint checks and the lamp-state test successfully. The earlier initial-start source correction is included in the installed DLL and changed-files package;28,910 fixture assertions and20 static contracts passed. This does not reclassify the owner's latest grounded truck-only start as failing.

## Compatibility and maintenance

No serialized fields, modifier IDs, item formats, network payloads, option IDs, XML, localization or bundle assets change in this follow-up. Runtime binding repeats for a replaced model and works on existing saves/new placements. Authority and native physics ownership remain unchanged. Camper-only collision filtering excludes external vehicles, actors and world solids; no global layer ignore or permanent kinematic/freeze is introduced. Cached collider arrays avoid per-visit collection allocations; the22x40 own-pair verification remains deliberately conservative across Unity activation resets.

Manual scenarios are included for modifier installation/removal, normal placement, driving, Camp/Travel, reload, remote ownership, both side/rear interaction, dye replacement/removal and night brake/reverse lamps. They are NOT RUN IN GAME. No test save or user save was edited. The Camp stability admission remains intact.

## REBIRTH FEATURE COMPLETION CONFIRMATION

Feature: camper body physics restoration, native body targeting, added-panel dye and pickup rear lamps; initial-entry admission follow-up included.
Source cumulative project: existing zzz_REBIRTH__3_0 workspace identified above.
Target project/version: same REBIRTH3.0 cumulative project for7 Days to Die3.0.
Design requirements reviewed: PASS
Relevant existing documentation reviewed: PASS
Accepted architecture preserved: PASS
Code requirements: PASS
XML requirements: N/A
UI requirements: N/A
Localization requirements: N/A
Images/assets requirements: PASS — approved assets preserved; runtime lamp material owned separately.
Serialization/persistence requirements: PASS — native existing state retained.
Networking/synchronization requirements: PASS — existing owner/observer state retained.
Documentation requirements: PASS
Packaging requirements: PASS — changed-files archive and SHA-256 manifest verified at delivery.
Compilation: Performed
In-game validation: Not performed
COMPLETION VERDICT: COMPLETE
Basis for verdict: required runtime code paths, scoped appearance/physical changes, lifecycle integration, regression fixtures and documentation are present. This is implementation conformance, not a claim that manual gameplay passed or that the earlier158m launch impulse was isolated. Installed file hashes and package file inventory are recorded in Followup_20261021.
