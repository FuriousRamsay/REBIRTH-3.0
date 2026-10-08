#!/usr/bin/env python3
"""PC130 source/XML invariants and isolated Python behavior models.
These tests do NOT compile C# or execute the game/NGUI. Run against PC129 + PC130.
"""
from pathlib import Path
import argparse, hashlib, math, re, struct, unittest
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[2]
UI='Scripts/Crafting/UI/PersonalCrafting/'
PROTECTED={'Config/XUi_InGame/windows.xml': '008ebbb7b7b707d6dbce8288805841a727ffe81280b00b16cc14c5f6d5c7f9a0', 'Config/XUi_InGame/templates.xml': '63cdaafbb70826628920748a558f807161e9863f17b61085ba09979e906f400c', 'Scripts/Input/RebirthNativeControls.cs': 'a58544b8423d7207385fe0231f00c94524d0e0b0170e2aa794f0c82cd52988fb', 'Scripts/Vehicles/RebirthHornDoorService.cs': '5f78b7a92bb4b3f0445a26ba98fcbac88c9f4c88e19d1e42df7667014b074359', 'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs': '1f02d3080fe7045192d0ffee36c4fb81e9db4e5dd3019bab0a6320aabbd17fa6', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventorySlot.cs': '8a71599f4ba1630a796356c6776f29694efb2f62051932a939f1bd8fe94661f6', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs': 'a234c973283237cf2db409bfaf8d5447db5a7c90eff8992ced482712568da5a0', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingOutcome.cs': '9a038bc3e4856d01b1cb6acd680e43fca2b4b842639fafee50dc242a42c71969', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemContext.cs': '85e5aca2227fce1601baa6ec2361c1bd0d15becd671ad718d6b5ac841275eb2c', 'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingHudSuppressionInstaller.cs': '1b2b3eb684c3eaf719db755c1517398f24b88b01c2dc85db2b9b4bc68e42dc3b', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs': 'd96c2eedbc00b7e7c273f4e752f8c783c3b496f4c15483becb73f9ca96283887', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingTopTabs.cs': '8b9b5a9663856a4270e8c28f6507954c2d4152e6fe109d2f2c3a78147fe524b9', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingActions.cs': 'f0d0010737e9d7736d3e611f861a1ab37831312a0c657563afc4b9131fad83a6', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemActionEntry.cs': '06bce4c36f6269415ee11cb8d0edefc218ec177c741ef80056d5d9fd88b84ffb', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueue.cs': '94002b38231c262709e8abe05429dbd7ef7c6615e0a6aacd2dd57d5790579434', 'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutAudit.cs': 'bcdc15099aecbef37998784388f9d90ae87617e537d82fd28093c53cbf31a92d', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueueEntry.cs': '7ac21c9b010d0839e71ca57b1520ce3e48fa68b7c893b262dcf52fee082e208b', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs': 'e4096e233462578647f55d5d4b82509c6d1dd33d31561fc13ecc6f7c827575e9', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryBridge.cs': '856f15c719724029ff17ea45ddeabc4fe9d4442190f78948349cdf89331fb3eb', 'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingNavigationService.cs': '17046eaf66cf6c43a7453e4dd5fba9fb599358f64bb61a59afd453a2fcb40a04', 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeEntry.cs': '7ecf5c1c13f804d6b9430fb95fc7d1e1b6a214dc6a812f028fb31c8380e3acd2', 'Scripts/UI/Console/RebirthConsolePopupSuppression.cs': '19883ae36c3635d3838b8f161d981dc14625bd98fa0f57fe91c1661d1bf4b392', 'Scripts/Survivor/UI/RebirthSurvivorFirstEntryUiService.cs': 'a3a9c2950245ec0ab5faedd600f55525501ca6406935e173120010a40417b4f6'}
LAYOUT_HASH='3745214cb7e5ea712ea5f7ec29446941c11a2ac6eb5ba5ede2b1c0d3cdd401e1'
LAYOUT_INSERTION='        // PC130: apply the session setting on initialization/open/explicit surface changes.\n        // The command updates an already-open window directly. Ordinary layout checks do\n        // not poll opacity or traverse the UI; unchanged colors are not rewritten.\n        if (force)\n            RebirthPersonalCraftingPanelOpacity.ApplyTo(owner);\n\n'
TARGETS=('rebirthCraftingLeftBackground','rebirthCraftingCenterBackground','rebirthCraftingRightBackground')

def f32(n): return struct.unpack('f',struct.pack('f',n))[0]

class OpacityModel:
    """Independent numeric/whitelist model; not a substitute for running the C# code."""
    def __init__(self): self.reset()
    def reset(self): self.percent=217*100/255; self.enabled=False
    @property
    def alpha(self): return f32(self.percent/100) if self.enabled else f32(217/255)
    def set(self,text):
        value=('' if text is None else text).strip()
        if value.endswith('%'): value=value[:-1].strip()
        if not re.fullmatch(r'[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?',value): return False
        n=float(value)
        if not math.isfinite(n) or not 0 <= n <= 100: return False
        self.percent=n; self.enabled=True; return True
    def apply(self,views):
        matched=changed=0
        for name in TARGETS:
            if name not in views or views[name] is None: continue
            matched+=1
            if views[name][3] == self.alpha: continue
            views[name]=views[name][:3]+(self.alpha,); changed+=1
        return matched,changed

class PC130Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.service=(ROOT/UI/'RebirthPersonalCraftingPanelOpacity.cs').read_text(encoding='utf-8-sig')
        cls.command=(ROOT/UI/'ConsoleCmdRebirthUiOpacity.cs').read_text(encoding='utf-8-sig')
        cls.layout=(ROOT/UI/'RebirthPersonalCraftingLayoutService.cs').read_text(encoding='utf-8-sig')
        cls.window=ET.parse(ROOT/'Config/XUi_InGame/windows.xml').getroot().find(".//window[@name='rebirthPersonalCraftingRoot']")
    def test_01_whitelist_is_exactly_the_three_pc129_panels(self):
        names=re.findall(r'"(rebirthCrafting[A-Za-z]+Background)"',self.service)
        self.assertEqual(list(TARGETS),names)
        for name in TARGETS:
            found=self.window.findall(".//sprite[@name='%s']"%name)
            self.assertEqual(1,len(found)); self.assertEqual('17,17,21,217',found[0].get('color'))
    def test_02_all_preexisting_runtime_files_except_layout_are_byte_identical(self):
        for rel,digest in PROTECTED.items():
            with self.subTest(file=rel): self.assertEqual(digest,hashlib.sha256((ROOT/rel).read_bytes()).hexdigest())
    def test_03_layout_has_only_the_one_explicit_addition(self):
        self.assertEqual(1,self.layout.count(LAYOUT_INSERTION))
        old=self.layout.replace(LAYOUT_INSERTION,'')
        self.assertEqual(LAYOUT_HASH,hashlib.sha256(old.encode()).hexdigest())
    def test_04_opacity_is_not_polled_in_regular_layout_checks(self):
        self.assertIn('if (force)\n            RebirthPersonalCraftingPanelOpacity.ApplyTo(owner);',self.layout)
        self.assertNotIn('void Update(',self.service+self.command)
        self.assertNotIn('HarmonyPatch',self.service+self.command)
    def test_05_setter_preserves_rgb_and_skips_equal_alpha(self):
        for required in ('Color color = sprite.Color;', 'if (color.a == alpha)', 'color.a = alpha;', 'sprite.Color = color;'):
            self.assertIn(required,self.service)
        for forbidden in ('color.r =','color.g =','color.b =','SetAllChildrenDirty(','RefreshBindings(','SetActive(','.Position =','.Size ='):
            self.assertNotIn(forbidden,self.service+self.command)
    def test_06_no_global_fade_or_persistence_writes(self):
        for forbidden in ('GamePrefs.Set','PlayerPrefs.','CanvasGroup','GlobalOpacityModifier =','File.','Directory.','GetComponentsInChildren'):
            self.assertNotIn(forbidden,self.service+self.command)
    def test_07_console_registration_uses_existing_contract(self):
        for required in ('[UnityEngine.Scripting.Preserve]',': ConsoleCmdAbstract','IsExecuteOnClient','AllowedInMainMenu','DefaultPermissionLevel','"rbuiopacity"'):
            self.assertIn(required,self.command)
        for forbidden in ('GUIWindowConsole.ID','PlayerInputManager','IsDormant','InputStyleChanged'):
            self.assertNotIn(forbidden,self.command+self.service)
    def test_08_locality_gate_precedes_mutation(self):
        text=self.command
        for guard in ('!_senderInfo.IsLocalGame','_senderInfo.RemoteClientInfo != null','_senderInfo.NetworkConnection != null','ui == null || ui.entityPlayer == null'):
            self.assertIn(guard,text); self.assertLess(text.index(guard),text.index('TrySetPercent(arg'))
    def test_09_status_is_read_only_and_logs(self):
        start=self.command.index('if (arg.Equals("status"')
        end=self.command.index('bool reset',start)
        block=self.command[start:end]
        self.assertIn('Print(',block)
        for name in ('ApplyTo(', 'TrySetPercent(', 'ResetToDefault('): self.assertNotIn(name,block)
        self.assertIn('SdtdConsole.Instance.Output(text);',self.command)
        self.assertIn('Log.Out(text);',self.command)
    def test_10_mutations_log_replay_and_actual_color_alpha(self):
        for s in ('opacity=','transparency=','alpha=','replay=','source='):
            self.assertIn(s,self.service)
        for s in ('apply=live','apply=next-open','panels=','changed=','persistence=session','WARNING=missing-panel'):
            self.assertIn(s,self.command)
    def test_11_parser_checks_finite_range_before_changing_state(self):
        for s in ('double.TryParse','CultureInfo.InvariantCulture','double.IsNaN(parsed)','double.IsInfinity(parsed)','parsed < 0d','parsed > 100d'):
            self.assertIn(s,self.service)
        self.assertLess(self.service.index('parsed > 100d'),self.service.index('requestedPercent = parsed'))
        self.assertIn('if (count > 1)',self.command)
    def test_12_reset_matches_xml_default_exactly(self):
        self.assertIn('DefaultAlpha = 217f / 255f;',self.service)
        m=OpacityModel(); self.assertEqual(f32(217/255),m.alpha); self.assertFalse(m.enabled)
        m.set('20'); m.reset(); self.assertEqual(f32(217/255),m.alpha); self.assertFalse(m.enabled)
    def test_13_numeric_model_accepts_percentages(self):
        for token,expected in [('0',0),('1',1),('0.75',.75),('70',70),('72.5',72.5),('100',100),(' 75% ',75),('85.098',85.098),('.5',.5)]:
            with self.subTest(token=token):
                m=OpacityModel(); self.assertTrue(m.set(token)); self.assertEqual(expected,m.percent)
                self.assertEqual(f32(expected/100),m.alpha)
    def test_14_numeric_model_rejects_invalid_inputs_without_mutation(self):
        for token in [None,'',' ','-1','100.001','NaN','Infinity','-Infinity','1e500','seventy','70%%','72,5','1_0','70 80']:
            with self.subTest(token=token):
                m=OpacityModel(); m.set('60'); previous=(m.enabled,m.percent,m.alpha)
                self.assertFalse(m.set(token)); self.assertEqual(previous,(m.enabled,m.percent,m.alpha))
    def test_15_model_changes_only_background_alphas(self):
        views={n:(.1,.2,.3,.9) for n in TARGETS}
        excluded=['rebirthCraftingTopBackground','rebirthCraftingRootBackground','slot','name','background','queueEntry','recipeEntry','rebirthActionOpaqueFill']
        views.update({n:(.4,.5,.6,1) for n in excluded}); before=views.copy()
        m=OpacityModel(); m.set('40'); self.assertEqual((3,3),m.apply(views))
        for n in TARGETS: self.assertEqual(before[n][:3],views[n][:3]); self.assertEqual(f32(.4),views[n][3])
        for n in excluded: self.assertEqual(before[n],views[n])
    def test_16_same_value_and_repeated_mode_application_do_not_rewrite(self):
        m=OpacityModel();m.set('70');views={n:(.1,.2,.3,.85) for n in TARGETS}
        self.assertEqual((3,3),m.apply(views))
        for i in range(100): self.assertEqual((3,0),m.apply(views))
        m.set('70%'); self.assertEqual((3,0),m.apply(views))
    def test_17_pending_value_applies_to_next_window_and_survives_reopen(self):
        m=OpacityModel(); m.set('60'); self.assertEqual((0,0),m.apply({}))
        for _ in range(5):
            views={n:(.1,.2,.3,f32(217/255)) for n in TARGETS}
            self.assertEqual((3,3),m.apply(views)); self.assertTrue(all(v[3]==f32(.6) for v in views.values()))
    def test_18_missing_view_is_skipped_not_replaced(self):
        m=OpacityModel();m.set('20');views={TARGETS[0]:(.1,.2,.3,.9),TARGETS[1]:None}
        self.assertEqual((1,1),m.apply(views)); self.assertEqual(2,len(views))
    def test_19_zero_and_full_endpoints_do_not_remove_controls(self):
        for value,alpha in [('0',0),('100',1)]:
            views={n:(.1,.2,.3,.85) for n in TARGETS};views['button']=(.4,.5,.6,1)
            m=OpacityModel();m.set(value);m.apply(views)
            self.assertEqual(4,len(views));self.assertEqual((.4,.5,.6,1),views['button'])
            self.assertTrue(all(views[n][3]==alpha for n in TARGETS))
    def test_20_default_does_not_accumulate_layer_opacity(self):
        m=OpacityModel(); self.assertAlmostEqual(38/255,1-m.alpha,places=6)
        for name in ('rebirthCraftingRootBackground','rebirthCraftingInventoryRegionBg','rebirthCraftingQueueRegionBg','rebirthCraftingItemContextBg'):
            node=self.window.find(".//*[@name='%s']"%name)
            self.assertEqual('0',node.get('color').split(',')[-1])

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('project_root',nargs='?',type=Path,default=ROOT)
    args=parser.parse_args();ROOT=args.project_root.resolve()
    unittest.main(argv=['pc130'],verbosity=2)
