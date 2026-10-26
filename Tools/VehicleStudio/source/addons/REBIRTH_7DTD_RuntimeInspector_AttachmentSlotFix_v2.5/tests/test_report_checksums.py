import unittest
from pathlib import Path
R=Path(__file__).resolve().parents[1]
class HashBindingTests(unittest.TestCase):
    def test_report_has_dll_hash(self):
        s=(R/'Run_RuntimeInspector_BuildTest.ps1').read_text()
        self.assertIn('compiledDllSha256=$builtDllSha256',s)
        self.assertIn('$builtDllSha256=ShaOrNull',s)
    def test_install_checks_hash(self):
        s=(R/'Install_RuntimeInspector_Dev.ps1').read_text()
        self.assertIn('Compiled DLL checksum differs from build report',s)
        self.assertIn('BUILD_PASS_NOT_INSTALLED',s)
