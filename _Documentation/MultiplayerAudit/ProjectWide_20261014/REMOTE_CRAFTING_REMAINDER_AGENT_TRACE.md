# Remote crafting remainder: reservation, owner grants and recovery

2026-10-15. Audit-only current cumulative REBIRTH3.0 workspace. Fifteen previously pending RemoteCrafting sources were read in full actual contiguous content; five previously fully reviewed sources reused at current matching hashes. No production edits/build/tests/game control/Git/shared report or ledger edits. This document is the only new workspace artifact. Parent owns camper refund work; this evidence does not replace that chain. Operating directives and current coverage ledger/AUDIT_REPORT consulted; FETCH_TRACE reviewed for native setter-hold qualifications, NATIVE_TRANSPORT_TRACE for source adapters/consumers and native owner actions, NPC_HAULING_AGENT_TRACE for full Network review, PERSONAL_CRAFTING_AGENT_TRACE for exact native queue/save transport. No duplicated issue numbering.

## Exact source scope

FULL new reads: ConsoleCmdRebirthRemoteResources, RebirthResourceItemFingerprint, RemoteResourceAvailabilityCache, RemoteResourceClientTransactions1398, RemoteResourceLiveSync738, RemoteResourceRefundJournal/Producer/Record/SaveCheckpoint/SupportPersistence, RemoteResourceToggleCommand630, RemoteResourceToggleLegacyPackages, RemoteResourceWorldCustodyScan/WorldIdentity, XUiC_RebirthIngredientEntry. All fifteen current hashes match original coverage inventory. FULL prior review reuse only: Network1068, Transactions462, Consumers382, NpcSources210, ActionConsumers248. Their current hashes match ledger; native caller/recovery qualifications from prior traces remain in force. Shared QuickStack/source-discovery/security/character repository and native extracts are dependencies read only at explicit boundaries below, not newly claimed full.

## Production reachability and role

QuickStackCore.cs:685-699 InitMod explicitly calls RemoteResourcePatchInstaller, AccessEventPatchInstaller, ConsumerPatchInstaller, ClientTransactionPatchInstaller and ActionConsumerPatchInstaller without DEBUG guard. Runtime Enabled gates work; dedicated client means connection exists and !IsServer, which includes a remote client on listen host, not just dedicated-server topology.

New full ClientTransaction installer sets installed=true before applying six Harmony classes. A failure during any class leaves future Install returning early: this extends existing MP-013 installer staging/retry class, not a newly observed missing hook. AccessEvent installer instead sets installed after eight patches and GameUpdate registration. Existing shared PatchClassOnce qualification/rollback MP-017 applies; full bootstrap previously reviewed.

Network.cs exact AfterWorldUnload1010-1020 flushes source/category state then clears registry/client availability/client pending leases, changes client epoch, resets server outcome journal and logistics scope; World.UnloadWorld postfix1062-1067 invokes it. LiveSync world replacement clears availability subscriptions/projections. These are actual callers; clearing unresolved client/server custody is the existing MP-009 issue, not successful delivery reconciliation.

## Availability is projection, not payment

Authority SnapshotCache stores exact Player reference, world, 2s expiry, position within1m, registry/state generation and workstation access revision. Build resolves nearby sources through established discovery/eligibility, excludes locked remote slots, clones stacks and type-count provenance, and excludes installed-mod items from consumable counts. Full source payload clones preserve metadata for stack projection; counts are deliberately type totals. Source invalidation filters only snapshots containing changed IDs; global/access/player invalidation increments projection revision.

LiveSync batches dirty source/player/station changes, bounded4096 before global dirty fallback. StationProjection serializes complete native item payload/count, busy and security access state, avoiding lossy integer-revision equality. Pump on GameUpdate runs owner receipt publication before server-role branch, validates exact current station identity, coalesces one player discovery/rebuild and sends only subscribed remote player projections. Remote subscriptions retain exact entity object and request nonce. Moving/new eligibility is also re-evaluated by cache TTL and requests. Invalidation cannot prove actual source debit publication; Transactions owns that boundary.

ClientAvailability requests have monotonic nonce, recorded World and Player,15s outstanding timeout,200ms minimum send throttle and2s refresh. QueueReceived rejects wrong world/nonce and nonincreasing sequence; newest pending envelope wins. PumpReceived checks current world/primary player/access revision before dispatching XUi inventory-change events. Clear discards cached data and request scope; no mutation or resource credit. GetCount/HasItems aggregate by type, GetSources provenance distinguishes source names/distances. Source count packet bound4096 and per-source type entries65536; full native stacks are serialized via GameUtils. This projection is not an entitlement or owner receipt.

## Native action preflight and authoritative withdrawal

BeforeCraftActivated: authority refreshes snapshot and passes native action; remote creates exact native modified requirement list via entry.hasItems. If local resources or selected station input grid already cover requirements, passes natively. Otherwise calculates shortfall, begins bounded128 lease with random process/session epoch and monotonic request ID, records actual action/recipe reference/count/full requirement list, and sends shortfall only. HasPendingInventoryOperation exposes lease/delivery-depth/grant hold to other inventory protocols. HasPendingCraftForUi preserves count while resources reserved so UI availability refresh does not clamp the original batch.

Repair preflight preserves native partial-repair behavior based on available first RepairTools entry, records item type/wear and requirement. Block-repair captures target/material fraction and existing repair progress; block-upgrade waits actual configured hit threshold and derives requirement. These are admission helpers and native replay, not independent crafting implementation. Local organization locks intentionally do not exclude local crafting materials; remote source locks do.

SendRequest binds current remote World, connection, persistent owner and live channel. False unsent result retires unissued lease; send exception deliberately retains lease because delivery is ambiguous. UpdatePendingTransactions queries every3s indefinitely with QueryOnly and same epoch/request/operation, never re-executes a lost unknown request automatically. Max128 prevents unbounded admission but has no durable owner outcome receipt.

ConsumeRequest authenticates native sender entity+user, persistent identity, remote/server direction and valid op1..4/mode. Server journal Resolve independently rediscovers and TryConsume's remote-only shortfall (includeLocal=false), capturing proven removed list even if later source throws. Per-world lock/claim protects duplicate execution and reentrant same-key request. Existing terminal is replayed, unknown query does not debit, completion checks same claim and world before publishing. Key is authenticated user|epoch|request|operation. Result carries exact removed ItemStack payloads; request/result count bound64. Original owner-native queue semantics remain accepted and preserved.

Native actual queue-before-void-removal and exact Get/SetCraftingData restoration are in PERSONAL_CRAFTING_AGENT_TRACE; MP-042 covers authority path specifically. Do not infer remote preflight is identical: remote server removes shortfall first and owner native removal consumes a temporary grant context. Original native ItemActionEntryCraft/Repair/ItemActionRepair and Bag/Inventory setter behaviors remain version-qualified by existing native extracts. Installed assembly identity previously verified SHA256 FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705; no current binary/play execution here.

## Owner replay, conservation and scope limits

ReceiveResult matches current lease epoch/request/operation; Unknown retains lease/query. Terminal increments delivery-depth, removes lease and pending action before replay/refund. Player is resolved from current primary world. Craft verifies original entry still has same Recipe reference and count; repair verifies type/wear; block repair checks target/current requirements; upgrade compares original target block requirement against stored requirement. These checks do not capture full original connection/creation/UI epoch/item payload identity like the separate dormant capability handshake. World unload ordinarily clears leases; retained same-world UI replacement/mutable action differences require qualification, not a claim of demonstrated cross-character delivery.

GrantContext is synchronous ThreadStatic. It clones removed stacks/type counts, lets patched counts/HasItems see grant, consumes local remainder, decrements temporary type counts, appends removed items and sets consumed=true. Native replay stays owner-local. Local ConsumeLocal uses direct Bag.SetSlot/Inventory.SetItem, excludes installed mods, and decrements its planned remainder after each void setter without actual postimage/readback. Native setter-hold rejection is known from FETCH_TRACE; actual craft/repair admission under each competing energy/library/gear hold must be proven before asserting duplication/loss. Do not disable those holds or generalize conditional setter refusal into an observed race. Library reservation checks exist throughout this grant path and other protocols can inspect pending-resource hold.

MP-008 confirmed: terminal pending is removed before refunds; RefundRemoteToLocal clones each returned stack, tries belt then bag and logs if placement fails, no retained remainder/receipt and misleading unconditional refunded log. Do not recreate original whole stack after partial insertion, silently drop or invent world-drop fallback. A replay exception also runs finally End to discard temporary context but bypasses later consumed/refund continuation after lease removal. This is an exception-boundary recovery qualification extending same custody issue; no exception injected.

MP-009 confirmed: server outcome dictionary memory-only, capacity2048 evicts oldest terminal regardless unacknowledged owner settlement; client Clear discards pending/epoch and world reset clears outcomes. No owner applied/refunded acknowledgement or serialized pending producer. Unknown queries after eviction can continue indefinitely; restart/reconnect cannot reconstruct lost client original intent. No unused-grant delivery protocol is implemented by dormant refund preparation files below. Reliable transport alone is not persistence or exactly-once owner credit.

Existing MP-040 multi-source partial commit and MP-041 stale companion returns remain source-adapter failures covered elsewhere. Full current ClientTransactions/LiveSync do not compensate those defects automatically. CookingPull op5 is handled by its separate service, excluded by ConsumeRequest op1..4; its use of same outcome journal is existing MP-010, not repaired by ordinary transaction query pump.

## Refund data is preparation, not live recovery

Full RefundProducer comment explicitly says deliberately not called by live packets until saved-unused verifier and exact native owner credit/save exist. Production search finds no caller of TryPrepare; its internal calls are sole callers of world identity and checkpoint. Journal.TrySettle has no live delivery caller found. Support persistence can retain/reload a saved image but cannot create entitlement or prove delivery.

Record validates exact world/owner64lowerhex, normalized creation, nonzero epoch/request, operation1..5, strict XML shape,1..64 stacks, count1..ushortmax AND<=nativeMaxCount, exact canonical native item codec payload round-trip and total text budgets. It stores immutable XElement image; getters clone/decode exact item values. This preserves identity/payload, not authority by parsing.

Journal validates no duplicate epoch/request keys, exact owner/world/creation, strict entry shape,128entry/4MiB encoded budget. Register stages candidate then saves under current guard before replacing in-memory journal; duplicate exact settled identities cannot re-credit. Settle requires supplied saved-owner verifier, exact retained image and durable save callback; those callbacks are contracts, not implemented native evidence. Settled identities remain retained, so capacity is conservative admission refusal rather than silent pruning. No production recovery is claimed.

Producer would resolve authenticated current remote gear owner/character, exact native user canonical identity, original cached record and save root; read opaque retained removal handle; build full-unused removal image only after supplied saved-unused verification. Rechecks scope/handle/world identity around persistence. It cannot validate an arbitrary caller-provided item list, and cannot automatically infer partial unused quantities. Its verifier is missing live integration by design.

SaveCheckpoint locks active record against reentrancy; validates current identity/creation/world image, stages support journal and marks dirty, never rolls uncertain save candidate back to an older image. Calls SaveIfDirty then requires HasSavedRemoteResourceRefunds exact final-file witness; idempotent in-memory registration alone insufficient. Partial repository read: HasSavedRemoteResourceRefunds70-84 loads validated current final record, checks creation/current path and deep image; support Write includes exact image. Wider Repository durability/recovery is parent persistence scope, not newly certified full.

WorldIdentity is save-owned64hex key in RebirthData/RemoteResources/worldIdentity.xml. Existing malformed primary or observable .bak/.tmp refuses generation; retained cached refund image or custody scan forbids creating replacement world. New key stages unique file, validates/flushed durable bytes, no-overwrite move and exact readback under current authoritative World/save root. CustodyScan checks only initial absent identity, rejects reparse/unreadable/oversize player records and any remoteResourceRefunds node, bounds4096files/64MiB total/16MiB each, never edits records. Conservative refusal is intentional, not a license to delete retained custody.

## Static-container toggle and presentation

ToggleCommand source Append/CanManage use actual live authorized source; no read of client-owned authoritative store assumed. Authority state/toggle requires main thread,current exact World, live player+persistent user, distance<=8m, loaded authorized source. State read intentionally enrolls untouched authorized opened static container if default-on and not excluded; it is an authored mutation, not pure query. TryReadState rejects workstation; toggle supports static or workstation adapter, updates per-user exclusion/activation and source invalidation. State store persistence itself is reused Network full review, not new independent save proof.

Scoped request handlers additionally verify exact current Clients.ForEntityId Sender. Client replies require current remote World/primary Player/exact tile object/position/UI reply scope and monotonic request ID before UI apply. New open/close/changed container invalidates scope; late replies cannot retarget replacement tile. Legacy four packet types retain wire readers but ProcessPackage is empty and cannot mutate state or UI. UI controller validates static storage feature identity/policy, refreshes twice per second, default unselected/On until authority response, sends state on changed/forced scope and toggle on click. It does not credit items.

Exact XML windows.xml:883 button controller and templates.xml:400-409 ingredient-card controller/tooltip inspected. Full XML/include/runtime atlas/localization not read here. IngredientEntry tooltip mirrors native selected source pool: material grid weight or workstation input grid when present, otherwise owner backpack/belt plus server/client provenance. It reads cache; it does not rescan/debit independently. Full payload eligibility remains native/transaction source responsibility. Diagnostics console explicitly scans sources/status/refresh and toggles debug boolean; it resolves remote console actor via sender entity, otherwise primary local player. These explicit diagnostics mutate caches/logging only; no automatic diagnostic execution or item grants.

## Conclusions and qualification

No new independent issue ID proposed. MP-008/009 confirmed with exact owner replay/throw boundary; MP-013 staging extends client transaction installer. Preserve normal native client inventory/queue architecture and original holds. Stage any correction through real original-operation settlement, exact remaining payload and durable applied-owner witness, not generic server replica mutation or claimed dormant helpers.

After parent's checkpoint, grouped qualification should include host/remote dedicated/listen local-only control; mixed local/remote/companion partial source failures; changed recipe/count/quality/window/repair item/upgrade target during reply; full/partial refund capacity; thrown native replay; repeated/result/query loss;2048eviction128pending; reconnect/world/save replacement; retained inventory holds; toggle replacement tile and late replies; disabled policy; exact metadata/quality/ammo preservation. NONE RUN. Native/direct helper reads are not runtime proof.

Source/design/evidence review PASS for exact15new files and5matching reused source files; architecture preserved PASS; documentation PASS; target XML bindings partial exact reads only; networking/recovery static trace PASS with known defects/unverified dependencies; implementation N/A; compile/in-game NOT PERFORMED per gate; packaging N/A evidence-only. COMPLETION VERDICT: COMPLETE for this source tranche; NOT COMPLETE for fixes, whole project, downstream source/persistence certification or multiplayer acceptance.

## FULL new source manifest

Actual content read completely; current hashes do not grant future review credit after changes.
Scripts/Crafting/RemoteCrafting/ConsoleCmdRebirthRemoteResources.cs | 170 | FCEC353182807F1DBD5AE574F2B4E72D81F07DA4F0B5FE09C2A6EE96F221CEDB
Scripts/Crafting/RemoteCrafting/RebirthResourceItemFingerprint.cs | 25 | 4610E3A6C99580C790E3DD5CADC0E63E6A529CCE1AF5B95F30CFEC86469CF4D3
Scripts/Crafting/RemoteCrafting/RemoteResourceAvailabilityCache.cs | 274 | 25A5CD1F2B7F95FC4A749DC155752BD1E5BCDC03A4D5D274A056D897AA8CA716
Scripts/Crafting/RemoteCrafting/RemoteResourceClientTransactions.cs | 1398 | 62E75AA07AC932199575A6D6F1406F112EFD59E8360C231D4755CEC0584EF84C
Scripts/Crafting/RemoteCrafting/RemoteResourceLiveSync.cs | 738 | E19976D8B3FC3EC1C9CB4AA9F7FEA492B154C299F6D5A845B2E8AA99F61F0E90
Scripts/Crafting/RemoteCrafting/RemoteResourceRefundJournal.cs | 112 | 190B7550C069FE5E7A9DE9D536A2DD20DBB2C1C7D8542800F6BEAAB22F8C51B9
Scripts/Crafting/RemoteCrafting/RemoteResourceRefundProducer.cs | 54 | 110FAA317DC7A3531BABEF19B7F8931C69A08A410CE17540650D0D8594513429
Scripts/Crafting/RemoteCrafting/RemoteResourceRefundRecord.cs | 97 | 78E1D1A752D69304C4BAAC34D4A489B72FC8BF6B623EDB3FACAFDCDF7996A2D1
Scripts/Crafting/RemoteCrafting/RemoteResourceRefundSaveCheckpoint.cs | 59 | D8CE972064DEAFEAB306199C9A733E9B9C099FBF2D0A1FFC3C8B233B30AFDC08
Scripts/Crafting/RemoteCrafting/RemoteResourceRefundSupportPersistence.cs | 25 | A4F24E6C9ACB6F88C340656FFD62A2FD0C2A5F0560EABB6AE5FE9BDDE7066051
Scripts/Crafting/RemoteCrafting/RemoteResourceToggleCommand.cs | 630 | A1F1D9DC64B53F81E6D2ABC0064717AA60351660FC8E47CC1876D797A3A48FB7
Scripts/Crafting/RemoteCrafting/RemoteResourceToggleLegacyPackages.cs | 97 | 7AB77D5F7A278798F0FAC46011D0A861558845CB7844DCBA022844784889893B
Scripts/Crafting/RemoteCrafting/RemoteResourceWorldCustodyScan.cs | 45 | 0333AD94A9771370A88CE40DA1A888C518E2FEA9E7A7B6900CD7818D8DD2A80C
Scripts/Crafting/RemoteCrafting/RemoteResourceWorldIdentity.cs | 80 | 3B0A9914E98B8226B84EAE5C5911C8AA327BFC52F045878D4E10700407FD3C95
Scripts/Crafting/RemoteCrafting/XUiC_RebirthIngredientEntry.cs | 207 | EAD3DD96DA74492C26BF6A1F61F6569CC3E066A09F8E422296B2A1CFD9B76D53

Reused prior FULL source evidence, same current hashes (not reread full this tranche):
Scripts/Crafting/RemoteCrafting/RemoteResourceNetwork.cs | 63A54594440BCFE4E9C2966BB7939CCDED2A98A2C6DDF4BBE5E7E828F4FE97D9
Scripts/Crafting/RemoteCrafting/RemoteResourceTransactions.cs | A7535727B1947CE11D3519AA3E5E8FE77849A88AEEBE98BD765AC2948BE6A002
Scripts/Crafting/RemoteCrafting/RemoteResourceConsumers.cs | FEA3B2BB06A3F77A247CD3B35226E1B31396361B33FA7D5032D044C7094EB3CC
Scripts/Crafting/RemoteCrafting/RemoteResourceNpcSources.cs | F1D88571CF6AB12C8360C5CDDE69383F7617A13E889CF97CEAD59EB0A557AE95
Scripts/Crafting/RemoteCrafting/RemoteResourceActionConsumers.cs | 35D2E4D96980C779F245AC53715E4F17C7F6D4A368ADED3FE3161EEEB85D5423

Partial current dependency identities (named members/excerpts only, not full new coverage):
Scripts/QuickStack/QuickStackCore.cs | 433BC2C79B93ABCB686EFFA2792B5371CA8A58F6DC2B2494530FBB4DDD8389D8
Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs | 0E23128DD664D51FD346B141DF6801ED9B3FB3AC289914AF9CA2C7DFF736D0FD
Config/XUi_InGame/windows.xml | D7D2B5BF991A2F5318A3F5591A9F883E43B7415194C2209042ED9A59253B0E8C
Config/XUi_InGame/templates.xml | D8DE7A0898691F1380C36E54240137E6F9A98E692D2C08B93161F4A565C4A417
