# World lighting and camper device trace

Status: source review; runtime scenarios NOT RUN. Fully read `RebirthCamperWorldLighting.cs` and `RebirthCamperEnergyDeviceAdapter.cs`.

## Authority and reset path

The device installer explicitly registers world-light patches. Prefab copy entry points establish a thread-local admission scope and restore it in a Harmony finalizer, including exceptions. Authoritative non-editor light admission clears the native original/current light bits unless the prefab or position is a trader area. SetBlocksRPC's outgoing values receive the same correction so peers are sent the admitted off state. Powered tiles also start untoggled during that scope.

Prefab overwrite invokes `OnPlainReset` before placement. Existing device baselines are marked removed, their live ownership entries are retired, POI control is disabled, and a new per-placement provenance stamp is written. The subsequent old baseline cannot match the replacement stamp. These are meaningful controls for the user's POI-reset rule; they still need the exact native reset call graph and host/remote observation qualification.

## Loaded-world migration and eligibility

Legacy loaded chunks are queued weakly and processed at most two entries per update. Migration skips traders, claimed blocks, player-placed powered tiles, unmatched prefab blocks and existing provenance markers. It permits only matching authored block type/rotation and retains a valid existing camper lease where possible. Plain-light discovery requires a valid provenance marker; cached results are limited to one second and recheck live tile identity.

Open integration case: the migration claim check uses the global native land-claim predicate, which includes the newly added camper claim. Determine whether a previously unmarked POI loaded inside a camper claim is permanently skipped. `ObservePoi` counts marked plain lights; `SetPoiLights(on)` refuses zero supported lights; explicit `MigratePoi` occurs only once POI control is active. Validate this ordering on a disposable legacy world before changing migration permission. An authored/player-modified ambiguity must not grant ownership by proximity.

## Device ownership and recovery

Only authoritative current-world leaf consumers without native parents/children are admitted. Already-powered unowned nodes are skipped. Ranged traps require native ownership; generic placed devices require the camper owner's claim. POI lights exclude trader areas and foreign claims. Competing campers have separate live ownership maps.

Before world changes, original/previous/expected device state is recorded into the vehicle's persisted lease. A pending save witness holds energy supply. Restoration checks current node identity, block type, original stamp for ordinary lights, expected state and permissions. Durable baselines remain because a vehicle save alone does not prove restored chunks reached disk. Shutdown releases live supplies and clears session maps; reload reconstructs the saved lease.

Qualification cases: manually toggling a supplied device, adding native wiring, changing claim ownership, removing/replacing a same-type block, two campers competing, unload/reload with the camper absent, and shutdown during pending lease publication. `Relinquish` explicitly sends zero power before abandoning ownership; verify this cannot override a newly established native supply when state or permissions change.

## Scope and performance observations

Ordinary electrical reach uses the spherical range policy. POI selection is separately nearest horizontal bounding-box distance within configured POI range and can include the whole selected POI; do not confuse it with the electrical sphere. Normal `ObservePoi` exits outside Camp mode. The Bridge-only `ObserveDevices` can inspect nearby POIs outside Camp and does not mutate or hydrate leases.

Each consumer evaluation traverses the global power dictionary; nearest POI traverses all prefabs; proximity defense scans all entities per defensive consumer. There is no measured timing evidence yet. Include loaded-world size, multiple campers and many traps in the performance batch before choosing a spatial-index change. Do not impose a new gameplay device/output cap as an optimization.

The legacy migration safeguards intentionally preserve some claimed/modified lighting. Final conformance review must explicitly explain the difference between default-off authored lights and player/camper-powered lights, rather than claiming every possible world light is forcibly off.
