using System;using System.Xml.Linq;
partial class RebirthWorldCharacterRepository{
    internal static bool HasSavedRecipeDiscovery(RebirthStablePlayerIdentity identity,
        RebirthStationRecipeDiscoveryRecord expected)
    {
        if(!serverAuthority||identity==null||expected==null)return false;
        var image=expected.Write();
        if(!RebirthStationRecipeDiscoveryRecord.TryReadStored(image,out var validated)||
            (string)image.Attribute("owner")!=identity.StorageKey)return false;
        string creation=(string)image.Attribute("creation");
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!serverAuthority||!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(creation,saved.Origin?.CreationId)||
                    !saved.Progression.RecipeDiscoveries.TryGetValue(validated.CanonicalRecipe,out var stored)||stored==null||
                    !serverAuthority||path!=GetPath(identity.StorageKey))return false;
                return XNode.DeepEquals(stored.Write(),image);
            }
            catch{return false;}
        }
    }
}