#!/usr/bin/env python3
"""PC127 + PC128 source invariants and isolated behavioral models (NOT a game/runtime test).

Usage: python BuildScripts/UI/validate_pc127_input_ownership_console.py [project_root]
Requires Python 3.9+ only. A source check is not a C# compiler. The behavioral models
exercise the documented native close ordering and PC127 policy; they do not execute
Unity, Harmony, the actual C# source, or 7 Days to Die.
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass, field
from pathlib import Path
import re
import sys
import unittest

PATHS = {
    'window': 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs',
    'actions': 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingActions.cs',
    'navigation': 'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingNavigationService.cs',
    'queue': 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueueEntry.cs',
    'console': 'Scripts/UI/Console/RebirthConsolePopupSuppression.cs',
    'controls': 'Scripts/Input/RebirthNativeControls.cs',
    'horn': 'Scripts/Vehicles/RebirthHornDoorService.cs',
    'creation': 'Scripts/Survivor/UI/RebirthSurvivorFirstEntryUiService.cs',
}
ROOT = Path(__file__).resolve().parents[2]


def mask_noncode(source: str) -> str:
    """Mask ordinary C# comments/strings, retaining offsets for method extraction.

    Not a language parser: this deliberately supports the syntax used by the changed
    methods, not arbitrary nested raw/interpolated C# string expressions.
    """
    token = re.compile(r'//[^\n]*|/\*.*?\*/|@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'', re.S)
    return token.sub(lambda m: ''.join('\n' if ch == '\n' else ' ' for ch in m.group()), source)


def method(source: str, name: str) -> str:
    """Return one method's masked body; require an actual declaration."""
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


class SourceInvariantTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.s = {name: (ROOT / path).read_text(encoding='utf-8-sig') for name, path in PATHS.items()}

    def test_inventory_never_writes_global_controllability(self):
        code = mask_noncode(self.s['window'])
        self.assertNotRegex(code, r'\b(?:SetControllable|SetControllableOverride)\s*\(')

    def test_inventory_does_not_reset_or_push_pop_action_stack(self):
        code = mask_noncode(self.s['window'])
        self.assertNotRegex(code, r'\bResetActionSets\s*\(')
        self.assertNotRegex(code, r'ActionSetManager\s*\.\s*(?:Push|Pop|Reset|Remove|Insert)\s*\(')
        self.assertNotIn('MaintainInputOwnership', code)
        self.assertNotIn('suppressGameplayRestoreOnClose', code)

    def test_close_leaves_native_pop_and_does_cleanup_in_finally(self):
        close = method(self.s['window'], 'OnClose')
        release = method(self.s['window'], 'ReleaseInputOwnership')
        self.assertIn('base.OnClose()', close)
        self.assertEqual(close.count('finally'), 2)
        self.assertIn('ReleaseInputOwnership()', close)
        for forbidden in ['IsModalWindowOpen(', 'IsInputActive(', 'StartCoroutine(', 'WaitForSeconds(', 'ResetActionSets(']:
            self.assertNotIn(forbidden, release)
        self.assertIn('inputOwnershipActive = false;', release)

    def test_only_own_text_focus_is_released(self):
        code = method(self.s['window'], 'ReleaseFocusedTextInput')
        self.assertIn('selected.transform.IsChildOf(rootTransform)', code)
        self.assertLess(code.index('IsChildOf('), code.index('RemoveFocus()'))

    def test_console_has_priority_over_inventory_cancel(self):
        code = method(self.s['window'], 'HandleCancelOrBack')
        self.assertLess(code.index('BlocksGameplayInput()'), code.index('Input.GetKeyDown'))
        self.assertRegex(code, r'BlocksGameplayInput\(\)\)\s*return false;')
        self.assertIn('!RebirthConsoleInputGuardRuntime.IsConsoleOpen()', method(self.s['window'], 'ReleaseInputOwnership'))

    def test_inventory_internal_navigation_does_not_reacquire(self):
        code = method(self.s['window'], 'SetSurfaceMode')
        for forbidden in ['AcquireInputOwnership(', 'ReleaseInputOwnership(', 'ResetActionSets(', 'SetControllable(']:
            self.assertNotIn(forbidden, code)
        external = method(self.s['window'], 'PrepareForExternalRoute')
        self.assertIn('ReleaseFocusedTextInput()', external)

    def test_console_hooks_exact_native_lifecycle(self):
        code = mask_noncode(self.s['console'])
        self.assertIn('nameof(GUIWindowConsole.OnOpen)', code)
        self.assertIn('nameof(GUIWindowConsole.OnClose)', code)
        self.assertRegex(code, r'void MarkOpenedPostfix\(GUIWindowConsole __instance\)')
        for name in ['RebirthConsoleOpenedPatch', 'RebirthConsoleClosedPatch', 'RebirthConsolePlayerMovementGuardPatch', 'RebirthConsoleItemActionListGuardPatch']:
            self.assertIn('CreateClassProcessor(typeof(' + name + ')).Patch()', code)
        self.assertNotIn('GetMethods(', code)
        self.assertNotIn('BindingFlags', code)
        self.assertNotIn('KeyCode.F1', code)
        self.assertNotIn('KeyCode.Escape', code)

    def test_console_live_showing_and_pending_open_are_authority(self):
        code = method(self.s['console'], 'IsConsoleOpen')
        self.assertIn('console.isShowing', code)
        self.assertIn('console.windowManager.IsWindowOpen(ConsoleWindowId)', code)
        self.assertIn('ui.windowManager.IsWindowOpen(ConsoleWindowId)', code)
        self.assertNotIn('realtimeSinceStartup', code)

    def test_pc128_console_identifier_does_not_require_native_static_id(self):
        code = mask_noncode(self.s['console'])
        self.assertRegex(code, r'private\s+const\s+string\s+ConsoleWindowId\s*=\s*nameof\(GUIWindowConsole\)\s*;')
        self.assertNotRegex(code, r'GUIWindowConsole\s*\.\s*ID\b')
        self.assertEqual(method(self.s['console'], 'IsConsoleOpen').count('IsWindowOpen(ConsoleWindowId)'), 2)

    def test_console_close_guard_is_exactly_one_frame_not_timeout(self):
        code = method(self.s['console'], 'BlocksGameplayInput')
        self.assertIn('s_closedFrame == Time.frameCount', code)
        close = method(self.s['console'], 'MarkClosedPostfix')
        self.assertIn('s_closedFrame = Time.frameCount', close)
        self.assertNotRegex(mask_noncode(self.s['console']), r'\b(?:WaitForSeconds|Invoke|StartCoroutine|ResetInputAxes|SetControllable|ResetActionSets)\s*\(')
        self.assertNotIn('realtimeSinceStartup', mask_noncode(self.s['console']))

    def test_console_clears_transient_jump_sprint_look_translation(self):
        code = method(self.s['console'], 'ClearGameplayMovement')
        for name in ['jump', 'running', 'down', 'downToggle']:
            self.assertIn('player.movementInput.' + name + ' = false;', code)
        for name in ['moveForward', 'moveStrafe']:
            self.assertIn('player.movementInput.' + name + ' = 0f;', code)
        self.assertIn('player.movementInput.rotation = Vector3.zero;', code)
        self.assertIn('if (player.vp_FPController != null)', code)
        self.assertIn('player.ClearMovementInputs()', code)

    def test_console_does_not_patch_its_typing_update(self):
        code = mask_noncode(self.s['console'])
        self.assertNotIn('nameof(GUIWindowConsole.Update)', code)
        self.assertNotIn('nameof(GUIWindowManager.Update)', code)
        self.assertNotIn('commandField', code)
        self.assertIn('HarmonyPriority(Priority.First)', code)
        self.assertIn('HarmonyPriority(Priority.Last)', code)
        section = self.s['console'].split('public static class RebirthConsolePlayerMovementGuardPatch', 1)[1]
        self.assertIn('return false;', method(section, 'Prefix'))
        self.assertIn('ClearGameplayMovement(', method(section, 'Postfix'))

    def test_attempted_automatic_open_does_not_manufacture_input_lock(self):
        section = self.s['console'].split('public static class RebirthConsolePopupPatch', 1)[1]
        section = section.split('internal static class RebirthConsoleOpenedPatch', 1)[0]
        self.assertIn('return !RebirthConsolePopupRuntimePolicy.SuppressAutomaticErrorPopups;', method(section, 'Prefix'))
        self.assertNotIn('MarkOpenedPostfix(', mask_noncode(section))

    def test_rebirth_player_and_vehicle_dispatchers_gate_console(self):
        code = method(self.s['controls'], 'PlayerMoveControllerUpdatePostfix')
        self.assertLess(code.index('BlocksGameplayInput()'), code.index('EnsureRegisteredFromNativePlatform('))
        vehicle = method(self.s['controls'], 'MoveByAttachedEntityPostfix')
        self.assertLess(vehicle.index('BlocksGameplayInput()'), vehicle.index('TryConsumeCruiseToggle('))
        guard = vehicle[:vehicle.index('TryConsumeCruiseToggle(')]
        self.assertIn('___movementInput.Clear()', guard)
        self.assertNotIn('cruiseState =', guard)
        horn = method(self.s['horn'], 'PlayerUpdate')
        self.assertLess(horn.index('BlocksGameplayInput()'), horn.index('HornPressed('))

    def test_underlying_crafting_callbacks_gate_console(self):
        for name in ['Craft_OnPress', 'Favorite_OnPress', 'Track_OnPress', 'Explorer_OnPress']:
            with self.subTest(callback=name):
                self.assertTrue(method(self.s['actions'], name).strip().startswith('if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput()) return;'))
        self.assertTrue(method(self.s['queue'], 'Cancel_OnPress').strip().startswith('if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput()) return;'))
        code = method(self.s['navigation'], 'Navigate')
        self.assertLess(code.index('BlocksGameplayInput()'), code.index('source == null'))
        section = self.s['console'].split('internal static class RebirthConsoleItemActionListGuardPatch', 1)[1]
        self.assertIn('return !RebirthConsoleInputGuardRuntime.BlocksGameplayInput();', method(section, 'Prefix'))

    def test_creation_captures_prior_ownership_before_snapshot_assignment(self):
        code = method(self.s['creation'], 'OnOwnerStateChanged')
        self.assertLess(code.index('bool hadPendingCreationUi'), code.index('creationUiRequired = required;'))
        for state in ['creationUiRequired', 'selectorOpenRequested', 'stagedSelectionReady', 'stagedWaitingForOwnerReady', 'stagedRequestId != 0UL', 'IsGateVisible']:
            self.assertIn(state, code[:code.index('bool locallyConfiguredForRebirth')])
        self.assertEqual(code.count('if (!hadPendingCreationUi) return;'), 2)
        self.assertEqual(code.count('CloseCreationWindowsAndRestoreControl();'), 2)
        ready = code[code.index('if (snapshot.HasCharacter'):]
        self.assertLess(ready.index('if (!hadPendingCreationUi)'), ready.index('CloseCreationWindowsAndRestoreControl()'))
        self.assertIn('selectorOpenRequested = false;', ready)
        self.assertIn('ClearStagedSelection();', ready)

    def test_real_creation_and_recovery_holds_are_retained(self):
        code = method(self.s['creation'], 'OnOwnerStateChanged')
        for required in ['CreationRequired', 'RecoveryRequired', 'LockLocalPlayer()', 'QueueSelectorOpen()']:
            self.assertIn(required, code)
        self.assertIn('player.SetControllable(false)', method(self.s['creation'], 'LockLocalPlayer'))
        clear = method(self.s['creation'], 'ClearStagedSelection')
        for required in ['stagedSelectionReady = false;', 'stagedRequestId = 0UL;', 'stagedWaitingForOwnerReady = false;']:
            self.assertIn(required, clear)

    def test_new_boundary_diagnostics_are_debug_gated(self):
        trace = method(self.s['window'], 'TraceInputOwnership')
        self.assertLess(trace.index('CraftingUiLoggingEnabled'), trace.index('Log.Out('))
        for name in ['MarkOpenedPostfix', 'MarkClosedPostfix', 'Install']:
            with self.subTest(method=name):
                body = method(self.s['console'], name)
                self.assertLess(body.index('CraftingUiLoggingEnabled'), body.index('Log.Out('))


@dataclass
class Window:
    name: str
    showing: bool = True
    enabled: bool = True


@dataclass
class NativeCloseModel:
    """Simplified native manager: callback runs BEFORE its corresponding Pop.

    Distinct strings stand for distinct action-set instances. It intentionally does
    not model rendering, all native windows, or shared GUIActions aliasing.
    """
    stack: list[str] = field(default_factory=lambda: ['gameplay'])
    windows: list[Window] = field(default_factory=list)
    control_override: bool = True
    cached_modal: bool = False
    resets: int = 0
    mismatch: int = 0

    def open(self, name='crafting'):
        w = Window(name)
        self.windows.append(w)
        self.stack.append(name)
        self.cached_modal = True
        return w

    def reset(self):
        self.resets += 1
        self.stack = ['gameplay'] + [w.name for w in self.windows if w.showing]

    def close(self, w, old_bug=False):
        w.showing = False
        if old_bug:
            self.control_override = not self.cached_modal
            self.reset()
        # Native DisableWindowActionSet is after the callback, even for old_bug.
        if w.enabled:
            if self.stack[-1] != w.name:
                self.mismatch += 1
            else:
                self.stack.pop()
            w.enabled = False

    def next_gui_pass(self):
        self.cached_modal = any(w.showing for w in self.windows)

    def can_move(self):
        return self.control_override and not self.cached_modal and self.stack == ['gameplay']


@dataclass
class CreationModel:
    required: bool = False
    selector: bool = False
    staged: bool = False
    waiting: bool = False
    request: int = 0
    gate: bool = False
    cleanups: int = 0
    holds: int = 0

    def snapshot(self, state='Ready', has_character=True, rebirth=True):
        had_pending = self.required or self.selector or self.staged or self.waiting or self.request != 0 or self.gate
        self.required = rebirth and state in ('CreationRequired', 'RecoveryRequired')
        if self.required:
            if not self.staged:
                self.holds += 1
                self.selector = True
            return
        if (has_character and state == 'Ready') or not rebirth:
            if not had_pending:
                return
            self.selector = self.staged = self.waiting = self.gate = False
            self.request = 0
            self.cleanups += 1


@dataclass
class ConsoleModel:
    showing: bool = False
    pending: bool = False
    closed_frame: int = -1
    text: str = ''
    gameplay: list[str] = field(default_factory=list)
    ui_actions: list[str] = field(default_factory=list)

    def blocked(self, frame):
        return self.showing or self.pending or (self.closed_frame >= 0 and frame == self.closed_frame)

    def open(self):
        self.pending = True
        self.closed_frame = -1

    def displayed(self):
        self.pending, self.showing = False, True

    def close(self, frame):
        self.showing = self.pending = False
        self.closed_frame = frame

    def press(self, text, frame, underlying_ui=False):
        # Console typing is independent of the patched gameplay update.
        if self.showing or self.pending:
            self.text += text
        if not self.blocked(frame):
            (self.ui_actions if underlying_ui else self.gameplay).append(text)


class BehavioralModelTests(unittest.TestCase):
    def test_old_close_counterexample_reproduces_warning_and_persistent_hold(self):
        m = NativeCloseModel()
        w = m.open()
        m.close(w, old_bug=True)
        m.next_gui_pass()
        self.assertEqual(m.mismatch, 1)
        self.assertEqual(m.resets, 1)
        self.assertFalse(m.can_move())  # stack reset alone does not release false override

    def test_reported_inventory_craft_inventory_drop_close_sequence(self):
        m = NativeCloseModel()
        w = m.open()
        bag = ['cloth', 'water', 'bandage']
        for surface in ['Inventory', 'Crafting', 'Inventory']:
            self.assertEqual(m.stack, ['gameplay', 'crafting'])  # internal switch, no push/pop
        bag[1] = None
        m.close(w)
        m.next_gui_pass()
        self.assertTrue(m.can_move())
        self.assertEqual((m.resets, m.mismatch), (0, 0))
        self.assertEqual(bag, ['cloth', None, 'bandage'])

    def test_repeated_sessions_do_not_accumulate_stack_entries(self):
        m = NativeCloseModel()
        for _ in range(100):
            w = m.open()
            for _ in range(30):  # internal nav does not touch manager stack
                self.assertEqual(len(m.stack), 2)
            m.close(w)
            m.next_gui_pass()
            self.assertTrue(m.can_move())
        self.assertEqual((m.mismatch, m.resets), (0, 0))

    def test_existing_respawn_or_creation_control_hold_is_not_overridden(self):
        m = NativeCloseModel(control_override=False)
        m.close(m.open())
        m.next_gui_pass()
        self.assertFalse(m.control_override)
        self.assertEqual(m.stack, ['gameplay'])
        self.assertFalse(m.can_move())

    def test_native_external_route_replaces_window_owner(self):
        m = NativeCloseModel()
        w = m.open()
        m.close(w)
        destination = m.open('character')
        m.next_gui_pass()
        self.assertFalse(m.can_move())
        self.assertEqual(m.stack, ['gameplay', 'character'])
        m.close(destination)
        m.next_gui_pass()
        self.assertTrue(m.can_move())
        self.assertEqual((m.mismatch, m.resets), (0, 0))

    def test_ready_condition_snapshots_are_not_creation_completions(self):
        c = CreationModel()
        for _ in range(100):
            c.snapshot()
        self.assertEqual((c.cleanups, c.holds), (0, 0))
        c.snapshot(rebirth=False)
        self.assertEqual(c.cleanups, 0)

    def test_real_creation_or_recovery_completion_runs_once(self):
        for status in ('CreationRequired', 'RecoveryRequired'):
            with self.subTest(status=status):
                c = CreationModel()
                c.snapshot(status, has_character=False)
                self.assertEqual(c.holds, 1)
                c.snapshot()
                self.assertEqual(c.cleanups, 1)
                for _ in range(8):
                    c.snapshot()
                self.assertEqual(c.cleanups, 1)

    def test_staged_and_gate_ownership_are_recognized_before_ready(self):
        for flag in ('required', 'selector', 'staged', 'waiting', 'request', 'gate'):
            with self.subTest(flag=flag):
                c = CreationModel(**{flag: 123 if flag == 'request' else True})
                c.snapshot()
                c.snapshot()
                self.assertEqual(c.cleanups, 1)
        c = CreationModel(required=True, staged=True)
        c.snapshot('CreationRequired', has_character=False)
        self.assertEqual(c.holds, 0)  # staged selection waits for server commit
        c.snapshot()
        self.assertEqual(c.cleanups, 1)

    def test_base_mode_transition_only_cleans_owned_creation_flow(self):
        c = CreationModel(selector=True)
        c.snapshot(rebirth=False)
        c.snapshot(rebirth=False)
        self.assertEqual(c.cleanups, 1)

    def test_console_pending_open_blocks_before_first_draw(self):
        c = ConsoleModel()
        c.open()
        c.press(' ', 10)
        self.assertTrue(c.blocked(10))
        self.assertEqual(c.text, ' ')
        self.assertEqual(c.gameplay, [])
        c.displayed()
        self.assertTrue(c.blocked(11))

    def test_console_space_wasd_and_action_keys_are_text_not_gameplay(self):
        c = ConsoleModel()
        c.open()
        c.displayed()
        keys = 'wasd hello world 123 re'
        for i, key in enumerate(keys, 20):
            c.press(key, i)
            c.press('', i, underlying_ui=True)
        self.assertEqual(c.text, keys)
        self.assertEqual(c.gameplay, [])
        self.assertEqual(c.ui_actions, [])

    def test_ready_refresh_does_not_clear_console_or_restore_gameplay(self):
        c = ConsoleModel()
        creation = CreationModel()
        c.open()
        c.displayed()
        for i in range(100):
            creation.snapshot()
            c.press(' ', i)
        self.assertEqual(creation.cleanups, 0)
        self.assertEqual(len(c.text), 100)
        self.assertEqual(c.gameplay, [])

    def test_console_close_consumes_one_frame_then_accepts_next_frame(self):
        c = ConsoleModel()
        c.open()
        c.displayed()
        c.close(100)
        c.press(' ', 100)
        self.assertEqual(c.gameplay, [])
        self.assertFalse(c.blocked(101))
        c.press('w', 101)
        self.assertEqual(c.gameplay, ['w'])
        c.open()  # opening again overrides the previous close-frame marker
        self.assertEqual(c.closed_frame, -1)
        self.assertTrue(c.blocked(101))

    def test_console_escape_does_not_also_close_underlying_inventory(self):
        c = ConsoleModel()
        m = NativeCloseModel()
        w = m.open()
        c.open()
        c.displayed()
        c.close(25)
        if not c.blocked(25):
            m.close(w)
        self.assertTrue(w.showing)
        self.assertFalse(m.can_move())
        # A fresh inventory close after the console frame restores normal native input.
        m.close(w)
        m.next_gui_pass()
        self.assertTrue(m.can_move())

    def test_rejected_automatic_open_does_not_lock_gameplay(self):
        c = ConsoleModel()
        suppress_popup = True
        if not suppress_popup:
            c.open()
        self.assertFalse(c.blocked(10))
        c.open()  # deliberate opening remains independent of that policy
        self.assertTrue(c.blocked(11))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('project_root', type=Path, nargs='?', default=ROOT)
    args = parser.parse_args()
    ROOT = args.project_root.resolve()
    missing = [str(ROOT / path) for path in PATHS.values() if not (ROOT / path).is_file()]
    if missing:
        parser.error('Missing source files:\n' + '\n'.join(missing))
    print('PC127 + PC128 validation: source invariants + isolated Python policy models.', flush=True)
    print('NOT a C# compile, Harmony integration, Unity, multiplayer, or in-game test.', flush=True)
    print('Source root: ' + str(ROOT), flush=True)
    suite = unittest.defaultTestLoader.loadTestsFromModule(sys.modules[__name__])
    result = unittest.TextTestRunner(stream=sys.stdout, verbosity=2).run(suite)
    sys.exit(0 if result.wasSuccessful() else 1)
