"""Explicit bounded diagnostic capture; never sends consumption requests or replay."""
import json
import urllib.parse
import urllib.request
from observe_consumption_live import ROOT


class TraceEndpoint:
    def __init__(self, role, session_path, base_url=None, player_id=None):
        session = json.loads(session_path.read_text(encoding='utf-8-sig'))
        self.role = role
        self.base = (base_url or f"http://127.0.0.1:{session['port']}").rstrip('/')
        self.token = session['token']
        self.player_id = player_id
        assert urllib.parse.urlsplit(self.base).scheme in ('http', 'https'), 'Explicit HTTP bridge endpoint required'

    def call(self, path, method='GET', **args):
        req = urllib.request.Request(self.base + path + '?' + urllib.parse.urlencode(args),
                                     headers={'X-Bridge-Token': self.token}, method=method,
                                     data=b'' if method == 'POST' else None)
        with urllib.request.urlopen(req, timeout=30) as response:
            value = json.load(response)
        assert value.get('ok') is True, 'Diagnostic bridge request failed'
        return value

    def command(self, operation):
        if operation == 'snapshot' and self.player_id is not None:
            operation = 'observe ' + str(self.player_id)
        reply = self.call('/console', 'POST', cmd='rbconsumptiontrace ' + operation)
        assert reply.get('hadError') is False, 'Native trace console command failed'
        for result in reply.get('results', []):
            for line in result.get('output', []):
                try:
                    value = json.loads(line)
                except (ValueError, TypeError):
                    continue
                if isinstance(value, dict) and 'events' in value and 'lastSequence' in value:
                    return value
        raise AssertionError('Native trace snapshot unavailable; deploy current diagnostic source')


def endpoints(args):
    result = [TraceEndpoint('remoteOwner', ROOT / 'Tools/GameBridge/out/session.json')]
    if args.server_session:
        result.append(TraceEndpoint('authoritativeServer', args.server_session, args.server_url, args.server_player))
    assert len({endpoint.base for endpoint in result}) == len(result), 'Separate real endpoint roles required'
    return result


def start(endpoints, evidence):
    evidence['trace'] = {'roles': {}, 'serverProvided': len(endpoints) == 2,
                         'scope': 'Diagnostic observations only; no ACK, native cure PASS or packet replay'}
    for endpoint in endpoints:
        role = evidence['trace']['roles'][endpoint.role] = {'enabledByBatch': False}
        # Existing native observer establishes actual world role; a host observation
        # cannot substitute for the genuine remote owner required by run().
        if endpoint.player_id is not None:
            observation = endpoint.command('snapshot').get('observation')
            assert isinstance(observation, dict) and observation.get('remoteWorld') is False and observation.get('remoteEntity') is True, 'Server requires actual current live remote player'
            assert observation.get('connectionWitness') is True, 'Server requires current real remote player connection'
            assert observation['player'] == endpoint.player_id == evidence['trace']['roles']['remoteOwner']['ownerEntityId'], 'Explicit server entity must be the genuine remote owner'
            role['ownerEntityId'] = observation['player']
        else:
            observation = endpoint.call('/state', sections='consumption').get('consumption')
            assert isinstance(observation, dict), 'Endpoint requires local-player observer or explicit --server-player'
            assert observation.get('remote') is (endpoint.role == 'remoteOwner'), 'Endpoint world role mismatch'
            role['ownerEntityId'] = observation['ownerEntityId']
        role['observationBefore'] = observation
        # Even an uncertain enable response needs one best-effort snapshot/off;
        # this is cleanup, not an input retry or request replay.
        role['enabledByBatch'] = True
        role['before'] = endpoint.command('on')


def finish(endpoints, evidence):
    trace = evidence.get('trace', {})
    for endpoint in endpoints:
        role = trace.get('roles', {}).get(endpoint.role)
        if not role or not role.get('enabledByBatch'):
            continue
        try:
            role['after'] = endpoint.command('snapshot')
        except Exception as failure:
            role['snapshotFailure'] = str(failure)
        finally:
            try:
                role['stopped'] = endpoint.command('off')
            except Exception as failure:
                role['stopFailure'] = str(failure)
    roles = trace.get('roles', {})
    server = roles.get('authoritativeServer', {}).get('after', {}).get('events', [])
    owner = roles.get('remoteOwner', {}).get('after', {}).get('events', [])
    sent = {row['publication']: row for row in server
            if row.get('publication') and row.get('stage') == 'result.send' and row.get('remoteWorld') is False}
    trace['correlatedPublications'] = [
        {'publication': row['publication'], 'serverRequest': sent[row['publication']].get('request'),
         'player': row.get('player'), 'ownerStage': row['stage'], 'ownerReason': row.get('reason')}
        for row in owner if row.get('publication') in sent and row.get('remoteWorld') is True
        and row.get('player') == sent[row['publication']].get('player')]
    trace['correlationObserved'] = bool(trace['correlatedPublications'])
    trace['missingServerReason'] = None if trace.get('serverProvided') else 'No genuine server endpoint supplied; owner trace only'
