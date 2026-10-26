"""Offline checks of durable v2.3 seat diagnostics and collector contracts.

These checks do not claim compilation or in-game logging has been verified.
"""
from __future__ import annotations
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'src/VehicleInspector'
SERVICE=(SRC/'VehicleInspectorService.cs').read_text()
LOG=(SRC/'Util/SeatDiagnosticLog.cs').read_text()
CMD=(SRC/'Commands/ConsoleCmdVehicleInspector.cs').read_text()
COLLECTOR=(ROOT/'Collect_RuntimeInspector_Captures.ps1').read_text()
VALIDATOR=ROOT/'tools/validate_capture.py'
spec=importlib.util.spec_from_file_location('rebirth_v23_validator',VALIDATOR)
validator=importlib.util.module_from_spec(spec)
spec.loader.exec_module(validator)

class SeatDiagnosticLoggingTests(unittest.TestCase):
    def test_seatdiag_does_not_depend_on_copying_console(self):
        self.assertIn('case "seatdiag":Output(s.SeatDiagnosticsToLog());break;',CMD)
        self.assertIn('Log.Out("[VehicleInspector] "+message)',CMD)
    def test_report_is_persisted_in_developer_mod_exports(self):
        self.assertIn('Path.Combine(exportFolder, "Diagnostics")',LOG)
        self.assertIn('"seatdiag_"',LOG)
        self.assertIn('File.Move(temp, target)',LOG)
        self.assertIn('FileMode.CreateNew',LOG)
        self.assertIn('new UTF8Encoding(false)',LOG)
    def test_game_log_contains_actual_multiline_report(self):
        self.assertIn('foreach (string raw in report',LOG)
        self.assertIn('Log.Out("[VehicleInspector][seatdiag] " + raw.Substring(offset, count))',LOG)
        self.assertIn('MaxGameLogLineCharacters = 1800',LOG)
    def test_report_bounded_does_not_dump_arbitrary_objects(self):
        self.assertIn('MaxBodyCharacters = 65536',LOG)
        self.assertIn('diagnostic content was truncated',LOG)
        # Legacy seat-member probe keeps strings and object references opaque.
        hint=(SRC/'Capture/SeatMemberProbe.cs').read_text()
        self.assertIn('" (opaque)"',hint)
        self.assertIn('prop.GetIndexParameters()',hint)
    def test_seated_snapshot_automatically_saves_diagnostics(self):
        self.assertIn('cap.scenarioLabel=="seated"',SERVICE)
        self.assertIn('SeatDiagnosticsToLog("automatic_seated_snapshot")',SERVICE)
        self.assertIn('string path=Save(cap,label);',SERVICE)
    def test_nearest_auto_selection_is_not_xml_or_seat_guess(self):
        self.assertIn('world.NearestVehicle(15f)',SERVICE)
        self.assertIn('Select(nearest,"seatdiag-nearest")',SERVICE)
        self.assertIn('XML binding is conspicuous',SERVICE)
        self.assertIn('seatIndexVerification=requires explicit game slot evidence',LOG)
    def test_logging_errors_are_not_false_successes(self):
        self.assertIn('ERROR: seat diagnostics could not be saved',SERVICE)
        self.assertIn('do not assume the report exists',SERVICE)
        self.assertIn('catch(Exception ex)',SERVICE)
    def test_collector_includes_diagnostics_and_can_run_without_capture(self):
        self.assertIn("Join-Path $folder 'Diagnostics'",COLLECTOR)
        self.assertIn("'seatdiag_*.txt'",COLLECTOR)
        self.assertIn('if($accepted.Count -eq 0 -and $diagnostics.Count -eq 0)',COLLECTOR)
        self.assertIn("'DIAGNOSTICS_ONLY_VALIDATED'",COLLECTOR)
        self.assertIn('validatedDiagnosticCount=$diagnostics.Count',COLLECTOR)
        self.assertIn('256KB',COLLECTOR)
    def test_legacy_captures_skipped_without_barrage(self):
        self.assertIn('$oldSkipped++;continue',COLLECTOR)
        self.assertIn('old-version capture(s) skipped',COLLECTOR)
    def test_captured_records_require_current_version(self):
        self.assertIn("$obj.inspectorVersion -ne '0.7.0-v2.4'",COLLECTOR)
        self.assertIn('"0.7.0-v2.4"',(SRC/'Model/Records.cs').read_text())
    def test_zip_analyzer_accepts_valid_seatdiag_text(self):
        content=b'REBIRTH Vehicle Inspector seat diagnostics v2.3\ncapturedUtc=2026-10-10T00:00:00Z\nvehicleEntityId=192\nCandidate #171 seat=-1\n'
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/'logs.zip'
            with zipfile.ZipFile(p,'w') as z:
                z.writestr('Diagnostics/seatdiag_20261010_192_abcdef12.txt',content)
                z.writestr('manifest.json',json.dumps({'diagnostics':[{'name':'seatdiag_20261010_192_abcdef12.txt','sha256':hashlib.sha256(content).hexdigest()}]}))
            result=validator.audit_zip(p)
            self.assertEqual(result['validDiagnostics'],1)
            self.assertEqual(result['invalidDiagnostics'],0)
            self.assertEqual(result['invalid'],0)
    def test_zip_analyzer_rejects_corrupted_text(self):
        content=b'not a seatdiag\n'
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/'corrupt.zip'
            with zipfile.ZipFile(p,'w') as z:
                z.writestr('Diagnostics/seatdiag_20261010_192_abcdef12.txt',content)
            result=validator.audit_zip(p)
            self.assertEqual(result['validDiagnostics'],0)
            self.assertEqual(result['invalidDiagnostics'],1)
    def test_collector_does_not_modify_rebirth_or_game_files(self):
        self.assertNotIn('Set-Content -LiteralPath $ModletPath',COLLECTOR)
        self.assertIn('Copy-Item -LiteralPath $item.FullName -Destination',COLLECTOR)

if __name__=='__main__':unittest.main()
