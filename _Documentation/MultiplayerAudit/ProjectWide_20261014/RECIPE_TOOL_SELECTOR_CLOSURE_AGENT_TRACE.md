# Recipe tool selector closure — 2026-10-15

Audit only; no production/XML edit, build, game, tests or command execution. Compared AUDIT_CLOSURE_ACCEPTANCE_AGENT_REVIEW and CRAFTING_XML_REACHABILITY_CLOSURE_AGENT_TRACE before reading. This closes the previously partial long selector body, not every game-merged recipe or native runtime branch.

## Literal content and include scope

Config/_Workstations/recipe_tools.xml **all91lines** displayed/read literally in complete untruncated output, including both entire124-name workbench Screwdriver predicates and all other selectors. Current SHA256 F0C37E45528A1F48DB49447F1DE423D2646CF2B674D3DC7B40C0D3E280DFF61E matches prior scoped content. New whole-file literal credit supersedes only that prior partial-read limitation, retaining old provenance. Root Config/recipes.xml all64lines also displayed: conditional character_progression('Rebirth') loads _Rebirth/recipes; else _Base/recipes. _Rebirth full7lines includes Survivor/Cooking/gun_repair/industrial_power/electrical_repair; _Base full3lines is empty config. Root then Workstations/recipes, Rebirth-only ammunition_routes, **unconditional recipe_tools**, mortar and Rebirth-only outfit_recipes. Thus recipe_tools runs in both progression branches over whatever recipes exist then. These XML branches are content conditions, not C# Debug/Release.

All17 `<if xpath(...) != null>` and paired setattribute selectors compare identical after XML quote decoding. Every assignment requires exact craft_area and not(@craft_tool). No recipe is created; explicit even-empty craft_tool attributes are not overwritten. If a named recipe is absent/wrong-area/already-explicit, its selector contributes no target. Forge special crucible selection precedes final missing-tool anvil catchall and cannot be overwritten by it. General station selectors are cementMixer→carBattery, circularMachine→CircularSawBlade, forge remaining→Anvil.

Exact named families/counts (253 name entries; not resolved recipe count): chemistry gas/gasBundle/oil3→Beaker; Alchemy Screamer1→HammerPliers; Alchemy mindcontrol/skeleton/potion/spawn14→Beaker; AmmoRecycler16→Pliers; Distiller mineralwater1→CookingPot; Research aura blueprints14→FountainPen; Toolbox station/build/wood20→HammerPliers; Toolbox ammo-machine/bike/decoy8→Screwdriver; chemistry HCL/polymers2→Beaker; forge moltensteel/bulletproof/steelarrowhead3→Crucible; workbench deploy/arrow/turret/wooddoor/pipebomb14→HammerPliers; workbench firearm/blade/melee/parts/nativeweapons/mods/repairkits/turrets/minibike124→Screwdriver; workbench garage/drawbridge/shutter/vault19→HammerPliers; workbench powered traps/relays/sensors/steelcellardoor14→Screwdriver. Exact every named target and declaration qualification follows in table.

## Native field and actual requirement/wear consumers

Retained installed native RecipesFromXml.cs:96–103 read literally: craft_tool resolves through ItemClass.GetItem(...).type into Recipe.craftingToolType and marks ItemClass.bCraftingTool; absent attribute becomes0. Export SHA25641F188A78CF78D0474365F1E5364DD5117365E8BE485BF8A0FB15C13DE08F1FD. Native Assembly-CSharp provenance reused FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705. No new decompilation/native-fullfile credit. Native completion XUiC_RecipeStack.Update and TileEntityWorkstation.HandleRecipeQueue full-body evidence reused from RECYCLING_MIGRATION_TRACE87–107: no ordinary required-tool durability write; repair output UseTimes decrement is a separate native repair action, not general crafting wear.

Current RebirthStationToolAvailability.cs all42lines read: actual helper requires exact craftingToolType and nonempty tool; MaxUseTimes<=0 accepts nondurable presence, else UseTimes<MaxUseTimes. SHA F73A0836EE21C529D16C0F0A3D8D6426FA682254A49487D5222EA61A0639FEC5. Active Camper NativeCrafting.Allowed:55 calls helper before Plan; recipe cloning retains craftingToolType:147/191; Picker115 uses it for presentation. These are actual active direct consumers under their existing owner/authority routes, not install inference from attributes.

**Corrected registration evidence:** RebirthSurvivorProgressionInstaller.cs33–35 explicitly calls PatchClassOnce for ToolAvailabilityPatch, OpenQueueToolHoldPatch and NativePublicationHoldPatch. Fresh_Init78→SurvivorInstaller112→ProgressionInstaller is the actual route. Earlier crafting-only installer searches missed this cross-domain registration; the temporary dormant assessment was wrong and is withdrawn. Existing prior crafting trace installed wording is supported. NativePublicationHoldPatch entire16lines SHA B94D1E3D012F8BBCB0E33DB2A74C3EC15F429CB1C7A01688B499DFEEEE9327DA checks publication custody then Survivor-mode active ordinary row tool availability. ToolAvailabilityPatch narrows native grid success for REBIRTH workspace; OpenQueueToolHold stops that workspace's active non-scrap Update when helper refuses. Installation is source-reachable subject to earlier installer failure, not a runtime hook-success claim. None applies ordinary tool wear.
RecyclerBonuses WearTargets/MaxProcessingSeconds/Healthy/ApplyWear current scoped bodies read again: nondurable/exhausted targets do not share wear, presence remains mandatory at zero rate, rate/seconds finite, detached clone accumulates bounded UseTimes. Runtime.Process invokes productive-seconds limit. This is scrap-rule RequiredTools, distinct from XML ordinary craft_tool. Earlier complete recycling/native evidence retained; no new whole bonuses/runtime credit. Existing ordinary wear migration gap remains; do not add wear to nondurable Pliers/HammerPliers/Screwdriver/chart or claim pen control active if its aura recipe is absent.

## Target existence qualification

Static exact literal recipe declaration scan across current Config XML plus installed vanilla Data/Config/recipes.xml selected aliases below. This scan is **not** full merged XML evaluation: positive names may be removed, conditional, different-area or overridden; negative declaration matches may be introduced by another mod or transformed dynamically. Such names are inert in this patch unless matching recipe actually exists. No historical absent alias is automatically migrated/recreated. The complete file literal read does not turn selector253 into253 live recipes.

Static name-entry declarations: 253; without any current literal declaration: 139. Table preserves exact names and area, including unresolved historical aliases.

| Area | Tool | Exact recipe name | Mod declaration | Native declaration |
|---|---|---|---|---|
| chemistryStation | toolBeaker | ammoGasCan | True | True |
| chemistryStation | toolBeaker | ammoGasCanBundle | False | True |
| chemistryStation | toolBeaker | resourceOil | True | True |
| AlchemyTable001_FR | FuriousRamsayHammerPliers | FuriousRamsayScreamerSignalTier1 | False | False |
| AlchemyTable001_FR | toolBeaker | FuriousRamsayMindControlDrink | False | False |
| AlchemyTable001_FR | toolBeaker | FuriousRamsayMindControlPotion | False | False |
| AlchemyTable001_FR | toolBeaker | FuriousRamsayPotionBottleSkeleton1 | False | False |
| AlchemyTable001_FR | toolBeaker | FuriousRamsayPotionBottleSkeleton2 | False | False |
| AlchemyTable001_FR | toolBeaker | FuriousRamsayPotionBottleSkeleton3 | False | False |
| AlchemyTable001_FR | toolBeaker | FuriousRamsayPotionBottleSkeleton4 | False | False |
| AlchemyTable001_FR | toolBeaker | FuriousRamsayPotionBottleSkeleton5 | False | False |
| AlchemyTable001_FR | toolBeaker | FuriousRamsayPotionBottleSkeletonArmy | False | False |
| AlchemyTable001_FR | toolBeaker | FuriousRamsaySpawnCubeSkeleton1_FR | False | False |
| AlchemyTable001_FR | toolBeaker | FuriousRamsaySpawnCubeSkeleton2_FR | False | False |
| AlchemyTable001_FR | toolBeaker | FuriousRamsaySpawnCubeSkeleton3_FR | False | False |
| AlchemyTable001_FR | toolBeaker | FuriousRamsaySpawnCubeSkeleton4_FR | False | False |
| AlchemyTable001_FR | toolBeaker | FuriousRamsaySpawnCubeSkeleton5_FR | False | False |
| AlchemyTable001_FR | toolBeaker | FuriousRamsaySpawnCubeZombie007_FR | False | False |
| SonjaAmmoRecyclerStation | FuriousRamsayPliers | 40mmGAmmo | False | False |
| SonjaAmmoRecyclerStation | FuriousRamsayPliers | 40mmGAmmoFrag | False | False |
| SonjaAmmoRecyclerStation | FuriousRamsayPliers | ammo44MagnumBulletAP | True | True |
| SonjaAmmoRecyclerStation | FuriousRamsayPliers | ammo44MagnumBulletHP | True | True |
| SonjaAmmoRecyclerStation | FuriousRamsayPliers | ammo762mmBulletAP | True | True |
| SonjaAmmoRecyclerStation | FuriousRamsayPliers | ammo762mmBulletHP | True | True |
| SonjaAmmoRecyclerStation | FuriousRamsayPliers | ammo9mmBulletAP | True | True |
| SonjaAmmoRecyclerStation | FuriousRamsayPliers | ammo9mmBulletHP | True | True |
| SonjaAmmoRecyclerStation | FuriousRamsayPliers | ammoArrowExploding | True | True |
| SonjaAmmoRecyclerStation | FuriousRamsayPliers | ammoArrowFlaming | True | True |
| SonjaAmmoRecyclerStation | FuriousRamsayPliers | ammoArrowSteelAP | True | True |
| SonjaAmmoRecyclerStation | FuriousRamsayPliers | ammoCrossbowBoltExploding | True | True |
| SonjaAmmoRecyclerStation | FuriousRamsayPliers | ammoCrossbowBoltFlaming | True | True |
| SonjaAmmoRecyclerStation | FuriousRamsayPliers | ammoCrossbowBoltSteelAP | True | True |
| SonjaAmmoRecyclerStation | FuriousRamsayPliers | ammoShotgunBreachingSlug | True | True |
| SonjaAmmoRecyclerStation | FuriousRamsayPliers | ammoShotgunSlug | True | True |
| WorkbenchDistiller001_FR | toolCookingPot | drinkJarPureMineralWater | True | True |
| WorkbenchResearchTable001_FR | FuriousRamsayFountainPen | FuriousRamsayPerkAgilityAuraBlueprint | False | False |
| WorkbenchResearchTable001_FR | FuriousRamsayFountainPen | FuriousRamsayPerkArmorPenetrationAuraBlueprint | False | False |
| WorkbenchResearchTable001_FR | FuriousRamsayFountainPen | FuriousRamsayPerkAttackSpeedAuraBlueprint | False | False |
| WorkbenchResearchTable001_FR | FuriousRamsayFountainPen | FuriousRamsayPerkBlockDamageAuraBlueprint | False | False |
| WorkbenchResearchTable001_FR | FuriousRamsayFountainPen | FuriousRamsayPerkFireRateAuraBlueprint | False | False |
| WorkbenchResearchTable001_FR | FuriousRamsayFountainPen | FuriousRamsayPerkFortuityAuraBlueprint | False | False |
| WorkbenchResearchTable001_FR | FuriousRamsayFountainPen | FuriousRamsayPerkHealingAuraBlueprint | False | False |
| WorkbenchResearchTable001_FR | FuriousRamsayFountainPen | FuriousRamsayPerkMobilityAuraBlueprint | False | False |
| WorkbenchResearchTable001_FR | FuriousRamsayFountainPen | FuriousRamsayPerkPierceAuraBlueprint | False | False |
| WorkbenchResearchTable001_FR | FuriousRamsayFountainPen | FuriousRamsayPerkShadowAuraBlueprint | False | False |
| WorkbenchResearchTable001_FR | FuriousRamsayFountainPen | FuriousRamsayPerkStabilityAuraBlueprint | False | False |
| WorkbenchResearchTable001_FR | FuriousRamsayFountainPen | FuriousRamsayPerkStaminaAuraBlueprint | False | False |
| WorkbenchResearchTable001_FR | FuriousRamsayFountainPen | FuriousRamsayPerkTradeAuraBlueprint | False | False |
| WorkbenchResearchTable001_FR | FuriousRamsayFountainPen | FuriousRamsayPerkVitalityAuraBlueprint | False | False |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | AlchemyTable001_FR | False | False |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | FuriousRamsayBatteryBank | True | False |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | FuriousRamsayBucket001 | False | False |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | FuriousRamsayChickenCoop000 | False | False |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | FuriousRamsayDewCollector | False | False |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | FuriousRamsayGeneratorBank | True | False |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | FuriousRamsaySolarBank | False | False |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | ModifierStation | True | False |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | WorkbenchCircularMachine001_FR | True | False |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | WorkbenchDistiller001_FR | True | False |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | WorkbenchGasStove001_FR | True | False |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | WorkbenchIronOven001_FR | True | False |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | WorkbenchResearchTable001_FR | True | False |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | WorkbenchShredder001_FR | True | False |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | cementMixer | True | True |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | chemistryStation | True | True |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | cntApiary | True | True |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | ladderWood | False | True |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | trapSpikesWoodDmg0 | True | True |
| WorkbenchToolbox001_FR | FuriousRamsayHammerPliers | workbench | True | True |
| WorkbenchToolbox001_FR | FuriousRamsayScrewdriver | FuriousRamsayAmmoMachine | False | False |
| WorkbenchToolbox001_FR | FuriousRamsayScrewdriver | FuriousRamsayBikeRepairKit | True | False |
| WorkbenchToolbox001_FR | FuriousRamsayScrewdriver | FuriousRamsaySpawnBlockDecoyTier1 | False | False |
| WorkbenchToolbox001_FR | FuriousRamsayScrewdriver | FuriousRamsaySpawnBlockDecoyTier2 | False | False |
| WorkbenchToolbox001_FR | FuriousRamsayScrewdriver | FuriousRamsaySpawnBlockDecoyTier3 | False | False |
| WorkbenchToolbox001_FR | FuriousRamsayScrewdriver | FuriousRamsaySpawnBlockDecoyTier4 | False | False |
| WorkbenchToolbox001_FR | FuriousRamsayScrewdriver | FuriousRamsaySpawnBlockDecoyTier5 | False | False |
| WorkbenchToolbox001_FR | FuriousRamsayScrewdriver | RepairableBicycle_FR | False | False |
| chemistryStation | toolBeaker | FuriousRamsayHCL | False | False |
| chemistryStation | toolBeaker | resourceScrapPolymers | False | True |
| forge | toolForgeCrucible | FuriousRamsayMoltenSteel | False | False |
| forge | toolForgeCrucible | glassBulletproofBlockVariantHelper | True | True |
| forge | toolForgeCrucible | resourceArrowHeadSteelAP | False | True |
| workbench | FuriousRamsayHammerPliers | FuriousRamsayDeployDefensePillar | False | False |
| workbench | FuriousRamsayHammerPliers | FuriousRamsayDeployDefensePillarConcrete | False | False |
| workbench | FuriousRamsayHammerPliers | FuriousRamsayDeployMine | False | False |
| workbench | FuriousRamsayHammerPliers | FuriousRamsayDeployMineCobble | False | False |
| workbench | FuriousRamsayHammerPliers | FuriousRamsayDeployShaftExtension | False | False |
| workbench | FuriousRamsayHammerPliers | FuriousRamsayDeployShaftExtensionCobble | False | False |
| workbench | FuriousRamsayHammerPliers | FuriousRamsayLootGatherer | False | False |
| workbench | FuriousRamsayHammerPliers | ammoArrowIron | True | True |
| workbench | FuriousRamsayHammerPliers | ammoCrossbowBoltIron | True | True |
| workbench | FuriousRamsayHammerPliers | ammoJunkTurretRegular | True | True |
| workbench | FuriousRamsayHammerPliers | frameShapes:VariantHelper | False | True |
| workbench | FuriousRamsayHammerPliers | modGunMeleeBlessedMetal | False | False |
| workbench | FuriousRamsayHammerPliers | oldWoodDoorDouble | True | True |
| workbench | FuriousRamsayHammerPliers | thrownAmmoPipeBomb | True | True |
| workbench | FuriousRamsayScrewdriver | AssaultRifle01_FR | False | False |
| workbench | FuriousRamsayScrewdriver | AssaultRifle02_FR | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayElectricalRepairKit | True | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayGunRepairKit | True | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayKnife001_FR | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayKnife002_FR | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayKnife003_FR | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayKnife004_FR | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayKnife005_FR | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayRangedWeaponPartBarrel1 | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayRangedWeaponPartBarrel2 | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayRangedWeaponPartBarrel3 | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayRangedWeaponPartBow1 | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayRangedWeaponPartBow2 | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayRangedWeaponPartExplosives1 | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayRangedWeaponPartExplosives2 | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayRangedWeaponPartHandle1 | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayRangedWeaponPartHandle2 | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayRangedWeaponPartHandle3 | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayRangedWeaponPartMagazine1 | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayRangedWeaponPartMagazine2 | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayRangedWeaponPartMagazine3 | False | False |
| workbench | FuriousRamsayScrewdriver | FuriousRamsayShockwaveGrenade | False | False |
| workbench | FuriousRamsayScrewdriver | GrenadeLauncher01_FR | False | False |
| workbench | FuriousRamsayScrewdriver | HeavyPistol01_FR | False | False |
| workbench | FuriousRamsayScrewdriver | HeavyPistol02_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsAxe001_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsAxe002_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsAxe003_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsAxe004_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsAxe005_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsBaton001_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsBaton002_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsCleaver001_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsClub001_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsClub002_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsClub003_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsClub004_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsClub005_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsClub006_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsClub008_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsClub009_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsClub010_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsHammer001_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsHammer002_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsHammer003_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsJunkBaton001_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsKatana001_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsKatana002_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsKatana003_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsKnuckles001_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsKnuckles002_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsKnuckles003_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsSciFiSword001_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsSciFiSword003_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsScythe001_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsScythe002_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsScythe003_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsSledgeHammer001_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsSledgeHammer002_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsSledgeHammer003_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsSpear001_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsSpear002_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsSpear003_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsSpear004_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsSpear005_FR | False | False |
| workbench | FuriousRamsayScrewdriver | ItemsWeaponsSpear006_FR | False | False |
| workbench | FuriousRamsayScrewdriver | MachineGun01_FR | False | False |
| workbench | FuriousRamsayScrewdriver | MachineGun02_FR | False | False |
| workbench | FuriousRamsayScrewdriver | Pistol01_FR | False | False |
| workbench | FuriousRamsayScrewdriver | Pistol02_FR | False | False |
| workbench | FuriousRamsayScrewdriver | Revolver01_FR | False | False |
| workbench | FuriousRamsayScrewdriver | Revolver02_FR | False | False |
| workbench | FuriousRamsayScrewdriver | Shotgun01_FR | False | False |
| workbench | FuriousRamsayScrewdriver | Shotgun02_FR | False | False |
| workbench | FuriousRamsayScrewdriver | SniperRifle01_FR | False | False |
| workbench | FuriousRamsayScrewdriver | SniperRifle02_FR | False | False |
| workbench | FuriousRamsayScrewdriver | SubMachineGun01_FR | False | False |
| workbench | FuriousRamsayScrewdriver | SubMachineGun02_FR | False | False |
| workbench | FuriousRamsayScrewdriver | TacticalAssaultRifle01_FR | False | False |
| workbench | FuriousRamsayScrewdriver | TacticalAssaultRifle02_FR | False | False |
| workbench | FuriousRamsayScrewdriver | autoTurret | True | True |
| workbench | FuriousRamsayScrewdriver | cellarDoorDoubleIron | True | True |
| workbench | FuriousRamsayScrewdriver | cntWoodBurningStove_PickedUp | False | False |
| workbench | FuriousRamsayScrewdriver | gunBowT1IronCrossbow | True | True |
| workbench | FuriousRamsayScrewdriver | gunBowT3CompoundBow | True | True |
| workbench | FuriousRamsayScrewdriver | gunBowT3CompoundCrossbow | True | True |
| workbench | FuriousRamsayScrewdriver | gunExplosivesT3RocketLauncher | False | True |
| workbench | FuriousRamsayScrewdriver | gunHandgunT1Pistol | False | True |
| workbench | FuriousRamsayScrewdriver | gunHandgunT2Magnum44 | False | True |
| workbench | FuriousRamsayScrewdriver | gunHandgunT3DesertVulture | False | True |
| workbench | FuriousRamsayScrewdriver | gunHandgunT3SMG5 | False | True |
| workbench | FuriousRamsayScrewdriver | gunMGT1AK47 | False | True |
| workbench | FuriousRamsayScrewdriver | gunMGT2TacticalAR | False | True |
| workbench | FuriousRamsayScrewdriver | gunMGT3M60 | False | True |
| workbench | FuriousRamsayScrewdriver | gunRifleT1HuntingRifle | False | True |
| workbench | FuriousRamsayScrewdriver | gunRifleT2LeverActionRifle | False | True |
| workbench | FuriousRamsayScrewdriver | gunRifleT3SniperRifle | False | True |
| workbench | FuriousRamsayScrewdriver | gunShotgunT1DoubleBarrel | False | True |
| workbench | FuriousRamsayScrewdriver | gunShotgunT2PumpShotgun | False | True |
| workbench | FuriousRamsayScrewdriver | gunShotgunT3AutoShotgun | False | True |
| workbench | FuriousRamsayScrewdriver | ironDoorBlockVariantHelper | True | True |
| workbench | FuriousRamsayScrewdriver | ironDoorDoubleBlockVariantHelper | True | True |
| workbench | FuriousRamsayScrewdriver | kitAdjustment | False | False |
| workbench | FuriousRamsayScrewdriver | kitBreakdown | False | False |
| workbench | FuriousRamsayScrewdriver | kitEliteReinforcement | False | False |
| workbench | FuriousRamsayScrewdriver | kitRetuning | False | False |
| workbench | FuriousRamsayScrewdriver | kitSurvivorUpgrade | False | False |
| workbench | FuriousRamsayScrewdriver | meleeToolAxeT2SteelAxe | True | True |
| workbench | FuriousRamsayScrewdriver | meleeToolAxeT3Chainsaw | True | True |
| workbench | FuriousRamsayScrewdriver | meleeToolPickT3Auger | True | True |
| workbench | FuriousRamsayScrewdriver | meleeWpnBatonT2StunBaton | True | True |
| workbench | FuriousRamsayScrewdriver | meleeWpnClubT3SteelClub | True | True |
| workbench | FuriousRamsayScrewdriver | meleeWpnKnucklesT3SteelKnuckles | True | True |
| workbench | FuriousRamsayScrewdriver | meleeWpnSledgeT3SteelSledgehammer | True | True |
| workbench | FuriousRamsayScrewdriver | meleeWpnSpearT3SteelSpear | True | True |
| workbench | FuriousRamsayScrewdriver | modArmorHelmetLight | True | True |
| workbench | FuriousRamsayScrewdriver | modGunFlashlight | True | True |
| workbench | FuriousRamsayScrewdriver | radioHam | False | False |
| workbench | FuriousRamsayScrewdriver | resourceRepairKit | True | True |
| workbench | FuriousRamsayScrewdriver | shotgunTurret | True | True |
| workbench | FuriousRamsayScrewdriver | thrownGrenade | True | True |
| workbench | FuriousRamsayScrewdriver | thrownGrenadeContact | True | True |
| workbench | FuriousRamsayScrewdriver | vehicleMinibikePlaceable | True | True |
| workbench | FuriousRamsayHammerPliers | ironGarageDoor_PoweredBlockVariantHelper | True | True |
| workbench | FuriousRamsayHammerPliers | ironGarageDoorBlockVariantHelper | True | True |
| workbench | FuriousRamsayHammerPliers | metalReinforcedWoodDrawBridge | True | True |
| workbench | FuriousRamsayHammerPliers | rollUpDoor3x3VariantHelper | True | True |
| workbench | FuriousRamsayHammerPliers | rollUpDoor5x4VariantHelper | True | True |
| workbench | FuriousRamsayHammerPliers | rollUpDoor7x4VariantHelper | True | True |
| workbench | FuriousRamsayHammerPliers | shuttersSteelBlockVariantHelper | True | True |
| workbench | FuriousRamsayHammerPliers | steelGarageDoor3x3_PoweredBlockVariantHelper | True | True |
| workbench | FuriousRamsayHammerPliers | steelGarageDoor4x3_PoweredBlockVariantHelper | True | True |
| workbench | FuriousRamsayHammerPliers | steelGarageDoor5x3_PoweredBlockVariantHelper | True | True |
| workbench | FuriousRamsayHammerPliers | vaultDoor01 | True | True |
| workbench | FuriousRamsayHammerPliers | vaultDoor01_Powered | True | True |
| workbench | FuriousRamsayHammerPliers | vaultDoor01Double | True | True |
| workbench | FuriousRamsayHammerPliers | vaultDoor01Double_Powered | True | True |
| workbench | FuriousRamsayHammerPliers | vaultHatch01 | True | True |
| workbench | FuriousRamsayHammerPliers | vaultHatch01_Powered | True | True |
| workbench | FuriousRamsayHammerPliers | woodenGarageDoor3x3_PoweredBlockVariantHelper | True | True |
| workbench | FuriousRamsayHammerPliers | woodenGarageDoor4x3_PoweredBlockVariantHelper | True | True |
| workbench | FuriousRamsayHammerPliers | woodenGarageDoor5x3_PoweredBlockVariantHelper | True | True |
| workbench | FuriousRamsayScrewdriver | bladeTrap | True | True |
| workbench | FuriousRamsayScrewdriver | cellarDoorDoubleSteel | True | True |
| workbench | FuriousRamsayScrewdriver | dartTrap | True | True |
| workbench | FuriousRamsayScrewdriver | electricfencepost | True | True |
| workbench | FuriousRamsayScrewdriver | electrictimerrelay | True | True |
| workbench | FuriousRamsayScrewdriver | electricwirerelay | True | True |
| workbench | FuriousRamsayScrewdriver | metalReinforcedWoodDrawBridgePowered | True | True |
| workbench | FuriousRamsayScrewdriver | motionsensor | True | True |
| workbench | FuriousRamsayScrewdriver | pressureplate | True | True |
| workbench | FuriousRamsayScrewdriver | pressureplateLong | True | True |
| workbench | FuriousRamsayScrewdriver | speaker | True | True |
| workbench | FuriousRamsayScrewdriver | spotlightPlayer | True | True |
| workbench | FuriousRamsayScrewdriver | switch | True | True |
| workbench | FuriousRamsayScrewdriver | tripwirepost | True | True |

No merged game table, menu, native wear action, concurrent multiplayer completion or save/reload was executed. Source selector boundary is closed; runtime qualification and existing custody/wear findings remain. Parent should integrate corrected dormant guard classification separately, without changing production here.



## Independent registration correction and productive-time consequence

Audit_metabolism independently identified the explicit cross-domain installer and parent was notified. Current source at SurvivorProgressionInstaller33–35 was read directly and verified, with native closure continuing in TOOL_GUARD_REACHABILITY trace. No claim of dormant guard registration remains. Initial scoped negative search was insufficient; this is an audit correction, not a source change. No defect should be promoted from that withdrawn absence claim.

RebirthHarmonyBootstrap whole body read confirms PatchClassOnce processes the supplied type and no global PatchAll; here the required supplied types exist in Survivor installer. Fixed RecyclerInstaller/Runtime/OutputIntegration lists do not need to repeat their installation. Generic Block-only discovery is irrelevant to this successful explicit route. Publication guard includes original CanProcessNativeQueue before mode-dependent availability. Open-queue guard does not advance productive time on missing/exhausted tools; native availability success is additionally restricted in REBIRTH workspace. Original detached/native held tool images and save receipt custody remain their separate implementation evidence. Helper admission is also active directly in placed PreparationService.CanUse394 and CamperNativeCrafting.Allowed55; Picker115 is presentation. Their presence/gating does not write UseTimes or satisfy ordinary productive-time wear migration. Scrap's own productive-time clamp/clone wear remains separate and nondurable handtools remain intentionally unworn.
Scoped installer current SHA256 E7CA3AD69BBAC7E6EB946E6384AE6C8C55CD3BE2AAC03B475EAA7BB97462E745; only1–48 read in this followup, no new fullfile credit.
