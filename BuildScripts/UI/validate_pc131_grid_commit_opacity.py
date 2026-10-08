#!/usr/bin/env python3
"""PC131 source/XML invariants and explicit deferred-grid Python models.

Run against a project containing PC130 + PC131:
  python BuildScripts/UI/validate_pc131_grid_commit_opacity.py [project_root]

These are NOT a C# compilation or an NGUI/game test. The models deliberately
separate the view pitch from the live grid pitch to reproduce the reported
first-scroll layout change. The source assertions connect the commit sequence
to the implementation; protected hashes guard unrelated behavior.
"""
from pathlib import Path
from dataclasses import dataclass
import argparse, hashlib, json, math, re, struct, unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
FIXTURES = Path(__file__).with_name('pc131_baseline_invariants.json')
UI = 'Scripts/Crafting/UI/PersonalCrafting/'
TARGETS = ('rebirthCraftingLeftBackground', 'rebirthCraftingCenterBackground', 'rebirthCraftingRightBackground')
INSERTION = '''
        // PC131: complete the shared backpack geometry in this same layout transaction.
        // The next scroll must only move rows, not finally apply the previous tab switch.
        owner.GetChildByType<XUiC_RebirthCraftingInventoryScroll>()?.ApplyLayoutGeometry();
'''

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

@dataclass
class DeferredGridModel:
    immediate: bool = True
    pitch: int = 77
    live_pitch: int = 77
    cell: int = 75
    physical: int = 100
    viewport_w: int = 1001
    viewport_h: int = 308
    offset: float = 0.0
    target: float = 0.0
    commits: int = 0
    cached: object = None
    @property
    def max_offset(self):
        rows = max(1, (self.physical + 12) // 13)
        return max(0, rows * self.pitch - self.viewport_h)
    def refresh(self, size, physical=100, ready=True, force=False):
        key = (size, physical)
        if not force and self.cached == key:
            return False
        if not ready:
            return False
        current_row, target_row = self.offset / self.pitch, self.target / self.pitch
        self.pitch, self.cell, self.viewport_w, self.viewport_h = fit(size)
        self.physical = physical
        self.offset = clamp(current_row * self.pitch, 0, self.max_offset)
        self.target = clamp(target_row * self.pitch, 0, self.max_offset)
        if self.immediate:
            self.live_pitch = self.pitch
        self.commits += 1
        self.cached = key
        return True
    def native_update(self):
        # The delayed path formerly left this synchronization to a later native update.
        self.live_pitch = self.pitch
    def first_scroll(self, direction=1, dt=1/60):
        self.target = clamp(self.target + direction * self.pitch, 0, self.max_offset)
        self.animate(dt)
        self.native_update()
    def animate(self, dt):
        self.target = clamp(self.target, 0, self.max_offset)
        if abs(self.offset - self.target) < .05:
            self.offset = self.target
            return
        self.offset += (self.target - self.offset) * clamp(max(0, dt) * 16, 0, 1)
        if abs(self.offset - self.target) < .15:
            self.offset = self.target
    @property
    def right_edge(self): return 12 * self.live_pitch + self.cell
    def arrangement(self):
        # Local positions within the grid. Its parent offset scrolls independently.
        return tuple((i % 13 * self.live_pitch, -(i // 13) * self.live_pitch, self.cell)
                     for i in range(self.physical))

class SourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.ref = json.loads(FIXTURES.read_text())
        cls.scroll = (ROOT/UI/'XUiC_RebirthCraftingInventoryScroll.cs').read_text(encoding='utf-8-sig')
        cls.layout = (ROOT/UI/'RebirthPersonalCraftingLayoutService.cs').read_text(encoding='utf-8-sig')
        cls.opacity = (ROOT/UI/'RebirthPersonalCraftingPanelOpacity.cs').read_text(encoding='utf-8-sig')
        cls.command = (ROOT/UI/'ConsoleCmdRebirthUiOpacity.cs').read_text(encoding='utf-8-sig')
        cls.windows = (ROOT/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8-sig')
    def test_01_layout_sizes_then_commits_shared_grid_synchronously(self):
        body = method(self.layout, 'ApplyInventoryInternalLayout')
        self.assertIn(INSERTION, self.layout)
        self.assertLess(body.index('SetRect(' + 'owner.GetChildById(', body.index('int scrollHeight')),
                        body.index('ApplyLayoutGeometry()'))
        for name in ('ApplyCenterInternalLayout', 'ApplyCenterInventoryLayout'):
            self.assertIn('ApplyInventoryInternalLayout(width, inventoryHeight);', method(self.layout, name))
        self.assertIn('RefreshGeometry(false);', method(self.scroll, 'ApplyLayoutGeometry'))
    def test_02_outer_layout_and_bottom_alignment_are_unchanged(self):
        old = self.layout.replace(INSERTION, '')
        self.assertEqual(self.ref['original_sha256'][UI+'RebirthPersonalCraftingLayoutService.cs'], sha(old))
    def test_03_live_grid_receives_both_pitch_values_before_reposition(self):
        body = method(self.scroll, 'RefreshGeometry')
        for value in ('grid.CellWidth = effectivePitch;', 'grid.CellHeight = effectivePitch;',
                      'grid.Grid.cellWidth = effectivePitch;', 'grid.Grid.cellHeight = effectivePitch;',
                      'grid.Grid.maxPerLine = Columns;'):
            self.assertLess(body.index(value), body.index('grid.Grid.Reposition();'))
        self.assertEqual(1, body.count('grid.Grid.Reposition();'))
    def test_04_slots_and_clip_are_ready_before_committed_cache(self):
        body = method(self.scroll, 'RefreshGeometry')
        for value in ('grid.Grid == null', 'clipView.UiTransform == null', 'slots.Length == 0',
                      'slots[i].ViewComponent.UiTransform == null'):
            self.assertLess(body.index(value), body.index('effectivePitch ='))
        for value in ('lastPhysical = physical;', 'lastSize = size;', 'geometryCommits++;'):
            self.assertGreater(body.index(value), body.index('grid.Grid.Reposition();'))
    def test_05_pending_view_position_flush_precedes_grid_arrangement(self):
        body = method(self.scroll, 'RefreshGeometry')
        self.assertLess(body.index('slot.ViewComponent.UpdateData();'), body.index('UiTransform.localScale ='))
        self.assertLess(body.index('grid.UpdateData();'), body.index('grid.Grid.Reposition();'))
        for s in ('slot.Update(', 'inventory.Update(', 'RefreshBindings(', 'SetStacks(', 'SetAllChildrenDirty('):
            self.assertNotIn(s, body)
    def test_06_native_clipping_is_committed_with_the_same_dimensions(self):
        body = method(self.scroll, 'RefreshGeometry')
        for s in ('clipView.Size = new Vector2i(viewportWidth, viewportHeight);',
                  'clipView.ClippingSize = new Vector2(viewportWidth, viewportHeight);',
                  'clipView.ClippingCenter = new Vector2(viewportWidth * 0.5f, -viewportHeight * 0.5f);',
                  'clipView.UpdateData();'):
            self.assertIn(s, body)
            self.assertLess(body.index(s), body.index('grid.Grid.Reposition();'))
    def test_07_unchanged_bounds_return_before_any_layout_write(self):
        body = method(self.scroll, 'RefreshGeometry')
        guard = 'if (!force && physical == lastPhysical && size.x == lastSize.x && size.y == lastSize.y)'
        self.assertIn(guard, body)
        self.assertLess(body.index(guard), body.index('grid.Columns ='))
    def test_08_scrolling_input_interpolation_and_selection_visibility_unchanged(self):
        for name, digest in self.ref['protected_scroll_methods_sha256'].items():
            with self.subTest(method=name):
                self.assertEqual(digest, sha(method(self.scroll, name)))
    def test_09_scroll_position_only_moves_the_grid_root(self):
        body = method(self.scroll, 'ApplyGridPosition')
        self.assertIn('baseGridPosition.x, baseGridPosition.y + Mathf.RoundToInt(pixelOffset)', body)
        self.assertIn('previous.x != position.x || previous.y != position.y', body)
        self.assertIn('inventory.ViewComponent.TryUpdatePosition();', body)
        for s in ('Reposition(', 'CellWidth', 'CellHeight', 'localScale', 'RefreshGeometry(', 'IsVisible ='):
            self.assertNotIn(s, body)
        self.assertNotIn('ApplySlotViewportVisibility', self.scroll)
    def test_10_resize_keeps_both_offsets_in_row_units(self):
        body = method(self.scroll, 'RefreshGeometry')
        for s in ('currentRowOffset = pixelOffset / Math.Max(1, effectivePitch)',
                  'targetRowOffset = targetPixelOffset / Math.Max(1, effectivePitch)',
                  'currentRowOffset * effectivePitch', 'targetRowOffset * effectivePitch'):
            self.assertIn(s, body)
    def test_11_existing_debug_gate_logs_actual_spacing_and_commit_count(self):
        body = method(self.scroll, 'TraceState')
        self.assertIn('!RebirthLogSettings.CraftingUiLoggingEnabled', body)
        self.assertIn('traceGrid.Grid.cellWidth', body)
        for s in ('nativePitch=', 'lastColumnRight=', 'geometryCommits='):
            self.assertIn(s, self.scroll)
    def test_12_queue_input_navigation_actions_and_slot_selection_are_byte_identical(self):
        for rel, digest in self.ref['protected_sha256'].items():
            with self.subTest(file=rel):
                self.assertEqual(digest, hashlib.sha256((ROOT/rel).read_bytes()).hexdigest())
    def test_13_only_the_three_main_background_alpha_literals_changed_in_xml(self):
        old = self.windows
        for name in TARGETS:
            pattern = r'(<sprite name="'+name+r'"[^>]*color="17,17,21,)250(")'
            old, count = re.subn(pattern, r'\g<1>217\2', old)
            self.assertEqual(1, count)
        self.assertEqual(self.ref['original_sha256']['Config/XUi_InGame/windows.xml'], sha(old))
        ET.fromstring(self.windows)
        ET.parse(ROOT/'Config/XUi_InGame/templates.xml')
    def test_14_runtime_default_and_reset_are_98_percent(self):
        self.assertIn('DefaultAlpha = 0.98f;', self.opacity)
        self.assertIn('DefaultPercent = 98d;', self.opacity)
        self.assertIn('requestedPercent = DefaultPercent;', method(self.opacity, 'ResetToDefault'))
        self.assertIn('OverrideEnabled = false;', method(self.opacity, 'ResetToDefault'))
        self.assertIn('PC131-default-98', self.opacity)
        self.assertIn('98% opacity / 2% transparency', self.command)
        self.assertNotIn('217/255', self.command)
    def test_15_opacity_service_other_than_default_and_source_label_is_unchanged(self):
        old = self.opacity.replace('DefaultAlpha = 0.98f;', 'DefaultAlpha = 217f / 255f;')
        old = old.replace('DefaultPercent = 98d;', 'DefaultPercent = 217d * 100d / 255d;')
        old = old.replace('PC131-default-98', 'PC129-default-217/255')
        self.assertEqual(self.ref['original_sha256'][UI+'RebirthPersonalCraftingPanelOpacity.cs'], sha(old))
    def test_16_opacity_command_changes_help_only(self):
        old = self.command.replace('restore the default: 98% opacity / 2% transparency',
                                   'restore the PC129 default: 217/255 alpha (~85.098%)')
        self.assertEqual(self.ref['original_sha256'][UI+'ConsoleCmdRebirthUiOpacity.cs'], sha(old))
    def test_17_scrollbar_width_and_fit_rules_are_preserved(self):
        for s in ('ScrollbarTrackWidth = 16;', 'ScrollbarThumbWidth = 12;',
                  'effectiveCell = Math.Max(38, effectivePitch - CellGap);',
                  'viewportWidth = effectivePitch * Columns;',
                  'viewportHeight = Math.Min(availableH, effectivePitch * VisibleRows);'):
            self.assertIn(s, self.scroll)

class DeferredLayoutModelTests(unittest.TestCase):
    inventory = (976, 284)       # 1000-wide compact panel minus padding/header
    crafting = (841, 276)        # 865-wide center column minus padding/header
    def test_18_original_deferred_path_reproduces_both_reported_symptoms(self):
        m = DeferredGridModel(immediate=False)
        m.refresh(self.inventory); m.native_update()
        m.refresh(self.crafting)
        self.assertEqual((62,71), (m.pitch, m.live_pitch))
        self.assertGreater(m.right_edge, m.viewport_w)
        before = m.arrangement(); m.first_scroll()
        self.assertNotEqual(before, m.arrangement())
        m.refresh(self.inventory)
        self.assertEqual((71,62), (m.pitch, m.live_pitch))
        self.assertLess(m.right_edge, m.viewport_w - 13*2)
        before = m.arrangement(); m.first_scroll()
        self.assertNotEqual(before, m.arrangement())
    def test_19_first_crafting_draw_fits_before_any_scroll(self):
        m = DeferredGridModel(); m.refresh(self.inventory); m.refresh(self.crafting)
        self.assertEqual((62,60,806,248), (m.pitch,m.cell,m.viewport_w,m.viewport_h))
        self.assertEqual(62, m.live_pitch)
        self.assertEqual(2, m.viewport_w - m.right_edge)
    def test_20_first_inventory_draw_fits_before_any_scroll(self):
        m = DeferredGridModel(); m.refresh(self.crafting); m.refresh(self.inventory)
        self.assertEqual((71,69,923,284), (m.pitch,m.cell,m.viewport_w,m.viewport_h))
        self.assertEqual(2, m.viewport_w - m.right_edge)
    def test_21_first_scroll_does_not_reflow_or_recommit_either_surface(self):
        for size in (self.inventory,self.crafting):
            with self.subTest(size=size):
                m = DeferredGridModel(); m.refresh(size)
                before=(m.arrangement(),m.viewport_w,m.viewport_h,m.commits)
                m.first_scroll()
                self.assertEqual(before,(m.arrangement(),m.viewport_w,m.viewport_h,m.commits))
                self.assertGreater(m.offset,0); self.assertLess(m.offset,m.target)
    def test_22_repeated_tab_switches_commit_only_on_transition(self):
        m = DeferredGridModel()
        for _ in range(50):
            for size in (self.inventory,self.crafting):
                m.refresh(size); before=m.commits
                for _ in range(5):
                    self.assertFalse(m.refresh(size)); m.first_scroll()
                    self.assertEqual(m.pitch,m.live_pitch)
                    self.assertEqual(before,m.commits)
                    self.assertEqual(2,m.viewport_w-m.right_edge)
    def test_23_mid_scroll_resize_preserves_logical_current_and_target_rows(self):
        m=DeferredGridModel(); m.refresh(self.inventory)
        m.offset=1.25*m.pitch; m.target=2.75*m.pitch
        m.refresh(self.crafting)
        self.assertAlmostEqual(1.25,m.offset/m.pitch)
        self.assertAlmostEqual(2.75,m.target/m.pitch)
        m.refresh(self.inventory)
        self.assertAlmostEqual(1.25,m.offset/m.pitch)
        self.assertAlmostEqual(2.75,m.target/m.pitch)
    def test_24_late_views_retry_identical_bounds_instead_of_caching_failure(self):
        m=DeferredGridModel()
        self.assertFalse(m.refresh(self.inventory,ready=False))
        self.assertIsNone(m.cached); self.assertEqual(0,m.commits)
        self.assertTrue(m.refresh(self.inventory,ready=True))
        self.assertEqual(m.pitch,m.live_pitch)
    def test_25_capacity_changes_clamp_offset_without_changing_pitch(self):
        m=DeferredGridModel(); m.refresh(self.inventory)
        m.offset=m.target=m.max_offset
        m.refresh(self.inventory,physical=52)
        self.assertEqual(0,m.offset); self.assertEqual(0,m.target)
        self.assertEqual(71,m.pitch); self.assertEqual(52,len(m.arrangement()))
    def test_26_all_authoritative_slots_stay_in_their_row_and_column(self):
        for size in (self.inventory,self.crafting,(706,210),(880,264)):
            m=DeferredGridModel(); m.refresh(size)
            positions=m.arrangement()
            self.assertEqual(100,len(positions))
            for i,(x,y,cell) in enumerate(positions):
                self.assertEqual((i%13*m.pitch,-(i//13)*m.pitch,m.cell),(x,y,cell))
                self.assertLessEqual(x+cell,m.viewport_w)
    def test_27_scroll_reaches_last_row_and_returns_without_size_changes(self):
        for size in (self.inventory,self.crafting):
            m=DeferredGridModel(); m.refresh(size); before=m.arrangement()
            for _ in range(12): m.first_scroll()
            for _ in range(120): m.animate(1/60)
            self.assertEqual(m.max_offset,m.offset)
            self.assertEqual(before,m.arrangement())
            for _ in range(12): m.first_scroll(-1)
            for _ in range(120): m.animate(1/60)
            self.assertEqual(0,m.offset); self.assertEqual(before,m.arrangement())
    def test_28_default_percentage_and_xml_quantization(self):
        self.assertEqual(f32(.98),f32(98/100))
        self.assertEqual(250,round(255*.98))
        self.assertLess(abs(250/255-.98),.5/255)
        # Color alpha only: do not alter the player's global UI opacity setting.
        self.assertAlmostEqual(.9702,.98*.99)

if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('project_root', nargs='?', type=Path, default=ROOT)
    args=parser.parse_args(); ROOT=args.project_root.resolve()
    unittest.main(argv=['pc131'],verbosity=2)
