using System.IO;

#nullable disable

/// <summary>
/// Compatibility helpers for 3.0 serialization.
/// 
/// 3.0's pooled binary reader/writer surface exposes span-based overloads. In this project/reference
/// setup, direct calls such as PooledBinaryWriter.Write(int) can trigger CS7069 ReadOnlySpan metadata
/// resolution errors. These helpers force primitive serialization through the framework BinaryWriter
/// and BinaryReader surfaces, which avoids span overload resolution entirely.
/// </summary>
public static class AdvancedFarmingBinaryCompat
{
    public static void WriteUShort(PooledBinaryWriter writer, ushort value)
    {
        ((BinaryWriter)(object)writer).Write(value);
    }

    public static void WriteInt(PooledBinaryWriter writer, int value)
    {
        ((BinaryWriter)(object)writer).Write(value);
    }

    public static void WriteULong(PooledBinaryWriter writer, ulong value)
    {
        ((BinaryWriter)(object)writer).Write(value);
    }

    public static void WriteBool(PooledBinaryWriter writer, bool value)
    {
        ((BinaryWriter)(object)writer).Write(value);
    }

    public static int ReadInt(PooledBinaryReader reader)
    {
        return ((BinaryReader)(object)reader).ReadInt32();
    }

    public static ulong ReadULong(PooledBinaryReader reader)
    {
        return ((BinaryReader)(object)reader).ReadUInt64();
    }

    public static bool ReadBool(PooledBinaryReader reader)
    {
        return ((BinaryReader)(object)reader).ReadBoolean();
    }
}
