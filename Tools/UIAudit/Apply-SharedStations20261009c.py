from pathlib import Path
import shutil,xml.etree.ElementTree as E,copy
before=Path('_Documentation/UiFeedback_20261009c/before')
def save(p,s):
 p=Path(p);b=before/p;b.parent.mkdir(parents=True,exist_ok=True)
 if p.exists() and not b.exists():shutil.copy2(p,b)
 p.write_text(s,encoding='utf-8-sig')
base=Path('Scripts/Crafting/UI/PersonalCrafting')
for n in ['XUiC_RebirthCraftingInventorySlot','XUiC_RebirthCraftingInventoryBridge']:
 p=base/(n+'.cs');s=p.read_text(encoding='utf-8-sig').replace('GetParentByType<XUiC_RebirthPersonalCrafting>()','RebirthCraftingPresentation.Resolve(this)').replace('XUiC_RebirthPersonalCrafting owner','RebirthCraftingPresentation owner').replace('XUiC_RebirthPersonalCrafting personalOwner','RebirthCraftingPresentation personalOwner');save(p,s)
p=base/'XUiC_RebirthCraftingRecipeCatalogue.cs';s=p.read_text(encoding='utf-8-sig').replace('workStation = string.Empty;','workStation = owner?.Workstation ?? string.Empty;').replace('craftingArea = new[] { string.Empty };','craftingArea = new[] { workStation };');save(p,s)
p=base/'RebirthCraftingRecipeCatalogueService.cs';s=p.read_text(encoding='utf-8-sig').replace('manager.GetCraftingCategoryDisplayList(string.Empty)','manager.GetCraftingCategoryDisplayList(owner?.Workstation ?? string.Empty)').replace('XUiM_Recipes.FilterRecipesByWorkstation(string.Empty, allRecipes)','XUiM_Recipes.FilterRecipesByWorkstation(owner?.Workstation ?? string.Empty, allRecipes)');s=s.replace('        catalogue.recipes.Clear();','        if (!string.IsNullOrEmpty(owner?.Workstation) && filtered != null)\n            filtered = XUiM_Recipes.FilterRecipesByWorkstation(owner.Workstation, filtered.AsReadOnly());\n\n        catalogue.recipes.Clear();');save(p,s)
# Reuse the actual personal controls and IDs, retaining four native station queue slots.
r=E.parse('Config/XUi_InGame/windows.xml').getroot();personal=copy.deepcopy(next(x for x in r.iter('window') if x.get('name')=='rebirthPersonalCraftingRoot'));personal.tag='rect';personal.set('pos','0,0')
old=E.parse('Config/XUi_InGame/station_templates.xml').getroot()
output=copy.deepcopy(next(x for x in old.iter() if x.get('name')=='windowOutput'))
right=next(x for x in personal.iter() if x.get('name')=='rebirthCraftingRightZone');right.insert(2,output)
rows=next(x for x in personal.iter() if x.get('name')=='rebirthCraftingQueueRowsHost')
for x in list(rows)[4:]:rows.remove(x)
# Variants keep their existing tool/fuel/smelting controls under the right column.
workspace=E.parse('Config/XUi_InGame/station_workspace.xml').getroot()
for w in workspace.iter('window'):
 extras=[copy.deepcopy(x) for x in w if x.tag!='rebirth_station_core']
 for x in list(w):w.remove(x)
 shared=copy.deepcopy(personal);rz=next(x for x in shared.iter() if x.get('name')=='rebirthCraftingRightZone')
 for x in extras:rz.append(x)
 w.set('controller','RebirthStationWorkspaceLayout, RebirthUtils');w.set('pos','0,0');w.append(shared)
E.indent(workspace,space='  ');save('Config/XUi_InGame/station_workspace.xml',E.tostring(workspace,encoding='unicode'))
