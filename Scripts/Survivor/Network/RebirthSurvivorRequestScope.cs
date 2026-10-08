using System;

public static class RebirthSurvivorRequestScope
{
    public static bool TryNormalize(string value,out string normalized)
    {
        normalized=null;
        if(Guid.TryParse(value,out var id)&&id!=Guid.Empty){normalized=id.ToString("N");return true;}
        if(IsLegacyCreation(value)){normalized=value;return true;}
        return false;
    }
    public static bool Matches(string requestedCreation, string currentCreation)
    {
        Guid requested, current;
        if(Guid.TryParse(requestedCreation,out requested)&&requested!=Guid.Empty)
            return Guid.TryParse(currentCreation,out current)&&requested==current;
        // Schema-1 migration generated a stable legacy- plus lowercase SHA256 ID.
        // Compare the complete persisted identity; never truncate it into a wire GUID.
        return IsLegacyCreation(requestedCreation)&&
            string.Equals(requestedCreation,currentCreation,StringComparison.Ordinal);
    }
    private static bool IsLegacyCreation(string value)
    {
        if(value==null||value.Length!=71||!value.StartsWith("legacy-",StringComparison.Ordinal))return false;
        for(int i=7;i<value.Length;i++)
            if(!(value[i]>='0'&&value[i]<='9'||value[i]>='a'&&value[i]<='f'))return false;
        return true;
    }
}