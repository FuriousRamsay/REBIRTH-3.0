"""Offline contract tests; live Unity and 7DTD compile remain separate acceptance gates."""
import hashlib, importlib.util, io, json, tempfile, unittest, zipfile
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT/'src'/'VehicleInspector'
spec=importlib.util.spec_from_file_location('poseanalyzer', ROOT/'tools'/'analyze_pose_reference.py')
pose=importlib.util.module_from_spec(spec)
spec.loader.exec_module(pose)

def make_zip(path, pose_slots=(40,41), wrong_hash=False, backslash=False):
    bones=[]
    hierarchy=[]
    def tr(name, at, prefix='$vehicle/Player_171/Skeleton'):
        return {'path':prefix+'/'+name,'pose':{'vehiclePosition':dict(zip('xyz',at))}}
    for n,v in {'Hips':(-.38,1.45,-.14),'LeftHand':(-.55,2.,.2),'RightHand':(-.35,1.6,.1),
                'LeftFoot':(-.57,1.2,.65),'RightFoot':(-.27,1.2,.67)}.items():
        hierarchy.append(tr(n,v));bones.append({'bone':n})
    hierarchy += [tr('IKHandL',(-.55,2.,.2),'$vehicle/M/SteeringWheel'),tr('IKHandR',(-.35,1.6,.1),'$vehicle/M/SteeringWheel')]
    rec={'captureType':'snapshot','scenarioLabel':'seated','hierarchy':hierarchy,'frames':[{'occupantCandidates':[{
        'association':'runtimeVehicleReference_verifiedVehicle_notSeat','associationSource':'AttachedToEntity',
        'reportedSeatIndex':-1,'bones':bones}]}],
        'knownSeatSamples':0,'unresolvedRequiredTransformCount':0,
        'unresolvedConditionalUpgradeTransformCount':1,
        'xmlConfig':{'seats':[{'index':i,'pose':str(v)} for i,v in enumerate(pose_slots)]}}
    raw=json.dumps(rec).encode()
    sha=hashlib.sha256(raw).hexdigest()
    diag=b'REBIRTH Vehicle Inspector seat diagnostics v2.3\ntrigger=command\n  player.EntityAlive.vehiclePoseMode : Int32 = 40\nCandidate #171 attached=yes seat=-1\n'
    diagsha=hashlib.sha256(diag).hexdigest()
    manifest={'files':[{'name':'seated.json','sha256':'0'*64 if wrong_hash else sha}],
              'diagnostics':[{'name':'seatdiag_123.txt','sha256':diagsha}]}
    with zipfile.ZipFile(path,'w') as z:
        z.writestr('seated.json',raw)
        z.writestr('Diagnostics\\seatdiag_123.txt' if backslash else 'Diagnostics/seatdiag_123.txt',diag)
        z.writestr('manifest.json',json.dumps(manifest))

class BilateralPoseTests(unittest.TestCase):
    def test_both_lower_limbs_prioritized_before_fingers(self):
        s=(SRC/'Capture/OccupantCapture.cs').read_text()
        for name in ['"Hips"','"LeftUpLeg"','"RightUpLeg"','"LeftFoot"','"RightFoot"']:
            self.assertIn(name,s)
        self.assertLess(s.index('foreach(string name in PriorityMeshBones)'),s.index('foreach(Transform bone in candidates)\n                    {'))
        self.assertIn('MaxBones=72',s)
        self.assertIn('seated_body_landmark_missing:',s)
    def test_pose_match_is_non_authoritative(self):
        s=(SRC/'Capture/SeatMemberProbe.cs').read_text()
        self.assertIn('unique_pose_in_bound_xml_not_authoritative_slot',s)
        self.assertIn('occupant.poseMatchedSeatIndex=found;',s)
        self.assertNotIn('occupant.reportedSeatIndex=found;',s)
        self.assertIn('occupant.reportedSeatIndex=-1;',s)
    def test_existing_slot_verification_remains_required(self):
        s=(SRC/'Capture/SeatMemberProbe.cs').read_text()
        self.assertIn('valid_player_slot_member',s)
        self.assertIn('occupant.reportedSeatStatus="verified_player_slot"',s)
        self.assertIn('AttachedToEntitySlotIdx',s)
    def test_explicit_method_hints_never_call_methods(self):
        s=(SRC/'Capture/SeatMemberProbe.cs').read_text()
        block=s[s.index('public static string[] MethodHints('):s.index('private static bool Relevant(')]
        self.assertIn('method.GetParameters()',block)
        self.assertNotIn('.Invoke(',block)
    def test_both_ik_targets_measured_and_pose_not_verified(self):
        with tempfile.TemporaryDirectory() as d:
            z=Path(d)/'sample.zip';make_zip(z, backslash=True)
            r=pose.analyze(z)
            self.assertEqual(r['snapshots'][0]['authoritativelyVerifiedSeatSamples'],0)
            self.assertEqual(r['nonAuthoritativePoseMatches'][0]['inferredSeatIndex'],0)
            self.assertEqual(r['snapshots'][0]['handToSteeringIK']['LeftHand']['distanceUnityUnits'],0)
            self.assertEqual(r['snapshots'][0]['handToSteeringIK']['RightHand']['distanceUnityUnits'],0)
    def test_ambiguous_pose_never_infers_seat(self):
        with tempfile.TemporaryDirectory() as d:
            z=Path(d)/'sample.zip';make_zip(z, pose_slots=(40,40))
            self.assertEqual(pose.analyze(z)['nonAuthoritativePoseMatches'],[])
    def test_corrupt_manifest_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            z=Path(d)/'sample.zip';make_zip(z, wrong_hash=True)
            with self.assertRaisesRegex(ValueError,'SHA-256 mismatch'):
                pose.analyze(z)
    def test_collector_marks_pose_only_as_inference(self):
        s=(ROOT/'Collect_RuntimeInspector_Captures.ps1').read_text()
        self.assertIn('verifiedSeatSamples=',s)
        self.assertIn('remains an INFERENCE',s)
        self.assertIn("0.7.0-v2.4",s)
    def test_actual_v23_derived_reference_consistent(self):
        f=ROOT/'evidence'/'ACTUAL_V23_CAPTURE_FINDINGS.json'
        j=json.loads(f.read_text())
        self.assertEqual(j['nonAuthoritativePoseMatches'][0]['poseMode'],40)
        self.assertEqual(j['nonAuthoritativePoseMatches'][0]['verification'],'unique_pose_in_xml_NOT_authoritative_seat')
        c=next(x for x in j['snapshots'] if x['scenario']=='seated')
        self.assertIn('RightFoot',c['exportedCoreBonesMissing'])
        self.assertIn('RightFoot',c['hierarchyLandmarks'])
        self.assertLess(c['handToSteeringIK']['LeftHand']['distanceUnityUnits'],.0001)
if __name__=='__main__':unittest.main()
