#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
errors=[]
def check(cond,msg):
    if not cond: errors.append(msg)
def text(rel): return (ROOT/rel).read_text(encoding='utf-8')

# All Config XML must remain parseable.
for f in (ROOT/'Config').rglob('*.xml'):
    try: ET.parse(f)
    except Exception as e: errors.append(f'XML parse failed {f.relative_to(ROOT)}: {e}')

# Typed Character Progression condition.
eval_cs=text('Scripts/Options/RebirthXmlPatchConditionEvaluator.cs')
check('character_progression' in eval_cs, 'missing character_progression XML conditional')
check('IsCharacterProgression' in eval_cs, 'missing typed Character Progression resolver')
check('RebirthPlayerProgressionMode.BaseGame' in eval_cs and 'RebirthPlayerProgressionMode.Rebirth' in eval_cs,
      'Character Progression resolver must explicitly support BaseGame and Rebirth')

# Mode-specific routers are explicit and Survivor content is Rebirth-only.
for d in ['Config/_Base','Config/_Rebirth']:
    check((ROOT/d).is_dir(), f'missing mode directory {d}')
for domain in ['buffs.xml','item_modifiers.xml','items.xml','recipes.xml','loot.xml','traders.xml']:
    t=text('Config/'+domain)
    check("character_progression('Rebirth')" in t, f'{domain} missing Rebirth conditional route')
    check('_Rebirth/' in t and '_Base/' in t, f'{domain} must route through both progression folders')
check('_Survivor/items.xml' not in text('Config/items.xml'), 'root items.xml must not bypass progression router to Survivor items')
check('../_Survivor/items.xml' in text('Config/_Rebirth/items.xml'), 'Rebirth items router must delegate to Survivor items')
check('_Survivor' not in text('Config/_Base/items.xml'), 'Base items router must not include Survivor content')

# User's standing scope rule is recorded as project architecture.
doc=text('_Documentation/Architecture/CHARACTER_PROGRESSION_XML_LAYERING_AND_SCOPE_DIRECTIVE.md')
check('If no Character Progression scope is stated' in doc and 'both' in doc.lower(), 'standing shared-by-default scope directive missing')

# Metabolism warning cleanup.
met=text('Config/_Metabolism/items.xml')
check(met.count('<remove ') == 85, f'expected one still-applicable legacy food/water remove per metabolism item, got {met.count("<remove ")}')
check(met.count("@action='ModifyCVar' and") == 85, 'all still-applicable metabolism legacy removes should combine CVar/ModifyStats in one XPath')
check('game_version() &lt; version(3,2,0)' not in met, 'obsolete pre-3.2 metabolism compatibility gates should not remain in the 3.2 target')

# Survivor 3.2 cleanup.
sitems=text('Config/_Survivor/items.xml')
check('drinkJarBlackStrapCoffee' not in sitems, 'removed 3.2 Blackstrap item still has an obsolete Survivor compatibility patch')
recipes=text('Config/_Survivor/recipes.xml')
check('<output ' not in recipes, 'Survivor recipes must not contain invalid output child elements')
check('WorkbenchGasStove001_FR' not in recipes and 'WorkbenchIronOven001_FR' not in recipes and 'FuriousRamsayBakingPan' not in recipes,
      'Survivor recipes still reference unported 2.6 workstation/tool definitions')
check('craft_area="campfire" craft_tool="toolCookingPot"' in recipes, 'Survivor recipes should use current campfire/cooking-pot path')
check('FuriousRamsayFatChunk' not in recipes and 'game_version() &lt; version(3,2,0)' not in recipes,
      'obsolete pre-3.2 Vegetable Stew compatibility patch should be absent from the 3.2 target')
loot=text('Config/_Survivor/loot.xml')
check('booksAllTiers' not in loot, 'removed 3.2 loot group booksAllTiers still referenced')

# Removed presentation bundles must not be referenced by active XML. Rage gameplay effects remain intact.
buffs=text('Config/buffs.xml')
check('NPCEffects.unity3d' not in buffs and 'FR_Particles.unity3d' not in buffs,
      'active buffs still reference missing legacy presentation bundles')
check('FuriousRamsayTempRageBuff' in buffs and 'DamageModifier' in buffs and 'RunSpeed' in buffs,
      'rage gameplay effects were lost while removing the missing presentation bundle')

# XUi log fixes.
xui=text('Config/XUi_InGame/windows.xml')
check('name="btnRandomize" pos="${Round((parentinnerwidth-200)/2,0)},0"' in xui, 'btnRandomize pos is not a Vector2i')
ring_line=next((line for line in xui.splitlines() if 'name="rebirthLevelRingProgress"' in line), '')
check('filldirection="Horizontal"' in ring_line, 'level ring XML must author a supported fill direction')
compass=text('Scripts/UI/XUiC_RebirthCompassWindow.cs')
check('UIBasicSprite.FillDirection.Radial360' in compass, 'runtime compass controller must still apply radial fill')

# Optional lockpick targets must resolve without Harmony warning-producing discovery helpers.
skill=text('Scripts/Survivor/Progression/RebirthSkillWaveAPatches.cs')
check('AccessTools.TypeByName("NetPackageTELock")' not in skill, 'NetPackageTELock still uses noisy AccessTools.TypeByName')
check('AccessTools.TypeByName("BlockSecureLoot")' not in skill, 'BlockSecureLoot still uses noisy AccessTools.TypeByName')
check('AccessTools.Method(typeof(GameManager), "TELockServer"' not in skill, 'TELockServer still uses noisy AccessTools.Method')
check('FindTypeSilently' in skill, 'silent optional-type resolver missing')

# Disabled future vehicle families must not produce runtime missing-part errors or salvage drops.
veh=text('Scripts/Vehicles/Restoration/RebirthVehicleRestorationSystem.cs')
source=text('Scripts/Vehicles/Restoration/RebirthVehicleSourceProfiles.cs')
check('IsFamilyRuntimeEnabled' in veh and 'if (!IsFamilyRuntimeEnabled(family.FamilyId)) continue;' in veh,
      'vehicle runtime binding audit does not skip disabled families')
check('activeFamilies' in source and 'IsFamilyRuntimeEnabled' in source and 'if (activeFamilies.Length == 0) return;' in source,
      'vehicle source profiles do not filter/omit disabled families')
check('bool runtimeEnabled = false;' in source and 'if (!runtimeEnabled) continue;' in source,
      'vehicle part runtime audit does not skip parts belonging only to disabled families')


# Trader shelf downgrade-loop cleanup and expected first-run NPC timeline severity.
trader=text('Config/_Trader/blocks.xml')
check("traderShelvesWoodEmpty" in trader and 'name="param1">Downgrade</setattribute>' in trader and 'DowngradeBlock" value="air"' not in trader,
      'trader terminal/helper shelf variants must exclude inherited Downgrade instead of targeting air')
generator=text('Scripts/BlockPickup/RebirthBlockPickupEmptyVariantGenerator.cs')
check('new XAttribute("param1", "Downgrade")' in generator and 'Property("DowngradeBlock", "air")' not in generator,
      'generated secure player storage must exclude inherited Downgrade on Extends')
npc=text('Scripts/NPC/Persistence/RebirthNpcPersistenceCoordinator.cs')
check('lastDetail = "legacy/untracked sidecars; first v237 checkpoint not written yet";' in npc and
      'Log.Out("[REBIRTH NPC Timeline] " + lastDetail + " worldTime="' in npc,
      'expected first-checkpoint NPC timeline state should be informational, not a warning')

# Completed focused debug trace should be quiet by default.
debug=text('Config/_Survivor/debug.xml')
check('character_progression_ui_logging="false"' in debug, 'Character Progression verbose debug should be disabled by default after successful trace')

# World lock still depends on actual world creation marker, not directory existence.
session=text('Scripts/Options/RebirthSandboxUiSession.cs')
persist=text('Scripts/Options/RebirthSandboxPersistence.cs')
check('CurrentWorldHasStarted()' in session, 'session no longer uses world-start marker for progression lock')
check('main.ttw' in persist, 'world-start persistence check must use main.ttw')

if errors:
    print('RESULT FAIL')
    for e in errors: print('FAIL:',e)
    sys.exit(1)
print('RESULT PASS')
print('character progression XML layering + 3.2 log cleanup audit passed')
