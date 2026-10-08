using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Compatibility adapter for the V3 Item Magnitude DegradationMax stat.  The current game stores
/// per-item magnitude overrides in ItemValue.Stats; older b259 publicized assemblies did not expose
/// that member.  This adapter therefore discovers the stat container at runtime and changes only
/// the existing DegradationMax stat entry.  It never clears/rebuilds Stats and fails closed when
/// the active game shape cannot be verified.
/// </summary>
public static class RebirthItemConditionStatAdapter
{
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static MemberInfo statsMember;
    private static Type cachedItemValueType;
    private static readonly Dictionary<Type, MemberInfo> TypeMemberCache = new Dictionary<Type, MemberInfo>();
    private static readonly Dictionary<Type, MemberInfo> ValueMemberCache = new Dictionary<Type, MemberInfo>();
    private static readonly Dictionary<Type, int> DegradationIndexCache = new Dictionary<Type, int>();
    private static bool warnedUnavailable;
    private const int FineProbeRadius = 48;
    private static readonly int[] CoarseProbeOffsets = { 64, 96, 128, 160, 192 };

    public static bool TrySetEffectiveMaxUseTimes(ItemValue item, int desiredMax, int upperCap, out int appliedMax, out string reason)
    {
        appliedMax = item != null ? Math.Max(0, item.MaxUseTimes) : 0;
        reason = string.Empty;
        if (item == null || item.ItemClass == null) { reason = "item unavailable"; return false; }
        desiredMax = Math.Max(0, Math.Min(desiredMax, Math.Max(0, upperCap)));
        int current = Math.Max(0, item.MaxUseTimes);
        if (desiredMax <= current) { appliedMax = current; reason = "no upward adjustment required"; return desiredMax == current; }

        Array stats;
        int index;
        MemberInfo valueMember;
        long raw;
        Type rawType;
        if (!TryLocateDegradationStat(item, out stats, out index, out valueMember, out raw, out rawType, out reason))
        {
            WarnOnce(reason);
            return false;
        }

        long minRaw = rawType == typeof(short) ? short.MinValue : rawType == typeof(sbyte) ? sbyte.MinValue : rawType == typeof(byte) ? byte.MinValue : rawType == typeof(ushort) ? ushort.MinValue : int.MinValue;
        long maxRaw = rawType == typeof(short) ? short.MaxValue : rawType == typeof(sbyte) ? sbyte.MaxValue : rawType == typeof(byte) ? byte.MaxValue : rawType == typeof(ushort) ? ushort.MaxValue : int.MaxValue;

        // Probe the actual game formula rather than assuming the Stat.value encoding.  Item
        // magnitude is expected to be monotonic, but quality/other effects may change its scale.
        long[] probes = new long[] { 1, -1, 2, -2, 5, -5, 10, -10, 100, -100, 1000, -1000 };
        double slope = 0.0;
        for (int i = 0; i < probes.Length; i++)
        {
            long candidate = ClampRaw(raw + probes[i], minRaw, maxRaw);
            if (candidate == raw) continue;
            int candidateMax;
            if (!TryEvaluateRaw(item, index, valueMember, candidate, rawType, out candidateMax)) continue;
            if (candidateMax != current)
            {
                slope = (candidateMax - current) / (double)(candidate - raw);
                if (Math.Abs(slope) > 0.000001) break;
            }
        }
        if (Math.Abs(slope) <= 0.000001) { reason = "DegradationMax magnitude stat did not produce a measurable MaxUseTimes response"; WarnOnce(reason); return false; }

        long estimate = ClampRaw(raw + (long)Math.Round((desiredMax - current) / slope), minRaw, maxRaw);
        long bestRaw = raw;
        int bestMax = current;
        int bestDistance = Math.Abs(desiredMax - current);
        EvaluateCandidate(item, index, valueMember, rawType, estimate, current, desiredMax, upperCap, ref bestRaw, ref bestMax, ref bestDistance);
        for (int offset = 1; offset <= FineProbeRadius && bestDistance != 0; offset++)
        {
            EvaluateCandidate(item, index, valueMember, rawType, ClampRaw(estimate - offset, minRaw, maxRaw), current, desiredMax, upperCap, ref bestRaw, ref bestMax, ref bestDistance);
            if (bestDistance == 0) break;
            EvaluateCandidate(item, index, valueMember, rawType, ClampRaw(estimate + offset, minRaw, maxRaw), current, desiredMax, upperCap, ref bestRaw, ref bestMax, ref bestDistance);
        }
        for (int i = 0; i < CoarseProbeOffsets.Length && bestDistance != 0; i++)
        {
            int offset = CoarseProbeOffsets[i];
            EvaluateCandidate(item, index, valueMember, rawType, ClampRaw(estimate - offset, minRaw, maxRaw), current, desiredMax, upperCap, ref bestRaw, ref bestMax, ref bestDistance);
            if (bestDistance == 0) break;
            EvaluateCandidate(item, index, valueMember, rawType, ClampRaw(estimate + offset, minRaw, maxRaw), current, desiredMax, upperCap, ref bestRaw, ref bestMax, ref bestDistance);
        }
        if (bestMax <= current) { reason = "no safe DegradationMax stat value reached the requested condition cap"; return false; }
        if (!TryWriteRaw(item, index, valueMember, bestRaw, rawType)) { reason = "failed to write DegradationMax magnitude stat"; return false; }
        appliedMax = Math.Max(0, item.MaxUseTimes);
        if (appliedMax > upperCap || appliedMax <= current || appliedMax != bestMax)
        {
            // Restore the prior raw value if verification fails.
            TryWriteRaw(item, index, valueMember, raw, rawType);
            appliedMax = Math.Max(0, item.MaxUseTimes);
            reason = "post-write verification rejected unsafe DegradationMax result";
            return false;
        }
        reason = "applied V3 DegradationMax item-magnitude adjustment";
        return true;
    }

    private static void EvaluateCandidate(ItemValue item, int index, MemberInfo valueMember, Type rawType, long candidate, int current, int desiredMax, int upperCap, ref long bestRaw, ref int bestMax, ref int bestDistance)
    {
        int candidateMax;
        if (!TryEvaluateRaw(item, index, valueMember, candidate, rawType, out candidateMax)) return;
        if (candidateMax < current || candidateMax > upperCap) return;
        int distance = Math.Abs(desiredMax - candidateMax);
        if (distance < bestDistance || (distance == bestDistance && candidateMax > bestMax))
        { bestDistance = distance; bestRaw = candidate; bestMax = candidateMax; }
    }

    public static bool HasDegradationMagnitudeStat(ItemValue item)
    {
        Array a; int i; MemberInfo m; long raw; Type t; string reason;
        return TryLocateDegradationStat(item, out a, out i, out m, out raw, out t, out reason);
    }

    private static bool TryEvaluateRaw(ItemValue source, int index, MemberInfo valueMember, long raw, Type rawType, out int max)
    {
        max = 0;
        try
        {
            ItemValue clone = source.Clone();
            if (clone == null || !EnsureProbeIsolation(source, clone) || !TryWriteRaw(clone, index, valueMember, raw, rawType)) return false;
            max = Math.Max(0, clone.MaxUseTimes);
            return true;
        }
        catch { return false; }
    }

    private static bool TryWriteRaw(ItemValue item, int index, MemberInfo knownValueMember, long raw, Type rawType)
    {
        try
        {
            Array stats = GetStats(item);
            if (stats == null || index < 0 || index >= stats.Length) return false;
            object boxed = stats.GetValue(index);
            if (boxed == null) return false;
            MemberInfo member = knownValueMember;
            if (member == null || (member.DeclaringType != null && !member.DeclaringType.IsAssignableFrom(boxed.GetType())))
                member = FindCachedMember(boxed.GetType(), "value", ValueMemberCache);
            if (member == null) return false;
            object converted = Convert.ChangeType(raw, rawType);
            if (!SetMemberValue(member, boxed, converted)) return false;
            stats.SetValue(boxed, index);
            return true;
        }
        catch { return false; }
    }

    private static bool TryLocateDegradationStat(ItemValue item, out Array stats, out int index, out MemberInfo valueMember, out long raw, out Type rawType, out string reason)
    {
        stats = null; index = -1; valueMember = null; raw = 0L; rawType = typeof(short); reason = string.Empty;
        try
        {
            stats = GetStats(item);
            if (stats == null || stats.Length == 0) { reason = "ItemValue.Stats is unavailable or has no magnitude entries"; return false; }
            Type itemType = item.GetType();
            int cachedIndex;
            if (DegradationIndexCache.TryGetValue(itemType, out cachedIndex) && TryReadStatAt(stats, cachedIndex, out valueMember, out raw, out rawType))
            { index = cachedIndex; return true; }
            for (int i = 0; i < stats.Length; i++)
            {
                MemberInfo candidateValue; long candidateRaw; Type candidateRawType;
                if (!TryReadStatAt(stats, i, out candidateValue, out candidateRaw, out candidateRawType)) continue;
                index = i; valueMember = candidateValue; rawType = candidateRawType; raw = candidateRaw;
                DegradationIndexCache[itemType] = i;
                return true;
            }
            reason = "ItemValue.Stats contains no DegradationMax magnitude entry";
            return false;
        }
        catch (Exception ex) { reason = "ItemValue.Stats audit failed: " + ex.GetType().Name; return false; }
    }

    private static bool TryReadStatAt(Array stats, int index, out MemberInfo valueMember, out long raw, out Type rawType)
    {
        valueMember = null; raw = 0L; rawType = typeof(short);
        if (stats == null || index < 0 || index >= stats.Length) return false;
        object boxed = stats.GetValue(index); if (boxed == null) return false;
        Type t = boxed.GetType();
        MemberInfo typeMember = FindCachedMember(t, "type", TypeMemberCache);
        MemberInfo candidateValue = FindCachedMember(t, "value", ValueMemberCache);
        if (typeMember == null || candidateValue == null) return false;
        object typeValue = GetMemberValue(typeMember, boxed);
        if (typeValue == null || Convert.ToInt32(typeValue) != Convert.ToInt32(PassiveEffects.DegradationMax)) return false;
        object rawValue = GetMemberValue(candidateValue, boxed); if (rawValue == null) return false;
        valueMember = candidateValue; rawType = rawValue.GetType(); raw = Convert.ToInt64(rawValue); return true;
    }

    private static MemberInfo FindCachedMember(Type type, string name, Dictionary<Type, MemberInfo> cache)
    {
        MemberInfo member;
        if (type != null && cache.TryGetValue(type, out member)) return member;
        member = FindMember(type, name);
        if (type != null && member != null) cache[type] = member;
        return member;
    }

    private static bool EnsureProbeIsolation(ItemValue source, ItemValue clone)
    {
        Array sourceStats = GetStats(source), cloneStats = GetStats(clone);
        if (sourceStats == null || cloneStats == null) return false;
        Type elementType = sourceStats.GetType().GetElementType();
        if (ReferenceEquals(sourceStats, cloneStats))
        {
            if (elementType == null || !elementType.IsValueType) return false;
            Array isolated = (Array)sourceStats.Clone();
            if (!SetMemberValue(statsMember, clone, isolated)) return false;
            cloneStats = GetStats(clone);
            if (ReferenceEquals(sourceStats, cloneStats)) return false;
        }
        else if (elementType != null && !elementType.IsValueType)
        {
            int count = Math.Min(sourceStats.Length, cloneStats.Length);
            for (int i = 0; i < count; i++)
            {
                object a = sourceStats.GetValue(i), b = cloneStats.GetValue(i);
                if (a != null && ReferenceEquals(a, b)) return false;
            }
        }
        return true;
    }

    private static Array GetStats(ItemValue item)
    {
        if (item == null) return null;
        Type t = item.GetType();
        if (cachedItemValueType != t || statsMember == null)
        {
            cachedItemValueType = t;
            statsMember = FindMember(t, "Stats");
        }
        if (statsMember == null) return null;
        return GetMemberValue(statsMember, item) as Array;
    }

    private static MemberInfo FindMember(Type type, string name)
    {
        for (Type t = type; t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(name, Flags); if (f != null) return f;
            PropertyInfo p = t.GetProperty(name, Flags); if (p != null) return p;
            FieldInfo[] fields = t.GetFields(Flags);
            for (int i = 0; i < fields.Length; i++) if (string.Equals(fields[i].Name, name, StringComparison.OrdinalIgnoreCase)) return fields[i];
            PropertyInfo[] props = t.GetProperties(Flags);
            for (int i = 0; i < props.Length; i++) if (string.Equals(props[i].Name, name, StringComparison.OrdinalIgnoreCase)) return props[i];
        }
        return null;
    }

    private static object GetMemberValue(MemberInfo member, object target)
    {
        FieldInfo f = member as FieldInfo; if (f != null) return f.GetValue(target);
        PropertyInfo p = member as PropertyInfo; return p != null ? p.GetValue(target, null) : null;
    }

    private static bool SetMemberValue(MemberInfo member, object target, object value)
    {
        try
        {
            FieldInfo f = member as FieldInfo; if (f != null) { if (f.IsInitOnly) return false; f.SetValue(target, value); return true; }
            PropertyInfo p = member as PropertyInfo; if (p != null && p.CanWrite) { p.SetValue(target, value, null); return true; }
        }
        catch { }
        return false;
    }

    private static long ClampRaw(long value, long min, long max) { return value < min ? min : value > max ? max : value; }
    private static void WarnOnce(string reason)
    {
        if (warnedUnavailable) return; warnedUnavailable = true;
        Log.Warning("[REBIRTH Repair Signatures] V3 item-magnitude adapter failed closed: " + reason);
    }
}
