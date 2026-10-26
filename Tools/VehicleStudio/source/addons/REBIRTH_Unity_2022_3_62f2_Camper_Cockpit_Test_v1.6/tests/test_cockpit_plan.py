import copy
import json
import pathlib
import sys
import unittest
ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
from camper_cockpit_plan import draft, dist, vec


def contracts():
    p = ROOT / 'evidence'
    return [json.loads((p / name).read_text()) for name in (
        'prefab_audit.json','camper_wheel_contract.json','camper_cockpit_contract.json')]

class DraftTests(unittest.TestCase):
    def setUp(self):
        self.audit, self.wheels, self.evidence = contracts()
    def generate(self):
        return draft(self.audit,self.wheels,self.evidence)
    def test_real_camper_accepted(self):
        r=self.generate();self.assertEqual(r['wheelCount'],4)
    def test_real_source_hash_locked(self):
        self.assertEqual(self.generate()['sourceSha256'],self.evidence['sourceSha256'])
    def test_pose_not_invented(self):
        self.assertEqual([s['poseId'] for s in self.generate()['seatCandidates']],['',''])
    def test_hand_ik_never_enabled(self):
        self.assertFalse(self.generate()['handIkEnabled'])
    def test_foot_ik_never_enabled(self):
        self.assertFalse(self.generate()['footIkEnabled'])
    def test_no_exit_guesses(self):
        self.assertEqual(self.generate()['exitAttempts'],[])
    def test_steering_pivot_from_audit(self):
        c=self.generate()['steeringWheel']['originalVisualPivot'];self.assertAlmostEqual(c['x'],-.413,3)
    def test_tilt_is_recorded(self):
        self.assertGreater(self.generate()['steeringWheel']['rotationEulerFromAudit']['x'],280)
    def test_driver_is_left_of_centre(self):
        self.assertLess(self.generate()['seatCandidates'][0]['position']['x'],0)
    def test_passenger_is_other_side(self):
        s=self.generate()['seatCandidates'];self.assertAlmostEqual(s[0]['position']['x'],-s[1]['position']['x'])
    def test_grip_span_based_on_measured_rim(self):
        self.assertAlmostEqual(self.generate()['suggestedGripSpacing'],.4443776607513428*.5*.75)
    def test_wrong_source_rejected(self):
        self.evidence['sourceAsset']='different.prefab'
        with self.assertRaises(ValueError):self.generate()
    def test_wrong_unity_rejected(self):
        self.evidence['unityVersion']='2020.3'
        with self.assertRaises(ValueError):self.generate()
    def test_changed_importer_rejected(self):
        self.evidence['sourceImporterMetaSha256']='0'*64
        with self.assertRaises(ValueError):self.generate()
    def test_different_geo_count_rejected(self):
        self.audit['geometry'].pop()
        with self.assertRaises(ValueError):self.generate()
    def test_wrong_steering_center_rejected(self):
        self.evidence['steeringExpectedCenter']['x']=200
        with self.assertRaises(ValueError):self.generate()
    def test_missing_wheel_rejected(self):
        self.wheels['wheels'].pop()
        with self.assertRaises(ValueError):self.generate()
    def test_ambiguous_wheel_rejected(self):
        self.audit['geometry'].append(self.audit['geometry'][-1]);
        with self.assertRaises(ValueError):self.generate()
    def test_nonfinite_coordinate_rejected(self):
        self.evidence['steeringExpectedPivot']['x']='NaN'
        with self.assertRaises(ValueError):self.generate()
    def test_candidate_position_is_provisional(self):
        r=self.generate();self.assertEqual(r['status'],'PROVISIONAL_NOT_7DTD_VERIFIED')
    def test_preview_steering_joint_named(self):
        self.assertTrue(self.generate()['steeringWheel']['pivotPath'].endswith('_UNVERIFIED'))
    def test_source_contains_no_bundle_or_xml_export(self):
        c=(ROOT/'UnitySources/REBIRTHQuickSmoke/Editor/RebirthUnityCamperCockpitSmoke.cs').read_text()
        self.assertNotIn('BuildPipeline.BuildAssetBundles(',c)
        self.assertNotIn('RebirthVehicleXmlExporter.',c)
    def test_script_mentions_current_unity(self):
        self.assertIn('2022.3.62f2',(ROOT/'Run_Camper_Cockpit_Test.ps1').read_text())
    def test_csharp_scene_open_script(self):
        c=(ROOT/'UnitySources/REBIRTHQuickSmoke/Editor/RebirthUnityCamperCockpitSmoke.cs').read_text()
        self.assertIn('EditorSceneManager.SaveScene',c)
        self.assertIn('RebirthVehicleRigWorkspace',c)
    def test_v15_source_retained_not_overwritten(self):
        script=(ROOT/'Run_Camper_Cockpit_Test.ps1').read_text()
        self.assertIn('Different script already exists',script)
    def test_report_maintains_unverified_flags(self):
        s=(ROOT/'UnitySources/REBIRTHQuickSmoke/Editor/RebirthUnityCamperCockpitSmoke.cs').read_text()
        self.assertIn('driverPoseVerified = false',s)
        self.assertIn('handIkGameVerified = false',s)
    def test_source_preserves_hashes(self):
        s=(ROOT/'UnitySources/REBIRTHQuickSmoke/Editor/RebirthUnityCamperCockpitSmoke.cs').read_text()
        self.assertIn('originalWheelPreviewUnchanged',s)
    def test_no_unknown_csharp_dependencies(self):
        s=(ROOT/'UnitySources/REBIRTHQuickSmoke/Editor/RebirthUnityCamperCockpitSmoke.cs').read_text()
        self.assertNotIn('using Newtonsoft',s)
        self.assertNotIn('UnityEngine.AddressableAssets',s)

if __name__=='__main__':unittest.main()
# Additional tests below are discovered through the same unittest module.
class PackagingSafetyTests(unittest.TestCase):
    def test_no_regex_slash_replacement(self):
        text=(ROOT/'Run_Camper_Cockpit_Test.ps1').read_text()
        self.assertNotIn("-replace '/',",text)
        self.assertIn("$expectedPreview.Replace('/',",text)
    def test_imported_source_hash_preflight(self):
        text=(ROOT/'Run_Camper_Cockpit_Test.ps1').read_text()
        self.assertIn('Get-FileHash',text)
        self.assertIn('sourceImporterMetaSha256',text)
    def test_v15_report_success_provenance(self):
        text=(ROOT/'Run_Camper_Cockpit_Test.ps1').read_text()
        self.assertIn('PASS_GEOMETRY_PREVIEW_NEEDS_REVIEW',text)
        self.assertIn('$wheelResult.wheelsAttached -ne 4',text)
    def test_success_output_disables_deployment(self):
        text=(ROOT/'UnitySources/REBIRTHQuickSmoke/Editor/RebirthUnityCamperCockpitSmoke.cs').read_text()
        self.assertIn('modXmlExported = false',text)
        self.assertIn('assetBundleBuilt = false',text)
    def test_raw_profiler_data_not_required(self):
        text=(ROOT/'Run_Camper_Cockpit_Test.ps1').read_text()
        self.assertNotIn('ModInfo.xml',text)
    def test_editor_profile_exporter_has_menu(self):
        text=(ROOT/'UnitySources/REBIRTHQuickSmoke/Editor/RebirthCamperProfileExporter.cs').read_text()
        self.assertIn('Export Selected Camper Profile JSON',text)
        self.assertIn('NOT_GAME_VERIFIED',text)
