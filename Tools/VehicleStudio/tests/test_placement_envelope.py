import copy
import json
import sys
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from camper_integration import placement_envelope


class PlacementEnvelope(unittest.TestCase):
    def test_actual_manifest_root_centred_rear_and_anchored_height(self):
        report = json.loads((ROOT / 'reports/playable_build/cfcbc9c3ed5c4666925018aaf4ddfe88/result.json').read_text(encoding='utf-8-sig'))
        box = placement_envelope(report)
        for axis in ('x', 'z'):
            low = report['boundsCenter'][axis] - report['boundsSize'][axis]/2
            high = report['boundsCenter'][axis] + report['boundsSize'][axis]/2
            self.assertLessEqual(-box[axis]/2, low-.1)
            self.assertGreaterEqual(box[axis]/2, high+.1)
        self.assertAlmostEqual(box['z'], 7.128857803344727)
        top = report['boundsCenter']['y'] + report['boundsSize']['y']/2
        self.assertAlmostEqual(.05 + box['y'], top + .1)
        self.assertGreaterEqual(.14 + report['boundsCenter']['y'] - report['boundsSize']['y']/2, 0)
        item = ET.parse(ROOT.parents[1] / 'Config/_Vehicles/camper_items.xml').getroot()
        value = item.find("append/item/property[@class='Action1']/property[@name='VehicleSize']").get('value')
        for actual, axis in zip(map(float, value.split(',')), ('x', 'y', 'z')):
            self.assertAlmostEqual(actual, box[axis], places=6)

    def test_malformed_nonfinite_and_below_ground_fail_separately(self):
        valid = {'boundsCenter': dict(x=0,y=1,z=0), 'boundsSize': dict(x=2,y=2,z=4)}
        malformed = copy.deepcopy(valid); malformed['boundsSize'].pop('z')
        with self.assertRaisesRegex(ValueError, 'Malformed'): placement_envelope(malformed)
        nonfinite = copy.deepcopy(valid); nonfinite['boundsCenter']['z'] = float('nan')
        with self.assertRaisesRegex(ValueError, 'Nonfinite'): placement_envelope(nonfinite)
        below = copy.deepcopy(valid); below['boundsCenter']['y'] = 0
        with self.assertRaisesRegex(ValueError, 'ground clearance'): placement_envelope(below)


if __name__ == '__main__':
    unittest.main()
