"""Native UI camper flows using source-defined controls and unique observed paths.

Only called through the owned-session/full-log supervisor. No guessed coordinates,
raw spawn, runtime item mutation, automatic restart or acceptance of dispatched input.
"""
import csv
import hashlib
import json
from pathlib import Path
import time
import xml.etree.ElementTree as ET
from run_camper_qualification import MOD, receipt_path

BASE = MOD.parents[1] / 'Data/Config'


def unique(nodes, predicate, purpose):
    matches = [node for node in nodes if predicate(node) and node.get('enabled') is not False and node.get('rect')]
    if len(matches) != 1:
        raise RuntimeError(f'{purpose}: require one actually visible control; observed {len(matches)}.')
    return matches[0]


def item_totals(slots):
    totals = {}
    for row in slots:
        item = row.get('item')
        if item:
            totals[item['name']] = totals.get(item['name'], 0) + item['count']
    return totals


def persistence_image(snapshot):
    # Entity IDs and acceptance flags are observations, never persistent identity.
    return {key: snapshot[key] for key in ('vehicleIdentity', 'dye', 'storageCapacity', 'cargo', 'storage', 'stations')}


def native_plan():
    blocks = ET.parse(BASE / 'blocks.xml').getroot()
    loot = ET.parse(BASE / 'loot.xml').getroot()
    containers = []
    for name in ('cntWallSafe_Player', 'cntFridgeStainlessSteel_Player'):
        block = blocks.find(f"block[@name='{name}']")
        prop = None if block is None else block.find("property[@class='CompositeFeatures']/property[@class='TEFeatureStorage']/property[@name='LootList']")
        if prop is None:
            raise RuntimeError('Installed native storage feature definition changed: ' + name)
        container = loot.find(f"lootcontainer[@name='{prop.get('value')}']")
        if container is None:
            raise RuntimeError('Native container loot definition is missing.')
        dimensions = [int(value) for value in container.get('size').split(',')]
        containers.append({'item': name, 'capacity': dimensions[0] * dimensions[1], 'lootList': prop.get('value')})
    modifiers = ET.parse(BASE / 'item_modifiers.xml').getroot()
    for name in ('modDyeRed', 'modDyeGreen', 'modDyeBlack'):
        if modifiers.find(f"item_modifier[@name='{name}']") is None:
            raise RuntimeError('Native cosmetic definition is missing: ' + name)
    recipes = ET.parse(BASE / 'recipes.xml').getroot()
    recipe = recipes.find("recipe[@name='resourceGunPowder'][@craft_area='chemistryStation']")
    if recipe is None or 'learnable' in recipe.get('tags', '').split(','):
        raise RuntimeError('Unlearned simple native chemistry recipe definition changed.')
    ingredients = [{'item': row.get('name'), 'count': int(row.get('count'))} for row in recipe.findall('ingredient')]
    for station in ('campfire', 'chemistryStation'):
        if blocks.find(f"block[@name='{station}']/property[@class='Workstation']") is None:
            raise RuntimeError('Native station definition is missing: ' + station)
    return {'containers': containers, 'stationRecipe': 'resourceGunPowder', 'ingredients': ingredients,
        'stationRecipeCount': int(recipe.get('count')), 'stationRecipeTime': float(recipe.get('craft_time')),
        'sourceHashes': {name: hashlib.sha256((BASE / name).read_bytes()).hexdigest()
            for name in ('blocks.xml', 'loot.xml', 'recipes.xml', 'item_modifiers.xml')}}


def local_labels():
    with (BASE / 'Localization.csv').open(encoding='utf-8-sig', newline='') as stream:
        native = {row['Key']: row['english'] for row in csv.DictReader(stream)}
    with (MOD / 'Config/Localization.csv').open(encoding='utf-8-sig', newline='') as stream:
        custom = {row[0]: row[1] for row in csv.reader(stream) if len(row) >= 2}
    return {'service': native['entitycommand_service'], 'cargo': native['entitycommand_storage'],
        'take': native['entitycommand_take'], 'recipe': native['resourceGunPowder'],
        'storage': custom['xuiRebirthCamperStorageManagement'], 'stations': custom['xuiRebirthCamperStationManagement'],
        'craft': custom['xuiRebirthCamperUseStation']}


class CamperUI:
    def __init__(self, suite, entity):
        self.suite = suite
        self.entity = entity
        self.plan = native_plan()
        self.labels = local_labels()
        self.folder = suite.folder / 'native_ui'
        self.folder.mkdir(exist_ok=True)
        (self.folder / 'source_plan.json').write_text(json.dumps(self.plan, indent=2), encoding='utf-8')
        self.ordinal = 0

    def tree(self):
        self.suite.identity()
        data = self.suite.raw('/ui/tree', all=1)
        if data.get('truncated'):
            raise RuntimeError('Visible UI tree truncated; no ambiguous input permitted.')
        self.ordinal += 1
        (self.folder / f'{self.ordinal:04d}_ui.json').write_text(json.dumps(data, indent=2), encoding='utf-8')
        return data['nodes']

    def node(self, predicate, purpose):
        return unique(self.tree(), predicate, purpose)

    def click(self, node, **params):
        return self.suite.act('/ui/click', path=node['path'], **params)

    def drag(self, source, target):
        value = self.suite.act('/ui/drag', **{'from.path': source['path'], 'to.path': target['path']})
        if value.get('stillHeld'):
            raise RuntimeError('Native transfer left a held cursor item; stop without replay/closing away custody.')
        return value

    def screenshot(self, label):
        value = self.suite.act('/screenshot', name='camper_ui_' + label)
        image = Path(value['path']).resolve()
        if not image.is_relative_to((MOD / 'Tools/GameBridge/out/screenshots').resolve()) or not image.is_file():
            raise RuntimeError('Screenshot missing or outside owned Bridge evidence.')
        import shutil
        shutil.copyfile(image, self.folder / image.name)

    def close(self):
        # Escape with no window opens the pause menu. Check actual source-defined
        # groups first and never send a blind extra Escape after native Take.
        for attempt in range(3):
            windows = self.suite.raw('/ui').get('openWindows', [])
            modal = [name for name in windows if name in ('vehicle', 'crafting', 'radial',
                'rebirthCamperStorageManagement', 'rebirthCamperStationManagement', 'rebirthCamperUseStation')
                or name.startswith('workstation_')]
            if not modal:
                return
            self.suite.act('/key', name='Escape')
        raise RuntimeError('Native camper window did not close within three observed attempts.')

    def approach(self):
        self.close()
        if self.suite.raw('/vehicle').get('attached'):
            self.suite.act('/dismount')
        rows = self.suite.raw('/entities', radius=250, limit=1000).get('entities', [])
        entity = next((row for row in rows if row['id'] == self.entity and row.get('class') == 'rebirthCamper80s'), None)
        if entity is None:
            raise RuntimeError('Exact normally obtained camper is missing.')
        x, y, z = entity['position']
        self.suite.act('/walkto', x=x + 4, z=z, radius=.5, maxSeconds=25)
        self.suite.act('/lookat', entity=self.entity)

    def radial(self, action):
        self.approach()
        self.suite.act('/activate', entity=self.entity, hold=.8)
        entries = [node for node in self.tree() if node.get('ctrl') == 'XUiC_RadialEntry' and node.get('rect')]
        if not 1 <= len(entries) <= 13:
            raise RuntimeError('Actual native radial entries unavailable; no guessed icon/index input.')
        # Native templates expose the selected label, not every action name.
        # Hover each real visible entry and verify actual selection text first.
        for entry in entries:
            self.suite.act('/ui/hover', path=entry['path'], seconds=.1)
            labels = [node for node in self.tree() if node.get('id') == 'selection' and node.get('text') == self.labels[action]]
            if len(labels) == 1:
                self.click(entry)
                return
        self.screenshot('radial_missing_' + action)
        raise RuntimeError('Native radial does not expose the exact source-defined action label: ' + self.labels[action])

    def snapshot(self, label):
        reply = self.suite.act('/console', cmd=f'rbcamperstate {self.entity}')
        path = receipt_path(reply, '[CamperState] CAPTURED ', MOD / 'Tools/GameBridge/out/camper_state')
        value = json.loads(path.read_text(encoding='utf-8-sig'))
        if value.get('schema') != 1 or value.get('entity') != self.entity or value.get('transferPending'):
            raise RuntimeError('Actual camper observation is incomplete or transfer pending.')
        (self.folder / (label + '.json')).write_text(json.dumps(value, indent=2), encoding='utf-8')
        return value

    def inventory_item(self, name):
        return self.node(lambda n: (n.get('item') or {}).get('name') == name and self.player_slot(n),
            'Actual player inventory item ' + name)

    @staticmethod
    def player_slot(node):
        path = node['path']
        return ('/inventory/' in path and (path.startswith('backpack/') or path.startswith('toolbelt/')
            or '/rebirthCraftingInventoryRegion/' in path)) or node.get('id', '').startswith('contextBackpackGridSlot')

    def empty_backpack(self):
        nodes = [n for n in self.tree() if 'item' in n and n.get('item') is None and self.player_slot(n)
            and not n['path'].startswith('toolbelt/') and n.get('rect')]
        if not nodes:
            raise RuntimeError('No actual visible empty backpack destination; do not discard cursor items.')
        return sorted(nodes, key=lambda n: n['path'])[0]

    def cosmetic(self):
        return self.node(lambda n: n.get('ctrl') == 'XUiC_ItemCosmeticStack' and '/cosmeticparts/' in n['path'], 'Native vehicle cosmetic slot')

    def dye(self):
        self.approach()
        initial = self.snapshot('initial_random_dye')
        if initial['dye'] not in {'modDye' + color for color in ('Brown', 'Red', 'Orange', 'Yellow', 'Green', 'Blue', 'Purple', 'Black', 'Pink')}:
            raise RuntimeError('New normally obtained camper lacks its native random initial dye.')
        self.radial('service')
        if (self.cosmetic().get('item') or {}).get('name') != initial['dye']:
            raise RuntimeError('Native cosmetic UI disagrees with actual vehicle dye.')
        self.screenshot('random_initial_cosmetic')
        for name in ('modDyeRed', 'modDyeGreen', 'modDyeBlack'):
            slot = self.cosmetic()
            if slot.get('item'):
                self.drag(slot, self.empty_backpack())
                if self.cosmetic().get('item'):
                    raise RuntimeError('Native cosmetic removal did not commit.')
            existing = [n for n in self.tree() if self.player_slot(n) and (n.get('item') or {}).get('name') == name]
            if not existing:
                self.suite.act('/give', item=name, count=1)
            elif len(existing) != 1 or existing[0]['item']['count'] != 1:
                raise RuntimeError('Dye transfer requires one actual single-item player stack; no blind split.')
            self.drag(self.inventory_item(name), self.cosmetic())
            if (self.cosmetic().get('item') or {}).get('name') != name or self.snapshot(name)['dye'] != name:
                raise RuntimeError('Native cosmetic application did not commit: ' + name)
            self.screenshot(name + '_cosmetic')
            self.close(); self.screenshot(name + '_exterior'); self.radial('service')
        self.drag(self.cosmetic(), self.empty_backpack())
        if self.cosmetic().get('item') or self.snapshot('removed_dye')['dye'] is not None:
            raise RuntimeError('Explicit cosmetic removal was not preserved.')
        self.close(); self.screenshot('removed_dye_blue_exterior')

    def module(self, slot):
        return self.node(lambda n: n.get('id') == 'camperModule' + str(slot)
            and n.get('ctrl') == 'XUiC_RebirthCamperModuleSlot', 'Native module position ' + str(slot))

    def assert_ten_modules(self):
        slots = [n for n in self.tree() if n.get('ctrl') == 'XUiC_RebirthCamperModuleSlot']
        if sorted(n.get('id') for n in slots) != ['camperModule' + str(i) for i in range(10)]:
            raise RuntimeError('Management does not present exactly ten actual module positions.')
        self.screenshot('management_ten_slots')

    def install(self, action, slot, item):
        self.radial(action); self.assert_ten_modules()
        if self.module(slot).get('item'):
            raise RuntimeError('Expected empty module position is occupied; refuse destructive replacement.')
        self.suite.act('/give', item=item, count=1)
        self.drag(self.inventory_item(item), self.module(slot))
        if (self.module(slot).get('item') or {}).get('name') != item:
            raise RuntimeError('Actual native module transfer did not commit.')
        self.close()

    def storage(self):
        for slot, container in enumerate(self.plan['containers']):
            self.install('storage', slot, container['item'])
            value = self.snapshot('storage_added_' + str(slot))
            expected = sum(row['capacity'] for row in self.plan['containers'][:slot + 1])
            if value['storageCapacity'] != expected or len(value['cargo']) != expected:
                raise RuntimeError('Cargo capacity must equal the installed native loot-definition sum.')
        self.radial('cargo')
        self.suite.act('/give', item='resourceRockSmall', count=7)
        cargo = [node for node in self.tree() if ('/inventory/' in node['path'] and node['path'].startswith('loot/')
            and not self.player_slot(node) or node.get('id', '').startswith('contextContainerGridSlot'))
            and 'item' in node and node.get('item') is None and node.get('rect')]
        if not cargo:
            raise RuntimeError('No actual visible native cargo slot; snapshot cannot replace cargo UI acceptance.')
        marker = self.inventory_item('resourceRockSmall')
        marker_count = marker['item']['count']
        self.drag(marker, sorted(cargo, key=lambda n: n['path'])[0])
        self.close()
        before = self.snapshot('cargo_before_capacity_reduction')
        if item_totals(before['cargo']).get('resourceRockSmall') != marker_count:
            raise RuntimeError('Actual cargo UI transfer did not commit marker quantity.')
        self.radial('storage')
        self.drag(self.module(1), self.empty_backpack()); self.close()
        after = self.snapshot('cargo_after_safe_reduction')
        if after['storageCapacity'] != self.plan['containers'][0]['capacity'] or item_totals(after['cargo']) != item_totals(before['cargo']):
            raise RuntimeError('Safe capacity reduction lost/duplicated cargo or retained wrong capacity.')
        # Reinstall the SAME returned module rather than give a duplicate.
        self.radial('storage'); self.drag(self.inventory_item(self.plan['containers'][1]['item']), self.module(1)); self.close()

    def open_station(self, slot):
        self.radial('craft')
        choice = self.node(lambda n: n.get('id') == 'camperStationChoice' + str(slot), 'Actual installed station picker choice')
        self.click(choice)
        self.screenshot('station_' + str(slot) + '_open')

    def switch_station(self, slot):
        self.click(self.node(lambda n: n.get('id') == 'camperSwitchStationButton', 'In-station switch button'))
        deadline = time.monotonic() + 10
        while time.monotonic() < deadline:
            self.suite.identity()
            choices = [n for n in self.tree() if n.get('id') == 'camperStationChoice' + str(slot) and n.get('rect')]
            if len(choices) == 1:
                self.click(choices[0])
                self.screenshot('station_' + str(slot) + '_switched_without_radial')
                return
            time.sleep(.25)
        raise RuntimeError('In-station switch did not open the installed-station picker within10seconds.')

    def station_slot(self, part):
        # Grid controllers are native and authored in station_templates/workspace.
        nodes = self.tree()
        grid = unique(nodes, lambda n: n.get('ctrl') == 'XUiC_Workstation' + part + 'Grid', 'Native ' + part + ' grid')
        slots = [n for n in nodes if n['path'].startswith(grid['path'] + '/') and 'item' in n and n.get('item') is None and n.get('rect')]
        if not slots:
            raise RuntimeError('Native station has no empty visible ' + part + ' slot.')
        return sorted(slots, key=lambda n: n['path'])[0]

    def stations(self):
        self.install('stations', 0, 'campfire')
        self.install('stations', 1, 'chemistryStation')
        self.install('stations', 2, 'chemistryStation')
        fresh = self.snapshot('fresh_independent_station_states')
        if fresh['stations'][1]['identity'] == fresh['stations'][2]['identity'] or any(
                row['queue'] or item_totals(row['fuel']) or item_totals(row['output']) for row in fresh['stations'][1:3]):
            raise RuntimeError('Fresh duplicate station states must be distinct and empty before any processing input.')
        self.open_station(0)
        self.suite.act('/give', item='toolCookingPot', count=1)
        self.drag(self.inventory_item('toolCookingPot'), self.station_slot('Tool'))
        self.switch_station(1)
        if item_totals(self.snapshot('campfire_native_tool')['stations'][0]['tools']).get('toolCookingPot') != 1:
            raise RuntimeError('Native mobile campfire tool installation did not commit.')
        self.suite.act('/give', item='resourceWood', count=20)
        fuel = self.inventory_item('resourceWood')
        supplied_fuel = fuel['item']['count']
        self.drag(fuel, self.station_slot('Fuel'))
        for ingredient in self.plan['ingredients']:
            self.suite.act('/give', item=ingredient['item'], count=ingredient['count'])
        search = self.node(lambda n: n.get('id') == 'rebirthCraftingRecipeSearch', 'Source-defined station recipe search')
        self.suite.act('/ui/type', path=search['path'], value=self.labels['recipe'])
        recipe = self.node(lambda n: n.get('recipe') == self.plan['stationRecipe'], 'Actual native station recipe')
        if recipe.get('hasIngredients') is not True:
            raise RuntimeError('Actual native recipe remains unavailable; requirements were not bypassed.')
        self.click(recipe)
        self.click(self.node(lambda n: n.get('id') == 'btnRebirthCraftingCraft', 'Actual native craft button'))
        self.click(self.node(lambda n: n.get('id') == 'onoff', 'Native fuel on/off control'))
        deadline = time.monotonic() + 30
        while time.monotonic() < deadline:
            value = self.snapshot('chemistry_processing')
            primary, duplicate = value['stations'][1:3]
            if primary['identity'] == duplicate['identity'] or item_totals(duplicate['fuel']) or item_totals(duplicate['output']) or duplicate['queue']:
                raise RuntimeError('Duplicate station identities or independent native state changed.')
            if item_totals(primary['output']).get(self.plan['stationRecipe']) == self.plan['stationRecipeCount']:
                break
            time.sleep(.5)
        else:
            raise RuntimeError('Native mobile station failed to produce expected output within30seconds.')
        if item_totals(primary['fuel']).get('resourceWood', 0) >= supplied_fuel and primary.get('fuelSeconds', 0) <= 0:
            raise RuntimeError('Produced output has no actual native fuel-consumption evidence.')
        nodes = self.tree()
        output_grid = unique(nodes, lambda n: n.get('ctrl') == 'XUiC_WorkstationOutputGrid', 'Native output grid')
        outputs = [node for node in nodes if node['path'].startswith(output_grid['path'] + '/')
            and (node.get('item') or {}).get('name') == self.plan['stationRecipe']]
        if sum(node['item']['count'] for node in outputs) != self.plan['stationRecipeCount']:
            raise RuntimeError('Native station output UI disagrees with actual produced item quantity.')
        self.screenshot('native_recipe_output')
        self.click(self.node(lambda n: n.get('id') == 'onoff', 'Native fuel off control'))
        self.close()
        value = self.snapshot('stations_completed')
        if value['stations'][1]['burning'] or value['stations'][1]['queue']:
            raise RuntimeError('Station must be stopped with completed queue before persistence staging.')
        self.cancel_station_job()

    def cancel_station_job(self):
        # Duplicate chemistry station is intentionally unfueled: its normal
        # native card can be selected without racing the one-second recipe.
        self.open_station(2)
        before_station = self.snapshot('cancel_station_before')['stations'][2]
        if before_station['burning'] or before_station['queue'] or item_totals(before_station['fuel']):
            raise RuntimeError('Cancellation fixture requires the actual independent unfueled station.')
        for ingredient in self.plan['ingredients']:
            self.suite.act('/give', item=ingredient['item'], count=ingredient['count'])

        def counts():
            # Each observed native inventory slot once; omit recipe icons and outputs.
            slots = {node['path']: node for node in self.tree() if self.player_slot(node) and node.get('rect')}
            return item_totals(slots.values())

        before = counts()
        search = self.node(lambda n: n.get('id') == 'rebirthCraftingRecipeSearch', 'Native cancellation recipe search')
        self.suite.act('/ui/type', path=search['path'], value=self.labels['recipe'])
        recipe = self.node(lambda n: n.get('recipe') == self.plan['stationRecipe'], 'Actual cancellation recipe')
        if recipe.get('hasIngredients') is not True:
            raise RuntimeError('Cancellation recipe unavailable under actual native requirements.')
        self.click(recipe)
        self.click(self.node(lambda n: n.get('id') == 'btnRebirthCraftingCraft', 'Native cancellation craft control'))
        deadline = time.monotonic() + 30
        while time.monotonic() < deadline:
            cards = [n for n in self.tree() if n.get('ctrl') == 'XUiC_RecipeStack'
                and (n.get('queued') or {}).get('recipe') == self.plan['stationRecipe']]
            queued = self.snapshot('cancel_queue_publication')['stations'][2]['queue']
            if cards and len(queued) == 1 and queued[0].get('cancelReady') is True:
                card = unique(cards, lambda n: True, 'Selected native paid queue card')
                break
            time.sleep(.25)
        else:
            raise RuntimeError('Native cancellation queue card did not appear within30seconds.')
        debited = counts()
        for ingredient in self.plan['ingredients']:
            if before.get(ingredient['item'], 0) - debited.get(ingredient['item'], 0) != ingredient['count']:
                raise RuntimeError('Native queued cancellation job did not debit its exact ingredient payment.')
        self.screenshot('native_cancellation_selected_queue')
        self.click(card)  # actual HandleOnPress; never replay a possibly committed cancel
        deadline = time.monotonic() + 30
        while time.monotonic() < deadline:
            after_station = self.snapshot('native_cancellation_pending')['stations'][2]
            after = counts()
            if not after_station['queue'] and after == before:
                break
            time.sleep(.5)
        else:
            raise RuntimeError('Native selected-job cancellation/refund remains pending; no click replay or hold clearing.')
        if after_station['output'] != before_station['output'] or after_station['fuel'] != before_station['fuel']:
            raise RuntimeError('Cancellation changed independent station output or unfueled input state.')
        self.screenshot('native_cancellation_refund_conserved')
        self.close()

    def run(self):
        self.dye(); self.storage(); self.stations()
        self.pickup()
        self.approach()
        snapshot = self.snapshot('persistence_before')
        if not snapshot['persistedModulesPresent']:
            raise RuntimeError('Completed UI state has no native persisted module snapshot.')
        checkpoint = {'kind': 'CAMPER_NATIVE_UI_PERSISTENCE_CHECKPOINT', 'save': 'CodexTest',
            'sourcePid': self.suite.pid, 'expected': persistence_image(snapshot), 'sourcePlan': self.plan,
            'reloadAccepted': False, 'pickupAccepted': True, 'visualAcceptance': False}
        path = self.folder / 'persistence_checkpoint.json'
        path.write_text(json.dumps(checkpoint, indent=2), encoding='utf-8')
        self.suite.report['nativeUI'] = {'status': 'ASSERTED_LIVE_UI_RESULTS_NOT_VISUAL_OR_RELOAD_ACCEPTANCE',
            'evidence': str(self.folder), 'checkpoint': str(path)}
        return path

    def pickup(self):
        # Native pickup requires an empty cargo bag. Return the actual marker
        # through cargo UI first; never erase it or replace the vehicle item.
        self.radial('cargo')
        marker = self.node(lambda n: (n.get('item') or {}).get('name') == 'resourceRockSmall'
            and not self.player_slot(n) and (n['path'].startswith('loot/') or n.get('id', '').startswith('contextContainerGridSlot')),
            'Actual cargo marker for native pickup preparation')
        self.drag(marker, self.empty_backpack()); self.close()
        empty = self.snapshot('cargo_emptied_for_native_pickup')
        if item_totals(empty['cargo']):
            raise RuntimeError('Native pickup requires empty cargo; do not discard contents.')
        # Exercise a player-selected dye and then explicit removal across pickup.
        for color in ('modDyeBlack', None):
            self.radial('service')
            if color:
                self.drag(self.inventory_item(color), self.cosmetic())
            else:
                self.drag(self.cosmetic(), self.empty_backpack())
            self.close()
            before = self.snapshot('before_pickup_' + (color or 'removed'))
            if before['dye'] != color:
                raise RuntimeError('Requested native dye state was not committed before pickup.')
            rows = self.suite.raw('/entities', radius=150, limit=1000).get('entities', [])
            position = next(row['position'] for row in rows if row['id'] == self.entity)
            ids = {row['id'] for row in rows}
            old_entity = self.entity
            self.radial('take')
            self.close()
            if any(row['id'] == old_entity for row in self.suite.raw('/entities', radius=150, limit=1000).get('entities', [])):
                raise RuntimeError('Native Take did not remove the exact vehicle; no uncertain replay.')
            self.suite.act('/key', name='Tab')
            source = self.inventory_item('rebirthCamper80sPlaceable')
            if source['item']['count'] != 1:
                raise RuntimeError('Native picked-up item must be one unambiguous actual camper.')
            belt = source if source['path'].startswith('toolbelt/') else None
            if belt is None:
                empties = [n for n in self.tree() if n['path'].startswith('toolbelt/') and n['path'].split('/')[-1].isdigit()
                    and 0 <= int(n['path'].split('/')[-1]) < 10 and 'item' in n and n.get('item') is None and n.get('rect')]
                if not empties:
                    raise RuntimeError('No actual empty toolbelt destination; refuse replacing another item.')
                belt = min(empties, key=lambda n: int(n['path'].split('/')[-1]))
                self.drag(source, belt)
            slot = int(belt['path'].split('/')[-1]) + 1
            self.close()
            self.suite.act('/select', slot=slot)
            pad = json.loads((self.suite.folder / 'track.json').read_text(encoding='utf-8-sig'))
            self.suite.act('/lookat', x=position[0], y=pad['surfaceY'], z=position[2])
            self.suite.act('/press', action='Secondary', seconds=.3)
            deadline = time.monotonic() + 8
            while time.monotonic() < deadline:
                created = [row for row in self.suite.raw('/entities', radius=150, limit=1000).get('entities', [])
                    if row['id'] not in ids and row.get('class') == 'rebirthCamper80s']
                if len(created) == 1:
                    self.entity = created[0]['id']; break
                if len(created) > 1:
                    raise RuntimeError('Ambiguous normally re-placed camper; no further action.')
                time.sleep(.25)
            else:
                raise RuntimeError('Normal placement of the picked-up native item was not confirmed.')
            after = self.snapshot('after_pickup_' + (color or 'removed'))
            if persistence_image(before) != persistence_image(after):
                raise RuntimeError('Native pickup/placement changed actual dye, module identity or native station contents.')
            self.screenshot('pickup_preserved_' + (color or 'removed'))
            self.suite.report['camper'] = self.entity
