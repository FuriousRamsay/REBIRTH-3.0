from pathlib import Path
r=Path.cwd()
def edit(f,a,b):
 p=r/f;s=p.read_text(encoding='utf-8-sig');assert a in s,(f,a[:60]);p.write_text(s.replace(a,b),encoding='utf-8')
edit('Config/_Survivor/condition_profiles.xml','implementation_surface="Trait Support exists; heavy-tool/heavy-melee Energy-cost penalty not yet hooked" implementation_state="PARTIAL" />','implementation_surface="Native StaminaLoss for axes/mining tools/heavy melee; spent stamina feeds metabolism Energy cost" implementation_state="IMPLEMENTED"><component target="stamina.heavy_tools" op="add" phase="runtime" value="0.07" /></modifier_profile>')
f='Scripts/Survivor/Progression/RebirthTraitGameplayModifierService.cs'
edit(f,'public float Buy, Sell, Harvest;','public float Buy, Sell, Harvest, HeavyStamina;')
edit(f,'projection.Carry = 0; projection.Buy = projection.Sell = projection.Harvest = 0f;','projection.Carry = 0; projection.Buy = projection.Sell = projection.Harvest = projection.HeavyStamina = 0f;')
edit(f,'projection.Carry = GetUnencumberedSlotDelta(remote.TraitIds);','projection.Carry = GetUnencumberedSlotDelta(remote.TraitIds);\n            projection.HeavyStamina = GetAdditiveRuntimeTotal(remote.TraitIds, "stamina.heavy_tools", 0f, 0.25f);')
edit(f,'projection.Buy = GetBarterBuyingBonus(record);','projection.HeavyStamina = GetAdditiveRuntimeTotal(record.Origin.TraitIds, "stamina.heavy_tools", 0f, 0.25f);\n            projection.Buy = GetBarterBuyingBonus(record);')
edit(f,'SetCVar(player, HarvestCountCVar, projection.Harvest);','SetCVar(player, HarvestCountCVar, projection.Harvest);\n        SetCVar(player, "$rbTraitHeavyStamina", projection.HeavyStamina);')
edit(f,'t=="barter.buying" ||','t=="stamina.heavy_tools" || t=="barter.buying" ||')
edit('Config/buffs.xml','<passive_effect name="HarvestCount" operation="perc_add" value="@$rbSurvivorTraitHarvestCount"/>\n      </effect_group>','<passive_effect name="HarvestCount" operation="perc_add" value="@$rbSurvivorTraitHarvestCount"/>\n      </effect_group>\n      <effect_group>\n        <requirement name="HoldingItemHasTags" tags="axe,miningTool,sledge,heavy"/>\n        <passive_effect name="StaminaLoss" operation="perc_add" value="@$rbTraitHeavyStamina" tags="primary,secondary"/>\n      </effect_group>')
# Explicit authored belt capacity, included in semantic hash; no persistence/protocol change.
f='Scripts/Survivor/Domain/RebirthSurvivorDefinitionModels.cs'
edit(f,'public int GearBagSlotBonus { get; private set; }','public int GearToolbeltSlotBonus { get; private set; }\n    public int GearBagSlotBonus { get; private set; }')
edit(f,'IList<RebirthTraitSupportEffectDefinition> effects)\n    {','IList<RebirthTraitSupportEffectDefinition> effects, int gearToolbeltSlotBonus = 0)\n    {')
edit(f,'GearBagSlotBonus=Math.Max(0,gearBagSlotBonus);','GearToolbeltSlotBonus=Math.Max(0,gearToolbeltSlotBonus);GearBagSlotBonus=Math.Max(0,gearBagSlotBonus);')
