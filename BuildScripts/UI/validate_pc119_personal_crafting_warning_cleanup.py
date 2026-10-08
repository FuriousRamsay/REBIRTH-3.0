#!/usr/bin/env python3
from pathlib import Path
import re, sys, xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
WINDOWS = ROOT/'Config'/'XUi_InGame'/'windows.xml'
TEMPLATES = ROOT/'Config'/'XUi_InGame'/'templates.xml'
BASE = ROOT/'Scripts'/'Crafting'/'UI'/'PersonalCrafting'
ITEM = BASE/'XUiC_RebirthCraftingItemContext.cs'
ITEM_ENTRY = BASE/'XUiC_RebirthCraftingItemActionEntry.cs'
ACTIONS = BASE/'XUiC_RebirthCraftingActions.cs'
INV = BASE/'XUiC_RebirthCraftingInventoryScroll.cs'
QUEUE = BASE/'XUiC_RebirthCraftingQueue.cs'
ENTRY = BASE/'XUiC_RebirthCraftingQueueEntry.cs'
OWNER = BASE/'XUiC_RebirthPersonalCrafting.cs'
HUDPATCH = BASE/'RebirthPersonalCraftingHudSuppressionInstaller.cs'
OUTCOME = BASE/'XUiC_RebirthCraftingOutcome.cs'

checks=[]
def ck(name, cond, detail=''):
    checks.append((name,bool(cond),detail))

paths=(WINDOWS,TEMPLATES,ITEM,ITEM_ENTRY,ACTIONS,INV,QUEUE,ENTRY,OWNER,HUDPATCH,OUTCOME)
src={p:p.read_text(encoding='utf-8') for p in paths}
w,t,item,item_entry,actions,inv,queue,entry,owner,hudpatch,outcome=[src[p] for p in paths]

for p in (WINDOWS,TEMPLATES):
    try:
        ET.parse(p); ck('XML parses: '+p.name,True)
    except Exception as e:
        ck('XML parses: '+p.name,False,str(e))

# Selected Item: eliminate the native <W> -> W competing-frame presentation.
action_block=re.search(r'<rect name="rebirthCraftingItemActionList".*?</rect>\s*</rect>',w,re.S)
action=action_block.group(0) if action_block else ''
ck('Selected-item action list found',bool(action))
ck('All five selected-item entries use stable Rebirth ItemActionEntry controller',action.count('controller="RebirthCraftingItemActionEntry, RebirthUtils"')==5)
ck('Stable ItemActionEntry subclasses native action entry','class XUiC_RebirthCraftingItemActionEntry : XUiC_ItemActionEntry' in item_entry)
ck('Stable ItemActionEntry normalizes native keyboardButton by id','GetChildById("keyboardButton")' in item_entry and 'NormalizeShortcutText();' in item_entry)
ck('Shortcut normalization removes native angle brackets',"text[0] == '<'" in item_entry and "text[text.Length - 1] == '>'" in item_entry)
ck('Shortcut normalization commits immediately','keyboard.SetTextImmediately(normalized);' in item_entry)
ck('Outer ItemContext no longer rewrites shortcut text every frame','NormalizeShortcut(' not in item and 'ApplyActionLabelPresentation' not in item)
ck('Item action rebuild is hidden during native dirty assignment','SetControllerVisible(actionList, false);' in item and 'SetControllerVisible(actionList, true);' in item)
ck('Item action native deferred assignment is settled synchronously','actionList.SetCraftingActionList' in item and 'actionList.Update(0f);' in item)
ck('Item action geometry is change-gated','lastActionLayoutCount' in item and 'lastActionListSize' in item and 'if (!force && activeCount == lastActionLayoutCount' in item)
ck('Selected-item action caption remains optical y=-10',len(re.findall(r'<label[^>]*name="name"[^>]*pos="0,-10"',action))==5)
ck('Selected-item shortcut remains optical y=-10',len(re.findall(r'<label[^>]*name="keyboardButton"[^>]*-10',action))==5)
ck('No periodic Selected Item refresh timer remains','nextRefresh' not in item)

# Selected Recipe: real bindings, same hover style, no competing action poll.
for id_ in ('rebirthCraftingCraftShortcut','rebirthCraftingFavoriteShortcut','rebirthCraftingTrackShortcut'):
    ck('Recipe shortcut label exists: '+id_, f'name="{id_}"' in w)
ck('Recipe Craft shortcut uses GUI DPad Up binding','BindingText(actions.DPad_Up)' in actions)
ck('Recipe Favorite shortcut uses GUI DPad Right binding','BindingText(actions.DPad_Right)' in actions)
ck('Recipe Track shortcut uses GUI DPad Left binding','BindingText(actions.DPad_Left)' in actions)
ck('Recipe shortcuts remove display brackets',"value[0] == '<'" in actions and "value[value.Length - 1] == '>'" in actions)
ck('Recipe shortcuts commit immediately','label.SetTextImmediately(text);' in actions)
ck('Recipe shortcut labels are placed on left side after icon','SetRect(craftShortcut?.Controller, 38, -10, 32, 24);' in actions and 'favoriteX + 38' in actions and 'trackX + 38' in actions)
ck('Recipe buttons use native ItemAction hover sprite','button.HoverSpriteName = "ui_game_select_row";' in actions)
ck('Recipe buttons use native ItemAction hover color','button.HoverSpriteColor = Color.white;' in actions)
ck('Recipe action controller has no independent 0.20s state poll','nextStateRefresh' not in actions and 'Time.realtimeSinceStartup' not in actions)
ck('Recipe action geometry writes are change-aware','if (!force && width == lastWidth) return;' in actions and 'controller.ViewComponent.Position.x != position.x' in actions)

# Queue: stable visible widgets decoupled from native backing RecipeStack children.
qt=re.search(r'<rebirth_personal_crafting_queue_entry>(.*?)</rebirth_personal_crafting_queue_entry>',t,re.S)
chunk=qt.group(1) if qt else ''
ck('Queue template found',bool(chunk))
for id_ in ('rebirthQueueDisplayIcon','rebirthQueueName','rebirthQueueStatus','rebirthQueueDisplayCount','rebirthQueueProgressFill','rebirthQueueDisplayTimer','rebirthQueueCancelButton'):
    ck('Stable queue visible field exists: '+id_, f'name="{id_}"' in chunk)
for id_ in ('background','itemIcon','count','timer','cancel'):
    ck('Native RecipeStack contract child retained: '+id_, f'name="{id_}"' in chunk)
ck('Native RecipeStack contract is offscreen','pos="-10000,-10000"' in chunk and chunk.count('pos="-10000,-10000"')>=5)
ck('Queue entry has no visible binding RefreshBindings path','RefreshBindings(' not in entry and 'GetBindingValueInternal' not in entry)
ck('Queue visible labels are diff-written','SetLabelText(nameLabel' in entry and 'lastDisplayedCount' in entry and 'lastDisplayedWholeSecond' in entry)
ck('Queue visible labels commit immediately','label.SetTextImmediately(text);' in entry)
ck('Queue visible icon commits immediately','displayIcon.SetSpriteImmediately(spriteName);' in entry)
ck('Only active card has dynamic progress','SetProgress(craftingNow ? RebirthCraftingQueueBridge.GetCurrentItemProgress(this) : 0f, craftingNow);' in entry)
ck('Progress Fill update is change-aware','Mathf.Abs(progressFill.Fill - clamped) > 0.0005f' in entry)
ck('Only active RecipeStack receives native per-frame tick','bool nativeTick = HasRecipeForPresentation && IsCrafting;' in entry and re.search(r'if \(nativeTick\)\s+base\.Update\(dt\);',entry) is not None)
ck('Queued visible cards use lightweight presentation tree','if (!nativeTick && HasRecipeForPresentation)' in entry and 'UpdatePresentationTree(dt);' in entry)
ck('Custom cancel invokes native ForceCancel refund path','ForceCancel();' in entry)
ck('Cancel remains compact 18x18','name="rebirthQueueCancelButton"' in chunk and 'width="18" height="18"' in chunk and 'const int cancelSize = 18;' in entry)
ck('Cancel hover remains subtle','hoverscale="1.16"' in chunk)
ck('Queue owner has immediate post-native mutation sync','SyncVisiblePresentationAfterNativeMutation' in queue and 'SyncPresentationAfterNativeQueuePass' in queue)
ck('New queue card geometry is staged before visibility',queue.find('// Stage geometry first.') < queue.find('SetVisible(entry, intersectsViewport);') if '// Stage geometry first.' in queue and 'SetVisible(entry, intersectsViewport);' in queue else False)
ck('Queue card position writes are change-aware','entry.ViewComponent.Position.x != desiredPosition.x' in queue and 'entry.ViewComponent.Size.x != cardWidth' in queue)
ck('Queue reserves scrollbar gutter permanently','int hostWidth = Math.Max(260, width - 20 - (ScrollbarWidth + 8));' in queue)
ck('Queue host width no longer depends on scroll threshold','needsScroll ?' not in queue[queue.find('private void ApplyPresentation'):queue.find('private void EnsureRowsClipPanel')])
ck('Queue scrollbar remains 16/12','private const int ScrollbarTrackWidth = 16;' in queue and 'private const int ScrollbarThumbWidth = 12;' in queue)
ck('Queue SoftClip remains active','UIDrawCall.Clipping.SoftClip' in queue)
ck('Queue keeps partially visible edge cards','bool intersectsViewport = bottom > 0.01f && top < viewportHeight - 0.01f;' in queue)
actual_get_recipes_calls = [line for line in queue.splitlines() if 'GetRecipesToCraft(' in line and not line.lstrip().startswith('//')]
ck('Queue uses cached entries instead of GetRecipesToCraft allocations',len(actual_get_recipes_calls) == 0 and 'entries != null ? entries.Length : 0' in queue)

# Cancel: suppress native broad SetAllChildrenDirty only during the synchronous native refund mutation.
ck('Owner exposes scoped incremental queue mutation','BeginIncrementalQueueMutation' in owner and 'EndIncrementalQueueMutation' in owner)
ck('Owner no longer overrides non-exposed SetAllChildrenDirty','override void SetAllChildrenDirty' not in owner)
ck('Owner exposes incremental mutation state for Harmony gate','IsIncrementalQueueMutationActive => incrementalQueueMutationDepth > 0' in owner)
ck('Broad dirty suppression moved to XUiController Harmony prefix','HarmonyPatch(typeof(XUiController), nameof(XUiController.SetAllChildrenDirty))' in hudpatch and 'class RebirthPersonalCraftingSetAllChildrenDirtyPatch' in hudpatch)
ck('Broad dirty Harmony prefix is installed once','PatchClassOnce(Harmony, typeof(RebirthPersonalCraftingSetAllChildrenDirtyPatch))' in hudpatch)
ck('Broad dirty Harmony prefix only skips active Rebirth mutation','personalCrafting == null || !personalCrafting.IsIncrementalQueueMutationActive' in hudpatch and 'return false;' in hudpatch)
ck('Queue cancel brackets native ForceCancel with suppression scope','personalCrafting?.BeginIncrementalQueueMutation();' in entry and 'finally' in entry and 'personalCrafting?.EndIncrementalQueueMutation();' in entry)
ck('Post-cancel selective refresh avoids full-tree dirty','RefreshAfterIncrementalQueueMutation' in owner and 'RefreshAuthoritativePresentation(true)' in owner and 'XUiC_RebirthCraftingRecipeDetails' in owner)
ck('Post-cancel selective refresh does not explicitly redraw requirements/outcome twice','GetChildByType<XUiC_RebirthCraftingRequirements>()?.RefreshNow();' not in owner[owner.find('public void RefreshAfterIncrementalQueueMutation'):owner.find('public override void OnOpen')])

# Existing regressions must remain intact.
ck('Backpack still uses continuous grid interpolation','inventory.ViewComponent.Position = new Vector2i(baseGridPosition.x, baseGridPosition.y + Mathf.RoundToInt(pixelOffset));' in inv)
ck('Per-slot backpack culling remains removed','ApplySlotViewportVisibility' not in inv)
ck('Inventory scrollbar remains 16/12',re.search(r'name="rebirthCraftingInventoryScrollTrack"[^>]*width="16"',w) is not None and re.search(r'name="rebirthCraftingInventoryScrollThumb"[^>]*width="12"',w) is not None)
ck('Recipe scrollbar remains 16/12',re.search(r'name="rebirthCraftingRecipeScrollTrack"[^>]*width="16"',w) is not None and re.search(r'name="rebirthCraftingRecipeScrollThumb"[^>]*width="12"',w) is not None)
ck('All 50 native queue slots remain authored',w.count('<rebirth_personal_crafting_queue_entry name="rebirthCraftingQueueSlot')==50)
ck('Requirements count label remains removed','name="rebirthCraftingRequirementCount"' not in w)
ck('Result icon outline remains removed','name="rebirthCraftingOutcomeResultIconFrame"' not in w)
ck('Top-right HUD source suppression remains installed','RebirthPersonalCraftingHudSuppressionInstaller.EnsureInstalled();' in owner and 'XUiC_QuestTrackerWindow' in hudpatch and 'XUiC_Location' in hudpatch)
ck('Owner still has no direct IsDormant compile dependency','.IsDormant' not in owner)

# PC118 compile-surface recovery checks from the user's actual V3.2 project errors.
ck('CraftingActions Preserve attribute is fully qualified','[UnityEngine.Scripting.Preserve]' in actions)
ck('CraftingActions no longer overrides non-exposed InputStyleChanged','override void InputStyleChanged' not in actions)
ck('CraftingActions no longer registers for inaccessible input-style override','RegisterForInputStyleChanges();' not in actions)
ck('CraftingActions input-style refresh is change-gated','currentInputStyle != rebirthLastInputStyle' in actions and 'RefreshShortcutLabels();' in actions)
ck('ItemActionEntry has no PlayerInputManager compile dependency','PlayerInputManager' not in item_entry)
ck('ItemActionEntry has no non-exposed InputStyleChanged override','override void InputStyleChanged' not in item_entry)

# PC119 warning cleanup checks from the user's actual compiler warning.
ck('CraftingActions cache no longer hides XUiController.lastInputStyle','private PlayerInputManager.InputStyle rebirthLastInputStyle' in actions)
ck('CraftingActions contains no private field named lastInputStyle',re.search(r'private\s+PlayerInputManager\.InputStyle\s+lastInputStyle\b', actions) is None)
ck('CraftingActions change gate uses renamed cache','currentInputStyle != rebirthLastInputStyle' in actions and 'rebirthLastInputStyle = currentInputStyle;' in actions)

# Simple structural safeguards for every changed C# source.
def brace_balance(text):
    return text.count('{') == text.count('}')
for p in (ITEM,ITEM_ENTRY,ACTIONS,INV,QUEUE,ENTRY,OWNER,HUDPATCH,OUTCOME):
    ck('C# braces balanced: '+p.name,brace_balance(src[p]))

failed=[c for c in checks if not c[1]]
for i,(name,ok,detail) in enumerate(checks,1):
    print(f'{i:02d}. {"PASS" if ok else "FAIL"}: {name}' + (f' ({detail})' if detail else ''))
print(f'\nResult: {len(checks)-len(failed)}/{len(checks)} checks passed')
sys.exit(1 if failed else 0)
