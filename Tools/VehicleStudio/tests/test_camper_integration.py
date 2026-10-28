import importlib.util
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('camper_integration', ROOT / 'camper_integration.py')
camper = importlib.util.module_from_spec(spec)
spec.loader.exec_module(camper)


class IntegrationGuards(unittest.TestCase):
    def test_localization_append_preserves_uncommitted_rows(self):
        prior = b'Key,english\r\nuserPending,A pending user change\r\n'
        new, addition = camper.localization_append(prior)
        self.assertTrue(new.startswith(prior))
        self.assertEqual(new.replace(addition, b'', 1), prior)

    def test_duplicate_localization_blocks_deployment(self):
        with self.assertRaisesRegex(ValueError, 'already exists'):
            camper.localization_append(('Key,english\n' + camper.ITEM + ',Existing\n').encode())

    def test_include_preserves_unrelated_xml_exactly(self):
        prior = b'<configs>\r\n  <!-- user pending -->\r\n  <include filename="_Purge/items.xml"/>\r\n</configs>\r\n'
        new, addition = camper.add_include(prior, '_Vehicles/camper_items.xml')
        self.assertEqual(new.replace(addition, b'', 1), prior)
        self.assertEqual(ET.fromstring(new).findall('include')[-1].get('filename'), '_Vehicles/camper_items.xml')

    def test_duplicate_include_is_rejected(self):
        prior = b'<configs><include filename="_Vehicles/camper_items.xml"/></configs>'
        with self.assertRaisesRegex(ValueError, 'already exists'):
            camper.add_include(prior, '_Vehicles/camper_items.xml')

    def test_rollback_preserves_later_localization_work(self):
        with tempfile.TemporaryDirectory() as folder:
            mod = Path(folder)
            dest = mod / 'Config/Localization.csv'
            dest.parent.mkdir()
            prior = b'Key,english\nuserPending,A pending user change\n'
            new, addition = camper.localization_append(prior)
            dest.write_bytes(new + b'anotherAgent,Later work\n')
            camper.rollback_ops(mod, [{'path': 'Config/Localization.csv', 'mode': 'localization', 'addition': addition.decode(), 'beforeSha256': camper.sha(prior)}])
            self.assertEqual(dest.read_bytes(), prior + b'anotherAgent,Later work\n')

    def test_rollback_refuses_changed_owned_bundle(self):
        with tempfile.TemporaryDirectory() as folder:
            mod = Path(folder)
            dest = mod / 'Resources/camper.unity3d'
            dest.parent.mkdir()
            dest.write_bytes(b'foreign replacement')
            with self.assertRaisesRegex(ValueError, 'modified after deployment'):
                camper.rollback_ops(mod, [{'path': 'Resources/camper.unity3d', 'mode': 'new', 'afterSha256': camper.sha(b'original')}])
            self.assertEqual(dest.read_bytes(), b'foreign replacement')

    def test_actual_native_donor_has_exactly_two_camper_seats_after_conversion(self):
        game = ROOT.parents[3]
        report = {
            'seats': [{'index': i, 'pose': 40 + i, 'position': dict(x=0, y=0, z=0),
                       'rotation': dict(x=0, y=0, z=0), 'exits': [dict(x=-1.7, y=0, z=0)]} for i in (0, 1)],
            'prefabName': 'rebirthCamper80s', 'transformPaths': ['M/VisualBody/80sCamperTruck/headlights_glass'],
            'boundsCenter': dict(x=0, y=1.32, z=0), 'boundsSize': dict(x=2.54, y=2.64, z=5.84),
            **{k: dict(x=0, y=0, z=0) for k in ('handLeftPosition', 'handRightPosition', 'handLeftRotation', 'handRightRotation')},
        }
        result = camper.definitions(game, report)
        vehicle = ET.fromstring(result['Config/_Vehicles/camper_vehicles.xml']).find('append/vehicle')
        seats = [p.get('class') for p in vehicle.findall('property[@class]') if p.get('class').startswith('seat')]
        self.assertEqual(seats, ['seat0', 'seat1'])
        self.assertIsNone(vehicle.find("property[@class='plow']"))
        self.assertEqual([vehicle.find(f"property[@class='seat{i}']/property[@name='pose']").get('value') for i in (0, 1)], ['40', '41'])
        item = ET.fromstring(result['Config/_Vehicles/camper_items.xml']).find('append/item')
        self.assertEqual(item.get('name'), camper.VEHICLE + 'Placeable')
        self.assertNotIn('vseat', item.find("property[@name='Tags']").get('value').split(','))
        for prop in vehicle.findall('property/property'):
            self.assertNotIn(prop.get('name'), ('modT', 'modRBT', 'modHideT', 'modOnT'))

    def test_actual_unity_reopen_report_passes_new_rear_lamp_staging_contract(self):
        import json
        report = json.loads((ROOT / 'reports/bundle_reopen/40e6c2ba563742caa47790a39fa6a973/result.json').read_text())
        camper.validate_candidate_report(report)
        report['transformPaths'].remove('M/RearLights/LeftRed')
        with self.assertRaisesRegex(ValueError, 'rear lens'):
            camper.validate_candidate_report(report)

    def test_original_failed_build_cannot_replace_successful_reopen_evidence(self):
        import json
        report = json.loads((ROOT / 'reports/playable_build/b671609321bd42d7958653cd588619f6/result.json').read_text())
        with self.assertRaisesRegex(ValueError, 'Verified Unity'):
            camper.validate_candidate_report(report)

    def test_directional_markers_require_measured_counts_and_geometry(self):
        import json
        import copy
        report = json.loads((ROOT / 'reports/bundle_reopen/40e6c2ba563742caa47790a39fa6a973/result.json').read_text())
        report.update(rendererCount=18, spotLightCount=6)
        report['directionalLamps'] = dict(lightCount=2, triangles=12, roofTriangles=8,
            redBodyTriangles=4, preservedAmberTriangles=4,
            markerPositions=[dict(x=0,y=1,z=0)]*2, markerDirections=[dict(x=0,y=0,z=1)]*2)
        report['transformPaths'] += ['M/BodyMarkerLights', 'M/VisualBody/80sCamperTruck/lights/BodyMarkerLenses']
        camper.validate_candidate_report(report)
        for field, value in (('lightCount', 3), ('triangles', 13), ('roofTriangles', 0), ('markerDirections', [])):
            changed = copy.deepcopy(report); changed['directionalLamps'][field] = value
            with self.assertRaises(ValueError): camper.validate_candidate_report(changed)
        changed = copy.deepcopy(report); del changed['directionalLamps']
        with self.assertRaises(ValueError): camper.validate_candidate_report(changed)
        changed = copy.deepcopy(report); changed['spotLightCount'] = 4
        with self.assertRaises(ValueError): camper.validate_candidate_report(changed)

    def test_authored_rear_seats_are_preserved_without_inheriting_donor_positions(self):
        report = {
            'seats': [{'index': i, 'pose': 40 if i == 0 else 41,
                       'position': dict(x=i * .1, y=.2, z=-i), 'rotation': dict(x=0, y=90 if i > 1 else 0, z=0),
                       'exits': [dict(x=1.7, y=0, z=-2)]} for i in range(8)],
            'prefabName': 'rebirthCamper80s', 'transformPaths': ['M/VisualBody/80sCamperTruck/headlights_glass'],
            'boundsCenter': dict(x=0, y=1.32, z=0), 'boundsSize': dict(x=2.54, y=2.64, z=5.84),
            **{k: dict(x=0, y=0, z=0) for k in ('handLeftPosition', 'handRightPosition', 'handLeftRotation', 'handRightRotation')},
        }
        result = camper.definitions(ROOT.parents[3], report)
        vehicle = ET.fromstring(result['Config/_Vehicles/camper_vehicles.xml']).find('append/vehicle')
        seats = [p for p in vehicle.findall('property[@class]') if p.get('class').startswith('seat')]
        self.assertEqual(len(seats), 8)  # Conversion capability; does not assert eight occupants fit.
        for index in range(8):
            part = vehicle.find(f"property[@class='seat{index}']")
            self.assertEqual(part.find("property[@name='position']").get('value'), camper.vector(report['seats'][index]['position']))
            self.assertEqual(part.find("property[@name='pose']").get('value'), str(40 if index == 0 else 41))
            for prop in part.findall('property'):
                self.assertNotIn(prop.get('name'), ('modT', 'modRBT', 'modHideT', 'modOnT'))
        report['seats'][2]['index'] = 1
        with self.assertRaisesRegex(ValueError, 'Contiguous'):
            camper.definitions(ROOT.parents[3], report)
        report['seats'][2]['index'] = 2
        report['seats'][2]['position']['x'] = float('nan')
        with self.assertRaisesRegex(ValueError, 'Finite'):
            camper.definitions(ROOT.parents[3], report)

if __name__ == '__main__':
    unittest.main()
