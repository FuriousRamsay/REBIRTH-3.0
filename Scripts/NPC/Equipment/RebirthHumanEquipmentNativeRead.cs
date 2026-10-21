using System;
using System.IO;
using System.Reflection;
using HarmonyLib;

// Inventory.ReadInto truncates to the constructor's immutable SlotCount. Human
// loadouts must retain the actual serialized shape (including legacy one-slot saves).
[HarmonyPatch(typeof(Inventory),nameof(Inventory.ReadInto))]
internal static class RebirthHumanEquipmentNativeRead
{
    private static readonly FieldInfo SlotCount=typeof(Inventory).GetField("<SlotCount>k__BackingField",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
    internal static void Install()=>RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.human.equipment.native.read"),typeof(RebirthHumanEquipmentNativeRead));
    [HarmonyPrefix]
    private static void Prefix(Inventory __instance,PooledBinaryReader _br)
    {
        if(__instance?.entity is not EntityRebirthHumanoidNPC)return;
        var stream=_br.BaseStream;if(!stream.CanSeek)throw new InvalidDataException("Human inventory frame must be seekable");
        long position=stream.Position;int count;
        try
        {
            if(_br.ReadByte()!=1||_br.ReadUInt16()!=2)throw new InvalidDataException("Human inventory codec changed");
            var size=StreamUtils.ReadVector2i(_br);count=_br.ReadInt16();
            if(count<1||count>20||size.x<1||size.y<1||(long)size.x*size.y!=count)throw new InvalidDataException("Human inventory frame shape");
        }
        finally{stream.Position=position;}
        if(count==__instance.SlotCount)return;
        // No native resize API exists for this readonly constructor field. The
        // original reader binds Hand after reading; only its truncation bound changes.
        if(SlotCount==null)throw new InvalidDataException("Human inventory shape field unavailable");
        SlotCount.SetValue(__instance,count);
        if(__instance.SlotCount!=count)throw new InvalidDataException("Human inventory shape was not preserved");
    }
}
