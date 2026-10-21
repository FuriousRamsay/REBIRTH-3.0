"""Build a reproducible inventory; capture-only fixtures stay manual, never functional PASS."""
import ast
import json
from pathlib import Path
import re
from run_gamebridge_batch import HERE, ROOT, read, write, validate_case


def group(name):
    for prefix, label in [('camper','camper'),('mp','audit'),('purge','purge'),('combat','combat'),('horde','combat'),('entity_studio','entities'),('npc','companions'),('dog','companions'),('trader','traders'),('cooking','stations'),('crafting','stations'),('workstation','stations'),('campfire','stations'),('farming','world'),('fire','world')]:
        if name.startswith(prefix):
            return label
    return 'inventory-progression-ui'


def main():
    cfg = read(HERE / 'gamebridge.config.json')
    core = ['mp049_reviewed_admission_smoke', 'smoke', 'combat_melee', 'combat_ranged']
    cases, inventory = [], []
    for path in sorted((HERE / 'tests').rglob('*.json')):
        rel = path.relative_to(HERE).as_posix()
        record = {'file': rel, 'id': path.stem, 'group': group(path.stem)}
        try:
            data = read(path)
            record['name'] = data.get('name', path.stem) if isinstance(data, dict) else path.stem
            case = {**record, 'kind': 'scenario', 'enabled': path.stem in core,
                    'requires': [] if path.stem in core[:2] else (['flat-open-test-area'] if path.stem in core else ['fixture:' + path.stem]),
                    'timeoutSeconds': 900, 'preconditions': data.get('preconditions', '') if isinstance(data, dict) else '', 'scope': data.get('scope', 'Only the assertions in this scenario; visual/remote/reload acceptance separate') if isinstance(data, dict) else ''}
            validate_case(case)
            case['notes'] = [s[k] for s in data['steps'] for k in s if k.startswith('_')]
            inventory.append({**record, 'classification': 'EXECUTABLE_SCENARIO', 'steps': len(data['steps'])})
            cases.append(case)
        except Exception as error:
            inventory.append({**record, 'classification': 'PLAN_OR_MANUAL_CAPTURE', 'reason': str(error)})
            cases.append({**record, 'kind': 'manual', 'enabled': False, 'requires': ['manual-observation:' + path.stem],
                          'instructions': 'Read original fixture and prepare observed selectors/state; ' + str(error), 'reference': rel})
    # Stable core ordering: smoke before fights. The game remains running throughout.
    cases.sort(key=lambda c: (core.index(c['id']) if c['id'] in core else len(core), c['group'], c['id']))
    ids = set()
    for case in cases:
        if case['id'] in ids:
            case['id'] = case['file'].replace('/', '__').removesuffix('.json')
        ids.add(case['id'])
    write(HERE / 'batches/current.json', {'schema': 1, 'name': 'Current-folder prepared gameplay batches',
          'world': cfg['world'], 'save': cfg['save'], 'cases': cases})
    # Separate reusable selection manifests; they retain exact fixture gates and
    # original order. All run in an already loaded session, without per-case restarts.
    for label in sorted({c['group'] for c in cases}):
        group_cases = [dict(c, enabled=True) for c in cases if c['group'] == label]
        write(HERE / ('batches/' + label + '.json'), {'schema': 1, 'name': label + ' prepared batch',
              'world': cfg['world'], 'save': cfg['save'], 'cases': group_cases})
    write(HERE / 'batches/startup-combat.json', {'schema': 1, 'name': 'Startup and paired combat in one session',
          'world': cfg['world'], 'save': cfg['save'], 'cases': [c for c in cases if c['id'] in core]})
    drivers = []
    for path in sorted(HERE.glob('*live.py')):
        source = path.read_text(encoding='utf-8-sig')
        tree = ast.parse(source)
        options = []
        for node in ast.walk(tree):
            if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute) and node.func.attr == 'add_argument':
                options.append([x.value for x in node.args if isinstance(x, ast.Constant)])
        drivers.append({'file': path.name, 'leaseAwareSharedRunner': 'camper_consolidated_regression_live import' in source,
                        'cli': options, 'status': 'PLAN_INPUT_REQUIRED', 'doc': ast.get_docstring(tree)})
    write(HERE / 'batches/inventory.json', {'schema': 1, 'scenarios': inventory, 'drivers': drivers})
    text = (ROOT / '_Documentation/MultiplayerAudit/ProjectWide_20261014/BATCHED_VALIDATION_AGENT_DRAFT.md').read_text(encoding='utf-8-sig')
    matrix = []
    for finding, batch, title in re.findall(r'^\| (MP-\d{3}) \| ([A-H]) \| (.+?) \|$', text, re.M):
        digits = finding.removeprefix('MP-')
        refs = [c['id'] for c in cases if re.search(r'(?:mp|MP)[_-]?' + digits, c['id'])]
        matrix.append({'finding': finding, 'batch': batch, 'requirement': title,
                       'candidateCases': refs, 'status': 'NOT_RUN',
                       'acceptance': 'Use finding source contract; candidate smoke/diagnostics do not close transaction, remote, fault, or persistence branches'})
    assert len(matrix) == 88
    write(HERE / 'batches/audit-coverage.json', {'schema': 1, 'findings': matrix,
          'sharedReload': 'One explicitly scheduled save/quit/reload after all prepared persistence cases; crash tests distinct',
          'topologies': {'local': 'NOT_RUN', 'listenRemote': 'REQUIRES_GENUINE_PEER', 'dedicatedRemote': 'REQUIRES_GENUINE_PEER'}})
    print(json.dumps({'inventory': len(inventory), 'executable': sum(c['kind'] == 'scenario' for c in cases), 'manual': sum(c['kind'] == 'manual' for c in cases), 'drivers': len(drivers), 'findings': len(matrix)}))


if __name__ == '__main__':
    main()
