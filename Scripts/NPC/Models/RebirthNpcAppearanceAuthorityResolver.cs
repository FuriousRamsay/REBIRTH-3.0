using System;
using System.Collections.Generic;
using System.Globalization;

#nullable disable

/// <summary>Authority game-thread selection from qualified native text definitions; no visual generation.</summary>
internal static class RebirthNpcAppearanceAuthorityResolver
{
    internal const string GeneratorId="rebirth-sdcs-resolved-v1";
    internal static RebirthHumanNpcAppearanceDescriptor Resolve(RebirthHumanNpcAppearanceDescriptor original,
        bool male, string modelId)
    {
        // Existing choices win even when a future catalogue is unavailable. Materialization validates separately.
        if(original.Resolved!=null) return original;
        if(!original.IsValid || original.Pipeline!=RebirthHumanNpcModelPipeline.SDCS ||
            string.IsNullOrEmpty(original.Archetype) || Archetype.GetArchetype(original.Archetype)==null)
            throw new InvalidOperationException("NPC resolution requires a concrete authored SDCS archetype.");
        string catalogue=RebirthSdcsCatalogService.QualifyTextCatalog();
        var random=new System.Random(original.Seed==0?1:original.Seed);
        // Match legacy generation order exactly. No invented fallback race/eye/variant IDs.
        string race=Pick(SDCSDataUtils.GetRaceList(male),random,false);
        string variantText=Pick(SDCSDataUtils.GetVariantList(male,race),random,false);
        if(!int.TryParse(variantText,NumberStyles.Integer,CultureInfo.InvariantCulture,out int variant)||variant<=0)
            throw new InvalidOperationException("Native SDCS variant is invalid.");
        string eye=Pick(SDCSDataUtils.GetEyeColorNames(),random,false);
        string hair=Pick(SDCSDataUtils.GetHairNames(male,SDCSDataUtils.HairTypes.Hair),random,true);
        var colours=SDCSDataUtils.GetHairColorNames();
        string colour=colours!=null&&colours.Count>0?colours[random.Next(colours.Count)].PrefabName:string.Empty;
        string mustache=string.Empty,chops=string.Empty,beard=string.Empty;
        if(male)
        {
            mustache=Pick(SDCSDataUtils.GetHairNames(true,SDCSDataUtils.HairTypes.Mustache),random,true);
            chops=Pick(SDCSDataUtils.GetHairNames(true,SDCSDataUtils.HairTypes.Chops),random,true);
            beard=Pick(SDCSDataUtils.GetHairNames(true,SDCSDataUtils.HairTypes.Beard),random,true);
        }
        if(!RebirthNpcResolvedAppearance.TryCreate(original.Pipeline,original.Seed,original.Archetype,catalogue,
            GeneratorId,modelId,male,race,variant,eye,hair,colour,mustache,chops,beard,out var resolved)||
            !original.TryWithResolved(resolved,out var result))
            throw new InvalidOperationException("Native SDCS choices cannot be represented in the bounded appearance schema.");
        return result;
    }
    private static string Pick(List<string> values,System.Random random,bool optional)
    {
        if(values==null||values.Count==0)
        { if(optional)return string.Empty;throw new InvalidOperationException("Required native SDCS choices unavailable."); }
        return values[random.Next(values.Count)];
    }
}