# Camper mode shortcut and blocked-driving notice

Source: current cumulative REBIRTH workspace, checkpoint identified by the parent task as 7ecb720. Target: installed native Assembly-CSharp.dll and current REBIRTH camper integration. Contribution type: coding and documentation for the parent test-build delivery. No game input, build, test or packaging was performed by this subtask.

Read: REBIRTH_PROJECT_OPERATING_DIRECTIVES, camper energy Camp requirements, existing action-set registration, physical focus/prompt, native-lock admission, EnergyAccess command/custody path and Runtime.RequestMode authority. Relevant native PlayerMoveController and EntityVehicle source was inspected. XUiC_WindowSelector and PlayerActionsPermanent were decompiled directly from the installed game assembly to verify current signatures and default Map key.

## Native evidence

PlayerActionsPermanent.CreateDefaultKeyboardBindings binds Map to Key.M. PlayerMoveController creates a global NGuiAction for PermanentActions.Map whose callback calls XUiC_WindowSelector.OpenSelectorAndWindow(entityPlayerLocal, "map"). The installed static signature is OpenSelectorAndWindow(EntityPlayerLocal, string).

EntityVehicle.MoveByAttachedEntity(EntityPlayerLocal) reads native VehicleActions.Move.Y/X into forward/strafe for the attached main entity, independently of the previous vehicle movement values. Therefore the blocked-drive notification reads these raw actions in a prefix, before any existing suppression or cruise postfix can erase the attempted input. Existing non-Travel suppression still clears driving axes/torque/steering and runs native camera/physics behavior.

## Changes

PlayerActionsRebirth adds ToggleCamperMode to the existing persistent REBIRTH action set, Vehicles control group and controller-rebindable actions. Default keyboard binding is M; controller default remains unbound, consistent with the other REBIRTH actions. Binding serialization and Controls UI exposure are inherited from RebirthNativeControls; no separate preference format is added.

RebirthCamperModeInput dispatches only while on foot, alive, no cursor/modal/console input, and looking at a reachable camper. Authored physical targets are accepted through the existing focus validator; otherwise the first solid ray hit must belong to the camper. Current local radial permission, owner identity, authoritative observed mode availability and no active native vehicle interaction are required. The prompt appends the current ToggleCamperMode binding and current native Activate binding with the localized switch-mode/hold-for-actions wording. A HandleInteraction postfix runs after the existing physical prompt and avoids duplicate suffixes. PhysicalInteraction itself is unchanged by this contribution.

The shortcut calls EnergyAccess.ExecuteModeShortcut. That helper rechecks permission, nonempty current character creation, exact observed revision and the Travel-to-Camp/Camp-to-Travel pair, then calls the existing private Execute path. The existing Busy gate covers source coordination, maintenance and custody journal before Runtime.RequestMode rechecks authoritative actor/owner, distance, source custody, holds, pending module transfers and parked/driver-seat deployment requirements. No window or native vehicle lock is opened/acquired by the shortcut. A repeated press during Deploying/Packing gives a localized transition-in-progress notice.

For remote clients, the new mode-only ToServer packet contains actor, vehicle identity, world identity, current character creation, expected mode/revision and sequence. Receive binds the sender to the current connection/entity, validates world/identity/character/current owner/no active interaction, rejects stale revisions/modes and replayed serials, then invokes the same ExecuteModeShortcut helper. Server Runtime.RequestMode remains the only mode mutation path. The client never predicts or writes mode. Existing native ItemValue sync carries accepted mode/timer changes; no new persistent metadata or save schema is introduced.

The native Map open call is intercepted only when the same ToggleCamperMode press has just been consumed for a permitted focused camper. Consumption is recorded per frame and actor so the native map callback and PlayerMoveController.Update may run in either order without duplicate mode requests. Map input elsewhere remains native. Rebinding uses the semantic action; the prompt follows the configured binding. Default M may appear as a duplicate binding in the native conflict UI because the Map action intentionally retains M for other contexts.

RebirthCamperEnergyDrivingInput.NotifyAttempt reads raw native forward/backward/left/right axis values, excludes console/cursor/modal input and passengers, and displays the localized Camp driving restriction at most once per three seconds per local player. Notification occurs before suppression and does not clear the player's shared input. Deploying/Packing remain blocked by the existing non-Travel gate as well.

## Localization owned by parent delivery

- inpActRebirthToggleCamperModeName: Switch Camper Mode
- inpActRebirthToggleCamperModeDesc: Switch the targeted camper between Travel and Camp.
- xuiRebirthCamperDriveCampBlocked: The camper cannot be driven in Camp. Leave the driver seat and switch to Travel.
- xuiRebirthCamperModeShortcutPrompt: {0} Switch mode | Hold {1} for actions
- xuiRebirthCamperModeTransitionBusy: The camper is already changing mode.

The parent owns localization file edits, transition duration/progress-bar implementation and complete feature validation. This contribution does not claim that those surfaces were implemented here.

## Requirement traceability

| Requirement | Implementation member(s) | Persistence/XML/UI/network impact | Validation | Result |
|---|---|---|---|---|
| Rebindable M mode shortcut | PlayerActionsRebirth.ToggleCamperMode, CreateActions/CreateDefaultKeyboardBindings | Existing action-set save/UI; no new save format | Existing action patterns + native default/signature source inspection | Implemented |
| Scoped Map conflict handling | RebirthCamperModeInput.Consume, RebirthCamperModeInputMap.Prefix | One map-open interception for consumed focused action | Native Map callback and signature decompilation | Implemented; runtime unverified |
| Binding-aware focused prompt | Prompt, TryFocused, RebirthCamperModeInputPrompt.Postfix | Localized native interaction prompt; parent localization keys | Source inspection | Implemented; runtime unverified |
| Preserve mode authority/custody | ExecuteModeShortcut, Request, Receive | New narrowly scoped authenticated request; existing mode persistence/sync | Traced Execute/Busy/RequestMode and receive guards | Implemented; runtime unverified |
| Notify blocked driving before suppressing | NotifyAttempt, RebirthCamperEnergyDrivingInputRead.Prefix | Local tooltip only, no networking/persistence | Native MoveByAttachedEntity raw axis source inspection | Implemented; runtime unverified |
| Explicit install | RebirthCamperModulePatchInstaller.Install | No automatic assembly scan | Installer source readback | Implemented |

## Limits and remaining validation

No compilation or gameplay test was run by this coding subtask. The parent should verify keyboard/controller rebind and prompt updates; Map away from a camper and when blocked by UI/console/permission; single-player and remote owner mode requests; stale/replayed requests; refusal during active native locks/custody/driver-seat deployment; notices for all four drive directions without interrupting camera/horn/exit; and no-window transition progress.

Exact expected revisions intentionally reject a remote mode request when the server state has changed since the client's last native snapshot. A new press after sync is required. No new response packet or mode prediction is introduced. The existing native action binding-conflict screen may report the intentional contextual M overlap.

Compilation: Not performed by this subtask.
In-game validation: Not performed by this subtask.
Completion: coding contribution implemented and source-reviewed; parent owns localization, progress UI, scenarios, packaging and mandatory feature delivery confirmation.
