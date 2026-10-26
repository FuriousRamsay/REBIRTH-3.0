"""Offline code contracts and 7DTD live-evidence fixture validation.
These tests do NOT compile the C# or verify a real game's slot mapping.
"""
import json
import re
import unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
SLOT=(ROOT/'src/VehicleInspector/Capture/AttachmentSlotProbe.cs').read_text()
SEAT=(ROOT/'src/VehicleInspector/Capture/SeatMemberProbe.cs').read_text()
MODEL=(ROOT/'src/VehicleInspector/Model/Records.cs').read_text()
COLLECT=(ROOT/'Collect_RuntimeInspector_Captures.ps1').read_text()
DIAG=(ROOT/'src/VehicleInspector/Util/SeatDiagnosticLog.cs').read_text()
SAMPLE=json.loads((ROOT/'evidence'/'V24_LIVE_CAPTURE_SUMMARY.json').read_text())

class AttachmentSlotTests(unittest.TestCase):
    def test_only_allowlisted_getters_invoked(self):
        self.assertIn('FindIntGetter(vehicle, "GetAttached")',SLOT)
        self.assertIn('FindIntGetter(vehicleComponent, "GetSeatPose")',SLOT)
        self.assertNotIn('"FindAttachSlot"',SLOT)
        self.assertNotIn('"AttachEntityToSelf"',SLOT)
        self.assertNotIn('"SetSeats"',SLOT)
    def test_requires_reference_equality_not_entity_id(self):
        self.assertIn('Object.ReferenceEquals(attached, player)',SLOT)
        self.assertNotIn('Reflect.Id(attached)==',SLOT)
    def test_paired_runtime_pose_matches_required(self):
        self.assertIn('expectedPose == nativePose && occupant.observedPoseMode == expectedPose',SLOT)
        self.assertIn('direct_attachment_slot_but_pose_corroboration_incomplete',SLOT)
        self.assertIn('verified_vehicle_attachment_slot_native_pose',SLOT)
    def test_bound_seats_and_verified_vehicle_required(self):
        self.assertIn('config.seats.Length == 0',SLOT)
        self.assertIn('config.status, "ok"',SLOT)
        self.assertIn('StartsWith("runtimeVehicleReference_"',SLOT)
    def test_bounded_to_configured_slots_and_caps_count(self):
        self.assertIn('MaxSlotsToInspect = 16',SLOT)
        self.assertIn('foreach (SeatConfig seat in config.seats)',SLOT)
        self.assertIn('seat.index < 0',SLOT)
        self.assertIn('getAttached',SLOT)
    def test_duplicate_reference_match_refused(self):
        self.assertIn('matching.Count > 1',SLOT)
        self.assertIn('valid.Clear()',SLOT)
        self.assertIn('vehicle_getattached_same_player_in_multiple_slots_no_slot_verified',SLOT)
    def test_attach_slot_cannot_be_inferred_from_pose_alone(self):
        self.assertIn('occupant.poseMatchedSeatIndex=found;',SEAT)
        self.assertNotIn('occupant.reportedSeatIndex=found;',SEAT)
        self.assertIn('AttachmentSlotProbe.Scan(',SEAT)
        self.assertLess(SEAT.index('RecordPoseMatch(player'), SEAT.index('AttachmentSlotProbe.Scan(player'))
    def test_pose_only_from_actual_capture_marked_inference(self):
        self.assertEqual(SAMPLE['seat']['observedPose'],40)
        self.assertEqual(SAMPLE['seat']['poseMatchedSeatIndex'],0)
        self.assertEqual(SAMPLE['seat']['reportedSeatIndex'],-1)
        self.assertEqual(SAMPLE['seat']['verifiedSeatSamples'],0)
        self.assertEqual(SAMPLE['seat']['seatEvidence'],0)
    def test_bilateral_capture_landmarks(self):
        n=SAMPLE['seated']['landmarks']
        for k in ('Hips','LeftHand','RightHand','LeftFoot','RightFoot','LeftLeg','RightLeg','LeftUpLeg','RightUpLeg'):
            self.assertIn(k,n)
            self.assertEqual(set(n[k]),{'x','y','z'})
    def test_essential_transforms_cleared_optional_plow_classified(self):
        self.assertEqual(SAMPLE['unresolvedRequired'],0)
        self.assertEqual(SAMPLE['unresolvedOptional'],[{'property':'modRBT','path':'Plow'}])
    def test_legacy_capture_ignored_by_new_collector(self):
        self.assertIn("0.7.0-v2.5",COLLECT)
        self.assertIn('seat diagnostics v2.5',COLLECT)
        self.assertIn('seat diagnostics v2.5',DIAG)
        self.assertIn('"0.7.0-v2.5"',MODEL)
    def test_runtime_counts_new_verified_slot_status(self):
        s=(ROOT/'src/VehicleInspector/VehicleInspectorService.cs').read_text()
        self.assertIn('o.reportedSeatStatus=="verified_vehicle_attachment_slot_native_pose"',s)
        self.assertIn('knownSeat++',s)
    def test_restore_and_upgrade_safety_preserved(self):
        s=(ROOT/'Run_RuntimeInspector_ExportFix.ps1').read_text()
        for term in ['REBIRTH_DEV_INSTALL.sha256','Previous DLL backup:',"[ValidateSet('Build','Upgrade')]",'dotnet build exit code:']:
            self.assertIn(term,s)
        self.assertIn('UPGRADE_PASS_RESTART_GAME_TO_TEST',s)

if __name__=='__main__': unittest.main()
