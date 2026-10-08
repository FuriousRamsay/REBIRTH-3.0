from pathlib import Path
exec(Path('BuildScripts/validate_pc190_ui.py').read_text())
c=w.xpath('/windows/window[@name="rebirthCreativeRoot"]')[0]
b=c.xpath('.//*[@name="creativeInventory"]')[0]
assert b.get('height')=='889'
assert c.xpath('.//textfield[@name="searchInput"]')[0].get('justify')=='left'
scroll=b.xpath('.//*[@name="characterBackpackScroll"]')[0]
assert int(scroll.get('height'))==6*76
assert 416+int(scroll.get('height')) < int(b.get('height'))
toolbar=b.xpath('.//*[@controller="ContainerStandardControls"]')[0]
assert int(toolbar.get('pos').split(',')[0])+128+13 == 14+380
assert not w.xpath('//*[@clipping="HardClip"]')
print('PASS: six-row Creative backpack, bounded viewport, toolbar right edge, left-aligned search, valid clipping.')
