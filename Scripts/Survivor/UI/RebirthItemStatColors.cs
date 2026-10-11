using System.Text.RegularExpressions;

// Apply the same sign-based negative color to every custom item-stat presentation.
public static class RebirthItemStatColors
{
    private static readonly Regex Tokens = new Regex(@"\[[^\]]*\]|[-−]\d+(?:[.,]\d+)*(?:%)?", RegexOptions.Compiled);
    public static string Format(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        // Use the registered native inline stat symbol; arbitrary atlas sprite names are not text symbols.
        var tint = XUiBindingHelper.StatBoostedColor;
        string star = "[" + tint.r.ToString("X2") + tint.g.ToString("X2") + tint.b.ToString("X2") + "][t][sp=ui_stat][/t][-]";
        text = text.Replace("[FFD700][sp=ui_stat][-]", "[sp=ui_stat]").Replace("[sp=ui_stat]", star);
        return Tokens.Replace(text, match => match.Value[0] == '['
            ? match.Value : "[FF0000]" + match.Value + "[-]");
    }
    public static string MarkDerived(string text, ItemValue item, params PassiveEffects[] inputs)
    {
        if (item != null && inputs != null)
            foreach (var effect in inputs)
                if (item.GetStatPercent(effect,true) != 1f)
                    return Format("[sp=ui_stat]" + text);
        return Format(text);
    }
    public static string NativeValue(ItemStack stack, EntityPlayer player, DisplayInfoEntry stat)
    {
        string text=XUiM_ItemStack.GetStatItemValueTextWithModInfo(stack,player,stat);
        return WithBonus(text,stack?.itemValue,stat);
    }
    public static string NativeValue(ItemValue item, EntityPlayer player, DisplayInfoEntry stat)
        => NativeValue(new ItemStack(item,1),player,stat);
    public static string WithBonus(string text,ItemValue item,DisplayInfoEntry stat)
    {
        if(item!=null&&stat!=null&&item.GetStatPercent(stat.StatType,true)!=1f && !string.IsNullOrEmpty(text) && !text.Contains("[sp=ui_stat]") && !text.Contains("[sp=ui_game_symbol_star]"))text="[sp=ui_stat]"+text;
        return text;
    }
}
