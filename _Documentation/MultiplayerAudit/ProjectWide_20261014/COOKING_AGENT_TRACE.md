# Cooking audit agent trace — 2026-10-15

Source/target: current shared cumulative REBIRTH 3.0 mod, exact installed Assembly-CSharp SHA256 FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705. Audit-only unique documentation. No production changes, builds, tests, game control or Git operations. Master report/coverage ledger untouched. Project directives read earlier in this audit; current report MP-010/037/041/042 and recycling migration/output evidence reviewed to avoid duplication.

## Actual reviewed scope

All34 current Scripts/Crafting/Cooking C# files FULL source read, including the98KB XUiC_RebirthCookingWorkspace and both partial-class files. Hashes below identify reviewed source. Native and other subsystem dependencies are named-method/PARTIAL unless stated explicitly. Config/_Cooking is much larger (including611KB workspace and392KB items), so XML coverage below explicitly identifies actual node/full-file reads; it is not an all-XML conformance claim.

## New confirmed managed cooking additional-output omission

Classification: medium functional/item return conservation; source-confirmed production integration omission, gameplay NOT RUN. This is separate from MP-028 partial insertion and MP-029 definition drift. Those paths at least enter the additional-output integration; the cooking route never does.

Concrete current binding: Config/_Cooking/recipes.xml:397 defines SurvivorStewBeefMushroom count1, craft_area=campfire, craft_tool=toolCookingPot and foodCanBeef1, mushrooms1, cornmeal1, goldenrod1, boiled water1. Config/_Cooking/runtime.xml:1039 binds that dish to campfire/pot. Actual items.xml item node read: Food/gateway, Nutrition20.9, Water250, Stomach300, cooking tags. Config/RebirthRecipeOutputs.xml:17 provides additional jar1 and can1 per completed application for the SAME recipe name#campfire.

Workspace.RefreshStationCatalogue -> Catalogue.AtStation/ForStation admits this cooking dish. Cook constructs/stamps concrete recipe, Heat.Mark marks held managed batch, Session.register persists owner ticket, callback Enqueue adds native-backed UI job and debits staged inputs. XUiC_RebirthCraftingQueueEntry.Update named method (:120–135) handles managed recipes by Heat.TickUi and deliberately skips base.Update/native outputStack. Its ready/take button (:65–69) and CookingStation.Update reload hotkey (:96–97) call Heat.Take.

Heat.Take (:193 onward) builds ONLY new ItemStack(Output(recipe), recipe.count*recipeCount), adds that to PlayerInventory.AddItem and drops exact remainder if needed, then acknowledges completion and clears queue. No RebirthRecipeOutputs.TryPlan, recipe-output receipt, outputStack scope or AddItemNoPartial scope is entered. Closed/native Heat.TickTile returns false for managed recipes, suppressing HandleRecipeQueue; native extra-output insertion is therefore bypassed there too. Milling TryMillingOutput likewise inserts ONLY Output(recipe) on a cloned output array. It has no extras hook. An owner can receive the stew without configured empty jar/can in this path; no packet fault or output-full event is required for the omission.

Scope: local/listen owners and placed remote owner managed cooking paths; mobile paths only where current local mobile cooking admission exists. Mortar potential byproduct omission is established at algorithm seam; exact current availability/classification of each legacy vinegar/yeast recipe still needs validation and is not assumed from fallback definitions alone. Ordinary campfire processing/chemistry recipes that remain native are distinct: they may enter additional-output integration and are not all missing extras.

Correction direction after audit/checkpoint: include one frozen combined primary+extras plan in managed batch admission/completion, with multiplicity=completed RECIPE APPLICATIONS/portions, not primary recipe.count. Preserve manually collected batch, burnt output, full-output refusal/drop policy and native owner inventory authority. Couple every output to the same original outcome and queue retirement; merely adding sequential extra AddItem calls would import MP-028's partial-success repeat/loss gap. Qualify count>1 recipes, multiportion batches, burnt meals, full bag/output, collection/drop failures, repeated click, cancellation and save/reload with changed definitions. No implementation undertaken.

## Managed collection/cancellation exceptional custody gaps

Source-confirmed mutation ordering; incidence requires callback/setter/drop/transport exception or interruption and was NOT induced. This is additional cooking scope of the known cross-inventory settlement problem, not proof that ordinary AddItem always fails.

Take grants primary output via AddItem (can partially insert), falls back to native ItemDropServer for positive remainder, then invokes Session.Request("complete") BEFORE ClearRecipe/RefreshQueue/syncTEfromUI. It has no in-progress claim or retained outcome. If insertion/drop already happened then a later synchronous completion request, callback or queue-sync boundary throws, a still-present row can allow repeated collection on retry. AddItem/drop failure before durable row removal can likewise leave uncertain paid output. Existing mobile IsCurrentCard is a precondition, not a multi-output receipt. A patch must retain original granted subset/uncertain outcome rather than assume exception means nothing happened or blindly repeat output.

Cancel computes concrete substitution refunds and strips only queue metadata, clears recipe/refreshes queue, turns fuel off and syncs TE BEFORE issuing each AddItem/drop refund. A throw during later refund leaves no integrated queued job/remainder delivery record. ReturnIngredients similarly empties a staged UI slot before RefundIngredient; station OnOpen clears hidden stranded output storage before inventory/drop recovery. These preserve ordinary no-throw behavior but do not close crash/exception custody. Do not fix by simply reversing order, which trades loss for duplication. One scoped original operation with recoverable before/after/remainder settlement is needed, preserving native owner actions. Exact callback exception reachability and native save ordering remain qualification tasks.

Native source context: exact XUiC_RecipeStack.Update full named method (:301–455) independently owns native output/retry/count decrement only when invoked. The custom managed subclass explicitly skips it, so native recipe inventory-full handling cannot be cited as recovery for Heat.Take/Cancel. Native AddItem partial transfer is already accounted for in the custom happy path (positive remainder only); no duplicate-full-stack refund allegation is made for that branch.

## Owner ingredients and existing MP-010/041/037

RebirthCookingLocalIngredients FULL read. Count/Take reject library resource holds, restrict belt to owned slots, normalize only the required fullIngredient clone, aggregate compatible demands, plan exact source locations, validate all preimages before setters, then append removed stacks/return receipts and call Bag.SetSlot/Inventory.SetItem. Void setters are not reread; a suppressed setter can report a removed ingredient without actual debit. Existing gear/camper native guards demonstrate suppression mechanisms, but actual overlapping cooking flow must be proven before claiming an observed unpaid meal. This is not a native routine setter refusal. No general server-debit redesign is proposed.

Cook fresh preflight captures exact recipe images/effects, inputs, owner/world/controller/UI generation/queue entry identity and double-read validates. It checks full liquid ingredients, unique types, native requirements and queue capacity before registration. Registration is asynchronous on remote clients. Callback verifies current session/input image/batch and later Enqueue rechecks queue occupancy, but does not rerun full tool/fuel/station live-lock preflight. Tool/fuel changes while register is in flight therefore remain an admission-boundary qualification. Current managed heat ticks themselves do not recheck required tool presence/durability. Whether intended tools must remain for entire cooking is not assumed; current callback scope should at least be tested against original admission expectations.

MP-010 retained: Pull waiting callbacks have no integrated retry/query/teardown and Receive removes before callback. CompleteIngredientPull can return a delivered receipt through captured xui.PlayerInventory even after original session invalidates; fallback player argument/world and xui inventory can differ after reuse. New direct placement path stages exact fetched+local physical stacks against frozen need/ghost/input images, and successfully consumed fetched stacks are zeroed only after placement. PullContinuation is source-free and claims before callback, so it prevents withdrawal replay; it is not durable remainder custody. ReturnPulledIngredients still needs original owner/session settlement.

MP-041 retained: RefundIngredient trusts ReturnReceipt.Restore quantity and subtracts it. Existing companion adapter stale-revision/no-op acceptance can erase refunds. This pass does not claim remote pull shares that exact offline/listen adapter failure. MP-037 retained: non-liquid Compatible compares cooking fields and omits general/electrical provenance, so partial native type-only merges can lose unrelated metadata. Liquid compatibility checks effective volume/profile/cooked yields/mods/mood plus other metadata/provenance. Full/partial/drag/sort hooks are explicitly installed, not inferred from Harmony attributes alone.

## Liquid, tool and recipe choice semantics

FullIngredient accepts non-drinks; Drink requires remaining>=capacity-.01. Catalogue.IsIngredient first rejects partial drinks, accepts herbs, cooking ingredient lookup, or food/drink definitions. Local Take and remote fetched-stage path require FullIngredient again. Thus the source code does not intentionally consume a partly used drink as a full liquid measure. Ghost fullIngredient metadata is a request marker and removed only from required clones; source physical metadata remains intact.

ImprovisedMethod: iron oven=Baked; gas stove with no liquid main ingredient=Pan; otherwise Soup. IsLiquidIngredient resolves Drink OR food water>=200 and water>Nutrition*15. Pan rejects ANY liquid; Baked allows at most liquid count<=solid count/4. Soup accepts combination. Recipe method/tool mapping: pot Soup, rebirthCookingFryingPan Pan, baking pan Baked; grill gets Pan burn behavior. Selected named recipe uses declared roles/substitutions and required tool rather than improvised method. Distinct ingredient type per role is enforced by solver/AssignRoles; native order with tool-bearing candidates first resolves experiment ties. Batch bounded1..9999 with checked quantity products; no synthetic ghost inventory is credited by choice.

Exact authored evidence: blocks.xml FULL read defines mortar output-only, gas Campfire tools/output/fuel with campfire+gas areas and ammoGasCan fuel, iron oven Campfire tools/output/fuel with oven area and ammoGasCan. Gas has ScrapToolWearPerMinute50 but this does not establish regular cooking wear. windows.xml FULL read binds stovetop pot/frying pan/grill and oven baking pan tool UI. workspace_windows named tool grids retain distinct campfire/stove/oven controllers; no layout-only argument substitutes for backend requirement checks. Runtime dish nodes show named campfire pot recipes and explicitly cold dishes. Catalogue.AtStation allows cold on all cooking work surfaces except mortar, campfire on wood stove, and campfire pot recipes on gas; ForStation clones ingredients and declared tool into current station area.

Tool wear remains the already reported ordinary-crafting migration gap; cooking checks availability at preflight and has no UseTimes writer in this subsystem. Generic RebirthStationOpenQueueToolHoldPatch is limited to XUiC_RebirthStationWorkspace; CookingStation is a separate type and managed update skips base native queue patch. Do not assert recycler wear satisfies cooking tool degradation.

## Registration, persistence, completion and retry

RebirthFreshInit invokes CookingBatch.Install explicitly. FULL Batch.Install installs native consume-independent cooking merge, recipe lookup, literature, transfers, processing activation patches and direct native output/update/HandleRecipeQueue/ItemValue constructor/AddCraftComplete/GiveExp/Recipe.Read hooks, then JobRuntime. Harmony attributes alone are not proof; these actual installers are present. Installed-DLL/current-source identity and successful live patch metadata NOT inspected.

Native queue persistence was traced in recycling pass: RecipeQueueItem.Write/Read FULL read, Recipe.Write/Read named read serialize whole recipe and ingredient ItemValue metadata. Batch restore adds saved craftingToolType and UseIngredientModifier=false from marker. Heat duration/elapsed/overdue/method/report marker and batch token/portions live on ingredient metadata; output data is copied deliberately while queue-only fields are excluded. Native ItemValue metadata codec compatibility remains the shared installed-native evidence, not a played save/reload result.

Session.register requires current server world character, valid recipe/catalogue, GUID token, bounded portions/output count, discovery/capability; camper address requires live lease. It persists Ticket before returning success; failed save removes new ticket and marks rollback dirty. Placed position is supplied text without an equivalent live station lock check in register; native station owner still controls actual enqueue. This registration alone is not item-payment or completion evidence. Remote Session packet authenticates entity+user but carries no original creation ID/transport epoch; character replacement/late request and server handler current world membership remain qualified limits, not a demonstrated arbitrary-player exploit.

Memory Tickets/Awards/Ready/PendingUnlocks persist under world-character cooking XML. Ready uses UTC expiry to avoid relog renewal. Awards highest completed ordinal/tombstone prevents repeat acknowledge; compact history retains known-batch identity. Read has tolerant per-node parsing and does not independently enforce finite XP (Math.Min/Max on parsed NaN can preserve NaN); parent world-character schema validation must be traced before promoting malformed-file impact. No current malformed save is claimed.

AwardCompleted requires server completion witness, current eligible authoritative character, registered ticket/name/output count and ordinal<=portions. It acknowledges memory before later statistics/skill/theory/save steps. A failure between acknowledgement and those effects will make later completion return at completed<=previous. This is an extension of existing staged progression-effect settlement concern (MP-016 family); exact independent award service recovery may narrow it and was not FULL-read here. Do not replay all rewards merely because final FlushPlayer failed. Failed final save queues owner publication and preserves acknowledged runtime memory, but this is not a joint station+character atomic commit.

JobRuntime is one server GameUpdate observer/reconciler, not heat clock. It rebuilds witnesses from native queued recipe/authoritative CraftCompleteList and owner ticket token, so reconnect entity ID need not match original cook. It retains witnessed uncredited completions while current eligibility restores; unavailable station is not treated as empty. It retires station links/tombstones only from observed loaded released absence/credited receipt. Chunk observers shared by position are authority-only, wanted each frame and removed on inactivity/reset; offline owners stop requesting observers but native loaded station persistence can retain receipt. Large unloaded-world/offline catch-up behavior not executed.

Session replies bind World, have15s timeout and bounded256 callbacks, main-thread Pump via JobRuntime. Pull callback service remains separate and unbounded/no-timeout as MP-010. ResetRuntime clears session replies/pending/client memory/completion witnesses; BlockPickup mod lifecycle calls JobRuntime.Reset on world/save cleanup (callsite discovery only). No deferred effect is silently declared durable simply because reset exists.

Camper restriction: FULL RebirthCamperCookingQueueAdmission.CanWrite requires current authoritative nonremote world, primary local station lease and matching one managed registered ticket; IsCurrentCard used by Take/Cancel delegates this. Remote mobile Take/Cancel therefore refuse this seam. Parent camper audit must decide/trace current supported remote cooking route before a universal missing-support assertion. It is explicitly not equivalent to placed remote native owner flow. No remote receipt protocol was added here.

## XML read and remaining limits

FULL files: Config/_Cooking/blocks.xml, windows.xml, workspace_xui.xml, xui.xml and prior FULL Config/RebirthRecipeOutputs.xml. Named-node reads: recipes.xml first140 lines plus SurvivorStewBeefMushroom/nearby legacy stews; runtime.xml foodBoiledMeat/SurvivorStewBeefMushroom bindings; actual items.xml SurvivorStewBeefMushroom complete node. Parsed source XML queried by exact item, not game loaded XML. Include order discovery: Config/_Rebirth items/recipes include cooking definitions, Config/items includes balance, blocks includes cooking, XUi_InGame/xui includes base then workspace replacements. workspace_windows source tool/input/queue controller bindings read by named nodes/search; its full611KB layout NOT read. Remaining runtime catalogue/all recipes/all item/burnt/balance/loot XML and actual merged definitions/assets/localization need further review. No UI screenshot judgment or game test claimed.

## Completion and full-read hashes

Bounded all34-C# source pass COMPLETE; cooking migration/multiplayer qualification remains NOT COMPLETE. Requirement trace: current sources/installers -> native-managed owner entry -> recipe/liquid/tool XML bindings -> inventory/cancel/retry/save/outputs -> documented findings/hashes: PASS for stated audit activities. No implementation or archive delivered. Runtime host/remote, failure injection, character replacement, save/reload and merged XML remain pending.
- FULL SOURCE READ: Scripts/Crafting/Cooking/ConsoleCmdRebirthCooking.cs | lines=19 | SHA256=E02AF18AB408C0895FF2035DD6253756F0A9E85F4290D1C56B6573E98639019C
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCampfireProcessing.cs | lines=111 | SHA256=C7E7E432EBA0EC38EC7030A877CB7953DB34D8C2439E938AF0F1B075063F2AFA
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingBatch.cs | lines=452 | SHA256=554AB670CDB1CB79D8E1714AAB82CE7EC8005D543C07A1B76EC730D2E2A5ACAE
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingBurntIcons.cs | lines=4 | SHA256=76C7C1168CBC80DD8D6B400752F5C7A52B831C4361DC380D511BAF7456775EF4
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingCatalogue.cs | lines=150 | SHA256=E89F0096EDCE8AD7147EEEB9AE251EA018085DF5602258988506608DEFAC0D3B
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingDiagnostics.cs | lines=63 | SHA256=E1C7BC45500A04291A4329241791D5B7A6A653D9E6680ED4410F502C3E66AF61
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingHeat.cs | lines=212 | SHA256=F28CE2DFC828762656D0A784C1F976A025F497E5F6941E18A4A21CB8B6308B79
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingHeatRules.cs | lines=55 | SHA256=E0B0548E4EE3DD9992F5761066D0FD96DF85FA83057B0089AFCEE66563030087
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingHudService.cs | lines=225 | SHA256=9CB5F6958FB07E9FD7CA37D934B51BEA93BCB0840B14D53A9F75D8335EFCBD1C
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingIngredientPlan.cs | lines=48 | SHA256=70B117E4AACB71CAA7E226A68D1905A93AB8FD2FCBFCB75EFC7D3B354F9DA3C1
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingInventoryCompatibility.cs | lines=106 | SHA256=DA57D6ABD3B1DB9F94B98972BC7EBE8F215DA18651A1688FE431F0B2B886B8A4
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingItemStats.cs | lines=195 | SHA256=97AC982A78DF9FBC879C12B3B079D135DDE32C7846993ACFE35C06DB91627703
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingJobRuntime.cs | lines=247 | SHA256=701C411109B229F07F291017D588943E1CB032148950433D582DC7D94597CEA1
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingLiterature.cs | lines=81 | SHA256=33E55996683294AB9A4AF6CAA6FEFEE693244E8C8E7FF57BC42BF01C46F4C33F
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingLocalIngredients.cs | lines=85 | SHA256=5FEF328E08FB18298044C60D7D1ABE59E376C19CD9D77E9A62208DD6917CC5A0
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingMemory.cs | lines=123 | SHA256=805F2CD9B82C4570C509CFB157021A59B34A47B68865C70682D155BC5DA293BF
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingNavigation.cs | lines=60 | SHA256=AB8C72D38740AE7FF164150DFFDF381E2C7F875F6CE8D7F64D2250220832FB3A
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingPointerDrop.cs | lines=144 | SHA256=AF50D7D4CDCBFE3064F7E3F76A1C58351D500FA13382247A23E10E812722E72F
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingPreparation.cs | lines=26 | SHA256=492D3CA12D1E1C2185DEC962193C64F3BD9EEF4F2130929993596DFB5CC43A3C
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingPull.cs | lines=98 | SHA256=2C5388DFB206AC208EE5D661D66FBF3E7B2D91A73E4745FC172CEDBF7D2DFE3C
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingPullContinuation.cs | lines=23 | SHA256=6C228F9CE6F79415CD00027297E0DA0874AF028A04A4CFF87694D92D9B64BEE1
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingRecipeLookup.cs | lines=62 | SHA256=30CFDDF2528941E309FE547D559236A4FC14564DDA4A6DA5E044B8AE777D9647
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingRules.cs | lines=22 | SHA256=9BD43041BFA7AB7623E6CC25922CB51FEA601444FAA5495C3BF1E83686897B7E
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingSessionService.cs | lines=218 | SHA256=1CB76D88AA1CDF894163FDD917AA9D45D9644005BE809A2EFCB72A6A1764E0CF
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingStationLocator.cs | lines=53 | SHA256=C9108F00C1D17A1918D31A2D0C7598D6A4314F49F93970E92B8D2E6301FD29C4
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingStationTracking.cs | lines=48 | SHA256=9A8C1FC579BC89FB5411FD9F5E3B1CA24190E3C0E3FF872B4A0E50BC13401510
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthCookingTransfers.cs | lines=37 | SHA256=551A6EB3ED55C84A26D96BEFB0249048E37E179B724E0DA8421496966263D77D
- FULL SOURCE READ: Scripts/Crafting/Cooking/RebirthRecipeUnlockNotifications.cs | lines=23 | SHA256=D9B98EF440CF5DAC33A0A85C88610DA00C06213DA5F17BFDC9087EAA6851E650
- FULL SOURCE READ: Scripts/Crafting/Cooking/XUiC_RebirthCookingAction.cs | lines=119 | SHA256=206F4198CE81B209492167BB8EF8EDE6223872855ECE758ADCCDFCA7EB930EB5
- FULL SOURCE READ: Scripts/Crafting/Cooking/XUiC_RebirthCookingScroll.cs | lines=78 | SHA256=68106668F3B357CCD6F7DA15FED74063C73FB96B2B4C20F9E3EC98101DA51807
- FULL SOURCE READ: Scripts/Crafting/Cooking/XUiC_RebirthCookingStation.cs | lines=189 | SHA256=753751578A8D73DF6D5CC110BAD99883DAE024534BE2CCF60BD5F3963449EC4A
- FULL SOURCE READ: Scripts/Crafting/Cooking/XUiC_RebirthCookingWorkspace.cs | lines=1463 | SHA256=0F52C2E90E267FBED7FCA0DE17634D3D317142EB3DFB7701DA2E4E7F43402BB2
- FULL SOURCE READ: Scripts/Crafting/Cooking/XUiC_RebirthCookingWorkspace.MillingLayout.cs | lines=93 | SHA256=93D2E245750FF92EF2F6BE0C698CBC0EB30F6A7C9B07510343773088F2CD4875
- FULL SOURCE READ: Scripts/Crafting/Cooking/XUiC_RebirthCookingWorkspace.SharedMilling.cs | lines=46 | SHA256=53AFF6AF0FDB04386011EBAFF2BCFEABF506F9ACFB13D35284F78D7394E60FA2

REBIRTH FEATURE COMPLETION CONFIRMATION
Feature: bounded cooking C# audit and relevant XML/native follow-through
Source cumulative project: current shared zzz_REBIRTH__3_0 workspace
Target project/version: REBIRTH 3.0; installed native hash identified above
Design requirements reviewed: PASS
Relevant existing documentation reviewed: PASS
Accepted architecture preserved: PASS
Code requirements: N/A (audit only; all34 cooking source files read)
XML requirements: N/A (audit only; exact node/full-file limits documented)
UI requirements: N/A (audit only)
Localization requirements: N/A
Images/assets requirements: N/A
Serialization/persistence requirements: N/A (evidence recorded, corrective implementation pending)
Networking/synchronization requirements: N/A (evidence recorded, runtime qualification pending)
Documentation requirements: PASS
Packaging requirements: N/A (delegated trace only)
Compilation: Not performed
In-game validation: Not performed
COMPLETION VERDICT: COMPLETE for bounded all-C# audit evidence only
Basis for verdict: Source reads, installer/owner/native/recipe and conservation chains captured with hashes. Whole cooking XML/game/multiplayer and corrective migration work remain NOT COMPLETE, as explicitly scoped above.
