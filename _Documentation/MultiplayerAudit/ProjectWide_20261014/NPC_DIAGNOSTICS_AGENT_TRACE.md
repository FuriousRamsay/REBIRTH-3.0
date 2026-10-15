# NPC diagnostics/qualification safety — 2026-10-15

Audit-only; no commands executed, production/build/game/tests/Git/shared ledger/report changes. Full nine current sources and exact native ConsoleCmdAbstract below. Negative caller searches -uuu; DEBUG/source build exclusion explicit. Prior admin/recovery/benchmark findings retained.

## Confirmed qualification writes to real progression stores

ProgressionQualification.RunSelfTest allocates random stable NPC/award and calls actual Award4000 twice, tests duplicate/query then returns without deleting record/replay. Acip02.Run allocates random NPC and grants real constitution/strength/dexterity XP plus rifle use. Acip03.Run allocates random NPC and commits actual profession outcomes across every profession. AdvancedQualification.Run uses fixed mentor(A401,B401), trainee(A402,B402), worker(A403,B403), grants real Farming XP, creates mentorship/credits, invokes25 accepted completion events and certification/specialization machinery. None captures/restores stores or cleans test records. Actual Award marks progression dirty, bumps projection and publishes. These are global live stores subsequently serialized by coordinator, not disposable test doubles.

Production caller AdminSupport431/440/446 exposes progressionqualify/professionqualify/advancedprogressionqualify under default Inspect classification. They are not DEBUG files and have no diagnostics-enabled/disposable-save requirement. Custom Inspect allows RemoteClientInfo nonnull unlike Repair; exact native ConsoleCmdAbstract default permission0 is outer console authorization, so this is not ordinary unauthenticated client access. A console-authorized remote operator can invoke mutating qualification without the custom Repair restriction. Repeated advanced check grows fixed test identities' XP/completion counters and can change certification results; random checks leave orphan durable progression rows. No live player's/NPC's ordinary stable ID collision established, and no real command was run. Source-confirmed diagnostic pollution/permission classification defect.

Separate DEBUG ConsoleCmdRebirthNpc IsExecuteOnClient=true exposes progression selftest without Diagnostics.Enabled guard despite help claiming read-only except debug toggle. On client Award authority rejects, on host/server it writes real state. Qualification query label durableQuery only inspects current in-memory view, not saved bytes/reload. Do not count PASS as persistence test. Current debug scalebenchmark destructive global reset is already NPC_CORE_PERFORMANCE trace; this full command read confirms misleading global read-only help.

## Command/topology and native permissions

ConsoleCmdRebirthNpc513 is enclosed #if DEBUG and Debug directory excluded from Release csproj39–41. It runs locally on clients by design. Most branches report snapshots, some initialize services/load stores; productionresources/dutymedical execute structural audit, adapters refresh declarations, interact opens actual local UI. diagnostics Enabled gates selected reports/scale but not progression selftest. SenderInfo is unused; native console routing/permissions remain outer authority. Client-local benchmark can clear its own client utility caches; do not automatically claim a remote invocation runs on server, because IsExecuteOnClient=true.

ConsoleCmdRebirthDog185 sits Debug folder (Release excluded) and IsExecuteOnClient=false, defaults native permission0. Ownership can select arbitrary playerEntityId for inspection. Reconcile explicitly schedules ghost repair/chunk observer/owner projection and coordinated SaveAll, so it is mutating despite diagnostics description. It can select an explicit player's identity but no custom local-server-only Repair rule. recoverkillall/forgetmissing delegate service guards; forgetmissing requires current persistent owner, Active dog/no live projection and removes aggregate+pending identity then saves. This is intentional administrative rollback cleanup, not harmless report. recoverkillall requires explicit old removal reason/cap; full lifecycle/native custody remains parent-owned. Movement/target/combat start bounded runtime trace recording, not combat execution by this command file. No new native permission bypass established merely from absence of custom policy.

Exact installed Assembly-CSharp default ConsoleCmdAbstract permission0 and IsExecuteOnClientfalse were read via existing read-only inspector (no build) in unique shared temp; native dispatch permission-list overrides not decompiled here. Thus user admin policy may override runtime required permission; zero is declared baseline, not proof every player can invoke it. Existing consolidated admin local Repair policy is stronger than this native-only DEBUG command contract.

## Report-only qualification semantics

LifecycleQualification tracks activation/stable maps, duplicate identities/count mismatches and64findings; snapshots clone findings. It audits active/runtime counts, not complete item/save/network/native actor state. Reset clears its own observation maps/counters and sets Qualified, so a clean self-reset does not prove every external world-scoped service cleared. 'proves world shutdown clears every observation owned by this service' is narrow self-observation. Transient startup ordering can retain Degraded finding until world reset. No gameplay mutation from these observer methods.

BuildQualification declares StructuralQualified from nonblocking compatibility, exactb259=false, verified native0/missing7, source baseline3.1b14/not-reflected. Reports explicitly runtimeAcceptanceNotRun and source structural evidence; these static strings do not establish current target native compatibility. Its ConsoleCmd build/audit only refreshes counters/report. Api compatibility strings/zero reflection are prior trace.

ProductionQualification captures bounded256samples of actual service count/report strings, memory and local audit duration; Reset clears samples but retains counters. ReleaseQualification checks registration string names, social cap, nonempty trade registry/direct activation candidates and <=50ms local audit; outputs structuralQualification and runtimeAcceptanceNotRun. It does not test owner inventory publication, packet delivery/reconnect/native action or save failures. There is no all-purpose gameplay proof in PASS. Snapshot objects can be caller-mutated, diagnostic only; no authority token created.

## Evidence limits

New confirmed finding is real-store progression qualification pollution and Inspect classification, scoped to console-authorized operator. No commands/tests run. Release/debug exclusion and authority checks must accompany any issue report; don't conflate remote owning client with server console execution. Parent still owns full dog lifecycle/removal custody and native console dispatch.

## Full-read manifest
- `Scripts/NPC/Debug/ConsoleCmdRebirthNpc.cs` — 513 lines; SHA256 `A72669ED775EC77F162E791B494C27518AE09D5C502A1134F44B48D5426071C9` (full read).
- `Scripts/NPC/Debug/ConsoleCmdRebirthDog.cs` — 185 lines; SHA256 `7F37C64EC19EA94EB09BF1B59066F793396FC191917E422718ACA69EA3CDF7F5` (full read).
- `Scripts/NPC/Progression/RebirthNpcProgressionQualification.cs` — 17 lines; SHA256 `9745B816524A462BAC7D5F0CE7152E6CF9C31ECB37559959BD7CE301911C9195` (full read).
- `Scripts/NPC/Progression/RebirthNpcProgressionAcip02Qualification.cs` — 20 lines; SHA256 `EB8F5D03D8958CCCB66B4203390D3512AC81CD385043ECEE56B1D83E24A79CD4` (full read).
- `Scripts/NPC/Progression/RebirthNpcProgressionAcip03Qualification.cs` — 27 lines; SHA256 `C06DC04A821817A8649794718E11D803E0FA6E5EFA8CB4EE731E6E87B767FE7B` (full read).
- `Scripts/NPC/Progression/RebirthNpcAdvancedProgressionQualification.cs` — 36 lines; SHA256 `42D3BB7E2B5F5E27716D658347C9DE0464C9FF5E6B6D75A5C440E2AFA4EA8015` (full read).
- `Scripts/NPC/Foundation/RebirthNpcLifecycleQualification.cs` — 299 lines; SHA256 `23577EFE7D04EF33802477C8E06C9AB80A5F2603F0531686A17A45E9A0FAF0FB` (full read).
- `Scripts/NPC/Compatibility/RebirthNpcBuildQualification.cs` — 159 lines; SHA256 `D59CB99610B825EE83F48AF88686516B45938FC4CBD1ABD129CE608C4F551FF2` (full read).
- `Scripts/NPC/Conformance/RebirthNpcProductionQualification.cs` — 78 lines; SHA256 `A2B6CD60A7D5C432728EF345CB1B2F74F770019AE989DD15320B6B4AF4C6E998` (full read).
- `E:/_Haven/shared/REBIRTHTemp/NpcDiagnosticsNative20261015/ConsoleCmdAbstract.cs` — 62 lines; SHA256 `5A69A4D4FB31FCBD1DF99DDCD866E96E77D21FE036BB0CF59145A5F846D82C45` (full read).
