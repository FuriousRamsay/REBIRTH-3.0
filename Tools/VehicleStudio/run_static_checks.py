"""Run original Vehicle Studio regression tests plus REBIRTH integration guards."""
import json
from pathlib import Path
import time
import unittest
import contextlib
import uuid

root = Path(__file__).resolve().parent
folder = root / 'reports' / 'static_checks' / uuid.uuid4().hex
folder.mkdir(parents=True, exist_ok=True)
print('[1 TEST] Running preserved source regression tests and camper integration guards', flush=True)
started = time.monotonic()
loader = unittest.TestLoader()
suite = unittest.TestSuite([
    loader.discover(str(root / 'source/tools/tests'), top_level_dir=str(root / 'source/tools/tests')),
    unittest.TestLoader().discover(str(root / 'tests'), top_level_dir=str(root / 'tests')),
])
with (folder / 'tests.log').open('w', encoding='utf-8') as stream:
    with contextlib.redirect_stdout(stream), contextlib.redirect_stderr(stream):
        result = unittest.TextTestRunner(stream=stream, verbosity=2).run(suite)
report = {'kind': 'REBIRTH_VEHICLE_STUDIO_STATIC_REGRESSION', 'testsRun': result.testsRun,
          'failures': [{'test': str(t), 'detail': detail} for t, detail in result.failures],
          'errors': [{'test': str(t), 'detail': detail} for t, detail in result.errors],
          'skipped': [{'test': str(t), 'reason': reason} for t, reason in result.skipped],
          'elapsedSeconds': round(time.monotonic() - started, 3),
          'status': 'PASS' if result.wasSuccessful() else 'FAIL', 'gameTested': False}
(folder / 'result.json').write_text(json.dumps(report, indent=2) + '\n')
print(f'[2 {report["status"]}] tests={result.testsRun}; skipped={len(result.skipped)}; report={folder / "result.json"}', flush=True)
raise SystemExit(0 if result.wasSuccessful() else 1)
