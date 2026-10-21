# Native camper body targeting correction

Target: current REBIRTH 3.0 workspace, 7 Days to Die 3.0 b259. Changed-files integration is owned by the parent task. No model, material, collider geometry, bundle or save changes are made by this correction.

## Source evidence

Installed native `PlayerMoveController.HandleInteraction/TryHandleEntity` requires a valid `E_` hit tag, native collection reach, `GameUtils.GetHitRootTransform` resolving an Entity and enabled native activation commands. The native interaction layer mask `-555266077` includes physical vehicle layer 21. `GameUtils.GetHitRootTransform("E_Vehicle", transform)` calls `CollisionCallForward.FindEntity`, which finds a direct Entity or an ancestor CollisionCallForward.Entity.

Native EntityVehicle.Init tags and forwards the Physics hierarchy. The authored Camp walk-in shell is under the visual hierarchy instead. Appearance previously supplied the missing native contract only on the rear-door pivot. Wall/furniture colliders elsewhere in the visual hierarchy therefore lacked that contract; fixing the 21 special station targets did not fix general native body selection. Travel also needs explicit binding when a physical hull is reassigned between hierarchies by body visibility code.

## Correction

`RebirthCamperNativeTargets.Bind` supplies the exact native tag and direct forwarding owner to every nontrigger owned solid collider during initial Appearance model binding, including inactive/disabled colliders. This covers both walls, rear shell, moving door and Travel hull without changing their physics layer, enabled state, size, pose or parent. It skips wheels and special triggers. The native E_VehicleStrong plough-impact tag is preserved. Existing valid native ancestor forwarding is reused so Physics child hulls do not acquire duplicate collision callbacks. Nested seated actors are excluded so their collider ownership cannot become the truck's.

The normal native selection path retains its nearest world/entity hit, wall occlusion, reach, ownership and radial commands. Special station/relay focus still treats real walls and closed doors as blockers. There is no broad proximity selection or through-wall fallback.

## Verification

`dotnet run --project Tools/CamperInteractionTests/CamperInteractionTests.csproj`: PASS 59 checks (synthetic geometry invocation). The fixture links production NativeTargets, PhysicalTargets, WireTarget and RelayGeometry directly. New checks cover native entity resolution on left/right shell, rear jamb, Travel CamperHull and rear door; inactive/disabled binding; preserved layer/enabled state; actor/trigger/wheel exclusion; and repeated binding. Existing special-target and obstruction checks also pass.

Native/Unity object shapes and physics hits are stubbed. This verifies the production ownership-binding logic and native source contract, not actual game raycasts or multiplayer play. Manual qualification still needs both sides and rear in Travel, both sides/rear in Camp, physical stations, and a world wall blocking the whole truck. No game was launched or controlled by this agent.
