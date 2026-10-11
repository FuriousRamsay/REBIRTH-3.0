from pathlib import Path
import shutil
p=Path('Scripts/Crafting/Cooking/XUiC_RebirthCookingWorkspace.cs');d=Path('_Documentation/UiFeedback_20261009b/before')/p;d.parent.mkdir(parents=True,exist_ok=True)
if not d.exists():shutil.copy2(p,d)
s=p.read_text(encoding='utf-8-sig').replace('public sealed class XUiC_RebirthCookingWorkspace','public sealed partial class XUiC_RebirthCookingWorkspace')
s=s.replace('        Scale("cookingTools",1);','        Scale("cookingTools",1);')
# Apply geometry at the end of the common layout, after its normal scaling.
needle='    private void Layout()'
start=s.index(needle);end=s.index('\n    }',start)
s=s[:end]+'\n        if (station.IsMilling) LayoutMilling();'+s[end:]
s=s.replace('        bool milling=station.IsMilling;','        bool milling=station.IsMilling;\n        Show("herbsLabel", !milling);\n        for (int herb = 9; herb < 12; herb++) Show("ingredient" + herb, !milling);')
old='Text("dishSkill"+i,r!=null&&RebirthServiceCraftSkillService.ClassifyRecipe(r)=="skill.drink_preparation"?RebirthSurvivorUiText.L("xuiRebirthCookingDrinksCategory", "DRINKS"):RebirthSurvivorUiText.L("xuiRebirthCookingCookingCategory", "COOKING"));'
assert old in s
s=s.replace(old,'Text("dishSkill"+i, r == null ? "" : milling ? RebirthSkillDisplayNames.Get(RebirthServiceCraftSkillService.ClassifyRecipe(r)) : RebirthServiceCraftSkillService.ClassifyRecipe(r)=="skill.drink_preparation" ? RebirthSurvivorUiText.L("xuiRebirthCookingDrinksCategory", "DRINKS") : RebirthSurvivorUiText.L("xuiRebirthCookingCookingCategory", "COOKING"));')
s=s.replace('bool foodResult = result != null &&','bool foodResult = !milling && result != null &&')
s=s.replace('Localization.Get("xuiCraftingTime")','Localization.Get("xuiRebirthCraftTime")')
s=s.replace('            var value=!loaded.IsEmpty()?loaded.itemValue:g?.itemValue;','            var value=!loaded.IsEmpty()?loaded.itemValue:g?.itemValue;\n            Text("millingIngredientName"+i, value?.ItemClass == null ? "" : Localization.Get(value.ItemClass.GetItemName()));\n            Show("millingIngredientName"+i, milling && value != null);')
p.write_text(s,encoding='utf-8')
# Add hidden names to existing ingredient controls; the milling layout exposes these.
import xml.etree.ElementTree as E
p=Path('Config/_Cooking/workspace_windows.xml');d=Path('_Documentation/UiFeedback_20261009b/before')/p;d.parent.mkdir(parents=True,exist_ok=True)
if not d.exists():shutil.copy2(p,d)
t=E.parse(p)
for root in t.getroot().findall('.//window'):
 for i in range(9):
  row=root.find('.//*[@name="ingredient'+str(i)+'"]')
  if row is not None:E.SubElement(row,'label',dict(name='millingIngredientName'+str(i),pos='48,-4',width='190',height='32',font_size='18',depth='16',color='235,235,240,255',visible='false',overflow='shrinkcontent'))
E.indent(t,space='  ');t.write(p,encoding='utf-8',xml_declaration=True)
