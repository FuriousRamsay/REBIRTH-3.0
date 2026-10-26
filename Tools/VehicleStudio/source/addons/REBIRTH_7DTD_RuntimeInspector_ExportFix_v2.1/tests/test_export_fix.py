import json
import sys
import unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
from validate_capture import validate_capture

class RuntimeExportFixTests(unittest.TestCase):
    def setUp(self):
        self.good=json.loads((ROOT/'tests/fixtures/synthetic_complete.json').read_text())
        self.bad=json.loads((ROOT/'tests/fixtures/synthetic_metadata_only.json').read_text())
        self.serializer=(ROOT/'src/VehicleInspector/Util/CaptureJsonWriter.cs').read_text()
        self.service=(ROOT/'src/VehicleInspector/VehicleInspectorService.cs').read_text()
    def test_metadata_only_capture_rejected(self):
        errors,_,_=validate_capture(self.bad)
        self.assertIn('hierarchy_missing_or_empty',errors)
        self.assertIn('frames_missing_or_empty',errors)
        self.assertIn('missing_nested_records_serializer_marker',errors)
    def test_full_synthetic_capture_passes_structure(self):
        errors,_,counts=validate_capture(self.good)
        self.assertEqual(errors,[])
        self.assertEqual(counts['hierarchyNodes'],1)
    def test_motion_must_have_multiple_frames(self):
        self.good['captureType']='motion'
        self.assertIn('motion_has_fewer_than_two_frames',validate_capture(self.good)[0])
    def test_bones_not_required_for_empty_capture(self):
        self.assertNotIn('no_occupant_bones',validate_capture(self.good)[1])
    def test_missing_seated_attachment_is_warning(self):
        self.good['scenarioLabel']='seated'
        errors,warnings,_=validate_capture(self.good)
        self.assertEqual(errors,[])
        self.assertIn('no_verified_seat_index',warnings)
    def test_observed_seat_claim_requires_attachment(self):
        self.good['scenarioLabel']='seated'
        occupant={'entityId':5,'reportedSeatIndex':0,'association':'nearbyCandidate_not_verifiedSeatOccupant','bones':[]}
        self.good['frames'][0]['occupantCandidates']=[occupant]
        self.good['occupantCandidateSamples']=1
        _,warnings,counts=validate_capture(self.good)
        self.assertEqual(counts['knownSeatSamples'],0)
        self.assertIn('no_verified_seat_index',warnings)
    def test_wrong_declared_counts_fail(self):
        self.good['frameCount']=10
        self.assertIn('declared_frameCount_mismatch',validate_capture(self.good)[0])
    def test_nested_serialization_explicit(self):
        self.assertIn('CaptureJsonWriter.Write(writer,cap)',self.service)
        self.assertNotIn('JsonUtility.ToJson(cap',self.service)
        self.assertIn('writer.Write(\'{\')',self.serializer)
        self.assertIn('type.GetFields(BindingFlags.Instance|BindingFlags.Public)',self.serializer)
        self.assertIn('WriteValue(writer,field.GetValue(value),depth+1)',self.serializer)
    def test_atomic_file_cleanup_on_writer_failure(self):
        self.assertIn('if(File.Exists(temp)) File.Delete(temp)',self.service)
    def test_restrict_serializer_to_model_namespace(self):
        self.assertIn('RebirthVehicleInspector.Model',self.serializer)
        self.assertIn('MaxDepth',self.serializer)
        self.assertIn('Non-finite float',self.serializer)
    def test_collector_rejects_legacy_records(self):
        code=(ROOT/'Collect_RuntimeInspector_Captures.ps1').read_text()
        self.assertIn('RebirthCaptureFields/1',code)
        self.assertIn('hierarchy/frames missing or empty',code)
        self.assertIn('knownSeatSamples',code)
    def test_guarded_upgrade_and_rollback(self):
        code=(ROOT/'Run_RuntimeInspector_ExportFix.ps1').read_text()
        self.assertIn('ConfirmEacOffAndTestWorld',code)
        self.assertIn('owned developer modlet',code)
        self.assertIn('REBIRTH_DEV_INSTALL.sha256',code)
        self.assertIn('WaitForExit(1000)',code)
        self.assertIn('RebirthVehicleInspector.dll',code)
        self.assertIn('Copy-Item -LiteralPath',code)
        self.assertIn('BACKUP',code.upper())
    def test_src_version_and_fields(self):
        rec=(ROOT/'src/VehicleInspector/Model/Records.cs').read_text()
        self.assertIn('inspectorVersion = "0.6.0"',rec)
        for name in ('hierarchyNodeCount','frameCount','occupantBoneSamples','knownSeatSamples'):
            self.assertIn(name,rec)
    def test_build_only_mode_installs_nothing(self):
        code=(ROOT/'Run_RuntimeInspector_ExportFix.ps1').read_text()
        self.assertIn('if($Mode -eq \'Build\')',code)
        self.assertIn('BUILD_PASS_NOT_INSTALLED',code)
        self.assertIn('UPGRADE_PASS_RESTART_GAME_TO_TEST',code)

if __name__=='__main__':unittest.main()
