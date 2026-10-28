import copy
import math
import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from validate_camper_seats import verify_native_steering


def capture(angle=0):
    return {'steering': {'localQuaternion': dict(zip(('x','y','z','w'), (0, math.sin(angle/2), 0, math.cos(angle/2)))),
                         'pivotVehicleLocal': dict(x=0,y=1,z=2), 'meshCenterVehicleLocal': dict(x=0,y=1,z=2)},
            'seats': [{'slot': 0, 'isLocalPlayer': True, 'ikFrames': [{'targets': [
                {'goal': goal, 'hasTransform': True, 'targetPath': 'M/SteeringWheel/IKHand'+hand}
                for goal,hand in (('LeftHand','L'),('RightHand','R'))]}]}]}


class NativeSteeringEvidence(unittest.TestCase):
    def test_native_shaft_left_and_right(self):
        for angle in (-.4,.4):
            verify_native_steering(capture(), capture(angle), held=True)

    def test_static_rim_wrong_axis_moving_center_and_wrong_binding_refused(self):
        bad = [capture()]
        axis = capture(.4); axis['steering']['localQuaternion']['x'] = axis['steering']['localQuaternion'].pop('y'); axis['steering']['localQuaternion']['y']=0; bad.append(axis)
        moved = capture(.4); moved['steering']['meshCenterVehicleLocal']['x']=.02; bad.append(moved)
        binding = capture(.4); binding['seats'][0]['ikFrames'][0]['targets'][0]['targetPath']='detached/IKHandL'; bad.append(binding)
        for state in bad:
            with self.assertRaises(RuntimeError):
                verify_native_steering(capture(),state,held=True)


if __name__ == '__main__':
    unittest.main()
