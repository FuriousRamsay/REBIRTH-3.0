"""Offline fixture-planning checks; no game, HTTP or subprocess launch."""
import copy
import unittest
from unittest.mock import patch
import local_fixture_batch_live as live


class FakeRunner:
    def __init__(self, full=False):
        self.report={}; self.full=full; self.moved=False; self.calls=[]
    def save(self): pass
    def guard_idle(self): pass
    def call(self, endpoint, method='GET', **args):
        self.calls.append((endpoint,method,args))
        if endpoint=='/state':
            return {'world':{'gameName':'CodexTest'},'openWindows':[],'toolbelt':([] if self.moved else [{'name':live.MEDICINE,'count':1}])}
        if endpoint=='/gearobserve':
            return {'gameName':'CodexTest','remote':False,'cursor':{'count':0},'gearHeld':False,'libraryHeld':False,'resourcePending':False,'creationHeld':False,'offerPending':False,'intentMarkers':[],
                    'bag':([{'itemData':'original-native-medicine','count':1}] if self.moved else []),
                    'belt':([] if self.moved else [{'itemData':'original-native-medicine','count':1}])}
        if endpoint=='/auditstate':
            return {'domains':{'cookingAdmission':{'clientHeld':0,'ownerDeltaHeld':False}}}
        if endpoint=='/ui/tree':
            nodes=[{'path':'toolbelt/window/inventory/2','item':None if self.moved else {'name':live.MEDICINE,'count':1},'rect':[1,1,50,50]}]
            if not self.full:nodes.append({'path':'crafting/inventory/4','ctrl':'XUiC_RebirthCraftingInventorySlot','item':{'name':live.MEDICINE,'count':1} if self.moved else None,'rect':[1,1,50,50]})
            return {'truncated':False,'openWindows':['crafting'],'nodes':nodes}
        if endpoint=='/ui/drag':
            self.moved=True
            return {'placedByClick':False,'stillHeld':None,'from':{'path':args['from.path']},'to':{'path':args['to.path']}}
        return {'ok':True,'actions':['permanent.Character']}


class FixtureTests(unittest.TestCase):
    def test_existing_medicine_moves_without_loss(self):
        runner=FakeRunner()
        live.backpack_only_medicine(runner)
        self.assertTrue(runner.moved)
        self.assertEqual(len(runner.report['preparations'][0]['moves']),1)
        self.assertFalse(any(e in ('/give','/console','/setblock') for e,_,_ in runner.calls))
        self.assertEqual([q['name'] for e,_,q in runner.calls if e=='/key'],['Tab','B'])
    def test_full_visible_backpack_refuses_without_deletion(self):
        runner=FakeRunner(full=True)
        with self.assertRaises(AssertionError):live.backpack_only_medicine(runner)
        self.assertFalse(runner.moved)
        self.assertFalse(any(e=='/ui/drag' for e,_,_ in runner.calls))
    def test_pending_owner_stops_before_ui(self):
        runner=FakeRunner(); original=runner.call
        def held(e,m='GET',**q):
            value=original(e,m,**q)
            if e=='/gearobserve':value['resourcePending']=True
            return value
        runner.call=held
        with self.assertRaises(AssertionError):live.backpack_only_medicine(runner)
        self.assertFalse(any(e=='/key' for e,_,_ in runner.calls))
    def test_retained_cursor_stops_without_retry(self):
        runner=FakeRunner();original=runner.call
        def held(e,m='GET',**q):
            if e=='/ui/drag':
                runner.calls.append((e,m,q))
                return {'placedByClick':False,'stillHeld':{'count':1}}
            return original(e,m,**q)
        runner.call=held
        with self.assertRaises(AssertionError):live.drag(runner,'toolbelt/window/inventory/2','crafting/inventory/4')
        self.assertEqual(sum(e=='/ui/drag' for e,_,_ in runner.calls),1)
    def test_native_images_detect_metadata_and_count_change(self):
        before={'bag':[{'itemData':'native-one','count':2}],'belt':[]}
        after={'bag':[],'belt':[{'itemData':'native-one','count':2}]}
        self.assertEqual(live.inventory_images(before),live.inventory_images(after))
        after['belt'][0]['itemData']='changed-metadata'
        self.assertNotEqual(live.inventory_images(before),live.inventory_images(after))
    def test_plan_validates_before_any_game_call(self):
        from run_gamebridge_batch import prepare,read,HERE
        prepared=prepare(read(HERE/'batches/local-fixtures-functional.json'),acknowledgements=['observed-flat-local-test-area'])
        self.assertEqual([row['status'] for row in prepared],['READY','READY'])


if __name__=='__main__':unittest.main()
