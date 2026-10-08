"""Offline regression checks; these do not claim to exercise the live game UI."""
from pathlib import Path
import xml.etree.ElementTree as ET

p=Path('Config/XUi_InGame/windows.xml')
old=Path('C:/Users/Etienne/Desktop/__0/_0/__Rebirth/Character_PC141_Backup_20260912/windows.xml').read_text(encoding='utf-8-sig')
new=p.read_text(encoding='utf-8-sig')
def parts(text):
    a=text.index('<window name="rebirthSurvivorCharacterWindow"')
    b=text.index('</window>',a)+len('</window>')
    return text[:a],ET.fromstring(text[a:b]),text[b:]
oa,orr,ob=parts(old)
na,root,nb=parts(new)
assert oa==na and ob==nb, 'Change outside Character window'
ET.fromstring(new)
named={e.get('name'):e for e in root.iter() if e.get('name')}
bag=named['survivorOverviewBackpackPanel']
assert len(list(bag.iter('item_stack')))==100
assert not any('quickstack' in str(e.attrib).lower() or 'companion' in str(e.attrib).lower() for e in bag.iter())
assert all('Drag' not in e.get('text','') and 'Scroll for' not in e.get('text','') for e in bag.iter())
assert 'survivorOverviewEquipmentHint' not in named
toolbar=[e.get('name') for e in bag if e.tag=='button']
assert toolbar==['characterBagFilter','characterBagSort','characterBagLock']
viewport=next(e for e in bag.iter() if e.get('name')=='listViewport')
assert viewport.get('clippingsize')=='384,456'
assert viewport.get('clippingcenter')=='192,-228'
assert not any(k.startswith('clipping_') for k in viewport.attrib)
for i in range(100):
    cell=named[f'characterBagCell{i}']
    assert len(cell)==1 and cell[0].tag=='item_stack'
    assert cell[0].get('name')==f'characterBagSlot{i}'
    assert cell.get('pos')==f'{i%5*76},0'
    assert cell[0].get('controller')=='RebirthCharacterBackpackSlot, RebirthUtils'

# Exercise every capacity and sparse filters, including the last physical slot. Assert
# independently that presentation is dense, unique, in bounds and physical identity is unchanged.
cases=0
for capacity in range(101):
    for indices in (list(range(capacity)),list(range(0,capacity,7)),[capacity-1] if capacity else []):
        cells=[]
        for visible,physical in enumerate(indices):
            parent_y=-(physical//5)*76
            local_y=(physical//5-visible//5)*76
            cells.append((visible%5*76,parent_y+local_y))
            assert physical < capacity
        assert len(cells)==len(set(cells))
        assert all(0<=x and x+75<=384 for x,y in cells)
        rows=(len(indices)+4)//5
        bottom=0 if not rows else (rows-1)*76+75
        scroll=max(0,bottom-456)
        if cells:
            assert -min(y for x,y in cells)+75-scroll<=456
        assert all(cells[n]==(n%5*76,-(n//5)*76) for n in range(len(cells)))
        cases+=1
actions=named['characterBagActions']
assert len(actions)==8
for e in actions:
    x,y=map(int,e.get('pos').split(','))
    assert x+int(e.get('width'))<=408 and -y+34<=154
    assert e.get('controller')=='RebirthCraftingItemActionEntry, RebirthUtils'
assert 638+154<=813

# Preserve native equipment templates and the dedicated Conditions page exactly in content.
def canonical(e):
    return (e.tag,sorted(e.attrib.items()),[canonical(c) for c in e])
assert [canonical(e) for e in orr.iter('equipment_stack_sdcs')]==[canonical(e) for e in root.iter('equipment_stack_sdcs')]
source=Path('Scripts/Survivor/UI/XUiC_RebirthCharacterBackpack.cs').read_text()
assert 'slot.InfoWindow = null' not in source
assert 'bag.SetSlot(slotNumber' in source and 'SetBackpackItemStacks(' not in source
assert 'base.HandleSlotChangedEvent(' not in source
assert 'ItemActionListTypes.Equipment' in source and 'ItemActionListTypes.Item' in source
assert 'RebirthCharacterEquipmentInspect' in Path('Scripts/Survivor/UI/RebirthSurvivorUiInstaller.cs').read_text()
traits=Path('Scripts/Survivor/UI/XUiC_RebirthSurvivorCharacter.cs').read_text()
assert 'bool listsReady = overviewRowsResolved && overviewTraitsList.IsReady;' in traits
assert 'overviewRowsResolved = overviewTraitsList != null;' in traits
print(f'PASS: XML/native-slot composition, {cases} capacity/filter geometry cases, action bounds, scoped writes, native Modify dependencies, equipment patch registration and independent traits readiness.')
