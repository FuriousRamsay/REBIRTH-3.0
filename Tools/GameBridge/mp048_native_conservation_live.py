"""AUTHORED_NOT_RUN. Read-only paired native trade conservation observations.
An externally authorized real trade supplies the Before and terminal checkpoints.
No launch, input, setter, save, receipt construction or fault injection occurs here.
"""
import argparse
import json
from pathlib import Path
from camper_consolidated_regression_live import Runner, need
from mp010_pull_lifecycle_live import topology, require_peers


def observe(args, plan, report):
    owner = Runner(report, Path(args.report), session_path=args.owner_session)
    host = Runner(report, Path(args.report), session_path=args.server_session)
    owner_scope = topology(owner)
    host_scope = topology(host, plan['hostPlayer'])
    require_peers(owner_scope, host_scope, args.local)
    need(owner.call('/guard')['enabled'] is True, 'owner native instincts remain enabled')
    need(host.call('/guard')['enabled'] is True, 'host native instincts remain enabled')
    inventory = owner.call('/auditstate', domain='nativeInventory')['domains']['nativeInventory']
    trade = owner.call('/auditstate', domain='npcTradeOriginals')['domains']['npcTradeOriginals']
    original = owner.call('/auditstate', domain='npcTradeOriginal', operation=plan['operation'])['domains']['npcTradeOriginal']
    npc = host.call('/auditstate', domain='npcTradeInventory', player=plan['hostPlayer'],
                    npc=plan['npc'], operation=plan['operation'])['domains']['npcTradeInventory']
    need(inventory.get('available') is True and inventory.get('cursorCount') == 0, 'actual native owner inventory and empty cursor required')
    need(trade.get('available') is True and trade.get('schema') == 1, 'actual original trade audit required')
    need(original.get('available') is True and original.get('schema') == 1, 'retained own native trade original required')
    for key in ('operation', 'original', 'npc', 'direction', 'item', 'currency', 'quantity', 'total'):
        need(original[key] == plan[key], 'scenario differs from actual immutable trade: ' + key)
    need(npc.get('available') is True and npc.get('schema') == 1 and npc.get('savedBacking') is True, 'actual loaded and independently saved NPC inventory required')
    need(npc['operation'] == plan['operation'], 'NPC observation operation mismatch')
    owner_end = topology(owner)
    host_end = topology(host, plan['hostPlayer'])
    for initial, final in ((owner_scope, owner_end), (host_scope, host_end)):
        for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'deaths', 'entityId'):
            need(initial[key] == final[key], 'endpoint changed during observation: ' + key)
    return {'ownerScope': owner_scope, 'hostScope': host_scope, 'inventory': inventory, 'trade': trade, 'npc': npc, 'original': original}


def delta_matches(before, after, expected):
    keys = set(before) | set(after) | set(expected)
    return all(int(after.get(key, 0)) - int(before.get(key, 0)) == expected.get(key, 0) for key in keys)


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    need(plan['direction'] in ('buy', 'sell'), 'explicit buy or sell direction required')
    need(plan['item'] != plan['currency'] and int(plan['quantity']) > 0 and int(plan['total']) > 0, 'distinct goods/currency and positive authority quantities required')
    need(len(plan['operation']) == 32 and len(plan['original']) == 64, 'actual immutable trade identity required')
    expectation = plan.get('expectation', 'paid')
    need(expectation in ('paid', 'held-before', 'unused'), 'explicit supported trade outcome required')
    report['plan'] = plan
    current = observe(args, plan, report)
    report['observation'] = current
    report['topologyEvidence'] = 'HOST_LOCAL' if args.local else ('DEDICATED_REMOTE' if current['hostScope']['dedicated'] else 'LISTEN_REMOTE')
    rows = [row for row in current['trade']['owner']['holds'] if row['operation'] == plan['operation']]
    need(len(rows) <= 1, 'one exact original hold required')
    if not args.resume:
        need(len(rows) == 1 and rows[0]['original'] == plan['original'], 'actual original Before hold required')
        row = rows[0]
        need(row['progress'] == 0 and not row['entered'] and row['saved'] and row['prefix'] and row['step'] == 0 and row['phase'] == 1,
             'actual independently saved untouched Before required')
        need(current['npc']['applied'] is False, 'operation already applied before baseline')
        need(current['original']['beforeMatches'] is True and current['original']['savedStage'] is True and
             current['original']['step'] == 0 and current['original']['phase'] == 1 and not current['original']['finalDisposition'],
             'complete actual live/saved native Before required')
        report['status'] = 'BASELINE_OBSERVED'
        return
    previous = json.loads(Path(args.resume).read_text(encoding='utf-8-sig'))
    need(previous.get('status') == 'BASELINE_OBSERVED' and previous['plan'] == plan, 'matching actual Before report required')
    need(previous['topologyEvidence'] == report['topologyEvidence'], 'topology class changed')
    before = previous['observation']
    for endpoint in ('ownerScope', 'hostScope'):
        for key in ('worldKey', 'ownerKey', 'creationId', 'deaths'):
            need(before[endpoint][key] == current[endpoint][key], 'immutable endpoint scope changed: ' + key)
    if expectation in ('held-before', 'unused'):
        need(current['original']['beforeMatches'] is True and current['original']['savedStage'] is True and
             current['original']['step'] == 0 and current['original']['phase'] == 1,
             'no-effect outcome must retain complete live/saved untouched owner Before')
        need(current['npc']['applied'] is False and current['npc']['revision'] == before['npc']['revision'],
             'no-effect outcome must not apply an NPC operation or revision')
        need(current['npc']['reservations'] == before['npc']['reservations'], 'no-effect outcome changed NPC reservations')
        need(delta_matches(before['inventory']['itemsByName'], current['inventory']['itemsByName'], {}),
             'no-effect outcome changed native owner goods, currency or unrelated items')
        need(delta_matches(before['npc']['quantities'], current['npc']['quantities'], {}),
             'no-effect outcome changed saved NPC stock')
        if expectation == 'held-before':
            need(len(rows) == 1 and rows[0]['original'] == plan['original'] and current['trade']['owner']['held'] is True,
                 'untouched held original lost its own financial hold')
            row = rows[0]
            need(row['progress'] == 0 and not row['entered'] and row['saved'] and row['prefix'] and
                 row['step'] == 0 and row['phase'] == 1 and not current['original']['finalDisposition'],
                 'held outcome cannot use an entered/advanced setter or final disposition')
            report['status'] = 'PASS_HELD_BEFORE_OBSERVATIONS'
        else:
            need(not rows and not current['trade']['owner']['held'] and
                 not any(row['operation'] == plan['operation'] for row in current['trade']['attempts']),
                 'positive unused original must finish hold/attempt release')
            need(current['original']['finalDisposition'] is True and current['original']['unused'] is True,
                 'unused outcome requires its positive saved native disposition')
            report['status'] = 'PASS_UNUSED_CONSERVATION_OBSERVATIONS'
        report['limitations'] = ('Exact native live/saved untouched Before, saved NPC backing and hold/disposition are observed. '
                                 'External refusal/interruption provenance and unobserved retries are not certified.')
        return
    need(not rows and not current['trade']['owner']['held'] and
         not any(row['operation'] == plan['operation'] for row in current['trade']['attempts']), 'actual original release must be complete')
    need(current['original']['afterMatches'] is True and current['original']['savedStage'] is True and
         current['original']['phase'] == 3 and current['original']['finalDisposition'] is True and not current['original']['unused'],
         'complete live/saved native After and positive paid closure required')
    need(current['npc']['applied'] is True and current['npc']['revision'] == before['npc']['revision'] + 1, 'one actual NPC operation/revision required')
    need(current['npc']['reservations'] == before['npc']['reservations'], 'trade changed unrelated reservations')
    sign = 1 if plan['direction'] == 'buy' else -1
    owner_delta = {plan['item']: sign * int(plan['quantity']), plan['currency']: -sign * int(plan['total'])}
    npc_delta = {key: -value for key, value in owner_delta.items()}
    need(delta_matches(before['inventory']['itemsByName'], current['inventory']['itemsByName'], owner_delta), 'actual owner item/currency delta or unrelated inventory differs')
    need(delta_matches(before['npc']['quantities'], current['npc']['quantities'], npc_delta), 'actual saved NPC item/currency delta or unrelated stock differs')
    report['status'] = 'PASS_CONSERVATION_OBSERVATIONS'
    report['limitations'] = 'Paired snapshots establish these exact totals, original live/saved bag/belt frames and release observations. They do not prove UI/interruption provenance, all retries or another topology.'


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--plan', required=True)
    parser.add_argument('--owner-session', required=True)
    parser.add_argument('--server-session', required=True)
    parser.add_argument('--report', required=True)
    parser.add_argument('--resume')
    parser.add_argument('--local', action='store_true')
    args = parser.parse_args()
    report = {'finding': 'MP048', 'status': 'NOT_RUN'}
    try:
        run(args, report)
    except Exception as error:
        report['status'] = 'FAIL'
        report['error'] = str(error)
        raise
    finally:
        Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
