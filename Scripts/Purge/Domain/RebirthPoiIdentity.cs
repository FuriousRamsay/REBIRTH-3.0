using System;
using System.Globalization;

// Native adapters supply canonical manifest geometry, never a player's displayed POI name.
internal sealed class RebirthPoiIdentity : IEquatable<RebirthPoiIdentity>
{
    public readonly string Prefab, Biome, Key;
    public readonly int X, Y, Z, Rotation, SizeX, SizeY, SizeZ;
    public RebirthPoiIdentity(string prefab, int x, int y, int z, int rotation,
        int sizeX, int sizeY, int sizeZ, string biome)
    {
        Prefab = Canonical(prefab, 256); Biome = Canonical(biome, 64);
        if (rotation < 0 || rotation > 3 || sizeX < 1 || sizeX > 4096 ||
            sizeY < 1 || sizeY > 4096 || sizeZ < 1 || sizeZ > 4096)
            throw new ArgumentException("Invalid POI geometry.");
        if (Math.Abs((long)x) > 10000000 || Math.Abs((long)y) > 10000000 || Math.Abs((long)z) > 10000000)
            throw new ArgumentException("POI origin outside supported bounds.");
        X=x; Y=y; Z=z; Rotation=rotation; SizeX=sizeX; SizeY=sizeY; SizeZ=sizeZ;
        // Length-prefixed name prevents delimiter collisions. Biome is metadata, not instance identity.
        Key = Prefab.Length.ToString(CultureInfo.InvariantCulture)+":"+Prefab+":"+
            x.ToString(CultureInfo.InvariantCulture)+":"+y.ToString(CultureInfo.InvariantCulture)+":"+
            z.ToString(CultureInfo.InvariantCulture)+":"+rotation.ToString(CultureInfo.InvariantCulture)+":"+
            sizeX.ToString(CultureInfo.InvariantCulture)+":"+sizeY.ToString(CultureInfo.InvariantCulture)+":"+sizeZ.ToString(CultureInfo.InvariantCulture);
    }
    internal static string Canonical(string value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > limit || value != value.Trim())
            throw new ArgumentException("Invalid canonical identifier.");
                for (int i=0;i<value.Length;i++)
        {
            if (char.IsControl(value[i])) throw new ArgumentException("Control character in identifier.");
            if (char.IsHighSurrogate(value[i]))
            { if(i+1>=value.Length || !char.IsLowSurrogate(value[++i])) throw new ArgumentException("Invalid UTF16 identifier."); }
            else if(char.IsLowSurrogate(value[i])) throw new ArgumentException("Invalid UTF16 identifier.");
        }
        return value.ToLowerInvariant();
    }
    public bool Equals(RebirthPoiIdentity other) { return other != null && Key == other.Key; }
    public override bool Equals(object other) { return Equals(other as RebirthPoiIdentity); }
    public override int GetHashCode() { return StringComparer.Ordinal.GetHashCode(Key); }
}