# Camper body modifier physics correction

Target: current REBIRTH 3.0 workspace, 7 Days to Die 3.0 b259. Changed-files integration is owned by the parent task; no independent cumulative archive is produced.

## Cause and correction

The bare pickup was stable in the user's latest test. Installing the camper body activated a solid `CamperHull` under the visual `REBIRTHCamperBodyModule`, detached from the native `Physics` Rigidbody. `RebirthCamperOwnerCorrections.BodyRoots` had moved this shape from its original compound owner. The active static shape penetrated the native dynamic `CabHull` by 1.155345 metres in the independent Unity audit. Detached solid ladder capsules also intersected the restored camper hull.

`RebirthCamperCampStationAppearance.Update` now restores only `CamperHull` to `Physics`, preserving its world pose. It explicitly enables that compound shape when the body modifier is installed and the vehicle is outside Camp mode; the existing walk-in shell still replaces travel hulls in Camp. Bare-pickup wheels, rigidbody, geometry, material, and spawn positioning are unchanged by this correction.

The visual door and ladder pivots remain intact. Cached non-trigger solids under this vehicle's body module, with no Rigidbody owner, ignore only this vehicle's native compound colliders. Other vehicles, terrain, blocks, and player collisions remain eligible. Pair ignores are checked after activation/enable changes because Unity can discard them when a collider is deactivated. There is no global layer collision change, save schema, network package, or added Rigidbody.

The audited Travel inventory after restoration is 22 detached ladder solids and 40 native compound solids, including four wheels: 880 candidate own-pair checks per appearance visit, with no per-visit collection allocation. A fingerprint-only optimization was not applied because it can miss an off/on cycle completed between visits and leave a cleared ignore pair unrepaired. This bounded cost should be profiled in the integrated game before introducing lifecycle caching.

## Evidence and limits

An independent Unity simulation reproduced body-only instability: broken body-on peak Y 3.737417, peak speed 9.012859, horizontal displacement about 21 metres. Restoring compound ownership alone removed the upward impulse but still drifted 6.34854 metres because of detached ladder contacts. The combined correction returned to the bare-body baseline: peak Y 0.5000045 and final position approximately (0.00005, 0.01341, 0.00002). This is causal evidence for the body-only regression; it does not claim to reproduce the earlier 158-metre native-game launch exactly.

Staged Debug compilation passed with 19 warnings and zero errors. The exact game retest is recorded separately in `Tools/VehicleStudio/tests/camper_body_physics_scenario.json` and remains NOT RUN by this agent. Parent task owns final integration/build and live testing coordination. Native game dynamics, terrain chunks, replicated ownership, save/reload, and Camp transitions must still be verified in the game.

## Changed files

- `Scripts/Vehicles/Camper/RebirthCamperCampStationAppearance.cs`: native compound restoration, body/Camp hull lifecycle, and own-body collision exclusions. Shared file also contains a separately owned native-target binding change.
- `_Documentation/Camper/CAMPER_BODY_PHYSICS_20261021.md`: causal evidence and validation limits.
- `Tools/VehicleStudio/tests/camper_body_physics_scenario.json`: exact disposable-world regression procedure.

Backup before edits: `E:/_Haven/shared/REBIRTHBackups/CamperBodyPhysics_20261021/RebirthCamperCampStationAppearance.cs`.

## Follow-up: driven body configuration and complete ownership scope

The user's later native-game test still launched with camper body, armor, plow and four turrets installed. The active log `output_log_client__2026-10-21__01-25-39.txt` persisted truck172 at Y42.8 through elapsed910 seconds and at Y345.0 on unload at1011.815 seconds. The initial body-module-only pair filter therefore did not qualify the full driven case.

`RebirthCamperOwnCollision` replaces that narrow filter with an actor-safe inventory of all solid colliders under the current vehicle root, including the model-root selection proxy and optional geometry outside the cosmetic module. Occupants and other child actors are excluded. Only detached owned solids versus the same vehicle's native compound Rigidbody are ignored; external collisions and native wheel/rigidbody ownership remain unchanged. Layer-ignored pairs are skipped unless their actual layer relationship later becomes collidable.

The inventory is cached without per-frame hierarchy scans or collection allocations. Native `SetVehicleDriven` and `Vehicle.SetItemValue` invalidate and immediately reapply it; turret binding explicitly invalidates after runtime target construction. Model/root/Rigidbody replacement, root child count and a bounded five-second fallback also refresh it. Pair ignores are verified after ordinary appearance toggles. The earlier 22-by40 count described only the first narrow filter and is not an inventory of the complete new policy.

An independent driven test with the model proxy disabled was stable for both filters and could not prove a plow/armor cause. That exclusion was identified as incomplete: the native model proxy must remain enabled and the real game layer14/layer21 matrix must be recorded. Do not claim that inactive optional geometry caused the launch without an active contact reproduction. The full scenario above remains required in the game.

The corrected proxy-included driven audit then reproduced a differential failure: old body-module-only exclusions reached Y7.140589 and ended at Y3.1668; the new full-owned exclusions stayed at peak Y0.5000055 and ended at Y0.0138 with normal steering angular speed0.902668. All source modifiers and four turret slots were enabled, native model/Rigidbody pose synchronization was applied each20ms, with steering and3500 total wheel torque. The Unity project collision matrix allowed layer14 against layer21; this is not yet evidence of the running game's matrix. The result establishes the missing-proxy collision path under that explicit condition, rather than proving the earlier158m launch's unique cause.

Follow-up staged compilation passed with19 warnings and zero errors. Eight static lifecycle contracts pass via `python Tools/VehicleStudio/tests/test_camper_own_collision.py`; these guard actor scope, full hierarchy coverage, cached invalidation hooks and pair repair, and do not substitute for native-game driving.

Additional source files in this follow-up: `Scripts/Vehicles/Camper/RebirthCamperOwnCollision.cs`, the collision-policy call in `RebirthCamperCampStationAppearance.cs`, and one invalidation call after turret physical-target binding in `RebirthCamperTurretRuntime.cs`.

## Hood access follow-up

The full model-root selection box expands to camper height when the body is installed. It spans the stock hood horizontally, so allowing its broad volume to collide with the native local movement controller can block a hood jump even though the real truck's hood shape did not change. `vp_FPController` uses a Unity CharacterController for `Move`; its wall-query CapsuleCast mask1084850184 excludes layer14, so the remaining broad selection box is not an intended physical body surface.

The full owned-rig pair filter remains intact. Only the exact `ModelTransform.GetComponent<BoxCollider>()` selection proxy ignores the current world's local player's actual `vp_FPController.CharacterController`. The collider stays enabled/nontrigger and queryable for native vehicle targeting; actual CabHull, CamperHull, wheels, world collisions and body geometry are untouched. The pair is rechecked after native activation changes and across player/model replacement. No global layer mask or movement/jump parameter changes are introduced.

This hood-specific source edit was not built or installed by the subagent, and no native game test was run. The static collision fixture now includes the exact proxy/controller scope and preservation of enabled/queryable selection geometry. The manual scenario includes body-installed hood jumps, physical body-wall checks, interaction, reload and subsequent driving stability.

### Qualified Camp hood route and Travel cab-box correction
The live Camp run passed supported hood (local feet Y1.188,Z1.974), then a .3-second native forward+jump toward the ladder volume aimed 1m higher passed native playerOnLadder. The generator now uses that route rather than ground-side access, whose volume starts above reachable feet height. The same Travel hood jump failed while selection proxy/controller ignore was confirmed active. Authored CabHull is a broad box (top1.7221m, frontZ2.043) while Camp replaces it with sloped hood support (top1.10..1.225m) and front cab skin. Runtime now retains native CabHull for vehicle/world collisions and copies ONLY those two existing convex collider meshes into the moving model for Travel support. They enter the full cached own-compound pair filter; no new mesh, material, asset, Rigidbody or world-layer policy is introduced. Only the local on-foot controller inside front hood approach absX<1.2,Z.7..4.2,Y-.5..3.2 ignores CabHull. After leaving that zone, an existing ignore persists only until the capsule bounds clear CabHull, avoiding re-enabling a collider through an intersecting player. Missing authored support fails closed. Camp keeps its original shell; bare-truck hull behavior is unchanged. Ten static lifecycle contracts and seven generator tests pass. Parent owns build and actual Travel re-test; this is not yet a live qualification.
