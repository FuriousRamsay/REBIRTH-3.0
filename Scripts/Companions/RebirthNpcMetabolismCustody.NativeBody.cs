using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthNpcMetabolismCustody
{
    private sealed class NpcBodyLoad
    {
        internal EntityRebirthNPC Body;internal World World;internal string Save;internal Stream Stream;
        internal byte[] Payload;internal RebirthNpcStableId Stable;internal bool Returned;internal IDisposable Memory;
    }
    private static readonly Dictionary<EntityRebirthNPC,NpcBodyLoad> nativeBodies=new Dictionary<EntityRebirthNPC,NpcBodyLoad>();
    private static byte[] ReadOriginalBody(XElement node,RebirthNpcStableId stable)
    {
        const int maximumText=((ushort.MaxValue+2)/3)*4;
        if(node?.Name!="nativeBody"||node.Attributes().Count()!=3||node.HasElements||(string)node.Attribute("stable")!=stable.ToString()||
            (int?)node.Attribute("length")<RebirthNpcNativeStableIdentityCodec.Size||(int?)node.Attribute("length")>ushort.MaxValue)throw new InvalidDataException("Original body framing invalid");
        string text=node.Value;if(text.Length<28||text.Length>maximumText)throw new InvalidDataException("Original native body encoded size exceeds bound");
        _=(int)node.Attribute("class");
        var bytes=Convert.FromBase64String(text);
        if(bytes.Length!=(int)node.Attribute("length")||Convert.ToBase64String(bytes)!=text)throw new InvalidDataException("Original complete body changed");
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
        {stream.Position=stream.Length-RebirthNpcNativeStableIdentityCodec.Size;if(RebirthNpcNativeStableIdentityCodec.Read(reader)!=stable||stream.Position!=stream.Length)throw new InvalidDataException("Original native body lineage changed");}
        return bytes;
    }
    private sealed class BoundedBodyStream:MemoryStream
    {
        public override void Write(byte[] bytes,int offset,int count){if(count<0||Position+count>ushort.MaxValue)throw new InvalidDataException("NPC original body exceeds native ECD bound");base.Write(bytes,offset,count);}
        public override void WriteByte(byte value){if(Position>=ushort.MaxValue)throw new InvalidDataException("NPC original body exceeds native ECD bound");base.WriteByte(value);}
    }
    private static XElement CaptureOriginalNpcBody(Original original)
    {
        if(!Current(original)||original.Npc is EntityRebirthDogCompanion)throw new InvalidDataException("Generic NPC body original required");
        // Actual native serialization under the already-reserved original budget.
        // This is a complete historical body checkpoint, not a claim of disk save.
        using var stream=new BoundedBodyStream();
        using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(stream);original.Npc.Write(writer,StreamModeWrite.Persistency);writer.Flush();}
        if(!Current(original)||!original.Npc.NativeIdentityReadable||stream.Length<RebirthNpcNativeStableIdentityCodec.Size)throw new InvalidDataException("NPC body changed during original serialization");
        return new XElement("nativeBody",new XAttribute("class",original.Npc.entityClass),new XAttribute("stable",original.Id.ToString()),new XAttribute("length",stream.Length),Convert.ToBase64String(stream.ToArray()));
    }
    internal static object EnterNpcBodyRead(EntityRebirthNPC body,PooledBinaryReader reader,StreamModeRead mode)
    {
        NpcBodyLoad scope=null;
        try
        {
            if(!ThreadManager.IsMainThread()||mode!=StreamModeRead.Persistency||body==null||body is EntityRebirthDogCompanion||reader?.BaseStream==null)return null;
            var world=GameManager.Instance?.World;if(world==null||world.IsRemote())return null;
            RetireNpcBodyObservations();if(nativeBodies.Count>=64)return null;
            string save=Path.GetFullPath(GameIO.GetSaveGameDir());if(!Directory.Exists(Path.Combine(save,"RebirthData","NpcConsumption")))return null;
            var stream=reader.BaseStream;if(!stream.CanRead||!stream.CanSeek||stream.Position!=0||stream.Length<RebirthNpcNativeStableIdentityCodec.Size||stream.Length>ushort.MaxValue)return null;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(256L*1024,out var memory))return null;
            scope=new NpcBodyLoad{Body=body,World=world,Save=save,Stream=stream,Memory=memory};scope.Payload=new byte[checked((int)stream.Length)];
            try{int offset=0;while(offset<scope.Payload.Length){int read=stream.Read(scope.Payload,offset,scope.Payload.Length-offset);if(read==0)throw new EndOfStreamException();offset+=read;}}
            finally{stream.Position=0;}
            return scope;
        }
        catch{scope?.Memory?.Dispose();return null;}
    }
    internal static void ObserveNpcBodyRead(EntityRebirthNPC body,PooledBinaryReader reader,object state)
    {
        if(state is not NpcBodyLoad scope)return;
        try
        {
            if(!ReferenceEquals(scope.Body,body)||!ReferenceEquals(scope.World,GameManager.Instance?.World)||!ReferenceEquals(reader.BaseStream,scope.Stream)||scope.Stream.Position!=scope.Stream.Length||
                !body.NativeIdentityReadable||body.NativeStableIdentity.IsEmpty||!HasColdHold(body.NativeStableIdentity))return;
            scope.Stable=body.NativeStableIdentity;scope.Stream=null;
            if(nativeBodies.TryGetValue(body,out var previous)){previous.Memory?.Dispose();previous.Payload=null;previous.Body=null;previous.World=null;}
            nativeBodies[body]=scope;scope.Returned=true;
        }
        catch{/* Observation failure never changes native read outcome or issues reentry. */}
        finally{EndNpcBodyRead(state);}
    }
    internal static void EndNpcBodyRead(object state)
    {
        if(state is NpcBodyLoad scope&&!scope.Returned){scope.Memory?.Dispose();scope.Memory=null;scope.Payload=null;scope.Body=null;scope.World=null;scope.Stream=null;}
    }
    private static void RetireNpcBodyObservations()
    {
        var world=GameManager.Instance?.World;
        foreach(var entry in nativeBodies.Where(pair=>!ReferenceEquals(pair.Value.World,world)).ToArray())
        {nativeBodies.Remove(entry.Key);entry.Value.Memory?.Dispose();entry.Value.Memory=null;entry.Value.Payload=null;entry.Value.Body=null;entry.Value.World=null;}
    }
}
