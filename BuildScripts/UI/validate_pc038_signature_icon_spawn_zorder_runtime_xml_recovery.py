#!/usr/bin/env python3
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
checks=[]
def check(ok,label,detail=''):
    checks.append((bool(ok),label,detail))
    print(('PASS' if ok else 'FAIL')+' | '+label+((' | '+detail) if detail else ''))

menu=(ROOT/'Config/XUi_Menu/windows.xml').read_text(encoding='utf-8')
loot=(ROOT/'Config/_Survivor/loot.xml').read_text(encoding='utf-8')
traders=(ROOT/'Config/_Survivor/traders.xml').read_text(encoding='utf-8')
progression=(ROOT/'Scripts/Survivor/Progression/RebirthSurvivorProgressionPatches.cs').read_text(encoding='utf-8')
manager=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs').read_text(encoding='utf-8')
gate=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthSpawnSelectionSurvivorGate.cs').read_text(encoding='utf-8')
ui_text=(ROOT/'Scripts/Survivor/UI/RebirthSurvivorUiText.cs').read_text(encoding='utf-8')

# Signature icon presentation/binding.
check('name="selectedProfileSignatureIcon"' in menu,'Profile Summary authors Signature Bonus icon')
check('atlas="RebirthSurvivorIcons"' in menu[menu.find('name="selectedProfileSignatureIcon"'):menu.find('name="selectedProfileSignatureIcon"')+300], 'Profile Summary Signature Bonus icon uses Survivor icon atlas')
check('selectedSignatureBonusIcon = Sprite("selectedProfileSignatureIcon")' in manager,'Profile manager binds Signature Bonus icon')
check('SetSprite(selectedSignatureBonusIcon, RebirthSurvivorUiText.SignatureBonusIcon(selectedProfile.BackgroundId))' in manager,'Profile manager renders authoritative Background Signature Bonus icon')
check('SetSprite(selectedSignatureBonusIcon, string.Empty)' in manager,'Profile manager clears icon when no profile is selected')
check('public static string SignatureBonusIcon(string backgroundId)' in ui_text,'Central Signature Bonus icon resolver remains authoritative')

# V3.2 window z-order. Ensure the reusable profile selector is above native menu/spawn surfaces,
# while the Creator is above the selector when Create New Profile is used from first-entry flow.
def depth_for(window_name):
    m=re.search(r'<window name="'+re.escape(window_name)+r'"[^>]*\bdepth="(\d+)"',menu)
    return int(m.group(1)) if m else None
profiles_depth=depth_for('rebirthSurvivorProfilesWindow')
creator_depth=depth_for('rebirthSurvivorCreatorWindow')
check(profiles_depth is not None and profiles_depth >= 400,'Survivor Profiles uses explicit high menu-panel depth',str(profiles_depth))
check(creator_depth is not None and profiles_depth is not None and creator_depth > profiles_depth,'Survivor Creator stays above Survivor Profiles',f'profiles={profiles_depth} creator={creator_depth}')

# Queue/open polling must not log/requeue every 250 ms once the selector is queued/showing.
check('StandaloneSpawnSelectorQueuedOrShowing()' in gate,'Spawn gate detects queued/showing standalone selector')
request_block=re.search(r'private void RequestStandaloneSpawnSelector\(string reason\)(.*?)\n    private bool StandaloneSpawnSelectorQueuedOrShowing',gate,re.S)
check(request_block is not None and 'if (StandaloneSpawnSelectorQueuedOrShowing())' in request_block.group(1),'Spawn refresh does not requeue selector already owned by window manager')
check(request_block is not None and 'if (standaloneRedirectPending) return;' in request_block.group(1),'Spawn redirect suppresses duplicate pending requests')

# Current V3.2 BlockModelTree.OnBlockDamaged signature and BlockValueRef position extraction.
expected_attr='[HarmonyPatch(typeof(BlockModelTree), nameof(BlockModelTree.OnBlockDamaged), new System.Type[] { typeof(WorldBase), typeof(BlockValueRef), typeof(BlockValue), typeof(int), typeof(int), typeof(ItemActionAttack.AttackHitInfo), typeof(bool), typeof(bool), typeof(int) })]'
check(expected_attr in progression,'Tree damage Harmony target pins exact V3.2 OnBlockDamaged overload')
patch_block=re.search(r'internal static class RebirthTreeDamageEvidencePatch(.*?)(?:\n}\n)',progression,re.S)
check(patch_block is not None and 'BlockValueRef _bvRef' in patch_block.group(1),'Tree damage Prefix binds V3.2 BlockValueRef argument')
check(patch_block is not None and '_bvRef.TryGetBlockPos(out blockPos)' in patch_block.group(1),'Tree damage derives block position through BlockValueRef')
check(patch_block is not None and 'Vector3i _blockPos' not in patch_block.group(1),'Obsolete V3.1 _blockPos Harmony binding removed')

# Loot group must be inserted before groupNightstand because LootFromXml requires definition-before-reference.
legacy_def='''<insertBefore xpath="/lootcontainers/lootgroup[@name='groupNightstand']">\n    <lootgroup name="rebirthLegacyMusicCassettes"'''
check(legacy_def in loot,'Legacy music cassette loot group inserts before groupNightstand')
check('<append xpath="/lootcontainers">\n    <lootgroup name="rebirthLegacyMusicCassettes"' not in loot,'Late root append for legacy cassette group removed')
check(loot.find('lootgroup name="rebirthLegacyMusicCassettes"') < loot.find('<append xpath="/lootcontainers/lootgroup[@name=\'groupNightstand\']">'),'Legacy cassette definition patch precedes Nightstand reference patch')

# PC025 legacy trader group compatibility: every custom group still referenced has a definition before tier_items.
custom_refs=sorted(set(re.findall(r'<item group="(FuriousRamsay[^"]+)"',traders)))
custom_defs=set(re.findall(r'<trader_item_group name="(FuriousRamsay[^"]+)"',traders))
missing=[x for x in custom_refs if x not in custom_defs]
check(not missing,'All referenced FuriousRamsay trader groups are defined in current V3.2 patch',','.join(missing) if missing else str(len(custom_refs)))
pc025=traders.find('<!-- PC025 / Chunk L:')
compat_idx=traders.find('<!-- PC038: V3.2 compatibility definitions')
check(compat_idx >= 0 and pc025 >= 0 and compat_idx < pc025,'Trader compatibility definitions patch before PC025 tier_items references')
check('FuriousRamsayFarmerAnimals' not in traders,'Empty legacy FarmerAnimals stock group is no longer referenced')
check('vehicleHelicopterPlaceable' not in traders[compat_idx:pc025] if compat_idx>=0 and pc025>=0 else False,'Obsolete helicopter item is not reintroduced by compatibility stock')

# Shutdown hardening for the exact native scroll-view error observed after force close.
update_start=manager.find('public override void Update(float dt)')
update_slice=manager[update_start:update_start+700]
check('xui.playerUI.windowManager == null) return;' in update_slice,'Profile manager exits Update after V3.2 window manager teardown')
check(update_slice.find('windowManager == null) return;') < update_slice.find('base.Update(dt);'),'Shutdown guard runs before native scroll/hotkey base.Update')

# Basic C# balance on changed runtime files.
for rel in [
    'Scripts/Survivor/Progression/RebirthSurvivorProgressionPatches.cs',
    'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs',
    'Scripts/Survivor/UI/XUiC_RebirthSpawnSelectionSurvivorGate.cs']:
    text=(ROOT/rel).read_text(encoding='utf-8')
    check(text.count('{')==text.count('}'),'brace counts balanced: '+Path(rel).name)

xml_count=0
xml_errors=[]
for p in ROOT.rglob('*.xml'):
    try:
        ET.parse(p); xml_count+=1
    except Exception as e:
        xml_errors.append(f'{p.relative_to(ROOT)}: {e}')
check(not xml_errors,'all project XML parses',f'XML={xml_count}' if not xml_errors else '; '.join(xml_errors[:4]))

failed=sum(1 for ok,_,_ in checks if not ok)
print(f'PC038_STATIC_CHECKS={len(checks)} PASSED={len(checks)-failed} FAILED={failed} XML={xml_count}')
sys.exit(1 if failed else 0)
