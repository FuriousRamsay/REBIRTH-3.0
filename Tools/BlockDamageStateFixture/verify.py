"""Offline-only regression; does not launch or connect to the game."""
import hashlib
import json
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[2]
here = Path(__file__).resolve().parent
mono = Path('C:/Program Files/Unity/Hub/Editor/2022.3.62f2/Editor/Data/MonoBleedingEdge/bin/mono.exe')
exe = here / 'bin/Debug/net481/BlockDamageStateFixture.exe'
source = root / 'Scripts/Survivor/Backgrounds/RebirthWorkmanshipSignatureService.cs'
before = (here / 'RebirthWorkmanshipSignatureService.before.txt').read_bytes()
old = b'harmony.Patch(m,prefix:new HarmonyMethod(typeof(RebirthWorkmanshipSignatureService),nameof(EmptyHandBlockInspectPrefix)),postfix:new HarmonyMethod(typeof(RebirthWorkmanshipSignatureService),nameof(EmptyHandBlockInspectPostfix)))'
new = b'harmony.Patch(m,prefix:new HarmonyMethod(typeof(RebirthEmptyHandBlockInspectionPatch),nameof(RebirthEmptyHandBlockInspectionPatch.Prefix)),postfix:new HarmonyMethod(typeof(RebirthEmptyHandBlockInspectionPatch),nameof(RebirthEmptyHandBlockInspectionPatch.Postfix)))'
assert before.count(old) == 1 and before.replace(old, new) == source.read_bytes(), 'Unexpected production source change'
results = {'sourceExactSubstitution': 'PASS', 'dllSHA256': hashlib.sha256((root / 'RebirthUtils.dll').read_bytes()).hexdigest(), 'gameplay': 'NOT RUN', 'tests': {}}
for name, command, marker in [
    ('clr_state_execution', [str(exe)], 'PASS:'),
    ('mono_baseline_negative', [str(mono), str(exe), 'baseline'], 'EXPECTED BASELINE FAILURE:'),
    ('mono_state_execution', [str(mono), str(exe)], 'PASS:'),
    ('native_baseline_negative', [str(mono), str(exe), 'native', str(root), 'baseline'], 'EXPECTED NATIVE BASELINE InvalidProgramException:'),
    ('native_actual_installer', [str(mono), str(exe), 'native', str(root)], 'on 14 native/mod Block methods')]:
    p = subprocess.run(command, cwd=root, capture_output=True, timeout=45)
    output = (p.stdout + p.stderr).decode('utf-8-sig', errors='replace')
    (here / (name + '.log')).write_text(output, encoding='utf-8')
    assert p.returncode == 0 and marker in output, (name, p.returncode, output)
    results['tests'][name] = {'status': 'PASS', 'exitCode': p.returncode}
(here / 'results.json').write_text(json.dumps(results, indent=2) + '\n')
print(json.dumps(results, indent=2))
