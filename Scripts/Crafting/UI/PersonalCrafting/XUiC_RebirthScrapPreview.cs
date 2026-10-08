using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthScrapPreview : XUiController
{
    private int longestName;
    private ItemActionEntryScrap action;
    private XUiC_RebirthCraftingItemContext context;
    public override void Init() { base.Init(); context = GetParentByType<XUiC_RebirthCraftingItemContext>(); Hide(); }
    public override void OnClose() { Hide(); base.OnClose(); }
    public void Hide() { action = null; ViewComponent.IsVisible = false; }
    public void Show(ItemActionEntryScrap value) { action = value; Render(); }
    public override void Update(float dt)
    {
        base.Update(dt);
        if (action == null) return;
        if (context == null || !context.IsContextVisible || context.SelectedSlot != action.ItemController) { Hide(); return; }
        Render();
    }
    private void Render()
    {
        var slot = action?.ItemController as XUiC_ItemStack;
        var stack = slot?.ItemStack;
        if (stack == null || stack.IsEmpty()) { Hide(); return; }
        var recipe = CraftingManager.GetScrapableRecipe(stack.itemValue, stack.count);
        if (recipe == null) { Hide(); return; }
        var source = stack.itemValue.ItemClass;
        var output = ItemClass.GetForId(recipe.itemValueType);
        float recovery = XUiM_Recipes.DisableSmelter && source.HasAnyTags(FastTags<TagGroup.Global>.Parse("scrap100")) ? 1f : .75f;
        int count = RebirthScrapPreviewMath.Output(source.GetWeight(), stack.count, output.GetWeight(), recovery, XUiM_Recipes.ScrappingOutputModifier);
        if (count == 0) { Hide(); return; }
        longestName = 0;
        SetRow(0, output, count);
        SetRow(1, null, 0);
        if (stack.itemValue.Meta > 0)
            foreach (var itemAction in source.Actions)
                if (itemAction is ItemActionRanged ranged && !(itemAction is ItemActionTextureBlock)
                    && ranged.MagazineItemNames != null && stack.itemValue.SelectedAmmoTypeIndex < ranged.MagazineItemNames.Length)
                {
                    var ammo = ItemClass.GetItem(ranged.MagazineItemNames[stack.itemValue.SelectedAmmoTypeIndex]);
                    SetRow(1, ammo.ItemClass, stack.itemValue.Meta);
                    break;
                }
        (GetChildById("previewStatus")?.ViewComponent as XUiV_Label)?.SetTextImmediately(action.Enabled ? "" : "Remove attachments before scrapping.");
        int rows = GetChildById("result1").ViewComponent.IsVisible ? 2 : 1;
        int width = Mathf.Clamp(120 + longestName * 10, 260, 380);
        int height = 40 + rows * 48 + (action.Enabled ? 6 : 48);
        ViewComponent.Size = new Vector2i(width, height);
        // A flat texture has no transparent atlas padding at the popup edges.
        var background = GetChildById("previewBg").ViewComponent as XUiV_Texture;
        background.AutoUnload = false;
        if (background.Texture != Texture2D.whiteTexture) background.Texture = Texture2D.whiteTexture;
        background.Color = Color.black;
        background.Position = new Vector2i(0, 0);
        background.Size = new Vector2i(width, height);
        foreach (var child in Children)
        {
            if (child.ViewComponent is XUiV_Sprite) child.ViewComponent.Size = new Vector2i(width, height);
        }
        for (int i = 0; i < rows; i++)
        {
            GetChildById("result" + i).ViewComponent.Position = new Vector2i(12, -40 - i * 48);
            GetChildById("resultName" + i).ViewComponent.Size = new Vector2i(width - 132, 40);
            GetChildById("resultCount" + i).ViewComponent.Position = new Vector2i(width - 76, -22);
        }
        var status = GetChildById("previewStatus").ViewComponent;
        status.Position = new Vector2i(12, -40 - rows * 48);
        status.Size = new Vector2i(width - 24, 44);
        ViewComponent.IsVisible = true;
    }
    private void SetRow(int i, ItemClass item, int count)
    {
        GetChildById("result" + i).ViewComponent.IsVisible = item != null && count > 0;
        if (item == null) return;
        longestName = System.Math.Max(longestName, item.GetLocalizedItemName().Length);
        var icon = GetChildById("resultIcon" + i).ViewComponent as XUiV_Sprite;
        icon.SpriteName = item.GetIconName();
        (GetChildById("resultName" + i).ViewComponent as XUiV_Label).SetTextImmediately(item.GetLocalizedItemName());
        (GetChildById("resultCount" + i).ViewComponent as XUiV_Label).SetTextImmediately(count.ToString());
    }
}
