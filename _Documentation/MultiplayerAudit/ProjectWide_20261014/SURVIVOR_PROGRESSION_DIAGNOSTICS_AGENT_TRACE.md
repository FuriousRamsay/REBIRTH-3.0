# Survivor progression diagnostic source closure — 2026-10-15

Audit only: no production edits, builds, tests, gameplay or Git. Seventeen complete literal C# bodies are recorded below. Called runtime service implementations are not new full-read coverage. Existing Debug callers/native console baseline from SURVIVOR_RUNTIME_REMAINDER_AGENT_TRACE are reused.

ConsoleCmdRebirthSkill is preserved, client-executed, rejects main menu and chooses the primary local player. Host reads WorldCharacterService progression; remote reads the owner projection without additional session/creation validation here. It never awards progression or mutates inventory/network/save state. Its only mutation stores detached diagnostic Marks in a private static dictionary keyed solely by entity ID. Complete body and rg -uuu find no reset path. A retained mark across world/profile changes with reused entity ID can yield a misleading diff. This is diagnostic provenance, not gameplay corruption; marks do not survive process restart.

All sixteen vector helpers expose Run/RunAll, with no install/Harmony hooks in their bodies. Existing Debug console and acceptance aggregators call crafting/combat/wave/resource/service suites. Release excludes Scripts/**/Debug/**; these helpers and rbskill are outside that folder and have no conditional guards. Release inclusion does not activate excluded Debug callers.

Backpack checks constants, eight profile bonuses and Scavenger bonus without expanding native inventory. Drone/leather check sample name classification. Generic/medical/technical/runtime closure check target predicates and selected skill/treatment classification without executing healing, awards or saved effects. Unsafe guardrails only read four flags (turret damage enabled, feed reliability disabled, metal grade enabled, chemistry payload disabled).

Combat checks damage split/cap math, decay and configured drone/explosive guards. Wave A checks signed curves, commerce caps, locomotion normalization/constants, partial armor burden recovery and cadence/award guards. Service checks signed craft/repair/medicine endpoints, infection budget transfer, recipe classification and award caps. Resource/TrackingHabitat check curves, tiers and configuration bounds. Resource delegates Phase8 RunVectors; its implementation is not newly covered and its purity is not inferred here.

PROG-DIAG-A: CookingQuality tests literal threshold 40, fixed 10-8 and 14-12 arithmetic, and two unconditional true arguments. Weatherproofing tests literal 0.70/1.10 arithmetic and literal clamp. Both can PASS despite broken runtime content or transfer. In particular they do not exercise the already recorded remote outerwear_lining failure. They provide no multiplayer/native inventory/receipt/save acceptance evidence.

CraftingProgression checks fixed registry counts 656/504/134/18, live504/planned0, selected rule/knowledge examples and all owned gated rules' live capability requirements. No runtime execution/count validation occurred; no stale-count defect claimed. Prior registry/content evidence remains separate. No suite proves save/reconnect/retry. Static pass/fail counters in some helpers lack concurrency protection, but inspected entry points are synchronous diagnostics; no reachable gameplay race claimed.

## FULL literal source manifest

Each row was completely read as source text. Hashes below fingerprint those bodies after reads, not hash-only coverage.

```csv
"file","sha256","coverageMethod","lines"
"Scripts/Survivor/Progression/RebirthBackpackExpansionVectorHarness.cs","D3D0CC6A52CA230A919CF2B1CC2051328F6E71F411AEDAB14B6115896EBBC39D","FULL literal source body read; no execution","32"
"Scripts/Survivor/Progression/RebirthCombatProgressionVectorHarness.cs","62CDC4DFD3897AB4A5593EF52041707BC40E95C466DC498EAEBAC9E94B271352","FULL literal source body read; no execution","40"
"Scripts/Survivor/Progression/RebirthCookingQualityVectorHarness.cs","1574FF3E98B9CC8D3017F9B3799F91095666C8681989D417B0A2C86A4C5EDDFF","FULL literal source body read; no execution","27"
"Scripts/Survivor/Progression/RebirthDroneFieldServiceVectorHarness.cs","C0B81942F4D2F0A096AD615F789DE573BF0421143C86044A05242CBBE611CBE8","FULL literal source body read; no execution","29"
"Scripts/Survivor/Progression/RebirthGenericTraitContractVectorHarness.cs","9406A5D4C125468D220B335EB980C37B7E56A73F9619D19E42236035BBE9E923","FULL literal source body read; no execution","37"
"Scripts/Survivor/Progression/RebirthMedicalCombatTraitVectorHarness.cs","409DA916A82E326EAF8542DD68806FCFA5F33E176FEBC36BF27634627AA30975","FULL literal source body read; no execution","39"
"Scripts/Survivor/Progression/RebirthResourceFieldSkillVectorHarness.cs","426AB5DFF89AD13BE8892B03CE1550B04C4415CC9FB8D6B0D7E6A0D35C750FDF","FULL literal source body read; no execution","68"
"Scripts/Survivor/Progression/RebirthServiceCraftSkillVectorHarness.cs","E07C1F992833CEC7DE04B15857BB987C01BFD8B16A2CB801F8A1567537D516E4","FULL literal source body read; no execution","56"
"Scripts/Survivor/Progression/RebirthSkillWaveAVectorHarness.cs","E9931DB895A44BE3884FF30F4CB2DF8E3D928ECD036C646352CA5A1A1F53A225","FULL literal source body read; no execution","119"
"Scripts/Survivor/Progression/RebirthTailoringLeatherRepairVectorHarness.cs","49A8523B2202DD08A50C5EC2C21059E676C244C56210BBE8D364669343475DCA","FULL literal source body read; no execution","35"
"Scripts/Survivor/Progression/RebirthTailoringWeatherproofingVectorHarness.cs","D238B920CFBAB387904FB9474C0877D06669F0C2647A39A4CC79AFF9D4200124","FULL literal source body read; no execution","27"
"Scripts/Survivor/Progression/RebirthTechnicalTraitPerformanceVectorHarness.cs","B39649565BFF6D4D42AB728C50470EC41625B15E270B2A3743D5CB37DD454993","FULL literal source body read; no execution","33"
"Scripts/Survivor/Progression/RebirthTrackingHabitatVectorHarness.cs","7432B2DE62DC748F2649DF80ED2A0DA091B96D9C87913B728DFA366865B22B4C","FULL literal source body read; no execution","42"
"Scripts/Survivor/Progression/RebirthTraitRuntimeClosureVectorHarness.cs","DC8000F5E3CA0A9A421D6277D653182D7530F88C90E7D99C76ADB190388D70E8","FULL literal source body read; no execution","37"
"Scripts/Survivor/Progression/RebirthUnsafeProgressionGuardrailVectorHarness.cs","6133BC541DC41C65CF28B91EE8288C7EA861F33687A7D594970B79E326CDD121","FULL literal source body read; no execution","29"
"Scripts/Survivor/Progression/ConsoleCmdRebirthSkill.cs","76C1CFC080FB6343A44FE98CA9394299DD06F1A9BB715619DE42FB26E4952156","FULL literal source body read; no execution","208"
"Scripts/Survivor/CraftingProgression/RebirthCraftingProgressionVectorHarness.cs","88D709BD1E0CA9972C533C529817B711FED5DBCE123E79AC862E36F36FEAFC2F","FULL literal source body read; no execution","113"
```
