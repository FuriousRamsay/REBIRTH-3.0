"""Offline acceptance guard tests. Never imports a live session or calls the bridge."""
import copy,unittest
from unittest.mock import patch
import acceptance as a

def tree(count):
 return {'truncated':False,'openWindows':[a.WINDOW],'nodes':[{'id':'contextContainerGridSlot'+str(i),'path':a.WINDOW+'/contextContainerGrid/contextSlotsViewport/contextSlotsContent/contextContainerGridSlot'+str(i),'ctrl':'XUiC_RebirthContextSlot','rect':[i%11*80,i//11*80,78,78],'item':None} for i in range(count)]}

class Guards(unittest.TestCase):
 def test_all_capacities(self):
  for n in (24,30,40,42,48):self.assertEqual(len(a.physical_slots(tree(n),n)),n)
 def test_padding_pool_rejected(self):
  with self.assertRaises(ValueError):a.physical_slots(tree(198),48)
 def test_padded_rows_rejected(self):
  with self.assertRaises(ValueError):a.physical_slots(tree(55),48)
 def test_truncation_rejected(self):
  t=tree(48);t['truncated']=True
  with self.assertRaises(ValueError):a.physical_slots(t,48)
 def test_duplicate_rejected(self):
  t=tree(48);t['nodes'].append(t['nodes'][0])
  with self.assertRaises(ValueError):a.physical_slots(t,48)
 def test_missing_index_rejected(self):
  t=tree(48);t['nodes'].pop(0)
  with self.assertRaises(ValueError):a.physical_slots(t,48)
 def test_wrong_controller_rejected(self):
  t=tree(48);t['nodes'][0]['ctrl']='XUiC_ItemStack'
  with self.assertRaises(ValueError):a.physical_slots(t,48)
 def test_offscreen_padding_rejected(self):
  t=tree(48);t['nodes'][0]['rect'][1]=-100000
  with self.assertRaises(ValueError):a.physical_slots(t,48)
 def test_backpack_not_counted(self):
  t=tree(48);back=copy.deepcopy(t['nodes'][0]);back['path']=a.WINDOW+'/contextBackpackGrid/contextContainerGridSlot0';t['nodes'].append(back)
  self.assertEqual(len(a.physical_slots(t,48)),48)
 def test_wrong_window(self):
  t=tree(48);t['openWindows']=[]
  with self.assertRaises(ValueError):a.physical_slots(t,48)
 def test_no_rect(self):
  t=tree(48);t['nodes'][0].pop('rect')
  with self.assertRaises(ValueError):a.physical_slots(t,48)
 def test_items_keep_index(self):
  t=tree(48);t['nodes'][47]['item']={'name':'resourceWood','count':17}
  self.assertEqual(a.physical_slots(t,48)[47]['item']['count'],17)
 def test_same_process_not_cold(self):
  t=dict(gameName='Audit',worldKey='world',ownerKey='owner',creationId='character',processIdentity='one',worldInstance='one')
  with self.assertRaises(ValueError):a.identity_equal(t,t,cold=True)
 def test_new_world_same_process_not_cold(self):
  t=dict(gameName='Audit',worldKey='world',ownerKey='owner',creationId='character',processIdentity='one',worldInstance='one');other=dict(t,worldInstance='two')
  with self.assertRaises(ValueError):a.identity_equal(t,other,cold=True)
 def test_new_process_same_world_instance_not_cold(self):
  t=dict(gameName='Audit',worldKey='world',ownerKey='owner',creationId='character',processIdentity='one',worldInstance='one');other=dict(t,processIdentity='two')
  with self.assertRaises(ValueError):a.identity_equal(t,other,cold=True)
 def test_new_process_same_owned_world(self):
  t=dict(gameName='Audit',worldKey='world',ownerKey='owner',creationId='character',processIdentity='one',worldInstance='one');a.identity_equal(t,dict(t,processIdentity='two',worldInstance='two'),cold=True)
 def test_other_save_owner_character_rejected(self):
  t=dict(gameName='Audit',worldKey='world',ownerKey='owner',creationId='character',processIdentity='one',worldInstance='one')
  for field in ('gameName','worldKey','ownerKey','creationId'):
   with self.assertRaises(ValueError):a.identity_equal(t,dict(t,**{field:'wrong'}))
 def test_inherited_player_ice_expectation(self):self.assertEqual(a.CATALOG['picked_ice_machine'][1],42)
 def test_natural_safe_control_separate(self):self.assertEqual((a.CATALOG['locked_wall_safe'][1],a.CATALOG['shared_safe_control'][1]),(24,40))
 def test_probe_dispatches_only_real_input_and_observation(self):
  class Fake:
   opened=False
   def __init__(self):self.calls=[]
   def call(self,path,query=None,method='GET'):
    self.calls.append((path,method,query))
    if path=='/target':return {'target':{'block':{'name':'cntDumpster','position':[1,40,2]}}}
    if path=='/activate':self.opened=True
    if path=='/key':self.opened=False
    if path=='/uitree':return tree(48) if self.opened else {'openWindows':[]}
    if path=='/screenshot':return {'path':'MOCK_CAPTURE_NOT_NATIVE_PROOF'}
    return {'ok':True}
  bridge=Fake()
  with patch.object(a.time,'sleep'):
   result=a.probe(bridge,dict(id='dumpster',catalog='dumpster',provenance='fresh_natural',position=[1,40,2],approach={'x':1,'z':4}))
  self.assertEqual(result['capacity'],48)
  self.assertEqual([p for p,m,q in bridge.calls if m=='POST'],['/walkto','/lookat','/activate','/screenshot','/key'])
 def test_all250_research_is_explicitly_classified(self):
  from pathlib import Path
  report=a.read(Path(__file__).resolve().parents[2]/'_Documentation/LootCapacityAcceptance_20261029/all-player-capacity-classification.json')
  self.assertEqual(len(report['rows']),250);self.assertEqual(report['directCount'],161);self.assertEqual(report['inheritedCount'],89)
  self.assertEqual(sum(report['counts'].values()),250)
  for row in report['rows']:
   if row['classification']=='EVIDENCED_CAPACITY_GAP':self.assertEqual(row['conditionalInfluence'],[]);self.assertNotEqual(row['size'],row['currentSize'])

if __name__=='__main__':unittest.main(verbosity=2)
