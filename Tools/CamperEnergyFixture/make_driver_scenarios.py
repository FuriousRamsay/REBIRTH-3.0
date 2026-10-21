"""Source-only materializer. Writes local-authority scenarios; never calls game."""
import argparse
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--entity', type=int, required=True, help='Actual current disposable camper entity ID')
parser.add_argument('--out', type=Path, required=True)
args = parser.parse_args()
assert args.entity > 0, 'Actual current entity ID required'
args.out.mkdir(parents=True, exist_ok=True)
def observed(checks):
    return {'expectCamper': {'entity': args.entity, 'timeout': 5, 'checks': checks}}
def check(field, value):
    return {'field': field, 'op': '==', 'value': value}
common = [check('vehicleDriverEntityId', -1), check('localPlayerAttachedEntityId', -1),
          {'field': 'localPlayerEntityId', 'op': '>=', 'value': 0}]
activate = {'action': {'command': 'activate', 'args': {'entity': args.entity}}}
blocked = {'name': 'LOCAL authority actual Camp native driver admission refusal', 'stopOnFailure': True,
           'preconditions': 'NOT RUN. Actual local authority only (/camper rejects remote worlds). Owned healthy fueled camper in Camp, no custody pending, UI closed, onfoot primary actual owner, driver seat empty. Real input paired with actual attachment IDs; no fake multiplayer qualification.',
           'steps': [{'expectUi': {'id': 'vehicleFuelCaption', 'text': 'CAMP'}}, observed(common), activate,
                     {'wait': 1}, observed(common), {'screenshot': 'energy_camp_native_driver_refused'}, {'expectNoErrors': True}]}
mounted = {'name': 'LOCAL authority actual Travel native driver mount and exit', 'stopOnFailure': True,
           'preconditions': 'NOT RUN. Same actual disposable local authority camper after full packing reaches Travel; healthy driving parts/fuel, onfoot owner, empty driver seat, no custody pending. Do not run while driving or in real save.',
           'steps': [{'expectUi': {'id': 'vehicleFuelCaption', 'text': 'TRAVEL'}}, observed(common), activate,
                     {'wait': 1}, observed([{'field': 'vehicleDriverEntityId', 'op': '==', 'expectedField': 'localPlayerEntityId'},
                                           check('localPlayerAttachedEntityId', args.entity), {'field': 'localPlayerEntityId', 'op': '>=', 'value': 0}]),
                     {'screenshot': 'energy_travel_actual_driver_mounted'},
                     {'action': {'command': 'move', 'args': {'forward': 1, 'seconds': 1}}},
                     {'action': {'command': 'key', 'args': {'name': 'E'}}}, {'wait': 1}, observed(common), {'expectNoErrors': True}]}
for name, scenario in [('energy_camp_driver_refused.json', blocked), ('energy_travel_driver_mounted.json', mounted)]:
    (args.out / name).write_text(json.dumps(scenario, indent=2) + '\n', encoding='utf-8')
print('Wrote source-only scenarios. No game request, build or test was executed.')
