import importlib.util
import json
import re
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location('surface_scenarios', ROOT / 'Tools/GameBridge/prepare_camper_surface_scenarios.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class SurfaceScenarioGeneration(unittest.TestCase):
    def setUp(self):
        self.files = module.generate({'entity': 321, 'presentation': {'bodyInstalled': True,
            'hoodApproach': {'x':10,'y':45,'z':23.6}, 'hoodAim': {'x':10,'y':46.1,'z':21.6},
            'ladderApproach': {'x':11.65,'y':45,'z':20}, 'ladderAim': {'x':11,'y':46,'z':20}}})

    def test_four_executable_scenarios_use_actual_entity(self):
        self.assertEqual(len(self.files), 4)
        for scenario in self.files.values():
            json.loads(json.dumps(scenario))
            for step in scenario['steps']:
                self.assertEqual(len(step), 1)
                if 'expectCamper' in step:
                    self.assertEqual(step['expectCamper']['entity'], 321)

    def test_walk_and_jump_are_real_input_not_teleport(self):
        for name in ('camper_hood_ladders_travel.json', 'camper_hood_ladders_camp.json'):
            actions = [s['action'] for s in self.files[name]['steps'] if 'action' in s]
            walks = [a for a in actions if a['command'] == 'walkto']
            self.assertEqual(len(walks), 1)
            self.assertTrue(all(a['args']['radius'] == .15 for a in walks))
            looks = [a for a in actions if a['command'] == 'lookat']
            self.assertEqual(looks[-1]['args']['y'], 47)
            self.assertTrue(any(a['command'] == 'move' and a['args'].get('seconds') == .3 for a in actions))
            self.assertEqual(sum(a['command'] == 'move' and a['args'].get('jump') == 1 for a in actions), 2)
            self.assertNotIn('teleport', json.dumps(actions))

    def test_relay_cycle_uses_native_button_and_actual_station_comparison(self):
        scenario = self.files['camper_relays_station_fade.json']
        group = scenario['steps'][0]['expectWindowOpen']
        xml = (ROOT / 'Config/_Vehicles/camper_energy_xui.xml').read_text()
        self.assertEqual(group, 'rebirthCamperEnergyManagement')
        self.assertIn('window_group name="' + group + '"', xml)
        clicks = [s for s in scenario['steps'] if s.get('action', {}).get('command') == 'click']
        self.assertEqual(len(clicks), 2)
        self.assertTrue(all(s['action']['args']['id'] == 'energyModeToggle' for s in clicks))
        comparisons = [c for s in scenario['steps'] if 'expectCamper' in s for c in s['expectCamper']['checks'] if 'expectedField' in c]
        self.assertEqual(len(comparisons), 2)
        self.assertTrue(all(c['expectedField'] == 'presentation.stationFadeOpacity' for c in comparisons))

    def test_reverse_uses_real_vehicle_input(self):
        actions = [s['action'] for s in self.files['camper_reverse_illumination.json']['steps'] if 'action' in s]
        self.assertTrue(any(a['command'] == 'move' and a['args'].get('forward') == -1 for a in actions))
        self.assertNotIn('setvelocity', json.dumps(self.files))

    def test_asserted_presentation_fields_exist_in_explicit_observer(self):
        source = (ROOT / 'Scripts/Vehicles/Camper/RebirthCamperTrace.cs').read_text()
        for scenario in self.files.values():
            for step in scenario['steps']:
                for check in step.get('expectCamper', {}).get('checks', []):
                    for key in ('field', 'expectedField'):
                        value = check.get(key, '')
                        if value.startswith('presentation.'):
                            self.assertIn('["' + value.split('.')[1] + '"]', source)

    def test_bare_or_missing_fixture_refused(self):
        with self.assertRaises(ValueError):
            module.generate({'entity': 321, 'presentation': {'bodyInstalled': False}})
        with self.assertRaises(ValueError):
            module.generate({'entity': -1, 'presentation': {}})

    def test_all_generated_step_kinds_and_actions_exist_in_bridge_runner(self):
        runner = (ROOT / 'Tools/GameBridge/gamebridge.ps1').read_text(encoding='utf-8-sig')
        commands = runner.split('$KvCommands = @{', 1)[1].split('function ConvertTo-Query', 1)[0]
        valid = set(re.findall(r'(\w+)\s*=\s*@\(', commands))
        for scenario in self.files.values():
            for step in scenario['steps']:
                kind = next(iter(step))
                self.assertIn("'" + kind + "' {", runner)
                if kind == 'action':
                    self.assertIn(step[kind]['command'], valid)
                if kind == 'expectCamper':
                    for check in step[kind]['checks']:
                        self.assertIn(check['op'], ('==', '>', '<', '>=', '<='))
                        self.assertNotEqual('value' in check, 'expectedField' in check)


if __name__ == '__main__':
    unittest.main()
