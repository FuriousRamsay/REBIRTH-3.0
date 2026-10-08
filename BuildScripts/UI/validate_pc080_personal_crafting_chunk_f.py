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
'loc':root/'Config/Localization.csv',
'details':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeDetails.cs',
'req':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRequirements.cs',
'entry':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRequirementEntry.cs',
'projection':root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingRequirementProjectionService.cs',
'outcome':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingOutcome.cs',
'vm':root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingOutcomeViewModel.cs',
'outcome_service':root/'Scripts/Crafting/UI/RebirthCraftOutcomeService.cs',
}
for n,p in files.items(): check('exists '+p.relative_to(root).as_posix(),p.exists())
for n in ['windows','xui']:
    try: etree.parse(str(files[n])); check('xml parse '+files[n].name,True)
    except Exception as e: check('xml parse '+files[n].name,False,str(e))

wt=files['windows'].read_text(errors='ignore'); xt=files['xui'].read_text(errors='ignore')
rt=files['req'].read_text(errors='ignore'); et=files['entry'].read_text(errors='ignore')
pt=files['projection'].read_text(errors='ignore'); ot=files['outcome'].read_text(errors='ignore')
vt=files['vm'].read_text(errors='ignore'); dt=files['details'].read_text(errors='ignore')
loc=files['loc'].read_text(errors='ignore')

# Rebirth-only architecture remains intact.
check('rebirth-only conditional retained',"character_progression('Rebirth')" in xt)
check('custom crafting root retained','name="rebirthPersonalCraftingRoot"' in xt)
check('base game not globally replaced','<set xpath="/xui/window_group[@name=\'crafting\']"' not in xt)

# Requirements visual/accounting contract.
rstart=wt.find('<rect name="rebirthCraftingRequirementsRegion"')
rend=wt.find('<rect name="rebirthCraftingInventoryRegion"',rstart)
rwindow=wt[rstart:rend if rend>rstart else None] if rstart>=0 else ''
check('requirements owns custom controller','controller="RebirthCraftingRequirements, RebirthUtils"' in rwindow)
check('requirements count label','name="rebirthCraftingRequirementCount"' in rwindow)
check('exactly six visible card definitions',len(re.findall(r'name="rebirthCraftingRequirementCard[0-5]"',rwindow))==6)
check('all six cards custom controller',rwindow.count('controller="RebirthCraftingRequirementEntry, RebirthUtils"')==6)
check('cards expose real item icon','name="rebirthCraftingRequirementIcon"' in rwindow and 'atlas="ItemIconAtlas"' in rwindow)
check('cards expose localized item name','name="rebirthCraftingRequirementName"' in rwindow)
check('cards expose have value','name="rebirthCraftingRequirementHave"' in rwindow)
check('cards expose need value','name="rebirthCraftingRequirementNeed"' in rwindow)
check('cards expose source tooltip','tooltip="{rebirthsourcebreakdown}"' in rwindow)
check('unused cards start hidden','visible="false"' in rwindow)
check('no required-material placeholder cards','text_key="xuiRebirthRequiredMaterial"' not in rwindow)
check('requirements standard scrollbar','name="rebirthCraftingRequirementScrollHost"' in rwindow and '<defaultscrollbar/>' in rwindow)
check('requirements scroll view/proxy','name="rebirthCraftingRequirementScrollView"' in rwindow and 'name="rebirthCraftingRequirementScrollProxy"' in rwindow)

# Exact native craft-input projection.
check('projection mirrors ingredient modifier','PassiveEffects.CraftingIngredientCount' in pt and 'recipe.UseIngredientModifier' in pt)
check('projection passes selected crafting tier','craftingTier: Math.Max(1, craftingTier)' in pt)
check('projection multiplies effective need by batch','Math.Max(0, perBatch) * batches' in pt)
check('projection uses authoritative player GetItemCount','xui.PlayerInventory.GetItemCount(itemValue)' in pt)
check('projection uses real all-stack projection','xui.PlayerInventory.GetAllItemStacks()' in pt)
check('quality ingredient eligibility mirrors mod exclusion','HasModSlots && stack.itemValue.HasMods()' in pt)
check('quality ingredient zero count normalizes to one','qualityIngredient && perBatch == 0' in pt)
check('requirements semantic enough state','HasEnough = have >= need' in pt and 'value.HasEnough' in et)
check('requirements refresh on batch change','craftCount.OnCountChanged += CraftCount_OnCountChanged' in rt)
check('requirements refresh resource/progression projection','Time.realtimeSinceStartup + 0.20f' in rt and 'BuildFingerprint(next)' in rt)
check('requirements list scrolls by 2-column rows','Columns = 2' in rt and 'VisibleRows = 3' in rt and 'firstVisibleRow * Columns' in rt)
check('more-than-six requirements remain reachable','MaxFirstRow()' in rt and 'requirements.Count <= VisibleCards' in rt and 'else' in rt)
check('stock draggable scrollbar adapter used','RebirthNativeScrollbarUtil.TryGetValue' in rt and 'RebirthNativeScrollbarUtil.TrySetValue' in rt)

# Remote Resources provenance integration.
check('source projection includes backpack','PlayerInventory.Backpack.GetItemCount' in pt)
check('source projection includes toolbelt','PlayerInventory.Toolbelt.GetItemCount' in pt)
check('source projection uses authoritative remote snapshot','RemoteResourceSnapshotCache.Get(player).Sources' in pt)
check('source projection uses client replicated provenance','RemoteResourceClientAvailability.GetSources(player)' in pt)
check('source projection includes distance','Vector3.Distance(player.position, source.Position)' in pt)
check('entry binding returns source tooltip','bindingName == "rebirthsourcebreakdown"' in et)

# Expected Outcome is source-backed and contains no fake probability block.
ostart=wt.find('<rect name="rebirthCraftingOutcomeRegion"')
oend=wt.find('</rect>\n        </rect>\n\n        <rect name="rebirthCraftingCenterZone"',ostart)
owindow=wt[ostart:oend if oend>ostart else rstart] if ostart>=0 else ''
check('outcome owns custom controller','controller="RebirthCraftingOutcome, RebirthUtils"' in owindow)
check('outcome two-part composition','name="rebirthCraftingOutcomeMetricsPanel"' in owindow and 'name="rebirthCraftingOutcomeResultsPanel"' in owindow)
for node in ['rebirthCraftingOutcomeStatusValue','rebirthCraftingOutcomeQualityValue','rebirthCraftingOutcomeXpValue','rebirthCraftingOutcomeResultsValue','rebirthCraftingOutcomeExplanation']:
    check('outcome node '+node,f'name="{node}"' in owindow)
check('no success chance row in custom outcome','xuiRebirthSuccessChance' not in owindow and 'SuccessChance' not in owindow)
check('no quality distribution rows in custom outcome','xuiRebirthHighQuality' not in owindow and 'xuiRebirthNormalQuality' not in owindow and 'xuiRebirthLowQuality' not in owindow)
check('no N/A filler in custom outcome','xuiRebirthNotApplicable' not in owindow and '>N/A<' not in owindow)
check('view model uses source-backed outcome service','RebirthCraftOutcomeService.Build' in vt)
check('view model uses real output count','XUiM_Recipes.GetRecipeCraftOutputCount' in vt)
check('view model checks native unlock','XUiM_Recipes.GetRecipeIsUnlocked' in vt)
check('view model checks real structural craft requirements','owner.CraftingRequirementsValid(recipe)' in vt)
check('view model checks material projection','requirements[i].HasEnough' in vt)
check('view model checks actual Craft action enabled state','details.CommandBridge.CanCraft' in vt)
check('view model deterministic explanation only','xuiRebirthOutcomeDeterministicNote' in vt)
check('view model defines no probability fields','SuccessChance' not in vt and 'HighQuality' not in vt and 'NormalQuality' not in vt and 'LowQuality' not in vt)
check('outcome refreshes with batch/resource state','craftCount.OnCountChanged += CraftCount_OnCountChanged' in ot and 'requirements?.RefreshNow()' in ot)

# Permanent geometry/no overlap and user backpack clarification.
check('details explicitly refreshes dependent regions','GetChildByType<XUiC_RebirthCraftingRequirements>()?.RefreshNow()' in dt and 'GetChildByType<XUiC_RebirthCraftingOutcome>()?.RefreshNow()' in dt)
combined=rt+et+pt+ot+vt+dt+rwindow+owindow
check('chunk F does not impose 32-slot backpack capacity','BackpackSize = 32' not in combined and 'capacity = 32' not in combined and 'Take(32)' not in combined)
check('chunk F does not create 4x8 inventory capacity','rows="4" cols="8"' not in rwindow+owindow)

# Localization coverage for new visible strings.
for key in ['xuiRebirthCraftStatus','xuiRebirthReadyToCraft','xuiRebirthMissingMaterials','xuiRebirthHave','xuiRebirthNeed','xuiRebirthSources','xuiRebirthSelectRecipeOutcomePrompt']:
    check('localization '+key,loc.count(key+',')==1,str(loc.count(key+',')))

# Basic source smoke test.
for key in ['details','req','entry','projection','outcome','vm']:
    text=files[key].read_text(errors='ignore')
    check('brace balance '+files[key].name,text.count('{')==text.count('}'),f"{text.count('{')}/{text.count('}')}")
    check('paren balance '+files[key].name,text.count('(')==text.count(')'),f"{text.count('(')}/{text.count(')')}")

passed=sum(1 for _,ok,_ in checks if ok); failed=[x for x in checks if not x[1]]
print(f'PC080 CHUNK F VALIDATION: {passed} PASS / {len(failed)} FAIL')
for name,ok,detail in failed: print('FAIL -',name,detail)
if failed: sys.exit(1)
