from pathlib import Path
import ast
import json
import unittest

ROOT=Path(__file__).resolve().parents[3]
SOURCE=(ROOT/'Scripts/GameBridge/RebirthGameBridgeVehiclePartsQualification.cs').read_text()


class QualificationScope(unittest.TestCase):
    def cleanup_function(self,observations):
        batch=ast.parse((ROOT/'Tools/GameBridge/test_vehicle_parts_batch.py').read_text())
        park=next(n for n in ast.walk(batch) if isinstance(n,ast.FunctionDef) and n.name=='park')
        rows=iter(observations);calls=[]
        class Bridge:
            def call(self,*args,**kwargs):calls.append((args,kwargs))
        def need(value,message):
            if not value:raise AssertionError(message)
        scope={'observe':lambda entity:next(rows),'bounded':lambda:None,'need':need,'r':Bridge()}
        exec(compile(ast.Module(body=[park],type_ignores=[]),'actual_batch_cleanup','exec'),scope)
        return scope['park'],calls

    def test_cleanup_on_foot_parked_sends_no_input(self):
        row={'localPlayerAttachedEntityId':-1,'driver':-1,'forwardVelocity':0}
        park,calls=self.cleanup_function([row])
        self.assertEqual(park(245),row)
        self.assertEqual(calls,[])

    def test_cleanup_lost_moving_vehicle_sends_no_input(self):
        park,calls=self.cleanup_function([{'localPlayerAttachedEntityId':-1,'driver':-1,'forwardVelocity':2}])
        with self.assertRaisesRegex(AssertionError,'driver lost'):park(245)
        self.assertEqual(calls,[])

    def test_cleanup_lost_seat_during_braking_sends_no_input(self):
        park,calls=self.cleanup_function([
            {'localPlayerAttachedEntityId':245,'driver':12,'forwardVelocity':2},
            {'localPlayerAttachedEntityId':999,'driver':-1,'forwardVelocity':2}])
        with self.assertRaisesRegex(AssertionError,'driver lost during braking'):park(245)
        self.assertEqual(calls,[])

    def test_actual_native_policy_and_damage(self):
        self.assertIn('RebirthVehicleConditionIntegration.IsOperational(',SOURCE)
        self.assertIn('RebirthVehiclePerformanceCompiler.Compile(',SOURCE)
        self.assertIn('entity.ApplyDamage(int.Parse(args[2]))',SOURCE)
        self.assertNotIn('RebirthVehicleConditionIntegration.Damage(',SOURCE)
        self.assertIn('RebirthVehicleAssemblyIndex.CommitEntity(entity,candidate)',SOURCE)

    def test_local_disposable_explicit_fixture_scope(self):
        for token in ('sender.RemoteClientInfo!=null','world.IsRemote()','!RebirthGameBridge.IsRunning',
                      'GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"','confirmCodexTest','!entity.HasDriver'):
            self.assertIn(token,SOURCE)
        observe=SOURCE.split('private static JObject Observe(',1)[1]
        self.assertNotIn('CommitEntity(',observe)
        self.assertNotIn('Resolve(',observe)

    def test_live_selftest_scenario_parses(self):
        scenario=json.loads((ROOT/'Tools/GameBridge/tests/vehicle_parts_native_selftest.json').read_text())
        self.assertEqual(scenario['steps'][0]['console'],'rbvehiclepartsqual selftest')
        self.assertTrue(scenario['steps'][-1]['expectNoErrors'])

    def test_paired_scope_and_all_base_families(self):
        self.assertIn('type=="rebirthCamper80s"||type=="vehicleTruck4x4"',SOURCE)
        self.assertIn('legacy Q1 zero roll rerolled',SOURCE)
        for content in ('BaseBicycle','BaseMinibike','BaseMotorcycle','Base4x4','BaseGyrocopter'):
            self.assertIn('content.ContentId!="'+content+'"',SOURCE)
        import sys
        sys.path.insert(0,str(ROOT/'Tools/GameBridge'))
        from test_vehicle_parts_batch import qualification
        self.assertTrue(qualification({'results':[{'output':['{"ok":true,"entity":245}']} ]})['ok'])
        with self.assertRaises(AssertionError):qualification({'results':[{'output':['*** ERROR unavailable']} ]})

    def test_run_only_guarded_preparation(self):
        batch=(ROOT/'Tools/GameBridge/test_vehicle_parts_batch.py').read_text()
        self.assertIn("if a.mode=='Run':\n            report['cleararea']=r.call('/cleararea','POST',radius=40)",batch)
        self.assertIn("report['restore']=r.call('/restore','POST')",batch)
        self.assertIn("r.call('/guard')['enabled'] is True",batch)
        self.assertNotIn("r.call('/guard','POST'",batch)
        self.assertNotIn("r.call('/console','POST',cmd='quit",batch)


if __name__=='__main__':unittest.main()
