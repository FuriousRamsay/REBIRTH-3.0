# Camper physical prompt and fade source corrections — 2026-10-14

Source: installed cumulative REBIRTH workspace, implementation following checkpoint 7ecb720. Target: the installed game's current Assembly-CSharp.dll and matching taller camper candidate. Delivery type: parent-coordinated changed-files package; this subtask changes production source and documentation only.

## Confirmed native behavior

Fresh decompilation used the existing NativeHudInspectBuild/Inspect.exe against ../../7DaysToDie_Data/Managed/Assembly-CSharp.dll (SHA256 FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705). Retrieved EntityPlayerLocal, PlayerMoveController and EntityFactory were written to E:/_Haven/shared/REBIRTHTemp/CamperPhysicalFix_EntityPlayerLocal_20261014.cs, CamperPhysicalFix_PlayerMoveController_20261014.cs and CamperPhysicalFix_EntityFactory_20261014.cs. This was source inspection, not a build or test.

EntityPlayerLocal.GetLookRay creates a camera ray and adds Origin.position, in both first-person and third-person paths. PlayerMoveController subtracts Origin.position before its Unity Physics.Raycast. RebirthCamperPhysicalTargets already subtracts exactly once, correctly. No speculative camera/eye offset was added.

Installed PlayerMoveController.HandleInteraction has eight parameters; activation is parameter index 7. It computes broad native entity hover text separately from activation. The existing REBIRTH prefix ran only on activation, leaving the native vehicle caption visible on physical station targets. Public fields playerUI and strTextLabelPointingTo and the native XUiC_InteractionPrompt.SetText call are confirmed in the installed source.

Fresh installed EntityFactory source places the actor component on the prefab's GameObject while RootTransform also contains sibling Physics geometry. Searching upward from a Physics collider for EntityPlayer therefore does not identify every actor-owned collider. The player-geometry exclusion now checks the actor's RootTransform hierarchy, leaving all other solid geometry participating in reach/LOS.

Vehicle ancestry was reviewed separately before the parent build. The installed factory's non-player branch adds EntityVehicle to the prefab's GameObject and assigns that same transform as ModelTransform. The supplied manifest places every interaction target beneath GameObject/M/VisualBody, including the rear-door pivot; these targets are descendants of the vehicle component. The Physics/CampWalkInColliders branch is a sibling, explaining why floor ownership requires RootTransform ancestry, but it does not explain target lookup failure. No matching target reparenting was found in the reviewed camper runtime. TryTarget/Resolve retain their component-parent ownership check; no root registry, broad search or proximity permission fallback was added without evidence it was needed.

## Implementation

RebirthCamperPhysicalInteraction.HandleInteraction now identifies the physical target for hover as well as activation. It retains Camp/reach/LOS from TryGetFocused and the existing authority/admission services. A postfix substitutes native tooltipInteract text using the actual activation binding and the target block's existing localized name. Power captions resolve actual native lowercase identities generatorbank, batterybank and solarbank. The rear door uses xuiRebirthCamperPhysicalDoor, supplied by parent localization integration. Disallowed actors see native ttLocked; actions still undergo the existing permission checks. Attached players and native third-person LOS failure do not route physical actions. The original method still handles unrelated hover, block focus boxes and native state.

RebirthCamperCampStationAppearance preserves the existing observed timer, MoveTowards smoothing, endpoint snaps and all mode/door/collider gates. The fade is applied to existing per-material property blocks as well as the renderer-wide block. Unity's indexed overrides have priority, so renderer-wide _CampVisibility alone cannot control a material with an existing indexed block. Existing native tint/finish properties are read and retained; an indexed block is not created where none exists. Material counts are cached with the bound model and renderers.

## Traceability and boundaries

| Requirement | Implementation members | Persistence/XML/UI/localization/networking impact | Source review |
|---|---|---|---|
| Physical station caption replaces broad camper prompt | PhysicalInteraction.HandleInteraction, ShowPhysicalPrompt | Existing native UI; one parent-owned door localization key; no packet or save change | PASS |
| Correct native ray coordinate convention | PhysicalTargets.TryGetFocused | No coordinate behavior change; evidence comment | PASS |
| Actor colliders do not block their own station reach | PhysicalTargets.OwnPlayer, Clear, TryGetFocused | No authority, save or network change | PASS |
| Fade respects existing material overrides | CampStationAppearance.Update, View.MaterialCounts | Cosmetic property blocks only; no authority, timer, door, save or packet change | PASS |
| Preserve interaction custody and door behavior | Existing Request/admission and unchanged appearance gates | Existing authoritative checks and manual door replication retained | PASS |

Relevant operating directives, CAMPER_PHYSICAL_ROUTING_20261014.md, CAMPER_CAMP_FADE_HANDOFF_20261014.md, CAMPER_MANUAL_DOOR_SOURCE_20261014.md, target manifest, target authoring source, current energy runtime/configuration, native factory and installed player/controller source were retrieved and reviewed.

Known limits: the reported six-second visible stepping is not proven to originate from material overrides. Source already samples presentation every 0.15 seconds and updates smoothing in both Update and PhysicsFixedUpdate; no six-second throttle was found here. No shader rewrite, timer extrapolation or replacement transition clock was introduced. Native activation not reaching a target because of scene-specific geometry still requires runtime evidence; this source change does not claim every reported in-game activation failure is reproduced or fixed. Missing genuine station installations retain the existing unavailable behavior.

Compilation: not performed. Tests: not performed. In-game validation: not performed. No game launch/control, build, install, git operation or packaging was performed by this subtask. Final cumulative conformance, localization integration, requested build and delivery verification belong to the parent task.
