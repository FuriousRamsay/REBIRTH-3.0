using System;
using System.Globalization;
using System.Text;

// Presentation only: retain translated names; readable fallback never changes knowledge IDs.
public static class RebirthKnowledgeDisplayNames
{
    public static string Get(string id)
    {
        string value = (id ?? string.Empty).Trim();
        if (value.Length == 0) return string.Empty;
        RebirthKnowledgeDefinition definition;
        if (RebirthSurvivorDefinitionRegistry.TryGetKnowledge(value, out definition) && definition != null &&
            !string.IsNullOrWhiteSpace(definition.NameKey))
        {
            string localized = Localization.Get(definition.NameKey);
            if (!string.IsNullOrWhiteSpace(localized) &&
                !string.Equals(localized, definition.NameKey, StringComparison.OrdinalIgnoreCase)) return localized;
        }
        // Recipe knowledge can reuse the actual native item's name when its own key is unavailable.
        if (value.StartsWith("recipe.", StringComparison.OrdinalIgnoreCase))
        {
            string nativeId = value.Substring(7);
            try
            {
                ItemValue item = ItemClass.GetItem(nativeId, false);
                if (item != null && !item.IsEmpty() && item.ItemClass != null)
                {
                    string name = item.ItemClass.GetLocalizedItemName();
                    if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, nativeId, StringComparison.OrdinalIgnoreCase)) return name;
                }
            }
            catch { }
        }
        StringBuilder readable = new StringBuilder(value.Length + 12);
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (current == '.' || current == '_' || current == '-')
            {
                readable.Append(' ');
                continue;
            }
            if (i > 0 && char.IsUpper(current) &&
                (char.IsLower(value[i - 1]) || (char.IsUpper(value[i - 1]) && i + 1 < value.Length && char.IsLower(value[i + 1]))))
                readable.Append(' ');
            readable.Append(current);
        }
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(readable.ToString());
    }
}
