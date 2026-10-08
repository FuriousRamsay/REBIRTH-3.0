// Explicit DTO doubles ONLY; actual OriginalCommand source linked. No game/codec ABI claim.
public struct Vector3i {public int x,y,z;}
public struct BlockValue {public uint rawData;public int damage;}
public static class SeedPlacementIntentCodecReview
{ public const int MaxSeedBytes=8192;public struct Intent{public int Slot;public Vector3i Position;public BlockValue Target,ExpectedOld;public byte[] Seed;public byte Flags;public sbyte Density;public long Texture;} }
