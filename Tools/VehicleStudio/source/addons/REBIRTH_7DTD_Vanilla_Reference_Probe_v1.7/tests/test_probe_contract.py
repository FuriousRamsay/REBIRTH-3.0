import copy
import json
from pathlib import Path
import sys
import unittest
import zipfile
import tempfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
from validate_probe_report import validate_report, read_probe, triple

class ProbeContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.result = json.loads((ROOT / 'samples' / 'SYNTHETIC_probe_result.json').read_text())
        cls.capture = json.loads((ROOT / 'samples' / 'SYNTHETIC_donor_xml_capture.json').read_text())

    def test_synthetic_fixture_passes_with_unverified_label(self):
        res = validate_report(self.result, self.capture)
        self.assertEqual(res['status'], 'XML_ONLY_VALIDATION_PASSED_NOT_RUNTIME_VALIDATION')

    def test_wrong_status_rejected(self):
        result = copy.deepcopy(self.result)
        result['status'] = 'PASS_GAMEPLAY_VERIFIED'
        with self.assertRaises(ValueError): validate_report(result, self.capture)

    def test_false_runtime_claim_rejected(self):
        result = copy.deepcopy(self.result)
        result['recordedCharacterBones'] = True
        with self.assertRaises(ValueError): validate_report(result, self.capture)

    def test_id_mismatch_rejected(self):
        cfg = copy.deepcopy(self.capture)
        cfg['xmlConfig']['name'] = 'differentVehicle'
        with self.assertRaises(ValueError): validate_report(self.result, cfg)

    def test_duplicate_seat_rejected(self):
        cfg = copy.deepcopy(self.capture)
        cfg['xmlConfig']['seats'][1]['index'] = 0
        with self.assertRaises(ValueError): validate_report(self.result, cfg)

    def test_bad_coordinates_rejected(self):
        cfg = copy.deepcopy(self.capture)
        cfg['xmlConfig']['seats'][0]['position'] = 'NaN,0,0'
        with self.assertRaises(ValueError): validate_report(self.result, cfg)

    def test_missing_driver_rejected(self):
        cfg = copy.deepcopy(self.capture)
        cfg['xmlConfig']['seats'][0]['index'] = 3
        with self.assertRaises(ValueError): validate_report(self.result, cfg)

    def test_count_disagreement_rejected(self):
        result = copy.deepcopy(self.result)
        result['wheelsFound'] = 7
        with self.assertRaises(ValueError): validate_report(result, self.capture)

    def test_zip_root_report_is_readable(self):
        with tempfile.TemporaryDirectory() as d:
            zpath = Path(d) / 'reports.zip'
            with zipfile.ZipFile(zpath,'w') as z:
                z.writestr('result.json', json.dumps(self.result))
                z.writestr('donor_xml_capture.json', json.dumps(self.capture))
            result, capture = read_probe(zpath)
            self.assertEqual(validate_report(result,capture)['seats'],2)

    def test_triple_scientific_notation(self):
        self.assertTrue(triple('.5, -2e-1, 0'))
        self.assertFalse(triple('NaN,0,0'))

if __name__ == '__main__': unittest.main()
