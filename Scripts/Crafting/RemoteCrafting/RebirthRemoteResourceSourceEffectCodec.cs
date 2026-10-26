using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Pure bounded encoding only. A receipt becomes an actual source effect only
// when the original lease stamps it under its one-use native mutation permit
// and the same native persistence owner publishes receipt plus original delta.
internal static class RebirthRemoteResourceSourceEffectCodec
{
    internal const int MaximumReceipts=128;
    internal const string ItemMetadataKey="rebirth.original.source.effects.v2";
    private const uint Magic=0x32455352;
    internal enum Domain:byte{RemoteResource=1,Logistics=2,NpcWork=3,SmartLooting=4,NpcWorkCredit=5,DogSmartLooting=6}
    internal sealed class Effect
    {
        internal Domain Purpose { get; }
        internal Guid Operation { get; }
        internal string Owner { get; }
        internal string Binding { get; }
        internal int Stage { get; }
        internal string Delta { get; }
        internal Effect(Domain purpose,Guid operation,string owner,string binding,int stage,string delta)
        {Purpose=purpose;Operation=operation;Owner=owner;Binding=binding;Stage=stage;Delta=delta;}
    }
    private static bool Hex(string text)=>text?.Length==64&&text.All(character=>character>='0'&&character<='9'||character>='a'&&character<='f');
    private static byte[] Bytes(string hex)
    {if(!Hex(hex))throw new InvalidDataException();var result=new byte[32];for(int i=0;i<result.Length;i++)result[i]=Convert.ToByte(hex.Substring(i*2,2),16);return result;}
    private static string Hex(byte[] bytes)=>BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
    internal static bool TryRead(byte[] image,string incarnation,out Effect[] effects)
    {
        effects=null;
        try
        {
            if(image==null||image.Length<22||image.Length>22+MaximumReceipts*114||!Guid.TryParseExact(incarnation,"N",out var expected)||expected==Guid.Empty)return false;
            using(var stream=new MemoryStream(image,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadUInt32()!=Magic||new Guid(reader.ReadBytes(16))!=expected)return false;int count=reader.ReadUInt16();
                if(count>MaximumReceipts||image.Length!=22+count*114)return false;
                var values=new List<Effect>();var keys=new HashSet<string>(StringComparer.Ordinal);
                for(int i=0;i<count;i++)
                {
                    var domain=(Domain)reader.ReadByte();var operation=new Guid(reader.ReadBytes(16));string owner=Hex(reader.ReadBytes(32)),binding=Hex(reader.ReadBytes(32));int stage=reader.ReadByte();string delta=Hex(reader.ReadBytes(32));
                    if(!Enum.IsDefined(typeof(Domain),domain)||operation==Guid.Empty||stage<1||stage>3||!keys.Add((byte)domain+"|"+operation.ToString("N")))return false;
                    values.Add(new Effect(domain,operation,owner,binding,stage,delta));
                }
                effects=values.ToArray();return stream.Position==stream.Length;
            }
        }
        catch{return false;}
    }
    internal static byte[] Empty(string incarnation)=>Encode(incarnation,Array.Empty<Effect>());
    private static byte[] Encode(string incarnation,IReadOnlyList<Effect> effects)
    {
        if(!Guid.TryParseExact(incarnation,"N",out var id)||id==Guid.Empty||effects==null||effects.Count>MaximumReceipts)throw new InvalidDataException();
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write(Magic);writer.Write(id.ToByteArray());writer.Write((ushort)effects.Count);
            foreach(var effect in effects)
            {
                if(effect==null||!Enum.IsDefined(typeof(Domain),effect.Purpose)||effect.Operation==Guid.Empty||effect.Stage<1||effect.Stage>3)throw new InvalidDataException();
                writer.Write((byte)effect.Purpose);writer.Write(effect.Operation.ToByteArray());writer.Write(Bytes(effect.Owner));writer.Write(Bytes(effect.Binding));writer.Write((byte)effect.Stage);writer.Write(Bytes(effect.Delta));
            }
            writer.Flush();return stream.ToArray();
        }
    }
    internal static bool TryPlan(byte[] before,string incarnation,Domain purpose,Guid operation,string owner,string binding,int stage,string delta,out byte[] after)
    {
        after=null;
        try
        {
            if(!Enum.IsDefined(typeof(Domain),purpose)||operation==Guid.Empty||!Hex(owner)||!Hex(binding)||!Hex(delta)||stage<1||stage>3||!TryRead(before,incarnation,out var entries))return false;
            var existing=entries.SingleOrDefault(effect=>effect.Purpose==purpose&&effect.Operation==operation);
            if(existing!=null)
            {
                if(existing.Owner!=owner||existing.Binding!=binding)return false;
                if(existing.Stage==stage){if(existing.Delta!=delta)return false;after=(byte[])before.Clone();return true;}
                if(existing.Stage!=1||stage!=2&&stage!=3)return false;
            }
            else if(stage!=1||entries.Length>=MaximumReceipts)return false;
            var values=entries.Where(effect=>effect.Purpose!=purpose||effect.Operation!=operation).ToList();values.Add(new Effect(purpose,operation,owner,binding,stage,delta));
            after=Encode(incarnation,values.OrderBy(effect=>(byte)effect.Purpose).ThenBy(effect=>effect.Operation.ToString("N"),StringComparer.Ordinal).ToArray());return true;
        }
        catch{return false;}
    }
    internal static bool TryRetire(byte[] before,string incarnation,Func<Effect,bool> savedTerminal,out byte[] after)
    {
        after=null;try{if(savedTerminal==null||!TryRead(before,incarnation,out var entries))return false;after=Encode(incarnation,entries.Where(value=>!savedTerminal(value)).ToArray());return true;}catch{return false;}
    }
}
