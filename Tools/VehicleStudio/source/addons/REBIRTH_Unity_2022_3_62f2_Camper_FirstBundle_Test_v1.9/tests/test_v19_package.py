import hashlib
import json
import pathlib
import re
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
REF = json.loads((ROOT / 'Reference/VERIFIED_4X4_DRIVER_REFERENCE.json').read_text())
PS = (ROOT / 'Run_Camper_FirstBundle.ps1').read_text()
CS = (ROOT / 'UnitySources/RebirthUnityCamperFirstBundleTest.cs').read_text()

class SafetyAndEvidence(unittest.TestCase):
    def test_verified_slot(self):
        self.assertEqual(REF['driverSeatIndex'], 0)
        self.assertEqual(REF['driverPoseId'], 40)
        self.assertIn('GetAttached(0)', REF['driverSeatVerification'])
        self.assertIn('GetSeatPose(0)', REF['driverSeatVerification'])
    def test_both_sides(self):
        for key in ('LeftHand','RightHand','LeftFoot','RightFoot','LeftLeg','RightLeg'):
            self.assertIn(key,REF['driverBonesVehicleLocal'])
    def test_required_paths(self):
        self.assertEqual(REF['requiredTransformMissing'],0)
        self.assertIn('Plow',[x['path'] for x in REF['unresolvedOptionalDetails']])
    def test_capture_sha(self):
        self.assertRegex(REF['sourceArchiveSha256'],r'^[a-f0-9]{64}$')
    def test_exact_unity_version(self):
        self.assertIn('2022.3.62f2',PS)
        self.assertIn('2022.3.62f2',CS)
    def test_run_id_result_guard(self):
        self.assertIn('$r.runId -ceq $runId', PS)
        self.assertIn('File.Exists(r.bundleReceipt)',CS)
    def test_precise_wheel_paths(self):
        for wheel in ('Wheel0','Wheel1','Wheel2','Wheel3'):
            self.assertIn('"'+wheel+'"',CS)
        self.assertIn('M/SteeringWheel',CS)
    def test_no_deploy(self):
        self.assertNotIn('Copy-Item -LiteralPath $r.bundlePath',PS)
        self.assertNotIn('Mods\\REBIRTH',PS)
        self.assertIn('No XML, localization, REBIRTH or 7DTD files changed.',PS)
    def test_preserved_sources(self):
        for marker in ('sourceCockpitSha256','sourceProfileSha256','originalAssetsUnchanged'):
            self.assertIn(marker,CS)
    def test_both_seats(self):
        self.assertIn('prior.seats[0].poseId!="40"',CS)
        self.assertIn('prior.seats[1].poseId!="41"',CS)
    def test_no_ik_assumptions(self):
        self.assertIn('clone.ik[0].hasLeftHand=false',CS)
        self.assertIn('clone.ik[0].hasRightHand=false',CS)
    def test_live_console(self):
        self.assertIn('WaitForExit(10000)',PS)
        self.assertIn('STATUS: FIRST_CAMPER_BUNDLE_PASS_NOT_GAME_TESTED',PS)
        self.assertIn('STATUS: FIRST_CAMPER_BUNDLE_FAILED',PS)
    def test_scripts_have_balanced_structural_tokens(self):
        # Source-level heuristic only, NOT a C# or PowerShell compiler.
        self.assertEqual(CS.count('{'),CS.count('}'))
    def test_archive_contract(self):
        self.assertTrue((ROOT/'README.md').is_file())
        self.assertTrue((ROOT/'Run_Camper_FirstBundle.ps1').is_file())
        self.assertTrue((ROOT/'UnitySources/RebirthUnityCamperFirstBundleTest.cs').is_file())
if __name__=='__main__': unittest.main()
