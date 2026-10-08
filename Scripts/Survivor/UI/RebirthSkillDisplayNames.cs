using System;
using System.Globalization;

// User-facing skill names never fall back to serialized skill.* identifiers.
public static class RebirthSkillDisplayNames
{
    public static string Get(string id)
    {
        string value=(id??string.Empty).Trim();
        if(value.Length==0)return string.Empty;
        RebirthSkillDefinition skill;
        if(RebirthSurvivorDefinitionRegistry.TryGetSkill(value,out skill)&&skill!=null&&!string.IsNullOrWhiteSpace(skill.NameKey))
        {
            string localized=Localization.Get(skill.NameKey);
            if(!string.IsNullOrWhiteSpace(localized)&&!string.Equals(localized,skill.NameKey,StringComparison.OrdinalIgnoreCase))return localized;
        }
        if(value.StartsWith("skill.",StringComparison.OrdinalIgnoreCase))value=value.Substring(6);
        value=value.Replace('_',' ').Replace('.',' ');
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value);
    }
}