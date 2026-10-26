"""Windows Compress-Archive compatibility: backslash entry names and CRLF reports."""
import hashlib
import importlib.util
import json
import tempfile
import unittest
import zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('zipvalidator25', ROOT/'tools/validate_capture.py')
v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
class WindowsZipTests(unittest.TestCase):
    def test_backslash_diagnostic_path_and_crlf_accepted(self):
        report=b'REBIRTH Vehicle Inspector seat diagnostics v2.5\r\nvehicleEntityId=192\r\n'
        entry='Diagnostics\\seatdiag_test.txt'
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'test.zip'
            with zipfile.ZipFile(path,'w') as z:
                z.writestr(entry,report)
                z.writestr('manifest.json',json.dumps({'validatedDiagnosticCount':1,'diagnostics':[{'name':'seatdiag_test.txt','sha256':hashlib.sha256(report).hexdigest()}]}))
            result=v.audit_zip(path)
            self.assertEqual(result['validDiagnostics'],1)
            self.assertEqual(result['invalidDiagnostics'],0)
            self.assertEqual(result['manifestWarnings'],[])
            self.assertEqual(result['diagnostics'][0]['file'],'Diagnostics/seatdiag_test.txt')
    def test_manifest_detects_missing_diagnostic(self):
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'test.zip'
            with zipfile.ZipFile(path,'w') as z:
                z.writestr('manifest.json',json.dumps({'validatedDiagnosticCount':1}))
            result=v.audit_zip(path)
            self.assertIn('manifest_diagnostic_count_mismatch',result['manifestWarnings'])
if __name__=='__main__':unittest.main()
