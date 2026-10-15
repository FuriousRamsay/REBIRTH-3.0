# QuickStack smaller-source remainder — 2026-10-15

Audit only; no production/game/commands/endpoints/build/tests/git. FETCH_TRACE, AUDIT_REPORT existing category-response/legacy stopwatch findings and NATIVE_TRANSPORT MP047/partial external coordinator boundary reviewed first. No prior partial core/service reference treated as full credit. rg -uuu callers include ignored source.

## Full current bodies

All names below under Scripts/QuickStack/. Eleven pending source bodies2667 lines fully read. ItemPolicy8 additionally full-read but parent pending count already excluded it; zero new pending credit. LogisticsTransferService5034 and QuickStackRadialUi1491 NOT yet full-read: remain explicit next tranche, no full credit.

| Source | Lines | SHA256 |
|---|---:|---|
| ConsoleCmdRebirthQuickStack.cs |185|BD1503704E9463B12A41BDF1A6BCD29018A9E08B7C0081D9E7F96BD8C9988E9C|
| LogisticsBagCodec.cs |52|A96EC163BCE489E9EFE4B9BC60C18D8437C5D49F03B88AE1BC6E06B21FCD9147|
| LogisticsInventoryCredits.cs |70|D8AE746829E011778F479D9C1977BBC9FDD30964F57F2745333DC457914F79A1|
| QuickStackCategoryUi.cs |392|6BE137D7FA286111B259A1001811CF8AF941F8FC54063F82DA71DA485EB3C3E5|
| QuickStackContainerCategoryCommand.cs |42|22756E359B60A143875DBCD7AAF31AA42EBCD8344F79F9DB539AA65E9FE582EB|
| QuickStackCore.cs |699|433BC2C79B93ABCB686EFFA2792B5371CA8A58F6DC2B2494530FBB4DDD8389D8|
| QuickStackExtensions.cs |583|30714C5D444C015E64818ECA74FECDC2A263B720CA084D32A1E7746C02F2DD12|
| QuickStackTypes.cs |13|B93D30804318E790A09EA1D965BBE213B2E2EABC97B51357A4253C8C4F529C38|
| RebirthUiIconAtlasPatch.cs |31|2677759468E174ADC04482181B9074F64F88D8A46BE5BDABD766103A0FAA7DD8|
| XUiC_RebirthItemQuickStackCategory.cs |486|42CFBDA7E0BD4C758F0EEBBF1362931ABF6DED323D85FCA0F141B9EE4C2CECF0|
| XUiC_RebirthQuickStackCategoryButton.cs |114|80A572DB14E6B677CEE44531F0A45C1DEE24CA079573561801FCDD26BA3BD030|
| QuickStackItemPolicy.cs |8|AD7F3B949DAED64E8CC502CC9CC47A4D086D887685E0BF937EEA63136B2EAF66|

## Active release/caller chain

Core685–698 ModApi.InitMod explicitly installs remote-resource/index/access/consumer/client transaction/action hooks, QuickStack patches and companions then applies sandbox state. SDK sources outside Debug compile both. QuickStack installer sets installed before Harmony patches (existing MP013 family). Backpack Init handler opens modern QuickStackRadialUi, not legacy RequestLocal; console run invokes legacy QuickStackService.RequestLocal, restock invokes legacy RestockService, preview legacy PreviewService. Existing stopwatch null failure at Core573 confirmed full body: on multiplayer diagnostics off mutates then sw.ElapsedMilliseconds throws. Preserve route specificity.

XUi XML actual consumers: Config/XUi_InGame/xui.xml47–48 category window group; windows.xml11 category controller;882 loot toolbar;885 ItemInfo tabs;898 category page. ItemInfo source includes WeaponTrainingPanel display consumer of previously full DPS service, no new award producer. Atlas patch explicitly installed; changes only custom categories sprite atlas. XML/native UI presentation remains active with policy-bound feature behavior.

## Sender and category authority

Category requests require ValidEntityIdForSender+ValidUserIdForSender, then native server entity persistent PrimaryId agreement for save. CanManage requires supported persistent static composite storage, <=6m and RemoteResourceAccess.AuthorizeComposite(false). Ownership itself is intentionally not mandatory: accessible container categories are ordinary direct interaction and editing while looting is allowed; do not report this as missing ownership permission. Child coordinates canonicalized; save registers real tile and removes alias. Client mask normalized by defined enum loop. Server read result contains only position/mask; client opens current primary UI without request nonce/world/player/target/dialog lifetime check. Existing late-response finding confirmed, no duplicate new issue. Save closes UI without server save ACK/result; rejection or persistence failure not presented.

ContainerCategoryCommand recalculates enabled native command then delegates OpenForContainer, which reauthorizes. Loot button refreshes .35s, hides unauthorized targets, custom name independent permission. Press uses remembered coordinate but server reauthorizes. Coordinates do not pin exact tile identity, so authorized replacement can inherit category edits; no unauthorized replacement exploit proven.

## Category persistence/reset failures

Extensions registry is world-file position map, no persistent crate GUID. Query migrates multiblock aliases in memory and marks dirty without synchronous writes. Save flushes(true), shared atomic publish, dirty retained on failure. **Load both primary and backup failure returns with loaded=true and empty accepted dictionary, no blocked-write state; later SetMask Save can overwrite corrupt original primary with partial replacement.** No max bytes/count or schema version fence; unrecognized root refuses, unknown extra nodes ignored. TryParse enum permits arbitrary numeric undefined categories (246), only excludes None. Such values can be saved and alias/mask bit shifts collide modulo32; SetMask later drops undefined values. Local save corruption/hand-edited numeric values, not network mask injection (network path normalizes).

AfterWorldUnload in previously full RemoteResourceNetwork1011–1020 flushes then unconditionally ClearWorld, so failed category publication's dirty memory is discarded (MP036 family; file primary preserved but unsaved changes lost). Also clears shared source registry/client coordinator/journal and client scope there. No extra world identity inside category loaded flag; lifecycle callback reliability remains parent native hook evidence. Backups recovery yields older configured routing when primary invalid; does not preserve latest unknown/corrupt state.

## Physical transfer: legacy and shared executor

Core planner refuses CanPlaceInContainer-false sources (fetch items), exact native item stacking and explicit category opt-in; global owned topup/category before other accessible containers. Protected occupied destinations can top up, protected empties excluded; source bag locks excluded. Planner uses current world source registry, sorted distance, CommitExternal reauthorizes permission/busy/owner class and reads live capacity. It does NOT recheck category assignment or range inside destination loop, but synchronous caller means concurrent main-thread UI edits require actual reentrancy evidence, not assumed threaded race.

Shared CommitExternal updates native destination slots before decrementing cloned remaining source and returns count; caller owns original source debit. Commit then SetSlot original bag without verifying success. Restock inversely fills bag via SetSlot then decrements native storage, without setter readback. Native owner mutation guards can refuse setters while external custody holds (previous FETCH/native evidence). Exact admission prevention in modern service remains next source; classify conditional custody failure, not demonstrated duplicate/loss. Same shared executor is called modern external source at Service3182/3223 and must not be dismissed solely as legacy; caller transaction/catch/rollback needs full service trace. Metadata merge follows native CanStackPartlyWith; no extra full serialized metadata equivalence guard in these transfer loops.

Legacy remote deposit uses server live bag without owner snapshot protocol; legacy result only counts/logging, no original-owner inventory settlement. Restock legacy likewise edits server remote bag and storage with no return inventory package. Native baseline latestPlayerData uploads don't keep remote live grids current (prior native evidence), so legacy console route has stale owner inventory architecture limits. Modern coordinator can hydrate/protocol-settle separately; do not extrapolate to it before reading. Console Execute uses primary local player and ignores sender identity for run/restock/debug, so on listen server native authorized remote console could operate host; command permission/native routing still external dependency, no bypass claim.

BagCodec uses explicit v3 request consistency envelope <=256 slots bounded before allocation, native full ItemStack read/write, no import of client lock/preferences. No auth itself; consumer decides consistency. Credits derive added-item delta by native ItemValue.Equals then apply into current primary player's bag, drop remaining via ItemDropServer. Apply has no own receipt/session/owner/saved ACK; only Service2346 caller found. Whole service caller must establish dedup/fencing; absence here alone is not a finding. Same for native drop success: void call not persisted receipt here.

Legacy preview static one callback+pending action matches action only, not request/session/world. Rapid identical action can feed stale preview to later UI; source finite lines12 but amount is source quantity not capacity-computed committed count. It is advisory, not custody proof. Unbounded ReadString handled by native envelope only. UI display category cache per item and DPS fingerprint are local presentation; finite formatting relies on validated upstream profile. No source persistence/award writes in item tabs.

## Next required scope

Read full LogisticsTransferService5034 and QuickStackRadialUi1491 next: request scope/correlation, owner snapshots/locks/checkpoints, credits dedup/ACK, native output and companion endpoints, service exception phases/retry/reset, normal modern UI physical state. MP047 Transfer/Mutate mismatch already established parent trace; reuse rather than rediscover. This trace closes11 pending smaller files only; not whole QuickStack audit completion. No new live exploit promoted without caller closure.
