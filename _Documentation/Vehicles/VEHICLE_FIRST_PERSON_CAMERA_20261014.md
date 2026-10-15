# Rebindable first-person and third-person vehicle camera

Source: current cumulative REBIRTH workspace (parent checkpoint 7ecb720) and installed native game Assembly-CSharp.dll. Target: current REBIRTH test build. Coding-only contribution; parent owns installation call, localization, build, gameplay validation, scenarios and packaging.

The operating directives were read before changes. Existing native-controls action registration, REBIRTH seat switching, camper seat-return, vehicle driving suppression and native camera/attachment implementations were inspected. Existing seat selection, driver permission, native attachment and on-foot perspective preferences are preserved. This feature applies to any native EntityVehicle rider, including a passenger who does not own the vehicle; it is not camper-specific.

## Native source evidence

Read supplied target-native source under E:/_Haven/shared/REBIRTHTemp/VehicleUIReportedInspect for EntityPlayerLocal, EntityVehicle and PlayerMoveController. Directly decompiled current installed vp_FPCamera, PlayerActionsLocal, PlayerActionsVehicle, PlayerActionsPermanent, Entity, EModelBase and EModelPlayer into E:/_Haven/shared/REBIRTHTemp/VehicleCameraNative_20261014 for the camera analysis.

- EntityPlayerLocal.SwitchFirstPersonViewFromInput refuses any AttachedToEntity, so the ordinary on-foot input method cannot meet the requested vehicle toggle. SetFirstPersonView(bool,bool) is the native model/event/FOV/holster integration used instead. The feature never assigns bPreferFirstPerson.
- EntityPlayerLocal.Awake sets cameraContainerTransform to the player's own transform. vp_FPCamera.OnValue_IsFirstPerson rotates its Parent when entering first person. Therefore SetNativeView captures/restores the player RootTransform rotation around that native event, preserving the seat's body orientation.
- EntityPlayerLocal.MoveByInput routes input to AttachedToEntity.MoveByAttachedEntity while attached, rather than the walking branch. EntityVehicle.AttachEntityToSelf disables m_characterController and installs native seated pose/IK. EntityPlayerLocal.AttachToEntity starts vp_FPController.Player.Driving; vp_FPController.FixedUpdate returns from its movement body while Driving is active. The camera feature keeps all those states intact.
- vp_FPCamera.UpdateInput consumes native InputSmoothLook into m_Yaw/m_Pitch. Its first-person LateUpdate supports DrivingPosition and rotates the camera without rotating its Parent while Driving. This remains the mouse/controller look implementation.
- Native third-person Update3rdPerson calls RefreshDrivingCameraPositions/GetCameraOffset. That path supplies a vehicle-center chase-camera point, not an authored seat eye. The new first-person view uses the posed native head Transform.position in Unity's shifted coordinate space, without adding Origin again or guessing a standing eye height. EModelBase.GetHeadTransform falls back to its model transform when no head exists; that fallback is explicitly rejected.
- EntityVehicle.PhysicsFixedUpdate chooses camera-heading steering when static isTurnTowardsLook is true; otherwise it reads movementInput.moveStrafe, which MoveByAttachedEntity obtains from VehicleActions.Move.X. Native keyboard bindings use A/D. No mouse-look value enters the first-person driver's steering branch.
- EntityVehicle.MoveByAttachedEntity toggles static isTurnTowardsLook on the native ToggleTurnMode action, default left mouse. This side effect is restored around the native call for a first-person driver so clicking cannot change the saved third-person turn preference.
- EntityVehicle.CameraChangeRotation only adjusts the first locally attached player's camera yaw, assuming the chase-camera mode. The new first-person view skips that correction and applies the actual seat-anchor yaw delta once in its camera update; passengers and drivers both follow the turning vehicle without coupling their free-look direction to steering.

## Controls and installation

PlayerActionsRebirth.ToggleVehicleCamera is an ordinary action in the existing REBIRTH Vehicles keyboard/controller control group. It defaults to Home and is fully rebindable; controller default is unbound, matching the existing REBIRTH pattern. Native source confirmed V is push-to-talk, B is Character and M is Map. No Home default appears in the inspected native Local/Vehicle/Permanent action sets or current REBIRTH action set. Custom user bindings remain under the native conflict UI and existing action-set save format.

Parent bootstrap must call RebirthVehicleCamera.Install() from general native-controls setup, not only from camper registration. The installer explicitly installs its own scoped Harmony classes and world-shutdown reset. This coding contribution does not edit the shared installer.

Localization needed in the parent delivery:

- inpActRebirthToggleVehicleCameraName: Switch Vehicle Camera
- inpActRebirthToggleVehicleCameraDesc: Switch between third-person and first-person while seated in any vehicle.
- xuiRebirthVehicleCameraUnavailable: This seat has no available first-person camera anchor.

The existing ttCameraRestricted key is reused when native camera restrictions prohibit a toggle. Localization was not edited by this subtask.

## Behavior and restoration

Poll requires a live local player in a current native vehicle seat, active native Driving activity and available camera/controller. A toggle is dispatched only with gameplay input allowed and no modal/cursor/console input. Native camera restriction policy is honored. First-person requires an actual head bone and a native seat anchor; if an active model loses either, it returns to third person rather than inventing a seat eye.

The view choice is session-local per player, defaulting to third person, and can be reused when remounting. Mount still executes the complete native attachment body before the camera view is applied. Native Detach remains responsible for restoring the player's preferred on-foot mode. Death invalidates active rider handling and leaves the native death camera in control; world shutdown replaces the weak state table.

First-person starts facing the native seat's enter rotation. It retains native free-look yaw/pitch afterward and follows anchor yaw changes. Seat information is cached until vehicle, model or slot changes, so the existing REBIRTH seat-transfer flow can move driver/passenger positions without detaching or losing this view choice. Native CameraInit continues to run and third-person return uses its native chase-camera implementation.

Only during the local first-person driver's PhysicsFixedUpdate call, a value-type scope captures isTurnTowardsLook and forces false. A Harmony finalizer restores the captured value even on exception. Third-person physics and another vehicle's physics run with the original global preference. Native keyboard/controller steering, acceleration, braking and all existing Camp driving suppression remain intact; no raw mouse input is copied into wheel steering.

Only during the local first-person camera LateUpdate, a value-type scope disables its standing-capsule camera-collision probe; that probe can otherwise pull the eye out of the cab. A finalizer restores HasCollision even on exception. The native update still computes rotation, springs and visual effects. A postfix places the camera at the actual posed head and refreshes the native look point, eliminating the standing spring/chase-center position. No player/vehicle collider is changed, and third-person camera collision remains native.

No seat membership, position, pose, IK, character-controller enable state, Driving activity, input binding for vehicle movement, native vehicle steering setting or on-foot perspective preference is permanently changed. There is no new network packet, authoritative game state, vehicle metadata, save layout or persistent camera setting. Existing native vehicle movement/networking remains responsible for driving.

## Traceability and validation

| Requirement | Implementation/member | Other impact | Validation performed | Result |
|---|---|---|---|---|
| Rebindable vehicle perspective toggle | PlayerActionsRebirth.ToggleVehicleCamera; RebirthVehicleCamera.Poll | Existing controls save/UI; parent localization | Native action/default bindings and existing registration read | Implemented; build/runtime unverified |
| Any rider in any vehicle | Rider, Attached, Detached | Local presentation only; no owner requirement | Native membership/attachment and seat-switch source read | Implemented; runtime unverified |
| Seated mouse free-look without walking | SetNativeView, BeforeCamera, AfterCamera | Existing native Driving/controller state retained | Traced MoveByInput, FPController.FixedUpdate, camera LateUpdate | Implemented; runtime unverified |
| Keyboard steering independent of mouse in FP | BeforePhysics, RestoreSteering, BeforeTurnInput | Per-call static setting scope; existing movement/networking | Native steering branch traced | Implemented; runtime unverified |
| Preserve third person and seat switching | Bind, native view call, scoped finalizers, heading patch | Native chase camera/seat-transfer paths retained | Capture/restore and native seat-transfer source review | Implemented; runtime unverified |
| Explicit bootstrap/localization | RebirthVehicleCamera.Install and keys above | Parent-owned installer/localization | Hook provided to parent | Parent integration required |

No compilation, tests, game launch or gameplay control occurred in this subtask. Parent should verify driver/passenger in multiple vehicles, first/third toggles, mouse look while stopped/turning, A/D steering without mouse steering, Camp suppression, seat transfer, vehicle exit/remount, death/respawn, controller rebind, UI/console gates, remote-client riders and clipping/readability from the actual seated eye. Vehicle-specific head-bone animation/visibility and camera collision inside authored cabs remain visual runtime checks; passing them is not claimed.

Compilation: Not performed by this subtask.
In-game validation: Not performed by this subtask.
Completion: coding contribution implemented and source-reviewed; parent owns complete feature conformance, integration, required validation and delivery confirmation.
