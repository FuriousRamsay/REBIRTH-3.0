import copy
import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from capture_simultaneous_camper import require_six_players


def occupied():
    names = ('Hips', 'Head', 'LeftHand', 'RightHand', 'LeftFoot', 'RightFoot')
    data = {'entityId': 12, 'attachMaxCount': 6, 'speed': 0,
            'productionBootstrap': {'state': 'Complete', 'lastFailure': '', 'moduleInstalled': True},
            'seats': [{'slot': i, 'configuredPose': 40 if i == 0 else 41,
                       'runtimePose': 40 if i == 0 else 41, 'occupantId': 100 + i,
                       'isPlayer': True, 'occupantAlive': True, 'attachedToCamper': True,
                       'isLocalPlayer': i == 0,
                       'bones': [{'name': name, 'vehicleLocal': {'x': 0, 'y': 1, 'z': 0}}
                                 for name in names]} for i in range(6)]}
    data['seats'][4]['bedPose'] = {'status': 'APPLIED_UNQUALIFIED', 'occupantId': 104,
        'camperId': 12, 'nativeSlot': 4, 'nativeMembership': True, 'clipHumanMotion': False,
        'clipGenericMotion': True, 'nativeRigCompatible': True,
        'clip': 'rebirthCamperBedLying',
        'controllerPreserved': True, 'nativeBedIKTargetsAbsent': True,
        'bodyAnimatorCount': 2, 'activeGraphs': 2}
    return data


class SimultaneousOccupants(unittest.TestCase):
    def test_complete_distinct_actual_players_and_stable_seat_order(self):
        data = occupied()
        self.assertEqual(require_six_players(data), list(range(100, 106)))
        data['seats'].reverse()
        self.assertEqual(require_six_players(data, list(range(100, 106))), list(range(100, 106)))

    def test_empty_dead_fake_duplicate_detached_or_replaced_players_refused(self):
        for field, value in (('occupantId', -1), ('occupantAlive', False),
                             ('isPlayer', False), ('occupantId', 100),
                             ('attachedToCamper', False), ('runtimePose', 42)):
            data = occupied(); data['seats'][5][field] = value
            with self.assertRaises(RuntimeError):
                require_six_players(data)
        data = occupied(); data['seats'][5]['occupantId'] = 999
        with self.assertRaises(RuntimeError):
            require_six_players(data, list(range(100, 106)))

    def test_missing_ambiguous_or_nonfinite_bones_and_motion_refused(self):
        cases = []
        data = occupied(); data['seats'][2]['bones'].pop(); cases.append(data)
        data = occupied(); data['seats'][2]['bones'].append(copy.deepcopy(data['seats'][2]['bones'][0])); cases.append(data)
        data = occupied(); data['seats'][2]['bones'][0]['vehicleLocal']['x'] = float('nan'); cases.append(data)
        data = occupied(); data['speed'] = .2; cases.append(data)
        data = occupied(); data['seats'][1]['isLocalPlayer'] = True; cases.append(data)
        for data in cases:
            with self.assertRaises(RuntimeError):
                require_six_players(data)

    def test_bed_clip_must_apply_to_all_actual_bodies_without_controller_replacement(self):
        for field, value in (('status', 'NOT_APPLIED'), ('activeGraphs', 1),
                             ('controllerPreserved', False), ('clipHumanMotion', True),
                             ('clipGenericMotion', False), ('nativeRigCompatible', False),
                             ('clip', 'unrelatedGenericClip'),
                             ('camperId', 13), ('occupantId', 105), ('nativeBedIKTargetsAbsent', False)):
            data = occupied(); data['seats'][4]['bedPose'][field] = value
            with self.assertRaises(RuntimeError):
                require_six_players(data)

    def test_missing_generic_or_native_layout_proof_is_not_accepted(self):
        for field in ('clipGenericMotion', 'nativeRigCompatible', 'clipHumanMotion'):
            data = occupied(); del data['seats'][4]['bedPose'][field]
            with self.assertRaises(RuntimeError):
                require_six_players(data)


if __name__ == '__main__':
    unittest.main()
