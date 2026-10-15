# Survivor progression UI source closure

Audit-only 2026-10-15. No production changes, build, tests, game control or Git. Scope chosen because background provenance/placement/pickup core is already covered by SURVIVOR_BACKGROUNDS_AGENT_TRACE and native transport evidence. This tranche does not claim the graph/query subsystem or all Survivor complete.

## Full source reads

Every line of the following current files was read in bounded output, including Explorer 1–240, 241–480, 481–720, 721–936. An earlier oversized output returned no visible source and was not counted. SHA256:

| Source | Lines | SHA256 |
|---|---:|---|
| Scripts/Survivor/UI/XUiC_RebirthSurvivorProgressionPanel.cs |151|2FBCE1164DA22F601C3CF6AA195F01668197294E2EB918C6CAB5607636AB8C7F|
| Scripts/Survivor/UI/RebirthProgressionWindowRouting.cs |89|83E1D2CC0BA2D089A537088BAB354BE48CADCEF5B126C86F9CB6900B9A458259|
| Scripts/Survivor/UI/RebirthProgressionRadialRouting.cs |27|08899677B5CC2D6272EE7F7213E5491E2B0FC4C9C7C86C8EE89F0B183506CD9A|
| Scripts/Survivor/UI/XUiC_RebirthProgressionExplorer.cs |936|D3C9F0D99D2D66C15B4814823AF816B9E3BB2B1696F027032DD8D2E0D5B3C83D|
| Scripts/Survivor/Progression/Explorer/RebirthProgressionExplorerNavigation.cs |85|39D9D33CF2F7D5C64976911637BCA638711459EE80488DF3B728E1A3A4C83671|
| Scripts/Survivor/HudTracking/RebirthHudTrackingPreferenceService.cs |224|3979CCD90FC7B9444997E511390F4704F53244CEF9F7AC2F488AA95D5E515EF8|

Total 1,512 full-read source lines. Support excerpts only: UiInstaller 1–110 and main-menu request/return 221–255; ExplorerUiProjection 1–120; PreferenceStore 1–75; XML controller/group declarations and final replacement opening. Prior full Creator, Character, UiSnapshot and definitions/installer evidence reused, not new full-read claims.

## Actual reachability and ownership

SDK default compile includes these non-Debug source paths. RebirthSurvivorInstaller:119 invokes UiInstaller through RunStep. UiInstaller installs RadialMenuRoutePatch alongside shortcut/selected-page hooks. Its installed flag is set after required patches and optional context integration; a required patch exception before that can leave earlier patches installed, with bootstrap PatchClassOnce relevant to retry (prior installer evidence; no new universal atomic-install claim).

Actual XML uses controller="RebirthProgressionExplorer, RebirthUtils" in both menu windows:2495 and in-game windows:12778. In-game later replacement at 23727–23729 removes/re-appends the Explorer window; early layout alone is not final layout evidence. Window groups are registered Config/XUi_Menu/xui.xml:21 and Config/XUi_InGame/xui.xml:119. Layout and all XML descendants were not fully reviewed here. Search found no Config reference to RebirthSurvivorProgressionPanel: treat that small panel as compiled but no proven current XML installation, rather than an active native Character replacement.

Explorer UiService:919–935 requires supplied XUi/playerUI/windowManager and registered child controller, then Prepare/Open. It does not itself prove supplied UI is the primary owner. Active callers include Character:1310–1347, Creator:3226–3249, profile manager:1075, crafting presentation/actions/details and lock cross-link. Character passes LiveCharacter and unique character:GUID return token; Creator passes CreatorPreview and current validated preview, with unique return token. Both suspend caller and unregister/reopen on Open failure (full prior traces). Main-menu integration creates Neutral request and deliberately leaves mainMenu open under modal. No server RPC or progression allocation occurs in Explorer controller or navigation model.

WindowRouting is the stricter native shortcut/radial gate. Disabled configured mode or absent/not-ready owner character chooses native behavior. Once custom ready state exists, mismatched/missing game/world/state/Guid/creation/definitions/player/manager, foreign UI, non-primary player, wrong world/entity reference, unspawned/dead/creation-held player blocks. It takes a second owner snapshot and repeats reference/header/creation checks. Witness StillCurrent compares exact game/world/state/UI/ownerUI/player/manager plus header/creation/Guid. Radial commands 1 or 3 alone use this path; require same sender/controller, console gameplay admission, same manager, and revalidation before close/navigation. Other command indices retain native callback. Therefore this route cannot borrow primary owner projection from a foreign XUi under its admitted checks; this does not imply every generic Explorer Open has this witness.

## Projection, local mutation, persistence and topology

Panel caches owner snapshot by header and text-cache revision every .25 seconds, formats attributes, sorted skills and knowledge. It performs no requests, awards or file writes. Its unproven installation prevents promoting a stale dormant panel issue to active UI defect.

Explorer Prepare resets navigation/home, focus, offsets, return context and preview. Init wires visual controls once; graph-node/search/breadcrumb/legend/history actions alter only local navigation/filter/presentation. Center node press returns immediately. Build uses graph/query/projection, with neutral access informational. Projection support 81–120 builds bounded 24+focus+24 slots and uses owner snapshot only for LiveCharacter, explicit creator preview or last creation validation for CreatorPreview, neutral otherwise. No capability unlock or award is invoked by a graph node click.

Explorer Update runs only when open: graph semantic hash, live owner header and training-projection revision cause re-render; skill details refresh every .5 seconds; scroll changes update bounded offsets. OnOpen registers custom scrollbar authority and disables native Escape close; its own Cancel-release handler ReturnOrClose first closes Explorer, then consumes return token or reveals/reopens caller. OnClose saves local preferences and unregisters both scrollbar authorities. It does not clear projection/preparedMode/search/filter fields; ordinary UiService Open always calls Prepare, but Prepare leaves search/filter fields retained. This is local UI retention, not saved progression or authoritative state.

TrackFocus is deliberately separate from graph navigation. It is admitted only LiveCharacter with projection focus, available Rebirth character header, trackable Skill/Knowledge registry node. It invokes PreferenceService.Mutate only to add/remove that stable ID. It does not award skill/theory/knowledge. The service re-resolves current primary player/world/platform before mutations and returns clones to readers. Missing owner/identity or failed load disables writes. It flushes outgoing exact context, retains failed detached writes, reload-admits detached target before retry, and compares mutation revision before clearing dirty. Context changes increment epochs/revisions. PreferenceStore excerpt derives file from normalized save-directory hash + stable local-platform storage key under user-data/RebirthData/HudTracking; no remote-player target argument or server packet. Actual full store/schema/atomic-file implementation remains a separate source-coverage requirement.

Listen host and remote owning client execute local XUi/owner projections and local preference writes. Dedicated server has no live XUi caller in this proven chain; none of these six files issues network authority mutation. Neutral menu and creator preview cannot toggle HUD preference via TryResolveTrackableFocus. Generic direct LiveCharacter Open can render global owner snapshot with supplied XUi; confirmed admitted shortcut paths are stronger. No reachable foreign-UI exploit was proved, and no native owner/replica inventory mutation is implicated.

## Concrete low-impact lifecycle issue

PROGRESSION-UI-A: ReturnRegistry's handler limit does not bound its queue. Register enqueues each previously absent token; TryHandle removes only dictionary entry; Unregister also removes only dictionary entry. Queue dequeues only while handlers.Count exceeds 128. Character/Creator generate fresh GUID tokens per opening and normally consume/unregister them, so repeated successful open/return can keep dictionary small while queue grows for the process lifetime. This is a reachable local retention defect (one token string per launch), not multiplayer authority or progression corruption. Queue retains strings, not consumed handler closures; do not overstate memory severity. No production correction made.

## Qualifications and remaining coverage

No new progression grant/trust defect proved here. Generic Explorer source assumes appropriate UI launch; window-routing witness does not automatically extend to UiService. Search results and enabled node filters can remain across Prepare; no loss/dupe inference. Runtime Escape timing, visual clipping, native controller hotkeys, dedicated menu lifecycle and failure injection untested. GraphRegistry/query/overlay/search/projection remainder, HudTracking registry/model/store/atomic helper and native scrollbar implementations are not fully read in this tranche. They must not be marked fully covered from these callers or the partial support excerpts. Prior evidence may cover portions; parent should reconcile exact source manifests rather than infer completeness.
