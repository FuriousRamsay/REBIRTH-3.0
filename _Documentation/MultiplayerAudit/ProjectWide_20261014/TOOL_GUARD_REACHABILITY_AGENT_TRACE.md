# Required-tool guard reachability correction

2026-10-15. Independent bounded audit; no production/build/game/tests. Compared hauling RECIPE_TOOL_SELECTOR_CLOSURE evidence and prior crafting/native traces before fresh current reads. A negative RecyclerInstaller list alone misses the real registration site. This trace corrects registration classification; it does not certify runtime installation or solve missing ordinary tool wear.

## Current exact registration evidence

Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs33–35 explicitly calls RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthStationToolAvailabilityPatch)), then RebirthStationOpenQueueToolHoldPatch and RebirthStationNativePublicationHoldPatch. Current file SHA256 E7CA3AD69BBAC7E6EB946E6384AE6C8C55CD3BE2AAC03B475EAA7BB97462E745. Read installer1–85 only in this tranche, not a full-body claim. The earlier statement these three are declaration-only/dormant is false for the current source.

Actual production bootstrap: Scripts/Bootstrap/RebirthFresh_Init.cs survivor RunStepOnce calls RebirthSurvivorInstaller.Install(_modInstance); SurvivorInstaller112 runs progression through RunStep. These current bounded caller excerpts establish an ordinary startup path. It is not conditional on character_progression mode in the shown dispatcher; individual guard predicates govern scope. Optional preceding registry/load failures and patch failure can prevent reaching registration; no current loaded Harmony status was observed. Existing retry/install findings remain separate. No reflection scan is needed to explain these classes. RecyclerInstaller indeed installs only its four separate explicit UI types, but is not the sole crafting installer.

## Literal current guard bodies

Fully read Scripts/Crafting/UI/RebirthStationToolAvailability.cs42lines, SHA256 F73A0836EE21C529D16C0F0A3D8D6426FA682254A49487D5222EA61A0639FEC5; and RebirthStationNativePublicationHoldPatch.cs16lines, SHA256 B94D1E3D012F8BBCB0E33DB2A74C3EC15F429CB1C7A01688B499DFEEEE9327DA. Count verified below manifest; these hashes bind full bodies, unlike installer excerpts.

HasRequirement accepts recipe with no craftingToolType; otherwise requires matching nonempty tool item type with maximum<=0 (authored nondurable) or UseTimes<maximum (not broken). It does not degrade any tool. HasRequirementPatch is native XUiC_WorkstationToolGrid.HasRequirement Postfix: only strengthens a true native result, only when WindowGroup.Controller is XUiC_RebirthStationWorkspace. Native false is not turned true.

OpenQueueToolHold Prefix targets XUiC_RecipeStack.Update. It passes noncrafting/null/scrap and non-RebirthStationWorkspace contexts; otherwise tests actual current UI tool grid and stops entire native Update when missing/broken tool. It has no explicit Survivor-mode check. This is availability/progress guard, not ordinary wear, output settlement or a general personal crafting policy.

NativePublicationHold Prefix targets TileEntityWorkstation.HandleRecipeQueue. It first calls ObservationDispatcher.CanProcessNativeQueue in every mode. If allowed and Survivor mode disabled, it passes original. In Survivor mode it examines actual tail row and passes empty/nonpositive/scrap, otherwise requires tool availability against station.Tools. This checks initial active row once per native invocation; ordinary->ordinary catchup row transition inside original while is a separate boundary qualification, not automatically validated per iteration by this prefix. Scrap runtime has distinct dispatch/wear integration.

Direct nonpatch helper call routes also remain active: StationPreparationService.CanUse and CamperStationNativeCrafting.Allowed consult HasRequirement. Therefore even a failed patch install would not make all source-level tool admission checks disappear; actual role/caller reachability must be distinguished.

## Exact native queue contrast

Reused installed native Assembly-CSharp SHA256 FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705 (3.2b10). Fresh bounded literal reads from existing MultiplayerAuditNative20261014 extracts: TileEntityWorkstation.HandleRecipeQueue632–675 and XUiC_RecipeStack.Update301–455. No new decompile/build. These methods alone contain no required-tool availability or required-tool UseTimes writes. Closed queue tests user access/queue/fuel state, decrements tail time and loops completion/output/additional queue cycle with negative-time carryover. Open Update handles full-output retry, native outputStack, multiplier/craft-time updates. XUiC_RecipeStack repair degradation near501 belongs repaired output, not proof of required-tool wear.

ToolGrid native body was absent from existing extract directory; no invented native method/full-read credit. Native recipe admission may independently check tool type in other callers; this review does not assert all native admission lacks it. Runtime exact patch ordering/loaded XML/current recipe type/native stack state remains unexecuted qualification.

## Correction and remaining finding scope

Correct prior declaration-only classification to explicit current startup-installed guards, conditional on successful bootstrap. Preserve documented scope asymmetry: workspace open guard is context scoped; closed tail required-tool guard is Survivor-only; mobile presentation has its own clock/hold boundaries. Do not install duplicates in RecyclerInstaller merely to repair a nonexistent missing-registration defect.

Ordinary degradable required-tool wear remains the existing migration gap: these current bodies only check presence/durability and never mutate wear. Existing recycler productive scrap wear is separate and nondurable ammo/outfit tools intentionally receive no wear. No new finding ID allocated. Per-iteration ordinary queue changes and non-Survivor native required-tool behavior warrant exact caller/native action tests before expanding correction scope; no broad all-mode defect promoted from this bounded check.

```csv
file,sha256,coverageMethod
Scripts/Crafting/UI/RebirthStationToolAvailability.cs,F73A0836EE21C529D16C0F0A3D8D6426FA682254A49487D5222EA61A0639FEC5,FULL_LITERAL_CURRENT_BODY
Scripts/Crafting/UI/RebirthStationNativePublicationHoldPatch.cs,B94D1E3D012F8BBCB0E33DB2A74C3EC15F429CB1C7A01688B499DFEEEE9327DA,FULL_LITERAL_CURRENT_BODY
Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs,E7CA3AD69BBAC7E6EB946E6384AE6C8C55CD3BE2AAC03B475EAA7BB97462E745,SCOPED_REGISTRATION_LINES1_85_ONLY
```

All source/native evidence remains distinct from installed DLL and genuine host/remote gameplay. No tests run and no production changes made.
