# Camp entry controller step offset

Source/target: current cumulative REBIRTH workspace, checkpoint identified by the parent task as 7ecb720; installed native game source decompiled into E:/_Haven/shared/REBIRTHTemp/CamperStandingClearance_20261014. Coding-only contribution to the parent's camper test-build delivery; packaging and compilation belong to the parent task.

Read: project operating directives; existing Camp energy requirements; RebirthCamperCampStationAppearance, RebirthCamperCampDoor, RebirthCamperPhysicalTargets and energy presentation observation. Existing native movement, vehicle identity, authoritative Camp state, save format and network paths are preserved.

## Findings and evidence

vp_FPController.SyncCharacterController captures CharacterController.stepOffset in m_StepHeight. FixedMove writes m_StepHeight into the CharacterController when grounded and its forward overhead test is clear; otherwise it writes 0.1. Therefore changing CharacterController.stepOffset in a vehicle Update cannot reliably constrain the movement call. The default m_StepHeight initializer is 0.7, but SyncCharacterController replaces it with the actual prefab/controller value. This coding task did not read a running game's actual value.

entry-isolation.txt under E:/_Haven/shared/REBIRTHTemp/CamperDefectCorrection_20261014 records h=1.8, step=0.7 failing with the continuous stair collider active, and passing after both overhead roof/lintel colliders are disabled. Removing the continuous stair collider also passes. These findings support a step/overhead interaction; they do not establish a width fault. The parent's supplied Unity experiment reports h=1.72 passing with steps 0.1/0.3 and failing with 0.5/0.7. A 0.1 cap is chosen for the test build because it gives more overhead margin on the continuous ramp and already matches native airborne/overhead behavior. Neither supplied comparison establishes that 0.1 resolves every native standing height.

The latest VehicleStudio rebirthCamper80s.prefab was inspected without modification. REBIRTHCampWalkInColliders owns Camp floor, Rear entry floor, Camp floor ramp and Camp stair continuous collision. Floor boxes and the continuous mesh provide the authored approach/interior scope.

## Implementation

RebirthCamperCampEntryController.Install is called explicitly by the module installer. Vehicle Update tracks camper entities; unload removes them; world shutdown clears the registry. Bindings are cached per vehicle and rebuilt when RootTransform or ModelTransform changes. All four expected floor/ramp colliders must exist. Legacy or partially loaded geometry is refused and retried.

A FixedMove prefix checks a live local player in the current world, excludes free flight/death/driver, requires a live Camp vehicle and active solid authored floor/ramp geometry, then tests the actual controller feet in each collider's local space. The horizontal margin is the capsule radius plus 0.2 metres; the vertical margin is 0.4 metres below to 1.25 metres above the floor/ramp bounds. This includes the approach and interior without a world-aligned box growing when the camper rotates. A 15 metre distance filter avoids geometry/state checks on distant tracked campers.

Only a step height greater than 0.1 is reduced. The native FixedMove body selects grounded or airborne behavior as usual. A Harmony finalizer restores the captured m_StepHeight and CharacterController.stepOffset even if the original method throws. No persistent mutation survives a movement call, so leaving Camp, moving away, changing models, unloading or dying cannot leave a reduced setting on the player. No height, radius, skin width, speed, jump, world collider, vehicle collider or stair asset is changed.

## Scope and limits

This is local native player movement integration. Remote clients run it for their own player; dedicated servers have no local player to change. It adds no serialized state, metadata, packet, option, UI or localization. It follows the existing observed Camp presentation instead of introducing new authority. State/geometry is checked each movement, but scene traversal occurs only when binding a new/model-changed camper. Cached mesh bounds assume the authored mesh is immutable for that model instance.

Actual native movement includes anti-bump and overhead detection absent from the isolated Unity movement experiment. The test build still requires an actual entry/exit check, crouch/standing check, rotated camper, ordinary terrain movement before/after, Camp-to-Travel transition, model reload and multiplayer local-client observation. Passing those checks is not claimed here.

## Contribution traceability and verification

| Requirement | Implementation/member | Other impact | Validation | Result |
|---|---|---|---|---|
| Narrow approach/interior clamp | RebirthCamperCampEntryController.Begin, Resolve, NearFloor | No persistence/XML/UI/network changes | Source/prefab inspection | Implemented; runtime unverified |
| Restore normal movement settings | End and RebirthCamperCampEntryMovement.Finalizer | No save migration | Source review of capture/finalizer paths | Implemented; runtime unverified |
| Explicit integration | RebirthCamperModulePatchInstaller.Install | Existing bootstrap preserved | Installer source readback | Implemented |
| Documentation and limitations | This document | Parent delivery packaging | File creation/readback | Implemented |

Compilation: Not performed by this subtask.
In-game validation: Not performed by this subtask.
Completion: coding contribution implemented and statically reviewed; parent task owns feature/build/game/scenario/delivery completion and the mandatory delivery confirmation.
