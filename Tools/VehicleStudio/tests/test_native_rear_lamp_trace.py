import copy
import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from compare_vehicle_sequences import validate_native_rear_lamps


def trace():
    lights = [{'path': 'Model/M/RearLights/' + side, 'active': True, 'enabled': True, 'intensity': 1.5,
               'color': [1,0,0]} for side in ('Left', 'Right')]
    return {'samples': [{'phaseIndex': phase, 'time': index + phase, 'braking': phase == 3,
        'forwardVelocity': -.2 if phase == 4 else 0, 'lights': copy.deepcopy(lights)} for phase in (3,4) for index in (0,.1)]}


class NativeRearLampTrace(unittest.TestCase):
    def test_actual_brake_and_reverse_have_two_red_native_emitters(self):
        self.assertEqual(set(validate_native_rear_lamps(trace())), {'braking', 'reverse'})

    def test_forward_only_dark_or_white_reverse_cannot_pass(self):
        bad = []
        forward = trace()
        for sample in forward['samples']: sample['forwardVelocity'] = 0
        bad.append(forward)
        dark = trace(); dark['samples'][-1]['lights'][0]['enabled'] = False; bad.append(dark)
        white = trace(); white['samples'][-1]['lights'][0]['color'] = [1,1,1]; bad.append(white)
        for data in bad:
            with self.assertRaises(ValueError): validate_native_rear_lamps(data)


if __name__ == '__main__':
    unittest.main()
