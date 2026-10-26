"""Static regression guards for 7 errors observed in the user's real v1.9 build.
Does NOT replace recompiling against the user's game assemblies.
"""
import unittest
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'src' / 'VehicleInspector'

class ObservedCompilerErrorRepairs(unittest.TestCase):
    def test_model_pose_aliased_in_all_three_affected_sources(self):
        for relative in ('Capture/OccupantCapture.cs', 'Capture/HierarchyCapture.cs', 'VehicleInspectorService.cs'):
            with self.subTest(file=relative):
                code = (SRC / relative).read_text(encoding='utf-8')
                self.assertIn('using Pose = RebirthVehicleInspector.Model.Pose;', code)
                self.assertIn('Pose.From(', code)

    def test_mod_event_ref_signature(self):
        code = (SRC / 'ModEntry.cs').read_text(encoding='utf-8')
        self.assertIn('ModEvents.GameStartDone.RegisterHandler(GameStartDone);', code)
        self.assertIn('GameStartDone(ref ModEvents.SGameStartDoneData data)', code)
        self.assertNotIn('GameStartDone()', code)

    def test_vehicles_module_game_assembly_reference(self):
        proj = ET.parse(SRC / 'VehicleInspector.csproj')
        refs = [n for n in proj.iter() if n.tag == 'Reference']
        module = [n for n in refs if n.attrib.get('Include') == 'UnityEngine.VehiclesModule']
        self.assertEqual(len(module), 1)
        hint = module[0].find('HintPath')
        self.assertIsNotNone(hint)
        self.assertEqual(hint.text, r'$(GameManaged)\UnityEngine.VehiclesModule.dll')
        self.assertIn('UnityEngine.VehiclesModule.dll', (SRC / 'VehicleInspector.csproj').read_text())

    def test_prior_warning_explicitly_addressed(self):
        self.assertIn('public new bool Equals(object a, object b)', (SRC / 'Util' / 'Reflect.cs').read_text())

    def test_progress_heartbeat_nonblocking_wait_and_real_exit_code(self):
        code = (ROOT / 'Run_RuntimeInspector_BuildTest.ps1').read_text()
        for symbol in ('WaitForExit(3000)', 'WaitForExit()', '$proc.Refresh()', '$proc.ExitCode', 'dotnet_stdout.log', 'dotnet_stderr.log', 'Build still running'):
            with self.subTest(symbol=symbol):
                self.assertIn(symbol, code)
        self.assertIn("summary='BUILD_FAIL_NOT_INSTALLED'", code)
        self.assertNotIn('Install_RuntimeInspector_Dev.ps1', code)

    def test_no_game_or_rebirth_directories_written_by_build(self):
        code = (ROOT / 'Run_RuntimeInspector_BuildTest.ps1').read_text()
        self.assertIn('installedDll=$false', code)
        self.assertIn('modifiedGameFiles=$false', code)
        self.assertNotIn('Copy-Item -LiteralPath $builtDll -Destination $GamePath', code)

if __name__=='__main__':
    unittest.main()
