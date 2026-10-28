using System;
using System.Linq;

// Deferred read-only native-context checks. Supply a SAME published v47 chunk
// snapshot, actual target state and exact original receipt bytes; no writer or
// payment/commit authority is supplied by this fixture.
internal static class RemoteOriginalBlockSnapshotChecks
{
    private static void Require(bool value,string label){if(!value)throw new Exception(label);}
    internal static void Run(byte[] actualPublishedSnapshot,int chunkX,int chunkZ,Vector3i position,uint actualRaw,ushort actualDamage,string receiptKey,byte[] receipt)
    {
        byte[] before=(byte[])actualPublishedSnapshot.Clone();
        Require(RebirthOriginalNativeBlockSnapshot.TryProve(actualPublishedSnapshot,chunkX,chunkZ,position,actualRaw,actualDamage,receiptKey,receipt,out var proof)&&proof?.Length==64,"same native raw/damage/receipt publication");
        Require(actualPublishedSnapshot.SequenceEqual(before),"reader never mutates saved bytes");
        Require(!RebirthOriginalNativeBlockSnapshot.TryProve(actualPublishedSnapshot,chunkX,chunkZ,position,actualRaw,(ushort)(actualDamage^1),receiptKey,receipt,out _),"wrong damage cannot pass raw-only witness");
        Require(!RebirthOriginalNativeBlockSnapshot.TryProve(actualPublishedSnapshot,chunkX,chunkZ,position,actualRaw^1,actualDamage,receiptKey,receipt,out _),"wrong raw state refused");
        Require(!RebirthOriginalNativeBlockSnapshot.TryProve(actualPublishedSnapshot,chunkX,chunkZ,position,actualRaw,actualDamage,receiptKey+":other",receipt,out _),"unrelated original receipt refused");
        Require(!RebirthOriginalNativeBlockSnapshot.TryProve(actualPublishedSnapshot,chunkX+1,chunkZ,position,actualRaw,actualDamage,receiptKey,receipt,out _),"wrong native chunk refused");
        Require(!RebirthOriginalNativeBlockSnapshot.TryProve(actualPublishedSnapshot.Take(28).ToArray(),chunkX,chunkZ,position,actualRaw,actualDamage,receiptKey,receipt,out _),"truncated same header never sufficient");
    }
}
