# Final contradiction review — current MP001–088

Audit-only targeted acceptance review, 2026-10-15. No production changes, build, game, tests or Git operations. This is a targeted contradiction check against current evidence, not a new claim of rereading all 1,805 bodies. Prior full-body manifests and bounded native/XML closures remain the provenance for unrelated findings.

## Required correction: my later MP069 wording inverted the affected mode

The main report is correct: MP069 affects remote **REBIRTH** mode. RebirthCamperCharacterAccess.Resolve uses ResolveLocal for remote native progression, but its REBIRTH branch requires authoritative WorldCharacterService/current cached repository ownership. My CAMPER_XML_REACHABILITY_CLOSURE_AGENT_TRACE.md and CAMPER_IMPLEMENTATION_WORKPACKAGE_AGENT_DRAFT.md used remote native-mode wording incorrectly; this review supersedes that wording. The original physical trace and current main MP069 need no reversal. Workpackage 1 must repair the remote REBIRTH branch while preserving the existing native-mode remote resolver. Verify both modes as controls. This was my prose error, not evidence of a production fix or mode regression.

## MP087: management inspector only; Energy quality bindings already exist

Current main title/body identify the management selected inspector, which is supported. Config/_Vehicles/camper_modules_windows.xml:145–180 authors plain-rect count, hidden durability/quality and empty stat nodes. XUiC_RebirthCamperModules.Inspect:156 updates name, description, icon and capacity; the selected stat/count/durability node names have no controller consumer in current Scripts search. Native backpack overlays do not populate this selected rect. Keep medium functional/UI scope, with no inventory conservation defect inferred.

Do not translate the report's white enlarged bottom quality requirement into a claim that Energy engine/component quality rendering is unimplemented. Config/_Vehicles/camper_energy_windows.xml:145–147 already binds durability fill/color and partquality, white size30 centered over the bottom bar; repeated component slots use the same bindings. XUiC_RebirthCamperEnergySlot:353 inherits native XUiC_BasePartStack; Present:377 clones the observed stack into ItemStack and refreshes bindings. A fresh read-only native extract confirms BasePartStack.GetBindingValueInternal returns partquality from itemStack.itemValue.Quality, partvisible from itemClass, partfill from remaining MaxUseTimes fraction (or1 when zero), and partcolor separately. No missing inherited binding is established. Rendering overlap/quality aesthetics remain NOT RUN. Management inspector and Energy component slots have different controller ownership and should remain separate acceptance cases.

## MP086: native progression omission remains independent

Config/XUi_InGame/templates.xml:1171 includes station_templates.xml only under character_progression('Rebirth'). That file defines rebirth_camper_station_navigation, while unconditional camper station picker/switch windows reference it (picker312; switch5/8). Prior exact native XUiFromXml closure shows missing template becomes an empty view, with diagnostic behavior conditional; do not claim the entire UI crashes. Native progression navigation omissions are supported on host as well as remote; this is independent of remote REBIRTH identity MP069. Avoid widening all Survivor station templates to native mode merely to publish the camper navigation definition.

## MP088 and dormant/fixed classifications

The current report correctly qualifies cached farming unload eligibility as lifecycle/conservation risk, not demonstrated persistent duplication. NATIVE_FARMING_PATH_CLOSURE_AGENT_TRACE.md proves OnUnload does not invoke custom OnDestroy unregistration, retained GetChunk passes cached liveness, and actual water consumers can select that stale provider. World reset clears it. MP034 missing-neighbor Air/open-column path is now native-source closed; stored SUN still applies. Ordinary mutation with a null cache is unestablished and must remain excluded.

Current report explicitly withdraws dormant profile preview/crop candidates and retains the independently active Creator Apply hotkey MP082. Tool guards are actively installed through SurvivorProgressionInstaller33–35; no missing-installer defect should be resurrected. Native dog melee inheritance is closed by NPC_NATIVE_CONTENT_CLOSURE_AGENT_TRACE.md, while missing ordinary NPC ambient producer/adapters remain unavailable orchestration rather than an installed population flow. Actual explicitly spawned actors are not globally dormant.

## Acceptance timing

AUDIT_CLOSURE_ACCEPTANCE_AGENT_REVIEW.md's five followups are historical outstanding boundaries at its timestamp. Subsequent RECIPE_TOOL_SELECTOR_CLOSURE_AGENT_TRACE.md, NPC_NATIVE_CONTENT_CLOSURE_AGENT_TRACE.md and NATIVE_FARMING_PATH_CLOSURE_AGENT_TRACE.md close the corresponding source questions or explicitly classify unavailable/unestablished paths. Do not present those five as still-unread native source merely because the older acceptance document lists them. The current main coverage claim is explicitly checkpoint-bound; runtime multiplayer/visual/fault verification is not implied by full source coverage.

No additional defect number is requested. MP086–088 remain supported with these exact scopes. The actionable contradiction is in my earlier mode wording, not the current main MP069. MP087 implementation ownership should explicitly distinguish management inspector from already-bound Energy component quality display.

## Current anchor hashes — bounded review, not new full-body credit

- _Documentation/MultiplayerAudit/ProjectWide_20261014/AUDIT_REPORT.md — D3368E2CBF9700BAE838F72855C6D1D43479C665AF7333EB0F806AF46AF6E0FD
- Scripts/Vehicles/Camper/RebirthCamperCharacterAccess.cs — 65DE481712E712E5EBAA5EEF178AA4470795C243C3FD5C308A95589F2AFF773F
- Scripts/Vehicles/Camper/RebirthCamperCharacterScope.cs — AE5F0A86F72F63609AD13E6E1FE4B76543ADEA0251304B1EDABEB5EEB685A400
- Scripts/Vehicles/Camper/XUiC_RebirthCamperModules.cs — 4EAFDA31DC00CC86F6890C952FC2E244BB192446864C9E17BE82A92B1BD3A1F2
- Scripts/Vehicles/Camper/Energy/XUiC_RebirthCamperEnergy.cs — F63C58BB11098930C481003AB3974DAC0B9DBF02F272BA591E357BD90C5DEE32
- Config/_Vehicles/camper_modules_windows.xml — 7900A961D85CB3E70BD1E5ECAEF912C18E2CB8244550E7F5C8AE017675669874
- Config/_Vehicles/camper_energy_windows.xml — B1F627665494F71E90F5010009D38D0A778DF1934D12F5486D1078C53B0EC865
- Config/XUi_InGame/templates.xml — D8DE7A0898691F1380C36E54240137E6F9A98E692D2C08B93161F4A565C4A417
- Config/XUi_InGame/station_templates.xml — D98607D5A7D5855B83348DD05E6308C485F25F2CEB846119A4DFF6A440F8E33C
- Config/_Vehicles/camper_station_picker_windows.xml — 590A58A1A3B2E62800CCFE7A78DD57F7C57CA87C81FC54AD61BD1D0268187A61
- Config/_Vehicles/camper_station_switch_windows.xml — A57D2E83574E24099D696E051740452822CDAC7D49A43E84ED92180156C5D685

Native Assembly-CSharp SHA256: FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705. Fresh read-only BasePartStack binding extract; no native build.
