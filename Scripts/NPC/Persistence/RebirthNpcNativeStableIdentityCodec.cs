using System;
using System.IO;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;

// Versioned native lineage only: no ownership, inventory or incarnation authority.
// The fixed 21-byte extension precedes the existing dog RBD1 extension.
internal static class RebirthNpcNativeStableIdentityCodec
{
    internal const int Magic=0x52424931;
    internal const int Size=21;
    private const string HarmonyId="rebirth.npc.native-identity.bound.v1";
    private static readonly HashSet<int> NativeNpcClasses=new HashSet<int>();
    internal static void ObserveNativeClass(EntityRebirthNPC actor)
    {
        if(actor==null)throw new ArgumentNullException(nameof(actor));
        lock(NativeNpcClasses)NativeNpcClasses.Add(actor.entityClass);
    }
    private static readonly MethodInfo NativeWrite=typeof(EntityCreationData).GetMethod("write",new[]{typeof(PooledBinaryWriter),typeof(StreamModeWrite)});
    private static readonly MethodInfo BoundPrefix=typeof(NativeWriteBound).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic);
    internal static bool NativeGuardInstalled
    {
        get
        {
            if(NativeWrite==null||BoundPrefix==null)return false;
            var patches=Harmony.GetPatchInfo(NativeWrite);if(patches==null)return false;
            foreach(var patch in patches.Prefixes)if(patch.owner==HarmonyId&&patch.PatchMethod==BoundPrefix)return true;
            return false;
        }
    }
    internal static void InstallNativeGuard()
    {
        if(NativeWrite==null||BoundPrefix==null)throw new InvalidOperationException("Exact native NPC ECD writer guard unavailable");
        var harmony=new Harmony(HarmonyId);
        if(!NativeGuardInstalled)harmony.CreateClassProcessor(typeof(NativeWriteBound)).Patch();
        RebirthNpcRetiredNativeLineage.Install(harmony);
        if(!NativeGuardInstalled||!RebirthNpcRetiredNativeLineage.GuardsInstalled)throw new InvalidOperationException("Native NPC ECD writer guard installation not verified");
    }
    [HarmonyPatch(typeof(EntityCreationData),"write")]
    private static class NativeWriteBound
    {
        private static void Prefix(EntityCreationData __instance)
        {
            // Positively observed actual CLR NPC initialization establishes an
            // exact native EntityClass hash. The ECD id may legitimately be -1
            // after cloning; ID/coordinate guesses never classify this guard.
            if(__instance==null)return;
            lock(NativeNpcClasses)if(!NativeNpcClasses.Contains(__instance.entityClass))return;
            if(__instance.entityData==null)throw new InvalidDataException("Native NPC entityData unavailable");
            RequireNativeEntityDataLength(__instance.entityData.Length);
        }
    }
    internal static void RequireNativeEntityDataLength(long length)
    {
        // Exact installed EntityCreationData.write uses an unchecked UInt16
        // length followed by the COMPLETE byte vector. Never permit wrapping.
        if(length<0||length>ushort.MaxValue)throw new InvalidDataException("NPC native entityData exceeds UInt16 framing; full original retained");
    }
    internal static RebirthNpcStableId Read(BinaryReader reader)
    {
        if(reader?.BaseStream==null||!reader.BaseStream.CanSeek)throw new InvalidDataException("Bounded seekable native identity stream required");
        var stream=reader.BaseStream;long start=stream.Position,remaining=stream.Length-start;
        if(remaining<4)return default;
        int magic=reader.ReadInt32();
        if(magic!=Magic){stream.Position=start;return default;}
        if(remaining<Size)throw new InvalidDataException("Truncated native NPC identity extension");
        if(reader.ReadByte()!=1)throw new InvalidDataException("Unsupported native NPC identity extension");
        var stable=new RebirthNpcStableId(reader.ReadUInt64(),reader.ReadUInt64());
        if(stable.IsEmpty)throw new InvalidDataException("Empty native NPC identity extension");
        return stable;
    }
    internal static void Write(BinaryWriter writer,RebirthNpcStableId stable)
    {
        if(writer==null||stable.IsEmpty)throw new InvalidDataException("Native NPC identity unavailable");
        writer.Write(Magic);writer.Write((byte)1);writer.Write(stable.High);writer.Write(stable.Low);
    }
}
