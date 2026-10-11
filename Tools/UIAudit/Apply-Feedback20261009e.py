from pathlib import Path
import shutil
root=Path('.')
d=Path('_Documentation/UiFeedback_20261009e/before')
files=['Config/XUi_InGame/windows.xml','Config/XUi_InGame/station_templates.xml','Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs','Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeDetails.cs','Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs','Scripts/UI/XUiC_RebirthContainerWorkspace.cs']
for f in files:
 p=d/f;p.parent.mkdir(parents=True,exist_ok=True)
 if not p.exists():shutil.copy2(f,p)
p=Path(files[2]);s=p.read_text(encoding='utf-8-sig');s=s.replace('bool externalDatasetChanged = resortRecipes && pageChanged;', '''// A native station refresh may supply an empty or global recipe list. Always
        // rebuild through this station's catalogue; retain native cross-links only in backpack crafting.
        bool stationDatasetChanged = !string.IsNullOrEmpty(owner?.Workstation) && resortRecipes && pageChanged;
        if (stationDatasetChanged) { filterDirty = true; category = string.Empty; }
        bool externalDatasetChanged = !stationDatasetChanged && resortRecipes && pageChanged;''');p.write_text(s,encoding='utf-8')
p=Path(files[0]);s=p.read_text(encoding='utf-8-sig');s=s.replace('name="width">860</setattribute>','name="width">590</setattribute>');p.write_text(s,encoding='utf-8')
p=Path(files[3]);s=p.read_text(encoding='utf-8-sig');s=s.replace('ItemValue value = new ItemValue(selectedRecipe.itemValueType);','ItemValue value = new ItemValue(selectedRecipe.itemValueType, selectedCraftingTier, selectedCraftingTier);\n        RebirthBackpackSectionStats.Render(this, new ItemStack(value, selectedRecipe.count), "recipeResult");');s=s.replace('if (descriptionLabel != null) descriptionLabel.Text = string.Empty;','if (descriptionLabel != null) descriptionLabel.Text = string.Empty;\n        RebirthBackpackSectionStats.Render(this, ItemStack.Empty, "recipeResult");');s=s.replace('int metaX = width - metaWidth - 14;', '''int metaX = width - metaWidth - 14;
        for (int i=0;i<7;i++)
        {
            SetRect(GetChildById("recipeResultStatName"+i), metaX, -(contentTop+i*20), metaWidth*3/5, 20);
            SetRect(GetChildById("recipeResultStatValue"+i), metaX+metaWidth*3/5, -(contentTop+i*20), metaWidth*2/5, 20);
        }''');p.write_text(s,encoding='utf-8')
# Add direct-child result stats to both shared station and backpack recipe details.
import re
for f in files[:2]:
 p=Path(f);s=p.read_text(encoding='utf-8-sig')
 pattern=r'(<label\b[^>]*name="rebirthCraftingSelectedRecipeKnowledge"[^>]*/>)'
 labels='\n'.join(f'<label name="recipeResultStat{kind}{i}" depth="65" width="160" height="20" font_size="16" color="{color}" justify="{justify}" overflow="clampcontent" />' for i in range(7) for kind,color,justify in [('Name','235,235,240,255','left'),('Value','210,200,100,255','right')])
 s,n=re.subn(pattern,lambda m:m[0]+'\n'+labels,s)
 assert n==1,(f,n)
 p.write_text(s,encoding='utf-8')
