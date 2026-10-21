"""Parent-run source geometry check; no game input or build.

Compare authored vehicle editor geometry with the actual item Modify XML. This
does not establish live NGUI collider bounds or item conservation.
"""
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
item = ET.parse(ROOT / 'Config/XUi_InGame/windows.xml').getroot().find(".//window[@name='rebirthModifyEditor']")
vehicle = ET.parse(ROOT / 'Config/_Vehicles/vehicle_editor_windows.xml').getroot().find(".//window[@name='windowVehicle']")
assert item is not None and vehicle is not None

def named(root, name):
    value = root.find(f".//*[@name='{name}']")
    assert value is not None, name
    return value

def same(left, right, keys):
    for key in keys:
        assert left.get(key) == right.get(key), (key, left.attrib, right.attrib)

same(item, vehicle, ('anchor', 'pos', 'width', 'height', 'cursor_area'))
for source, target in [('editorHeader', 'vehicleEditorHeader'), ('editorBody', 'vehicleEditorBody'),
                       ('editorEquipment', 'vehicleEquipment'), ('editorBackpack', 'editorBackpack'),
                       ('editorStats', 'vehicleEditorStats'), ('editorCosmeticMods', 'vehicleEditorCosmeticMods')]:
    same(named(item, source), named(vehicle, target), ('pos', 'width', 'height'))
same(named(item, 'editorBack'), named(vehicle, 'vehicleEditorClose'), ('pos', 'width', 'height', 'sprite', 'defaultcolor', 'type', 'fillcenter'))
same(named(item, 'modifyFramerebirthModifyEditor'), named(vehicle, 'modifyFramevehicleEditor'), ('width', 'height', 'sprite', 'color', 'type', 'fillcenter'))
same(named(item, 'cosmeticparts'), named(vehicle, 'cosmeticparts'), ('pos', 'rows', 'cols', 'cell_width', 'cell_height', 'controller'))
cosmetic = named(vehicle, 'cosmeticparts').find('item_part_slot')
assert cosmetic is not None and cosmetic.get('controller') == 'RebirthVehicleCosmeticSlot, RebirthUtils'
assert cosmetic.get('empty_slot_text_key') == 'xuiEmptyCosmeticSlot'
parts = named(vehicle, 'parts')
assert parts.get('controller') == 'VehiclePartStackGrid' and parts.get('rows') == '8'
assert parts.find('rect').get('controller') == 'RebirthVehiclePartSlot, RebirthUtils'
for action in ('btnRefuel', 'btnRepair'):
    assert named(vehicle, action).find('button').get('name') == 'clickable'
assert len(vehicle.findall('.//item_stack[@controller="RebirthEditorBackpackSlot, RebirthUtils"]')) == len(item.findall('.//item_stack[@controller="RebirthEditorBackpackSlot, RebirthUtils"]'))
controller = (ROOT / 'Scripts/Vehicles/Camper/XUiC_RebirthVehicleFrame.cs').read_text(encoding='utf-8-sig')
assert 'private void Fit()' not in controller and 'localScale=' not in controller
print('PASS authored item Modify/vehicle editor geometry and retained native controller structure; live input/collider/layout unqualified.')
