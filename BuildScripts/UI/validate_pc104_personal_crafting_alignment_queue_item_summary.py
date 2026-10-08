#!/usr/bin/env python3
from pathlib import Path
import sys, xml.etree.ElementTree as ET
root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('.')

def read(rel):
    return (root/rel).read_text(encoding='utf-8',errors='ignore')
xml=read('Config/XUi_InGame/windows.xml')
tpl=read('Config/XUi_InGame/templates.xml')
details=read('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeDetails.cs')
entry=read('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeEntry.cs')
ctx=read('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemContext.cs')
queue=read('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueue.cs')
qentry=read('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueueEntry.cs')
checks=[]
def ck(name,cond):
    checks.append((name,bool(cond))); print(('PASS ' if cond else 'FAIL ')+name)
ET.parse(root/'Config/XUi_InGame/windows.xml'); ET.parse(root/'Config/XUi_InGame/templates.xml')
ck('craft title/value share exact runtime y', 'int craftTimeY = -(contentTop + 101);' in details and details.count('craftTimeY') >= 3)
ck('batch title deliberately below craft-count top', 'batchControlY - 9' in details)
ck('requirement HAVE labels right justified', xml.count('rebirthCraftingRequirementHaveLabel') >= 6 and xml.count('pos="206,-2" width="54" height="19" font_size="17" justify="right"') >= 6)
ck('requirement HAVE values right justified', xml.count('pos="266,-2" width="58" height="19" font_size="17" justify="right"') >= 6)
ck('requirement NEED labels right justified', xml.count('pos="206,-21" width="54" height="19" font_size="17" justify="right"') >= 6)
ck('requirement NEED values right justified', xml.count('pos="266,-21" width="58" height="19" font_size="17" justify="right"') >= 6)
ck('recipe status visual baseline lowered relative to state icon', 'stateY - 4' in entry and 'stateY)' in entry)
ck('item action labels reassert every frame', 'ApplyActionLabelPresentation();' in ctx and ctx.count('ApplyActionLabelPresentation();') >= 2)
ck('item action caption and shortcut independent', 'SetRect(nameController, 0, labelY, cell, 24);' in ctx and 'SetRect(keyboardController, cell - 48, labelY, 38, 24);' in ctx)
ck('item action geometric vertical center', '(listHeight - 24) / 2' in ctx)
ck('shortcut normalized uppercase without angle wrappers', 'NormalizeShortcut' in ctx and "text[0] == '<'" in ctx)
ck('metabolism consumables suppress fake quality/durability', 'RebirthConsumableResolver.TryResolve(value, out consumable)' in ctx and 'return sb.ToString();' in ctx)
ck('drink summary reports remaining volume', 'RebirthLiquidContainerService.GetRemainingMl' in ctx and 'RebirthLiquidContainerService.FormatVolume' in ctx)
ck('queue uses two columns', 'private const int Columns = 2;' in queue and 'private const int VisibleGridRows = 2;' in queue)
ck('queue keeps four live native entries visible', 'private const int VisibleEntries = 4;' in queue)
ck('queue card compact height', 'private const int CardHeight = 124;' in queue)
ck('queue card places icon at top', 'SetRect(GetChildById("itemIcon"), iconX, -8, iconSize, iconSize);' in qentry)
ck('queue progress bar thicker', 'int progressHeight = 10;' in qentry)
ck('queue quantity above progress bar', 'rebirthQueueQtyCaption' in qentry and '-55' in qentry and 'progressY = -78' in qentry)
ck('queue time below progress bar', 'rebirthQueueTimeCaption' in qentry and '-94' in qentry)
ck('queue label/value text same size', 'SetLabelFont("rebirthQueueQtyCaption", 14);' in qentry and 'SetLabelFont("count", 14);' in qentry and 'SetLabelFont("rebirthQueueTimeCaption", 14);' in qentry and 'SetLabelFont("timer", 14);' in qentry)
ck('queue template matches two-column compact card defaults', 'width="185" height="124"' in tpl and 'height="10"' in tpl)
for n,t in [('details',details),('entry',entry),('context',ctx),('queue',queue),('queue entry',qentry)]:
    ck(n+' brace balance', t.count('{')==t.count('}'))
failed=[n for n,v in checks if not v]
print(f'RESULT: {len(checks)-len(failed)}/{len(checks)} PASS')
if failed:
    print('FAILED:', ', '.join(failed)); raise SystemExit(1)
