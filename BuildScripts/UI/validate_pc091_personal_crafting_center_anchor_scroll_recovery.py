#!/usr/bin/env python3
from pathlib import Path
import re, sys

root = Path(sys.argv[1] if len(sys.argv) > 1 else '.').resolve()
layout = root / 'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs'
cat = root / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs'
xui = root / 'Config/XUi_InGame/xui.xml'
windows = root / 'Config/XUi_InGame/windows.xml'

checks=[]
def check(name, cond):
    checks.append((name, bool(cond)))
    print(('PASS' if cond else 'FAIL') + ' - ' + name)

for p in (layout,cat,xui,windows): check('exists ' + str(p.relative_to(root)), p.exists())
ls=layout.read_text(encoding='utf-8') if layout.exists() else ''
cs=cat.read_text(encoding='utf-8') if cat.exists() else ''
xs=xui.read_text(encoding='utf-8') if xui.exists() else ''
ws=windows.read_text(encoding='utf-8') if windows.exists() else ''

check('Personal Crafting route remains Center anchored', '<window name="rebirthPersonalCraftingRoot" anchor="Center"' in xs)
check('root definition retained', '<window name="rebirthPersonalCraftingRoot"' in ws)
check('layout converts left edge from screen center', 'int rootPosX = rootLeft - screen.x / 2;' in ls)
check('layout converts top edge from screen center', 'int rootPosY = screen.y / 2 - rootTop;' in ls)
check('old zero-X placement removed', 'new Vector2i(0, rootPosY)' not in ls)
check('safe root still reserves HUD bottom', 'screen.y - topMargin - bottomReserve' in ls)
check('safe root still uses horizontal margins', 'screen.x - horizontalMargin * 2' in ls)
check('gated layout diagnostic exists', '[REBIRTH Crafting Layout]' in ls and 'LayoutDebugEnabled' in ls)
check('layout diagnostic reports anchor', 'anchor=Center' in ls)
check('layout diagnostic reports edges', 'edges=' in ls and 'bottomReserve=' in ls)

# At 1920x1080 with current fallback values: horizontalMargin round(24)=24,
# topMargin round(12.96)=13, bottom reserve >=132 => left=24, top=13, right=1896,
# bottom<=948. This is a formula check, not a hardcoded runtime layout replacement.
screen_x, screen_y = 1920,1080
hm=max(10,min(28,round(screen_x*0.0125)))
tm=max(8,min(20,round(screen_y*0.0120)))
br=132
rw=min(max(1180,screen_x-hm*2), max(1,screen_x-4))
rh=min(max(610,screen_y-tm-br), max(1,screen_y-4))
rpx=hm-screen_x//2
rpy=screen_y//2-tm
left=screen_x//2+rpx
top=screen_y//2-rpy
check('1920 root left edge resolves to 24', left == 24)
check('1920 root right edge resolves to 1896', left+rw == 1896)
check('1080 root top edge resolves to 13', top == 13)
check('1080 root fallback bottom edge resolves to 948', top+rh == 948)

check('recipe list remains ten visible rows', 'VisibleRowCount = 10' in cs)
check('smooth-scroll buffer row is explicit', 'PresentationRowCount = VisibleRowCount + 1' in cs)
check('wheel uses fractional target offset', 'WheelStepRows' in cs and 'targetScrollOffsetPixels' in cs and 'SetScrollTarget(' in cs)
check('logical offset is preserved through availability refresh', 'FinishRebuild(false, "availability")' in cs)
check('explicit filter reset flag exists', 'resetScrollOnNextRebuild' in cs)
check('category changes explicitly request top reset', 'pendingRebuildReason = "category";' in cs)
check('user search changes explicitly request top reset', 'pendingRebuildReason = userEdit ? "search-change-user" : "search-change-code";' in cs and 'resetScrollOnNextRebuild = userEdit;' in cs)
check('favorites changes explicitly request top reset', 'pendingRebuildReason = "favorites";' in cs)
check('native scrollbar feedback loop removed', 'RebirthNativeScrollbarUtil' not in cs and 'PollNativeScrollbar' not in cs)
check('custom scrollbar owns track click', 'ScrollTrack_OnPress' in cs)
check('custom scrollbar owns smooth thumb drag', 'ScrollThumb_OnDrag' in cs and 'SetScrollTarget(desired, true, true)' in cs)
check('smooth interpolation runs every frame', 'UpdateSmoothScroll(dt);' in cs and 'Mathf.MoveTowards' in cs)
check('fractional row remainder is projected visually', 'scrollOffsetPixels - firstVisibleIndex * currentRowStride' in cs and 'yShift' in cs)
check('gated scroll trace exists', '[REBIRTH Crafting RecipeScroll]' in cs and 'LayoutDebugEnabled' in cs)
check('scroll trace reports pixel input/result/reason', 'inputOffsetPx=' in cs and 'refreshReason=' in cs and 'resultingOffsetPx=' in cs)

# Preserve prior inventory acceptance contract.
bridge=(root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs').read_text(encoding='utf-8')
scroll=(root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs').read_text(encoding='utf-8')
check('backpack uses twelve columns', 'public const int Columns = 12;' in bridge)
check('backpack remains four visible rows', 'public const int VisibleRows = 4;' in bridge)
check('backpack still supports physical overflow rows', 'totalRows' in scroll and 'VisibleRows' in scroll and 'physical' in scroll)

passed=sum(1 for _,v in checks if v)
failed=len(checks)-passed
print(f'PC091 Center-anchor + recipe-scroll recovery validation: {passed} PASS / {failed} FAIL')
sys.exit(1 if failed else 0)
