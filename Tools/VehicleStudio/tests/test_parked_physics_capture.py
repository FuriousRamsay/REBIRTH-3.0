import copy
import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from capture_parked_camper import check_parked
from run_camper_qualification import same_session


def parked():
    return {'entityId': 42, 'entityClass': 'rebirthCamper80s', 'speed': 0,
            'position': dict(x=1,y=2,z=3), 'rigidbodyState': {'angularVelocity': dict(x=0,y=0,z=0)},
            'seats': [{'occupantId': -1, 'attachedToCamper': False} for _ in range(4)]}


class ParkedPhysicsCapture(unittest.TestCase):
    def test_exact_entity_and_session_identity(self):
        check_parked(parked(), 42)
        with self.assertRaises(RuntimeError):
            check_parked(parked(), 43)
        session = {'pid': 10, 'port': 8765, 'token': 'owned'}
        self.assertTrue(same_session(dict(session), session))
        for key, value in (('pid', 11), ('port', 8766), ('token', 'replaced')):
            self.assertFalse(same_session(dict(session, **{key: value}), session))

    def test_runaway_or_any_occupant_or_missing_explicit_empty_seat_refused(self):
        baseline = parked()['position']
        bad = []
        speed = parked(); speed['speed'] = 1.1; bad.append(speed)
        spin = parked(); spin['rigidbodyState']['angularVelocity']['y'] = 1.1; bad.append(spin)
        drift = parked(); drift['position']['x'] += 1.1; bad.append(drift)
        seated = parked(); seated['seats'][3]['occupantId'] = 10; bad.append(seated)
        unknown = parked(); unknown['seats'][0].pop('occupantId'); bad.append(unknown)
        for state in bad:
            with self.assertRaises(RuntimeError):
                check_parked(state, 42, baseline)


if __name__ == '__main__':
    unittest.main()
