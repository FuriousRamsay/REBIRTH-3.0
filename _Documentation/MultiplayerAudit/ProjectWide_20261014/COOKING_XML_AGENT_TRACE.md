# Cooking XML catalogue and output binding audit — 2026-10-15

Audit-only continuation of COOKING_AGENT_TRACE.md and RECYCLING_AGENT_TRACE.md. No production edits, builds, tests, game commands, Git operations, or shared report/coverage-ledger changes. Existing source/native traces are reused, not credited as new full reads. Operating directives and existing audit evidence were reviewed. Runtime reproduction remains NOT RUN. Preserve native owner actions and the four supported scrap stations.

## Read method and exact scope

COMPLETE SEMANTIC XML READ (not a literal line-by-line claim): Config/_Cooking/runtime.xml, items.xml, burnt_items.xml, balance_items.xml, loot.xml, improvised_items.xml, recipes.xml. Each whole document was parsed with XmlDocument, whitespace-only formatting removed, and every exact distinct subtree interned into a directed acyclic graph; all node attributes, non-whitespace text, comments, child order and every reference occurrence were retained. Every graph node and root was displayed/read in bounded chunks. This is a content read, not a name/property scan or hash-only coverage assertion. Duplicate subtrees were read once and their complete use lists read. Initial exporter used PowerShell XML-adapted .Name (shadowed by @name); it was corrected to XmlNode.get_Name(), all graphs regenerated, and corrected graphs used for conclusions. Truncated displays were reread at missing boundaries. The unsuccessful template experiment is not evidence of coverage.

Reproducible scratch evidence: E:/_Haven/shared/REBIRTHTemp/CookingXmlAudit20261015/ExportXmlGraph.ps1 and ReadXmlGraph.ps1, plus seven *.graph.txt files. Graph lines contain id, true node tag, all attributes, ordered child ids and text. Counts below are document element occurrences / unique graph nodes (unique count also includes text/comments where present): runtime 1012/471; items 6120/1294; burnt 3698/292; balance 839/641; loot 320/319; improvised 326/66; recipes 404/204. These counts explain normalization, not gameplay validation.

FULL literal read reused/rechecked: Config/RebirthRecipeOutputs.xml, Config/_Cooking/blocks.xml, Config/_Rebirth/items.xml, recipes.xml and loot.xml; Config/_Survivor/loot_food_exclusions.xml and Config/_Workstations/mortar_recipes.xml. Include-bearing relevant regions of Config/items.xml, recipes.xml, loot.xml, blocks.xml and XUi_InGame/xui.xml were read. Config/_Survivor/recipes.xml cooking recipe nodes read in full; unrelated gear/seed nodes are outside this tranche. Config/_Workstations/recipe_tools.xml has targeted cooking/ammo/forge/tool selector reads only, not full credit. Vanilla Data/Config/recipes.xml matching byproduct recipe nodes read in full, not its entire file. RebirthScrap.xml remains the previously documented structural/sampled rule review, not newly FULL. UI layout files (hud_windows, workspace_windows) remain outside this gameplay-content tranche and retain previous partial status.

## Include order and authority

Config/items.xml includes _Metabolism/items.xml before its progression branch. Rebirth mode then includes _Rebirth/items.xml, which includes _Survivor/items.xml and food_content.xml before _Cooking/items.xml and improvised_items.xml. The outer file applies _Cooking/balance_items.xml, then burnt_items.xml after that Rebirth include. Thus the authored N/P/L food nutrition/water values in items.xml are superseded by balance_items.xml; quoting only their initial values would be wrong. Final global Drink stack patches apply later. Outside Rebirth progression, these Rebirth food/balance/burnt definitions are not included by that branch.

Config/recipes.xml Rebirth branch includes _Rebirth/recipes.xml: _Survivor/recipes.xml precedes _Cooking/recipes.xml. Later _Workstations/recipe_tools.xml and mortar_recipes.xml can set existing recipe station/tool attributes; mortar removes craft_tool from all recipes at its station. _Cooking/recipes.xml itself sets foodCornMeal craft_area to mortar and appends the N01–N66, P01–P04, ten legacy stews, Forest Smoothie, pans and three station recipes. No <output> nodes occur in these appended cooking recipes. Additional outputs come from the separate registry.

Catalogue runtime.xml is loaded directly from the executing assembly directory by RebirthCookingCatalogue.Get, not through XML include processing. It has 135 dishes: 77 campfire, 26 iron oven, 16 gas stove, 12 cold, 3 mortar, 1 chemistry. Its complete technique set is M01–M44: T=time, Q=comfort, H=compatible herb; M23 has basil/dill and M24 sage. Dish book/magazine/card ids resolve to the authored literature items. Preparation-reference books/magazines are separate from card discovery gates. Seven BasicRecipes and universal policy remain explicit exceptions; foodCharredMeat is excluded by IsCooking even though a catalogue and burnt item exist.

AtStation uses catalogue station ahead of native recipe area: cold may use cooking surfaces except mortar; campfire pot dishes may use gas stove; cntWoodBurningStove accepts campfire. ForStation clones the native recipe with selected station and catalogue tool while retaining primary count, ingredients, effects and time. This explains why the four _Survivor dishes originally declared campfire/pot become gas-pan or oven-pan in managed UI; raw recipe craft_area alone is not final managed selection authority. Notes in @substitutions are descriptive text; only <substitute> children authorize actual alternatives. N22–N29 notes, for example, must not be treated as fully implemented substitution rules simply because they describe proposed variants. No aliases were invented.

## Authored consumables, burn and balance

items.xml appends 300 items: N01–N66, P01–P04, ten legacy stews plus Forest Smoothie, cards, M01–M44, B01–B45, and two pans. Normal meals use the custom metabolism consume Action0, safe/normal profile and explicit shared variety families; recipe output multiplicities range 1–5 for the food recipes and 4 for milled components. N37/N38 are Drink/tea with 220 ml capacity/initial volume/DegradationMax and sip sizes125/50, empty jar identity and nonreusable flag. Forest Smoothie is Drink/tea with250 ml. These drinks have no automatic native consume-container assumptions added by this audit.

balance_items.xml removes/reappends nutrition and water/initial-volume fields. Examples of effective authored values: N01 nutrition11.8/water350; N02 18.88/350; N04 8.26/45; N09 29.5/100; N37/N38 nutrition3.54/initial volume220; Forest Smoothie18.88/250; BeefMushroom22.42/water250; BrainStew16.52/250. A batch's dynamic cooking metadata can override authored stats; the initial per-item XML is not a claim about final consumed batch nutrition. Existing partial-stack/provenance finding MP037 remains separate.

burnt_items.xml appends132 explicit rebirthBurnt_<source> items. Shared definition: Food, nutrition0.5, water0, stomach300, safe/normal, mood-5, shared burnt_food family, hydration cost100, stack10, no economic value, hidden creative, custom metabolism consume action. Burnt boiled water and other burnt drinks are intentionally authored Food identities. RebirthCookingHeat.Output derives the exact prefixed id, preserves generated metadata, zeroes water/energy, caps generated nutrition at2, and sets burnt/quality/comfort. This is not evidence of an owner/network defect. Cold recipes and milling do not use a heat-burn path; existence of an unused burnt item such as charred meat is not itself a reachability finding.

improvised_items.xml contains12 stack1 definitions: Soup01–04, Pan01–04, Baked01–04. Soup baseline water100; pan/baked0; all nutrition10, stomach300, simple mood0, no economic value, same consume action, separate shared families improvised_soup/pan/baked. Dynamic assembly supplies actual batch metadata, so these are fallback values, not a new free-nutrition assertion.

## Loot exact effective effect

_Cooking/loot.xml defines128 equally weighted card entries,45 weighted books,44 weighted magazines,81 food entries, then appends card/book/magazine distribution into cupboards/bookcases/book piles/mailboxes, food pool into groupFoodUncommon, pans into groupCookingTools (each prob0.5, random_durability=false). All referenced item identifiers are included in the cooking item definitions.

Crucial later operation: Config/loot.xml includes _Survivor/loot_food_exclusions.xml after the _Rebirth/loot.xml chain. That document removes the named prepared meals/fresh ingredients (including every N/P/legacy pool member) and removes every item reference with group=rebirthCookingFoodPool. Consequently the authored cooking food pool does NOT supply those meals in Rebirth mode after these patches. Its food entries and group call are intentionally inert under the later owner policy. Literature and pan entries are not removed by that food exclusion. The earlier container_junk_balance layer and reading-option cassette exclusions are distinct. No loot-loss MP finding is inferred from intended food removal, and the raw cooking food pool is not evidence of runtime meal availability.

## Additional output bindings: concrete managed impact

RebirthRecipeOutputs uses exact case-insensitive recipe-name#crafting-area keys, rejects IsScrap, loads Config/RebirthRecipeOutputs.xml beside the assembly (with loaded-mod fallback), then captures authored <output> nodes from the actual patched recipe document at RecipesFromXml.LoadRecipies Prefix. Later definitions replace whole lists; completed applications multiply counts, never primary recipe.count. Missing recipe/item remains diagnostic and no alias fallback occurs. The separate file's39 definitions include historical unavailable routes, not39 verified current recipes. Loaded merged document/current DLL identity remains unverified because no game/config load was run.

The full cooking recipe/item/catalogue read expands the previously confirmed managed bypass finding in COOKING_AGENT_TRACE.md to all ten actually authored stews and Forest Smoothie. For each stew the runtime catalogue, recipe primary item and item definition agree. Forest Smoothie is cold with a live personal recipe. Managed Heat.Take grants only primary Output(recipe); it never invokes TryPlan for additional outputs. It suppresses the native completion path, so native integration cannot rescue these returns. These are concrete consumed-container losses for successful managed cooking, not speculative missing historical recipes. Expected extras per completed application:

| Actual recipe | Native authored area | Primary count | Empty jars | Empty cans |
|---|---|---:|---:|---:|
| SurvivorStewBeefMushroom | campfire | 1 | 1 | 1 |
| SurvivorStewBrainStew | campfire | 5 | 5 | 0 |
| SurvivorStewChickenChiliYucca | campfire | 1 | 1 | 2 |
| SurvivorStewChickenSoupCorn | campfire | 1 | 0 | 2 |
| SurvivorStewLambPeasRavioli | campfire | 1 | 1 | 3 |
| SurvivorStewMisoPetFood | campfire | 1 | 0 | 3 |
| SurvivorStewPetFoodPuree | campfire | 1 | 1 | 2 |
| SurvivorStewSalmonChiliPear | campfire | 1 | 1 | 3 |
| SurvivorStewTunaCatFoodPumpkin | campfire | 1 | 1 | 2 |
| SurvivorStewTunaDogFoodBlueberry | campfire | 1 | 1 | 2 |
| foodForestSmoothie | empty/personal | 1 | 1 | 0 |

BrainStew proves multiplicity matters: one application produces5 portions and owes5 jars, not25. Successful take, full inventory spill, cancellation before completion, burnt take, save/reload and independent remote-owner/host all remain required execution cases in the parent trace. No runtime claim is made. The existing finding should be extended with this table, not duplicated under a new issue number.

ForStation changes craftingArea to the chosen cooking station; therefore a future managed integration cannot simply call TryPlan on the rewritten recipe and assume campfire definitions match gas/wood routing. It must retain canonical paid recipe identity/output definition and honor MP029 binding. This is a remediation constraint on the confirmed bypass, not a separately proven loss path beyond it.

Other registry definitions: current vanilla source has resourceGlue campfire and two chemistry variants; native foodBoiledMeat/CornBread/EggBoiled are campfire, while their registry entries exist only at historical cntWoodBurningStove_PickedUp. No current recipe nodes for most historical chemicals/resourceYeast/dyes/wood-stove variants were found in the parsed recipe-file inventory; this absence is discovery-only until the actual merged native recipe document is captured. Do not silently rename them or claim every historical extra currently executes. Four allowed scrapping stations remain SonjaOutfitDesigner, SonjaAmmoRecyclerStation, SonjaRecyclerStation, WorkbenchShredder001_FR. Their scrap yields are <ingredient> yield rules in RebirthScrap.xml, never these regular <output> definitions. RecipeOutputs.TryGet explicitly excludes IsScrap. None of the39 registry keys names those four stations. SonjaRecyclerStation normal recipe area=workbench explains the earlier mixed-queue reachability analysis, without expanding the station allowlist.

## Limits and reassignment

No new issue number proposed. This tranche strengthens actual XML reachability/counts of the existing managed byproduct bypass and prevents false coverage/loot claims. Source/native network/cancel/save analysis remains in COOKING_AGENT_TRACE.md; no XML-only claim can validate loaded transport, retries or persistence. Complete rule-by-rule RebirthScrap.xml content, remaining workspace/HUD visual XML, whole unrelated include files and real merged engine config remain explicitly uncredited. Parent should verify these findings and update shared reports/ledger. Ready for reassignment.

## Hash manifest
- COMPLETE SEMANTIC XML READ: Config/_Cooking/runtime.xml | bytes=75676 | SHA256=CC25DA977F45096602015EF4A31E6A041907E442C0ED0EB1BB2628A88B2D9FB2 | graph SHA256=D7C9F31A0CD111C0B81F39E47CC65174F2B754D2C873E364E071AA9C5CC28D63
- COMPLETE SEMANTIC XML READ: Config/_Cooking/items.xml | bytes=391909 | SHA256=03EAE872CE45BD78C137E7B57D40DC6370562AF7AA859B0095D18636510AADAD | graph SHA256=69EDDB69E57205DB9DC3846D9992E98EA34A08CC62F64F75D875FBCE68BEB895
- COMPLETE SEMANTIC XML READ: Config/_Cooking/burnt_items.xml | bytes=233585 | SHA256=DC926E4F609E2D7DB2FE6C1FEFCE6896E31F0190648931425F9FE4DBCB8EEE91 | graph SHA256=F7871959130D2EFE5C33F865B34E2158FBA9F135ADCC5ADACD1BF9E7B179E0A4
- COMPLETE SEMANTIC XML READ: Config/_Cooking/balance_items.xml | bytes=66331 | SHA256=2F0DF648EBF48BE6002F9174E180AF19C54C9EDAC977244D3A92EE85DFDE7D89 | graph SHA256=E2F6B8B8796934E586ACBAD8101537AECD97F5088D3DE6D034A10E35EA9F110D
- COMPLETE SEMANTIC XML READ: Config/_Cooking/loot.xml | bytes=21653 | SHA256=ACF1C1423BD42C1546A6BE092C38894AACF8D394F9548DF635AEFFBD94A77559 | graph SHA256=42D519DBC27BEC9DBE46296C3A128BBA50E1C60E9A56133E3C76F39FD382CFF9
- COMPLETE SEMANTIC XML READ: Config/_Cooking/improvised_items.xml | bytes=20747 | SHA256=4C616C77CFFE306A94E1A8B27A93ADD6A34427EE2716EE2FD02CBAFF74856FC3 | graph SHA256=A86C874246F48120D5D681BFD2E79B002EED001F00CD7FE6280D0C486A9DF540
- COMPLETE SEMANTIC XML READ: Config/_Cooking/recipes.xml | bytes=29780 | SHA256=919EE9E1EDFE2AB3DBEE6921621595913BFA76C28C681852142EB8BA0DA2369A | graph SHA256=D3B3F22CD2B686F1345AC2742BB7EE536FB0C326F469F1943FA8103E6EEFAA80
- FULL literal XML read/reused: Config/RebirthRecipeOutputs.xml | SHA256=E09633C29A01E3458273D7FD1C6E24896102A7717FF123DC1A8035C8E1643E74
- FULL literal XML read/reused: Config/_Cooking/blocks.xml | SHA256=1A2FD33957836C75700BF5C78007387ACD7825270BA437385E9B79DA17449BBE
- FULL literal XML read/reused: Config/_Rebirth/items.xml | SHA256=B6232DCE23EF277896767B70D2E781C7E350DEE825AF481FFFA23575208FB999
- FULL literal XML read/reused: Config/_Rebirth/recipes.xml | SHA256=C545B3897CF1C19494B7EA95AEDF13378437B2CB1A1DFB0DF6CEB1C5BB7ADDDD
- FULL literal XML read/reused: Config/_Rebirth/loot.xml | SHA256=7E27F95019A8232F9DDCD185095FBD6EFB72AAAF4881C4FC2438CF5633ED6C03
- FULL literal XML read/reused: Config/_Survivor/loot_food_exclusions.xml | SHA256=63C74D4EC0167CF39B97166F7C2FC3F101D6C8BAD12E1E0C3889287AAB622084
- FULL literal XML read/reused: Config/_Workstations/mortar_recipes.xml | SHA256=58AD4D4092856D6BFA0E7FDF9A555D99B6A7B56AFA4774645994217EA0E36D35
