# Camper client remainder audit

Audit-only 2026-10-15. Twelve full literal source bodies,1362 lines, manifest below. No production edits/build/game/tests/Git/shared ledger. Reconciled current prior Camper trace source names first. No source or class named RemoteStationView exists under current Scripts; actual view is Picker + Switch + WindowSession. Prior continuation covers RemoteStationClient; this tranche follows its actual control lifecycle. Installer/transport evidence reused from CAMPER_PHYSICAL_AGENT_TRACE and CAMPER_STATION_CONTINUATION_AGENT_TRACE, not credited anew.

## Reachability and native boundary

Production ModulePatchInstaller patches WindowBinding/Validity/Close, queue projection writeback, template capture/clear, pointer hooks and remote pending guards. Native craft activation routes RemoteStationClient, whose server adapter is RemoteStationActions.Install/Handle. XML excerpts: Config/_Vehicles/windows.xml1077 includes camper_station_picker_windows.xml; its window3 resolves RebirthCamperStationPicker; camper_modules_xui.xml4 references that window; XUi_InGame/station_templates.xml3254 resolves RebirthCamperStationSwitch. These XML contexts were checked, not fully reread/credited.

Read-only installed Assembly-CSharp XUiC_WorkstationWindowGroup extraction verifies OnClose order: syncTEfromUI; SetUserAccessing(false); unsubscribe FuelChanged/InputChanged/Destroyed; base.OnClose; LockManager.UnlockRequestLocal. Camper transpiler replaces exactly one unlock call and throws on unsupported callsite count. OpenQueueProcessing sync prefix92 (excerpt only) suppresses all remote syncTEfromUI and preserves lastTickTime in finalizer. Hence remote UI projection cannot overwrite server station through ordinary native close. Native assembly SHA256 reused FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705; no new build/installed mod parity claim.

## Session completion/failure/reopen

WindowSession.OpenCore requires original vehicle/player world, live callback, distance8m, service permission, available native window/controller and unique player/installation session. Remote requires remote world + exact address/installation; local additionally exact runtime station and no transfer. Binds detached state, installs three associations before native SetTileEntity/Open and pointer completion. Failure calls original flushAndRelease before Release; if flush fails, associations intentionally stay to prevent a second editor overwriting unreconciled state. Successful Release checks exact vehicle and player association, removes installation/player/window entries and suspends native actor payment. Reopen is rejected while playerWindows association exists.

BeforeUpdate detects death/range/world/entity/runtime/permission/callback invalidation; marks Closing and invokes native window close once. Admitted/lookup require !Closing. Ordinary Unlock calls original flushAndRelease then Release. Remote switch Unlock instead calls Client.CloseForSwitch with Release callback. Switch waits until grid custody, queue commit and remote pending settle, begins pointer transition, marks Closing, closes source window, waits for exact association retirement/ack, then yields one native-close frame before requesting new grant with presentation intent. No mutation replay is synthesized during wait.

CLIENT-A: refined confirmed switch transport exception defect from CONTINUATION-B. If CloseForSwitch's Send throws during native OnClose, outer SwitchAfterPublication catches Close and resets SwitchRequested=false but leaves Session.Closing=true and all associations retained. RemoteClient also retains SwitchClosePending and SwitchClosed. BeforeUpdate subsequently sees Closing and will not invoke close again; OpenCore refuses another station. Thus prior continuation suggestion that ordinary close necessarily releases this already-closed switch window was too broad. A manually invoked successful native close might reconcile it, but that recovery is not proven reachable by normal UI here. Send exception not induced. No item duplication/loss inferred; retained association is availability and should not be removed before checking custody.

CLIENT-B: no explicit world-clear/disconnect reset exists in these session sources. Weak tables do not guarantee cleanup while native window controller remains strongly reachable: its Session retains vehicle/player, and native UI owns controller. Pointer has proper world checks; server lease transport has separate lifecycle. Do not assert session always leaks across worlds: native UI/window destruction outside this tranche can drop keys, and successful native OnClose removes them. Failure paths above demonstrably retain associations for reconciliation; full native world teardown not newly closed.

## Actual client controls

Picker requires twenty unique labelled slots, filters presses to current nonempty installation, compacts slots for display, owns scroll count, refreshes card presentation .5s and remote overview queries through Client. Update invalidation closes window; OnClose clears callbacks/IDs/inspection and invokes captured release unless selection deliberately retains lease. OnClose invokes release after base.OnClose; if native base throws that captured callback is not reached, qualified exceptional teardown risk rather than independently reproduced finding. Cards show snapshot queue/time/tools/fuel without local progress mutation.

Switch is visible only for current mobile session/overview. Six tabs page across twenty installed positions; buttons revalidate current scope and exact target. OnOpen captures/hides native backpack/top chrome, selects active page; OnClose restores captured chrome and clears tab IDs/groups. No independent save or network authority. SelectionIntent weakly keys local actor; Take consumes once, exact vehicle/expiry30s/physical callback. CapturePhysical alone does not enforce expiry but actual access consumers still validate their captured guard; no bypass inferred.

PointerTransition captures exact primary local player/world/vehicle/cursor/point/target for30s. Current rejects world/entity replacement, death/range, changed cursor/input style and cancel/back actions. Expire/CompleteLater and HoldsCursor clear original pending only by exact identity. Completion restores saved pointer once to exact matching current window; no OS cursor override, repeated position loop or server mutation. Soft cursor allowance/setvisible patches preserve native hidden flag and captured cursor scope. Diagnostics are bounded first-navigation debug trace30s and event budgets; debugStarted is never reset, so future navigation diagnostics do not restart in another world. This is observability limitation, not retained cursor permission.

OverviewSnapshotPolicy rejects identity change, lower station-revision, and same-revision slot identity/state revision regressions; higher station revision accepted. It does not compute crafting progress. QueueProjection requires admitted native window and matching card/queue capacities, rejects repairs, syncs native presentation then SetRecipe on cards and marks noncrafting. Writeback cannot introduce/replace paid rows outside scoped queue processor; projection exceptions do not authorize debit/retry.

## Server policy, commands and persistence

RemoteStationActions.Handle verifies current exact transport lease before every branch. Recycler resolves actual installed supported state and rejects grid custody; recipe input name/slot/operation equality validated. Craft resolves unique server recipe by name; client CraftPlayer flag must match server source policy. Native non-Survivor branches use NativeActorPayment/NativeCrafting. Survivor temporarily scopes RemoteUsesPlayerPayment delegate to original actor/station/lease and restores previous delegate in finally. ObserveTerminal uses character records and current installation to invoke existing queue/training/refund recovery; enumerations that can retire refund records use snapshots. This dispatch does not mint completion receipts or accept client queue arrays.

PlayerSource uses actual loaded native workstation template when available, otherwise TemplateSourcePolicy, including dedicated server with no UI. Policy captures four merged XUi documents at Load and clears at ClearData; limits each8MiB, expands templates bounded8192nodes/depth64, rejects ambiguous/missing controller/window/input counts and conditional input grids. Uses native ReflectionHelpers XUiC_ resolution; zero input grids =>owner payment, one=>station payment. This is server-derived policy, not client declaration. Full native XML template override semantics were not independently decompiled here; no schema-bypass claim.

RemotePendingSync only guards server Camper updates containing flags12 and only existing runtime with TransferPending; importantly does not create runtime while saved entity item is still loading. PendingLock delegates scoped existing server authority. PendingCommand denies ordinary native activation while transfer marker is held, custom Camper command paths retain their own authority. These hooks do not authorize remote inventory setters. All persistence/custody receipts remain prior publication/refund/remote owner application evidence; UI state itself is transient.

## Full source manifest

| Source | Lines | SHA256 |
|---|---:|---|
| Scripts/Vehicles/Camper/RebirthCamperRemoteStationActions.cs | 109 | 4C3789627E41F1934B413A0C24D8F9E1E62D202D69EC73E2E4A0670AE0A93EAB |
| Scripts/Vehicles/Camper/RebirthCamperRemotePendingGuards.cs | 52 | 4F5219294E8F977DDF14DAE7222FC1A74CB05A23763611A1B27A533F5A624205 |
| Scripts/Vehicles/Camper/RebirthCamperRemoteStationPlayerSource.cs | 32 | 0859904B24BCA355F74E119074FFE0BE7A1A0B5F546D7A605EBFB93F69F5B33C |
| Scripts/Vehicles/Camper/RebirthCamperStationPointerTransition.cs | 265 | 85B8A96A02FB6885B1781D35F90F057C7687B1F528D53D71235986BC5B02E5EF |
| Scripts/Vehicles/Camper/RebirthCamperStationQueueProjection.cs | 50 | A818D9E8976DCC45DDE8060C5457B0F8E4EEF4D5E149E30DBA357B03121F8CC0 |
| Scripts/Vehicles/Camper/RebirthCamperStationWindowSession.cs | 324 | C208EC51A30854AC8C90C0DF53B7D0CAF62513B7EEDF1BBF6B2161D62C4E5150 |
| Scripts/Vehicles/Camper/RebirthCamperStationWindowBinding.cs | 55 | 218C0C2F27FDDCECE22D29309A209965F0E526B18D608A765AAEE81A532770AD |
| Scripts/Vehicles/Camper/RebirthCamperOverviewSnapshotPolicy.cs | 21 | BCE0CA11B4E89875DA584F83D277AEF7C8A4B7B7385AEF71B9F5104D6C0D9B3C |
| Scripts/Vehicles/Camper/XUiC_RebirthCamperStationPicker.cs | 159 | 28DA275E7E17E1738D19F25467E594AC10E75348AFE17589D6F7CE9BBAC303C4 |
| Scripts/Vehicles/Camper/RebirthCamperStationTemplateSourcePolicy.cs | 103 | 017BA17BD18A026545359A5F32053D338457769B29360EFD0AD8A882EEC75C27 |
| Scripts/Vehicles/Camper/RebirthCamperStationSelectionIntent.cs | 24 | 16A698414C99217B48963A68348D091B6F21594DD59E33B05E6C10A5FE29051E |
| Scripts/Vehicles/Camper/XUiC_RebirthCamperStationSwitch.cs | 168 | D7B182FBD6B0DC44F3A132812274D2E388AD80A4F9F47F4104BA33416DEA0467 |

Hashes taken after full reads. No behavior execution, exception injection, build or layout screenshot. Only twelve manifest files claim full coverage; external native method/XML excerpts and reused traces remain bounded evidence.

