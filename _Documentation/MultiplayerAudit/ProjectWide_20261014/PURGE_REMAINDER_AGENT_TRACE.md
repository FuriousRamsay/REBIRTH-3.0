# Purge objectives, supply scheduling, quest policy — 2026-10-15

Audit-only. No production/build/tests/game/Git/shared report or ledger changes. Preserves accepted native2.6 launch-consumption architecture and rejection of expanded supply disk-confirmation redesign. Current coverage ledger had seven Purge SOURCE READ rows and99 pending. No PURGE_TRACE file was found by rg -uuu filename search; read dated SUPPLY_SAVE_AUTHORITY_TRACE_20261011, REMOTE_SUPPLY_REQUEST_TRACE_20261012 and PLAYER_REWARD_ENTRY_TRACE_20261012 instead. Existing findings and earlier runtime qualifications remain limits, not silently promoted to certification.

Exact literal full scope35 C# sources plus four XML files below. Initial combined output omitted SupplyAccount middle and MilestoneNotification middle; separate reads completed both. Objectives count is15 (early scope estimate16 corrected). MapModApi, WorldModApi, KillContributor and NoticeDelivery were added to close executable callers. Hashes represent actual full content reads, not traversal. Additional native/loot dependencies remain named-method/partial.

## Executable installation, authority and lifecycle

ReleasePolicy is compile-time PURGE_QUALIFICATION only. Current documented Debug configuration defines that by default; ordinary Release does not. Thus all described gameplay paths are qualification-active, not universally enabled in a default Release DLL. WorldModApi Preserve IModApi loads four policies and individually guards five native event registrations against partial retry duplication. Its update calls world lifecycle, census, progress, milestone, discovery, maintenance, supply account and coordinator in sequence; starting/shutdown resets these owners. MapModApi installs three map hooks transactionally and pulses map sync/notice delivery. ThemeModApi installs eleven exact Harmony hook classes with UnpatchSelf on failure and sets installed last. SupplyModApi resolves all14 exact targets before patching, includes SpawnSupplyCrate finalizer to dispose scope, rolls its own patches back on failure and sets installed last. Native actual mod-loader discovery and current installed DLL parity were not executed.

Theme disables new trader generation/cache/list, Trader-source AddQuest, recognized trader PartyShare acceptance/addition and server ShareQuest events, and trader dialog offers. Existing owned quest loading and share removals remain; recognizes trader position or existing normal-trader statistics classifier, plus vanilla trader giver. No inventory deletion is performed. Exact native share/quest implementations were not re-extracted here; runtime multi-peer acceptance/rejection remains unqualified. Biome suppression uses a closed named buff/variable family for EntityPlayer only; ordinary weather/injury/trap hazards are outside that family. Weak-table cleanup is per native buffs object and sandbox revision; AddBuff/Tick hooks apply on original local/remote execution.

LootRefreshHook stops authoritative TEFeatureStorage.UpdateTick in Purge, allowing remote/native outside-mode calls. Named full method read of existing VehicleUIReportedInspect/TEFeatureStorage.cs:224–254 confirms native timed refill/touch/reset only after base.UpdateTick, with player-placed/player-owned/untouched/nonempty guards. Composite other features are not intercepted. This is partial native-file evidence; no newly verified DLL provenance for that scratch extraction, so earlier exact-native qualification remains necessary.

## Census and shared progression

Census is server/main-thread only, compares complete authored prefabs.xml to native decorator list before publishing a denominator: exact name/origin/rotation/ground offset multiplicities, file size/mtime stability,200000 rows,4096 rooms, unique eligible identity, valid authored biome. Repeated road decoration allowed as multiset; unresolved/source mismatch/empty load never publishes100%. Slices128XMLnodes/64native entries and4ms, initial bootstrap<=100ms. Decoration events invalidate; no native sleeper rooms activated by census. A parse/identity failure freezes not-ready until invalidation/world lifecycle; fixing file without native change is not an automatic retry in this class.

Eligibility XML is literal exact exclusion list, case-insensitive trimmed matching, trader and rwgonly+streettile exclusion. Combat eligibility requires nonzero authored spawn maximum OR nonblank room minScript, independent of runtime readiness. Policy file invalidity withholds census rather than inventing denominator. No per-player entry/trading gate derived from progress.

ObjectiveProgress binds current store/world/census generation, subscribes publication changes, tracks exact changed identities, detects gaps and rebuilds. Snapshot scan completes before catch-up so deaths do not starve the initial scan. Bounded64work/4ms; detached immutable counter publication only after exact current snapshot catches up. State reset-only=undiscovered, cleared=2. Count mode1unit/POI; tier mode quarter-point weights4,8,14,22,32,44 then60+16*(tier-6), integer ceiling thresholds avoid float boundaries. Target uses sandbox10..90step5 when valid, otherwise authored default75. ObjectiveText is presentation, not reward authority.

Milestone evaluation requires matching map world/nonempty session and current objective revision/generation. Discovery six configured bands15/25/35/45/55/65 at tiers0..5 scale exactly against target75; saved numerator preserves fractional thresholds. Historical receipt key biome+permanentid intentionally survives resets and tuning, not re-earned under same id. Store strict version1..4 migration,<=1024 receipts/64 biomes, current world, ancestor/file reparse refusal, exclusive writer lease, exact predecessor hash, flushed CreateNew candidate, replace/move and reread before publication. Candidate recovery retains history and requires nonregression. Failed write withholds subsequent service publication until reopen; no receipt from arbitrary remote packet.

Automatic discovery consumes historical earned tiers and reveals authored eligible POI markers within15000m of any alive/on-foot/nonfly spawned current player. World-wide shared markers are intended; no terrain reveal/room activation. Sliced256examined/32markers/4ms,30s resweep, store pending blocks competing writer, reset-only envelopes promoted singly, failed publication retained. Maintenance console authenticates remote current connection and current player, one job at a time, earned-tier-only preview/replay range1..100000; only requester cancels remote job. Saves pending markers through store, counts retained uncertainty after lifecycle publication,60s stall timeout refreshed by actual progress; no receipt reward/feedback spam. Actual native console permissions and world-store transaction internals remain separate dependencies.

## Supply entitlement, owner input and native flight

SupplyService scans immutable clearance cumulative credits/lifetime kills in bounded64records/256contributors/4ms slices without restarting on each new publication. Stable player keys; maximum16384 contributors/overflow checks. Earned quota base75 +5 per prior drop capped+150, minus10 per equipped glove ModPurgeTier1..5 on base, minimum1. Equipment reads current server native equipped hands, highest matching tier, current authenticated online player; offline credits retained rather than redeemed at assumed price. Native equipment synchronization itself not requalified.

Accounts monotonic observed/spent/earned/delivered plus at most next-sequence inFlight. Delivery token SHA256(world|stableplayer|sequence) first16bytes. Bounded candidate store<=4MiB disk/1MiB XML, exclusive lease, strict schema/current native world/reparse guards, exact predecessor bytes/hash, nonregression, flushed candidate atomic publication and reread. Candidate recovery acknowledges only exact requested successor before native effects. One account mutation per pulse. No client supplied reward counts accepted.

Coordinator current authoritative world/main thread,5s cadence, round-robin<=16 tracked players, alive/spawned/non-spectator/on-foot/nonfly; skips active director flight/NoLoot. Reserves earned entitlement before original native SpawnAirDrop, accepts only true plus an active exact native flight, then records TryLaunched and notice. Delivered means accepted launch as accepted design; no crate disk-confirmation replacement proposed. Failed account publication retains awaitingAccount and blocks further launches; retry original token. Queued/active reservation supplies native flight path (<=100m authored radius), original first matching crate spawn scope, owner stable stamp and native spawnBy compatibility projection. Native director owns flight Tick/crate lifecycle. Prepared/inherited resumable flight behavior preserves legacy save fields; it does not force region/chunk reads. Crash between accepted native flight and account launch publication remains a cross-system durability limit of accepted flow, not authorization for a redesigned disk confirmation system.

Spawn scope ThreadStatic nested restore/finalizer, exact current controller/world/flight, first crate only; attaches stamp BEFORE registration and throws if custody attachment fails. Serialization weak-table entry binds entity reference/id/birth/world, native authority and source.77byte trailer exact magic/version/world/32byte stableowner/sequence/token, canonical deterministic token; unknown/malformed trailer cannot establish ownership. ToServer writer and FromClient reader excluded; only Persistency marks authoritative read. Authoritative access rechecks current server world, exact entity registration/birth/world every use. Native16bit entity envelope bound enforced before append. ReconcileLoadedCrate recognizes only stamped loaded crate and original account sequence, not forced-load evidence. Full native airdrop/entity read/write source was not freshly available in current extraction directory; dated native-flow traces reused with this explicit limit.

SupplyAccess server stable owner only, rebinds native runtime spawnBy for owner reconnect, disables sharing. Lock denied while exact objective world/revision not ready, preventing early fallback loot for owned crate. NetPackageBag hook requires current authenticated ClientInfo and native crate lock ownership; unstamped ordinary crates keep native access behavior. SupplyLoot selects first unfinished biome pine_forest/desert/snow/wasteland/burnt_forest from exact current progress; unavailable progress fallback pine_forest. Completed sweep burnt_forest is previously documented conformance distinction versus legacy unreachable biomeNum==5 behavior, not exact parity claim.

## Notifications, XML and limits

Clear notices subscribe live store transitions only, suppress duplicate restored cleared/reset-pending records, use actual epoch dead contributor identities; offline names from native persistent player list, live names override, sanitized80chars, paginated aggregate. Discovery/clear/supply/milestone packets ToClient only and bind original map world/session. NoticeDelivery current remote-world identity, handshake deferred<=64/notices60s; mismatched session drops. These are presentation custody, not item grants. Milestone queue primes journal topics as received, delayed8s tooltip,0.5s retry/sweep; saved reading restoration is separate from replaying live fanfare. Full InformationTopics/Service/journal and full map handshake remain pending.

Four policy XML files fully read: supply75/5/150/radius100; objective75; discovery six literal bands; eligibility full literal exclusion list. Loot XML only targeted Config/loot.xml includes _Purge/loot.xml then supply_modifiers_loot.xml, and _Purge/loot.xml:410–424 five exact airDropPurge_*8x4 containers referencing same-biome groupAirDropPurge_*. Full nested group outputs/probability/recipe/items definitions NOT read here. Quest/buff XML families not fully read. No XML full coverage inferred from include scan.

No new confirmed gameplay defect added. Not-ready/failed-load availability, notification loss during crash/queue expiration and accepted launch coupling retain explicit limits. No multi-peer/gameplay/save-fault experiment performed. Remaining native reset/evidence/manifest/POI store/map/scanner/information files require subsequent tranches; this is not all-Purge source closure.

## Full-read current manifest
| File | Lines | SHA256 |
|---|---:|---|
| Scripts/Purge/Objectives/RebirthPurgeDiscoveryPolicy.cs | 60 | 599180F3B71C51EEB204821B247B8BDC6346C4391E25B13563F966342C18AD6F |
| Scripts/Purge/Objectives/RebirthPurgeDiscoveryService.cs | 107 | C91B630422015D22A26A2FAB3775F5F8CF45F7F27FFFB7F386AF7D00F8D83E1F |
| Scripts/Purge/Objectives/RebirthPurgeMilestoneService.cs | 62 | B7A4847E0A85832C8AC6A315BA0F3EE57C6CAFCE3DA448C35278BA6A0EAC0474 |
| Scripts/Purge/Objectives/RebirthPurgeMilestoneStore.cs | 161 | 24E148F2F9EDB326CED8E220C6D9F34D73316081003997C5201E1A9D2AE1EEBF |
| Scripts/Purge/Objectives/RebirthPurgeObjectivePolicy.cs | 51 | C877F832E927EA948FF40E3B80422980E7F9477D797352139AF290D72E4A6722 |
| Scripts/Purge/Objectives/RebirthPurgeObjectiveProgress.cs | 152 | 7AD85E6670A67F63A2A63D4A2856525C4C4DB7C0778D64B18196B2E575B65A23 |
| Scripts/Purge/Objectives/RebirthPurgeObjectiveText.cs | 26 | 394B7FB6E117A1ABCC23770E42DBCE43F6A9A2CCB7594A9EEAC61A7AE81B8DAA |
| Scripts/Purge/Objectives/RebirthPurgePoiCensus.cs | 199 | 59FBA7297AF35A47B4A3968E6C65253A2C9207FED95C918851D1F9F00D190574 |
| Scripts/Purge/Objectives/RebirthPurgePoiEligibility.cs | 37 | ACAC708FA072317218EC06FC4C0E22D9D958066B0964B3F7D77B0A72A8911026 |
| Scripts/Purge/Objectives/RebirthPurgeProgression.cs | 28 | 49A13AFA35AF15AD76241A2BC7CB849676A9864455D47C12CD519931027AC162 |
| Scripts/Purge/Objectives/RebirthPurgeSupplyAccount.cs | 131 | E9A300A9564A49E497E2CD23576B488B96DF50300EFE9438B769864E55F83D94 |
| Scripts/Purge/Objectives/RebirthPurgeSupplyAccountStore.cs | 114 | F43E3A492AC9B08ED941400D415E362C56FBC06D7B1ED956E4F6F070451C0167 |
| Scripts/Purge/Objectives/RebirthPurgeSupplyCrateStamp.cs | 43 | 20D4B001B3A18A81880AC2C0780368D7A72F18FE34F606EDA270E758361BBEFB |
| Scripts/Purge/Objectives/RebirthPurgeSupplyPolicy.cs | 66 | E00243E27AB20EC674D46FE6A67768709FB0B0F769E785707DFDE5E0971C3AB7 |
| Scripts/Purge/Objectives/RebirthPurgeSupplyService.cs | 153 | 90B119DD12996591A2B53BF24CCAF22F64EBEC64482E25B74438D2ABC60E6CA7 |
| Scripts/Purge/Runtime/RebirthPurgeThemeModApi.cs | 198 | 2E9CEDA3168382E1D3655740B54B38BCE598B6BCA11838491E59D65EE635D830 |
| Scripts/Purge/Runtime/RebirthThemePolicy.cs | 8 | 706A5DDF70D7DF35471D670A526657935930D11AFBB0D44F211787DE0CD3799D |
| Scripts/Purge/Runtime/RebirthPurgeLootRefreshHook.cs | 11 | F1D4F6E2B739111811D2DBDF2263C5EA7B86AC872B46E208EC92A7035538A65D |
| Scripts/Purge/Runtime/RebirthPurgeReleasePolicy.cs | 15 | 4588BAA8B5F533DBDC21642E3ED0312AE4C176FE3AB52E34F70E480EB8F9A168 |
| Scripts/Purge/Runtime/RebirthPurgeDiscoveryMaintenance.cs | 210 | D3E4D1350BD4F80176212968DC8D8D7593641B7F86FE8B00B742230B5AF2FB63 |
| Scripts/Purge/Runtime/RebirthPurgeSupplyEquipment.cs | 56 | 0106339F3E048A7FCB552CEBCE407142B174D1F822AEA5374762664BBB0B9057 |
| Scripts/Purge/Runtime/RebirthPoiMapModApi.cs | 45 | 149C373269ED1BEC814AD8AE91EBDE3E9E61CE2E47BB72E469A7B05F631803CD |
| Scripts/Purge/Runtime/RebirthPoiWorldModApi.cs | 25 | 075DE11D5B2A6124E86FAB1FEC5EB003F052BA6B4395D8F25FD3B22AEED146B4 |
| Scripts/Purge/Native/RebirthPurgeSupplyCoordinator.cs | 154 | 0A61FE58AE9C09770361ADF6DD7968751B1C333439B2D93A682007D85BD41DEA |
| Scripts/Purge/Native/RebirthPurgeSupplyAccess.cs | 63 | 1FE139542D8481A02AE3453CD6544529DF8A6641832B9B5A1F8648A99DDC7F4C |
| Scripts/Purge/Native/RebirthPurgeSupplyLoot.cs | 39 | 941269C74DC3623620EA71A018BD6F788CD74157B7682172149D90CACC045259 |
| Scripts/Purge/Native/RebirthPurgeSupplySpawnScope.cs | 32 | 9A7088D8329FD427C50F43BB5B8E6E9D35B66E0B0393ABF42F36D9AAAD6091D2 |
| Scripts/Purge/Native/RebirthPurgeSupplyCrateSerialization.cs | 82 | C2134FB234AAE13A70B8F719C9755CCBF5B625E46B8722F50170EFC6F740EBDE |
| Scripts/Purge/Native/RebirthPurgeSupplyModApi.cs | 59 | 6E4D545F9B193FD1620180C7169F49012E70354E0F6D58AE6DBA52C870501CC0 |
| Scripts/Purge/Native/RebirthPurgeKillContributor.cs | 46 | 95685BA7A33385C0EEF0AF1C54223AA58D90C40ACA45DE2FF3C50A48D1DF059D |
| Scripts/Purge/Network/RebirthPurgeClearNotification.cs | 110 | A1244128EA2BC9758883975260FA64D55D6076B4C27F9B4394642C7108D8F6F6 |
| Scripts/Purge/Network/RebirthPurgeDiscoveryNotification.cs | 67 | 4E0FAFEDEA3875516F1F9EA32E1619928394DC4C1C677A1AFA787C170F732AB4 |
| Scripts/Purge/Network/RebirthPurgeMilestoneNotification.cs | 162 | E65BB154021EE8B5D7006C032F16FBAB9326DDFE7E46D0C77512F5CCE12BB4F4 |
| Scripts/Purge/Network/RebirthPurgeSupplyNotification.cs | 36 | 677DF83A712033152A16724B88AA21983CB8C0287E538AF21B289884472DC1D1 |
| Scripts/Purge/Network/RebirthPurgeNoticeDelivery.cs | 41 | EC23CFC25213CD86A5E7B69AE11503436F0A5E6E62FF7248D517EF12CF84FAEA |
| Config/_purge_supplies.xml | 4 | C60545C1F4E96F745567524E03B7EF69A137D30F82B84454A65CDEE603F45B8B |
| Config/_purge_discovery.xml | 13 | F1D155B1C228450ADBCE751D6F64A15227FDE1765BBE8F2B2A0D6F324A89625E |
| Config/_purge_objectives.xml | 3 | AC14AD2A43D245A0EC715ADA78E9471ADAFF78047E38C51004407B9EBCB0AB6C |
| Config/_purge_eligibility.xml | 117 | 5D2D383131DC82E4119B57C69651DFC5F06A29FF3A86928D700B511600938E53 |
