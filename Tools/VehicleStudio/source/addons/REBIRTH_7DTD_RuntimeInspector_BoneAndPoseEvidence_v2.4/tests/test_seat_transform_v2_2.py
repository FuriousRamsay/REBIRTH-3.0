"""Regression for runtime V2.2 evidence model and unresolved upgrade handling.

These are offline source/DTO contract checks, not 7DTD API/compilation tests.
"""
import copy
import importlib.util
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'src/VehicleInspector'
TOOL = ROOT / 'tools/analyze_capture_v2_2.py'
VALIDATOR = ROOT / 'tools/validate_capture.py'


def import_module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

ana = import_module(TOOL, 'capture_summary')
val = import_module(VALIDATOR, 'capture_validator')


def sample():
    d = json.loads((ROOT / 'tests/fixtures/synthetic_complete.json').read_text())
    d['inspectorVersion'] = '0.7.0-v2.4'
    d['transformChecks'] = [
        {'className':'wheel0','property':'tireTransform','expectedPath':'Wheel0','found':True,'classification':'required','severity':'pass'},
        {'className':'plow','property':'modRBT','expectedPath':'Plow','found':False,'classification':'conditional_upgrade','severity':'review_conditional_upgrade'},
    ]
    d['transformCheckCount'] = 2
    d['unresolvedRequiredTransformCount'] = 0
    d['unresolvedConditionalUpgradeTransformCount'] = 1
    return d


class SeatAndTransformTests(unittest.TestCase):
    def test_required_wheels_are_not_optional(self):
        d=sample()
        d['transformChecks'][0]['found']=False
        d['unresolvedRequiredTransformCount']=1
        errs,warns,_=val.validate_capture(d)
        self.assertEqual(errs,[])
        self.assertIn('missing_required_transforms:1',warns)

    def test_conditional_plow_not_required(self):
        d=sample()
        errs,warns,_=val.validate_capture(d)
        self.assertEqual(errs,[])
        self.assertIn('unresolved_optional_upgrade_transforms:1',warns)
        sm=ana.summarize(d)
        self.assertEqual(len(sm['unresolvedConditionalUpgrades']),1)
        self.assertEqual(sm['unresolvedRequired'],[])

    def test_mismatched_upgrade_count_rejected(self):
        d=sample()
        d['unresolvedConditionalUpgradeTransformCount']=0
        self.assertIn('conditional_upgrade_transform_count_mismatch',val.validate_capture(d)[0])

    def test_v22_requires_explicit_transform_classification(self):
        d=sample()
        del d['transformChecks'][1]['classification']
        self.assertIn('unclassified_v2_2_transform',val.validate_capture(d)[0])

    def test_vehicle_attachment_without_seat_is_not_verified(self):
        d=sample()
        d['scenarioLabel']='seated'
        d['frames'][0]['occupantCandidates']=[{'entityId':171,'association':'runtimeVehicleReference_verifiedVehicle_notSeat',
           'reportedSeatIndex':-1,'reportedSeatStatus':'attached_vehicle_but_no_authoritative_slot_index',
           'seatEvidence':[],'bones':[]}]
        d['occupantCandidateSamples']=1;d['vehicleAttachmentSamples']=1
        errs,warns,_=val.validate_capture(d)
        self.assertEqual(errs,[])
        self.assertIn('no_verified_seat_index',warns)
        self.assertFalse(ana.summarize(d)['occupants'][0]['seatVerified'])

    def test_false_seat_index_rejected_even_if_number_valid(self):
        d=sample()
        d['frames'][0]['occupantCandidates']=[{'entityId':171,'association':'runtimeVehicleReference_verifiedVehicle_notSeat',
           'reportedSeatIndex':0,'reportedSeatStatus':'possible_slot_unverified_member',
           'reportedSeatSource':None,'seatEvidence':[],'bones':[]}]
        d['occupantCandidateSamples']=1;d['vehicleAttachmentSamples']=1
        self.assertIn('unverified_seat_index_claimed',val.validate_capture(d)[0])

    def test_verified_seat_requires_source_and_status(self):
        d=sample()
        d['frames'][0]['occupantCandidates']=[{'entityId':171,'association':'runtimeVehicleReference_verifiedVehicle_notSeat',
           'reportedSeatIndex':0,'reportedSeatStatus':'verified_player_slot',
           'reportedSeatSource':'playerMember.AttachedToEntitySlotIdx','seatEvidence':[], 'bones':[]}]
        d['occupantCandidateSamples']=1;d['vehicleAttachmentSamples']=1;d['knownSeatSamples']=1
        self.assertEqual(val.validate_capture(d)[0],[])
        self.assertTrue(ana.summarize(d)['occupants'][0]['seatVerified'])

    def test_nonspecific_slot_not_trusted_by_source(self):
        s=(SRC/'Capture/SeatMemberProbe.cs').read_text()
        self.assertIn('TrustedAttachmentSlotMember(member)',s)
        self.assertIn('possible_slot_unverified_member',s)
        self.assertIn('conflicting_explicit_slot_members_no_index_assigned',s)

    def test_private_inherited_slots_checked(self):
        s=(SRC/'Util/Reflect.cs').read_text()
        self.assertIn('scan = scan.BaseType',s)
        self.assertIn('BindingFlags.DeclaredOnly',s)

    def test_seatdiag_is_exposed(self):
        s=(SRC/'Commands/ConsoleCmdVehicleInspector.cs').read_text()
        self.assertIn('case "seatdiag"',s)
        self.assertIn('s.SeatDiagnosticsToLog()',s)
        self.assertIn('SeatMemberProbe.Hints', (SRC/'VehicleInspectorService.cs').read_text())

    def test_unknown_seat_does_not_autoselect_driver(self):
        s=(SRC/'Capture/SeatMemberProbe.cs').read_text()
        self.assertIn('occupant.reportedSeatIndex=-1;',s)
        self.assertIn('attached_vehicle_but_no_authoritative_slot_index',s)
        self.assertNotIn('occupant.reportedSeatIndex=0;',s)

    def test_plow_classification_requires_mod_property(self):
        s=(SRC/'Capture/TransformLinkChecks.cs').read_text()
        self.assertIn('partProp.key=="mod"',s)
        self.assertIn('"conditional_upgrade"',s)
        self.assertIn('"required"',s)

    def test_version_capture_fields_serialized(self):
        s=(SRC/'Model/Records.cs').read_text()
        for field in ['reportedSeatStatus','reportedSeatSource','seatEvidence',
                      'classification, severity','unresolvedRequiredTransformCount',
                      'unresolvedConditionalUpgradeTransformCount']:
            self.assertIn(field,s)
        self.assertIn('RebirthCaptureFields/1', (SRC/'VehicleInspectorService.cs').read_text())

    def test_collector_skips_prior_inspector(self):
        s=(ROOT/'Collect_RuntimeInspector_Captures.ps1').read_text()
        self.assertIn("$obj.inspectorVersion -ne '0.7.0-v2.4'",s)
        self.assertIn('requiredMissing',s)


if __name__=='__main__': unittest.main()
