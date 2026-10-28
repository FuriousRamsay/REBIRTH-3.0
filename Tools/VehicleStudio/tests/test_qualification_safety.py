import sys
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from run_camper_qualification import same_session, receipt_path, wait_quiescent, require_no_live_zombies, prepare_safe_arrival, parked_ready, choose_empty_pad, pad_search_candidates, Suite


class QualificationSafety(unittest.TestCase):
    def test_full_pad_search_keeps_dimensions_radius_and_preserved_entities(self):
        player = [100, 40, 0]
        positions = [[0, 40, 20], [20, 40, -40]]
        candidates = pad_search_candidates({'position': [0, 40, 0]}, {'player': {'position': player}},
            {'count': 2, 'entities': [{'position': p} for p in positions]})
        self.assertTrue(candidates)
        for x, z, floor, length, width in candidates:
            self.assertEqual((floor, length, width), (0, 80, 40))
            self.assertLessEqual((x - player[0]) ** 2 + (z - player[2]) ** 2, 145 ** 2)
            for p in positions + [player]:
                self.assertFalse(x - 24 <= p[0] <= x + 24 and z - 44 <= p[2] <= z + 44)

    def test_read_only_search_avoids_observed_containers_before_any_mutation(self):
        suite = Suite.__new__(Suite); suite.report = {}; commands = []
        def diagnostic(command, marker):
            commands.append(command)
            x, z, _, length, width = map(int, command.split()[3:])
            first = len(commands) == 1
            return dict(kind='CAMPER_PAD_READ_ONLY_PREFLIGHT', xMin=x - width // 2, zMin=z - length // 2,
                width=width, length=length, status='FAIL_NO_WORLD_CHANGES' if first else 'READY_NO_WORLD_CHANGES',
                blockers=[{'reason': 'preserved-tile-entity'}] if first else [], mutationLimitExceeded=False,
                preservedTileEntities=[{'position': {'x': 0, 'y': 40, 'z': 0}}] if first else [])
        suite.diagnostic = diagnostic
        pad = suite.find_pad({'position': [0, 40, 0]}, {'player': {'position': [100, 40, 0]}}, {'count': 0, 'entities': []})
        self.assertEqual(pad[3:], [80, 40])
        self.assertEqual(len(commands), 2)
        self.assertTrue(all(cmd.startswith('camperpad inspect CodexTest ') for cmd in commands))
        self.assertFalse(pad[0] - 24 <= 0 <= pad[0] + 24 and pad[1] - 44 <= 0 <= pad[1] + 44)

    def test_search_is_bounded_and_contradictory_ready_proof_refuses(self):
        suite = Suite.__new__(Suite); suite.report = {}; count = []
        def diagnostic(command, marker):
            count.append(command)
            x, z, _, length, width = map(int, command.split()[3:])
            return dict(kind='CAMPER_PAD_READ_ONLY_PREFLIGHT', xMin=x-width//2, zMin=z-length//2,
                width=width, length=length, status='FAIL_NO_WORLD_CHANGES', blockers=[], mutationLimitExceeded=False)
        suite.diagnostic = diagnostic
        arguments = ({'position': [0, 40, 0]}, {'player': {'position': [100, 40, 0]}}, {'count': 0, 'entities': []})
        with self.assertRaisesRegex(RuntimeError, 'Bounded'):
            suite.find_pad(*arguments)
        self.assertEqual(len(count), 32)
        suite.report = {}
        def contradictory(command, marker):
            data = diagnostic(command, marker)
            data.update(status='READY_NO_WORLD_CHANGES', blockers=[{'reason': 'container'}])
            return data
        suite.diagnostic = contradictory
        with self.assertRaisesRegex(RuntimeError, 'contradictory'):
            suite.find_pad(*arguments)

    def test_second_instance_or_missing_owned_process_refuses_before_bridge_input(self):
        suite = Suite.__new__(Suite)
        suite.pid = 21
        calls = []
        def raw(path, **params):
            calls.append(path)
            return {'pid': 21, 'state': 'ingame', 'mainThreadStallMs': 0} if path == '/ping' else {'world': {'gameName': 'CodexTest'}}
        suite.raw = raw
        for processes in (set(), {22}, {21, 22}):
            with patch('run_camper_qualification.running_game_pids', return_value=processes):
                with self.assertRaisesRegex(RuntimeError, 'sole instance'):
                    suite.identity()
            self.assertEqual(calls, [])
        with patch('run_camper_qualification.running_game_pids', return_value={21}):
            suite.identity()
        self.assertEqual(calls, ['/ping', '/state'])

    def test_auto_pad_includes_local_player_and_dead_entities_in_closed_margin(self):
        ground = {'position': [0, 40, 0]}
        state = {'player': {'position': [23, 40, -43]}}
        entities = {'count': 0, 'entities': []}
        self.assertEqual(choose_empty_pad(ground, state, entities), [-2, 0, 0, 80, 40])
        state = {'player': {'position': [100, 40, 0]}}
        entities = {'count': 1, 'entities': [{'position': [23, 40, 0], 'dead': True}]}
        self.assertEqual(choose_empty_pad(ground, state, entities), [-2, 0, 0, 80, 40])
        # The boundary is inclusive: -1 remains blocked by the corpse.
        # No corpse deletion is substituted.

    def test_auto_pad_refuses_truncated_nonfinite_or_fully_occupied_ground(self):
        ground = {'position': [0, 40, 0]}
        state = {'player': {'position': [0, 40, 0]}}
        with self.assertRaisesRegex(RuntimeError, 'entity-free'):
            choose_empty_pad(ground, state, {'count': 2, 'entities': [{'position': [-20, 40, 0]}, {'position': [20, 40, 0]}]})
        for entities in ({'count': 1, 'entities': []},
                         {'count': 1, 'entities': [{'position': [float('nan'), 40, 0]}]}):
            with self.assertRaises(RuntimeError):
                choose_empty_pad(ground, state, entities)

    def test_player_relocation_planning_never_excludes_other_occupants(self):
        ground = {'position': [0, 40, 0]}
        state = {'player': {'position': [0, 40, 0]}}
        self.assertEqual(choose_empty_pad(ground, state, {'count': 0, 'entities': []}, allow_player_move=True), [0, 0, 0, 80, 40])
        with self.assertRaisesRegex(RuntimeError, 'entity-free'):
            choose_empty_pad(ground, state, {'count': 2, 'entities': [{'position': [-20, 40, 0], 'dead': True}, {'position': [20, 40, 0], 'dead': True}]}, allow_player_move=True)
        # Moving the real player outside28m allows the same candidate through
        # the default gate; a failed/no movement still refuses that gate.
        moved = {'player': {'position': [28, 40, 0]}}
        self.assertEqual(choose_empty_pad(ground, moved, {'count': 0, 'entities': []}), [0, 0, 0, 80, 40])

    def test_recorded_loot_bag_is_preserved_by_nearby_lateral_selection(self):
        ground = {'position': [-314.8, 40, 1794]}
        state = {'player': {'position': [-295.777, 40.456, 1753.011]}}
        entities = {'count': 1, 'entities': [{'position': [-310.363, 40.773, 1794.642], 'dead': True}]}
        self.assertEqual(choose_empty_pad(ground, state, entities), [-335, 1794, 0, 80, 40])

    def test_mount_requires_exact_empty_vehicle_and_measured_stationary_physics(self):
        row = {'entityId': 12, 'entityClass': 'rebirthCamper80s', 'speed': 0,
            'rigidbodyState': {'angularVelocity': {'x': 0, 'y': 0, 'z': 0}},
            'seats': [{'occupantId': -1, 'attachedToCamper': False}]}
        self.assertTrue(parked_ready(row, 12))
        self.assertFalse(parked_ready(dict(row, speed=.2), 12))
        for changed in (dict(row, entityId=13), dict(row, seats=[]),
                        dict(row, seats=[{'occupantId': 22, 'attachedToCamper': True}]),
                        dict(row, rigidbodyState={})):
            with self.assertRaises(RuntimeError):
                parked_ready(changed, 12)
    def test_native_corpses_are_not_live_threats_and_incomplete_scan_refused(self):
        require_no_live_zombies({'count': 2, 'entities': [{'dead': True, 'health': 0}, {'dead': False, 'health': 0}]})
        for reply in ({'count': 1, 'entities': [{'dead': False, 'health': 10}]},
                      {'count': 2, 'entities': [{'dead': True}]},
                      {'count': 1, 'entities': [{}]}):
            with self.assertRaises(RuntimeError):
                require_no_live_zombies(reply)

    def test_arrival_clears_and_restores_before_guard_suppression(self):
        calls = []
        def act(path, **params):
            calls.append(path)
            return {'remaining': []} if path == '/cleararea' else {'bleed': False}
        prepare_safe_arrival(act, lambda *args, **params: {'count': 1, 'entities': [{'dead': True, 'health': 0}]})
        self.assertEqual(calls, ['/enemyspawns', '/cleararea', '/restore', '/guard'])
    def test_waits_for_real_routine_completion_and_two_idle_samples(self):
        ticks = [0.0]
        snapshots = []
        statuses = iter([True, False, True, False, False])
        def status():
            return dict(enabled=False, heal=False, reload=False, bleed=False,
                needs={'enabled': False, 'supply': False}, busy=next(statuses))
        wait_quiescent(status, snapshots.append, clock=lambda: ticks[0], pause=lambda seconds: ticks.__setitem__(0, ticks[0] + seconds))
        self.assertEqual([row['busy'] for row in snapshots], [True, False, True, False, False])

    def test_stuck_or_unobservable_routine_aborts_before_setup(self):
        def idle():
            return dict(enabled=False, heal=False, reload=False, bleed=False,
                needs={'enabled': False, 'supply': False}, busy=False)
        ticks = [0.0]
        def busy():
            value = idle(); value['busy'] = True; return value
        with self.assertRaisesRegex(RuntimeError, 'quiescent'):
            wait_quiescent(busy, lambda value: None, timeout=1, clock=lambda: ticks[0],
                pause=lambda seconds: ticks.__setitem__(0, ticks[0] + seconds))
        for field in ('busy', 'bleed', 'needs'):
            value = idle(); value.pop(field)
            with self.assertRaises(RuntimeError):
                wait_quiescent(lambda: value, lambda row: None)

    def test_replacement_or_incomplete_session_refused(self):
        original = {'pid': 12, 'port': 54001, 'token': 'owned-token'}
        self.assertTrue(same_session(dict(original), original))
        self.assertFalse(same_session({}, {}))
        for key in original:
            changed = dict(original)
            changed[key] = 13 if key == 'pid' else 54002 if key == 'port' else 'replacement'
            self.assertFalse(same_session(changed, original))
        self.assertFalse(same_session(original, {'pid': True, 'port': 54001, 'token': 'owned-token'}))

    def test_receipt_must_be_unique_successful_existing_and_confined(self):
        with tempfile.TemporaryDirectory() as folder:
            allowed = Path(folder) / 'captures'
            allowed.mkdir()
            capture = allowed / 'result.json'
            capture.write_text('{}')
            def reply(lines, error=False):
                return {'hadError': error, 'results': [{'output': lines}]}
            line = '[CamperTrack] PREPARED ' + str(capture)
            self.assertEqual(receipt_path(reply([line]), '[CamperTrack] PREPARED ', allowed), capture.resolve())
            invalid = [reply([line, line]), reply([line], True), reply(['*** ERROR refused', line]),
                reply(['[CamperTrack] PREPARED ' + str(allowed.parent / 'outside.json')]),
                reply(['[CamperTrack] PREPARED ' + str(allowed / 'absent.json')])]
            for value in invalid:
                with self.assertRaises(RuntimeError):
                    receipt_path(value, '[CamperTrack] PREPARED ', allowed)


if __name__ == '__main__':
    unittest.main()
