using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;

// Powered's own version, not base TileEntity's version or peer protocol.
internal sealed class RebirthCamperEnergyMaintenanceReceipt
{
    internal Guid Target,Operation,Vehicle;
    internal string World,Owner,Creation;
    internal Vector3i Position;
    internal uint BlockRaw;
    internal int BeforeDamage,AfterDamage;
    internal bool Applied;
    internal byte[] Encode()
    {
        using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Encoding.UTF8,true))
        {
            w.Write(0x314D4352);w.Write(Target.ToByteArray());w.Write(Operation.ToByteArray());w.Write(Vehicle.ToByteArray());
            Text(w,World);Text(w,Owner);Text(w,Creation);w.Write(Position.x);w.Write(Position.y);w.Write(Position.z);
            w.Write(BlockRaw);w.Write(BeforeDamage);w.Write(AfterDamage);w.Write(Applied);w.Flush();return s.ToArray();
        }
    }
    private static void Text(BinaryWriter w,string value){if(string.IsNullOrEmpty(value)||value.Length>512)throw new InvalidDataException("Maintenance binding invalid.");w.Write(value);}
    internal static RebirthCamperEnergyMaintenanceReceipt Decode(byte[] bytes)
    {
        if(bytes==null||bytes.Length>4096)throw new InvalidDataException("Maintenance receipt bound exceeded.");
        using(var s=new MemoryStream(bytes,false))using(var r=new BinaryReader(s,Encoding.UTF8,true))
        {
            if(r.ReadInt32()!=0x314D4352)throw new InvalidDataException("Maintenance receipt format invalid.");
            var v=new RebirthCamperEnergyMaintenanceReceipt {Target=new Guid(r.ReadBytes(16)),Operation=new Guid(r.ReadBytes(16)),Vehicle=new Guid(r.ReadBytes(16)),
                World=r.ReadString(),Owner=r.ReadString(),Creation=r.ReadString(),Position=new Vector3i(r.ReadInt32(),r.ReadInt32(),r.ReadInt32()),
                BlockRaw=r.ReadUInt32(),BeforeDamage=r.ReadInt32(),AfterDamage=r.ReadInt32(),Applied=r.ReadBoolean()};
            if(s.Position!=s.Length||v.Target==Guid.Empty||v.Operation==Guid.Empty||v.Vehicle==Guid.Empty||v.BeforeDamage<=0||v.AfterDamage<0||v.AfterDamage>=v.BeforeDamage||
                string.IsNullOrEmpty(v.World)||v.World.Length>512||string.IsNullOrEmpty(v.Owner)||v.Owner.Length>512||string.IsNullOrEmpty(v.Creation)||v.Creation.Length>512)
                throw new InvalidDataException("Maintenance receipt fields invalid.");return v;
        }
    }
}

internal static class RebirthCamperEnergyMaintenanceMarkers
{
    private sealed class State {internal RebirthCamperEnergyMaintenanceReceipt Receipt;internal int ReadVersion;internal long Span=-1;internal byte[] Written;}
    private static readonly ConditionalWeakTable<TileEntityPowered,State> states=new ConditionalWeakTable<TileEntityPowered,State>();
    private static bool installed;
    internal static RebirthCamperEnergyMaintenanceReceipt Get(TileEntityPowered te)=>states.TryGetValue(te,out var s)?s.Receipt:null;
    internal static void Set(TileEntityPowered te,RebirthCamperEnergyMaintenanceReceipt receipt){states.GetOrCreateValue(te).Receipt=receipt;te.SetModified();}
    internal static void ResetSpan(TileEntityPowered te){var s=states.GetOrCreateValue(te);s.Span=-1;s.Written=null;}
    internal static bool MatchesSnapshot(TileEntityPowered te,byte[] bytes)
    {
        if(!states.TryGetValue(te,out var s)||s.Written==null||s.Span<8||s.Span>bytes.Length-s.Written.Length)return false;
        for(int i=0;i<s.Written.Length;i++)if(bytes[(int)s.Span+i]!=s.Written[i])return false;
        return true;
    }
    public static int ReadVersion(PooledBinaryReader reader,TileEntityPowered te,StreamModeRead mode)
    {
        int version=reader.ReadInt32();
        if(version<0||version>2||version==2&&(mode!=StreamModeRead.Persistency||!installed))throw new InvalidDataException("Unsupported Powered maintenance version.");
        var s=states.GetOrCreateValue(te);s.ReadVersion=version;
        // A normal peer update must not erase server-only persistent custody.
        if(mode==StreamModeRead.Persistency)s.Receipt=null;
        return version==2?1:version;
    }
    public static void WriteVersion(PooledBinaryWriter writer,int version,TileEntityPowered te,StreamModeWrite mode)
    {writer.Write(mode==StreamModeWrite.Persistency&&Get(te)!=null?2:version);}
    public static void ReadAfter(TileEntityPowered __instance,PooledBinaryReader _br,StreamModeRead _eStreamMode)
    {
        var s=states.GetOrCreateValue(__instance);if(s.ReadVersion!=2)return;
        int length=_br.ReadInt32();if(length<=0||length>4096)throw new InvalidDataException("Maintenance extension bound invalid.");
        var bytes=_br.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();s.Receipt=RebirthCamperEnergyMaintenanceReceipt.Decode(bytes);
    }
    public static void WriteAfter(TileEntityPowered __instance,PooledBinaryWriter _bw,StreamModeWrite _eStreamMode)
    {
        var receipt=Get(__instance);if(_eStreamMode!=StreamModeWrite.Persistency||receipt==null)return;
        var bytes=receipt.Encode();long span=RebirthStationSnapshotEvidence.StartStationSpan(__instance,_bw.BaseStream);
        _bw.Write(bytes.Length);_bw.Write(bytes);
        if(span>=0){var s=states.GetOrCreateValue(__instance);s.Span=span;using(var memory=new MemoryStream())using(var w=new BinaryWriter(memory)){w.Write(bytes.Length);w.Write(bytes);w.Flush();s.Written=memory.ToArray();}}
    }
    internal static bool Install(Harmony harmony)
    {
        if(installed)return true;
        var read=AccessTools.Method(typeof(TileEntityPowered),"read");var write=AccessTools.Method(typeof(TileEntityPowered),"write");
        if(read==null||write==null||!Anchor(PatchProcessor.GetOriginalInstructions(read),true)||!Anchor(PatchProcessor.GetOriginalInstructions(write),false))return false;
        try
        {
            harmony.Patch(read,postfix:new HarmonyMethod(typeof(RebirthCamperEnergyMaintenanceMarkers),nameof(ReadAfter)),transpiler:new HarmonyMethod(typeof(RebirthCamperEnergyMaintenanceMarkers),nameof(ReadPatch)));
            harmony.Patch(write,postfix:new HarmonyMethod(typeof(RebirthCamperEnergyMaintenanceMarkers),nameof(WriteAfter)),transpiler:new HarmonyMethod(typeof(RebirthCamperEnergyMaintenanceMarkers),nameof(WritePatch)));
            installed=true;return true;
        }
        catch
        {
            // Remove these exact four hooks only; retain every other mod's patches.
            harmony.Unpatch(read,AccessTools.Method(typeof(RebirthCamperEnergyMaintenanceMarkers),nameof(ReadAfter)));
            harmony.Unpatch(read,AccessTools.Method(typeof(RebirthCamperEnergyMaintenanceMarkers),nameof(ReadPatch)));
            harmony.Unpatch(write,AccessTools.Method(typeof(RebirthCamperEnergyMaintenanceMarkers),nameof(WriteAfter)));
            harmony.Unpatch(write,AccessTools.Method(typeof(RebirthCamperEnergyMaintenanceMarkers),nameof(WritePatch)));throw;
        }
    }
    private static bool IsAnchor(CodeInstruction i,bool read)=>i.operand is MethodInfo m&&(i.opcode==OpCodes.Call||i.opcode==OpCodes.Callvirt)&&
        (read?m.Name=="ReadInt32"&&m.ReturnType==typeof(int)&&m.GetParameters().Length==0:m.Name=="Write"&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==typeof(int));
    private static bool Anchor(IEnumerable<CodeInstruction> code,bool read)=>code.Count(i=>IsAnchor(i,read))==1;
    public static IEnumerable<CodeInstruction> ReadPatch(IEnumerable<CodeInstruction> code)=>Replace(code,true);
    public static IEnumerable<CodeInstruction> WritePatch(IEnumerable<CodeInstruction> code)=>Replace(code,false);
    private static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> input,bool read)
    {
        var code=input.ToList();if(!Anchor(code,read))throw new InvalidOperationException("Exact Powered maintenance IL anchor changed.");
        foreach(var i in code)
        {
            if(!IsAnchor(i,read)){yield return i;continue;}
            var self=new CodeInstruction(OpCodes.Ldarg_0);self.labels.AddRange(i.labels);self.blocks.AddRange(i.blocks);yield return self;
            yield return new CodeInstruction(OpCodes.Ldarg_2);
            yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(RebirthCamperEnergyMaintenanceMarkers),read?nameof(ReadVersion):nameof(WriteVersion)));
        }
    }
}
