# Final crafting UI helper audit — 2026-10-15

Audit-only. No production changes, build, tests, gameplay, Git, or shared report/ledger edits. Operating directives and existing crafting, cooking, recycling, personal, remote and native-transport traces reviewed. Exact installed native evidence reused from those traces: 3.2b10 Assembly-CSharp SHA256 FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705. No new native extraction or runtime qualification claimed.

## Full literal reads and current SHA256

All six C# files below read completely, including rereading the two matching prior CRAFTING_REMAINDER entries; policy XML read completely (initial combined output truncation resolved by separate full read). This is actual content coverage, not normalized or traversal coverage.

| File | Lines | SHA256 |
|---|---:|---|
| Scripts/Crafting/UI/RebirthImprovisationRecovery.cs | 70 | A82403CB9B3B887275213E3B9E181CB795A489A8AB36B0B1B6B4D80307D412A2 |
| Scripts/Crafting/UI/RebirthRecipeDiscoveryPolicy.cs | 44 | 203E0A29C9BD75FA16BE0986E8F01141FB4CCF08D4B07C0204655502B7A57FA6 |
| Scripts/Crafting/UI/RebirthRecipeDiscoveryRules.cs | 48 | ED0A060881349F7A4B638779971B3BC074CDE590B15A00172BB954EDDA6FB7DA |
| Scripts/Crafting/UI/RebirthCraftOutcomeService.cs | 164 | A195497D4F31B007C38837009EFE68CD35BAF54A49B834553928CB36A963C4AD |
| Scripts/Crafting/UI/XUiC_RebirthRecipeSmoothScroll.cs | 109 | FCE434583C76408EBEB513CDFAFD49B0316480E8609634B49C4026776CEC8A07 |
| Scripts/Crafting/UI/XUiC_RebirthCraftingPresentation.cs | 266 | 679A06B5EFE687F53058C28A965E87199135B0D93225C2099A2D66BD017F8601 |
| Config/_CraftingDiscovery/recipe_policy.xml | 85 | C67ECA891F373A879148813D30938C271C87CBBBF8C5BD5FD663318D3BC52F41 |

## Reachability and behavior

ImprovisationRecovery is a detached deterministic planner. Root-wide rg -uuu search excluding documentation and Tools, and targeted Scripts/Config/project search, find only its definition. No production TryPlan caller established. It accepts canonical category identifiers (lowercase letters/digits/underscore/dot/hyphen, bounded96), finite progress clamped0..100, ordered recovery bounds below1, bounded1..9 paid stacks and UInt16 stack counts. A seeded LCG partitions cloned input payloads into returned/lost fragments with count conservation. It writes no inventory, journal, network or save state. No current random crafting failure or loss is inferred from its existence.

RecipeDiscoveryPolicy is strict version1 XML: exact root/attribute shape, two recipe attributes, no nested recipe elements, nonblank bounded identifiers, unique ordinal recipe names and maximum8192 entries. Unknown recipe names are allowed by a valid loaded policy; listed names require the callback's real reading marker. Current literal XML has81 mappings: rebirthCookingFoodN01..N66 to CN01..CN66, P01..P04 to CP01..CP04, ten named SurvivorStew recipes to CL01..CL10, and foodForestSmoothie to CL11. Names and sequence were read literally; there is no skipped N segment. Reading grants do not substitute for skill/material gates.

RecipeDiscoveryRules loads beside its assembly from Config/_CraftingDiscovery/recipe_policy.xml, under a shared lock, with prohibited DTD, null resolver and2MiB XML limit. Allows evaluates RebirthKnowledgeService.HasKnowledge(player, RebirthLiteratureService.RecipeReadMarker(id)). Live callers include CookingSessionService registration, cooking station requirements, capability evaluation and camper native crafting; ordinary preparation/discovery helpers retain their separately documented reachability limits. This is no remote grant endpoint. The knowledge service/persistence and owner protocols remain covered by their existing traces, not requalified here.

Conditional availability limitation: loaded=true precedes I/O; any first load/parse exception logs once and leaves policy null. Subsequent Allows rejects every recipe, including unlisted ones, and TryGetRequiredReading/RequiresReading return false for the process lifetime. No reset/retry is present in this file. Current XML is valid; no observed failure or item-loss path is established. Repairing a missing/bad file after first access requires process restart. This is a fail-closed configuration availability limit, not a confirmed multiplayer ownership defect.

CraftOutcomeService builds a read-only snapshot from recipe, player, batches and quality. Success is deterministic100%; primary count uses long multiplication, XP is native base recipe craft XP rather than a promised survivor skill grant, quality derives from chosen/prepared tier, and knowledge/time are projections. No award, random roll, inventory debit, network send or durable state is performed. Personal Details/OutcomeViewModel and the older native presentation call Build. Existing actual completion/degradation/network findings remain in personal/recycling traces.

CraftingPresentation defines native CraftingInfoWindow and outcome-panel subclasses. Native base Init keeps native actions; reflection reads cached recipe. Info bindings supply description, output stats, knowledge/color/time and explorer focus. Output-stat cache keys recipe reference/tier, so changes in other dependencies are not independently invalidating keys; no concrete changing-buff stale display reproduced. Outcome panel subscribes recipe-list/count events on open, removes on close, polls selection/tier at0.20s and caches by player/recipe/count/tier for a frame. Extra output preview delegates the existing output planner, not delivery. Neither class changes authority or saves inventory. Config/XUi_InGame/windows.xml:14071 binds RebirthCraftOutcomePanel and:14103 sets RebirthCraftingInfoWindow. Later personal crafting root replacement and existing presentation trace prevent assuming this older tree is the active personal surface; these exact binding nodes are partial XML reads, not full windows.xml coverage.

SmoothScroll only changes recipe-list geometry/viewport and scroll position. Row count bounded8..256,46px rows,368px viewport; initial forced refreshes and recipe-count change reset offset. It performs no crafting transaction. Current root-wide production and Scripts/Config/project rg -uuu find only class definition; no XML controller binding or caller established. Its presence must not be credited as an installed active scroll fix.

## Cross-manifest pending assessment

Current Scripts/Crafting inventory contains154 C# files. Compared current SHA256 with all *TRACE.md contents: before this document, nine lacked any current-hash entry; four are newly read helpers above, three are parent refund files, and two debug files remain. The two reused files already match CRAFTING_REMAINDER full-read entries. A matching hash is an index check only; semantic provenance remains the explicit full-read declarations in COOKING_AGENT_TRACE (34 production cooking files), RECYCLING_AGENT_TRACE (nine production recycling files), CRAFTING_REMAINDER, PERSONAL_CRAFTING, PERSONAL_CRAFTING_PRESENTATION, REMOTE_CRAFTING_REMAINDER, STATION_ADMISSION and NATIVE_TRANSPORT. No coverage is inferred from traversal alone.

True remaining nonrefund full-source tranche at this inventory:
- Scripts/Crafting/Cooking/Debug/RebirthCookingPointerTrace.cs — 8B8A7D48A49D64B6DB000CF214D21C027CCCF7EA57940B0EAC8873A49D44817F.
- Scripts/Crafting/Recycling/Debug/ConsoleCmdRebirthRecyclingAudit.cs — E11F3633B0E85A6D8BD1408BBABD34BF66D7D099B74B351F9047FB7175EF077E.

Parent refund scope: RefundBackpackPlan (65668D3DE23A3E18632F5E408E407BCC4F21342E09954336A9AE5047C2935902) and RefundPlayerFileWitness (3A0C278D3F407251BE85D342A00D20752CF2E59D6CC9803FEC8BC77EB6893916) now explicitly full-read in NATIVE_TRANSPORT:642, but had no hash manifest at comparison. RefundReceiptReader (1A95D634E9A112AB56DAC5B8DE63185FC32E9680A45D69D7DB08728E47D6EA1F) is still assigned to parent; no full-read credit claimed by this agent. RefundArchive/Preparation have existing evidence and remain parent-owned. New/current edits after these hashes require new comparison.

No new confirmed gameplay finding added. All runtime/native topology limits and existing findings remain intact; source closure does not establish played cancel/reload/reconnect/concurrency success.
