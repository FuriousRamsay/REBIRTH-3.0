"""Check resolved scrollbar states and shared panel geometry."""
from pathlib import Path
exec(Path('BuildScripts/validate_pc194_ui.py').read_text())
for tree in (w,resolve('Config/XUi_InGame/templates.xml','../../Data/Config/XUi_InGame/templates.xml')):
    for node in tree.xpath('//*[@name]'):
        name=node.get('name').lower()
        if 'scroll' not in name and name not in ('listtrack','listthumb','thumb','track'): continue
        if node.tag not in ('button','rect','scrollbar','sprite'): continue
        assert node.get('use_selection_box') != 'true', (name,'selection glow')
        if node.tag=='button':
            assert node.get('hovercolor')==node.get('defaultcolor'), (name,'hover tint')
            assert node.get('selectedcolor')==node.get('defaultcolor'), (name,'selected tint')
common=resolve('Config/XUi_Common/templates.xml','../../Data/Config/XUi_Common/templates.xml')
assert common.xpath('/templates/defaultscrollbar/scrollbar')[0].get('use_selection_box')=='false'
explorer=w.xpath("/windows/window[@name='rebirthProgressionExplorerWindow']")[0]
for index in range(49):
    assert len(explorer.xpath(".//*[@name='btnProgressionNode%d']"%index))==1
    assert len(explorer.xpath(".//*[@name='progressionNodeName%d']"%index))==1
for panel in ('IncomingLane','OutgoingLane','FocusStage'):
    node=explorer.xpath(".//*[@name='progressionExplorer%s']"%panel)[0]
    assert node.xpath('./sprite[@fillcenter="false"]')
assert explorer.xpath(".//*[@name='progressionNodeCard24']")[0].get('height')=='352'
print('PASS: scrollbar hover/selected colors equal resting colors; no selection boxes; native template covered; explorer controls preserved.')
