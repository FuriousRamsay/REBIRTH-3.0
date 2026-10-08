using System;
// Candidate only. Resolves the effective native output description; adds no gameplay prose.
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
    private static string Localized(string key)
    {
        if(string.IsNullOrWhiteSpace(key))return string.Empty;
        string value=Localization.Get(key);
        return string.IsNullOrWhiteSpace(value)||string.Equals(value,key,StringComparison.OrdinalIgnoreCase)?string.Empty:value;
    }
}


