"""Offline orchestration regression; no game process, launch, or network required."""
import copy
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import urllib.error
from unittest.mock import patch
import run_gamebridge_batch as batch


class FakeBridge:
    instances = []
    fail_acquire = False

    def __init__(self, path):
        self.path = Path('pinned-session.json')
        self.identity = {'pid': 55, 'startedUtc': 'original', 'processStartUtc': 'original', 'port': 8765, 'outputDir': 'unique'}
        self.run_token = None
        self.leased = False
        self.calls = []
        self.instances.append(self)

    def call(self, endpoint, method='GET', **query):
        self.calls.append((endpoint, method, query))
        if endpoint == '/ping':
            return {'ok': True, 'version': '1.1.0', 'state': 'ingame', 'pid': 55, 'lastLogSeq': 42, 'batch': {'uncertain': False}}
        if endpoint == '/batch':
            if self.fail_acquire:
                raise RuntimeError('exclusive owner conflict')
            self.leased = query.get('action') != 'release'
            return {'ok': True, 'runToken': 'PRIVATE_CAPABILITY', 'released': True}
        if endpoint == '/gearobserve':
            return {'cursor':{'count':0},**{k:False for k in ('gearHeld','libraryHeld','resourcePending','creationHeld','offerPending')}}
        if endpoint == '/auditstate':
            return {'domains':{'cookingAdmission':{'clientHeld':0,'ownerDeltaHeld':False}}}
        if endpoint == '/state':
            if self.leased and self.run_token != 'PRIVATE_CAPABILITY':
                raise urllib.error.HTTPError('http://fixture/state', 409, 'exclusive', {}, None)
            return {'world': {'gameName': 'CodexTest', 'gameWorld': 'fixture-world'}}
        if endpoint == '/log':
            return {'ok': True, 'count': 0, 'entries': []}
        return {'ok': True}


def case(identifier):
    return {'id': identifier, 'group': 'smoke', 'kind': 'scenario', 'enabled': True,
            'file': 'tests/mp049_reviewed_admission_smoke.json', 'requires': []}


def success(command, **kwargs):
    assert not any(word in command for word in ('launch', 'quit', 'restart', 'reset-save', 'build'))
    assert kwargs['env']['REBIRTH_BRIDGE_RUN_TOKEN'] == 'PRIVATE_CAPABILITY'
    return subprocess.CompletedProcess(command, 0, 'TEST OUTPUT\n' + json.dumps({'passed': True, 'totalSteps': 3}), '')


class BatchTests(unittest.TestCase):
    def setUp(self):
        FakeBridge.instances.clear()
        FakeBridge.fail_acquire = False
        self.temp = tempfile.TemporaryDirectory()
        self.out = patch.object(batch, 'OUT', Path(self.temp.name))
        self.out.start()
        self.plan = {'schema': 1, 'name': 'fixture', 'save': 'CodexTest', 'world': 'fixture-world', 'cases': [case('one'), case('two')]}

    def tearDown(self):
        self.out.stop()
        self.temp.cleanup()

    def execute(self, executor=success):
        return batch.run(self.plan, batch.prepare(self.plan), None, FakeBridge, executor)

    def test_two_cases_one_session_one_lease(self):
        path, report = self.execute()
        self.assertEqual(report['status'], 'PASS_SELECTED')
        self.assertEqual([r['status'] for r in report['cases']], ['PASS', 'PASS'])
        self.assertEqual(len(FakeBridge.instances), 1)
        calls = FakeBridge.instances[0].calls
        self.assertEqual(sum(e == '/batch' and q.get('action') == 'acquire' for e, m, q in calls), 1)
        self.assertTrue(report['leaseReleased'])
        self.assertNotIn('PRIVATE_CAPABILITY', path.read_text())
        self.assertFalse((path.parent / 'control.json').exists())
        self.assertFalse(any(q.get('enabled') == 0 for e, m, q in calls if e == '/guard'))

    def test_failure_stops_following_input(self):
        calls = []
        def failed(command, **kwargs):
            calls.append(command)
            return subprocess.CompletedProcess(command, 1, '{"passed":false}', 'failed')
        _, report = self.execute(failed)
        self.assertEqual([r['status'] for r in report['cases']], ['FAIL', 'BLOCKED'])
        self.assertEqual(len(calls), 1)

    def test_no_screenshot_batch_still_observes_and_releases(self):
        self.plan['captureScreenshots'] = False
        _, report = self.execute()
        calls = FakeBridge.instances[0].calls
        self.assertEqual(report['status'], 'PASS_SELECTED')
        self.assertFalse(any(e == '/screenshot' for e, _, _ in calls))
        self.assertTrue(any(e == '/state' for e, _, _ in calls))
        self.assertTrue(report['leaseReleased'])

    def test_deadline_stops_next_child(self):
        def timeout(command, **kwargs):
            raise subprocess.TimeoutExpired(command, 10)
        _, report = self.execute(timeout)
        self.assertEqual([r['status'] for r in report['cases']], ['FAIL', 'BLOCKED'])

    def test_missing_assertions_rejected_before_contact(self):
        with self.assertRaises(ValueError):
            batch.validate_case({**case('capture'), 'file': 'tests/camper_camp_presentation_capture.json'})

    def test_gates_and_unknown_case(self):
        self.plan['cases'][0]['requires'] = ['fixture:one']
        prepared = batch.prepare(self.plan)
        self.assertEqual(prepared[0]['status'], 'NOT_RUN')
        self.assertEqual(batch.prepare(self.plan, acknowledgements=['fixture:one'])[0]['status'], 'READY')
        with self.assertRaises(ValueError):
            batch.prepare(self.plan, selected=['typo'])

    def test_foreign_lease_never_released(self):
        FakeBridge.fail_acquire = True
        _, report = self.execute()
        self.assertEqual(report['status'], 'FAIL')
        self.assertFalse(any(q.get('action') == 'release' for e, m, q in FakeBridge.instances[0].calls))

    def test_local_lock_excludes_second_runner(self):
        with batch.BatchLock():
            with self.assertRaises(RuntimeError):
                with batch.BatchLock():
                    self.fail('second acquired')
        with batch.BatchLock():
            pass

    def test_pinned_identity_change_before_http(self):
        root = Path(self.temp.name)
        s = {'outputDir': str(root), 'pid': 1, 'port': 8765, 'startedUtc': 'original', 'processStartUtc': 'original', 'token': 'secret'}
        path = root / 'session.json'
        path.write_text(json.dumps(s))
        transport = batch.Transport(path)
        s['processStartUtc'] = 'reused-pid'
        path.write_text(json.dumps(s))
        with patch('urllib.request.urlopen', side_effect=AssertionError('must not contact replaced session')):
            with self.assertRaises(RuntimeError):
                transport.call('/state')

    def test_transport_discovery_failure_records_terminal_report(self):
        def missing(path):
            raise RuntimeError('no pinned session')
        path, report = batch.run(self.plan, batch.prepare(self.plan), None, missing, success)
        self.assertEqual(report['status'], 'FAIL')
        self.assertTrue(path.exists())
        self.assertTrue(all(r['status'] == 'BLOCKED' for r in report['cases']))

    def test_retained_cursor_does_not_release_lease(self):
        class CursorBridge(FakeBridge):
            def call(self, endpoint, method='GET', **query):
                value=super().call(endpoint,method,**query)
                if endpoint=='/gearobserve':value['cursor']['count']=3
                return value
        path, report=batch.run(self.plan,batch.prepare(self.plan),None,CursorBridge,success)
        self.assertFalse(report['leaseReleased'])
        self.assertTrue((path.parent/'control.json').exists())
        self.assertFalse(any(q.get('action')=='release' for e,m,q in CursorBridge.instances[-1].calls))

    def test_unacknowledged_cooking_does_not_release_lease(self):
        class CookingBridge(FakeBridge):
            def call(self, endpoint, method='GET', **query):
                value=super().call(endpoint,method,**query)
                if endpoint=='/auditstate':value['domains']['cookingAdmission'].update(clientHeld=1,client={'accepted':False})
                return value
        path,report=batch.run(self.plan,batch.prepare(self.plan),None,CookingBridge,success)
        self.assertFalse(report['leaseReleased'])
        self.assertTrue((path.parent/'control.json').exists())

    def test_real_manifest_preflight(self):
        plan = batch.read(batch.HERE / 'batches/current.json')
        prepared = batch.prepare(plan)
        self.assertGreater(len(prepared), 500)
        self.assertEqual(sum(i['status'] == 'READY' for i in prepared), 2)


if __name__ == '__main__':
    unittest.main()
