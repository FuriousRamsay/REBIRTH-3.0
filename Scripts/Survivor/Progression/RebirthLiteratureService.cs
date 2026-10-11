using System;

#nullable disable

/// <summary>
/// Server-authoritative reusable literature transaction.
/// Reading/studying never removes or degrades the physical item. The server re-finds the exact
/// inventory stack by item type + seed before applying any discovery or theory change.
/// </summary>
public static class RebirthLiteratureService
{
    public const string InternalReadMarkerPrefix = "literature.read.";
    public const string RecipeReadMarkerPrefix = "literature.recipe_read.";
    private static readonly System.Collections.Generic.Dictionary<string,string> RecipeMarkers =
        new System.Collections.Generic.Dictionary<string,string>(StringComparer.Ordinal);
    public static string RecipeReadMarker(string knowledgeId)
    {
        if (string.IsNullOrEmpty(knowledgeId)) return string.Empty;
        // Cache pure identifier construction only, never a player's mutable read state.
        // Bounded for external/modded IDs, with exact casing retained for save compatibility.
        if (knowledgeId.Length > 256) return RecipeReadMarkerPrefix + knowledgeId;
        lock (RecipeMarkers)
        {
            if (RecipeMarkers.TryGetValue(knowledgeId, out var marker)) return marker;
            if (RecipeMarkers.Count >= 4096) RecipeMarkers.Clear();
            return RecipeMarkers[knowledgeId] = RecipeReadMarkerPrefix + knowledgeId;
        }
    }
    public static bool IsRecipeReadMarker(string id)
        => !string.IsNullOrEmpty(id) && id.StartsWith(RecipeReadMarkerPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool IsInternalReadMarker(string id)
    {
        return !string.IsNullOrEmpty(id) && (id.StartsWith(InternalReadMarkerPrefix, StringComparison.OrdinalIgnoreCase) || IsRecipeReadMarker(id));
    }

    public static bool TryReadMatchingInventoryItem(EntityPlayer player, int itemType, ushort seed, out string message)
    {
        if (!RebirthSandboxOptionManager.Current.RequireTimedReading)
        {
            if(RebirthBackpackLibraryReservation.BlocksResourceUse(player))
            {message=Localization.Get("xuiRebirthLibraryTransferPending");return false;}
            return TryCompleteStudy(player,itemType,seed,out message);
        }
        return RebirthLiteratureStudySessionService.TryBegin(player,itemType,seed,out message);
    }

    public static bool TryCompleteStudy(EntityPlayer player, int itemType, ushort seed, out string message)
    {
        message=string.Empty;
        if(player==null || !RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld())
        { message=Localization.Get("xuiRebirthReadingUnavailable"); return false; }
        if(RebirthCharacterCreationHoldService.IsHeld(player))
        { message=Localization.Get("xuiRebirthReadingFinishCreation"); return false; }

        if(RebirthBackpackLibraryReservation.BlocksResourceUse(player))
        {message=Localization.Get("xuiRebirthLibraryTransferPending");return false;}

        ItemStack stack;
        if(!TryFindMatchingInventoryStackPublic(player,itemType,seed,out stack) || stack==null || stack.IsEmpty() || stack.itemValue==null || stack.itemValue.ItemClass==null)
        { message=Localization.Get("xuiRebirthReadingItemMissing"); return false; }

        string itemId=stack.itemValue.ItemClass.GetItemName();
        RebirthLiteratureDefinition definition;
        if(!RebirthProgressionRuntimeConfig.TryGetLiterature(itemId,out definition) || definition==null)
        { message=Localization.Get("xuiRebirthReadingUnknownItem"); return false; }

        return TryApplyDefinition(player,definition,"literature:"+itemId,"book",out message);
    }

    public static bool TryCompleteAudiobook(EntityPlayer player,string audiobookItemId,out string sourceLiteratureId,out string message)
    {
        sourceLiteratureId=string.Empty;
        message=string.Empty;
        if(player==null || !RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld())
        { message=Localization.Get("xuiRebirthAudiobookUnavailable"); return false; }
        if(RebirthCharacterCreationHoldService.IsHeld(player))
        { message=Localization.Get("xuiRebirthAudiobookFinishCreation"); return false; }

        if(RebirthBackpackLibraryReservation.BlocksResourceUse(player))
        {message=Localization.Get("xuiRebirthLibraryTransferPending");return false;}

        RebirthAudiobookDefinition audio;
        if(!RebirthProgressionRuntimeConfig.TryGetAudiobook(audiobookItemId,out audio)||audio==null)
        { message=Localization.Get("xuiRebirthAudiobookUnknownCassette"); return false; }

        RebirthLiteratureDefinition definition;
        if(!RebirthProgressionRuntimeConfig.TryGetLiterature(audio.SourceLiteratureId,out definition)||definition==null)
        { message=Localization.Get("xuiRebirthAudiobookMaterialMissing"); return false; }

        sourceLiteratureId=audio.SourceLiteratureId;
        return TryApplyDefinition(player,definition,"audiobook:"+audiobookItemId,"cassette",out message);
    }

    private static bool TryApplyDefinition(EntityPlayer player,RebirthLiteratureDefinition definition,string sourceKey,string mediumName,out string message)
    {
        message=string.Empty;
        if(player==null||definition==null)return false;

        if(string.Equals(definition.Kind,"discovery",StringComparison.OrdinalIgnoreCase))
        {
            if(RebirthKnowledgeService.HasKnowledge(player,definition.KnowledgeId))
            {
                if (!RecordRecipeRead(player,definition.KnowledgeId)) { message=Localization.Get("xuiRebirthRecipeReadSaveFailed"); return false; }
                message=string.Format(Localization.Get("xuiRebirthLiteratureAlreadyKnown"),RebirthKnowledgeService.GetDisplayName(definition.KnowledgeId));
                return true;
            }
            if(!RebirthKnowledgeService.Grant(player,definition.KnowledgeId,sourceKey))
            { message=Localization.Get("xuiRebirthLiteratureLearnFailed"); return false; }
            if (!RecordRecipeRead(player,definition.KnowledgeId)) { message=Localization.Get("xuiRebirthRecipeReadSaveFailed"); return false; }
            message=string.Format(Localization.Get("xuiRebirthLiteratureLearned"),RebirthKnowledgeService.GetDisplayName(definition.KnowledgeId));
            return true;
        }

        if(string.Equals(definition.Kind,"theory",StringComparison.OrdinalIgnoreCase))
        {
            float applied; bool already;
            if(!RebirthSkillKnowledgeService.TryStudyLiterature(player,definition.SkillId,definition.Amount,definition.MarkerId,sourceKey,out applied,out already))
            { message=Localization.Get("xuiRebirthLiteratureStudyFailed"); return false; }
            if(already)
            {
                message=Localization.Get("xuiRebirthLiteratureAlreadyStudied");
                return true;
            }
            string skillName=GetSkillDisplayName(definition.SkillId);
            if(applied>0.0001f)
                message=string.Format(Localization.Get("xuiRebirthLiteratureTheoryGained"),skillName,applied.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture));
            else
                message=string.Format(Localization.Get("xuiRebirthLiteratureNoNewTheory"),skillName);
            return true;
        }

        message=Localization.Get("xuiRebirthReadingUnknownItem");
        return false;
    }

    private static bool RecordRecipeRead(EntityPlayer player,string knowledgeId)
    {
        if (string.IsNullOrEmpty(knowledgeId)) return false;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if (!RebirthSkillAwardService.TryGetEligible(player,out identity,out record)) return false;
        string marker = RecipeReadMarker(knowledgeId);
        if (record.Progression.KnowledgeIds.Contains(marker)) return true;
        record.Progression.KnowledgeIds.Add(marker);
        RebirthWorldCharacterService.MarkDirty(record,"literature-recipe-read");
        if (!RebirthWorldCharacterService.FlushPlayer(player,"literature-recipe-read"))
        {
            record.Progression.KnowledgeIds.Remove(marker);
            RebirthWorldCharacterService.MarkDirty(record,"literature-recipe-read-retry");
            return false;
        }
        if (!RebirthSurvivorNetworkService.SendOwnerState(player,0L,true,"literature-recipe-read"))
            RebirthSkillAwardService.QueueOwnerPublication(player);
        return true;
    }

    public static bool IsAlreadyCompleted(EntityPlayer player,RebirthLiteratureDefinition definition)
    {
        if(player==null||definition==null)return false;
        if(string.Equals(definition.Kind,"discovery",StringComparison.OrdinalIgnoreCase))
            return !string.IsNullOrEmpty(definition.KnowledgeId) && RebirthKnowledgeService.HasKnowledge(player,RecipeReadMarker(definition.KnowledgeId));
        if(string.Equals(definition.Kind,"theory",StringComparison.OrdinalIgnoreCase))
        {
            return RebirthKnowledgeService.HasKnowledge(player,definition.MarkerId);
        }
        return false;
    }

    public static string GetAlreadyCompletedMessage(EntityPlayer player,RebirthLiteratureDefinition definition)
    {
        if(definition==null)return Localization.Get("xuiRebirthLiteratureAlreadyStudied");
        if(string.Equals(definition.Kind,"discovery",StringComparison.OrdinalIgnoreCase))
            return string.Format(Localization.Get("xuiRebirthLiteratureAlreadyKnown"),RebirthKnowledgeService.GetDisplayName(definition.KnowledgeId));
        return Localization.Get("xuiRebirthLiteratureAlreadyStudied");
    }

    private static string GetSkillDisplayName(string skillId) => RebirthSkillDisplayNames.Get(skillId);

    public static bool HasMatchingInventoryItem(EntityPlayer player,int itemType,ushort seed)
    {
        ItemStack stack;
        return TryFindMatchingInventoryStackPublic(player,itemType,seed,out stack)&&stack!=null&&!stack.IsEmpty();
    }

    public static bool TryFindMatchingInventoryStackPublic(EntityPlayer player,int itemType,ushort seed,out ItemStack stack)
    {
        stack=null;
        if(player==null)return false;
        ItemStack[] bag;
        ItemStack[] belt;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(connection!=null && connection.IsServer && !(player is EntityPlayerLocal))
        {
            // Native inventory packets update ClientInfo.latestPlayerData, not the
            // remote entity's bag. Never fall back to stale entity storage.
            var owner=connection.Clients.ForEntityId(player.entityId);
            if(owner?.latestPlayerData==null)return false;
            bag=RebirthPlayerDataInventory.ReadSlots(owner.latestPlayerData,true);
            belt=RebirthPlayerDataInventory.ReadSlots(owner.latestPlayerData,false);
        }
        else
        {
            bag=player.bag!=null?player.bag.ItemGrid.items:null;
            belt=player.inventory!=null?player.inventory.ItemGrid.items:null;
        }
        if(TryFind(bag,int.MaxValue,itemType,seed,out stack))return true;
        return TryFind(belt,RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt!=null?belt.Length:0),itemType,seed,out stack);
    }

    private static bool TryFind(ItemStack[] slots,int limit,int itemType,ushort seed,out ItemStack stack)
    {
        stack=null;if(slots==null)return false;
        for(int i=0;i<Math.Min(limit,slots.Length);i++)
        {
            ItemStack value=slots[i];
            if(value==null||value.IsEmpty()||value.itemValue==null||value.itemValue.type!=itemType||value.itemValue.Seed!=seed)continue;
            stack=value;return true;
        }
        return false;
    }
}
