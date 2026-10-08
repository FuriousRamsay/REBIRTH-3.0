"""Adapt verified 2.6 station definitions to current assets and workstation windows."""
import copy
import json
import xml.etree.ElementTree as ET
from content_model import ROOT, PRODUCTION

STATIONS = {
    'WorkbenchMortarPestle001_FR': ('Mortar and Pestle', None, {'resourceRockSmall':25}),
    'WorkbenchGasStove001_FR': ('Gas Stovetop', 'rebirthCookingToolsStovetop', {'resourceForgedIron':25, 'resourceMechanicalParts':5, 'resourceMetalPipe':10}),
    'WorkbenchIronOven001_FR': ('Iron Oven', 'rebirthCookingToolsOven', {'resourceForgedIron':40, 'resourceMechanicalParts':5, 'resourceMetalPipe':10}),
}

def append_property(parent, name, value):
    node = parent.find("property[@name='"+name+"']")
    if node is None:
        node = ET.SubElement(parent, 'property', name=name)
    node.set('value', str(value))

def build():
    source = json.loads((PRODUCTION/'source_audit.json').read_text())['station_definitions']
    blocks = ET.Element('configs')
    body = ET.SubElement(blocks, 'append', xpath='/blocks')
    windows = ET.Element('configs')
    window_body = ET.SubElement(windows, 'append', xpath='/windows')
    xui = ET.Element('configs')
    xui_body = ET.SubElement(xui, 'append', xpath='/xui')
    native = ET.parse(ROOT.parent.parent/'Data/Config/XUi_InGame/windows.xml').getroot()
    template = native.find("window[@name='windowToolsCampfire']")
    for ident, (title, tool_window, costs) in STATIONS.items():
        block = ET.fromstring(source[ident])
        # The 2.6 pickup materials were mod definitions, not native 3.2 materials.
        append_property(block, 'Material', 'MstoneFurniture' if ident=='WorkbenchMortarPestle001_FR' else 'Mmetal')
        for child in list(block):
            if child.tag=='property' and child.get('name') in ('UnlockedBy','OpenSound','CloseSound','CraftSound','CraftCompleteSound','ParticleName','ParticleOffset'):
                block.remove(child)
            elif child.tag=='drop':
                block.remove(child)
        append_property(block, 'OpenSound', 'open_workbench')
        append_property(block, 'CloseSound', 'close_workbench')
        repair = block.find("property[@class='RepairItems']")
        if ident=='WorkbenchMortarPestle001_FR':
            repair.clear()
            repair.set('class', 'RepairItems')
            append_property(repair, 'resourceRockSmall', 5)
        else:
            append_property(block, 'Class', 'Campfire')
            append_property(block, 'FuelType', 'ammoGasCan')
            workstation = block.find("property[@class='Workstation']")
            append_property(workstation, 'Modules', 'tools,output,fuel')
            if ident=='WorkbenchGasStove001_FR':
                append_property(workstation, 'CraftingAreaRecipes', ident+',campfire')
        ET.SubElement(block, 'drop', event='Destroy', name='resourceScrapIron' if tool_window else 'resourceRockSmall', count='5')
        ET.SubElement(block, 'drop', event='Fall', name=ident, count='1', prob='1', stick_chance='1')
        body.append(block)
        group = ET.SubElement(xui_body, 'window_group', name='workstation_'+ident,
            controller='XUiC_WorkstationWindowGroup', open_backpack_on_open='true',
            close_compass_on_open='true', defaultselected='bp.content')
        names = ['windowCraftingList','craftingInfoPanel','rebirthCraftingQueueStation']
        if tool_window:
            names += [tool_window, 'windowFuel']
        names += ['windowOutput','windowNonPagingHeader']
        for name in names:
            ET.SubElement(group, 'window', name=name)
        if tool_window:
            window = copy.deepcopy(template)
            window.set('name', tool_window)
            window.set('width', '330')
            grid = window.find('.//grid')
            grid.set('pos','53,-3')
            grid.set('required_tools', 'toolCookingPot,rebirthCookingFryingPan,toolCookingGrill' if 'Stovetop' in tool_window else 'FuriousRamsayBakingPan')
            window_body.append(window)
    # Keep the existing requirement controller and source-count bindings, but expose
    # all nine ingredients used by the catalogue's most complex dish.
    grid_path = "/windows/window[@name='craftingInfoPanel']//grid[@controller='IngredientList']"
    for attribute, value in [('cols','3'),('rows','3'),('cell_width','208')]:
        ET.SubElement(windows, 'setattribute', xpath=grid_path, name=attribute).text = value
    ET.SubElement(windows, 'remove', xpath=grid_path+'/*')
    replacement = ET.SubElement(windows, 'append', xpath=grid_path)
    templates = ET.parse(ROOT/'Config/XUi_InGame/templates.xml').getroot()
    row = copy.deepcopy(templates.find('.//rebirth_crafting_ingredient_card/rect'))
    row.set('name','0')
    row.set('width','206')
    for node in row.findall('sprite'):
        if node.get('name')=='cardBackground':
            node.set('width','206')
        elif node.get('name')=='icon':
            node.set('width','34'); node.set('height','34'); node.set('pos','23,-27')
    for node in row.findall('label'):
        if node.get('name')=='name':
            node.set('pos','46,-5'); node.set('width','152'); node.set('font_size','13')
        elif node.get('name')=='needcount':
            node.set('pos','118,-29'); node.set('width','80'); node.set('font_size','14')
        else:
            node.set('pos','46,-31'); node.set('width','70'); node.set('font_size','9')
    replacement.append(row)
    return blocks, windows, xui
