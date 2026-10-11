from pathlib import Path
import xml.etree.ElementTree as E
import shutil
base=Path('.')
backup=base/'_Documentation/UiFeedback_20261009b/before'
def save_before(p):
 d=backup/p; d.parent.mkdir(parents=True,exist_ok=True)
 if not d.exists(): shutil.copy2(p,d)
def edit(p,f):
 p=Path(p);save_before(p);s=p.read_text(encoding='utf-8-sig');p.write_text(f(s),encoding='utf-8')
p='Tools/UIAudit/Test-StashBatchPrice.ps1'
edit(p,lambda s:s.replace('Economic=76,Selling=149','EconomicValue=76,BarteringSelling=149').replace('PassiveEffects.Selling','PassiveEffects.BarteringSelling').replace('public static class GamePrefs{public static float Income=1;public static float GetFloat(EnumGamePrefs e)=>Income;}','public static class GamePrefs{public static float GetFloat(EnumGamePrefs e)=>0;}\nnamespace SandboxOptions { public enum SandboxOptions { TraderSellPrices=130 } public static class SandboxOptionManager { public static float Income=1; public static float GetFloat(SandboxOptions o)=>Income; } }').replace('GamePrefs.Income','SandboxOptions.SandboxOptionManager.Income'))
for p in ['Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventory.cs','Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs']:
 edit(p,lambda s:s.replace('windowGroup?.Controller is XUiC_RebirthCookingStation || windowGroup?.Controller is XUiC_RebirthStationWorkspace ? 22','windowGroup?.Controller is XUiC_RebirthCookingStation cooking && !cooking.IsMilling ? 22'))
p=Path('Config/XUi_InGame/station_templates.xml');save_before(p);tree=E.parse(p);root=tree.getroot();core=root.find('.//rect[@name="stationCore"]')
def n(name,parent=core):
 r=parent.find('.//*[@name="'+name+'"]');assert r is not None,name;return r
def a(name,parent=core,**kw):
 el=n(name,parent);el.attrib.update({k:str(v) for k,v in kw.items()});return el
core.remove(n('stationIngredientShell'))
a('stationResultShell',pos='398,0',width=958,height=490)
a('stationResultShellBackground',width=958,height=490);a('stationResultShellFrame',width=958,height=490)
a('craftingInfoPanel',pos='398,0',width=958,height=490)
a('contentCraftingInfo',width=958,height=444)
a('rebirthCraftInfoBackground',width=958,height=444)
a('recipeSummary',width=942,height=250)
a('summaryDescription',pos='146,-8',width=430,height=175)
a('stationRecipeDescription',width=430,height=145)
r=n('stationRecipeDescription');r.find('defaultscrollbar').set('pos','412,0');r.find('defaultscrollbar').set('barheight','145');r.find('scrollview').attrib.update(width='404',height='145')
a('summaryDescriptionLabel',width=398,height=145)
a('stationOutputStats',pos='590,-8',width=342,height=145)
r=n('stationOutputStats');r.find('defaultscrollbar').attrib.update(pos='326,0',barheight='145');r.find('scrollview').attrib.update(width='318',height='145');a('stationOutputStatsText',width=312,height=145)
a('stationKnowledge',pos='590,-156',width=342,height=80)
# Keep knowledge details reachable without covering the ingredient rows.
n('stationKnowledge').find('label[@text="{rebirthknowledge}"]').attrib.update(pos='0,-32',width='338',height='48',font_size='16')
a('btnRebirthCraftKnowledge',pos='202,0',width=136)
a('recipeMetadata',pos='146,-176',width=420,height=65)
r=n('recipeMetadata');r.find('label[@text_key="xuiRebirthBatchSize"]').set('pos','266,0');a('recipeCraftCountControl',pos='266,-26')
a('actionBand',pos='8,-254',width=942,height=40)
n('actionBand').find('sprite').set('width','942');a('itemActions',n('actionBand'),width=942,cell_width=314)
a('ingredients',pos='8,-300',width=942,height=140)
r=n('ingredients');r.find('sprite').attrib.update(width='942',height='140');r.find('label').attrib.update(text_key='xuiRebirthRequirements',width='918');r.find('grid').attrib.update(rows='3',cols='3',pos='0,-38',width='942',height='102',cell_width='314',cell_height='34');r.findall('sprite')[1].set('width','942')
a('rebirthRecipeProgressionLock',pos='8,-444',width=942,height=30)
a('rebirthCraftInfoFrame',width=958,height=490)
a('stationItemInfo',pos='398,0')
a('stationEmpty',pos='398,0',width=958,height=490)
for el in n('stationEmpty'):
 if el.tag=='sprite':el.set('width','958');el.set('height','490' if el.get('height')=='580' else el.get('height','2'))
a('rebirthCraftingInventoryRegion',pos='398,-500',width=958,height=313)
a('rebirthCraftingInventoryRegionBg',width=958,height=313);a('rebirthCraftingInventoryHeaderRule',width=958)
a('rebirthCraftingInventoryScroll',width=934,height=260)
a('stationQueueShell',height=375);a('stationQueueShellBackground',height=375);a('stationQueueShellFrame',height=375);a('rebirthCraftingQueueRegion',height=375)
# Compact named requirement rows preserve IngredientEntry and its native bindings.
r=root.find('.//rebirth_station_ingredient_slot/rect');r.attrib.update(width='308',height='32')
for sp in r.findall('sprite')[:2]:sp.attrib.update(width='308',height='32')
a('icon',r,width=28,height=28,pos='17,-16')
a('needcount',r,pos='236,-5',width=68,height=24,font_size=17)
E.SubElement(r,'label',dict(name='ingredientName',pos='38,-5',width='194',height='24',font_size='17',color='235,235,240,255',text='{itemname}',depth='5',overflow='shrinkcontent'))
# Expand actions to share the full center width.
r=root.find('.//rebirth_station_action_entry/rect');r.set('width','308')
for sp in r.findall('sprite'):
 if sp.get('width')=='180':sp.set('width','308')
a('Name',r,width=226);a('keyboardButton',r,pos='300,-17');a('gamepadIcon',r,pos='274,-22')
E.indent(tree,space='  ');tree.write(p,encoding='utf-8',xml_declaration=True)
print('Updated sale regression and shared non-cooking station template')
