"""Offline safety checks for real captured 80s camper evidence, NOT a Unity test."""
from __future__ import annotations
from pathlib import Path
import copy
import json
import math
import unittest

ROOT = Path(__file__).resolve().parents[1]
load = lambda n: json.loads((ROOT/'evidence'/n).read_text('utf-8'))


def classify(contract, audit):
    expected = contract['wheels']
    source = audit['nodes']
    wheels = [n for n in source if n['name'].startswith('80sCamperTruck_wheel_')]
    if len(wheels) != 4:
        raise ValueError('Exactly four independent named wheel nodes are required')
    geometry_by_center = {}
    for g in audit['geometry']:
        if g['path'].startswith('80sCamperTruck_wheel_'):
            key = tuple(round(g['boundsCenter'][x],3) for x in 'xyz')
            if key in geometry_by_center:
                raise ValueError('Duplicate geometry wheel location')
            geometry_by_center[key] = g
    if len(geometry_by_center) != 4:
        raise ValueError('Four separate wheel meshes required')
    used = set()
    output={}
    for w in expected:
        hits=[]
        ref=w['expectedCenter']
        for node in wheels:
            pos=node['rootPosition']
            distance=math.sqrt(sum((pos[axis]-ref[axis])**2 for axis in 'xyz'))
            if distance < contract['wheelCenterTolerance']:
                hits.append(node)
        if len(hits) != 1:
            raise ValueError('Ambiguous wheel geometry at '+w['corner'])
        n=hits[0]
        if n['name'] != w['originalName'] or n['path'] in used and n['path'] not in ('80sCamperTruck_wheel_FR',):
            raise ValueError('Wheel label mismatches evidence')
        key = tuple(round(n['rootPosition'][axis],3) for axis in 'xyz')
        if key not in geometry_by_center:
            raise ValueError('No separate wheel renderer at '+w['corner'])
        g=geometry_by_center[key]
        if math.sqrt(sum((g['boundsSize'][k]-w['expectedBoundsSize'][k])**2 for k in 'xyz')) >= contract['wheelBoundsTolerance']:
            raise ValueError('Wheel renderer bounds drifted')
        used.add(n['path'])
        output[w['corner']]=n
    if len(output)!=4:return None
    return output


class CamperContractTests(unittest.TestCase):
    def setUp(self):
        self.c=load('camper_wheel_contract.json')
        self.a=load('prefab_audit.json')
        self.r=load('result.json')
        self.components=load('prefab_components.json')

    def test_real_prefab_pinned(self):
        self.assertEqual(self.c['sourceAsset'],self.a['sourceAsset'])
        self.assertEqual(self.c['sourceSha256'],self.a['sourceSha256'])
        self.assertEqual(self.c['sourceImporterMetaSha256'],self.a['sourceImporterMetaSha256'])
        self.assertEqual(self.c['unityVersion'],'2022.3.62f2')

    def test_four_distinct_corner_positions(self):
        found=classify(self.c,self.a)
        self.assertEqual(set(found),{'front-left','front-right','rear-left','rear-right'})
        self.assertEqual(len({tuple(n['rootPosition'][k] for k in 'xyz') for n in found.values()}),4)

    def test_duplicate_name_is_rear_left(self):
        wheels=classify(self.c,self.a)
        self.assertEqual(wheels['rear-left']['name'],'80sCamperTruck_wheel_FR')
        self.assertLess(wheels['rear-left']['rootPosition']['x'],0)
        self.assertLess(wheels['rear-left']['rootPosition']['z'],0)
        self.assertEqual(self.c['wheels'][2]['previewName'],'80sCamperTruck_wheel_RL')

    def test_left_right_and_front_rear_signs(self):
        w=classify(self.c,self.a)
        for corner,node in w.items():
            pos=node['rootPosition']
            self.assertEqual(pos['x']<0,'left' in corner)
            self.assertEqual(pos['z']>0,'front' in corner)

    def test_wheel_centres_symmetrical(self):
        w=classify(self.c,self.a)
        at=lambda c,k:w[c]['rootPosition'][k]
        self.assertAlmostEqual(at('front-left','x'), -at('front-right','x'),3)
        self.assertAlmostEqual(at('rear-left','x'), -at('rear-right','x'),3)
        self.assertAlmostEqual(at('front-left','z'),at('front-right','z'),3)
        self.assertAlmostEqual(at('rear-left','z'),at('rear-right','z'),3)
        self.assertAlmostEqual(at('front-left','y'),at('rear-right','y'),3)

    def test_wheelbase_track_and_wheel_size(self):
        w=classify(self.c,self.a)
        p=lambda c,k:w[c]['rootPosition'][k]
        self.assertAlmostEqual(p('front-left','z')-p('rear-left','z'),2.909,2)
        self.assertAlmostEqual(p('front-right','x')-p('front-left','x'),1.488,2)
        for n in w.values():self.assertAlmostEqual(n['rootPosition']['y'],.375,2)
        for item in self.c['wheels']:
            self.assertAlmostEqual(item['expectedBoundsSize']['y'],.778,2)
            self.assertAlmostEqual(item['expectedBoundsSize']['z'],.778,2)

    def test_separate_steering_wheel_mesh(self):
        names={n['name'] for n in self.a['nodes']}
        self.assertIn('80sCamperTruck_steeringwheel',names)
        self.assertIn('80sCamperTruck_steeringwheel',{g['path'] for g in self.a['geometry']})

    def test_all_geometry_intact(self):
        self.assertEqual(self.c['geometryObjects'],16)
        self.assertEqual(self.c['hierarchyNodes'],18)
        self.assertEqual(len(self.a['geometry']),16)
        self.assertEqual(len(self.a['nodes']),18)

    def test_no_custom_or_missing_components(self):
        self.assertEqual(self.components['customMonoBehaviours'],0)
        self.assertEqual(self.components['missingComponents'],0)

    def test_no_7dtd_verification_claim(self):
        self.assertFalse(self.r['gameValidated'])
        self.assertFalse(self.r['assetBundleBuilt'])
        self.assertFalse(self.r['seatsCalibrated'])
        self.assertFalse(self.r['userReviewedPivots'])

    def test_rejects_deleted_wheel(self):
        broken=copy.deepcopy(self.a)
        broken['nodes']=[x for x in broken['nodes'] if x['name']!='80sCamperTruck_wheel_RR']
        with self.assertRaisesRegex(ValueError,'four independent'):
            classify(self.c,broken)

    def test_rejects_relocated_wheel(self):
        broken=copy.deepcopy(self.a)
        for x in broken['nodes']:
            if x['name']=='80sCamperTruck_wheel_RR':
                x['rootPosition']['z']-=1.0
        with self.assertRaisesRegex(ValueError,'Ambiguous'):
            classify(self.c,broken)

    def test_rejects_changed_source_name(self):
        broken=copy.deepcopy(self.a)
        for x in broken['nodes']:
            if x['name']=='80sCamperTruck_wheel_RR':x['name']='badname_wheel_RR'
        with self.assertRaises(ValueError):classify(self.c,broken)

    def test_rejects_changed_wheel_bounds(self):
        broken=copy.deepcopy(self.a)
        for x in broken['geometry']:
            if x['path']=='80sCamperTruck_wheel_RR': x['boundsSize']['x']+=.2
        with self.assertRaisesRegex(ValueError,'bounds drifted'):
            classify(self.c,broken)

    def test_powershell_exact_folder_and_project(self):
        txt=(ROOT/'Run_Camper_WheelRig.ps1').read_text('utf-8')
        self.assertIn('C:\\REBIRTH\\VehicleStudio_QuickTest',txt)
        self.assertIn('2022.3.62f2',txt)
        self.assertIn('RebirthUnityCamperWheelRigSmoke.RunBatch',txt)

    def test_editor_source_disallows_deployment(self):
        txt=(ROOT/'UnitySources/REBIRTHQuickSmoke/Editor/RebirthUnityCamperWheelRigSmoke.cs').read_text('utf-8')
        self.assertNotIn('BuildOneAssetBundle(',txt)
        self.assertNotIn('BuildPipeline.BuildAssetBundles(',txt)
        self.assertIn('UNAPPROVED_Camper_FourWheel_Preview.prefab',txt)
        self.assertIn('sourceUnchanged',txt)
        self.assertIn('SaveCleanPrefab(',txt)


if __name__=='__main__': unittest.main()
