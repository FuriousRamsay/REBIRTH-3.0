# Project gap narrative reconciliation and small gameplay/diagnostic closure

2026-10-15 audit only. No production edits, build, tests, game, profiling, endpoints or commands executed. Twelve genuine pending sources fully read, 1,342 lines, plus scoped native/XML/caller dependencies qualified below. Main ledger/report unchanged. Requested owned reconciliation CSV updated; earlier snapshot trace retains its historical counts.

## Narrative reconciliation

Reviewed144 no-recognized rows and15 scope-reference rows against existing narrative/ledger evidence before selecting files. Nineteen rows with current hashes and explicit ledger wording SOURCE READ **in full** or SOURCE READ **complete** were missed by the first narrow phrase matcher; normalized to LEDGER_FULL_READ. This repairs evidence classification, not new source reading. Parent newly appended seven literal full energy helper bodies/current hashes in NATIVE_TRANSPORT_TRACE:729 onward; those seven moved from hash-reference to TRACE_FULL_READ. Five older prose-reference rows remain independently qualified rather than falsely called untouched. These are AdvancedFarming console, Fire Debug command, Healing ItemActionUseMedRebirth, UI CraftingWindowLookup and QuestBackpackActionContext. Parent will reconcile their exact current-source proof; this tranche does not reread them or treat narrative mention as fresh full credit.

After the twelve new full reads the current CSV snapshot remains1,805 source rows: canonical1011, trace497, ledger130, new literal20 (prior8+new12), semantic-only17, no-recognized116, older-reference5 and changed-hash9. Unrecognized does not prove never read: some parent narrative descriptions use abbreviated names rather than full filenames. Inventory selection remains distinct from actual source review. No pending/counter status certifies runtime behavior.

Changed9 were promptly sent to parent: Energy/RebirthCamperEnergyAccess, EnergyDeviceAdapter, EnergyDrivingInput, EnergyRuntime, EnergySnapshotWire, RebirthCamperWorldLighting, XUiC_RebirthCamperEnergy; Camper/RebirthCamperStationAccess and StationNativeActorPayment. CSV records initial ledger versus current hashes. No edits or duplicate parent source reads. Later external changes require another hash check; this snapshot is not a filesystem lock.

## Production vehicle respawn route

Seven Respawn files read entirely. Bootstrap RebirthFresh_Init:101 explicitly installs vehicle downgrade prefix. Respawn ModApi registers GameStartDone binding after definitions load. SandboxOptionManager:851 applies per-save VehicleBlockRespawnDays, normalized to0/3/7/14/21; setting invokes shared definition projection. Config/blocks.xml:17 includes _Vehicles/blocks.xml, whose :183–205 declares invisible BlockRespawnRebirth marker with Next=carsRandomHelper. These are targeted dependency excerpts, not full bootstrap/options/XML credit.

RuntimeRegistry binds concrete helper outputs, captures per-block-object baseline and restores/rebinds on option changes. Definition identity and last written **type** determine ownership, not every BlockValue bit; no concrete same-type metadata conflict was established. Shared definition projection executes on each initialized process; native server world tick/save remains authority for physical markers. Registry state is process-static, with same-object/type checks for definition reload; no explicit lifecycle clear was found. ModApi sets initialized before event registration (known partial-install family); patch installer sets installed only after PatchClassOnce returns. Do not assume either implies current DLL patch success.

Enabled composite vehicle destruction prefixes only downgrade chains reaching carRespawner_FR, bounded16 steps; returns Downgrade and suppresses original composite feature result. Other composite loot containers retain original. Marker ignores only recursive damage; direct damage still uses base. Enabled tick calls base native plant growth then on server ensures newly spawned definition remains repeatable. Disabled added marker invalidates scheduled native update then removes itself server-side; disabled tick removes itself server-side. The enabled base tick relies on native BlockPlantGrowing/WorldBlockTicker authority, scheduling, placeholder resolution and chunk persistence. Those full native bodies were not newly read here, so exact remote tick admission and interruption/save guarantees remain native dependencies, not inferred from comments. No custom respawn packet or persistence store is authored by these seven sources.

GetTickRate returns days*60*20*60; Debug ForceOneTick overrides. Existing scheduled marker due times are not explicitly rewritten by option setter: do not assert immediate rescheduling of already persisted timers without native evidence. Off cleanup is explicit in tick/add hooks. This tranche did not create a marker, change an option or execute a growth tick.

## Debug respawn commands actually mutate

ConsoleCmdRebirthVehicleRespawn is #if DEBUG and Debug-directory omitted Release. It does not override IsExecuteOnClient: native console default routing/permission applies. info reads state, scan synchronously scans bounded radius1–64 and vertical13, resolving RemoteClientInfo entity or primary player. validate is **not pure**: it calls BindCarsRandomHelperTargets and changes global definitions before generating its report. fast changes static ForceOneTick; no lifecycle reset assignment was found by `rg -uuu`, so it can persist across worlds in the same process. due refuses remote world and adds native scheduled updates for matching nearby markers. This is explicit operator test mutation, not report-only validation or a demonstrated unauthorized network path. Maximum scan volume129*129*13=216,333 block reads; no measured performance result. No Debug command executed.

## Powered-light shadow diagnostics

Three Debug sources fully read. CampfireFx command file is an empty cleanup placeholder, not a current callable command. PoweredLightShadows command explicitly changes diagnostic state and prints scene counts. Its Harmony FrameUpdate postfix exists only while suppression requested; it enumerates scene LightLOD and matches BlockPoweredLight, mutates Unity Light shadows/strength only, not block-light/power/save fields. Native console client execution is explicit. Other source callers include AdvancedFarming console and AutoBench (the latter remains pending full body).

Confirmed diagnostic qualification: EnsurePatchInstalled catches and reports failure, but SetDisabled(true) nevertheless sets DisablePoweredLightShadows=true, performs immediate suppression and returns text saying disabled. Current scene suppression can happen without continued per-frame patch success; status separately exposes patchInstalled. This is an attempt/status discrepancy, not a new gameplay authority finding. Restore uses LightLOD.shadowStateMaster/shadowStrengthMaster, not exact pre-tool per-instance values. It therefore cannot promise preservation of another tool's temporary rendered-shadow state. Flag and patch have no world reset caller found under `rg -uuu`; explicit diagnostic state can cross worlds. Source restore catches no Unpatch exception; a thrown removal can skip immediate restore. These conditional failure paths were not injected.

Release facade was checked as a supporting/reused dependency: #if !DEBUG returns Debug-required/no-op and no Harmony attribute. All selected Debug files are physically omitted by csproj non-Debug rule. No rendering or profiling was performed.

## Trader commands and placement policy

Debug TraderVoices command is client-executed and has real side effects: log flag, personal preset Save and local test sound. It also performs native Harmony patch-info reflection and reports file existence. Targeted Preferences.Set :17–22 clamps to valid index or0, writes SdPlayerPrefs and Save; this is personal presentation configuration, not synchronized sandbox policy or player-item custody. No complete preference/controller file credit is granted from the output excerpt. It is Debug-directory omitted Release although source lacks its own DEBUG guard. No audio played or preferences written.

TraderPlacementExceptions is production bootstrap-installed :115. Prefix only changes native traderAllowed input when caller supplied false, world/player data exists, protection option is enabled, target is in trader area and current entity held item name matches an explicit compatibility allowlist (two substring families and exact names). Native original still executes. Exact existing World.cs CanPlaceBlockAt method read :3582–3625: traderAllowed bypasses trader predicate, while player bounds and land-claim check remain. This method does not itself perform every collision/inventory/placement check implied by the source comment; broader native placement caller chain remains a dependency. Held item identity is not a requested BlockValue parameter here, so binding of request item versus current held item must be verified at actual native caller before any exploit claim. No custom sender packet or trusted client bypass was established.

Native scope: only the above World.CanPlaceBlockAt method, not full World type. Existing extract World.cs SHA2562A9CADCCAB502C7FAFBBC7459EDA090F84C230025A1F08C7B77356CC175905F1; exact native assembly provenance remains FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705 in prior traces. No inspector/build used.

## Remaining work

CSV retains116 no-recognized sources, mostly parent Camper work and small pending registries/diagnostics. Remaining large FrameSpikeProfiler1186, LightFxAutoBench560 and runtime/report registry bodies require actual reads if no narrative owner qualifies them. Changed hashes are parent inspection scope. No new live multiplayer defect promoted without native/caller proof; confirmed effects here principally classify diagnostic test safety and production installer reachability. No runtime PASS or performance acceptance claimed.

## Literal full-read manifest

| Source | Lines | SHA-256 |
|---|---:|---|
| Scripts/Lighting/Debug/ConsoleCmdRebirthCampfireFxDiagnosticsFresh.cs | 6 | 35503303CA344CA2693D4A0A37475250F24A1CF047375699D07AB7A58EA67302 |
| Scripts/Lighting/Debug/ConsoleCmdRebirthPoweredLightShadowsFresh.cs | 54 | 19DB7BD71827F111D5C39EFACAB9650FF7608CEA30F89353B29826D721F3A610 |
| Scripts/Lighting/Debug/Harmony_RebirthPoweredLightShadowDiagnosticsPatch.cs | 218 | 91F6233E335B652037180CCB1DEE38B8C1F744B8E32BF07F941EA8DC97FBEDD2 |
| Scripts/TraderProtection/RebirthTraderPlacementExceptions.cs | 97 | E6432DFDEA060632CB5075A60276F659DDBAA32B59CA5B848498B01B53A47A7A |
| Scripts/TraderVoices/Debug/ConsoleCmdRebirthTraderVoices.cs | 48 | 560C60C18DD3F435CFA9EE705D9FEA43F9CABE825EE5D53292B636DB52DFA7B2 |
| Scripts/Vehicles/Respawn/BlockRespawnRebirth.cs | 178 | 0B0DD17AADB43DBC3AB671FDDD2ADC2A3A2DC1DFC5F651ECB178FF974B55A0B1 |
| Scripts/Vehicles/Respawn/Debug/ConsoleCmdRebirthVehicleRespawn.cs | 388 | 5EBD70AFB4FAB1F1A40E8F5B3F94CD37CA78EA1FB5723EC8B9510DA0175E8DD1 |
| Scripts/Vehicles/Respawn/Patching/RebirthVehicleLootContainerDowngradePatch.cs | 76 | 7324043C355FE05B722F726B8A8A23AD992252C4E519971AC343B7884C975005 |
| Scripts/Vehicles/Respawn/Patching/RebirthVehicleRespawnPatchInstaller.cs | 29 | D0005E409979536F256E738E16430FE8916385C4B95CD1E457528F197EB7355A |
| Scripts/Vehicles/Respawn/RebirthVehicleRespawnModApi.cs | 36 | 35C0F009BBB995603F3A6F7A58FF6A6E65E0A481D315C19A34D6F6AC15717E2D |
| Scripts/Vehicles/Respawn/RebirthVehicleRespawnRuntimePolicy.cs | 29 | 7B7CAA4DFE0F9BC035E339CE79ACEBF136AEB0975D28AB685AB42DD7A042E734 |
| Scripts/Vehicles/Respawn/RebirthVehicleRespawnRuntimeRegistry.cs | 183 | ED36EEA718B049E0E94C1BC2825A15A863A2C19F0EFFB9CF4338613574EC05A4 |
