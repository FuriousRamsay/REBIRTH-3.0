using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

#nullable disable

/// <summary>
/// Native-authority adapter for the Rebirth-owned Personal Crafting recipe catalogue.
///
/// The service owns no craft rules. It projects the same XUiM_Recipes/CraftingManager data used by
/// the stock RecipeList, including quest/challenge flags, category filtering, search, Favorites,
/// unlock state, ingredient availability and native RecipeInfo ordering. Ingredient projection is
/// deliberately completed through XUiC_RecipeList.BuildRecipeInfosList so existing Harmony patches
/// that extend that method (notably Remote Crafting container resources) remain authoritative.
/// </summary>
public sealed class RebirthCraftingRecipeCatalogueService
{
    public sealed class Category
    {
        public string Name;
        public string Icon;
        public string DisplayName;
    }

    private readonly XUi xui;
    private readonly RebirthCraftingPresentation owner;
    private readonly List<Category> categories = new List<Category>();
    private readonly Dictionary<string, string> categoryDisplayByName =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public RebirthCraftingRecipeCatalogueService(XUi xui, RebirthCraftingPresentation owner)
    {
        this.xui = xui;
        this.owner = owner;
    }

    public IReadOnlyList<Category> Categories => categories;

    public void RefreshCategories()
    {
        categories.Clear();
        categoryDisplayByName.Clear();

        UIDisplayInfoManager manager = UIDisplayInfoManager.Current;
        List<CraftingCategoryDisplayEntry> source = manager != null
            ? manager.GetCraftingCategoryDisplayList(owner?.Workstation ?? string.Empty)
            : null;

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            CraftingCategoryDisplayEntry entry = source[i];
            if (entry == null || string.IsNullOrEmpty(entry.Name))
                continue;

            Category category = new Category
            {
                Name = entry.Name ?? string.Empty,
                Icon = entry.Icon ?? string.Empty,
                DisplayName = !string.IsNullOrEmpty(entry.DisplayName)
                    ? entry.DisplayName
                    : entry.Name
            };

            categories.Add(category);
            if (!categoryDisplayByName.ContainsKey(category.Name))
                categoryDisplayByName.Add(category.Name, category.DisplayName);
        }
    }

    public void RebuildFiltered(
        XUiC_RebirthCraftingRecipeCatalogue catalogue,
        string searchText,
        string category,
        bool favoritesOnly,
        ItemStack ingredient = null)
    {
        if (catalogue == null || xui == null)
            return;

        ReadOnlyCollection<Recipe> allRecipes = XUiM_Recipes.GetRecipes();
        string station = owner?.Workstation ?? string.Empty;
        bool milling = (owner?.Controller as XUiC_RebirthCookingStation)?.UsesSharedMillingPresentation == true;
        if (!string.IsNullOrEmpty(station))
            allRecipes = allRecipes.Where(r => r != null && (milling
                ? RebirthCookingCatalogue.AtStation(r, station)
                : string.Equals(r.craftingArea, station, StringComparison.OrdinalIgnoreCase)))
                .Select(r => milling ? RebirthCookingCatalogue.ForStation(r, station) : r)
                .ToList().AsReadOnly();
        if (string.Equals(station, "forge", StringComparison.OrdinalIgnoreCase))
            allRecipes = XUiM_Recipes.FilterRecipesByWorkstation(station, allRecipes).AsReadOnly();
        MarkQuestAndChallengeRecipes(allRecipes);

        List<Recipe> filtered;
        if (ingredient != null && !ingredient.IsEmpty())
        {
            // Identical semantic lookup to b10's SetRecipeDataByIngredientStack. Do not
            // substitute output-name search or fall back to a remembered category.
            filtered = XUiM_Recipes.FilterRecipesByIngredient(ingredient, allRecipes);
        }
        else if (!string.IsNullOrEmpty(searchText))
        {
            // Preserve the stock RecipeList search semantics: search uses the global recipe set
            // (assembly recipes remain excluded by XUiM_Recipes.FilterRecipesByName itself).
            filtered = XUiM_Recipes.FilterRecipesByName(searchText, allRecipes);
        }
        else
        {
            filtered = allRecipes.ToList();
            if (favoritesOnly)
                CraftingManager.GetFavoriteRecipesFromList(ref filtered);
            else if (!string.IsNullOrEmpty(category))
                XUiM_Recipes.FilterRecipesByCategory(category, ref filtered);
        }


        catalogue.recipes.Clear();
        if (filtered != null)
            catalogue.recipes.AddRange(filtered);

        BuildRecipeInfosFromCurrentSource(catalogue);
    }

    public void BuildRecipeInfosFromCurrentSource(XUiC_RebirthCraftingRecipeCatalogue catalogue)
    {
        if (catalogue == null || xui == null || xui.PlayerInventory == null)
            return;

        List<ItemStack> items = catalogue.updateStackList;
        items.Clear();
        items.AddRange(xui.PlayerInventory.GetBackpackItemStacks());
        items.AddRange(xui.PlayerInventory.GetToolbeltItemStacks());

        // Source-verified native path. Remote Crafting patches BuildRecipeInfosList and therefore
        // continues to augment this exact call with nearby-resource availability.
        catalogue.BuildRecipeInfosList(items);
        catalogue.recipeInfos.Sort(catalogue.CompareRecipeInfos);
        catalogue.UpdateRecipes();
    }

    public string ResolveCategoryDisplayName(Recipe recipe)
    {
        if (recipe == null)
            return string.Empty;

        ItemClass item = ItemClass.GetForId(recipe.itemValueType);
        string[] groups = null;
        if (item != null)
            groups = item.IsBlock() ? item.GetBlock().GroupNames : item.Groups;

        if (groups != null)
        {
            for (int i = 0; i < groups.Length; i++)
            {
                string group = groups[i];
                if (string.IsNullOrEmpty(group))
                    continue;

                string display;
                if (categoryDisplayByName.TryGetValue(group, out display))
                    return display ?? group;
            }
        }

        if (!string.IsNullOrEmpty(owner?.Workstation))
            return RebirthSkillDisplayNames.Get(RebirthServiceCraftSkillService.ClassifyRecipe(recipe));

        // A recipe can legitimately appear because it is tracked/quest/challenge even when it
        // does not belong to the active category. Do not invent a fake category in that case.
        return string.Empty;
    }

    private void MarkQuestAndChallengeRecipes(ReadOnlyCollection<Recipe> allRecipes)
    {
        if (allRecipes == null || xui.playerUI == null || xui.playerUI.entityPlayer == null)
            return;

        List<string> questRecipes = xui.playerUI.entityPlayer.QuestJournal.GetQuestRecipes();
        List<Recipe> challengeRecipes = xui.QuestTracker != null && xui.QuestTracker.TrackedChallenge != null
            ? xui.QuestTracker.TrackedChallenge.CraftedRecipes()
            : null;

        bool hasQuestRecipes = questRecipes != null && questRecipes.Count > 0;
        bool hasChallengeRecipes = challengeRecipes != null && challengeRecipes.Count > 0;

        for (int i = 0; i < allRecipes.Count; i++)
        {
            Recipe recipe = allRecipes[i];
            if (recipe == null)
                continue;

            if (hasChallengeRecipes && challengeRecipes.Contains(recipe))
            {
                recipe.isChallenge = true;
                recipe.isQuest = false;
            }
            else if (hasQuestRecipes && questRecipes.Contains(recipe.GetName()))
            {
                recipe.isQuest = true;
                recipe.isChallenge = false;
            }
            else
            {
                recipe.isQuest = false;
                recipe.isChallenge = false;
            }
        }
    }
}
