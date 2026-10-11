from pathlib import Path
p=Path('Scripts/Crafting/Cooking/XUiC_RebirthCookingWorkspace.MillingLayout.cs');s=p.read_text();s=s.replace('        Text("resultTitle",','''        MillingRect("referenceTitle",980,-48,472,26);
        MillingRect("bookReferenceIcon",980,-80,52,52);
        MillingRect("referenceStatus",1040,-80,168,80);
        MillingRect("magazineReferenceIcon",1214,-80,52,52);
        MillingRect("magazineReferenceText",1274,-80,170,80);
        MillingRect("prepStatus",980,-156,472,26);
        MillingRect("prepare",980,-185,472,34);
        var preparation = Find("prepare");
        foreach (var id in new[] { "actionFill", "actionFrame" })
            if (preparation?.GetChildById(id)?.ViewComponent is XUiView fill) fill.Size = new Vector2i(472,34);
        if (preparation?.GetChildById("label")?.ViewComponent is XUiView caption)
        { caption.Position = new Vector2i(236,-17); caption.Size = new Vector2i(410,24); }
        MillingRect("prepTrack",980,-221,472,6);
        MillingRect("prepProgress",980,-221,472,6);
        MillingRect("referenceBenefits",980,-144,472,18);
        Text("resultTitle",''')
p.write_text(s)
p=Path('Scripts/Crafting/Cooking/XUiC_RebirthCookingWorkspace.cs');s=p.read_text().replace('        Show("herbsLabel", !milling);','        Show("herbsLabel", !milling);\n        Show("ingredientGuide", !milling);').replace('Show("nonFoodStats", result != null && !foodResult && !hasReferences);','Show("nonFoodStats", result != null && !foodResult && (!hasReferences || milling));');p.write_text(s)
