from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]

class ScriptSafetyTests(unittest.TestCase):
    def test_script_has_readonly_evidence_flags(self):
        src = (ROOT / 'Run_Vanilla_Reference_Probe.ps1').read_text(encoding='utf8')
        for key in ['changedGameFiles = $false', 'installedGameDll = $false',
                    'recordedCharacterBones = $false', 'matchedRuntimeVehicle = $false',
                    'xml_only_runtime_unverified']:
            self.assertIn(key, src)

    def test_script_avoids_game_mutation_commands(self):
        src = (ROOT / 'Run_Vanilla_Reference_Probe.ps1').read_text(encoding='utf8')
        for operation in ['Remove-Item ', 'Copy-Item ', 'Move-Item ', 'Invoke-WebRequest ',
                          'Invoke-RestMethod ', 'Start-Process ', 'Set-Content ', 'Set-Item ',
                          'Add-Type ', 'dotnet build ']:
            self.assertNotIn(operation, src)

    def test_exact_4x4_names_in_code(self):
        src = (ROOT / 'Run_Vanilla_Reference_Probe.ps1').read_text(encoding='utf8')
        self.assertIn("'vehicleTruck4x4'", src)
        self.assertIn("'vehicle4x4Truck'", src)

if __name__ == '__main__': unittest.main()
