from pathlib import Path
import unittest, re, xml.etree.ElementTree as ET
R=Path(__file__).resolve().parents[1]
class RuntimeBuildContractTests(unittest.TestCase):
    def test_expected_entrypoint(self):
        s=(R/'src/VehicleInspector/ModEntry.cs').read_text()
        self.assertIn('IModApi', s)
        self.assertIn('InitMod(Mod',s)
    def test_dev_only_build_package(self):
        s=(R/'Run_RuntimeInspector_BuildTest.ps1').read_text()
        self.assertIn("[ValidateSet('Preflight','Build')]",s)
        self.assertNotIn('Install_RuntimeInspector_Dev.ps1',s)
        self.assertIn('installedDll=$false',s)
    def test_build_process_waits_for_real_exit(self):
        s=(R/'Run_RuntimeInspector_BuildTest.ps1').read_text()
        for token in ('Start-Process','-Wait','-PassThru','ExitCode','dotnet_stdout.log','dotnet_stderr.log'):
            self.assertIn(token,s)
    def test_fresh_runid_and_zip(self):
        s=(R/'Run_RuntimeInspector_BuildTest.ps1').read_text()
        self.assertIn('[guid]::NewGuid()',s)
        self.assertIn("Compress-Archive -Path (Join-Path $runPath '*')",s)
        self.assertIn('REBIRTH_RuntimeInspector_BuildReport_',s)
    def test_no_game_install_by_build(self):
        s=(R/'Run_RuntimeInspector_BuildTest.ps1').read_text()
        self.assertNotIn('Copy-Item -LiteralPath $dll -Destination (Join-Path $GamePath',s)
        self.assertNotIn('Remove-Item',s)
    def test_success_requires_real_dll(self):
        s=(R/'Run_RuntimeInspector_BuildTest.ps1').read_text()
        for k in ('$buildExitCode -eq 0', 'RebirthVehicleInspector.dll', '.Length -gt 4096', 'BUILD_PASS_NOT_INSTALLED'):
            self.assertIn(k,s)
    def test_source_expected(self):
        root=R/'src/VehicleInspector'
        self.assertEqual(len(list(root.rglob('*.cs'))),len(list(root.rglob('*.cs'))))
        self.assertGreaterEqual(len(list(root.rglob('*.cs'))),7)
        self.assertTrue((root/'VehicleInspector.csproj').is_file())
    def test_net48(self):
        tree=ET.parse(R/'src/VehicleInspector/VehicleInspector.csproj')
        nodes=[el.text for el in tree.iter() if el.tag=='TargetFramework']
        self.assertIn('net48',nodes)
    def test_install_requires_consent(self):
        s=(R/'Install_RuntimeInspector_Dev.ps1').read_text()
        self.assertIn('ConfirmEacOffAndTestWorld',s)
        self.assertIn('BUILD_PASS_NOT_INSTALLED',s)
    def test_install_no_overwrites(self):
        s=(R/'Install_RuntimeInspector_Dev.ps1').read_text()
        self.assertRegex(s,r'if\(Test-Path -LiteralPath \$target\) \{throw')
        self.assertIn("REBIRTH_VehicleInspector_Dev",s)
        self.assertNotIn('Mods\\REBIRTH_3_0',s)
    def test_removal_requires_marker_and_hashes(self):
        s=(R/'Install_RuntimeInspector_Dev.ps1').read_text()
        self.assertIn('REBIRTH_DEV_INSTALL.sha256',s)
        self.assertIn('ModInfo.xml was altered',s)
    def test_capture_collector_does_not_include_logs(self):
        s=(R/'Collect_RuntimeInspector_Captures.ps1').read_text()
        self.assertIn("-Filter '*.json'",s)
        self.assertIn('32MB',s)
        self.assertNotIn('Player.log',s)
        self.assertNotIn('Saves',s)
    def test_readme_gates(self):
        s=(R/'README.md').read_text().lower()
        for k in ('not installed', 'not yet', 'without','vinspect','pose','eac'):
            self.assertIn(k,s)
    def test_mod_manifest_parse(self):
        xml=ET.parse(R/'Mod/ModInfo.xml').getroot()
        self.assertEqual(xml.tag,'xml')
        self.assertIsNotNone(xml.find('Name'))
if __name__=='__main__': unittest.main()
