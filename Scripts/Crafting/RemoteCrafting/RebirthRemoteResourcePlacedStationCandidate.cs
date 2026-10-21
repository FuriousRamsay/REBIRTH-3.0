using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Noemax.GZip;

// Native serialization only. This scope cannot apply its detached inputs or
// queue to a station, claim payment, or publish a region snapshot.
internal static class RebirthRemoteResourcePlacedStationCandidate
{
    internal const int MaximumTileBytes=8*1024*1024;
    private sealed class Scope
    {internal TileEntityWorkstation Station;internal ItemStack[] Input,Output;internal RecipeQueueItem[] Queue;internal bool Burning;internal int Inputs,Outputs,Queues,Burns,Clocks,ClockOffset=-1;internal PooledBinaryWriter Writer;}
    [ThreadStatic]private static Scope active;
    private static bool installed;
    internal static bool IsInstalled=>installed;
    internal static void Install()
    {
        if(installed)return;
        using(var bytes=File.OpenRead(typeof(DeflateOutputStream).Assembly.Location))using(var sha=System.Security.Cryptography.SHA256.Create())
            if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","")!="9A92198CB65D3B62300713F00B6FCA3DC41A910772C006D8C54EF78B87FF58D1")
                throw new NotSupportedException("Placed native deflate capacity proof is not qualified for this assembly.");
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.remote.resources.placed.candidate.v1"),typeof(RebirthCamperEnergyDetachedStackRead));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.remote.resources.placed.candidate.v1"),typeof(RebirthCamperEnergyDetachedArrayRead));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.remote.resources.placed.candidate.v1"),typeof(NativeWrite));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.remote.resources.placed.candidate.v1"),typeof(CandidateObservationIsolation));
        if(!layoutQualified)throw new InvalidOperationException("Placed native candidate callsites were not qualified.");
        installed=true;
    }
    private static bool layoutQualified;
    internal static long VariableClockPayloadBound(int rawSnapshotBytes)
    {
        if(!installed||rawSnapshotBytes<9||rawSnapshotBytes>RebirthStationSnapshotEvidence.MaximumSnapshotBytes)return long.MaxValue;
        long n=rawSnapshotBytes-8L,blocks=(n+16382)/16383+2;
        // Exact hash-qualified native level3 fast deflate, memoryLevel8:
        // <=9 fixed-Huffman bits/input byte, 16383 tokens/full block,
        // each consuming >=1 byte; dynamic/stored cost is bounded by fixed.
        // Four bytes/block +64 covers block/end/alignment/stored selection
        // rounding and finish overhead.
        // This bounds ANY clock bytes, not the measured zero-clock delta.
        return 8+(9*n+7)/8+4*blocks+64;
    }
    internal static bool TryMeasureChunk(byte[] originalSnapshot,long tileOffset,byte[] originalTile,byte[] candidateTile,out int rawBytes,out int nativePayloadBytes)
        =>TryMeasureChunk(originalSnapshot,tileOffset,originalTile,candidateTile,0,out rawBytes,out nativePayloadBytes,out _);
    internal static bool TryMeasureChunk(byte[] originalSnapshot,long tileOffset,byte[] originalTile,byte[] candidateTile,int exactMandatoryGrowth,out int rawBytes,out int nativePayloadBytes,out long payloadUpperBound)
    {
        rawBytes=nativePayloadBytes=0;payloadUpperBound=long.MaxValue;
        try
        {
            if(exactMandatoryGrowth<0||originalSnapshot==null||originalSnapshot.Length<9||originalSnapshot.Length>RebirthStationSnapshotEvidence.MaximumSnapshotBytes||
                originalTile==null||candidateTile==null||originalTile.Length<1||candidateTile.Length<1||candidateTile.Length>MaximumTileBytes||
                tileOffset<8||tileOffset>originalSnapshot.Length-originalTile.Length||
                originalSnapshot[0]!=116||originalSnapshot[1]!=116||originalSnapshot[2]!=99||originalSnapshot[3]!=0||
                originalSnapshot[4]!=47||originalSnapshot[5]!=0||originalSnapshot[6]!=0||originalSnapshot[7]!=0)return false;
            for(int i=0;i<originalTile.Length;i++)if(originalSnapshot[(int)tileOffset+i]!=originalTile[i])return false;
            long length=(long)originalSnapshot.Length-originalTile.Length+candidateTile.Length;
            long maximumLength=length+exactMandatoryGrowth;
            if(maximumLength>RebirthStationSnapshotEvidence.MaximumSnapshotBytes||length<9)return false;
            long bound=VariableClockPayloadBound((int)maximumLength);
            if(bound>RebirthStationRegionPayload.MaximumPayloadBytes)return false;
            // Native Chunk.save writes TE type followed directly by TE.write,
            // with no enclosing TE length field. All other snapshot bytes stay
            // identical; no native chunk, entity or tile object is fabricated.
            using(var candidate=new MemoryStream((int)length))
            using(var output=new RegionStream())
            {
                candidate.Write(originalSnapshot,0,(int)tileOffset);candidate.Write(candidateTile,0,candidateTile.Length);
                int tail=(int)tileOffset+originalTile.Length;candidate.Write(originalSnapshot,tail,originalSnapshot.Length-tail);
                candidate.Position=8;output.Write(originalSnapshot,0,8);
                // Exact installed RegionFileChunkWriter native compression:
                // unchanged 8-byte header, Noemax deflate level3, Restart.
                byte[] payload;
                using(var zip=new DeflateOutputStream(output,3,leaveOpen:true))
                {StreamUtils.StreamCopy(candidate,zip,new byte[4096]);zip.Restart();payload=output.ToArray();}
                if(!RebirthStationChunkInflate.TryDecode(payload,(int)length-8,out var decoded)||decoded.Length!=length-8)return false;
                byte[] raw=candidate.ToArray();for(int i=0;i<decoded.Length;i++)if(decoded[i]!=raw[i+8])return false;
                rawBytes=(int)length;nativePayloadBytes=payload.Length;payloadUpperBound=bound;return true;
            }
        }
        catch{return false;}
    }
    internal static bool TrySerialize(TileEntityWorkstation station,ItemStack[] plannedInput,RecipeQueueItem[] plannedQueue,out byte[] bytes)
        =>TrySerialize(station,plannedInput,plannedQueue,station?.isBurning??false,out bytes);
    internal static bool TrySerialize(TileEntityWorkstation station,ItemStack[] plannedInput,RecipeQueueItem[] plannedQueue,bool plannedBurning,out byte[] bytes)
        =>TrySerialize(station,plannedInput,plannedQueue,plannedBurning,out bytes,out _);
    internal static bool TrySerialize(TileEntityWorkstation station,ItemStack[] plannedInput,RecipeQueueItem[] plannedQueue,bool plannedBurning,out byte[] bytes,out int clockOffset)
        =>TrySerialize(station,plannedInput,station?.Output,plannedQueue,plannedBurning,out bytes,out clockOffset);
    internal static bool TrySerialize(TileEntityWorkstation station,ItemStack[] plannedInput,ItemStack[] plannedOutput,RecipeQueueItem[] plannedQueue,bool plannedBurning,out byte[] bytes,out int clockOffset)
    {
        bytes=null;clockOffset=-1;
        try
        {
            if(!installed||!ThreadManager.IsMainThread()||active!=null||station==null||station is RebirthCamperStationState||
                plannedInput==null||plannedInput.Length>byte.MaxValue||plannedOutput==null||plannedOutput.Length>byte.MaxValue||plannedQueue==null||plannedQueue.Length<1||plannedQueue.Length>byte.MaxValue||
                plannedInput.Concat(plannedOutput).Any(value=>value==null||value.itemValue==null||value.count<0||value.count>ushort.MaxValue)||plannedQueue.Any(value=>value==null))return false;
            if(!RebirthRemoteResourcePlacedStationQueue.TryDecode(RebirthRemoteResourcePlacedStationQueue.Encode(plannedQueue),out var detachedQueue))return false;
            var scope=new Scope{Station=station,Input=ItemStack.Clone(plannedInput),Output=ItemStack.Clone(plannedOutput),Queue=detachedQueue,Burning=plannedBurning};
            // Full native metadata and recipe wire must be representable BEFORE
            // entering the scope. Detached decoding grants no world authority.
            var inputImage=RebirthCamperInventorySnapshot.Capture(scope.Input);
            if(!inputImage.SequenceEqual(RebirthCamperInventorySnapshot.Capture(RebirthCamperInventorySnapshot.Restore(inputImage))))return false;
            var outputImage=RebirthCamperInventorySnapshot.Capture(scope.Output);
            if(!outputImage.SequenceEqual(RebirthCamperInventorySnapshot.Capture(RebirthCamperInventorySnapshot.Restore(outputImage))))return false;
            using(var stream=new LimitedStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(stream);scope.Writer=writer;active=scope;
                try{station.write(writer,StreamModeWrite.Persistency);writer.Flush();}
                finally{active=null;}
                // Only Persistency's one input and one queue call may consume
                // the scope. Other native write modes never enter this API.
                if(scope.Inputs!=1||scope.Outputs!=1||scope.Queues!=1||scope.Burns!=1||scope.Clocks!=1||scope.ClockOffset<0||scope.ClockOffset>stream.Length-8)return false;
                bytes=stream.ToArray();clockOffset=scope.ClockOffset;return bytes.Length>0;
            }
        }
        catch{bytes=null;return false;}
    }
    private static ItemStack[] Input(TileEntityWorkstation station)
    {
        var scope=active;
        if(scope==null||!ReferenceEquals(scope.Station,station))return station.input;
        if(++scope.Inputs!=1)throw new InvalidDataException("Repeated candidate input call.");
        return scope.Input;
    }
    private static ItemStack[] Output(TileEntityWorkstation station)
    {
        var scope=active;if(scope==null||!ReferenceEquals(scope.Station,station))return station.output;
        if(++scope.Outputs!=1)throw new InvalidDataException("Repeated candidate output call.");return scope.Output;
    }
    private static bool Burning(TileEntityWorkstation station)
    {
        var scope=active;
        if(scope==null||!ReferenceEquals(scope.Station,station))return station.isBurning;
        if(++scope.Burns!=1)throw new InvalidDataException("Repeated candidate burn call.");
        return scope.Burning;
    }
    private static ulong Clock(TileEntityWorkstation station)
    {
        var scope=active;
        if(scope==null||!ReferenceEquals(scope.Station,station))return station.lastTickTime;
        if(++scope.Clocks!=1||scope.Writer?.BaseStream==null)throw new InvalidDataException("Repeated candidate clock call.");
        scope.ClockOffset=checked((int)scope.Writer.BaseStream.Position);return 0;
    }
    internal static bool TryNormalizeClock(byte[] actualNativeTile,int clockOffset,out byte[] normalized,out ulong observedClock)
    {
        normalized=null;observedClock=0;
        if(!installed||actualNativeTile==null||actualNativeTile.Length>MaximumTileBytes||clockOffset<0||clockOffset>actualNativeTile.Length-8)return false;
        normalized=(byte[])actualNativeTile.Clone();
        for(int i=0;i<8;i++){observedClock|=(ulong)normalized[clockOffset+i]<<(8*i);normalized[clockOffset+i]=0;}
        return true; // Comparison only; caller retains actual bytes/clock proof.
    }
    private static void Queue(TileEntityWorkstation station,PooledBinaryWriter writer,int version)
    {
        var scope=active;
        if(scope==null||!ReferenceEquals(scope.Station,station)){station.writeRecipeStackArray(writer,version);return;}
        if(version!=50||++scope.Queues!=1)throw new InvalidDataException("Unsupported candidate queue call.");
        writer.Write((byte)scope.Queue.Length);
        foreach(var row in scope.Queue)row.Write(writer);
    }
    private sealed class LimitedStream:MemoryStream
    {
        public override void Write(byte[] buffer,int offset,int count)
        {if(count<0||Position>MaximumTileBytes-count)throw new InvalidDataException("Placed native tile exceeds candidate bound.");base.Write(buffer,offset,count);}
        public override void WriteByte(byte value)
        {if(Position>=MaximumTileBytes)throw new InvalidDataException("Placed native tile exceeds candidate bound.");base.WriteByte(value);}
    }
    private sealed class RegionStream:MemoryStream
    {
        public override void Write(byte[] buffer,int offset,int count)
        {if(count<0||Position>RebirthStationRegionPayload.MaximumPayloadBytes-count)throw new InvalidDataException("Placed native region sector bound exceeded.");base.Write(buffer,offset,count);}
        public override void WriteByte(byte value)
        {if(Position>=RebirthStationRegionPayload.MaximumPayloadBytes)throw new InvalidDataException("Placed native region sector bound exceeded.");base.WriteByte(value);}
    }
    // A shared save observer may call its read-only Current predicate during
    // an actual snapshot. Our nested candidate must not contaminate that
    // snapshot's span capture or become a publication witness of its own.
    [HarmonyPatch(typeof(RebirthStationSnapshotEvidence),nameof(RebirthStationSnapshotEvidence.StartStationSpan))]
    private static class CandidateObservationIsolation
    {
        private static bool Prefix(object tile,Stream stream,ref long __result)
        {
            var scope=active;
            if(scope==null||!ReferenceEquals(scope.Station,tile)||!ReferenceEquals(scope.Writer?.BaseStream,stream))return true;
            __result=-1;return false;
        }
    }
    [HarmonyPatch(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.write))]
    private static class NativeWrite
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            var code=source.ToList();
            FieldInfo input=AccessTools.Field(typeof(TileEntityWorkstation),"input");
            FieldInfo output=AccessTools.Field(typeof(TileEntityWorkstation),"output");
            FieldInfo burning=AccessTools.Field(typeof(TileEntityWorkstation),"isBurning");
            FieldInfo clock=AccessTools.Field(typeof(TileEntityWorkstation),"lastTickTime");
            MethodInfo queue=AccessTools.Method(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.writeRecipeStackArray),new[]{typeof(PooledBinaryWriter),typeof(int)});
            MethodInfo replaceInput=AccessTools.Method(typeof(RebirthRemoteResourcePlacedStationCandidate),nameof(Input));
            MethodInfo replaceOutput=AccessTools.Method(typeof(RebirthRemoteResourcePlacedStationCandidate),nameof(Output));
            MethodInfo replaceQueue=AccessTools.Method(typeof(RebirthRemoteResourcePlacedStationCandidate),nameof(Queue));
            MethodInfo replaceBurn=AccessTools.Method(typeof(RebirthRemoteResourcePlacedStationCandidate),nameof(Burning));
            MethodInfo replaceClock=AccessTools.Method(typeof(RebirthRemoteResourcePlacedStationCandidate),nameof(Clock));
            int inputs=0,outputs=0,queues=0,burns=0,clocks=0;
            if(input==null||output==null||burning==null||clock==null||queue==null||replaceInput==null||replaceOutput==null||replaceQueue==null||replaceBurn==null||replaceClock==null)throw new MissingMemberException("Placed native candidate layout unavailable.");
            foreach(var instruction in code)
            {
                if(instruction.opcode==OpCodes.Ldfld&&Equals(instruction.operand,input))
                {instruction.opcode=OpCodes.Call;instruction.operand=replaceInput;inputs++;}
                else if(instruction.opcode==OpCodes.Ldfld&&Equals(instruction.operand,output))
                {instruction.opcode=OpCodes.Call;instruction.operand=replaceOutput;outputs++;}
                else if(instruction.opcode==OpCodes.Ldfld&&Equals(instruction.operand,burning))
                {instruction.opcode=OpCodes.Call;instruction.operand=replaceBurn;burns++;}
                else if(instruction.opcode==OpCodes.Ldfld&&Equals(instruction.operand,clock))
                {instruction.opcode=OpCodes.Call;instruction.operand=replaceClock;clocks++;}
                else if((instruction.opcode==OpCodes.Call||instruction.opcode==OpCodes.Callvirt)&&Equals(instruction.operand,queue))
                {instruction.opcode=OpCodes.Call;instruction.operand=replaceQueue;queues++;}
            }
            // Exact installed schema50 has Persistency/ToServer/ToClient,
            // each with one input and one queue write. No partial installation.
            if(inputs!=3||outputs!=3||queues!=3||burns!=3||clocks!=3)throw new InvalidOperationException("Placed native candidate callsite count changed.");
            layoutQualified=true;return code;
        }
    }
}
