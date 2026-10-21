using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

// Pure bounded row-owned artifact codec. Neither a marker nor this detached
// artifact authorizes work/output: the coordinator must additionally prove the
// exact native row/tools/incarnation in one positively saved native snapshot.
internal static class RebirthProductiveEarnedRowProof
{
    internal const int MaximumArtifactBytes = 6 * 1024 * 1024;
    internal const int MaximumFrameBytes = 256 * 1024;
    private const uint BodyMagic = 0x32574552; // REW2: exact frozen native ToServer bodies.
    private const uint DefinitionBodyMagic=0x33574552; // REW3: native bodies plus full original authored recipe/output definition.
    private const uint MobileBodyMagic=0x34574552; // REW4: full definition and immutable mobile RCS5 origination digest.
    private static readonly System.Text.UTF8Encoding StrictUtf8=new System.Text.UTF8Encoding(false,true);
    private const uint Magic = 0x31574552; // REW1
    private const uint MarkerMagic = 0x314D4552; // REM1
    internal sealed class Frame
    {
        internal Guid Birth, Operation;
        internal string World;
        internal int RowIndex, Multiplier;
        internal float WorkedSeconds;
        internal byte[] BeforeRow, AfterRow, BeforeTools, AfterTools;
        internal byte[] BeforeNative,AfterNative;
        internal int BeforeElapsedOffset=-1,AfterElapsedOffset=-1;
        internal string FullRecipeBinding,ExtrasDefinition;
        internal byte[] MobileOriginDigest;
        internal bool HasMobileOrigin=>MobileOriginDigest!=null;
        internal bool HasRecipeDefinition=>FullRecipeBinding!=null||ExtrasDefinition!=null;
        internal bool HasNativeBodies=>BeforeNative!=null||AfterNative!=null;
    }
    internal static byte[] Encode(Frame frame)
    {
        Validate(frame);
        int size = EncodedLength(frame);
        using (var stream = new MemoryStream(size))
        using (var writer = new BinaryWriter(stream))
        {
            WriteFrame(writer, frame); writer.Flush();
            if (stream.Length != size || stream.Capacity != size) throw new InvalidDataException("Earned artifact size changed.");
            return stream.GetBuffer(); // Exact private allocation; no second whole-artifact copy.
        }
    }
    private static int EncodedLength(Frame frame)
    {
        long size = 96L + frame.BeforeRow.Length + frame.AfterRow.Length + frame.BeforeTools.Length + frame.AfterTools.Length;
        if (frame.HasNativeBodies) size += 16L + frame.BeforeNative.Length + frame.AfterNative.Length;
        if(frame.HasMobileOrigin)size+=32;
        if(frame.HasRecipeDefinition)size+=8L+StrictUtf8.GetByteCount(frame.FullRecipeBinding)+StrictUtf8.GetByteCount(frame.ExtrasDefinition);
        if (size > MaximumArtifactBytes) throw new InvalidDataException("Earned artifact exceeds domain bound.");
        return checked((int)size);
    }
    private static void WriteFrame(BinaryWriter writer, Frame frame)
    {
        writer.Write(frame.HasMobileOrigin?MobileBodyMagic:frame.HasRecipeDefinition?DefinitionBodyMagic:frame.HasNativeBodies ? BodyMagic : Magic); writer.Write(frame.Birth.ToByteArray());
        writer.Write(frame.Operation.ToByteArray()); writer.Write(Convert.FromBase64String(frame.World));
        writer.Write(frame.RowIndex); writer.Write(frame.Multiplier); writer.Write(frame.WorkedSeconds);
        Write(writer, frame.BeforeRow); Write(writer, frame.AfterRow);
        Write(writer, frame.BeforeTools); Write(writer, frame.AfterTools);
        if (frame.HasNativeBodies)
        {
            writer.Write(frame.BeforeElapsedOffset); writer.Write(frame.AfterElapsedOffset);
            WriteNative(writer, frame.BeforeNative); WriteNative(writer, frame.AfterNative);
        }
        if(frame.HasMobileOrigin)writer.Write(frame.MobileOriginDigest);
        if(frame.HasRecipeDefinition){Write(writer,StrictUtf8.GetBytes(frame.FullRecipeBinding));Write(writer,StrictUtf8.GetBytes(frame.ExtrasDefinition));}
    }
    private static void RequireCanonical(Frame frame, byte[] bytes)
    {
        Validate(frame);
        if (bytes == null || EncodedLength(frame) != bytes.Length) throw new InvalidDataException("Earned artifact mismatch.");
        using (var stream = new ComparingStream(bytes))
        using (var writer = new BinaryWriter(stream))
        {
            WriteFrame(writer, frame); writer.Flush();
            if (stream.Position != bytes.Length) throw new InvalidDataException("Noncanonical earned artifact.");
        }
    }
    // Canonical re-encoding compares incrementally without allocating a second artifact.
    private sealed class ComparingStream : Stream
    {
        private readonly byte[] expected; private int position;
        internal ComparingStream(byte[] value) { expected = value; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => expected.Length;
        public override long Position { get => position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count < 0 || count > expected.Length - position) throw new InvalidDataException("Noncanonical earned artifact.");
            for (int i = 0; i < count; i++) WriteByte(buffer[offset + i]);
        }
        public override void WriteByte(byte value)
        {
            if (position >= expected.Length || expected[position] != value) throw new InvalidDataException("Noncanonical earned artifact.");
            position++;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
    internal static bool MatchesEncoded(Frame frame,byte[] image)
    {try{RequireCanonical(frame,image);return true;}catch{return false;}}
    internal static Frame Decode(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 100 || bytes.Length > MaximumArtifactBytes)
            throw new InvalidDataException("Invalid earned artifact size.");
        using (var stream = new MemoryStream(bytes, false))
        using (var reader = new BinaryReader(stream))
        {
            uint magic=reader.ReadUInt32();
            if(magic!=Magic&&magic!=BodyMagic&&magic!=DefinitionBodyMagic&&magic!=MobileBodyMagic)throw new InvalidDataException("Unsupported earned artifact.");
            var frame = new Frame { Birth = new Guid(Exact(reader,16)), Operation = new Guid(Exact(reader,16)),
                World = Convert.ToBase64String(Exact(reader,32)), RowIndex = reader.ReadInt32(),
                Multiplier = reader.ReadInt32(), WorkedSeconds = reader.ReadSingle(),
                BeforeRow = Read(reader), AfterRow = Read(reader), BeforeTools = Read(reader), AfterTools = Read(reader) };
            if(magic==BodyMagic||magic==DefinitionBodyMagic)
            {
                frame.BeforeElapsedOffset=reader.ReadInt32();frame.AfterElapsedOffset=reader.ReadInt32();
                frame.BeforeNative=ReadNative(reader);frame.AfterNative=ReadNative(reader);
            }
            if(magic==MobileBodyMagic)frame.MobileOriginDigest=Exact(reader,32);
            if(magic==DefinitionBodyMagic||magic==MobileBodyMagic){frame.FullRecipeBinding=StrictUtf8.GetString(Read(reader));frame.ExtrasDefinition=StrictUtf8.GetString(Read(reader));}
            if (stream.Position != stream.Length) throw new InvalidDataException("Trailing earned artifact data.");
            Validate(frame);
            RequireCanonical(frame, bytes);
            return frame;
        }
    }
    internal static byte[] Marker(Frame frame, byte[] artifact)
    {
        // Decode first; callers cannot bind a valid header to different frames.
        RequireCanonical(frame, artifact);
        using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
        using (var sha = SHA256.Create())
        {
            writer.Write(MarkerMagic); writer.Write(frame.Birth.ToByteArray());
            writer.Write(frame.Operation.ToByteArray()); writer.Write(sha.ComputeHash(artifact));
            writer.Flush(); return stream.ToArray();
        }
    }
    internal static bool MatchesMarker(byte[] marker, byte[] artifact)
    {
        try { var frame=Decode(artifact); return marker!=null && marker.SequenceEqual(Marker(frame,artifact)); }
        catch { return false; }
    }
    internal static bool TryReadMarker(byte[] marker,out Guid birth,out Guid operation)
    {
        birth=Guid.Empty;operation=Guid.Empty;
        if(marker==null||marker.Length!=68||BitConverter.ToUInt32(marker,0)!=MarkerMagic)return false;
        var bytes=new byte[16];Array.Copy(marker,4,bytes,0,16);birth=new Guid(bytes);
        Array.Copy(marker,20,bytes,0,16);operation=new Guid(bytes);
        return birth!=Guid.Empty&&operation!=Guid.Empty;
    }
    // Uses the existing bounded native preflight BEFORE native queue/item decoders.
    // This establishes canonical detached frames only, never productive work.
    internal static bool TryDecodeNative(Frame frame, out RecipeQueueItem before, out RecipeQueueItem after,
        out ItemStack[] beforeTools, out ItemStack[] afterTools)
    {
        before=null;after=null;beforeTools=null;afterTools=null;
        try
        {
            Validate(frame);
            if(!RebirthNativeItemConformanceReader.TryDecodeStationQueue(frame.BeforeRow,out var b)||b.Length!=1||
                !RebirthNativeItemConformanceReader.TryDecodeStationQueue(frame.AfterRow,out var a)||a.Length!=1||
                b[0].Multiplier!=frame.Multiplier||a[0].Multiplier!=frame.Multiplier||b[0].Recipe==null||a[0].Recipe==null||
                a[0].CraftingTimeLeft>0||b[0].CraftingTimeLeft==0||
                (b[0].CraftingTimeLeft>0&&a[0].CraftingTimeLeft>=b[0].CraftingTimeLeft)||
                (b[0].CraftingTimeLeft<0&&a[0].CraftingTimeLeft<=b[0].CraftingTimeLeft)||b[0].RepairItem!=null||a[0].RepairItem!=null)return false;
            // Every native field except the final earned timer remains exact.
            float timer=a[0].CraftingTimeLeft;a[0].CraftingTimeLeft=b[0].CraftingTimeLeft;
            bool same=CaptureRow(a[0]).SequenceEqual(frame.BeforeRow);a[0].CraftingTimeLeft=timer;
            if(!same||frame.BeforeTools.Length<2||frame.AfterTools.Length<2||frame.BeforeTools[0]!=1||frame.AfterTools[0]!=1||
                frame.BeforeTools[1]<1||frame.BeforeTools[1]!=frame.AfterTools[1])return false;
            int capacity=frame.BeforeTools[1];
            if(!RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(Convert.ToBase64String(frame.BeforeTools),capacity,MaximumFrameBytes,349528,out beforeTools)||
                !RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(Convert.ToBase64String(frame.AfterTools),capacity,MaximumFrameBytes,349528,out afterTools))return false;
            if(frame.HasNativeBodies&&!RebirthProductiveEarnedNativeBody.ValidPlan(frame))return false;
            before=b[0];after=a[0];return true;
        }
        catch {before=null;after=null;beforeTools=null;afterTools=null;return false;}
    }
    private sealed class BoundedFrameStream : MemoryStream
    {
        public override void Write(byte[] buffer,int offset,int count)
        {
            if(count<0||Position<0||Position>MaximumFrameBytes-count)throw new InvalidDataException("Earned row exceeds native frame bound.");
            base.Write(buffer,offset,count);
        }
        public override void WriteByte(byte value)
        {if(Position>=MaximumFrameBytes)throw new InvalidDataException("Earned row exceeds native frame bound.");base.WriteByte(value);}
        public override void SetLength(long value)
        {if(value<0||value>MaximumFrameBytes)throw new InvalidDataException("Earned row exceeds native frame bound.");base.SetLength(value);}
    }
    internal static byte[] CaptureRow(RecipeQueueItem row)
    {
        if(row==null)throw new ArgumentNullException(nameof(row));
        using(var stream=new BoundedFrameStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            var previous=writer.Encoding;
            try
            {
                writer.Encoding=new System.Text.UTF8Encoding(false,false);writer.SetBaseStream(stream);
                writer.Write((byte)1);row.Write(writer);writer.Flush();
                if(stream.Length>MaximumFrameBytes)throw new InvalidDataException("Earned row frame exceeds bound.");
                var bytes=stream.ToArray();
                if(!RebirthNativeItemConformanceReader.TryDecodeStationQueue(bytes,out var decoded)||decoded.Length!=1)
                    throw new InvalidDataException("Noncanonical earned native row.");
                return bytes;
            }
            finally {writer.Encoding=previous;}
        }
    }
    private static void Validate(Frame frame)
    {
        if (frame == null || frame.Birth == Guid.Empty || frame.Operation == Guid.Empty ||
            frame.RowIndex < 0 || frame.RowIndex >= byte.MaxValue || frame.Multiplier < 1 || frame.Multiplier > short.MaxValue ||
            float.IsNaN(frame.WorkedSeconds) || float.IsInfinity(frame.WorkedSeconds) || frame.WorkedSeconds <= 0)
            throw new InvalidDataException("Invalid earned row identity/work.");
        byte[] world;
        try { world=Convert.FromBase64String(frame.World??""); }
        catch { throw new InvalidDataException("Invalid earned world identity."); }
        if (world.Length!=32 || Convert.ToBase64String(world)!=frame.World) throw new InvalidDataException("Noncanonical earned world identity.");
        Check(frame.BeforeRow); Check(frame.AfterRow); Check(frame.BeforeTools); Check(frame.AfterTools);
        if(frame.HasMobileOrigin&&(frame.MobileOriginDigest.Length!=32||frame.HasNativeBodies||!frame.HasRecipeDefinition))throw new InvalidDataException("Invalid mobile earned origination digest.");
        if(frame.HasRecipeDefinition&&(!(frame.HasNativeBodies||frame.HasMobileOrigin)||string.IsNullOrEmpty(frame.FullRecipeBinding)||string.IsNullOrEmpty(frame.ExtrasDefinition)||
            StrictUtf8.GetByteCount(frame.FullRecipeBinding)>MaximumFrameBytes||StrictUtf8.GetByteCount(frame.ExtrasDefinition)>MaximumFrameBytes))
            throw new InvalidDataException("Invalid full original earned recipe definition.");
        if(frame.HasNativeBodies)
        {
            CheckNative(frame.BeforeNative);CheckNative(frame.AfterNative);
            if(frame.BeforeElapsedOffset<13||frame.BeforeElapsedOffset>frame.BeforeNative.Length-8||
                frame.AfterElapsedOffset<13||frame.AfterElapsedOffset>frame.AfterNative.Length-8)
                throw new InvalidDataException("Invalid native earned clock span.");
        }
        else if(frame.BeforeElapsedOffset!=-1||frame.AfterElapsedOffset!=-1)throw new InvalidDataException("Native clock without body.");
    }
    private static void CheckNative(byte[] bytes)
    {if(bytes==null||bytes.Length<30||bytes.Length>2097088)throw new InvalidDataException("Invalid native earned body length.");}
    private static void WriteNative(BinaryWriter writer,byte[] bytes){CheckNative(bytes);writer.Write(bytes.Length);writer.Write(bytes);}
    private static byte[] ReadNative(BinaryReader reader)
    {
        int length=reader.ReadInt32();if(length<30||length>2097088||length>reader.BaseStream.Length-reader.BaseStream.Position)
            throw new InvalidDataException("Invalid native earned body length.");
        return Exact(reader,length);
    }
    private static void Check(byte[] bytes)
    { if(bytes==null || bytes.Length<1 || bytes.Length>MaximumFrameBytes) throw new InvalidDataException("Invalid earned native frame length."); }
    private static void Write(BinaryWriter writer,byte[] bytes) { Check(bytes);writer.Write(bytes.Length);writer.Write(bytes); }
    private static byte[] Read(BinaryReader reader)
    {
        int length=reader.ReadInt32();
        if(length<1 || length>MaximumFrameBytes || length>reader.BaseStream.Length-reader.BaseStream.Position)
            throw new InvalidDataException("Invalid earned native frame length.");
        return Exact(reader,length);
    }
    private static byte[] Exact(BinaryReader reader,int length)
    {var result=reader.ReadBytes(length);if(result.Length!=length)throw new EndOfStreamException();return result;}
}
