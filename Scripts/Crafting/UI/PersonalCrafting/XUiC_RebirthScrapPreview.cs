using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthScrapPreview : XUiController
{
    private int longestName;
    private ItemActionEntryScrap action;
    private XUiC_RebirthCraftingItemContext context;
    private readonly XUiController[] resultRows = new XUiController[2];
    private readonly XUiV_Sprite[] resultIcons = new XUiV_Sprite[2];
    private readonly XUiV_Label[] resultNames = new XUiV_Label[2];
    private readonly XUiV_Label[] resultCounts = new XUiV_Label[2];
    private XUiV_Label previewStatus;
    private XUiV_Texture previewBackground;
    public override void Init()
    {
        base.Init();
        context = GetParentByType<XUiC_RebirthCraftingItemContext>();
        previewStatus = GetChildById("previewStatus")?.ViewComponent as XUiV_Label;
        previewBackground = GetChildById("previewBg").ViewComponent as XUiV_Texture;
        for (int i = 0; i < resultRows.Length; i++)
        {
            resultRows[i] = GetChildById("result" + i);
            resultIcons[i] = GetChildById("resultIcon" + i).ViewComponent as XUiV_Sprite;
            resultNames[i] = GetChildById("resultName" + i).ViewComponent as XUiV_Label;
            resultCounts[i] = GetChildById("resultCount" + i).ViewComponent as XUiV_Label;
        }
        Hide();
    }
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
        previewStatus?.SetTextImmediately(action.Enabled ? "" : "Remove attachments before scrapping.");
        int rows = resultRows[1].ViewComponent.IsVisible ? 2 : 1;
        int width = Mathf.Clamp(120 + longestName * 10, 260, 380);
        int height = 40 + rows * 48 + (action.Enabled ? 6 : 48);
        ViewComponent.Size = new Vector2i(width, height);
        // A flat texture has no transparent atlas padding at the popup edges.
        var background = previewBackground;
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
            resultRows[i].ViewComponent.Position = new Vector2i(12, -40 - i * 48);
            resultNames[i].Size = new Vector2i(width - 132, 40);
            resultCounts[i].Position = new Vector2i(width - 76, -22);
        }
        var status = previewStatus;
        status.Position = new Vector2i(12, -40 - rows * 48);
        status.Size = new Vector2i(width - 24, 44);
        ViewComponent.IsVisible = true;
    }
    private void SetRow(int i, ItemClass item, int count)
    {
        resultRows[i].ViewComponent.IsVisible = item != null && count > 0;
        if (item == null) return;
        string itemName = item.GetLocalizedItemName();
        longestName = System.Math.Max(longestName, itemName.Length);
        var icon = resultIcons[i];
        icon.SpriteName = item.GetIconName();
        resultNames[i].SetTextImmediately(itemName);
        resultCounts[i].SetTextImmediately(count.ToString());
    }
}
