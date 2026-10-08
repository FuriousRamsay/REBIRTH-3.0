#!/usr/bin/env python3
from pathlib import Path
import sys
from lxml import etree

root=Path('.')
if '--root' in sys.argv:
    i=sys.argv.index('--root'); root=Path(sys.argv[i+1])

errors=[]
xui=(root/'Config/XUi_Menu/windows.xml').read_text(encoding='utf-8')
creator=(root/'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs').read_text(encoding='utf-8')
etree.parse(str(root/'Config/XUi_Menu/windows.xml'))

if 'name="backgroundStartingItemsText"' in xui:
    errors.append('old compact Starting Items text remains')
if 'font_size="19" text_key="xuiRebirthSurvivorStartingItems"' not in xui:
    errors.append('Starting Items heading is not 19px')
for i in range(8):
    for token in [f'backgroundStartingItemRow{i}',f'backgroundStartingItemIcon{i}',f'backgroundStartingItemName{i}',f'backgroundStartingItemMeta{i}']:
        if token not in xui: errors.append('missing '+token)
section=xui[xui.find('backgroundStartingItemsPanel'):xui.find('creatorDetailScroll')]
if '<defaultscrollbar/>' not in section:
    errors.append('native/default scrollbar missing')
if 'visible="true"' not in section:
    errors.append('Starting Items native scroll host is not authored visible')
if 'name="backgroundStartingItemsNativeScrollProxy"' not in section or 'height="132"' not in section:
    errors.append('Starting Items native proxy does not match the eight-slot grid track height')
if 'IsVisible = total > BackgroundStartingItemRows' not in creator:
    errors.append('Starting Items fallback scrollbar is not hidden when all eight slots fit')
for token in ['BackgroundStartingItemRows = 8','SetStartingItemIcon','BackgroundStartingItemsScroll','PollBackgroundStartingItemsNativeScroll','backgroundStartingItemsNativeLayoutRefreshFrames','RefreshNativeScrollView(backgroundStartingItemsNativeScrollView)']:
    if token not in creator: errors.append('missing controller '+token)
if 'ItemClass.GetItemClass(item.ItemId, false)' not in creator:
    errors.append('item icons not resolved from actual item definitions')

print('Experience Starting Items rows audit errors='+str(len(errors)))
for e in errors: print('ERROR:',e)
sys.exit(1 if errors else 0)
