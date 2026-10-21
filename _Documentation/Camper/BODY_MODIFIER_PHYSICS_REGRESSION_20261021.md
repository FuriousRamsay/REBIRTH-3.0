# Camper body modifier physics regression, 2026-10-21

Target: installed REBIRTH 3.0 camper body modifier. User clarified ordinary truck-only initial placement is correct; bounce/sink begins only when the camper body modifier is installed. No approved visible model, wheel geometry, materials, prefab or bundle changed in this audit. No game launched.

## Confirmed ownership regression

Compared Unity source prefab `Assets/REBIRTHCamperTurretProduction_20261020/rebirthCamper80s.prefab` before pickup bodywork with `Assets/REBIRTHApprovedPickupProduction_20261020/rebirthCamper80s.prefab`.

The earlier CamperHull is `Physics/CamperHull`, layer21 solid BoxCollider, attached to the native Physics Rigidbody. The approved pickup source instead has it under `GameObject/M/VisualBody/REBIRTHCamperBodyModule/CamperHull`, layer21 solid but attachedRigidbody=null. Activating the body therefore activates an independent static collider intersecting its own dynamic rigidbody compound.

Current world-space CamperHull bounds at identity: min(-.95842,.56676,-3.46443), max(.96356,3.02487,.71593). CabHull remains Physics-bound: min(-.90375,.47250,-2.77516), max(.90375,1.72211,2.04296). Unity ComputePenetration reports CabHull/CamperHull penetration1.155345m downward and multiple rail/CamperHull penetrations. Wheels remain four Physics children with unchanged authored values.

Restoring only CamperHull ownership reveals 22 detached layer21 solid rear ladder capsules penetrating the restored hull .02924 through .19173m. They still need their own-vehicle collision pairs suppressed; removing their world/player collision is unnecessary. Pair filtering must be reapplied after activation changes, because Unity can reset ignored pairs when colliders are disabled.

## Controlled offline physical comparison

Unity2022.3.62f2, flat static ground, mass2400, authored wheel radius/suspension/friction, native COM(0,.1,0), manual gravity mass*9.81, 500 steps of .02s, initial anchor y.5. No player capsule. Selection proxy layer14 excluded from this comparison, because it is not the intended physical vehicle hull; this is an offline approximation, not a claim that the full game's layer matrix was reproduced. Native drive/controller/terrain code and moving visual sync are not executed.

| Configuration | Peak y | Peak speed | Horizontal displacement after10s |
|---|---:|---:|---:|
| BodyOFF baseline | .5 |2.212914 |.00016m |
| BodyON broken detached hull |3.737417 |9.012859 |21.14m |
| BodyON restored compound only |.5000045 |2.701279 |6.35m |
| BodyON restored compound plus own detached-solid pair filtering |.5000045 |2.21294 |.00006m |

All corrected cases settle at rigidbody y.01341, the same as truck-only baseline. Thus the combined correction eliminates both the reproduced upward launch and ladder-driven lateral motion in the controlled test. This supports a narrow runtime restoration of original compound ownership plus own-vehicle pair filtering; no wheel/suspension tuning is justified.

## Later driven regression qualification

The user subsequently reported driven instability with the earlier body-only filter. Its stationary result and excluded layer14 selection proxy were insufficient. See DRIVEN_PHYSICS_PROXY_REVIEW_20261021.md: proxy-included real-wheel driven test reproduces a missing own-pair instability, and full-owned filtering removes it. Do not treat this earlier report as complete driving qualification.

## Implementation boundary and remaining verification

The safe_vehicle_spawn agent owns runtime integration in CampStationAppearance. Preserve the original hull transform world pose while reparenting, explicitly enable only for body-installed Travel, and suppress only each vehicle's own detached body solids against its own rigidbody compound. Leave external player/world/vehicle collision and bodyOFF native rig unchanged. No bundle rebuild is needed for this runtime correction.

Manual game verification remains necessary: install/remove modifier on grounded vehicle, pickup/place with body installed, travel and deployed Camp, save/reload, and client ownership transfer. Offline testing demonstrates physical cause and correction under stated conditions; it does not certify the complete game behavior.

## Artifacts and reproduction

Diagnostic source: `Tools/VehicleStudio/UnitySources/RebirthCamperBodyPhysicsAudit.cs`. Existing project Editor copy matches source. Run Unity batchmode with executeMethod `RebirthCamperBodyPhysicsAudit.Run`; writes the combined report under shared staged `CamperCorrections_20261021/BodyPhysicsCombinedAudit`.

Versioned evidence in `BodyPhysicsAudit_20261021/`: original-current-inventory-and-ignore.txt; compound-only.txt; compound-and-own-pair-filter.txt. Earlier first report intentionally included selection proxy to inventory every potential collision; later controlled reports exclude it as described. Original/current comparison is inventory evidence, not proof that all old-prefab physics simulations match a working game. Previous source/bundle audit established the approved source matches installed collider/wheel inventory.
