# Final three finding work packages — independent agent draft

2026-10-15. Parent reserves IDs086–088; IDs below are proposed allocation only. Audit/planning artifact. No main report/ledger or production changes, build, game, tests or Git. Fresh exact-node/caller searches reconfirmed these three findings against current sources. Prior full-read/native evidence remains the authority; hashes below identify current revisions.

## Proposed MP-086 — Native progression omits Camper station navigation

Suggested severity: **medium functional (P2)**. Source-confirmed deployment omission, not a crash or inventory defect.

Exact path: Config/XUi_InGame/templates.xml:1171 includes station_templates.xml only under character_progression('Rebirth'). The sole definition of rebirth_camper_station_navigation is Config/XUi_InGame/station_templates.xml:3253–3305. Unconditional consumers are Config/_Vehicles/camper_station_picker_windows.xml:312 and camper_station_switch_windows.xml:5/8. The picker/group and switch-window includes themselves remain active outside Rebirth mode. Source search found no second definition. Native XUiFromXml missing-template branch returns createEmptyView, with a warning only when DebugXuiLoading is enabled. Therefore native progression has no XUiC_RebirthCamperStationSwitch/tabs at those template instances; do not claim total UI parsing fails. Rebirth progression is the control and does load the definition. This affects a native-mode host independently of the separate remote CurrentCreation defect.

Minimal correction: extract only this shared Camper navigation template into a common Camper template include available in both progression modes; retain one definition and existing public template ID. Do not unconditionally load the entire Rebirth station template file or expose unrelated mode-only controls. One XML integrator owns templates.xml, station_templates.xml and the extracted small definition. C# controller/native station ownership need no redesign.

Scenario code to prepare before testing: extend camper_single_navigation.json, camper_two_type_navigation.json and camper_station_switch.json with both native/Rebirth disposable fixtures. Assert actual navigation controller/tab nodes, correct target installation/window, no Camper switch on ordinary placed station, and exactly one definition/controller instance. Then test host plus real remote owner; verify pointer position and original paid queue/item state remain intact through switching. Dedicated server needs its actual template policy, not a fake client flag. Runtime NOT RUN.

Evidence: CAMPER_XML_REACHABILITY_CLOSURE_AGENT_TRACE.md XML-CLAIM-A; CAMPER_CLIENT_REMAINDER_AGENT_TRACE.md actual controller lifecycle. Node searches reconfirmed current locations.

## Proposed MP-087 — Management inspector indicators have no updater

Suggested severity: **medium functional (P2)** because requested quality/count/durability presentation is missing. This is not an item-state corruption or lock bypass finding. If final report convention treats cosmetic incompleteness as low, retain the concrete mapping defect and scope either way.

Exact path: Config/_Vehicles/camper_modules_windows.xml:145 defines camperSelectedPanel as a plain rect, without a native ItemInfo controller. Empty stat labels are at158–171; camperSelectedCount172; hidden camperSelectedDurability173 with empty qualityNumber177; hidden camperSelectedDurabilityCount180. Current Scripts/Vehicles/Camper/XUiC_RebirthCamperModules.cs:156 Inspect updates only camperInspectName, Description, Icon and Capacity. Full body previously read. Fresh exact-name search across Scripts found no consumer for camperSelectedCount, camperSelectedDurability or camperStatName. These authored widgets therefore remain empty/hidden: their XML presence is not functioning count/durability/quality/stats support.

Scope: selected module management inspector only. Native Backpack/Editor ItemStack quality overlays and vehicle HUD stats have separate native/controllers; no evidence they all fail. Modules are presentation clones, and inspection must never mutate the actual installed module or native selection state.

Minimal correction: wire the existing selected-stack indicators to a narrow owned presentation helper using native ItemValue quality/MaxUseTimes/current condition and existing stat APIs. Hide unsupported attributes for nondurable/no-quality items and clear stale values on empty/new selection. Update only XUiC_RebirthCamperModules.cs and the matching panel nodes. Preserve existing native ModuleSlot selection/border behavior and existing capacity display. No new durability on tools or global ItemInfo/Inventory rewrite. Coordinate shared stat helper access with UI owner instead of copying an evaluator.

Scenario code before tests: extend camper_storage_dashboard_ui.json and camper_station_dashboard_ui.json. Select a quality/durable item and a plain nondurable container; assert exact displayed count/quality/condition where supported, hidden condition otherwise, correct selected-container capacity, and clearing after deselection/removal. Compare full native item snapshots before/after inspection. Screenshot and visually inspect icon, selection border, clipping and stale labels. Verify lock-mode/Shift/drag separately using existing native owner controls; do not treat this inspector finding as proof a lock flow is defective. Runtime NOT RUN.

Evidence: CAMPER_FINAL_UI_AGENT_TRACE.md and CAMPER_XML_REACHABILITY_CLOSURE_AGENT_TRACE.md XML-CLAIM-B.

## Proposed MP-088 — Cached water provider can survive native chunk unload

Suggested severity: **medium functional/conservation risk (P2)** for confirmed stale-provider admission and detached mutation. High conservation severity would require clearly labeling the persistent water discrepancy as a possible consequence; this audit did not observe a reload duplication incident.

Exact path: Scripts/AdvancedFarming/AdvancedFarmingWaterProviderRegistry.cs:746 IsTileEntityLive accepts non-null stored chunk and !IsRemoving. TryResolveEntry:777–801 returns CachedTank/CachedPlot during the eight-game-second cache interval without confirming the queried world's current chunk/tile identity or InProgressUnloading. CheckForWater:219 uses the result; TryDepleteBestWaterStorage:362 directly decrements the chosen tank and marks/publishes it. RebirthUtilities:171/182/188–190 exposes the actual depletion route; CatchupService collects these provider objects for current crop water maintenance.

Exact native closure: ChunkCluster removes a chunk by Chunk.OnUnload then RemoveChunkSync. Chunk.OnUnload sets InProgressUnloading, calls TileEntity.OnUnload and ChunkBlockLayer.OnUnload, not OnDestroy. Native TileEntity.OnUnload is empty and GetChunk returns the retained field. Block layer calls OnBlockUnloaded, not OnBlockRemoved. Custom AdvancedFarmingTileEntities.cs tank472 and plot402 override only OnDestroy for unregister. BlockWaterTank.OnBlockRemoved unregisters but does not run on ordinary native unload. Thus a previously resolved provider near a loaded crop's chunk boundary can remain admissible after its chunk leaves the world map. Cached reads/debits can target that detached object until cache expiry/re-registration/reset. Native persistent tank state need not include the later detached debit; no exact persistence loss/duplication magnitude was measured.

Scope: same-world loaded/unloading boundary and finite game-clock cache interval; world shutdown clears registry via ModAPI. An expiry relookup prunes the absent provider. Do not call this unbounded cross-world cache contamination or eight wall-clock seconds. Source proves invalid liveness; native unload scheduling and reload persistence determine observed gameplay frequency.

Minimal correction: make cached eligibility world-aware and require exact current loaded chunk/tile object, non-unloading state and matching provider type/block before returning or mutating it. Invalidate/unregister provider and active plot registrations at the actual native unload hook; preserve ordinary persisted water and allow reload registration. Keep direct mutation's current-object revalidation immediately before debit, not only a time-based cache check. No new water cap, forced refill, deletion of tank state or broad game chunk patch. Farming source owner owns registry/tile unload adapters; shared native hook owner coordinates only if needed.

Scenario code before tests: prepare an explicit deterministic provider liveness fixture plus real disposable unload/reload flow. Prime cache with a tank/dew collector beside a crop across a chunk edge, unload only provider chunk, query/deplete while crop remains loaded, and assert provider refusal/no detached debit; load current replacement and assert correct fresh water consumption. Include cache expiry, clock rollback, same-position replacement, ordinary removal, activated plot, world reset and remote UI control. Record original saved/current tank water and crop usage so no water is manufactured or lost. Use native read-only chunk/TE identity observations where existing bridge supports them; define any necessary observation API explicitly before scenario execution. No arbitrary unsupported scenario step names. No actual game, test or fault injection performed.

Evidence: NATIVE_FARMING_PATH_CLOSURE_AGENT_TRACE.md NATIVE-FARM-A. Exact native Assembly-CSharp SHA256 FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705. MP034 missing-neighbor light classification is separate and should not be merged into the provider finding.

## Disjoint integration

086: Camper XML owner; no Inventory/NPC/Survivor source edits. 087: Camper presentation owner; shared UI helper interface only by agreement. 088: Farming owner; no Camper receipt or NPC economy work. Parent allocates finding numbers, main report wording and test owner. All scenario code must be prepared/reviewed first; execution awaits the authorized implementation/build/game phase. Existing source findings need not be fixed to complete the audit.

## Current revision anchors

Hashes identify sources reviewed at exact node/method scope in this confirmation; previous linked traces carry full-body provenance.

| File | SHA256 |
|---|---|
| Config/XUi_InGame/templates.xml | D8DE7A0898691F1380C36E54240137E6F9A98E692D2C08B93161F4A565C4A417 |
| Config/XUi_InGame/station_templates.xml | D98607D5A7D5855B83348DD05E6308C485F25F2CEB846119A4DFF6A440F8E33C |
| Config/_Vehicles/camper_station_picker_windows.xml | 590A58A1A3B2E62800CCFE7A78DD57F7C57CA87C81FC54AD61BD1D0268187A61 |
| Config/_Vehicles/camper_station_switch_windows.xml | A57D2E83574E24099D696E051740452822CDAC7D49A43E84ED92180156C5D685 |
| Config/_Vehicles/camper_modules_windows.xml | 7900A961D85CB3E70BD1E5ECAEF912C18E2CB8244550E7F5C8AE017675669874 |
| Scripts/Vehicles/Camper/XUiC_RebirthCamperModules.cs | 4EAFDA31DC00CC86F6890C952FC2E244BB192446864C9E17BE82A92B1BD3A1F2 |
| Scripts/AdvancedFarming/AdvancedFarmingWaterProviderRegistry.cs | 2E1095DCE19224869C79F6E1210D1E9E7C2FD2CBA7DEA527F03C6EB7E47F2D69 |
| Scripts/AdvancedFarming/AdvancedFarmingTileEntities.cs | 54EA85BD307031D8D2A1110B9CDF773C43F7E095C278C41467FCC9198DA98887 |
| Scripts/AdvancedFarming/BlockWaterTankRebirth.cs | A58181B1DE5585C58BBA3F1B081AAF8D3E98D46BD7CE2CADE101E490EFD49A63 |
| Scripts/AdvancedFarming/RebirthUtilities.cs | 980099177F916F60F89C11DAFD73F69C3DBD5F16F84DEDDB0A87DED06DDCE369 |
