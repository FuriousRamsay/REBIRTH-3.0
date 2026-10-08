using HarmonyLib;
using System;
using System.Threading;

#nullable disable

/// <summary>
/// Vanilla allocates a new tag array on every <c>FastTags | FastTags</c> even when the result equals one of the operands.
/// <c>BuffClass.ModifyValue</c> runs <c>MinEventContext.Tags |= tags</c> for every active buff on every effect lookup, and the HUD, crosshair and
/// movement code do many lookups per frame, so this alone was ~3.6 MB/s of garbage (about 30% of all allocation in normal play).
///
/// <c>FastTags</c> is a readonly struct and its bit arrays are only ever written while they are being built (never afterwards), so an existing
/// array can safely be shared. When one operand already contains every bit of the other, the union is that operand and no array is needed.
/// The result has the same tag content as the vanilla operator; only the allocation disappears.
///
/// Safety net: on sampled calls the returned set is compared bit by bit with the true union; on any difference the patch disables itself.
/// </summary>
[HarmonyPatch(typeof(FastTags<TagGroup.Global>), "op_BitwiseOr")]
public static class RebirthFastTagsUnionPatch
{
    public static bool Enabled = true;
    private static long calls;
    private static long shared;
    private static bool failureLogged;

    public static long Calls { get { return Interlocked.Read(ref calls); } }
    public static long SharedResults { get { return Interlocked.Read(ref shared); } }

    public static string Status()
    {
        return "[REBIRTH Perf] FastTags union patch enabled=" + Enabled + " calls=" + Calls + " allocation-free=" + SharedResults;
    }

    [HarmonyPrefix]
    public static bool Prefix(FastTags<TagGroup.Global> _a, FastTags<TagGroup.Global> _b, ref FastTags<TagGroup.Global> __result)
    {
        if (!Enabled)
            return true;

        FastTags<TagGroup.Global> result;
        if (Covers(_a, _b))
            result = _a;
        else if (Covers(_b, _a))
            result = _b;
        else
        {
            Interlocked.Increment(ref calls);
            return true;
        }

        long n = Interlocked.Increment(ref calls);
        Interlocked.Increment(ref shared);
        if ((n <= 4000 || (n & 8191) == 0) && !Verify(_a, _b, result))
            return true;
        __result = result;
        return false;
    }

    /// <summary>True when <paramref name="c"/> contains every bit of <paramref name="s"/> (an empty set is contained in everything).</summary>
    private static bool Covers(FastTags<TagGroup.Global> c, FastTags<TagGroup.Global> s)
    {
        if (s.singleBit > 0)
            return HasBit(c, s.singleBit);
        ulong[] sb = s.bits;
        if (sb == null)
            return true;
        for (int i = 0; i < sb.Length; i++)
        {
            ulong w = sb[i];
            if (w == 0UL)
                continue;
            if (c.singleBit > 0)
            {
                if (i != (c.singleBit >> 6) || (w & ~(1UL << c.singleBit)) != 0UL)
                    return false;
            }
            else
            {
                ulong[] cb = c.bits;
                if (cb == null || i >= cb.Length || (w & ~cb[i]) != 0UL)
                    return false;
            }
        }
        return true;
    }

    private static bool HasBit(FastTags<TagGroup.Global> c, int bit)
    {
        if (c.singleBit > 0)
            return c.singleBit == bit;
        ulong[] cb = c.bits;
        if (cb == null)
            return false;
        int index = bit >> 6;
        return index < cb.Length && (cb[index] & (1UL << bit)) != 0UL;
    }

    private static int BitSpan(FastTags<TagGroup.Global> t)
    {
        int span = t.singleBit > 0 ? (t.singleBit >> 6) + 1 : 0;
        if (t.bits != null && t.bits.Length > span)
            span = t.bits.Length;
        return span * 64;
    }

    /// <summary>Checks that <paramref name="result"/> is exactly the union of the operands.</summary>
    private static bool Verify(FastTags<TagGroup.Global> a, FastTags<TagGroup.Global> b, FastTags<TagGroup.Global> result)
    {
        int max = Math.Max(Math.Max(BitSpan(a), BitSpan(b)), BitSpan(result));
        for (int bit = 1; bit < max; bit++)
        {
            bool expected = a.Test_Bit(bit) || b.Test_Bit(bit);
            if (expected == result.Test_Bit(bit))
                continue;
            Enabled = false;
            if (!failureLogged)
            {
                failureLogged = true;
                Log.Error("[REBIRTH Perf] FastTags union shortcut disagreed with the true union at bit " + bit + "; the patch disabled itself.");
            }
            return false;
        }
        return true;
    }
}
