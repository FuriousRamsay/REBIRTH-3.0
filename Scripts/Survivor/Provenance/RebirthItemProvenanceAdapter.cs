using System;
using System.Text;
using HarmonyLib;

#nullable disable

public sealed class RebirthItemProvenanceSnapshot
{
    public int Version;
    public string Kind = string.Empty;
    public string SourceId = string.Empty;
    public string CreatorStableId = string.Empty;
    public string BackgroundId = string.Empty;
    public string BonusId = string.Empty;
    public string SkillId = string.Empty;
    public float SkillValue;
    public float Workmanship;
    public float OriginalMaxUseTimes;
    public string BatchToken = string.Empty;
}

/// <summary>
/// Item-carried provenance adapter. Uses b259 ItemValue typed metadata so provenance follows the
/// item through inventory/container/drop/network serialization. This foundation stores no effect
/// multiplier and does not alter durability; later domain chunks consume the persisted baseline.
/// </summary>
public static class RebirthItemProvenanceAdapter
{
    public const int CurrentVersion = 2;
    private const int LegacyVersion = 1;
    private const int FloatScale = 100000;
    private const string KVersion = "RebirthProvVersion";
    private const string KKind = "RebirthProvKind";
    private const string KSource = "RebirthProvSource";
    private const string KCreator = "RebirthProvCreator";
    private const string KBackground = "RebirthProvBackground";
    private const string KBonus = "RebirthProvBonus";
    private const string KSkill = "RebirthProvSkill";
    private const string KSkillValue = "RebirthProvSkillValue";
    private const string KWorkmanship = "RebirthProvWorkmanship";
    private const string KOriginalMax = "RebirthProvOriginalMaxUseTimes";
    private const string KBatch = "RebirthProvBatch";
    private const string KDurabilityOriginalMax = "RebirthDurabilityOriginalMaxUseTimes";
    private const int DurabilityScale = 1000;

    public static bool HasProvenance(ItemValue value)
    {
        int version;
        return value != null && value.TryGetMetadata(KVersion, out version) && IsSupportedVersion(version);
    }

    public static bool StampCreated(ItemValue value, EntityPlayer creator, string kind, string sourceId,
        string skillId, float skillValue, float workmanship, string batchToken)
    {
        if (value == null || value.ItemClass == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return false;
        RebirthProvenanceAuthorSnapshot author;
        if (!RebirthProvenanceIdentity.TryCapture(creator, out author)) return false;
        Stamp(value, new RebirthItemProvenanceSnapshot
        {
            Version = CurrentVersion,
            Kind = Clean(kind),
            SourceId = Clean(sourceId),
            CreatorStableId = author.StablePlayerId,
            BackgroundId = author.BackgroundId,
            BonusId = author.BonusId,
            SkillId = Clean(skillId),
            SkillValue = skillValue,
            Workmanship = workmanship,
            OriginalMaxUseTimes = Math.Max(0f, value.MaxUseTimes),
            BatchToken = Clean(batchToken)
        });
        return true;
    }

    public static void Stamp(ItemValue value, RebirthItemProvenanceSnapshot snapshot)
    {
        if (value == null || snapshot == null) return;
        value.SetMetadata(KVersion, CurrentVersion);
        value.SetMetadata(KKind, Clean(snapshot.Kind));
        value.SetMetadata(KSource, Clean(snapshot.SourceId));
        value.SetMetadata(KCreator, Clean(snapshot.CreatorStableId));
        value.SetMetadata(KBackground, Clean(snapshot.BackgroundId));
        value.SetMetadata(KBonus, Clean(snapshot.BonusId));
        value.SetMetadata(KSkill, Clean(snapshot.SkillId));
        value.SetMetadata(KSkillValue, Encode(snapshot.SkillValue));
        value.SetMetadata(KWorkmanship, Encode(snapshot.Workmanship));
        // V2 gives durability its own range/scale. V1 used the generic signed value codec and
        // silently truncated values above 20,000 even though the dedicated durability baseline
        // already supported much larger values.
        value.SetMetadata(KOriginalMax, EncodeDurability(Math.Max(0f, snapshot.OriginalMaxUseTimes)));
        value.SetMetadata(KBatch, Clean(snapshot.BatchToken));
    }

    public static bool TryRead(ItemValue value, out RebirthItemProvenanceSnapshot snapshot)
    {
        snapshot = null;
        if (value == null) return false;
        int version;
        if (!value.TryGetMetadata(KVersion, out version) || !IsSupportedVersion(version)) return false;
        string kind, source, creator, background, bonus, skill, batch;
        int skillValue, workmanship, originalMax;
        value.TryGetMetadata(KKind, out kind); value.TryGetMetadata(KSource, out source);
        value.TryGetMetadata(KCreator, out creator); value.TryGetMetadata(KBackground, out background);
        value.TryGetMetadata(KBonus, out bonus); value.TryGetMetadata(KSkill, out skill);
        value.TryGetMetadata(KBatch, out batch);
        value.TryGetMetadata(KSkillValue, out skillValue); value.TryGetMetadata(KWorkmanship, out workmanship);
        value.TryGetMetadata(KOriginalMax, out originalMax);
        snapshot = new RebirthItemProvenanceSnapshot
        {
            Version = version, Kind = kind ?? string.Empty, SourceId = source ?? string.Empty,
            CreatorStableId = creator ?? string.Empty, BackgroundId = background ?? string.Empty,
            BonusId = bonus ?? string.Empty, SkillId = skill ?? string.Empty,
            SkillValue = Decode(skillValue), Workmanship = Decode(workmanship),
            OriginalMaxUseTimes = version == LegacyVersion
                ? Math.Max(0f, Decode(originalMax))
                : Math.Max(0f, DecodeDurability(originalMax)),
            BatchToken = batch ?? string.Empty
        };
        return true;
    }

    /// <summary>
    /// Applies only REBIRTH provenance-owned metadata from an authoritative correction onto the
    /// caller's current item incarnation. Native use state, mods, cosmetic data and unrelated
    /// metadata remain owned by the current item and are never replaced by a delayed correction.
    /// </summary>
    public static bool TryApplyOwnedCorrection(ItemValue target, ItemValue authoritativeSource)
    {
        if (target == null || authoritativeSource == null) return false;

        int sourceVersion;
        if (!authoritativeSource.TryGetMetadata(KVersion, out sourceVersion) || !IsSupportedVersion(sourceVersion))
            return false;

        int targetVersion;
        bool targetHasVersion = target.TryGetMetadata(KVersion, out targetVersion);
        if (targetHasVersion && !IsSupportedVersion(targetVersion)) return false;

        RebirthItemProvenanceSnapshot source;
        if (!TryRead(authoritativeSource, out source) || source == null) return false;

        // If the item has already acquired a provenance identity, a delayed correction may only
        // refresh the same logical operation. This prevents a recycled type/seed/quality triple
        // from receiving an older craft's provenance.
        RebirthItemProvenanceSnapshot current;
        if (TryRead(target, out current) && current != null)
        {
            if (!Same(current.Kind, source.Kind) || !Same(current.SourceId, source.SourceId) ||
                !Same(current.BatchToken, source.BatchToken)) return false;
        }

        Stamp(target, source);

        int durability;
        if (authoritativeSource.TryGetMetadata(KDurabilityOriginalMax, out durability) && durability > 0)
            target.SetMetadata(KDurabilityOriginalMax, durability);
        return true;
    }


    public static bool TryGetDurabilityOriginalMaxUseTimes(ItemValue value, out float originalMax)
    {
        originalMax = 0f;
        if (value == null) return false;
        RebirthItemProvenanceSnapshot provenance;
        if (TryRead(value, out provenance) && provenance != null && provenance.OriginalMaxUseTimes > 0f)
        {
            originalMax = provenance.OriginalMaxUseTimes;
            return true;
        }
        int encoded;
        if (!value.TryGetMetadata(KDurabilityOriginalMax, out encoded) || encoded <= 0) return false;
        originalMax = (float)encoded / DurabilityScale;
        return originalMax > 0f;
    }

    public static bool EnsureDurabilityOriginalMaxUseTimes(ItemValue value, float originalMax)
    {
        float existing;
        if (value == null || originalMax <= 0f) return false;
        if (TryGetDurabilityOriginalMaxUseTimes(value, out existing) && existing > 0f) return true;
        value.SetMetadata(KDurabilityOriginalMax, EncodeDurability(originalMax));
        return true;
    }

    /// <summary>Server-authoritative correction. Existing crafted provenance remains the upper-bound authority.</summary>
    public static bool ForceDurabilityOriginalMaxUseTimes(ItemValue value, float originalMax)
    {
        if (value == null || originalMax <= 0f) return false;
        RebirthItemProvenanceSnapshot provenance;
        if (TryRead(value, out provenance) && provenance != null && provenance.OriginalMaxUseTimes > 0f) return true;
        value.SetMetadata(KDurabilityOriginalMax, EncodeDurability(originalMax));
        return true;
    }

    public static bool AreStackCompatible(ItemValue a, ItemValue b)
    {
        int rawA = 0, rawB = 0;
        bool hasRawA = a != null && a.TryGetMetadata(KVersion, out rawA);
        bool hasRawB = b != null && b.TryGetMetadata(KVersion, out rawB);
        if ((hasRawA && !IsSupportedVersion(rawA)) || (hasRawB && !IsSupportedVersion(rawB)))
            return false;

        RebirthItemProvenanceSnapshot pa, pb;
        bool ha = TryRead(a, out pa), hb = TryRead(b, out pb);
        if (!ha && !hb)
        {
            float da, db; bool hda=TryGetDurabilityOriginalMaxUseTimes(a,out da), hdb=TryGetDurabilityOriginalMaxUseTimes(b,out db);
            return hda==hdb && (!hda || Math.Abs(da-db)<0.0001f);
        }
        if (ha != hb) return false;
        return Same(pa.Kind,pb.Kind) && Same(pa.SourceId,pb.SourceId) && Same(pa.CreatorStableId,pb.CreatorStableId)
            && Same(pa.BackgroundId,pb.BackgroundId) && Same(pa.BonusId,pb.BonusId) && Same(pa.SkillId,pb.SkillId)
            && Math.Abs(pa.SkillValue-pb.SkillValue) < 0.0001f && Math.Abs(pa.Workmanship-pb.Workmanship) < 0.0001f
            && Math.Abs(pa.OriginalMaxUseTimes-pb.OriginalMaxUseTimes) < 0.0001f && Same(pa.BatchToken,pb.BatchToken);
    }

    public static string BuildDebugSummary(ItemValue value)
    {
        RebirthItemProvenanceSnapshot p;
        if (!TryRead(value, out p))
        {
            float durabilityBaseline;
            return TryGetDurabilityOriginalMaxUseTimes(value,out durabilityBaseline)
                ? "[REBIRTH Provenance Item] none durabilityOriginalMaxUseTimes="+durabilityBaseline.ToString("0.###")
                : "[REBIRTH Provenance Item] none";
        }
        StringBuilder b = new StringBuilder();
        b.Append("[REBIRTH Provenance Item] v=").Append(p.Version).Append(" kind=").Append(p.Kind)
            .Append(" source=").Append(p.SourceId).Append(" creator=").Append(p.CreatorStableId)
            .Append(" background=").Append(p.BackgroundId).Append(" bonus=").Append(p.BonusId)
            .Append(" skill=").Append(p.SkillId).Append("@").Append(p.SkillValue.ToString("0.###"))
            .Append(" workmanship=").Append(p.Workmanship.ToString("0.###"))
            .Append(" originalMaxUseTimes=").Append(p.OriginalMaxUseTimes.ToString("0.###"))
            .Append(" batch=").Append(p.BatchToken);
        return b.ToString();
    }

    private static int EncodeDurability(float value) { return (int)Math.Round(Math.Max(0f, Math.Min(2000000f, value)) * DurabilityScale); }
    private static float DecodeDurability(int value) { return (float)value / DurabilityScale; }
    private static int Encode(float value) { return (int)Math.Round(Math.Max(-20000f, Math.Min(20000f, value)) * FloatScale); }
    private static float Decode(int value) { return (float)value / FloatScale; }
    private static bool IsSupportedVersion(int version) { return version == LegacyVersion || version == CurrentVersion; }
    private static string Clean(string value) { return (value ?? string.Empty).Trim(); }
    private static bool Same(string a, string b) { return string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal); }
}

[HarmonyPatch(typeof(ItemStack), nameof(ItemStack.CanStackWith), new Type[] { typeof(ItemStack), typeof(bool) })]
public static class RebirthProvenanceItemStackPatch
{
    public static void Postfix(ItemStack __instance, ItemStack __0, ref bool __result)
    {
        if (!__result || __instance == null || __0 == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        if (!RebirthItemProvenanceAdapter.AreStackCompatible(__instance.itemValue, __0.itemValue) ||
            !RebirthElectricalItemProvenance.AreStackCompatible(__instance.itemValue, __0.itemValue)) __result = false;
    }
}
