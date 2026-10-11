from pathlib import Path
import shutil,xml.etree.ElementTree as E,copy
before=Path('_Documentation/UiFeedback_20261009c/before')
def edit(name,fn):
 p=Path(name);b=before/p;b.parent.mkdir(parents=True,exist_ok=True)
 if not b.exists():shutil.copy2(p,b)
 s=p.read_text(encoding='utf-8-sig');p.write_text(fn(s),encoding='utf-8-sig')
edit('Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingPresentation.cs',lambda s:s.replace('return For(station);','if(station!=null)return For(station);\n        var cooking=child?.GetParentByType<XUiC_RebirthCookingStation>();\n        return cooking?.UsesSharedMillingPresentation==true?For(cooking):null;'))
edit('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs',lambda s:s.replace('        service.RefreshCategories();','        workStation = owner?.Workstation ?? string.Empty;\n        craftingArea = new[] { workStation };\n        service.RefreshCategories();'))
edit('Scripts/Crafting/Cooking/XUiC_RebirthCookingStation.cs',lambda s:s.replace('    private float cookingSync;','    public bool UsesSharedMillingPresentation => GetChildById("rebirthMillingProcessor") != null;\n    private RebirthCraftingPresentation sharedPresentation;\n    private float cookingSync;').replace('        // The cooking screen deliberately','        if(UsesSharedMillingPresentation) sharedPresentation=RebirthCraftingPresentation.For(this);\n        // The cooking screen deliberately').replace('        burnTimeLeft = GetChildById("burnTimeLeft")?.ViewComponent as XUiV_Label;','''        burnTimeLeft = GetChildById("burnTimeLeft")?.ViewComponent as XUiV_Label;
        if(sharedPresentation!=null)
        {
            recipeList=GetChildByType<XUiC_RebirthCraftingRecipeCatalogue>();
            craftCountControl=GetChildByType<XUiC_RecipeCraftCount>();
            (recipeList as XUiC_RebirthCraftingRecipeCatalogue)?.AttachSearchInput(GetChildById("rebirthCraftingRecipeSearch") as XUiC_TextInput,
                GetChildById("rebirthCraftingRecipeSearchPlaceholder")?.ViewComponent as XUiV_Label);
        }''').replace('        displayedBurnSeconds = -1;','        sharedPresentation?.AdvanceCraftIntentEpoch();sharedPresentation?.Coordinator.Open();\n        displayedBurnSeconds = -1;').replace('        // Cancel before base teardown','        sharedPresentation?.AdvanceCraftIntentEpoch();sharedPresentation?.Coordinator.Close();\n        // Cancel before base teardown'))
edit('Scripts/Crafting/Cooking/XUiC_RebirthCookingWorkspace.cs',lambda s:s.replace('        Wire("pullIngredients", Pull);','        if(station.UsesSharedMillingPresentation)return;\n        Wire("pullIngredients", Pull);').replace('        open = false;','        sharedCraftPending=false;\n        open = false;').replace('        var actions=xui.playerUI.playerInput?.GUIActions;','''        if(station.UsesSharedMillingPresentation)
        {
            if(sharedCraftPending&&!pulling)
            {
                sharedCraftPending=false;
                if(string.IsNullOrEmpty(status))Cook();
                else ReturnIngredients();
            }
            if(!string.IsNullOrEmpty(status)&&status!=sharedLastStatus)
                GameManager.ShowTooltip(xui.playerUI.entityPlayer,status);
            sharedLastStatus=status;
            if((refresh-=dt)<=0){refresh=.75f;Render();}
            return;
        }
        var actions=xui.playerUI.playerInput?.GUIActions;''').replace('    private void Layout()\n    {','    private void Layout()\n    {\n        if(station.UsesSharedMillingPresentation)return;').replace('        try { RenderContents(); }','''        try {
            if(station.UsesSharedMillingPresentation){result=Resolve();book=null;magazine=null;}
            else RenderContents();
        }'''))
edit('Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingCommandBridge.cs',lambda s:s.replace('        BaseItemActionEntry action = BuildCraft(recipe, craftingTier, false);','''        var milling=owner?.Controller as XUiC_RebirthCookingStation;
        if(milling?.UsesSharedMillingPresentation==true)
            return milling.GetChildByType<XUiC_RebirthCookingWorkspace>().CanCraftShared(recipe,craftCount.Count);
        BaseItemActionEntry action = BuildCraft(recipe, craftingTier, false);''').replace('        BaseItemActionEntry action = BuildCraft(recipe, craftingTier, true);','''        var milling=owner?.Controller as XUiC_RebirthCookingStation;
        if(milling?.UsesSharedMillingPresentation==true)
        {
            milling.GetChildByType<XUiC_RebirthCookingWorkspace>().CraftShared(recipe,craftCount.Count);
            return;
        }
        BaseItemActionEntry action = BuildCraft(recipe, craftingTier, true);'''))
# Dedicated mortar surface uses the exact same shared templates. Its processor keeps the existing twelve transient ingredient slots only.
p=Path('Config/XUi_InGame/station_workspace.xml');w=E.parse(p).getroot();basic=w.find('append/window');m=copy.deepcopy(basic);m.set('name','rebirthStationRootMilling')
processor=E.SubElement(m,'rect',{'name':'rebirthMillingProcessor','controller':'RebirthCookingWorkspace, RebirthUtils','pos':'-10000,-10000','width':'1','height':'1','always_update':'true'})
for i in range(12):E.SubElement(processor,'item_stack',{'name':'cookingSlot'+str(i),'controller':'RebirthCookingSlot, RebirthUtils','pos':'0,0','width':'75','height':'75'})
w.find('append').append(m);E.indent(w,space='  ');p.write_text(E.tostring(w,encoding='unicode'),encoding='utf-8')
edit('Config/_Cooking/workspace_xui.xml',lambda s:s.replace('''<append xpath="/xui/window_group[@name='workstation_WorkbenchMortarPestle001_FR']">
    <window name="rebirthCookingRoot" anchor="Center" />''','''<append xpath="/xui/window_group[@name='workstation_WorkbenchMortarPestle001_FR']">
    <window name="rebirthStationRootMilling" anchor="Center" />'''))
