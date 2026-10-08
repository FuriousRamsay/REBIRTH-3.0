from pathlib import Path
from lxml import etree
import re, sys

root=Path(__file__).resolve().parents[2]
checks=[]
def check(name,ok,detail=''):
    checks.append((name,bool(ok),detail))

files={
'windows':root/'Config/XUi_InGame/windows.xml',
'xui':root/'Config/XUi_InGame/xui.xml',
'root':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs',
'layout':root/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs',
'catalogue':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs',
'details':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeDetails.cs',
'actions':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingActions.cs',
'bridge':root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingCommandBridge.cs',
}
for n,p in files.items(): check('exists '+p.relative_to(root).as_posix(),p.exists())
for n in ['windows','xui']:
    try: etree.parse(str(files[n])); check('xml parse '+files[n].name,True)
    except Exception as e: check('xml parse '+files[n].name,False,str(e))

wt=files['windows'].read_text(errors='ignore'); xt=files['xui'].read_text(errors='ignore')
ot=files['root'].read_text(errors='ignore'); lt=files['layout'].read_text(errors='ignore')
ct=files['catalogue'].read_text(errors='ignore'); dt=files['details'].read_text(errors='ignore')
at=files['actions'].read_text(errors='ignore'); bt=files['bridge'].read_text(errors='ignore')

# Rebirth-only architecture and native Base Game preservation.
check('rebirth-only conditional retained',"character_progression('Rebirth')" in xt)
check('public crafting route retained',"window_group[@name='crafting']" in xt)
check('base-game group not globally replaced','<set xpath="/xui/window_group[@name=\'crafting\']"' not in xt)
check('custom root remains sole Rebirth child','<remove xpath="/xui/window_group[@name=\'crafting\']/window"/>' in xt and 'name="rebirthPersonalCraftingRoot"' in xt)

# Permanent selected-recipe region.
start=wt.find('<rect name="rebirthCraftingDetailsRegion"')
end=wt.find('<rect name="rebirthCraftingRequirementsRegion"',start)
region=wt[start:end if end>start else None] if start>=0 else ''
check('selected recipe region controller','controller="RebirthCraftingRecipeDetails, RebirthUtils"' in region)
for node in ['rebirthCraftingSelectedRecipeIcon','rebirthCraftingSelectedRecipeName','rebirthCraftingSelectedRecipeType',
             'rebirthCraftingSelectedRecipeDescription','rebirthCraftingSelectedRecipeKnowledge',
             'rebirthCraftingSelectedRecipeTimeTitle','rebirthCraftingSelectedRecipeTimeValue',
             'rebirthCraftingRecipeCraftCount','rebirthCraftingActionsStrip']:
    check('details node '+node, f'name="{node}"' in region)
check('knowledge button exists','name="btnRebirthCraftingViewKnowledge"' in region)
check('batch uses native RecipeCraftCount','name="rebirthCraftingRecipeCraftCount"' in region and 'controller="RecipeCraftCount"' in region)
recipe_only=region[:region.find('rebirthCraftingItemContext')] if 'rebirthCraftingItemContext' in region else region
check('no native ItemActionList visual in Selected Recipe details','ItemActionList' not in recipe_only and '<item_action_entry' not in recipe_only)
check('custom horizontal Craft action','name="btnRebirthCraftingCraft"' in region)
check('custom horizontal Favorite action','name="btnRebirthCraftingFavorite"' in region)
check('custom horizontal Track action','name="btnRebirthCraftingTrack"' in region)

# Real native behavioral queue is present. Chunk I may promote the hidden Chunk-E host into the
# visible Rebirth queue, but it must remain an XUiC_CraftingQueue contract with exactly four real
# runtime RecipeStack-derived entries rather than a fake presentation-only queue.
qstart=wt.find('<rect name="rebirthCraftingQueueRegion"')
qwindow=wt[qstart:qstart+5000] if qstart>=0 else ''
legacy_queue = ('name="rebirthCraftingRuntimeQueue"' in qwindow and
                'controller="CraftingQueue"' in qwindow and
                'name="rebirthCraftingRuntimeQueue" pos="-5000,-5000"' in qwindow and
                'rows="1" cols="4"' in qwindow and '<recipe_stack name="0"/>' in qwindow)
chunk_i_queue = ('name="rebirthCraftingQueueController"' in qwindow and
                 'controller="RebirthCraftingQueue, RebirthUtils"' in qwindow and
                 qwindow.count('<rebirth_personal_crafting_queue_entry name="rebirthCraftingQueueSlot') == 4)
check('real runtime queue host exists', legacy_queue or chunk_i_queue)
check('runtime queue remains native-contract based', legacy_queue or chunk_i_queue)
check('no duplicate hidden and visible runtime queues', not ('rebirthCraftingRuntimeQueue' in qwindow and 'rebirthCraftingQueueController' in qwindow))
check('runtime queue is exactly four real slots', legacy_queue or chunk_i_queue)
check('root captures native behavioral queue','craftingQueue = GetChildByType<XUiC_CraftingQueue>();' in ot)
check('root captures native craft count','craftCountControl = GetChildByType<XUiC_RecipeCraftCount>();' in ot)

# Native data/command authority.
check('details uses native item icon authority','GetPropertyOverride("CustomIcon"' in dt and 'ItemIconAtlas' in region)
check('details uses native description authority','GetItemDescriptionKey()' in dt and 'DescriptionKey' in dt)
check('details uses native craft tier','GetCraftingTier(' in dt)
check('details uses native craft time','XUiM_Recipes.GetRecipeCraftTime' in dt)
check('details uses Rebirth Knowledge projection','RebirthCraftOutcomeService.Build' in dt)
check('knowledge deep link focus','GetKnowledgeFocusId' in dt and 'RebirthProgressionExplorerLaunchRequest' in dt)
check('knowledge deep link returns to crafting','new RebirthProgressionExplorerReturnContext(' in dt and '"crafting"' in dt)
check('craft bridge uses native Craft action','new ItemActionEntryCraft' in bt)
check('favorite bridge uses native Favorite action','new ItemActionEntryFavorite' in bt)
check('track bridge uses native Track action','new ItemActionEntryTrackRecipe' in bt)
check('native action enabled state honored','action.RefreshEnabled();' in bt and 'action.OnDisabledActivate();' in bt)
check('native action sound behavior retained','Manager.PlayInsidePlayerHead(action.SoundName)' in bt and 'Manager.PlayInsidePlayerHead(action.DisabledSound)' in bt)
check('catalogue can restore offscreen selection for native action','GetBehaviorEntryForRecipe' in ct and 'ApplyRows();' in ct)
check('catalogue publishes automatic selection','PublishSelectionIfChanged(preferred);' in ct)

# Stable layout: permanent regions; no inventory/item panel swapping contract.
check('details minimum preserves action strip',('232, 300' in lt) or ('inventoryMinHeight = 170' in lt and '190, 300' in lt))
check('details controller polls catalogue CurrentRecipe','catalogue.CurrentRecipe' in dt)
check('details never opens native item info','itemInfoPanel' not in dt and 'emptyInfoPanel' not in dt and 'craftingInfoPanel' not in dt)

# Explicit forward freeze from user clarification: 8x4 is viewport only, not capacity.
# Chunk G implements it; Chunk E must not add any 32-slot capacity clamp.
combined=dt+at+bt+ct+ot+lt+region
check('chunk E does not impose 32-slot backpack capacity','BackpackSize = 32' not in combined and 'capacity = 32' not in combined and 'Take(32)' not in combined)

# Basic source delimiter smoke test.
for key in ['root','layout','catalogue','details','actions','bridge']:
    text=files[key].read_text(errors='ignore')
    check('brace balance '+files[key].name,text.count('{')==text.count('}'),f"{text.count('{')}/{text.count('}')}")
    check('paren balance '+files[key].name,text.count('(')==text.count(')'),f"{text.count('(')}/{text.count(')')}")

passed=sum(1 for _,ok,_ in checks if ok); failed=[x for x in checks if not x[1]]
print(f'PC079 CHUNK E VALIDATION: {passed} PASS / {len(failed)} FAIL')
for name,ok,detail in failed: print('FAIL -',name,detail)
if failed: sys.exit(1)
