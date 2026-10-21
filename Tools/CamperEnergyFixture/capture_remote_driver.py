"""Read-only actual endpoint/attachment assertions; no game input or packet injection.
Author has not executed this source. Explicit real remote+server endpoints required.
"""
import argparse
import json
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'GameBridge'))
from consumption_trace_live import TraceEndpoint

parser = argparse.ArgumentParser()
parser.add_argument('--client-session', type=Path, required=True)
parser.add_argument('--server-session', type=Path, required=True)
parser.add_argument('--server-url', required=True)
parser.add_argument('--entity', type=int, required=True)
parser.add_argument('--expected-driver', choices=('yes', 'no'), required=True)
parser.add_argument('--out', type=Path, required=True)
args = parser.parse_args()
report = {'scope': 'Actual read-only remote owner/server native driver state, no causal input or mode/save/device qualification', 'complete': False}
try:
    client = TraceEndpoint('remoteOwner', args.client_session)
    server = TraceEndpoint('authoritativeServer', args.server_session, args.server_url)
    assert client.base != server.base, 'Separate real endpoint roles required'
    assert args.entity > 0, 'Actual current camper entity required'
    owner = client.call('/state', sections='consumption').get('consumption')
    report['owner'] = owner
    assert isinstance(owner, dict) and owner.get('remote') is True, 'Genuine remote client world required'
    player = owner.get('ownerEntityId')
    assert isinstance(player, int) and player > 0, 'Current actual owner entity required'
    server.player_id = player
    witness = server.command('snapshot').get('observation')
    report['connectionWitness'] = witness
    assert isinstance(witness, dict) and witness.get('remoteWorld') is False and witness.get('remoteEntity') is True
    assert witness.get('connectionWitness') is True and witness.get('player') == player, 'Exact current real remote player connection required'
    vehicle = server.call('/camper', entity=args.entity)
    report['serverCamper'] = vehicle
    assert vehicle.get('entity') == args.entity and isinstance(vehicle.get('vehicleIdentity'), str), 'Exact current native server camper required'
    assert isinstance(vehicle.get('vehicleDriverEntityId'), int), 'Actual native driver seat observation missing'
    is_driver = vehicle['vehicleDriverEntityId'] == player
    assert is_driver is (args.expected_driver == 'yes'), 'Actual native server driver does not meet expected state'
    report['actualRemoteOwnerIsDriver'] = is_driver
    report['complete'] = True
except Exception as error:
    report['failure'] = str(error)
finally:
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'complete': report['complete'], 'report': str(args.out)}))
sys.exit(0 if report['complete'] else 1)
