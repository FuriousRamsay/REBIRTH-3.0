# Driven camper physics: native model proxy, 2026-10-21

User reports fully modified camper launches/spins when driven despite the prior stationary body-only self-collision correction. That earlier test was insufficient; its excluded model proxy was an explicit but unverified assumption about the game's layer14/21 collision matrix.

Source: existing installed-equivalent approved Unity rig, with the versioned rear-corner visual candidate (two extra renderer-only meshes; no collision difference). Unity2022.3.62f2 read-only test, no game launch or bundle installation. Four wheels unchanged, native mass2400 and COM(0,.1,0), 3500Nm total wheel motor torque, 10m/s surrogate speed limit, ±15degree front steering from3–9s,13seconds at.02s. All authored body/armor/plow/four turret slot roots activated, Travel equipment and walk-in shell disabled; Physics descendants layer21 and nonkinematic, manual gravity. Visual model copies Rigidbody position/rotation each step, matching the native pose-follow relationship. This is not execution of the full native engine/network/terrain/driver controller.

## Results

| Active native layer14 model proxy | Own-pair policy | Peak y | Peak speed | Peak spin | Final y |
|---|---|---:|---:|---:|---:|
| Excluded (previous assumption) | Old BodyModule only |.5000055|10.39737|.9026682|.0138|
| Excluded | Full owned detached solids |.5000055|10.39737|.9026682|.0138|
| Included, project14/21 collision enabled | Old BodyModule only |7.140589|5.149775|1.777984|3.1668|
| Included, project14/21 collision enabled | Full owned detached solids |.5000055|10.39737|.9026682|.0138|

At-rest old policy with proxy included also rises to7.140589; full-owned remains at.5000055. No wheel reports a self ground hit in these cases. 88 authored detached nontrigger colliders and67 native compound colliders were inventoried including inactive/disabled shapes; only22 ladder solids plus model proxy are active detached solids here. Runtime-created entities/turrets are not reproduced by merely enabling authored slot roots.

Confirmed: the native model-root layer14 solid BoxCollider is outside BodyModule and omitted by the old own-pair cache. With collision enabled it penetrates the native rigidbody compound and produces instability. Full actor-owned filtering includes it and returns the controlled test to the normal driven baseline without modifying world/player collision, wheel values or physical geometry.

Limit: the installed game's exact layer14/21 collision matrix was not independently extracted. Unity project default matrix is the stated collision-enabled case; this test cannot certify that exact runtime cause or reproduce the user's entire158m+ launch. It does prove why the model proxy cannot safely be excluded from the own-actor collision policy based on an unverified intended-selection assumption. Parent/safe_vehicle_spawn own the runtime all-owned filter, attachment-time invalidation and final DLL build. Manual fully modified vehicle drive/seat/reload/client verification remains required.

Reports: DrivenPhysicsAudit_20261021/proxy-excluded.txt and proxy-included.txt; diagnostic Tools/VehicleStudio/UnitySources/RebirthCamperDrivenPhysicsAudit.cs now defaults proxy-included. Original excluded results preserved separately. No physical source prefab edits made by this diagnostic.
