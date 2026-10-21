using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

// Explicit invocation only; all values/packets are detached and never ProcessPackage'd.
[Preserve]
public sealed class ConsoleCmdRebirthConsumptionCodecObserve : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
    public override string[] getCommands() => new[] { "rbconsumptioncodecobserve" };
    public override string getDescription() => "Checks consumption publication with the loaded native item codec; no gameplay mutation.";
    public override string getHelp() => "rbconsumptioncodecobserve (requires running Bridge, main thread and loaded foodHoney registry)";
    public override void Execute(List<string> parameters, CommandSenderInfo sender)
    {
        try
        {
            if (parameters.Count != 0 || !RebirthGameBridge.IsRunning || !ThreadManager.IsMainThread())
                throw new InvalidOperationException(getHelp());
            SdtdConsole.Instance.Output(RebirthGameBridgeConsumptionCodec.Run().ToString(Formatting.None));
        }
        catch (Exception error)
        { SdtdConsole.Instance.Output("*** ERROR consumption native codec: " + error.GetType().Name + ": " + error.Message); }
    }
}

internal static class RebirthGameBridgeConsumptionCodec
{
    internal static JObject Run()
    {
        var item = ItemClass.GetItem("foodHoney").Clone();
        if (item == null || item.IsEmpty() || item.ItemClass == null)
            throw new InvalidOperationException("Loaded native foodHoney registry required.");
        item.SetMetadata("rbCodecProbeText", "detached-consumption");
        item.SetMetadata("rbCodecProbeInt", 17);
        item.SetMetadata("rbCodecProbeFloat", 2.5f);
        ItemValue decoded;
        string encoded = RebirthNativeItemCodec.Encode(item.Clone());
        Check(RebirthNativeItemCodec.TryDecode(encoded, out decoded) && decoded.Equals(item), "actual native nonempty codec preserves metadata");
        byte[] final = Write(new NetPackageRebirthMetabolismConsumeResult()
            .Setup(new RebirthConsumeResult { Success = true, Message = "codec-only" })
            .WithInventory(7, RebirthMetabolismSourceKind.Backpack, 0,
                new ItemStack(item.Clone(), 1), ItemStack.Empty.Clone(),0));
        Check(final.SequenceEqual(Write(Read(final))), "count1 to canonical empty packet roundtrip");
        byte[] remaining = Write(new NetPackageRebirthMetabolismConsumeResult()
            .Setup(new RebirthConsumeResult { Success = true, Message = "codec-only" })
            .WithInventory(7, RebirthMetabolismSourceKind.HeldToolbelt, 0,
                new ItemStack(item.Clone(), 2), new ItemStack(item.Clone(), 1),0));
        Check(remaining.SequenceEqual(Write(Read(remaining))), "count2 to count1 packet retains metadata");
        Refused(ReplaceAfterImage(final, encoded), "nonempty item disguised as zero count");
        Refused(ReplaceAfterImage(final, "not-base64"), "malformed empty image");
        byte[] emptyBytes = Convert.FromBase64String(RebirthNativeItemCodec.Encode(ItemStack.Empty.Clone().itemValue));
        Refused(ReplaceAfterImage(final, Convert.ToBase64String(emptyBytes.Concat(new byte[] { 0 }).ToArray())), "empty image with trailing bytes");
        return new JObject { ["schema"] = 1, ["pass"] = true, ["checks"] = 6,
            ["nativeCodec"] = true, ["gameplayMutation"] = false, ["packetProcessed"] = false,
            ["assembly"] = typeof(ItemValue).Assembly.FullName,
            ["nativeAssemblyMvid"] = typeof(ItemValue).Module.ModuleVersionId.ToString(),
            ["modAssemblyMvid"] = typeof(NetPackageRebirthMetabolismConsumeResult).Module.ModuleVersionId.ToString(),
            ["finalServingBytes"] = final.Length, ["remainingServingBytes"] = remaining.Length };
    }
    private static void Check(bool condition, string label)
    { if (!condition) throw new InvalidDataException(label); }
    private static byte[] Write(NetPackageRebirthMetabolismConsumeResult packet)
    {
        using (var stream = new MemoryStream())
        using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            Encoding previous = writer.Encoding;
            try { writer.Encoding = new UTF8Encoding(false, false); writer.SetBaseStream(stream); packet.write(writer); writer.Flush(); return stream.ToArray(); }
            finally { writer.Encoding = previous; }
        }
    }
    private static NetPackageRebirthMetabolismConsumeResult Read(byte[] bytes)
    {
        using (var stream = new MemoryStream(bytes, false))
        using (var reader = MemoryPools.poolBinaryReader.AllocSync(true))
        {
            Encoding previous = reader.Encoding;
            try
            {
                reader.Encoding = new UTF8Encoding(false, false); reader.SetBaseStream(stream);
                // Current native NetPackage.write prefixes UInt16 package id.
                ((BinaryReader)reader).ReadUInt16();
                var packet = new NetPackageRebirthMetabolismConsumeResult(); packet.read(reader);
                Check(stream.Position == stream.Length, "packet did not consume full wire");
                return packet;
            }
            finally { reader.Encoding = previous; }
        }
    }
    private static byte[] ReplaceAfterImage(byte[] wire, string image)
    {
        long offset;
        using (var stream = new MemoryStream(wire, false))
        using (var reader = new BinaryReader(stream, Encoding.UTF8))
        {
            reader.ReadUInt16(); reader.ReadByte(); reader.ReadString(); reader.ReadBoolean(); reader.ReadByte(); reader.ReadSingle(); reader.ReadSingle();
            reader.ReadInt32(); reader.ReadBoolean(); reader.ReadString();
            reader.ReadBytes(16); reader.ReadInt32(); reader.ReadByte(); reader.ReadInt32(); reader.ReadInt32();
            reader.ReadInt32(); reader.ReadString(); reader.ReadInt32(); offset = stream.Position;
        }
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        { writer.Write(wire, 0, checked((int)offset)); writer.Write(image); writer.Flush(); return stream.ToArray(); }
    }
    private static void Refused(byte[] wire, string label)
    {
        try { Read(wire); }
        catch (InvalidDataException) { return; }
        throw new InvalidDataException("Accepted " + label);
    }
}
