using System;
// Resolves authored recipe/output descriptions without changing crafting requirements.
internal static class RebirthRecipeDescriptionText
{
    internal static string Get(string recipeName)
    {
        string name=(recipeName??string.Empty).Trim();
        if(name.Length==0)return string.Empty;
        string authored=Localized(name+"Desc");
        if(authored.Length>0)return authored;
        string key=string.Empty;
        try
        {
            Recipe recipe=CraftingManager.GetRecipe(name);
            ItemValue value=recipe==null?ItemClass.GetItem(name,false):null;
            ItemClass item=recipe!=null?ItemClass.GetForId(recipe.itemValueType):value==null||value.IsEmpty()?null:value.ItemClass;
            if(item!=null)
            {
                if(item.IsBlock())
                {
                    int type=recipe!=null?recipe.itemValueType:value.type;
                    if(Block.list!=null&&type>=0&&type<Block.list.Length&&Block.list[type]!=null)
                        key=Block.list[type].DescriptionKey;
                }
                else key=item.GetItemDescriptionKey();
            }
        }
        catch { }
        string description=Localized(key);
        return description.Length>0?description:Localized(name+"Desc");
    }
    internal static string GetForRecipe(Recipe recipe)
    {
        if(recipe==null)return string.Empty;
        string description=Get(recipe.GetName());
        if(!string.IsNullOrWhiteSpace(description))return description;
        string fallback=Localized("xuiRebirthRecipeDescriptionFallback");
        return fallback.Length>0?fallback:"Check the ingredients and crafting requirements below.";
    }
    private static string Localized(string key)
    {
        if(string.IsNullOrWhiteSpace(key))return string.Empty;
        string value=Localization.Get(key);
        return string.IsNullOrWhiteSpace(value)||string.Equals(value,key,StringComparison.OrdinalIgnoreCase)?string.Empty:value;
    }
}


