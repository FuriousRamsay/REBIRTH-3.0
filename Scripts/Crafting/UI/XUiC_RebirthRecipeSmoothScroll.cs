using System;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Makes the redesigned crafting recipe catalogue a real continuous scroll region instead of
/// exposing the native page-by-page presentation.
///
/// XUiC_RecipeList remains authoritative for filtering, sorting, availability, selection and
/// recipe assignment. XML authors a bounded pool of row controllers; this controller only sizes
/// that pool and its grid to the number of currently filtered RecipeInfo records so the stock
/// scrollview/defaultscrollbar can move smoothly by pixels/rows.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthRecipeSmoothScroll : XUiController
{
    private const int RowHeight = 46;
    private const int ViewportHeight = 368;
    private const int ContentWidth = 396;
    private const int AuthoredRowCapacity = 256;
    private const int MinimumRows = 8;

    private XUiC_RecipeList recipeList;
    private XUiController scrollView;
    private int lastRecipeCount = -1;
    private int settleFrames = 3;

    public override void Init()
    {
        base.Init();
        ResolveControls();
        RefreshGeometry(true);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        settleFrames = 3;
        lastRecipeCount = -1;
        ResolveControls();
        RefreshGeometry(true);
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return;

        if (recipeList == null || scrollView == null)
            ResolveControls();

        if (settleFrames > 0)
        {
            settleFrames--;
            RefreshGeometry(true);
            return;
        }

        RefreshGeometry(false);
    }

    private void ResolveControls()
    {
        recipeList = GetChildById("recipes") as XUiC_RecipeList;
        scrollView = GetChildById("rebirthRecipeScrollView");
    }

    private void RefreshGeometry(bool force)
    {
        if (recipeList == null || recipeList.ViewComponent == null)
            return;

        int recipeCount = recipeList.recipeInfos != null ? recipeList.recipeInfos.Count : 0;
        if (!force && recipeCount == lastRecipeCount)
            return;

        bool countChanged = recipeCount != lastRecipeCount;
        lastRecipeCount = recipeCount;

        int rows = Mathf.Clamp(Math.Max(MinimumRows, recipeCount), MinimumRows, AuthoredRowCapacity);
        int contentHeight = Math.Max(ViewportHeight, rows * RowHeight);

        XUiV_Grid grid = recipeList.ViewComponent as XUiV_Grid;
        if (grid != null && grid.Rows != rows)
            grid.Rows = rows;

        Vector2i size = recipeList.ViewComponent.Size;
        if (size.x != ContentWidth || size.y != contentHeight)
            recipeList.ViewComponent.Size = new Vector2i(ContentWidth, contentHeight);

        // Changing the number of active grid rows changes the native RecipeList page capacity.
        // Mark it dirty so its next update assigns the filtered RecipeInfos across that row pool.
        if (countChanged)
            recipeList.IsDirty = true;

        if (scrollView != null)
        {
            RebirthNativeScrollbarUtil.Refresh(scrollView);
            if (countChanged)
            {
                RebirthNativeScrollbarUtil.TrySetValue(scrollView, 0f);
                RebirthNativeScrollbarUtil.Refresh(scrollView);
            }
        }
    }
}
