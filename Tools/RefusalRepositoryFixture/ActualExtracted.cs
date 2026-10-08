using System;using System.Collections.Generic;using System.Globalization;using System.Linq;using System.Xml.Linq;public sealed class RebirthTraitSupportRuntimeState
{
    public string SupportProfileId;
    public float GraceRemainingActiveSeconds;
    public float ManagedRemainingActiveSeconds;
    public float PositiveRemainingActiveSeconds;
    public float CooldownRemainingActiveSeconds;
    public int Stacks;
    public RebirthTraitSupportRuntimeState Clone()
    {
        return new RebirthTraitSupportRuntimeState
        {
            SupportProfileId = SupportProfileId,
            GraceRemainingActiveSeconds = GraceRemainingActiveSeconds,
            ManagedRemainingActiveSeconds = ManagedRemainingActiveSeconds,
            PositiveRemainingActiveSeconds = PositiveRemainingActiveSeconds,
            CooldownRemainingActiveSeconds = CooldownRemainingActiveSeconds,
            Stacks = Stacks
        };
    }
}

public sealed class RebirthWorldSupportState
{
    // Ordered cassette ownership is separate from the bounded equipment projection.
    public bool MusicShuffle = true;
    public long GearRevision;
    public RebirthGearTransferPhase GearTransferPhase;
    public RebirthGearTransferState PendingGearTransfer;
    public RebirthBackpackLibraryReceipt PendingLibraryTransfer;
    public RebirthBackpackLibraryPhase LibraryTransferPhase;
    public RebirthGearSettlement LastGearSettlement;
    public RebirthGearPreparationRefusal PendingGearPreparationRefusal;
    public RebirthGearPreparationRefusal LastGearPreparationRefusal;
    public RebirthGearTransferState LastGearSettlementOriginal;
    public RebirthBackpackLibrarySettlement LastLibrarySettlement;
    internal System.Xml.Linq.XElement RemoteResourceRefundJournalImage;
    public long MusicRevision;
    public RebirthMusicTransferState PendingMusicTransfer;
    public readonly List<RebirthMusicCassetteState> MusicCassettes = new List<RebirthMusicCassetteState>();
    public long AudiobookRevision;
    public readonly List<RebirthAudiobookCassetteState> AudiobookCassettes = new List<RebirthAudiobookCassetteState>();
    public readonly Dictionary<string, RebirthTraitSupportRuntimeState> Entries = new Dictionary<string, RebirthTraitSupportRuntimeState>(StringComparer.OrdinalIgnoreCase);
    // Lightweight REBIRTH-owned wearable/utility slots. Values are stable item IDs, not native armor/equipment objects.
    public readonly Dictionary<string, string> EquippedGearBySlot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    // Exact serialized ItemValue for equipped gear. ItemId remains the lightweight
    // projection used by rules/UI; this preserves quality, durability, seed, mods and metadata.
    public readonly Dictionary<string, string> EquippedGearItemDataBySlot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public RebirthWorldSupportState Clone()
    {
        RebirthWorldSupportState copy = new RebirthWorldSupportState();
        copy.MusicShuffle = MusicShuffle;
        copy.GearRevision = GearRevision;
        copy.GearTransferPhase = GearTransferPhase;
        copy.PendingGearTransfer = PendingGearTransfer; // Immutable payload, safe to share.
        copy.PendingGearPreparationRefusal = PendingGearPreparationRefusal; // Immutable unprepared original refusal.
        copy.LastGearPreparationRefusal = LastGearPreparationRefusal; // Immutable retired original acknowledgment.
        copy.LastGearSettlement = LastGearSettlement; // Immutable durable outcome.
        copy.LastGearSettlementOriginal = LastGearSettlementOriginal; // Immutable original, never permission to replay.
        copy.PendingLibraryTransfer=PendingLibraryTransfer; // Immutable receipt.
        copy.LibraryTransferPhase=LibraryTransferPhase;
        copy.LastLibrarySettlement=LastLibrarySettlement; // Immutable durable outcome.
        copy.RemoteResourceRefundJournalImage=RemoteResourceRefundJournalImage==null?null:new System.Xml.Linq.XElement(RemoteResourceRefundJournalImage);
        copy.MusicRevision = MusicRevision;
        copy.AudiobookRevision = AudiobookRevision;
        foreach(var cassette in AudiobookCassettes)
            if(cassette != null)copy.AudiobookCassettes.Add(cassette.Clone());
        copy.PendingMusicTransfer = PendingMusicTransfer?.Clone();
        foreach (var cassette in MusicCassettes)
            if (cassette != null) copy.MusicCassettes.Add(new RebirthMusicCassetteState { ItemId = cassette.ItemId, ItemData = cassette.ItemData });
        foreach (KeyValuePair<string, RebirthTraitSupportRuntimeState> pair in Entries) if (pair.Value != null) copy.Entries[pair.Key] = pair.Value.Clone();
        foreach (KeyValuePair<string, string> pair in EquippedGearBySlot) copy.EquippedGearBySlot[pair.Key] = pair.Value ?? string.Empty;
        foreach (KeyValuePair<string, string> pair in EquippedGearItemDataBySlot) copy.EquippedGearItemDataBySlot[pair.Key] = pair.Value ?? string.Empty;
        return copy;
    }
}

public sealed class RebirthMusicCassetteState
{
    public string ItemId = string.Empty;
    public string ItemData = string.Empty;
}

public sealed class RebirthGearInventorySnapshot
{
    public readonly RebirthGearInventoryPlan.Stack[] Bag;
    public readonly RebirthGearInventoryPlan.Stack[] Belt;
    public readonly int OwnedBeltSlots;

    private RebirthGearInventorySnapshot(RebirthGearInventoryPlan.Stack[] bag,
        RebirthGearInventoryPlan.Stack[] belt, int ownedBeltSlots)
    {
        Bag = bag; Belt = belt; OwnedBeltSlots = ownedBeltSlots;
    }

    // Detached reconstruction only. This factory grants no authority or writes.
    public static bool TryCaptureEncoded(RebirthGearInventoryPlan.Stack[] bag,
        RebirthGearInventoryPlan.Stack[] belt,int owned,out RebirthGearInventorySnapshot snapshot)
    {
        snapshot=null;
        if(!RebirthGearEncodedSnapshot.TryCopy(bag,belt,owned,out var b,out var t))return false;
        snapshot=new RebirthGearInventorySnapshot(b,t,owned);return true;
    }
    public bool IsUsableSource(bool isBag, int index)
    {
        return index >= 0 && index < (isBag ? Bag.Length : OwnedBeltSlots);
    }

public static bool TryCapture(object bag,object belt,int owned,out RebirthGearInventorySnapshot s){s=ServerDoubles.Image;return ServerDoubles.Upload;}}public static partial class Program {    private static XElement SerializeSupport(RebirthWorldSupportState state,string ownerKey,string creation)
    {
        state = state ?? new RebirthWorldSupportState();
        XElement node = new XElement("support");
        XElement gear = new XElement("gear");
        List<string> gearSlots = new List<string>(state.EquippedGearBySlot.Keys); gearSlots.Sort(StringComparer.Ordinal);
        for (int i = 0; i < gearSlots.Count; i++)
        {
            string slotId = gearSlots[i];
            string itemId = state.EquippedGearBySlot[slotId] ?? string.Empty;
            if (string.IsNullOrEmpty(slotId) || string.IsNullOrEmpty(itemId)) continue;
            string itemData; state.EquippedGearItemDataBySlot.TryGetValue(slotId,out itemData);
            XElement gearSlot=new XElement("slot", new XAttribute("id", slotId), new XAttribute("itemId", itemId));
            if(!string.IsNullOrEmpty(itemData))gearSlot.Add(new XAttribute("itemData",itemData));
            gear.Add(gearSlot);
        }
        node.Add(gear);
        node.Add(RebirthGearTransferPersistence.Write(state.GearRevision, state.PendingGearTransfer, state.GearTransferPhase));
        if(state.PendingLibraryTransfer!=null&&(state.PendingGearTransfer!=null||state.PendingMusicTransfer!=null))throw new InvalidOperationException("Competing library custody.");
        node.Add(RebirthBackpackLibraryPersistence.Write(state.GearRevision,state.PendingLibraryTransfer,state.LibraryTransferPhase));
        if(state.PendingGearPreparationRefusal!=null)
        {
            if(!state.PendingGearPreparationRefusal.MatchesSupport(creation,state.GearRevision,
                state.PendingGearTransfer!=null||state.PendingLibraryTransfer!=null||state.PendingMusicTransfer!=null,
                state.LastGearSettlement?.TransactionId))throw new InvalidOperationException("Competing or invalid unprepared gear refusal.");
            node.Add(state.PendingGearPreparationRefusal.Write());
        }
        if(state.LastGearPreparationRefusal!=null)
        {
            if(!RebirthGearPreparationRefusalRetirement.MatchesSupport(state.LastGearPreparationRefusal,creation,state.GearRevision,
                state.PendingGearPreparationRefusal,state.LastGearSettlement?.TransactionId))throw new InvalidOperationException("Invalid retired gear refusal.");
            node.Add(RebirthGearPreparationRefusalRetirement.Write(state.LastGearPreparationRefusal));
        }
        if(state.LastGearSettlement!=null)node.Add(state.LastGearSettlement.Write());
        var terminalOriginal=RebirthGearTerminalOriginalPersistence.Write(state.LastGearSettlement,state.LastGearSettlementOriginal);
        if(terminalOriginal!=null)node.Add(terminalOriginal);
        if(state.LastLibrarySettlement!=null)node.Add(state.LastLibrarySettlement.Write());
        node.Add(RemoteResourceRefundSupportPersistence.Write(state.RemoteResourceRefundJournalImage,ownerKey,creation));
        XElement music = new XElement("music", new XAttribute("shuffle", state.MusicShuffle), new XAttribute("revision", state.MusicRevision));
        if (state.PendingMusicTransfer != null) music.Add(state.PendingMusicTransfer.ToXml());
        foreach (var cassette in state.MusicCassettes)
            if (cassette != null && !string.IsNullOrEmpty(cassette.ItemId))
                music.Add(new XElement("cassette", new XAttribute("itemId", cassette.ItemId), new XAttribute("itemData", cassette.ItemData ?? string.Empty)));
        node.Add(music);
        node.Add(RebirthAudiobookLibraryPersistence.Write(state.AudiobookRevision,state.AudiobookCassettes));
        List<string> ids = new List<string>(state.Entries.Keys); ids.Sort(StringComparer.Ordinal);
        for (int i = 0; i < ids.Count; i++)
        {
            RebirthTraitSupportRuntimeState s = state.Entries[ids[i]]; if (s == null) continue;
            node.Add(new XElement("entry", new XAttribute("profileId", s.SupportProfileId ?? ids[i]),
                new XAttribute("graceRemainingActiveSeconds", F(s.GraceRemainingActiveSeconds)),
                new XAttribute("managedRemainingActiveSeconds", F(s.ManagedRemainingActiveSeconds)),
                new XAttribute("positiveRemainingActiveSeconds", F(s.PositiveRemainingActiveSeconds)),
                new XAttribute("cooldownRemainingActiveSeconds", F(s.CooldownRemainingActiveSeconds)),
                new XAttribute("stacks", Math.Max(0, s.Stacks))));
        }
        return node;
    }

    private static bool TryDeserializeSupport(XElement node,string ownerKey,string creation, out RebirthWorldSupportState state, out string error)
    {
        state=new RebirthWorldSupportState();error=string.Empty;
        if(!RemoteResourceRefundSupportPersistence.TryRead(node,ownerKey,creation,out state.RemoteResourceRefundJournalImage))
        {error="Invalid remote resource refund custody; refusing to discard retained items.";return false;}
        if(node==null || node.Elements("gearTransfers").Count()!=1
            || !RebirthGearTransferPersistence.TryRead(node.Element("gearTransfers"),out state.GearRevision,out state.PendingGearTransfer,out state.GearTransferPhase))
        { error="Invalid gear transfer custody section; refusing to discard pending items.";return false; }
        System.Collections.Generic.List<RebirthAudiobookCassetteState> audioEntries;
        if(node.Elements("audiobooks").Count()>1 ||
            !RebirthAudiobookLibraryPersistence.TryRead(node.Element("audiobooks"),out state.AudiobookRevision,out audioEntries))
        { error="Invalid audiobook custody; refusing to discard stored tapes.";return false; }
        state.AudiobookCassettes.AddRange(audioEntries);
        XElement music=node.Element("music");
        if(music!=null)
        {
            if (!RebirthMusicTransferState.TryRead(music.Element("pendingTransfer"), out state.PendingMusicTransfer))
            { error = "Invalid pending music transfer; refusing to discard item custody."; return false; }
            long revision;
            if(long.TryParse(A(music,"revision"),out revision))state.MusicRevision=Math.Max(0,revision);
            bool shuffle;
            if(bool.TryParse(A(music,"shuffle"),out shuffle))state.MusicShuffle=shuffle;
            foreach(var cassette in music.Elements("cassette"))
            {
                string itemId=A(cassette,"itemId");
                if(!string.IsNullOrEmpty(itemId))state.MusicCassettes.Add(new RebirthMusicCassetteState { ItemId=itemId, ItemData=A(cassette,"itemData") });
            }
        }
        if(!RebirthGearSettlement.TryRead(node,state.GearRevision,out state.LastGearSettlement)||
            state.LastGearSettlement!=null&&state.PendingGearTransfer!=null&&
            (state.LastGearSettlement.TransactionId==state.PendingGearTransfer.TransactionId||
             state.LastGearSettlement.CreationId!=state.PendingGearTransfer.CreationId||
             state.LastGearSettlement.GearRevision>state.PendingGearTransfer.ExpectedRevision))
        {error="Invalid terminal gear settlement; refusing to discard custody.";return false;}
        if(!RebirthGearTerminalOriginalPersistence.TryRead(node,state.LastGearSettlement,out state.LastGearSettlementOriginal))
            {error="Invalid retained original gear settlement.";return false;}
        if(!RebirthBackpackLibrarySettlement.TryRead(node,state.GearRevision,out state.LastLibrarySettlement)
            ||!RebirthBackpackLibraryPersistence.TryRead(node,state.GearRevision,out state.PendingLibraryTransfer,out state.LibraryTransferPhase)
            ||state.PendingLibraryTransfer!=null&&(state.PendingGearTransfer!=null||state.PendingMusicTransfer!=null)
            ||state.LastLibrarySettlement!=null&&state.PendingLibraryTransfer!=null&&
                (state.LastLibrarySettlement.TransactionId==state.PendingLibraryTransfer.TransactionId||
                 state.LastLibrarySettlement.CreationId!=state.PendingLibraryTransfer.CreationId||
                 state.LastLibrarySettlement.GearRevision>state.PendingLibraryTransfer.ExpectedGearRevision))
        {error="Invalid or competing library custody; refusing to discard pending items.";return false;}
        XElement gear=node.Element("gear");if(gear!=null)foreach(XElement x in gear.Elements("slot")){string slot=A(x,"id");string item=A(x,"itemId");if(string.IsNullOrEmpty(slot)||string.IsNullOrEmpty(item))continue;state.EquippedGearBySlot[slot]=item;string itemData=A(x,"itemData");if(!string.IsNullOrEmpty(itemData))state.EquippedGearItemDataBySlot[slot]=itemData;}
        if(!RebirthGearPreparationRefusal.TryRead(node,out state.PendingGearPreparationRefusal)||
            state.PendingGearPreparationRefusal!=null&&!state.PendingGearPreparationRefusal.MatchesSupport(creation,state.GearRevision,
                state.PendingGearTransfer!=null||state.PendingLibraryTransfer!=null||state.PendingMusicTransfer!=null,
                state.LastGearSettlement?.TransactionId))
        {error="Invalid or competing unprepared gear refusal; retaining original custody.";return false;}
        if(!RebirthGearPreparationRefusalRetirement.TryRead(node,out state.LastGearPreparationRefusal)||
            state.LastGearPreparationRefusal!=null&&!RebirthGearPreparationRefusalRetirement.MatchesSupport(state.LastGearPreparationRefusal,
                creation,state.GearRevision,state.PendingGearPreparationRefusal,state.LastGearSettlement?.TransactionId))
        {error="Invalid retired gear refusal acknowledgment.";return false;}
        foreach(XElement x in node.Elements("entry")){float g,m,p,c;int stacks;if(!TryFloat(x,"graceRemainingActiveSeconds",out g)||!TryFloat(x,"managedRemainingActiveSeconds",out m)||!TryFloat(x,"positiveRemainingActiveSeconds",out p)||!TryFloat(x,"cooldownRemainingActiveSeconds",out c))continue;if(!int.TryParse(A(x,"stacks"),NumberStyles.Integer,CultureInfo.InvariantCulture,out stacks))stacks=0;string id=A(x,"profileId");if(string.IsNullOrEmpty(id))continue;state.Entries[id]=new RebirthTraitSupportRuntimeState{SupportProfileId=id,GraceRemainingActiveSeconds=Math.Max(0f,g),ManagedRemainingActiveSeconds=Math.Max(0f,m),PositiveRemainingActiveSeconds=Math.Max(0f,p),CooldownRemainingActiveSeconds=Math.Max(0f,c),Stacks=Math.Max(0,stacks)};}return true;
    }

    internal static bool HasSavedUnpreparedGearBase(RebirthStablePlayerIdentity identity,RebirthWorldCharacterRecord expected,string marker)
    {
        if(!serverAuthority||identity==null||expected?.Support==null||expected.Origin==null||!expected.IsComplete)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryGetCurrentCached(identity,out var current)||!ReferenceEquals(current,expected)||
                    !TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Support==null||!saved.IsComplete||saved.Origin?.CreationId!=expected.Origin.CreationId)return false;
                foreach(var support in new[]{saved.Support,expected.Support})
                    if(support.PendingGearTransfer!=null||support.PendingLibraryTransfer!=null||support.PendingMusicTransfer!=null||
                        support.PendingGearPreparationRefusal!=null&&!support.PendingGearPreparationRefusal.MatchesOriginal(marker))return false;
                var before=saved.Support.Clone();var after=expected.Support.Clone();
                before.PendingGearPreparationRefusal=null;after.PendingGearPreparationRefusal=null;
                return XNode.DeepEquals(SerializeSupport(before,identity.StorageKey,expected.Origin.CreationId),
                    SerializeSupport(after,identity.StorageKey,expected.Origin.CreationId));
            }
            catch{return false;}
        }
    }
    internal static bool HasSavedGearPreparationRefusal(RebirthStablePlayerIdentity identity,RebirthGearPreparationRefusal expected)
    {
        if(!serverAuthority||identity==null||expected==null)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved==null||!saved.IsComplete||saved.Support?.PendingGearPreparationRefusal==null||
                    !RebirthSurvivorRequestScope.Matches(expected.CreationId,saved.Origin?.CreationId)||
                    saved.Support.GearRevision!=expected.ObservedRevision||
                    !XNode.DeepEquals(saved.Support.PendingGearPreparationRefusal.Write(),expected.Write())||
                    !TryGetCurrentCached(identity,out var current)||current==null||!current.IsComplete||
                    !RebirthSurvivorRequestScope.Matches(expected.CreationId,current.Origin?.CreationId)||
                    !ReferenceEquals(current.Support?.PendingGearPreparationRefusal,expected)||
                    current.Support.GearRevision!=expected.ObservedRevision)return false;
                return XNode.DeepEquals(SerializeSupport(saved.Support,identity.StorageKey,expected.CreationId),
                    SerializeSupport(current.Support,identity.StorageKey,expected.CreationId));
            }
            catch{return false;}
        }
    }
    // Retry base permits ONLY the known pending-to-retired refusal metadata transition.
    internal static bool HasSavedGearRefusalRetirementBase(RebirthStablePlayerIdentity identity,RebirthWorldCharacterRecord expected,RebirthGearPreparationRefusal refusal)
    {
        if(!serverAuthority||identity==null||expected==null||refusal==null)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryGetCurrentCached(identity,out var current)||!ReferenceEquals(current,expected)||!current.IsComplete||
                    !TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||saved==null||!saved.IsComplete||
                    !RebirthSurvivorRequestScope.Matches(refusal.CreationId,saved.Origin?.CreationId)||
                    !RebirthSurvivorRequestScope.Matches(refusal.CreationId,current.Origin?.CreationId))return false;
                foreach(var support in new[]{saved.Support,current.Support})
                {
                    if(support==null||support.GearRevision!=refusal.ObservedRevision||support.PendingGearTransfer!=null||
                        support.PendingLibraryTransfer!=null||support.PendingMusicTransfer!=null)return false;
                    var original=support.PendingGearPreparationRefusal??support.LastGearPreparationRefusal;
                    if(original==null||!XNode.DeepEquals(original.Write(),refusal.Write()))return false;
                }
                var before=saved.Support.Clone();var after=current.Support.Clone();
                before.PendingGearPreparationRefusal=null;before.LastGearPreparationRefusal=null;
                after.PendingGearPreparationRefusal=null;after.LastGearPreparationRefusal=null;
                return XNode.DeepEquals(SerializeSupport(before,identity.StorageKey,refusal.CreationId),
                    SerializeSupport(after,identity.StorageKey,refusal.CreationId));
            }
            catch{return false;}
        }
    }
    internal static bool HasSavedGearPreparationRefusalRetirement(RebirthStablePlayerIdentity identity,RebirthGearPreparationRefusal expected)
    {
        if(!serverAuthority||identity==null||expected==null)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved==null||!saved.IsComplete||saved.Support?.LastGearPreparationRefusal==null||
                    !RebirthSurvivorRequestScope.Matches(expected.CreationId,saved.Origin?.CreationId)||
                    saved.Support.GearRevision<expected.ObservedRevision||
                    !XNode.DeepEquals(saved.Support.LastGearPreparationRefusal.Write(),expected.Write())||
                    !TryGetCurrentCached(identity,out var current)||current==null||!current.IsComplete||
                    !RebirthSurvivorRequestScope.Matches(expected.CreationId,current.Origin?.CreationId)||
                    !ReferenceEquals(current.Support?.LastGearPreparationRefusal,expected)||
                    current.Support.GearRevision<expected.ObservedRevision||
                    current.Support.PendingGearPreparationRefusal?.TransactionId==expected.TransactionId)return false;
                return XNode.DeepEquals(SerializeSupport(saved.Support,identity.StorageKey,expected.CreationId),
                    SerializeSupport(current.Support,identity.StorageKey,expected.CreationId));
            }
            catch{return false;}
        }
    }
    private static string A(XElement e,string n){if(e==null)return string.Empty;XAttribute a=e.Attribute(n);return a!=null?a.Value:string.Empty;}
    private static bool TryFloat(XElement e,string n,out float v){return float.TryParse(A(e,n),NumberStyles.Float,CultureInfo.InvariantCulture,out v);}
    private static string F(float v){return v.ToString("R",CultureInfo.InvariantCulture);}
}