"""Offline authoring integration checks; does not validate runtime transfers/audio."""
from pathlib import Path
import csv
import xml.etree.ElementTree as E

ROOT = Path(__file__).resolve().parents[2]
def parse(path):
    return E.parse(ROOT / path)

with (ROOT / 'Config/Localization.csv').open(encoding='utf-8-sig', newline='') as f:
    localization = dict(row for row in csv.reader(f) if len(row) == 2)
with (ROOT.parent.parent / 'Data/Config/Localization.csv').open(encoding='utf-8-sig', newline='') as f:
    for row in csv.reader(f):
        if len(row) > 1:
            localization.setdefault(row[0], row[1])

items = {}
for path in (ROOT / 'Config').rglob('items.xml'):
    for item in E.parse(path).iter('item'):
        if item.get('name'):
            items[item.get('name')] = item

device = 'rebirthGearWalkmanHeadphones'
assert device in items, 'Combined device item missing'
device_props = {p.get('name'): p.get('value') for p in items[device].findall('property')}
assert device_props.get('CustomIcon') == 'WalkmanMod_FR', 'Combined device must use original combined artwork'
assert (ROOT / 'UIAtlases/ItemIconAtlas/WalkmanMod_FR.png').is_file()
recipes = [r for r in parse('Config/_Survivor/recipes.xml').iter('recipe') if r.get('name') == device]
assert len(recipes) == 1, 'Combined device recipe missing or duplicated'
assert {i.get('name'): i.get('count') for i in recipes[0].findall('ingredient')} == {
    'FuriousRamsayWalkman': '1', 'FuriousRamsayHeadphones': '1'}, 'Wrong device ingredients'
for ingredient in recipes[0].findall('ingredient'):
    assert ingredient.get('name') in items, 'Undefined component'
profiles = [p for p in parse('Config/_Survivor/support_profiles.xml').iter('support_profile')
            if p.get('gear_slot_id') == 'walkman']
assert len(profiles) == 1 and profiles[0].get('gear_item_id') == device

music = ['FuriousRamsayCassette' + f'{i:02d}' for i in range(1, 24)]
catalog = parse('Config/_Survivor/music_cassettes.xml')
assert catalog.find('policy').get('requires_equipped_item') == device
entries = list(catalog.iter('cassette'))
assert len(entries) == len(music) and {e.get('item_id') for e in entries} == set(music)
for entry in entries:
    item = items[entry.get('item_id')]
    props = {p.get('name'): p.get('value') for p in item.findall('property')}
    assert entry.get('icon_key') == props.get('CustomIcon'), ('Catalog icon mismatch', entry.get('item_id'))
    assert entry.get('song_name') == props.get('SongName'), ('Catalog song mismatch', entry.get('item_id'))
for name in music:
    assert name in items and localization.get(name), ('Missing music item/name', name)
    props = {p.get('name'): p.get('value') for p in items[name].findall('property')}
    assert props.get('CustomIcon') == 'rb_music_cassette', ('Wrong artwork', name)
    assert not any('ListenAudiobook' in p.get('value', '') for p in items[name].iter('property'))
assert (ROOT / 'UIAtlases/ItemIconAtlas/rb_music_cassette.png').is_file()
assert (ROOT / 'Resources/FR_Music.unity3d').is_file(), 'Legacy audio bundle missing'

windows = parse('Config/XUi_InGame/windows.xml')
library = [w for w in windows.iter('window') if w.get('name') == 'rebirthMusicLibraryRoot']
assert len(library) == 1, 'Library window missing or duplicated'
names = [node.get('name') for node in library[0].iter() if node.get('name')]
assert len(names) == len(set(names)), 'Duplicate library control names'
for name in [*(f'musicSlot{i}' for i in range(24)), *(f'musicSelection{i}' for i in range(24)),
             *(f'musicAvailable{i}' for i in range(8)), 'musicPage',
             'musicPlay', 'musicPause', 'musicRemove', 'musicShuffle', 'musicClose']:
    assert name in names, ('Missing controller target', name)
for node in library[0].iter():
    for key in ('caption_key', 'tooltip_key', 'text_key'):
        if node.get(key):
            assert localization.get(node.get(key)), ('Missing UI localization', node.get(key))
groups = [g for g in parse('Config/XUi_InGame/xui.xml').iter('window_group')
          if g.get('name') == 'rebirthMusicLibrary']
assert len(groups) == 1
assert any(w.get('name') == 'rebirthMusicLibraryRoot' for w in groups[0].iter('window'))
print('PASS: device/components/recipe/profile, 23 music items, assets, library controls and localization. Runtime untested.')

ghost = [n for n in windows.iter('sprite') if n.get('name') == 'survivorOverviewGearWalkmanIcon']
assert len(ghost) == 1 and ghost[0].get('sprite') == 'WalkmanMod_FR'
assert ghost[0].get('color') == '200,200,200,180'
print('PASS: empty Walkman combined icon uses consistent gear-slot tint; visual verification pending.')
