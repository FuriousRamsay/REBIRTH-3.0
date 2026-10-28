import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from prepare_cabin_seating import validate_stage


def layout(count, steering='0,-60,0'):
    root = ET.Element('configs')
    vehicle = ET.SubElement(ET.SubElement(root, 'append', xpath='/vehicles'), 'vehicle', name='rebirthCamper80s')
    ET.SubElement(vehicle, 'property', name='steering', value=steering)
    for slot in range(count):
        seat = ET.SubElement(vehicle, 'property', {'class': f'seat{slot}'})
        for name, value in {'pose': '40' if slot == 0 else '41', 'position': f'{slot},0,0',
                            'rotation': '0,0,0', 'exit': '1,0,0'}.items():
            ET.SubElement(seat, 'property', name=name, value=value)
    return ET.tostring(root)


class CabinRestage(unittest.TestCase):
    def validate(self, baseline, candidate):
        with tempfile.TemporaryDirectory() as folder:
            folder = Path(folder)
            (folder/'baseline_camper_vehicles.xml').write_bytes(baseline)
            (folder/'camper_vehicles.xml').write_bytes(candidate)
            receipt = {'status': 'STAGED_FOUR_OCCUPANT_CANDIDATE_REQUIRES_AVATAR_FIT',
                       'capacity': 4, 'installed': False, 'visualAcceptance': False,
                       'sourceSha256': hashlib.sha256(baseline).hexdigest(),
                       'candidateSha256': hashlib.sha256(candidate).hexdigest()}
            path = folder/'result.json'
            path.write_text(json.dumps(receipt))
            return validate_stage(path)[0]

    def test_two_four_and_six_seat_sources_can_restage_legacy_candidate(self):
        for count in (2, 4, 6):
            self.assertEqual(self.validate(layout(count), layout(4))['capacity'], 4)

    def test_other_capacity_and_front_or_steering_changes_refused(self):
        for baseline, candidate in ((layout(5), layout(4)), (layout(4), layout(4, '0,-90,0')),
                                    (layout(4), layout(4).replace(b'value="0,0,0"', b'value="1,0,0"', 1))):
            with self.assertRaises(ValueError):
                self.validate(baseline, candidate)
