using System;
using System.IO;

// Server-owned physical cassettes. UI never supplies serialized item data.
public static class RebirthMusicLibraryService
{
    public const int Capacity = 24;

    public static bool IsMusicCassette(string id)
    {
        const string prefix = "FuriousRamsayCassette";
        if (id == null || id.Length != prefix.Length + 2 || !id.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        char tens = id[prefix.Length], units = id[prefix.Length + 1];
        if (tens < '0' || tens > '9' || units < '0' || units > '9') return false;
        int number = (tens - '0') * 10 + units - '0';
        return number >= 1 && number <= 23;
    }

    internal static bool CanBeginChange(RebirthWorldSupportState state,long expectedRevision)
    {
        return state!=null&&expectedRevision>=0&&expectedRevision<long.MaxValue&&
            state.MusicRevision==expectedRevision&&state.PendingGearTransfer==null&&
            state.PendingMusicTransfer==null&&state.PendingLibraryTransfer==null;
    }

    public static bool TryChange(EntityPlayer player, int operation, int index, int itemType,
        ushort seed, long expectedRevision, out string message)
    {
        message = Localization.Get("xuiRebirthMusicUnavailable");
        RebirthWorldCharacterRecord record;
        if (player == null || player.IsDead() || !RebirthWorldCharacterRepository.IsServerAuthority
            || !RebirthSurvivorMode.IsEnabledForCurrentWorld()
            || RebirthCharacterCreationHoldService.IsHeld(player)
            || !RebirthWorldCharacterService.TryGet(player, out record) || record?.Support == null
            || !record.IsComplete || !RebirthSurvivorGearService.HasEquippedWalkman(player)) return false;
        if (record.Support.MusicRevision != expectedRevision)
        { message = Localization.Get("xuiRebirthMusicRefresh"); return false; }
        var state = record.Support;
        if(state.PendingLibraryTransfer!=null){message=Localization.Get("xuiRebirthLibraryTransferPending");return false;}
        if(!CanBeginChange(state,expectedRevision))
        { message=Localization.Get("xuiRebirthMusicRefresh"); return false; }
        if(operation==4)return TryReorder(player,record,index,expectedRevision,out message);
        // Physical insertion/removal must go through the saved transfer journal,
        // owning native receipt and saved-player checkpoint on every topology.
        if (operation == 3) state.MusicShuffle = index != 0;
        else return false;
        state.MusicRevision++;
        RebirthWorldCharacterService.MarkDirty(record, "music-library-change");
        RebirthSurvivorNetworkService.SendOwnerState(player, Math.Max(0L, record.Revision - 1L), true, "music-library-change");
        message = string.Empty;
        return true;
    }

    private static bool TryReorder(EntityPlayer player,RebirthWorldCharacterRecord record,int encoded,
        long expectedRevision,out string message)
    {
        message=Localization.Get("xuiRebirthMusicRefresh");
        var state=record?.Support;
        if(!CanBeginChange(state,expectedRevision)||encoded<0||encoded>=Capacity*Capacity)return false;
        int source=encoded/Capacity,target=encoded%Capacity;
        if(source>=state.MusicCassettes.Count||target>=state.MusicCassettes.Count||source==target)return false;
        var first=state.MusicCassettes[source];var second=state.MusicCassettes[target];
        if(first==null||second==null)return false;
        RebirthStablePlayerIdentity identity;
        RebirthWorldCharacterRecord owned;
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out identity)||identity==null||
            !RebirthWorldCharacterRepository.TryGet(identity,out owned)||!ReferenceEquals(owned,record))return false;
        state.MusicCassettes[source]=second;state.MusicCassettes[target]=first;
        state.MusicRevision=expectedRevision+1;
        record.Touch("music-library-reorder");
        bool saved;
        try{saved=RebirthWorldCharacterRepository.SaveIfDirty(identity,"music-library-reorder");}catch{saved=false;}
        if(!saved)
        {
            state.MusicCassettes[source]=first;state.MusicCassettes[target]=second;
            state.MusicRevision=expectedRevision;
            record.Touch("music-library-reorder-rollback");
            return false;
        }
        if(!RebirthSurvivorNetworkService.SendOwnerState(player,0L,true,"music-library-reorder"))
            RebirthSkillAwardService.QueueOwnerPublication(player);
        message=string.Empty;return true;
    }
    internal static string Encode(ItemValue value)
    {
        return RebirthNativeItemCodec.Encode(value);
    }

    internal static int FindReturnSlot(ItemStack[] slots, ItemValue value, out ItemStack replacement)
    {
        replacement = null;
        if (slots == null) return -1;
        var returned = new ItemStack(value, 1);
        string exactValue = Encode(value);
        int empty = -1;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.IsEmpty())
            {
                if (empty < 0) empty = i;
                continue;
            }
            // Native stack compatibility does not compare every ItemValue field.
            // Keep cassette metadata intact even when its item type is identical.
            if (!slot.CanStackWith(returned) || Encode(slot.itemValue) != exactValue) continue;
            replacement = slot.Clone();
            replacement.count++;
            return i;
        }
        if (empty >= 0) replacement = returned;
        return empty;
    }
}

