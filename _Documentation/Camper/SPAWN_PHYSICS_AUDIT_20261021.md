# Read-only pickup spawn collider and physics audit

Source/target: installed REBIRTH 3.0 `Resources/rebirth_camper80s.unity3d`, native address `rebirthCamper80s`, and its approved Unity source prefab `Assets/REBIRTHApprovedPickupProduction_20261020/rebirthCamper80s.prefab`. Investigates the owner's reported fresh-start truck rising from y40.81 to approximately y198.6 followed by falling/death. No game launch/control, prefab/material edits, bundle build or production mutation was performed.

Installed bundle SHA-256 before and after: `669013F8097EFEC0E61B4247493706F91883FA2AC480C6E092BDACC38C0ADB81`.

## Confirmed geometry

The source prefab and reopened installed bundle have matching active collision geometry in the harness's bare-Travel presentation. They contain a Physics Rigidbody of mass 2400, center of mass (0,.1,0), scale (1,1,1), drag .05 and angular drag .05. Its authored useGravity is false; the harness supplies a standard mass*9.81 downward force as a disclosed surrogate, not native vehicle gameplay forces.

Active CabHull bounds relative to the vehicle origin:

- x: -.90375 .. .90376
- y: .47250 .. 1.72212
- z: -2.77516 .. 2.04298
- Local box center (0,1.09730,-.36610), size (1.80750,1.24960,4.81810), layer 21, non-trigger, attached to Physics Rigidbody.

The separate root/model selection box is also authored enabled and non-trigger on layer 14, with no attached Rigidbody. Bounds: x±.96, y .325..1.975, z -3.25..2.25. Native game layer filtering/selection handling is not reproduced by this authoring-project comparison. A second test disables detached colliders to isolate the actual Physics compound.

All four WheelColliders are attached to the Physics Rigidbody: radius .389073, mass45, suspension distance .43, spring15000, damper2000, target position.5, force-app point.2, damping.25. Centers are x±.744, y.375, z1.289(front) and -1.620(rear). These are measurements of the installed asset, not a recommendation to retune its approved rig.

A static capsule approximation (radius.3, total height1.8, center y.9) at the same player/truck spawn anchor overlaps:

| Truck collider | Minimum separating depth | Separating direction for truck collider |
|---|---:|---|
| Physics/CabHull | 1.20375 m | (-1,0,0) |
| Root/model layer14 box | 1.26 m | (-1,0,0) |

This confirms a substantial geometric overlap when the player remains at the truck origin before attachment. The capsule is an explicitly disclosed shape approximation; the actual player native CharacterController/collider-forwarding force code is not executed. It is not evidence that the geometric overlap alone produced the entire reported launch.

## Offline PhysX comparison

`RebirthPickupSpawnPhysicsAudit.Run` creates a new empty scene, loads the source/bundle, records geometry, then simulates the exact bundle Rigidbody and authored wheel/collider geometry over flat ground at anchor y.5 for ten seconds at .02-second steps. The static capsule, when present, is removed at .48 seconds to approximate the reported attachment interval. Native driving forces, network motion, actual CharacterController collision callbacks, terrain/chunk meshes and game layer collision matrix are absent.

| Dynamic trial | Player capsule | Detached/model colliders | Wheels | Peak y | Peak speed | Final position |
|---|---|---|---|---:|---:|---|
| Physics compound isolation | No | Disabled | Yes | .50000 | 2.212914 m/s | (-.00016,.01341,-.00003) |
| Same compound plus player overlap | Yes, first .48s | Disabled | Yes | .520662 | 1.293456 m/s | (.98942,.01341,-.02607) |
| Authored detached collider included | No | Enabled | Yes | .50000 | 2.672174 m/s | (.10070,-.43479,-.28850) |
| Detached collider plus overlap | Yes, first .48s | Enabled | Yes | .50000 | 10.1973 m/s | (-.11633,-.44727,-.94490) |
| Hull-only isolation | No | Disabled | Disabled | .50000 | 4.267109 m/s | (0,-.47250,0) |

The player-overlap case moves the isolated truck approximately one metre sideways during its first .18 seconds. Including the detached authored model box changes the solver behavior substantially; because native layer filtering is absent, this is a diagnostic isolation rather than a claim that this detached collider participates in the same way in the running game.

No offline trial reproduced a 158-metre upward launch. The Physics compound with its exact wheel values settles on the flat test ground. Therefore this audit does not justify changing the approved wheel/suspension/model asset. It does support eliminating the pre-attachment player/truck overlap and checking native character-controller push/seat timing in the spawn coordinator. That production fix belongs to the parent/spawn agent; no production fix is claimed by this diagnostic.

## Reproduction and evidence

- Diagnostic source: `Tools/VehicleStudio/UnitySources/RebirthPickupSpawnPhysicsAudit.cs`, copied to the existing Unity project's Editor folder.
- Unity 2022.3.62f2 entry: `-batchmode -quit -projectPath E:/_Haven/shared/REBIRTHVehicleStudio_Camper -executeMethod RebirthPickupSpawnPhysicsAudit.Run`.
- Detailed collider/bounds/penetration and .02..10-second traces: `E:/_Haven/shared/REBIRTHStaged/CamperCorrections_20261021/SpawnPhysicsAudit/physics-audit.txt`.
- Unity log: `E:/_Haven/shared/REBIRTHStaged/CamperCorrections_20261021/spawn-physics-unity.log`, completed with exit code0 and `SPAWN_PHYSICS_AUDIT_COMPLETE`.

This is completed analysis, not a claim of resolved gameplay. The parent consolidated report owns fix conformance, package hashes and completion assessment. No game testing is represented as performed.
