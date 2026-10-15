# Survivor native lifecycle dependency closure

2026-10-15 audit only. No production changes, builds, game, tests, profiling, gameplay commands or git operations. Existing inspector was run read-only to extract LocalPlayerUI; no tool was built. HAV3N coordination was checked first: QUIET notice expired 2026-10-05, and only light CPU/source inspection occurred. This trace resolves qualifications from SURVIVOR_NETWORK_CREATION_AGENT_TRACE.md, not a newly demonstrated stale-session exploit.

## Read provenance and coverage

New FULL source read: `Scripts/Survivor/UI/RebirthItemWindowLifecycle.cs`, 255 lines, SHA256 `A8FBED805F1A6D606C783171DEA647A1112B6020DB99B4036F350AB0EAC6AEB6`.

Repeated FULL read, no new unique coverage credit: `Scripts/Metabolism/RebirthMetabolismInstaller.cs`, 940 lines, SHA256 `0C6C6A2EC85EED1696F2ECB9BBCDCE2D0DE03E85A384FBC833DED81275C45875`. Read in bounded ranges 1–175,176–330,331–500,501–720,721–940; an oversized attempt was replaced with bounded reads. METABOLISM_AGENT_TRACE.md already fully covered this same hash, including death/reset. Repeated gate54 hash `264BB65076A1DB7701192FF6C7CFC6992C8C493FFCC3C8C3034B7FC789E13F34` and WindowReturn18 hash `D3B7CBBE33C58B8C62BF1F61741FBBB906F27C412A4CCE0AC8585010AFD18A2E` were already fully covered. Hold405 and PersistenceLifecycle102 remain prior traces; not newly counted.

Installed Assembly-CSharp.dll SHA256 `FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705`. Existing inspector `Tools/StorageGear/inspect/bin/Debug/net9.0/inspect.dll` extracted `LocalPlayerUI` into `E:/_Haven/shared/REBIRTHTemp/SurvivorNativeLifecycle20261015/LocalPlayerUI.cs`, 930 lines, SHA256 `2016C05E3868D160C718A591EC0244E9715C933BC3223134F49F5D4A2F0393C2`. Native reads are PARTIAL named-method evidence, not full-class credit: QueueUIForNewPlayerEntity, DispatchNewPlayerForUI, GetUIForPrimaryPlayer, GetUIForPlayer, CreateUIForNewLocalPlayer, Awake and OnDestroy. Unrelated navigation body was truncated in an initial broad read and is not claimed as fully reviewed.

Existing native extracts, PARTIAL named-method reads:

| File under E:/_Haven/shared/REBIRTHTemp/MultiplayerAuditNative20261014 | SHA256 | Scope |
|---|---|---|
| GameManager.cs |7BD2D1EC5C938E17E5EFF7F43DEAAD13190A1793329819AECE4A4AB4BAD2A63D|StartGame, startGameCo opening through UI queue; IsSafeToConnect/Disconnect; Disconnect/disconnectLater; SaveAndCleanupWorld event/UI/world ordering; ItemDropServer initial spawning path|
| ConnectionManager.cs |9CA48CD595EE8E1C4E086576B02DACB91D3F6CD1E0F3ADC2D447FE9044732C1F|Disconnect, Connect, Set/GetConnectionToServer, DisconnectFromServer, StopServers, Update, ProcessPackages|
| EntityPlayerLocal.cs |EA0C73D90A279C9D5BC598CE66784A1923F551E163963DE8F1ACAF800E31E889|Awake only|

Native NetPackage ShouldProcess and ValidEntityIdForSender were read selectively from the existing extract; no new full-class credit. SurvivorUiInstaller first65 lines and metabolism HandlePlayerDeath/AddToBackpackOrDrop are selective caller/body evidence only.

## Native UI queue conclusion

LocalPlayerUI.QueueUIForNewPlayerEntity deduplicates by UI object. GetUIForPlayer first returns an already assigned matching UI, otherwise notes an unassigned non-primary fallback, then dequeues the first Unity-nonnull pending UI and assigns its mEntityPlayer. Destroyed queue entries are skipped using Unity's overloaded null comparison. EntityPlayerLocal.Awake calls this resolver then immediately dereferences playerUI.windowManager. No world ID is encoded in the queue, but its normal owner is the current StartGame attempt.

Critically, GameManager.startGameCo invokes ModEvents.GameStarting BEFORE its yields, XML preparation and `QueueUIForNewPlayerEntity(CreateUIForNewLocalPlayer())`. Therefore the FirstEntry GameStarting cleanup cannot destroy the newly queued UI of this ordinary start. It removes remnants from an earlier attempt. Do not promote the preceding trace's broad-cleanup observation into a current-start destruction defect.

SaveAndCleanupWorld invokes WorldShuttingDown before closing/removing entities and UI. It destroys every non-primary/non-clean UI, including unassigned UIs, then later opens the main menu and unloads/nulls World. LocalPlayerUI.OnDestroy removes the object from playerUIs, but does not clear the pending assignment queue itself. Survivor's explicit queue cleanup complements this native omission; native dequeue also skips destroyed objects. No missing-player-UI crash or duplicate-camera failure was demonstrated in this audit.

## Session fencing and actual dispatch

ConnectionManager.Update drains server ClientInfo connections with ToClient disallowed; client Update drains only current nonnull/non-disconnected connectionToServer[0/1], with ToServer disallowed. ProcessPackages checks packet direction, rejects unauthenticated server-side packets unless AllowedBeforeAuth, then invokes ShouldProcess and ProcessPackage against current GameManager.World. Base ShouldProcess returns true; there is no general world-epoch comparison inside this loop.

ConnectionManager.Disconnect disconnects both current client channels and nulls them; it clears connected state. StopServers stops the protocol, Disconnects, then invokes SaveAndCleanupWorld. DisconnectFromServer similarly invokes its event, Disconnects, then cleans the world. This is concrete native session separation: an old connection is no longer selected by future Update calls. Thus missing epoch fields in Survivor projection alone do not establish cross-session owner-state adoption.

Within an already fetched package batch ProcessPackages does not recheck connection identity after each package. Establishing stale delivery would require a specific earlier packet synchronously tearing down and starting another world before a later Survivor packet in that same batch, or a transport implementation returning an old batch through a new connection. No such complete chain was found here. INetConnection.GetPackages implementation and protocol workers were not fully audited in this tranche. Keep that possibility qualified; do not describe it as confirmed.

ValidEntityIdForSender on server accepts only Sender.entityId unless its explicitly optional attached-entity route is requested. Survivor calls default false, so the request owner check is real. Null Sender would fault, not authorize another player. Direction filtering rejects ToClient Survivor state/result packets sent from a client before their ProcessPackage can call ClientState.

## Deferred leave callback conclusion

FirstEntry.LeaveWorld pre-spawn schedules a global ThreadManager coroutine; two frame yields precede current-manager Disconnect. It is not owned by the soon-destroyed gameplay UI. Ordinary execution deliberately survives closing that UI and disconnects the same still-active session. Native cleanup opens the main menu only as part of teardown after disconnect, so the ordinary UI flow does not provide a new-world start in those two frames before this callback initiates teardown. Missing an epoch/cancel token remains hardening evidence, but no actual stale callback chain was established. Do not promote the earlier hypothesis without a concrete alternate cancellation/start entry.

## Creation hold, death and metabolism lifecycle

The current installer explicitly installs death/disconnect/spawn/stat hooks with PatchClassOnce; this is production code, not Debug-only. GameUpdate ticks every authoritative world player, including remote EntityPlayers, at the configured real-time cadence; actual Tick deduplication remains existing METABOLISM_AGENT_TRACE evidence. The Installer sets installed=true before its work, so an install exception prevents automatic retry through this entry; this repeated source fact is not a new native install measurement.

ShouldDefer requires a complete committed world-character record resolved from server entity identity in Rebirth mode. It returns false for remote client authority and Base Game mode; client display/native OT behavior must therefore not be confused with authoritative digestion. IsLinkedToCommittedOrigin accepts an empty metabolism creation marker only for explicit legacy origins; current origins require matching request scope. Rebirth spawn defers uncommitted characters, checks the generation, sanitizes nonfresh and saves; NewGame/EnterMultiplayer never resets an already committed Survivor merely because of native spawn reason. Base Game branch explicitly retains fresh reset behavior.

Death hook calls authoritative HandlePlayerDeath. That method skips uncommitted characters, returns a cloned hydration slot to the first empty backpack slot or server item-drop path, clears ingestion/timed effects, resets cooldown/runtime entity, touches and saves. Its helper return value is ignored, and native ItemDropServer returns void. Native drop creates and spawns an EntityItem before optional chunk merging; a missing chunk after spawning does not itself prove failed delivery. Missing World/exception/custody uncertainty and disk failure are not modeled as receipts here. Existing METABOLISM_AGENT_TRACE covers the broader conservation/persistence issues; no duplicate finding identifier is assigned.

Disconnect hook touches an existing server player state and saves before native player removal. Final failed-save/reset remains existing MP-036 family already documented; this trace does not count that as new.

## Item-editor UI lifecycle (new full source)

SurvivorUiInstaller explicitly installs modify capture/return, cosmetics return and drag-menu hooks. `rg -uuu` also locates direct RebirthEditorActions.Adapt consumers in character/crafting/backpack controllers; these are actual entry chains, not attribute-only inference. Capture is Rebirth-mode only, snapshots owner/item/scroll/inventory mode and replaces the per-XUi token. Native assemble/cosmetic inventory operations remain delegated, not replaced with a custom transfer.

ReturnLater waits for the cancel gesture, requires a live UI/player and unchanged token, removes the pending record, opens the destination, then restores selection/scroll after end-of-frame. This rejects superseded returns on the same XUi. It has no dictionary shutdown clear; an aborted return due to destroyed UI/player leaves a strong reference in static Returns. Confirmed retention is UI object lifetime/memory scope, not cross-owner item mutation: native cleanup destroys that UI, and later worlds create another XUi key. After the second yield it checks owner-open state but not the original token; no gameplay mutation is performed by those scroll/selection restorations.

EditorActions replaces native entry objects and disables their events; Recipes delegates a cloned selected stack to catalogue navigation. LiteratureUse refreshes reservation/input/cursor/item checks then delegates to the authored native action. Literature authorization itself belongs to the other agent; no duplicate full scope claimed. The full file contains no new server ownership bypass or durable character write.

## Outcome and remaining scope

The requested native queue/session dependencies now have method-level evidence and ordinary-flow exclusions. No stale owner packet or new-session disconnect was proven. Newly closed unique production source is ItemWindowLifecycle255; repeated metabolism installer/gate and existing WindowReturn receive zero new unique credit. Remaining exact proof for an adversarial/interleaved stale packet is transport GetPackages/protocol batch lifetime; for global leave callback it is an actual alternate start/cancel sequence, not simply an absent epoch field. All negative source caller searches used `rg -uuu`; searches over existing native extracts are bounded to extracted types, not an assertion about the entire assembly.
