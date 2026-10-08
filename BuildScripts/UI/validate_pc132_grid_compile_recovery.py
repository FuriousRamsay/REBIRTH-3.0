#!/usr/bin/env python3
"""PC132 source-contract checks and INDEPENDENT Python layout models.

Usage: python BuildScripts/UI/validate_pc132_grid_compile_recovery.py [project_root]
Use --patch-only to check just this hotfix payload. Full mode requires PC126-131.
These tests DO NOT compile C#, load game assemblies, or render Unity/NGUI.
They reject the exact unavailable member dependencies reported by the owner,
protect unchanged code by hashes, and exercise deferred-position models.
"""
from pathlib import Path
from dataclasses import dataclass
import argparse, hashlib, json, math, re, struct, unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
FIXTURES = Path(__file__).with_name('pc132_baseline_invariants.json')
UI = 'Scripts/Crafting/UI/PersonalCrafting/'
PATCH_ONLY = False

def mask_noncode(source: str) -> str:
    token = re.compile(r'//[^\n]*|/\*.*?\*/|@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'', re.S)
    return token.sub(lambda m: ''.join('\n' if ch == '\n' else ' ' for ch in m.group()), source)

def method(source: str, name: str) -> str:
    code = mask_noncode(source)
    pattern = (r'\b(?:public|private|internal|protected)\s+'
               r'(?:(?:static|override|virtual|sealed|new|async)\s+)*'
               r'[\w.<>,?\[\]]+\s+' + re.escape(name) + r'\s*\([^)]*\)\s*\{')
    m = re.search(pattern, code, re.S)
    if m is None:
        raise AssertionError('Method not found: ' + name)
    start = m.end() - 1
    depth = 1
    for i in range(start + 1, len(code)):
        depth += (code[i] == '{') - (code[i] == '}')
        if depth == 0:
            return code[start + 1:i]
    raise AssertionError('Unclosed method: ' + name)

def sha(text): return hashlib.sha256(text.encode()).hexdigest()
def f32(n): return struct.unpack('f', struct.pack('f', n))[0]
def clamp(n, a, b): return min(b, max(a, n))

def fit(size):
    # Same preexisting sizing rule: 13 columns, 4 visible rows, 2px cell gap.
    width, height = size
    available_w = max(13 * 36, width - 24 - 4)
    available_h = max(4 * 36, height)
    pitch = max(40, max(1, available_w // 13))
    if pitch * 4 > available_h:
        pitch = max(40, available_h // 4)
    return pitch, max(38, pitch - 2), pitch * 13, min(available_h, pitch * 4)


class SourceContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.fixture=json.loads(FIXTURES.read_text())
        cls.source=(ROOT/cls.fixture['changed_file']).read_text(encoding='utf-8-sig')
        cls.code=mask_noncode(cls.source)
        cls.body=method(cls.source,'RefreshGeometry')

    def test_01_no_unavailable_grid_wrapper_or_data_refresh_calls(self):
        self.assertNotRegex(self.code,r'\.\s*Grid\b')
        self.assertNotRegex(self.code,r'\.\s*UpdateData\s*\(')
        self.assertNotIn('System.Reflection',self.code)
        self.assertNotIn('GetMethod(',self.code)
        self.assertNotIn('HarmonyPatch',self.code)

    def test_02_grid_is_existing_component_with_a_host_aware_cache(self):
        b=method(self.source,'ResolveNativeGrid')
        self.assertIn('host.GetComponent<UIGrid>()',b)
        self.assertIn('nativeGridHost != host || nativeGridComponent == null',b)
        self.assertIn('nativeGridComponent = null;',b)
        self.assertNotIn('AddComponent',self.code)

    def test_03_clip_is_existing_component_with_a_host_aware_cache(self):
        b=method(self.source,'ResolveNativeClip')
        self.assertIn('host.GetComponent<UIPanel>()',b)
        self.assertIn('nativeClipHost != host || nativeClipComponent == null',b)
        self.assertIn('nativeClipComponent = null;',b)

    def test_04_all_geometry_readiness_checks_precede_mutation(self):
        b=self.body
        for term in ('nativeGrid == null','nativeClip == null','slots.Length == 0',
                     'slots[i].ViewComponent.UiTransform == null'):
            self.assertIn(term,b)
            self.assertLess(b.index(term),b.index('grid.Columns ='))

    def test_05_same_dimensions_return_before_component_lookups_and_writes(self):
        b=self.body
        guard='!force && physical == lastPhysical && size.x == lastSize.x && size.y == lastSize.y'
        self.assertIn(guard,b)
        self.assertLess(b.index(guard),b.index('ResolveNativeGrid(grid)'))
        self.assertLess(b.index(guard),b.index('grid.Columns ='))

    def test_06_native_reposition_still_happens_before_cache_commit(self):
        b=self.body
        for term in ('nativeGrid.cellWidth = effectivePitch;',
                     'nativeGrid.cellHeight = effectivePitch;',
                     'nativeGrid.maxPerLine = Columns;'):
            self.assertIn(term,b)
            self.assertLess(b.index(term),b.index('nativeGrid.Reposition();'))
        self.assertEqual(1,b.count('nativeGrid.Reposition();'))
        self.assertLess(b.index('nativeGrid.Reposition();'),b.index('lastPhysical = physical;'))
        self.assertLess(b.index('CommitScrollbarGeometry(thumb);'),b.index('lastSize = size;'))

    def test_07_slot_padding_is_resolved_through_preexisting_position_api(self):
        b=self.body
        terms=('slotView.Position = logicalPosition;', 'slotView.TryUpdatePosition();',
               'Vector3 paddedPosition = slotView.UiTransform.localPosition;',
               'Mathf.RoundToInt(paddedPosition.x) - logicalPosition.x',
               'Mathf.RoundToInt(paddedPosition.y) - logicalPosition.y')
        for term in terms:
            self.assertIn(term,b)
            self.assertLess(b.index(term),b.index('nativeGrid.Reposition();'))
        self.assertIn('slotPositionPadding.Length != slots.Length',b)

    def test_08_native_result_is_mirrored_without_second_grid_layout_calculator(self):
        b=self.body[self.body.index('nativeGrid.Reposition();'):]
        for term in ('Vector3 arrangedPosition = slotView.UiTransform.localPosition;',
                     'Mathf.RoundToInt(arrangedPosition.x) - padding.x',
                     'Mathf.RoundToInt(arrangedPosition.y) - padding.y',
                     'slotView.TryUpdatePosition();'):
            self.assertIn(term,b)
        self.assertNotIn('i % Columns',b)
        self.assertNotIn('i / Columns',b)
        self.assertNotIn('localPosition =',b)

    def test_09_live_and_view_clipping_use_the_same_bounds(self):
        b=self.body
        for term in ('clipView.ClippingSize = new Vector2(viewportWidth, viewportHeight);',
                     'clipView.ClippingCenter = new Vector2(viewportWidth * 0.5f, -viewportHeight * 0.5f);',
                     'new Vector4(viewportWidth * 0.5f, -viewportHeight * 0.5f,',
                     'nativeClip.baseClipRegion = clipRegion;',
                     'nativeClip.clipping = UIDrawCall.Clipping.SoftClip;',
                     'nativeClip.clipSoftness = Vector2.zero;'):
            self.assertIn(term,b)
            self.assertLess(b.index(term),b.index('nativeGrid.Reposition();'))

    def test_10_scrollbar_commit_changes_only_bounds_and_position(self):
        b=method(self.source,'CommitScrollbarGeometry')
        for term in ('view.TryUpdatePosition();','GetComponent<UIWidget>()',
                     'widget.width = size.x;', 'widget.height = size.y;',
                     'GetComponent<BoxCollider>()','collider.center = center;',
                     'collider.size = extent;'):
            self.assertIn(term,b)
        for term in ('Enabled =','enabled =','IsVisible =','RefreshBindings',
                     '.Color', 'OnPress', 'OnDrag','SetActive','Reposition'):
            self.assertNotIn(term,b)

    def test_11_smooth_scroll_input_and_other_existing_methods_are_unchanged(self):
        for name,digest in self.fixture['protected_scroll_methods_sha256'].items():
            with self.subTest(method=name):
                self.assertEqual(digest,sha(method(self.source,name)))

    def test_12_scroll_movement_has_no_layout_culling_or_rebinding(self):
        b=method(self.source,'ApplyGridPosition')
        self.assertIn('inventory.ViewComponent.TryUpdatePosition();',b)
        for term in ('Reposition','RefreshGeometry','localScale','IsVisible =','SetStacks'):
            self.assertNotIn(term,b)
        for term in ('RefreshBindings(', 'SetAllChildrenDirty(', 'SetStacks(', '.Update(0'):
            self.assertNotIn(term,self.body)

    def test_13_debug_trace_reads_actual_component_and_keeps_existing_gate(self):
        b=method(self.source,'TraceState')
        self.assertIn('!RebirthLogSettings.CraftingUiLoggingEnabled',b)
        self.assertIn('UIGrid nativeGrid = ResolveNativeGrid(traceGrid);',b)
        self.assertIn('nativeGrid.cellWidth',b)
        for term in ('nativePitch=', 'lastColumnRight=', 'geometryCommits='):
            self.assertIn(term,self.source)

    def test_14_offset_and_fitting_math_stay_the_same(self):
        for term in ('currentRowOffset = pixelOffset / Math.Max(1, effectivePitch)',
                     'targetRowOffset = targetPixelOffset / Math.Max(1, effectivePitch)',
                     'currentRowOffset * effectivePitch', 'targetRowOffset * effectivePitch',
                     'viewportWidth = effectivePitch * Columns;',
                     'viewportHeight = Math.Min(availableH, effectivePitch * VisibleRows);',
                     'effectiveCell = Math.Max(38, effectivePitch - CellGap);'):
            self.assertIn(term,self.body)
        self.assertIn('ScrollbarTrackWidth = 16;',self.code)
        self.assertIn('ScrollbarThumbWidth = 12;',self.code)

    def test_15_delimiters_and_declared_method_names(self):
        pairs={')':'(',']':'[','}':'{'}; stack=[]
        for c in self.code:
            if c in '([{': stack.append(c)
            elif c in pairs:
                self.assertTrue(stack)
                self.assertEqual(stack.pop(),pairs[c])
        self.assertFalse(stack)
        for name in ('ResolveNativeGrid','ResolveNativeClip','CommitScrollbarGeometry'):
            self.assertTrue(method(self.source,name).strip())

    def test_16_other_pc131_runtime_files_are_byte_identical(self):
        if PATCH_ONLY: self.skipTest('requires merged PC131 project')
        for rel,digest in self.fixture['pc131_runtime_files_sha256'].items():
            with self.subTest(file=rel):
                self.assertEqual(digest,hashlib.sha256((ROOT/rel).read_bytes()).hexdigest())

    def test_17_protected_queue_input_navigation_and_selection_files_are_unchanged(self):
        if PATCH_ONLY: self.skipTest('requires merged PC131 project')
        for rel,digest in self.fixture['protected_files_sha256'].items():
            with self.subTest(file=rel):
                self.assertEqual(digest,hashlib.sha256((ROOT/rel).read_bytes()).hexdigest())

    def test_18_default_and_reset_are_still_98_and_xml_parses(self):
        if PATCH_ONLY: self.skipTest('requires merged PC131 project')
        s=(ROOT/UI/'RebirthPersonalCraftingPanelOpacity.cs').read_text()
        self.assertIn('DefaultAlpha = 0.98f;',s)
        self.assertIn('DefaultPercent = 98d;',s)
        self.assertIn('requestedPercent = DefaultPercent;',method(s,'ResetToDefault'))
        for name in ('windows.xml','templates.xml'):
            ET.parse(ROOT/'Config/XUi_InGame'/name)

@dataclass
class ViewModel:
    logical: tuple=(0,0)
    padding: tuple=(0,0)
    native: tuple=(0,0)
    dirty: bool=True
    scale: float=1.0
    def set_position(self,p):
        self.logical=p; self.dirty=True
    def try_update_position(self):
        if self.dirty:
            self.native=tuple(a+b for a,b in zip(self.logical,self.padding))
    def deferred_flush(self):
        self.try_update_position(); self.dirty=False

class LayoutModel:
    def __init__(self):
        self.slots=[ViewModel(padding=(i%3,-(i%2))) for i in range(104)]
        self.pitch=77; self.cell=75; self.offset=0.; self.target=0.
        self.commits=0; self.cached=None; self.physical=100
        self.viewport_w=1001; self.viewport_h=308; self.live_pitch=77
        self.clip=(0,0,0,0)
    @property
    def max_offset(self):
        return max(0,max(1,(self.physical+12)//13)*self.pitch-self.viewport_h)
    def commit(self,size,physical=100,ready=True,force=False):
        if not force and self.cached==(size,physical):return False
        if not ready:return False
        row=self.offset/self.pitch; targetrow=self.target/self.pitch
        self.pitch,self.cell,self.viewport_w,self.viewport_h=fit(size)
        self.physical=physical
        paddings=[]
        for i,v in enumerate(self.slots):
            logical=v.logical; v.set_position(logical); v.try_update_position()
            paddings.append(tuple(n-l for n,l in zip(v.native,logical)))
            v.scale=self.cell/75 if i<physical else 0
        self.clip=(self.viewport_w*.5,-self.viewport_h*.5,self.viewport_w,self.viewport_h)
        self.offset=clamp(row*self.pitch,0,self.max_offset)
        self.target=clamp(targetrow*self.pitch,0,self.max_offset)
        # INDEPENDENT stand-in for native grid arrangement; production uses UIGrid.Reposition.
        self.live_pitch=self.pitch
        for i,v in enumerate(self.slots):
            if i<physical:v.native=((i%13)*self.pitch,-(i//13)*self.pitch)
        for v,pad in zip(self.slots,paddings):
            desired=tuple(n-p for n,p in zip(v.native,pad))
            if v.logical!=desired:v.set_position(desired)
            v.try_update_position()
        self.cached=(size,physical);self.commits+=1
        return True
    def flush(self):
        for v in self.slots:v.deferred_flush()
    def shape(self):
        return tuple((v.native,v.scale) for v in self.slots)
    def scroll(self,dt=1/60):
        self.target=clamp(self.target+self.pitch,0,self.max_offset)
        self.offset+=(self.target-self.offset)*min(1,max(0,dt)*16)
    @property
    def right_edge(self):return self.slots[12].native[0]+self.cell

class LayoutModelTests(unittest.TestCase):
    inventory=(976,284)
    crafting=(841,276)
    def test_19_position_only_substitution_would_leave_deferred_reset_bug(self):
        v=ViewModel(logical=(0,0));v.try_update_position()
        v.native=(744,0) # native grid assigns last column
        v.deferred_flush()
        self.assertEqual((0,0),v.native)

    def test_20_first_crafting_arrangement_fits_without_scrolling(self):
        m=LayoutModel();m.commit(self.inventory);m.flush();m.commit(self.crafting)
        self.assertEqual((62,60,806,248),(m.pitch,m.cell,m.viewport_w,m.viewport_h))
        self.assertEqual(2,m.viewport_w-m.right_edge)

    def test_21_first_inventory_arrangement_fits_without_scrolling(self):
        m=LayoutModel();m.commit(self.crafting);m.flush();m.commit(self.inventory)
        self.assertEqual((71,69,923,284),(m.pitch,m.cell,m.viewport_w,m.viewport_h))
        self.assertEqual(2,m.viewport_w-m.right_edge)

    def test_22_later_native_view_flush_cannot_reset_arranged_positions(self):
        for size in (self.inventory,self.crafting):
            m=LayoutModel();m.commit(size);before=m.shape();m.flush()
            self.assertEqual(before,m.shape())

    def test_23_scroll_neither_reflows_nor_rescales_cells(self):
        for size in (self.inventory,self.crafting):
            m=LayoutModel();m.commit(size);before=m.shape();commits=m.commits
            m.scroll();m.flush()
            self.assertEqual(before,m.shape());self.assertEqual(commits,m.commits)
            self.assertGreater(m.offset,0);self.assertLess(m.offset,m.target)

    def test_24_repeated_tab_transitions_and_deferred_flushes_stay_consistent(self):
        m=LayoutModel()
        for _ in range(50):
            for size in (self.inventory,self.crafting):
                self.assertTrue(m.commit(size));before=m.shape();m.flush()
                self.assertEqual(before,m.shape())
                self.assertEqual(2,m.viewport_w-m.right_edge)
                self.assertFalse(m.commit(size))
        self.assertEqual(100,m.commits)

    def test_25_padded_slot_positions_preserve_native_result(self):
        m=LayoutModel();m.slots[12].padding=(9,-7);m.commit(self.crafting)
        v=m.slots[12]
        self.assertEqual((744,0),v.native)
        self.assertEqual((735,7),v.logical)
        m.flush();self.assertEqual((744,0),v.native)

    def test_26_both_current_and_target_rows_survive_resize(self):
        m=LayoutModel();m.commit(self.inventory);m.offset=1.25*m.pitch;m.target=2.5*m.pitch
        m.commit(self.crafting)
        self.assertAlmostEqual(1.25*m.pitch,m.offset)
        self.assertAlmostEqual(2.5*m.pitch,m.target)

    def test_27_not_ready_does_not_cache_or_half_apply_layout(self):
        m=LayoutModel();before=m.shape()
        self.assertFalse(m.commit(self.inventory,ready=False))
        self.assertIsNone(m.cached);self.assertEqual(0,m.commits);self.assertEqual(before,m.shape())
        self.assertTrue(m.commit(self.inventory))

    def test_28_physical_capacity_and_clip_bounds_are_retained(self):
        m=LayoutModel();m.commit(self.inventory)
        self.assertTrue(all(v.scale>0 for v in m.slots[:100]))
        self.assertTrue(all(v.scale==0 for v in m.slots[100:]))
        self.assertEqual((461.5,-142,923,284),m.clip)
        m.commit(self.crafting)
        self.assertEqual((403,-124,806,248),m.clip)

    def test_29_capacity_shrink_clamps_offsets_without_item_data_work(self):
        m=LayoutModel();m.commit(self.inventory);m.offset=m.target=m.max_offset
        m.commit(self.inventory,physical=39)
        self.assertEqual(0,m.max_offset);self.assertEqual(0,m.offset);self.assertEqual(0,m.target)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root',nargs='?',type=Path,default=ROOT)
    parser.add_argument('--patch-only',action='store_true')
    args=parser.parse_args();ROOT=args.root.resolve();PATCH_ONLY=args.patch_only
    print('PC132: SOURCE CONTRACTS + INDEPENDENT PYTHON MODELS; NOT A C# / UNITY TEST',flush=True)
    print('Project root:',ROOT,flush=True)
    unittest.main(argv=['validate_pc132_grid_compile_recovery'],verbosity=2)
