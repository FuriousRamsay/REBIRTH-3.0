from pathlib import Path
p=Path('Scripts/Crafting/Cooking/XUiC_RebirthCookingWorkspace.MillingLayout.cs');s=p.read_text()
s=s.replace('child.ID == "actionFill"','child == action.GetChildById("actionFill")').replace('child.ID == "actionFrame"','child == action.GetChildById("actionFrame")').replace('child.ID == "label"','child == action.GetChildById("label")')
s=s.replace('MillingRect("resultDescriptionReader",146,-92,426,130);','MillingRect("resultDescriptionReader",146,-92,342,110);').replace('MillingRect("resultDescription",0,0,398,130);','MillingRect("resultDescription",0,0,312,110);').replace('new Vector2i(404,130)','new Vector2i(318,110)')
s=s.replace('"batchLabel",148,73','"batchLabel",148,90').replace('"batchMin",270,60','"batchMin",270,74').replace('"batchMinus",305,60','"batchMinus",305,74').replace('"batchInput",327,76,68,32','"batchInput",327,90,68,26').replace('"batchPlus",418,60','"batchPlus",418,74').replace('"batchMax",455,60','"batchMax",455,74')
s=s.replace('        Find("rebirthCraftingInventoryScroll")?.GetChildByType<XUiC_RebirthCraftingInventoryScroll>();','        (Find("rebirthCraftingInventoryScroll") as XUiC_RebirthCraftingInventoryScroll)?.ApplyLayoutGeometry();\n        Text("rebirthCraftingTabCraftingLabel", Localization.Get("xuiCrafting"));')
p.write_text(s)
