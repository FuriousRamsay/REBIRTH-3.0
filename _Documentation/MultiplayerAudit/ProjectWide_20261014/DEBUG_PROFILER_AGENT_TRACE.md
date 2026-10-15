# Debug scene profiler and dry-run planner

2026-10-15 static audit, current shared cumulative workspace; target operating REBIRTH3.0/b259. Audit-only; no command/profiler execution, production edits, build, tests, game, git or shared ledger/report changes. Prior FARMING_TRACE.md sampling reset/facade evidence and COMMANDS_REMAINDER_AGENT_TRACE.md wrapper evidence reused. Performance is secondary to multiplayer; this documents existing diagnostics without implementing or enabling any work.

## Full-read scope and caller authority

SceneContentProfilerFresh1171 fully read (command plus runner). PerformanceDryRunPlanner Debug and Release facade fully read. AdvancedFarmingPerfSnapshotService1645 only selective declaration/DTO first240 plus SetSampling/ResetSampling274–423; no whole-file credit. Native-light-hook first148 and farming ModApi reset90–120 selectively read, not full credit. Existing exact-native ConsoleCmdAbstract/console dispatch evidence reused, not new runtime verification.

Scene command rbsceneperf/rbfarmcontent/rbscenefarm is #if DEBUG under Scripts/Debug physically omitted from Release. IsExecuteOnClient=true/defaultpermission0 inherited; native console authorization applies. Remote client's local invocation profiles its client scene, listen-host invocation profiles shared host process; neither represents standalone dedicated performance. Command center chooses remote sender entity if present, otherwise first world player before primary local player, with zero fallback. This may select another player on a listen server; it is census targeting, not ownership or mutation authority. No server packet/quest/inventory grant authored.

## Confirmed sampling interference

Runner captures prior bool SamplingEnabled after census, then calls SetSampling(true). Exact service implementation always ResetSampling when enabled, clearing all shared counters/name buckets before setting true. Thus starting scene capture while another sampler is enabled destroys its accumulated samples. RestoreSamplingState only restores bool by SetSampling(previous); if previous=true it resets counters again. Existing data is not restored, despite stop text saying sampling state restored. Normal end invokes Restore then Destroy -> OnDestroy Restore again; stop invokes AbortAndRestore then Destroy -> OnDestroy again. This is a concrete diagnostic data-preservation/interference defect; it does not debit gameplay resources.

Replacement starts call StopRunner(false), schedule Destroy of old GameObject, create/configure new runner immediately. Both old OnDestroy and new run alter same sampling bool without run-generation/lease ownership. With Unity deferred destruction, old prior=false restore can disable new run after it enabled sampling. Timing ordering not executed/decompiled here; source competing restoration is confirmed, precise frame interleaving remains runtime qualification. An unrelated manual sampling toggle during a run is overwritten by final old-bool restore as well. Other farming frame/stutter profiler and console commands use same SetSampling globally (rg -uuu), so exclusivity is not guaranteed by scene command's own singleton.

## Confirmed cross-world diagnostic lifetime gap

Runner GameObject DontDestroyOnLoad; RunProfile retains no captured-world check after initial census and loop has no GameManager.World/current-save/generation test. It samples static counters each unscaled frame until duration or explicit abort. Final census resolves current World afresh using old coordinates; it can therefore combine worldA start and worldB final/state counters if a world changes mid-run. Farming ModApi reset clears shared sampling counters and stops stutter trace, but rg -uuu SceneContentProfilerRunner references are entirely own file; no lifecycle retirement handler exists. Cached block-type classification dictionary is static and has no world/reset invalidation here. These are report provenance/isolation defects conditional on changing worlds; no wrong-world gameplay write is authored.

RunProfile is an iterator without try/finally around census/sample/report loop. Exceptions in census or capture/logging can prevent normal Restore/FinishAndDestroy; OnDestroy restores only if object later destroyed, and no automatic catch destroys it after coroutine exception. Start census fails before prior sampling bool captured, so later stop defaults restore false rather than original enabled state. Exact error occurrence not injected; conditional failure cleanup gap, not reproduced hang. Stop catches AbortAndRestore exceptions then destroys object and reports restored even if restore threw; diagnostic status is not a verified restore receipt.

## Workload and native-hook separation

Radius4–128, vertical4–64, duration30–900s, interval5–60s, finite spike5–250/severe up to500ms, warmup0–60 clamped below duration. Census synchronous volume uses square horizontal side257 at maximum, up to129 vertical ->8,520,321 block reads, plus candidate TE lookups and whole-scene FindObjectsByType renderers/particles/lights/LightLOD then radius filter. Default radius64/vertical24 ->815,409 block reads. Start and end census both run, despite help saying one-time census; per-frame sampling avoids repeat census during loop. Operator-run census can stall host main thread; no test command executed here. CPU/GPU usage not measured.

No GC.Collect forced; GC.GetTotalMemory(false)/CollectionCount and Time.unscaledDeltaTime observed each frame. Spike array32 retained, top12 logged; counter deltas and report allocations affect measured process. Reports correlate frame/farming/GC counters but do not isolate cause or prove multiplayer performance parity. Initial census outside elapsed frame loop excludes its own scan cost from frame sample totals. Public BuildSceneCensus lacks own radius validation but only current source callers use command clamps; rg -uuu found no external caller.

Native light hook sample flag only gates timestamps/record calls around already-authored AdvancedFarming light behavior; scene profiler does not install/alter Harmony or BypassBlockLightPatch. Existing farming installer/native light semantics remain FARMING_TRACE.md dependencies. Do not infer profiling enables or disables actual farming/light gameplay. Toolbelt notifications resolve current primary local player and swallow display failures, not server messages or gameplay commands. No export file generated by this profiler; log asks user to send existing client log zip but does not send/create that zip itself.

## Dry-run planner full closure

Debug planner full source builds StringBuilder reports from module registry and shadow snapshots. Baseline/sweep are plans; BuildResult maps toggle safety enum into proposed future action, appends default/manual-review/patch-overhead cautions. It never executes selected action, toggles shadow state, patches Harmony, measures FPS, writes file or sends packet. Four sweep passes classify modules by safety; report is planning metadata rather than runtime proof. Release facade #if !DEBUG returns Debug-required messages for all APIs. Callers Sys and rbperffresh were previously fully read; no duplicate wrapper credit.

## Remaining scope and maintenance

Whole AdvancedFarmingPerfSnapshotService1645 still a full-file obligation unless parent has separately finished it; only selective body evidence here. Top-level Scripts/Debug report wrappers remain pending explicit source coverage, except prior named traces. Correct existing diagnostic state isolation/cross-world admission before treating scene captures as independent valid evidence, preserving normal source behavior; no unsolicited profiler redesign or implementation is performed in audit. Native Unity coroutine exception/deferred Destroy timing requires exact runtime qualification before asserting deterministic replacement sequence.

## Full-read manifest
- FULL Scripts/Debug/Commands/ConsoleCmdRebirthSceneContentProfilerFresh.cs SHA256 98105E2618264C0A5F2429A637D5419F1DFA222D0BFF5F7B0AF62FCCC26D6425 (1171 lines; full read).
- FULL Scripts/Performance/Debug/RebirthPerformanceDryRunPlanner.cs SHA256 DE15804A9935AF3FF801BCF83E300860F3D8C71997111C219E688A314BCE5D06 (257 lines; full read).
- FULL Scripts/Performance/RebirthPerformanceDryRunPlanner.Release.cs SHA256 57D5F5BB51E91855AED3B45F5732F770E360A0A50D48C19F7CC65B8C8D99C4ED (49 lines; full read).

REBIRTH FEATURE COMPLETION CONFIRMATION: assigned static profiler tranche COMPLETE. Directives/evidence/architecture/documentation PASS; code/XML/UI/localization/assets/persistence/network modifications N/A; packaging N/A. Compilation Not performed; in-game validation Not performed. No performance acceptance or fixes claimed.
