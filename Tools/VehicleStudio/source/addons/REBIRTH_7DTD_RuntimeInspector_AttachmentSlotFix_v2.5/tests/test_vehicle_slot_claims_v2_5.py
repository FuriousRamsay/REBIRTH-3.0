"""Functional validator tests for v2.5's new seat evidence format."""
import copy
import importlib.util
import json
import unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sp=importlib.util.spec_from_file_location('v25validator', ROOT/'tools/validate_capture.py')
validator=importlib.util.module_from_spec(sp);sp.loader.exec_module(validator)

class VehicleSlotClaims(unittest.TestCase):
    def setUp(self):
        self.sample=json.loads((ROOT/'tests/fixtures/synthetic_complete.json').read_text())
        self.sample['inspectorVersion']='0.7.0-v2.5'
        self.sample['scenarioLabel']='seated'
        self.sample['transformChecks']=[{'className':'wheel0','property':'tireTransform','expectedPath':'Wheel0','found':True,'classification':'required','severity':'pass'}]
        self.sample['transformCheckCount']=1
        self.sample['unresolvedRequiredTransformCount']=0
        self.sample['unresolvedConditionalUpgradeTransformCount']=0
        self.sample['frames'][0]['occupantCandidates']=[{'entityId':171,
             'association':'runtimeVehicleReference_verifiedVehicle_notSeat',
             'reportedSeatIndex':0,
             'reportedSeatStatus':'verified_vehicle_attachment_slot_native_pose',
             'reportedSeatSource':'vehicle.GetAttached(0)+Vehicle.GetSeatPose(0)+player.vehiclePoseMode',
             'observedPoseMode':40,
             'seatEvidence':[{'member':'GetAttached(0)','source':'vehicleMethod','rawValue':'same_player_reference;xmlPose=40;nativePose=40;playerPose=40','assessment':'verified_vehicle_attachment_slot_native_pose'}],
             'bones':[]}]
        self.sample['occupantCandidateSamples']=1
        self.sample['vehicleAttachmentSamples']=1
        self.sample['knownSeatSamples']=1
    def test_good_explicit_slot_claim_accepted(self):
        errors,_,counts=validator.validate_capture(self.sample)
        self.assertEqual(errors,[])
        self.assertEqual(counts['knownSeatSamples'],1)
    def test_pose_only_not_counted(self):
        d=copy.deepcopy(self.sample)
        d['frames'][0]['occupantCandidates'][0]['seatEvidence']=[]
        self.assertIn('unverified_vehicle_slot_claimed',validator.validate_capture(d)[0])
    def test_different_slot_evidence_rejected(self):
        d=copy.deepcopy(self.sample)
        d['frames'][0]['occupantCandidates'][0]['seatEvidence'][0]['member']='GetAttached(1)'
        self.assertIn('unverified_vehicle_slot_claimed',validator.validate_capture(d)[0])
    def test_claim_requires_exact_slot_source(self):
        d=copy.deepcopy(self.sample)
        d['frames'][0]['occupantCandidates'][0]['reportedSeatSource']='pose_only_40'
        self.assertIn('unverified_vehicle_slot_claimed',validator.validate_capture(d)[0])
    def test_slot_unverified_does_not_count(self):
        d=copy.deepcopy(self.sample)
        o=d['frames'][0]['occupantCandidates'][0]
        o['reportedSeatIndex']=-1;o['reportedSeatStatus']='attached_vehicle_but_no_authoritative_slot_index'
        d['knownSeatSamples']=0
        errors,warnings,_=validator.validate_capture(d)
        self.assertEqual(errors,[])
        self.assertIn('no_verified_seat_index',warnings)
    def test_claim_count_cannot_be_falsified(self):
        d=copy.deepcopy(self.sample)
        d['knownSeatSamples']=2
        self.assertIn('declared_knownSeatSamples_mismatch',validator.validate_capture(d)[0])

if __name__=='__main__':unittest.main()
