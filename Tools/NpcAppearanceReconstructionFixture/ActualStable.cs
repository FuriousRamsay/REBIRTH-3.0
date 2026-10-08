using System; using System.Globalization;public readonly struct RebirthNpcStableId : IEquatable<RebirthNpcStableId>
{
    public readonly ulong High;
    public readonly ulong Low;

    public RebirthNpcStableId(ulong high, ulong low)
    {
        High = high;
        Low = low;
    }

    public bool IsEmpty => High == 0UL && Low == 0UL;

    public static bool TryParse(string value, out RebirthNpcStableId stableId)
    {
        stableId = default(RebirthNpcStableId);
        if (string.IsNullOrWhiteSpace(value)) return false;
        string normalized = value.Trim();
        if (normalized.Length != 32) return false;
        ulong high, low;
        if (!ulong.TryParse(normalized.Substring(0, 16), System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out high) ||
            !ulong.TryParse(normalized.Substring(16, 16), System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out low)) return false;
        stableId = new RebirthNpcStableId(high, low);
        return !stableId.IsEmpty;
    }

    public static RebirthNpcStableId NewId()
    {
        byte[] bytes = Guid.NewGuid().ToByteArray();
        return new RebirthNpcStableId(BitConverter.ToUInt64(bytes, 0), BitConverter.ToUInt64(bytes, 8));
    }

    public bool Equals(RebirthNpcStableId other) => High == other.High && Low == other.Low;
    public override bool Equals(object obj) => obj is RebirthNpcStableId other && Equals(other);
    public override int GetHashCode() => unchecked((High.GetHashCode() * 397) ^ Low.GetHashCode());
    public override string ToString() => High.ToString("x16") + Low.ToString("x16");
    public static bool operator ==(RebirthNpcStableId left, RebirthNpcStableId right) => left.Equals(right);
    public static bool operator !=(RebirthNpcStableId left, RebirthNpcStableId right) => !left.Equals(right);
}

