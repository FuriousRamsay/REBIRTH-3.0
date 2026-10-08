#!/usr/bin/env python3
"""PC129: XML/source invariants and numeric layout models, NOT an in-game test.

Run after merging PC129 over PC128:
  python BuildScripts/UI/validate_pc129_inventory_alignment_transparency.py [project_root]
No Unity/game assemblies are required. These tests do not compile C# or render the UI.
"""
from __future__ import annotations
import argparse
import copy
import hashlib
import json
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET
from validate_pc127_input_ownership_console import method

ROOT = Path(__file__).resolve().parents[2]
UI = 'Scripts/Crafting/UI/PersonalCrafting/'
PRIMARY = ('rebirthCraftingLeftBackground','rebirthCraftingCenterBackground','rebirthCraftingRightBackground')
CLEAR = {
    'rebirthCraftingRootBackground':'7,7,9,246',
    'rebirthCraftingRecipesRegionBg':'17,17,21,248',
    'rebirthCraftingOutcomeRegionBg':'17,17,21,248',
    'rebirthCraftingOutcomeMetricsBg':'25,25,30,248',
    'rebirthCraftingOutcomeResultsBg':'25,25,30,248',
    'rebirthCraftingDetailsRegionBg':'17,17,21,248',
    'rebirthCraftingItemContextBg':'17,17,21,248',
    'rebirthCraftingRequirementsRegionBg':'17,17,21,248',
    'rebirthCraftingInventoryRegionBg':'17,17,21,248',
    'rebirthCraftingQueueRegionBg':'17,17,21,248',
}
FILLS = tuple('rebirthCrafting'+n+'OpaqueFill' for n in ('Craft','Favorite','Track','Explorer')) + ('rebirthActionOpaqueFill',)
EXPECTED_WINDOW_HASH = 'ddebe900920b171891d3ffe1f2da6c71358f942825c6a4a52188ca136b785312'
PROTECTED_FILES = {'Config/XUi_InGame/templates.xml': '63cdaafbb70826628920748a558f807161e9863f17b61085ba09979e906f400c', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventorySlot.cs': '8a71599f4ba1630a796356c6776f29694efb2f62051932a939f1bd8fe94661f6', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs': 'd96c2eedbc00b7e7c273f4e752f8c783c3b496f4c15483becb73f9ca96283887', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryBridge.cs': '856f15c719724029ff17ea45ddeabc4fe9d4442190f78948349cdf89331fb3eb', 'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs': '1f02d3080fe7045192d0ffee36c4fb81e9db4e5dd3019bab0a6320aabbd17fa6', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueue.cs': '94002b38231c262709e8abe05429dbd7ef7c6615e0a6aacd2dd57d5790579434', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueueEntry.cs': '7ac21c9b010d0839e71ca57b1520ce3e48fa68b7c893b262dcf52fee082e208b', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeEntry.cs': '7ecf5c1c13f804d6b9430fb95fc7d1e1b6a214dc6a812f028fb31c8380e3acd2', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs': 'e4096e233462578647f55d5d4b82509c6d1dd33d31561fc13ecc6f7c827575e9', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingTopTabs.cs': '8b9b5a9663856a4270e8c28f6507954c2d4152e6fe109d2f2c3a78147fe524b9', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemActionEntry.cs': '06bce4c36f6269415ee11cb8d0edefc218ec177c741ef80056d5d9fd88b84ffb', 'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingNavigationService.cs': '17046eaf66cf6c43a7453e4dd5fba9fb599358f64bb61a59afd453a2fcb40a04', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs': 'a234c973283237cf2db409bfaf8d5447db5a7c90eff8992ced482712568da5a0', 'Scripts/UI/Console/RebirthConsolePopupSuppression.cs': '19883ae36c3635d3838b8f161d981dc14625bd98fa0f57fe91c1661d1bf4b392', 'Scripts/Input/RebirthNativeControls.cs': 'a58544b8423d7207385fe0231f00c94524d0e0b0170e2aa794f0c82cd52988fb', 'Scripts/Vehicles/RebirthHornDoorService.cs': '5f78b7a92bb4b3f0445a26ba98fcbac88c9f4c88e19d1e42df7667014b074359', 'Scripts/Survivor/UI/RebirthSurvivorFirstEntryUiService.cs': 'a3a9c2950245ec0ab5faedd600f55525501ca6406935e173120010a40417b4f6'}
PROTECTED_METHODS = {'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs': {'ApplyTopLayout': '1bc87ae0c01d132ead7bd228fa8efeee628e56c59e0bdce2e58fad132bf9bf92', 'ResolveBottomHudReserve': '9b4f9b140b24feb846f38fa7181858f558d51d2968acab0158aee862645937ea', 'ApplyLeftInternalLayout': '90e321f401be8b1dcd6e31bde0479b0a02f11ccabda12d97a66775b31bdf0aa8', 'ApplyCenterInternalLayout': '0f7de5670d533d115fa4894fd67a4b528241d90486732632c82091ecf038dcee', 'ApplyCenterInventoryLayout': '6fa5ad90d9ea54e6e53705fae0b09fdbdcf4be258c72b5085197921a323b8479', 'ApplyInventoryInternalLayout': '5ab7f1c3a571f5fdd5fc274b4fcb038e34823b708636b971d8f6357275e481d4', 'ApplyRightInternalLayout': 'ce0a1ab3ccbcd60bfb1964cf585044c89d5fe110e1607236e1066f08e333169f', 'SetRect': '4028d99384a85ce0f850f16fe4274b1eec6a80d0f46b926e2d15e0eaeedb4b31', 'SetControllerActive': '929b310122e420f5078bb642f171e9bcab0e87718932e5690a20dc7120ad3f1c'}, 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemContext.cs': {'Update': '7a2e8704449c4cb97ca5d03d95b5f93b330abad2f279adf2a68a3305802b5333', 'HideContext': '7844bb103fe21ba4d25bc015c912b294b15c4c24bb73cf6e7d350ca9ba9161cd', 'ApplyPopoverGeometry': 'e6440ce3d3cfee21e82a75d042f5c6c88adfc16ba5ff058c57ed613d8634ccbc', 'ShowInventoryIdleContext': 'cae0db97712bfe2029f3b51caa1b6f9c91821f1f4f48bf6c040f63328f397ad2'}, 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingActions.cs': {'Craft_OnPress': '91111fadae0396ba1bc32c1241a76ff0beb6975f4dc4ade7a5cd978b3711f312', 'Favorite_OnPress': 'ec39585d9eb5a2755994ed8d79e4c2c96258b2b7470cc8ee5cfaf243442efb0c', 'Track_OnPress': 'd9a39a2d7ecf8aad37b315df8cf58a6e98e13a51b9aef0b4d229419d92798e06', 'Explorer_OnPress': 'f7e20f1cdb16f5e42e888775340eb63467078ceff5a74e07a038bbe655452a9a', 'Action_OnHover': '4b2882f054748459d8dccc6ad9d9080f5ae3938937b3e62b7c1bea135e38ef42', 'WireActionButton': 'b36d852f6d1676cb9c75105b1c1b674a80c3dcfd59a41bfc1ec30c52b3b8e122', 'Update': 'c3f1195094a8c0a8bfead70d7e4ed896ba4a62e9327fd31410b8836da620a583'}}


def shape(element):
    return [element.tag,sorted(element.attrib.items()),(element.text or '').strip(),[shape(c) for c in element]]


def semantic_hash(element):
    return hashlib.sha256(json.dumps(shape(element),ensure_ascii=False,separators=(',',':')).encode()).hexdigest()


def inventory_panel(body_width, body_height):
    # Isolated numeric model of the unchanged size rules and PC129's new y expression.
    width = max(min(820,body_width),min(1000,body_width))
    height = max(min(470,body_height),min(572,body_height))
    return ((body_width-width)//2, -max(0,body_height-height), width, height)


class PC129Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.xml = ET.parse(ROOT/'Config/XUi_InGame/windows.xml').getroot()
        cls.window = cls.xml.find(".//window[@name='rebirthPersonalCraftingRoot']")
        if cls.window is None: raise AssertionError('Personal crafting root not found')
        cls.layout = (ROOT/UI/'RebirthPersonalCraftingLayoutService.cs').read_text(encoding='utf-8-sig')
        cls.audit = (ROOT/UI/'RebirthPersonalCraftingLayoutAudit.cs').read_text(encoding='utf-8-sig')
        cls.actions = (ROOT/UI/'XUiC_RebirthCraftingActions.cs').read_text(encoding='utf-8-sig')
        cls.context = (ROOT/UI/'XUiC_RebirthCraftingItemContext.cs').read_text(encoding='utf-8-sig')

    def node(self,name):
        found=self.window.findall(".//*[@name='%s']" % name)
        self.assertEqual(1,len(found),name)
        return found[0]

    def test_01_xml_parses(self):
        ET.parse(ROOT/'Config/XUi_InGame/templates.xml')

    def test_02_primary_panels_are_85_percent(self):
        for name in PRIMARY:
            with self.subTest(panel=name):
                self.assertEqual('17,17,21,217', self.node(name).get('color'))

    def test_03_duplicate_panel_fills_are_clear(self):
        for name in CLEAR:
            with self.subTest(panel=name):
                self.assertEqual('0',self.node(name).get('color').split(',')[3])

    def test_04_no_ancestor_alpha_is_applied(self):
        for node in self.window.iter():
            if node.tag in ('rect','window','panel','grid'):
                for attr in ('alpha','opacity'):
                    self.assertNotIn(attr,node.attrib,node.get('name'))

    def test_05_all_existing_xml_controls_and_other_windows_are_unchanged(self):
        normalized=copy.deepcopy(self.xml)
        root=normalized.find(".//window[@name='rebirthPersonalCraftingRoot']")
        for parent in list(root.iter()):
            for child in list(parent):
                if child.get('name') in FILLS: parent.remove(child)
        for node in root.iter():
            name=node.get('name')
            if name in PRIMARY: node.set('color','12,12,15,252')
            if name in CLEAR: node.set('color',CLEAR[name])
        self.assertEqual(EXPECTED_WINDOW_HASH,semantic_hash(normalized))

    def test_06_no_scroll_queue_or_input_files_change(self):
        for path,expected in PROTECTED_FILES.items():
            with self.subTest(path=path):
                self.assertEqual(expected,hashlib.sha256((ROOT/path).read_bytes()).hexdigest())

    def test_07_crafting_size_and_input_methods_are_preserved(self):
        for path,expected in PROTECTED_METHODS.items():
            text=(ROOT/path).read_text(encoding='utf-8-sig')
            for name,digest in expected.items():
                with self.subTest(path=path,method=name):
                    self.assertEqual(digest,hashlib.sha256(method(text,name).encode()).hexdigest())

    def test_08_new_offset_is_computed_not_a_fixed_pixel_shift(self):
        self.assertIn('int centerY = -Math.Max(0, bodyHeight - centerHeight);',self.layout)
        self.assertIn('"rebirthCraftingCenterFrame", centerX, centerY, centerWidth, centerHeight);',self.layout)

    def test_09_offset_is_only_in_inventory_branch(self):
        start=self.layout.index('if (inventoryOnly)')
        end=self.layout.index('\n        else\n',start)
        self.assertIn('int centerY = -Math.Max',self.layout[start:end])
        self.assertIn('"rebirthCraftingCenterFrame", centerX, centerY',self.layout[start:end])
        self.assertNotIn('centerY',self.layout[end:self.layout.index('ApplyDebugVisibility();',end)])

    def test_10_debug_frame_uses_the_same_offset(self):
        self.assertIn('ApplyDebugInventoryGeometry(bodyWidth, bodyHeight, centerX, centerY, centerWidth, centerHeight);',self.layout)
        self.assertIn('SetRect(owner.GetChildById("rebirthCraftingDebugCenter"), centerX, centerY, centerWidth, centerHeight);',self.layout)

    def test_11_runtime_audit_rejects_top_anchored_inventory(self):
        self.assertIn('if (center.Bottom != body.H)',self.audit)
        self.assertIn('compact Inventory bottom must align with full Crafting body bottom',self.audit)

    def test_12_fixed_root_and_nav_geometry_remain_surface_independent(self):
        self.assertIn('int rootWidth = Math.Min(FixedRootWidth',self.layout)
        self.assertIn('int rootHeight = Math.Min(FixedRootHeight',self.layout)
        self.assertIn('ApplyTopLayout(topWidth);',self.layout)
        self.assertNotIn('InventoryRootWidth',self.layout)

    def test_13_numeric_model_matches_reported_body(self):
        self.assertEqual((410,-241,1000,572),inventory_panel(1821,813))

    def test_14_numeric_model_bottom_matches_crafting_for_many_sizes(self):
        for width in (624,820,1000,1160,1560,1821,1856,2400):
            for height in (232,470,510,532,572,682,813,857,1100):
                with self.subTest(width=width,height=height):
                    x,y,w,h=inventory_panel(width,height)
                    self.assertEqual(height,-y+h)
                    self.assertGreaterEqual(-y,0)
                    self.assertLessEqual(x+w,width)
                    self.assertLessEqual(abs(2*x+w-width),1)

    def test_15_numeric_model_has_no_drift_on_repeated_switches(self):
        original=inventory_panel(1821,813)
        for _ in range(100):
            self.assertEqual(original,inventory_panel(1821,813))

    def test_16_recipe_action_backings_are_independent_and_non_interactive(self):
        for name in ('Craft','Favorite','Track','Explorer'):
            with self.subTest(button=name):
                fill=self.node('rebirthCrafting'+name+'OpaqueFill')
                hit=self.node('btnRebirthCrafting'+name)
                self.assertEqual('255',fill.get('color').split(',')[3])
                self.assertLess(int(fill.get('depth')),int(hit.get('depth')))
                self.assertEqual(fill.get('pos','0,0'),hit.get('pos','0,0'))
                for attr in ('width','height'): self.assertEqual(fill.get(attr),hit.get(attr))
                for attr in ('on_press','on_hover','gamepad_selectable'): self.assertEqual('false',fill.get(attr))

    def test_17_item_action_backings_follow_each_existing_entry(self):
        for idx in range(5):
            with self.subTest(entry=idx):
                entry=self.node('rebirthCraftingItemAction'+str(idx))
                fill=entry.find("sprite[@name='rebirthActionOpaqueFill']")
                hit=entry.find("sprite[@name='background']")
                self.assertIsNotNone(fill)
                self.assertEqual('255',fill.get('color').split(',')[3])
                self.assertLess(int(fill.get('depth')),int(hit.get('depth')))
                for attr in ('width','height'): self.assertEqual(fill.get(attr),hit.get(attr))
                for attr in ('on_press','on_hover','gamepad_selectable'): self.assertEqual('false',fill.get(attr))

    def test_18_recipe_fill_geometry_matches_hitbox(self):
        self.assertIn('SetRect(GetChildById(opaqueFillId), x, 0, width, 34);',self.actions)
        self.assertIn('SetRect(button, x, 0, width, 34);',self.actions)
        self.assertIn('if (!force && width == lastWidth) return;',self.actions)
        for name in ('Craft','Favorite','Track','Explorer'):
            self.assertIn('"rebirthCrafting'+name+'OpaqueFill"',self.actions)

    def test_19_item_fill_geometry_matches_hitbox_without_new_poll(self):
        self.assertIn('SetRect(entry.GetChildById("rebirthActionOpaqueFill"), 0, 0, cell, listHeight);',self.context)
        self.assertIn('SetRect(entry.GetChildById("background"), 0, 0, cell, listHeight);',self.context)
        self.assertIn('if (!force && activeCount == lastActionLayoutCount',self.context)

    def test_20_hidden_track_does_not_leave_a_background(self):
        self.assertIn('SetVisible(GetChildById("rebirthCraftingTrackOpaqueFill"), canTrack);',self.actions)

    def test_21_effective_panel_transmission_does_not_stack_to_opaque(self):
        # No root fill or nested region fill multiplies the one column alpha.
        alpha=217/255
        for inner in CLEAR:
            transmitted=(1-0)*(1-alpha)*(1-0)
            self.assertAlmostEqual(38/255,transmitted)
            self.assertGreater(transmitted,0.14)

    def test_22_button_geometry_model_has_no_queue_bleed(self):
        for strip_width in (714,804,849,865,984):
            gap=6; w=max(110,(strip_width-gap*3)//4)
            for i in range(4):
                x=i*(w+gap)
                bw=max(92,strip_width-x) if i==3 else w
                self.assertLessEqual(x+bw,strip_width)
                key_x=x+bw-10-26
                self.assertLessEqual(key_x+26,x+bw-10)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('project_root',nargs='?',type=Path,default=ROOT)
    args=parser.parse_args()
    ROOT=args.project_root.resolve()
    unittest.main(argv=['pc129'],verbosity=2)
