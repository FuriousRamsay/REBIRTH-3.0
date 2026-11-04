"""Summarize the finished B22 capture; never infer allocations from CPU samples."""
import json, sys
from pathlib import Path

root = Path(__file__).resolve().parent
path = Path(sys.argv[1]) if len(sys.argv) > 1 else root / 'out/profile_20261102_214502.json'
p = json.loads(path.read_text(encoding='utf-8-sig'))
gc = p.get('gc', {})
s = p.get('mainSnapshot', {})
wall_us = p['wallMs'] * 1000
pauses = p.get('runtimeSuspensions', {}).get('events', [])
r = {
    'file': str(path), 'wallMs': p['wallMs'], 'samples': p['total'],
    'trackAlloc': p['trackAlloc'], 'allocSeen': p['allocSeen'],
    'stackDropped': p['stackDropped'], 'heap': p.get('heap'),
    'gc': {k: gc.get(k) for k in ('count', 'totalUs', 'maxUs')},
    'runtimeSuspensions': {'count': len(pauses), 'totalUs': sum(e[1] for e in pauses), 'maxUs': max((e[1] for e in pauses), default=0)},
    'sampler': s,
    'samplerSuspendShareOfWall': s.get('suspendUs', 0) / wall_us,
    'samplerWorkShareOfWall': s.get('sampleWorkUs', 0) / wall_us,
    'limitations': [
        'Mixed startup, fixture setup, teleport, combat, UI and shutdown; no controlled FPS comparison.',
        ('Allocations sampled; allocation traffic and final heap cannot establish a memory leak.' if p['trackAlloc'] else 'Allocations disabled; final heap and this capture cannot establish a memory leak.'),
        'Long capture/work brackets signal observation risk; they include OS/context wait and do not alone measure added game pause. Do not attribute all freezes to game code.',
        'Reload wall-clock outliers are failed numeric qualification, not accepted weapon balance measurements.'
    ]
}
out = (Path(sys.argv[2]) if len(sys.argv) > 2 else root / 'out/purge-b22') / 'finished-profile-review.json'
out.write_text(json.dumps(r, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in r.items() if k not in ('sampler', 'limitations', 'file')}, indent=2))
