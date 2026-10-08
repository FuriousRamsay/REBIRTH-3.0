using System;

// Detached native completion record; not output delivery or durable terminal evidence.
internal static class RebirthStationCompletionReceipt
{
    internal const string Prefix="rebirth.station.receipt.";
    internal sealed class Identity
    {
        internal readonly string Job,Creation,Definition;
        internal readonly int Tier,Count,Experience;
        internal Identity(string job,string creation,string definition,int tier,int count,int xp)
        {Job=job;Creation=creation;Definition=definition;Tier=tier;Count=count;Experience=xp;}
    }
    // Any reserved marker requires authoritative settlement, even if malformed.
    // Native CheckForCraftComplete would otherwise grant XP by a reusable entity ID.
    internal static bool HasReservedMarker(CraftCompleteData data)
    {
        var metadata=data?.CraftedItemStack?.itemValue?.Metadata;
        if(metadata==null)return false;
        foreach(var key in metadata.Keys)
            if(key!=null&&key.StartsWith(Prefix,StringComparison.Ordinal))return true;
        return false;
    }
    internal static bool CanUseNativeRewards(System.Collections.Generic.IList<CraftCompleteData> records)
    {
        if(records==null)return true;
        foreach(var receipt in records)if(HasReservedMarker(receipt))return false;
        return true;
    }
    // Identity filter only; never substitutes for authenticated native terminal proof.
    internal static bool TryReadIdentity(CraftCompleteData data,out Identity identity)
    {
        identity=null;
        try
        {
            var item=data?.CraftedItemStack?.itemValue;
            if(item==null||item.type<=0||item.Metadata==null||data.CrafterEntityID<=0||data.RecipeUsedCount!=1||
                string.IsNullOrEmpty(data.RecipeName)||data.RecipeName.Length>256||data.ItemScrapped!=string.Empty||
                !item.TryGetMetadata(Prefix+"version",out int version)||version!=1||
                !item.TryGetMetadata(Prefix+"job",out string job)||!Guid.TryParseExact(job,"N",out var jobId)||jobId==Guid.Empty||
                !item.TryGetMetadata(Prefix+"creation",out string creation)||
                !RebirthSurvivorRequestScope.TryNormalize(creation,out var character)||character!=creation||
                !item.TryGetMetadata(Prefix+"definition",out string definition)||definition==null||definition.Length!=64||
                !item.TryGetMetadata(Prefix+"tier",out int tier)||tier<0||tier>6||
                !item.TryGetMetadata(Prefix+"count",out int count)||count<1||count>32767||count!=data.CraftedItemStack.count||
                !item.TryGetMetadata(Prefix+"xp",out int xp)||xp<0||xp!=data.CraftExpGain)return false;
            foreach(char c in definition)if(!Uri.IsHexDigit(c))return false;
            foreach(var key in item.Metadata.Keys)
                if(key.StartsWith(Prefix,StringComparison.Ordinal))
                    switch(key.Substring(Prefix.Length))
                    {case "version":case "job":case "creation":case "definition":case "tier":case "count":case "xp":break;default:return false;}
            identity=new Identity(job,creation,definition,tier,count,xp);return true;
        }
        catch{return false;}
    }
    // Live absence filter only; absence is not durable cancellation or refund evidence.
    internal static bool HasNoJobReceipt(System.Collections.Generic.IList<CraftCompleteData> records,string job)
    {
        if(string.IsNullOrEmpty(job))return false;
        if(records==null)return true;
        if(records.Count>short.MaxValue)return false;
        try
        {
            foreach(var data in records)
            {
                var item=data?.CraftedItemStack?.itemValue;
                if(item==null||data.CraftedItemStack.count<0)return false;
                bool marked=false;
                if(item.Metadata!=null)foreach(var key in item.Metadata.Keys)
                    if(key.StartsWith(Prefix,StringComparison.Ordinal)){marked=true;break;}
                if(marked&&(!TryReadIdentity(data,out var identity)||identity.Job==job))return false;
            }
            return true;
        }
        catch{return false;}
    }
    // Saved admission binding only; authenticity still requires server-native terminal publication evidence.
    internal static bool MatchesAdmission(RebirthStationGridAdmission admission,Recipe savedRecipe,CraftCompleteData data)
    {
        try
        {
            if(admission==null||!admission.IsPublicationAttempted||!TryReadIdentity(data,out var identity)||
                identity.Job!=admission.JobId||identity.Creation!=admission.CreationId||identity.Definition!=admission.DefinitionId||
                !admission.MatchesQueuedQuality(savedRecipe,1,identity.Tier)||savedRecipe.count!=identity.Count||
                savedRecipe.craftExpGain!=identity.Experience||savedRecipe.GetName()!=data.RecipeName)return false;
            var item=data.CraftedItemStack.itemValue;
            return item.type==savedRecipe.itemValueType&&item.ItemClass!=null&&
                (!item.ItemClass.HasQuality||item.Quality==identity.Tier);
        }
        catch{return false;}
    }
    internal static bool TryCreate(RebirthStationGridAdmission admission,RecipeQueueItem queued,
        ItemValue nativeOutput,int outputCount,out CraftCompleteData receipt)
    {
        receipt=null;
        try
        {
            if(admission==null||!admission.IsPublicationAttempted||
                !RebirthSurvivorRequestScope.TryNormalize(admission.CreationId,out var creation)||creation!=admission.CreationId||
                queued?.Recipe==null||queued.StartingEntityId<=0||
                !admission.MatchesQueuedQuality(queued.Recipe,queued.Multiplier,queued.Quality)||
                nativeOutput==null||nativeOutput.type!=queued.Recipe.itemValueType||outputCount!=queued.Recipe.count||
                nativeOutput.HasMetadata(Prefix+"version")||nativeOutput.ItemClass==null||
                nativeOutput.ItemClass.HasQuality&&nativeOutput.Quality!=queued.Quality)return false;
            var item=nativeOutput.Clone();
            item.SetMetadata(Prefix+"version",1);item.SetMetadata(Prefix+"job",admission.JobId);
            item.SetMetadata(Prefix+"creation",admission.CreationId);item.SetMetadata(Prefix+"definition",admission.DefinitionId);
            item.SetMetadata(Prefix+"tier",(int)queued.Quality);item.SetMetadata(Prefix+"count",outputCount);
            item.SetMetadata(Prefix+"xp",queued.Recipe.craftExpGain);
            receipt=new CraftCompleteData(queued.StartingEntityId,new ItemStack(item,outputCount),
                queued.Recipe.GetName(),string.Empty,queued.Recipe.craftExpGain,1);
            return true;
        }
        catch{return false;}
    }
}