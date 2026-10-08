from pathlib import Path
import sys, re
import xml.etree.ElementTree as ET

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[2]
checks=[]
def ck(name, ok, detail=''):
    checks.append((name,bool(ok),detail))
    print(('PASS' if ok else 'FAIL')+' - '+name+((' :: '+detail) if detail else ''))
def txt(rel):
    p=root/rel; ck('exists '+rel,p.exists()); return p.read_text(errors='ignore') if p.exists() else ''

xml=txt('Config/XUi_InGame/windows.xml')
bridge=txt('Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs')
scroll=txt('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs')
slot=txt('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventorySlot.cs')
ctx=txt('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemContext.cs')
cat=txt('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs')
layout=txt('Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs')
audit=txt('Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutAudit.cs')

try:
    ET.parse(root/'Config/XUi_InGame/windows.xml'); ck('windows.xml parses',True)
except Exception as e: ck('windows.xml parses',False,str(e))

# Wide Backpack: full-width presenter, still four rows and real Bag capacity.
ck('wide backpack uses proportional thirteen-column viewport after PC101 supersession','public const int Columns = 13;' in bridge)
ck('backpack remains four visible rows','public const int VisibleRows = 4;' in bridge)
ck('authored presenter covers 100-slot ceiling after PC101 supersession','public const int AuthoredRows = 8;' in bridge and 'AuthoredSlotCount = Columns * AuthoredRows' in bridge)
ck('XML grid is 13 x 8 presenter after PC101 supersession','name="inventory" rows="8" cols="13"' in xml)
ck('XML grid seeds proportional 60px pitch','cell_width="60" cell_height="60"' in xml and '<item_stack name="0" controller="RebirthCraftingInventorySlot, RebirthUtils"/>' in xml)
ck('scroll dynamically fits complete slot from horizontal width','availableW / Columns' in scroll and 'effectiveCell = Math.Max(38' in scroll and 'Vector3.one * uniformScale' in scroll)
ck('scroll still derives total rows from physical Bag','totalRows = Math.Max(1, (physical + Columns - 1) / Columns);' in scroll)
ck('physical Bag length remains authoritative','bag.GetSlots().Length' in bridge)
ck('no 32-slot capacity clamp', all(t not in bridge+scroll+slot for t in ['Take(32)','new ItemStack[32]','MaxItemCount = 32']))
ck('slot palette uses darker charcoal without root override after PC101','new Color32(30, 30, 36, 255)' in slot and 'Never tint the ItemStack root' in slot)
ck('slot palette preserves native slot behavior','XUiC_RebirthCraftingInventorySlot : XUiC_ItemStack' in slot and 'base.HandleClickComplete()' in slot)

# Selected Item must truly replace the top Selected Recipe surface.
cpos=xml.find('<rect name="rebirthCraftingItemContext"')
dpos=xml.find('<rect name="rebirthCraftingDetailsRegion"')
ip=xml.find('<rect name="rebirthCraftingInventoryRegion"')
rp=xml.find('<rect name="rebirthCraftingRequirementsRegion"')
ck('item context authored in center sequence',dpos>=0 and cpos>dpos and rp>cpos)
ck('item context is not inside inventory region', not (ip>=0 and cpos>ip and cpos<xml.find('<rect name="rebirthCraftingRightZone"',ip)))
ck('item context initially hidden','name="rebirthCraftingItemContext" depth="80" pos="0,0"' in xml and 'controller="RebirthCraftingItemContext, RebirthUtils" visible="false"' in xml)
ck('item context uses full top surface height','private const int ContextHeight = 232;' in ctx)
ck('selection hides Selected Recipe region','SetRecipeDetailsVisible(false)' in ctx)
ck('closing item restores Selected Recipe region','SetRecipeDetailsVisible(true)' in ctx)
ck('recipe details GameObject is actually toggled','UiTransform.gameObject.SetActive(visible)' in ctx)
ck('layout expands item context through former requirements surface','int itemContextHeight = detailsHeight + ZoneGap + requirementsHeight;' in layout and 'SetRect(owner.GetChildById("rebirthCraftingItemContext"), 0, 0, width, itemContextHeight);' in layout)
ck('item context remains center-owned and requirements-aware','rebirthCraftingItemContext' in audit or 'rebirthCraftingItemContext' in layout)
ck('item actions remain native ItemActionList','controller="ItemActionList"' in xml and 'SetCraftingActionList(XUiC_ItemActionList.ItemActionListTypes.Item, selectedSlot)' in ctx)
ck('item info comes from actual item','itemClass.GetIconName()' in ctx and 'selectedSlot.ItemNameText' in ctx and 'GetItemDescriptionKey()' in ctx)
ck('legacy floating positioning is gone', all(t not in ctx for t in ['selectedInTopHalf','bottomY','topY']))

# Recipe scrolling: one live authority, direct row wheel capture, raw fallback, persistent user target.
ck('catalogue has explicit active instance','private static XUiC_RebirthCraftingRecipeCatalogue activeInstance;' in cat)
ck('inactive catalogue cannot mutate live scroll','activeInstance != null && activeInstance != this' in cat and 'UpdateControllerTree(dt);' in cat)
ck('visible input can claim authority','private void ClaimScrollAuthority' in cat and 'authorityClaim reason=' in cat)
ck('row tree recursively receives XUi wheel events','WireScrollRecursive(row)' in cat and 'controller.OnScroll += HandleScrollEvent;' in cat)
ck('raw wheel fallback is present','Input.GetAxis("Mouse ScrollWheel")' in cat and 'PollMouseWheelFallback()' in cat)
ck('raw fallback is scoped to recipe rows','IsMouseOverRecipeArea()' in cat and 'IsMouseInsideColliderTree(row.ViewComponent.UiTransform)' in cat)
ck('input event claims the physically active catalogue','ClaimScrollAuthority("event:" + ControllerId(sender))' in cat)
ck('raw hover input claims active catalogue','ClaimScrollAuthority("poll")' in cat)
ck('thumb input claims active catalogue','ClaimScrollAuthority("thumb-drag")' in cat)
ck('track input claims active catalogue','ClaimScrollAuthority("track-press")' in cat)
ck('same-frame duplicate wheel is suppressed','lastHandledWheelFrame == Time.frameCount' in cat)
ck('user target survives temporary zero-range rebuild','private float authoritativeScrollTargetPixels;' in cat and 'targetScrollOffsetPixels = Mathf.Clamp(authoritativeScrollTargetPixels, 0f, MaxPixelOffset);' in cat)
ck('wheel updates authoritative target','SetScrollTarget(authoritativeScrollTargetPixels + direction * step, false, true);' in cat)
ck('thumb updates authoritative target','SetScrollTarget(desired, true, true);' in cat)
ck('track updates authoritative target','SetScrollTarget(normalized * MaxPixelOffset, false, true);' in cat)
ck('only deliberate rebuild reset clears authority','SetScrollTarget(0f, true, true);' in cat)
ck('scroll trace includes instance and authority','snapshot instance=' in cat and 'authority=' in cat and 'active=' in cat)
ck('smooth fractional interpolation retained','WheelStepRows = 0.62f' in cat and 'Mathf.MoveTowards(scrollOffsetPixels, targetScrollOffsetPixels' in cat)

# Top navigation baseline must be the same in authored and runtime layouts.
ck('all seven XML top-tab labels moved down to centered baseline',xml.count('Label" depth="7" pos="43,-18"')==7,str(xml.count('Label" depth="7" pos="43,-18"')))
ck('runtime top-tab baseline matches XML','"rebirthCraftingTab" + suffix + "Label"), 43, -18' in layout)
ck('tab icons retain 28px centered visual size',xml.count('width="28" height="28" atlas="RebirthUiIcons" sprite="rb_tab_')>=7)

# Source delimiter smoke checks.
for name,text in [('bridge',bridge),('scroll',scroll),('slot',slot),('context',ctx),('catalogue',cat),('layout',layout),('audit',audit)]:
    ck(name+' brace balance',text.count('{')==text.count('}'),f"{text.count('{')}/{text.count('}')}")
    ck(name+' paren balance',text.count('(')==text.count(')'),f"{text.count('(')}/{text.count(')')}")

passed=sum(1 for _,ok,_ in checks if ok); failed=[c for c in checks if not c[1]]
print(f'PC096 wide backpack/context swap/scroll authority validation: {passed} PASS / {len(failed)} FAIL')
for name,_,detail in failed: print('FAIL -',name,detail)
sys.exit(1 if failed else 0)
