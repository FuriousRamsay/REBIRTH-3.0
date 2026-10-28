"""Stage native-derived vehicle XML. Never installs into Config automatically."""
import argparse
import copy
import json
from pathlib import Path
import xml.etree.ElementTree as ET

def prop(node, name, value):
    child = node.find(f"property[@name='{name}']")
    if child is None:
        child = ET.SubElement(node, 'property', name=name)
    child.set('value', str(value))

def vec(value):
    return ', '.join(format(value[k], '.8g') for k in ('x', 'y', 'z'))

def stage(game, evidence, output):
    output.mkdir(parents=True, exist_ok=True)
    native = ET.parse(game / 'Data/Config/vehicles.xml').getroot()
    native_entities = ET.parse(game / 'Data/Config/entityclasses.xml').getroot()
    vehicles = ET.Element('configs')
    append = ET.SubElement(vehicles, 'append', xpath='/vehicles')
    items = ET.Element('configs')
    item_append = ET.SubElement(items, 'append', xpath='/items')
    entities = ET.Element('configs')
    entity_append = ET.SubElement(entities, 'append', xpath='/entity_classes')
    records = []
    for kind, donor in [('Buggy', 'vehicleTruck4x4'), ('Bike', 'vehicleMotorcycle')]:
        fit = json.loads((evidence / kind / 'result.json').read_text(encoding='utf-8-sig'))
        vehicle = copy.deepcopy(native.find(f"vehicle[@name='{donor}']"))
        if vehicle is None:
            raise ValueError(f'Missing exact donor {donor}')
        target = 'rebirth' + kind
        vehicle.set('name', target)
        prop(vehicle, 'recipeName', target + 'Placeable')
        # The supplied buggy has two seats, and the bike review currently covers
        # its rider only. Extra-seat and visual equipment upgrades need separate
        # fit/geometry authoring rather than inheriting invisible donor seats.
        for child in list(vehicle):
            part = child.get('class', '')
            if part.startswith('seat') and part[4:].isdigit() and int(part[4:]) >= len(fit['seats']):
                vehicle.remove(child)
        for index, seat in enumerate(fit['seats']):
            node = vehicle.find(f"property[@class='seat{index}']")
            prop(node, 'pose', seat['pose'])
            prop(node, 'position', vec(seat['anchor']))
            prop(node, 'rotation', '0, 0, 0')
            for modifier in list(node):
                if modifier.get('name') in ('mod', 'modT', 'modRot'):
                    node.remove(modifier)
        steering = vehicle.find("property[@class='handlebars']")
        pivot = {'x': 0, 'y': 1.4727043, 'z': .53195697} if kind == 'Bike' else {'x': -.4369882, 'y': 1.4370896, 'z': .935714}
        prop(steering, 'transform', 'M/Forks' if kind == 'Bike' else 'M/Steering')
        driver = fit['seats'][0]
        for hand, key in [('L', 'leftTarget'), ('R', 'rightTarget')]:
            local = {axis: driver[key][axis] - pivot[axis] for axis in ('x', 'y', 'z')}
            prop(steering, 'IKHand' + hand + 'Position', vec(local))
        if kind == 'Buggy':
            prop(steering, 'steerAngle', '0, 0, -90')
        for hand in ('L', 'R'):
            rotation = ('-7.29, 77.3, 29.69' if hand == 'L' else '4.15, 105.65, -158.73') if kind == 'Bike' else ('0, 80, 30' if hand == 'L' else '0, 100, -150')
            prop(steering, 'IKHand' + hand + 'Rotation', rotation)
        # Native SetColors changes the first renderer/material beneath paint.
        # Our multi-material source uses a scoped hook instead, so it must not
        # also tint the headlamp material reached by the donor's broad M path.
        for paint in vehicle.findall(".//property[@name='paint']"):
            parent = next(node for node in vehicle.iter() if paint in list(node))
            parent.remove(paint)
        headlight = vehicle.find("property[@class='headlight']")
        prop(headlight, 'transform', 'M/Forks/HeadLights/Lights' if kind == 'Bike' else 'M/HeadLights/Lights')
        append.append(vehicle)
        bundle = '#@modfolder:Resources/rebirth_' + kind.lower() + '.unity3d?' + target
        entity = copy.deepcopy(native_entities.find(f"entity_class[@name='{donor}']"))
        if entity is None:
            raise ValueError(f'Missing donor entity {donor}')
        entity.set('name', target)
        prop(entity, 'Prefab', bundle)
        entity_append.append(entity)
        item = ET.SubElement(item_append, 'item', name=target + 'Placeable')
        prop(item, 'Extends', donor + 'Placeable')
        prop(item, 'Tags', 'vehicle,vengine,vfuel,vlight,vstorage,canHaveCosmetic,rebirthCamperPattern')
        prop(item, 'Meshfile', bundle)
        prop(item, 'CustomIcon', donor + 'Placeable')
        action = ET.SubElement(item, 'property', {'class': 'Action1'})
        prop(action, 'Vehicle', target)
        size = '3.0008953,2.7420378,6.2883654' if kind == 'Buggy' else '1.2272671,1.737891,2.7011812'
        prop(action, 'VehicleSize', size)
        records.append({'vehicle': target, 'donor': donor, 'seatCount': len(fit['seats']),
                        'scope': 'Definition candidate; remaining donor transform/mod references require final prefab validation'})
    ET.indent(vehicles)
    ET.ElementTree(vehicles).write(output / 'standard_vehicles.xml', encoding='utf-8', xml_declaration=True)
    for document, filename in [(items, 'standard_items.xml'), (entities, 'standard_entityclasses.xml')]:
        ET.indent(document)
        ET.ElementTree(document).write(output / filename, encoding='utf-8', xml_declaration=True)
    (output / 'receipt.json').write_text(json.dumps({'status': 'STAGED_NOT_INSTALLED', 'vehicles': records}, indent=2), encoding='utf-8')

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('game', type=Path)
    parser.add_argument('evidence', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    stage(args.game, args.evidence, args.output)
