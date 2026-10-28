import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import prepare_cabin_seating as seating
from test_cabin_restage import layout


class SixOccupantPlan(unittest.TestCase):
    def test_missing_geometry_refuses_invented_coordinates(self):
        with self.assertRaises(ValueError):
            seating.prepare(6)

    def test_measured_six_places_preserve_baseline_and_do_not_install_fake_bed_pose(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / 'Config/_Vehicles').mkdir(parents=True)
            source = root / 'Config/_Vehicles/camper_vehicles.xml'
            original = layout(4)
            source.write_bytes(original)
            tools = root / 'tools'
            (tools / 'scenarios').mkdir(parents=True)
            (tools / 'scenarios/camper_cabin_candidates.json').write_text(json.dumps({'candidates': [
                {'name': 'front_bench_pose41', 'pose': 41, 'attachment': [.64, .384, -1.001], 'yaw': 180},
                {'name': 'rear_bench_pose41', 'pose': 41, 'attachment': [.64, .384, -1.974], 'yaw': 0}]}))
            evidence = root / 'evidence'
            evidence.write_bytes(b'fixture')
            digest = hashlib.sha256(evidence.read_bytes()).hexdigest()
            faces = []
            for y, xmin, xmax, zmin, zmax in ((1.919, -.773, .754, -.474, .262),
                                           (1.538, -.866, -.263, -1.168, -.602)):
                a, b, c, d = ([xmin, y, zmin], [xmax, y, zmin], [xmax, y, zmax], [xmin, y, zmax])
                for triangle in ((a, b, c), (a, c, d)):
                    faces.append(dict(zip(('a', 'b', 'c'), triangle), center=[sum(p[i] for p in triangle)/3 for i in range(3)]))
            geometry = root / 'geometry.json'
            geometry.write_text(json.dumps({'status': 'PASS_REFERENCE_MATCHED_FBX_HORIZONTAL_GEOMETRY_NOT_AVATAR_FIT',
                                            'referenceMaxVertexError': 0, 'source': str(evidence), 'sourceSha256': digest,
                                            'reference': str(evidence), 'referenceSha256': digest, 'horizontalFaces': faces}))
            with patch.object(seating, 'MOD', root), patch.object(seating, 'ROOT', tools):
                folder = seating.prepare(6, geometry)
            report = json.loads((folder / 'result.json').read_text())
            self.assertEqual([p['slot'] for p in report['occupants']], list(range(6)))
            self.assertEqual(source.read_bytes(), original)
            self.assertFalse(report['installable'])
            self.assertEqual(report['occupants'][4]['pose'], 41)  # Native parameter; actual authored lying overlay required.
            self.assertEqual(report['occupants'][4]['yaw'], 0)
            self.assertEqual(len(report['occupants'][4]['attachment']), 3)
            self.assertTrue(report['requiresAuthoredBedPoseBundle'])
            self.assertEqual(report['occupants'][4]['role'], 'lying_bed')
            self.assertEqual(report['occupants'][5]['role'], 'sink_adjacent_seated')
            self.assertTrue((folder / 'camper_vehicles.xml').exists())
            with self.assertRaises(ValueError):
                seating.validate_stage(folder / 'result.json')
            evidence = {'prefabVerified': True, 'bedPoseAuthored': True, 'bedPoseClipName': 'rebirthCamperBedLying',
                        'transformPaths': ['M/BedPoseAsset']}
            self.assertEqual(seating.validate_stage(folder / 'result.json', evidence)[0]['capacity'], 6)
            candidate_path = folder / 'camper_vehicles.xml'
            candidate = candidate_path.read_bytes()
            for goal in ('IKHandL', 'IKHandR', 'IKFootL', 'IKFootR'):
                for field in ('Position', 'Rotation'):
                    with self.subTest(nativeIKProperty=goal + field):
                        tree = ET.fromstring(candidate)
                        seat = tree.find("./append/vehicle/property[@class='seat4']")
                        ET.SubElement(seat, 'property', name=goal + field, value='0, 0, 0')
                        altered = ET.tostring(tree)
                        candidate_path.write_bytes(altered)
                        report['candidateSha256'] = hashlib.sha256(altered).hexdigest()
                        (folder / 'result.json').write_text(json.dumps(report))
                        with self.assertRaisesRegex(ValueError, 'native sitting hand/foot IK'):
                            seating.validate_stage(folder / 'result.json', evidence)
            candidate_path.write_bytes(candidate)
            report['candidateSha256'] = hashlib.sha256(candidate).hexdigest()
            (folder / 'result.json').write_text(json.dumps(report))
            self.assertEqual(seating.validate_stage(folder / 'result.json', evidence)[0]['capacity'], 6)



if __name__ == '__main__':
    unittest.main()
