import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest
import zipfile
import tempfile

ROOT=Path(__file__).resolve().parents[1]
MODULE=ROOT/'tools'/'generate_donor_plan.py'
SPEC=importlib.util.spec_from_file_location('donorplan',MODULE)
P=importlib.util.module_from_spec(SPEC); SPEC.loader.exec_module(P)
REPORT=ROOT/'evidence'/'donor_capture_readonly.json'
RESULT=ROOT/'evidence'/'donor_result_readonly.json'
PLAN=ROOT/'evidence'/'camper_donor_calibration_plan.json'
SCRIPT=ROOT/'Run_Camper_DonorPose_Test.ps1'
UNITY=ROOT/'UnitySources'/'REBIRTHQuickSmoke'/'Editor'/'RebirthUnityCamperDonorPoseSmoke.cs'

class DonorPlanTests(unittest.TestCase):
    def setUp(self):
        self.plan=json.loads(PLAN.read_text())
        self.result=json.loads(RESULT.read_text())
        self.capture=json.loads(REPORT.read_text())

    def mock_zip(self, donor=None, result=None):
        with tempfile.NamedTemporaryFile(suffix='.zip',delete=False) as f:
            path=Path(f.name)
        with zipfile.ZipFile(path,'w') as z:
            z.writestr('donor_xml_capture.json',json.dumps(donor or self.capture))
            z.writestr('result.json',json.dumps(result or self.result))
        self.addCleanup(lambda: path.unlink(missing_ok=True))
        return path

    def test_actual_donor_vehicle_id(self):
        self.assertEqual(self.capture['xmlConfig']['name'],'vehicleTruck4x4')

    def test_donor_source_hash_matches_result(self):
        self.assertEqual(self.plan['donorXmlSha256'],self.result['sourceSha256'])
        self.assertEqual(self.plan['donorCaptureSha256'],hashlib.sha256(REPORT.read_bytes()).hexdigest())

    def test_pose_40_and_41(self):
        self.assertEqual((self.plan['driverPoseId'],self.plan['passengerPoseId']),('40','41'))

    def test_all_six_donor_poses_are_reference_only(self):
        self.assertEqual([d['poseId'] for d in self.plan['donorSeats']],list(map(str,range(40,46))))
        self.assertEqual(self.plan['selectedSeatCountForPreview'],2)
        self.assertEqual(self.plan['approvedSeatCount'],0)

    def test_seat_positions_exits_are_not_approved(self):
        self.assertEqual(self.plan['approvedExitCount'],0)
        self.assertTrue(all('donorExitCandidates' in s for s in self.plan['donorSeats']))
        self.assertTrue(all('donorPosition' in s for s in self.plan['donorSeats']))

    def test_hand_ik_is_reference_only(self):
        self.assertFalse(self.plan['enablesHandIk'])
        self.assertFalse(self.plan['enablesFootIk'])
        self.assertEqual(self.plan['steeringPartClass'],'handlebars')
        self.assertEqual(self.plan['donorSteeringTransformPath'],'M/SteeringWheel')
        self.assertAlmostEqual(self.plan['donorHandLeftPosition']['x'],-0.2596)
        self.assertAlmostEqual(self.plan['donorHandRightPosition']['x'],0.24465)

    def test_wheel_links_match_installed_game(self):
        wheels=self.plan['donorWheels']
        self.assertEqual(len(wheels),4)
        self.assertEqual([d['tireTransform'] for d in wheels],['Wheel0','Wheel1','Wheel2','Wheel3'])
        self.assertTrue(wheels[0]['steerTransform'] and wheels[1]['steerTransform'])
        self.assertFalse(wheels[2]['steerTransform'] or wheels[3]['steerTransform'])

    def test_reject_tampered_donor_pose(self):
        bad=copy.deepcopy(self.capture)
        for p in bad['xmlConfig']['parts']:
            if p['className']=='seat0':
                next(x for x in p['properties'] if x['key']=='pose')['value']='99'
        with self.assertRaises(ValueError):P.build(self.mock_zip(donor=bad))

    def test_reject_wrong_game_vehicle(self):
        bad=copy.deepcopy(self.capture)
        bad['xmlConfig']['name']='vehicleMotorcycle'
        with self.assertRaises(ValueError):P.build(self.mock_zip(donor=bad))

    def test_reject_nonfinite_ik_coordinate(self):
        bad=copy.deepcopy(self.capture)
        for p in bad['xmlConfig']['parts']:
            if p['className']=='handlebars':
                next(x for x in p['properties'] if x['key']=='IKHandLPosition')['value']='nan,0,0'
        with self.assertRaises(ValueError):P.build(self.mock_zip(donor=bad))

    def test_regenerate_plan_against_real_capture(self):
        generated=P.build(self.mock_zip())
        without_digest=copy.deepcopy(generated)
        without_digest.pop('donorCaptureSha256')
        old=copy.deepcopy(self.plan);old.pop('donorCaptureSha256')
        self.assertEqual(without_digest,old)

    def test_runner_checks_game_xml_sha(self):
        s=SCRIPT.read_text(encoding='utf-8')
        self.assertIn('Get-FileHash -LiteralPath $gameXml -Algorithm SHA256',s)
        self.assertIn('donorXmlSha256',s)
        self.assertIn('Start-Process',s)
        self.assertIn('-Wait -PassThru',s)

    def test_runner_does_not_modify_rebirth_or_game_files(self):
        s=SCRIPT.read_text(encoding='utf-8')
        self.assertNotIn('Copy-Item -LiteralPath $sourceCs -Destination $gameXml',s)
        self.assertNotIn('Mods\\REBIRTH',s)
        self.assertIn('REBIRTHQuickSmoke\\Editor',s)
        self.assertIn('A DIFFERENT C# script already exists, refusing overwrite',s)

    def test_unity_clones_profile_not_altering_original(self):
        s=UNITY.read_text()
        self.assertIn('UnityEngine.Object.Instantiate(sourceProfile)',s)
        self.assertIn('clone.seats[0].poseId = plan.driverPoseId',s)
        self.assertIn('clone.seats[1].poseId = plan.passengerPoseId',s)
        self.assertNotIn('clone.seats[0].position = plan.donorSeats[0].donorPosition',s)
        self.assertIn('clone.seats[0].position != expectedDriver',s)
        self.assertIn('HashFile(oldSceneDisk) == oldSceneSha',s)
        self.assertIn('SaveScene(scene, result.sceneAsset, true)',s)

    def test_unity_does_not_enable_ik_or_build_bundle(self):
        s=UNITY.read_text()
        self.assertIn('clone.ik[0].hasLeftHand || clone.ik[0].hasRightHand',s)
        self.assertNotIn('clone.ik[0].hasLeftHand = true',s)
        self.assertNotIn('BuildPipeline.BuildAssetBundles(',s)
        self.assertNotIn('Install-REBIRTH',s)

    def test_unity_reports_real_failure_on_exception(self):
        s=UNITY.read_text()
        self.assertIn('result.status = "FAIL"',s)
        self.assertIn('File.WriteAllText(Path.Combine(reportDir, "result.json")',s)

if __name__=='__main__':unittest.main()
