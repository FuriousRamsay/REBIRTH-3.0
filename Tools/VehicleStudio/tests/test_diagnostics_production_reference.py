from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


class DiagnosticsProductionReference(unittest.TestCase):
    def test_helper_references_live_production_without_compiled_clones(self):
        project = ET.parse(ROOT / 'RuntimeDiagnostics/CamperDiagnostics.csproj').getroot()
        self.assertEqual(project.findall('.//Compile'), [])
        reference = next(r for r in project.findall('.//Reference') if r.get('Include') == 'RebirthUtils')
        self.assertEqual(reference.findtext('Private'), 'false')
        self.assertTrue(reference.findtext('HintPath').endswith('RebirthUtils.dll'))
        self.assertEqual(project.findtext('.//AssemblyName'), 'RebirthCamperDiagnostics')
        friend = (ROOT.parents[1] / 'Scripts/Vehicles/Camper/RebirthCamperDiagnosticsFriend.cs').read_text()
        self.assertIn('[assembly: InternalsVisibleTo("RebirthCamperDiagnostics")]', friend)


if __name__ == '__main__':
    unittest.main()
