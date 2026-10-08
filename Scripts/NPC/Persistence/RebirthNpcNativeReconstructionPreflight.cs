using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// FileVersion38/current-layout preflight. No native Read, world effects or item allocation.
internal static class RebirthNpcNativeReconstructionPreflight
{
    internal static bool TryValidateItemPayload(string payload,int itemsStart,Func<int,bool?> modifierKind)
    {
        if(string.IsNullOrEmpty(payload)||payload.Length>262144||modifierKind==null)return false;
        try{byte[] bytes=Convert.FromBase64String(payload);using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
        {var parser=new Parser(reader,itemsStart,modifierKind);parser.Item(0);return stream.Position==stream.Length;}}
        catch(Exception ex) when(ex is IOException||ex is InvalidDataException||ex is ArgumentException||ex is FormatException||ex is DecoderFallbackException){return false;}
    }
    internal static bool TryValidate(RebirthNpcNativeReconstruction evidence,int itemsStart,byte biomeSource,
        Func<byte,bool> sourceAllowed,Func<int,bool?> modifierKind,Func<int,bool> cosmeticKnown)
    {
        if(evidence==null || sourceAllowed==null || modifierKind==null || cosmeticKnown==null) return false;
        try
        {
            XElement root=evidence.Write();
            foreach(string name in new[]{"actor","bodyDamage","stats","buffs","toolbelt","equipment","bag"})
            {
                byte[] bytes=Convert.FromBase64String((string)root.Element(name).Attribute("payload"));
                using(var stream=new MemoryStream(bytes,false)) using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
                {
                    var parser=new Parser(reader,itemsStart,modifierKind);
                    if(name=="actor")
                    {
                        byte source=reader.ReadByte(); Require(sourceAllowed(source));
                        if(source==biomeSource){reader.ReadInt32();reader.ReadInt64();}
                        reader.ReadUInt64();reader.ReadInt32();parser.Finite();
                    }
                    else if(name=="bodyDamage") {Require(reader.ReadInt32()==4);reader.ReadInt32();reader.ReadUInt32();}
                    else if(name=="stats") {Require(reader.ReadInt32()==11);Require(reader.ReadInt32()==6);for(int i=0;i<5;i++)parser.Finite();}
                    else if(name=="buffs") parser.Buffs();
                    else
                    {
                        Require(reader.ReadByte()==(name=="toolbelt"?1:name=="equipment"?6:2));
                        int count=parser.Grid(name,root.Element("slots"));
                        if(name=="toolbelt")Require(reader.ReadByte()<count);
                        if(name=="equipment")
                        {
                            Require(count==13);
                            for(int i=0;i<13;i++){int id=reader.ReadInt32();Require(id==0||cosmeticKnown(id));}
                            int unlocked=reader.ReadInt32();Require(unlocked>=0&&unlocked<=4096);
                            var ids=new HashSet<int>();for(int i=0;i<unlocked;i++){int id=reader.ReadInt32();Require(cosmeticKnown(id)&&ids.Add(id));}
                        }
                    }
                    Require(stream.Position==stream.Length);
                }
            }
            return true;
        }
        catch(Exception ex) when(ex is IOException || ex is InvalidDataException || ex is ArgumentException || ex is FormatException || ex is OverflowException)
        {return false;}
    }
    private static void Require(bool valid){if(!valid)throw new InvalidDataException("NPC native reconstruction preflight refused.");}
    private sealed class Parser
    {
        private readonly BinaryReader r;private readonly int itemsStart;private readonly Func<int,bool?> modifierKind;private int items;
        internal Parser(BinaryReader reader,int start,Func<int,bool?> kind){r=reader;itemsStart=start;modifierKind=kind;}
        internal void Finite(){float value=r.ReadSingle();Require(!float.IsNaN(value)&&!float.IsInfinity(value));}
        private bool Bool(){byte value=r.ReadByte();Require(value<=1);return value==1;}
        private uint Varint()
        {
            uint value=0;for(int i=0;i<5;i++){byte b=r.ReadByte();if(i==4)Require((b&0xf8)==0);value|=(uint)(b&127)<<(7*i);if((b&128)==0){Require(i==0||(b&127)!=0);return value;}}
            throw new InvalidDataException("NPC native length is invalid.");
        }
        private string Text(int maximum)
        {
            uint length=Varint();Require(length<=maximum&&length<=r.BaseStream.Length-r.BaseStream.Position);
            byte[] bytes=r.ReadBytes((int)length);Require(bytes.Length==length);
            return new UTF8Encoding(false,true).GetString(bytes);
        }
        internal void Buffs()
        {
            Require(r.ReadByte()==3);int count=r.ReadUInt16();Require(count<=128);var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for(int i=0;i<count;i++)
            {
                string name=Text(256);Require(name.Length>0&&names.Add(name));r.ReadByte();r.ReadUInt32();r.ReadInt32();r.ReadByte();r.ReadUInt16();
                r.ReadInt32();r.ReadInt32();r.ReadInt32();
            }
            count=r.ReadUInt16();Require(count<=1024);names.Clear();
            for(int i=0;i<count;i++){string name=Text(256);Require(name.Length>0&&names.Add(name));Finite();}
        }
        internal void Item(int depth)
        {
            Require(depth<=8&&++items<=8192);byte version=r.ReadByte();if(version==0)return;Require(version==9);
            byte flags=r.ReadByte();Require((flags&~3)==0);int id=r.ReadUInt16();if((flags&1)!=0)id=checked(id+itemsStart);
            bool? modifier=modifierKind(id);Require(modifier.HasValue);Finite();r.ReadUInt16();r.ReadUInt16();
            int metadata=r.ReadByte();var keys=new HashSet<string>(StringComparer.Ordinal);
            for(int i=0;i<metadata;i++)
            {
                string key=Text(4096);Require(key.Length>0&&keys.Add(key));int kind=r.ReadInt32();
                if(kind==1)Finite();else if(kind==2)r.ReadInt32();else if(kind==3)Text(65536);else Require(false);
            }
            if((flags&2)!=0){int stats=r.ReadByte();for(int i=0;i<stats;i++){r.ReadByte();r.ReadInt16();r.ReadInt16();}}
            if(!modifier.Value)for(int group=0;group<2;group++){int count=r.ReadByte();for(int i=0;i<count;i++)if(Bool())Item(depth+1);}
            r.ReadByte();r.ReadByte();r.ReadUInt16();if(Bool())r.ReadInt64();
        }
        private void Stack(){int count=r.ReadUInt16();if(count>0)Item(0);}
        internal int Grid(string area,XElement proofs)
        {
            Require(r.ReadUInt16()==2);int x=r.ReadInt32(),y=r.ReadInt32(),count=r.ReadInt16();
            Require(x>=0&&y>=0&&count>=0&&count<=4096&&(long)x*y==count);
            var slots=proofs.Elements("slot").Where(n=>(string)n.Attribute("area")==area).ToDictionary(n=>(int)n.Attribute("index"));
            for(int i=0;i<count;i++)
            {
                int quantity=r.ReadUInt16();long start=r.BaseStream.Position;
                if(quantity>0)
                {
                    Item(0);Require(slots.TryGetValue(i,out var proof)&&(int)proof.Attribute("count")==quantity);
                    long end=r.BaseStream.Position;byte[] exact=Convert.FromBase64String((string)proof.Attribute("payload"));Require(exact.Length==end-start);
                    r.BaseStream.Position=start;Require(r.ReadBytes(exact.Length).SequenceEqual(exact));slots.Remove(i);
                }
                else Require(!slots.ContainsKey(i));
            }
            Require(slots.Count==0);
            if(Bool()){uint length=Varint();Require(length==count);int bytes=(count+7)/8;Require(r.ReadBytes(bytes).Length==bytes);}
            if(Bool())
            {
                r.ReadInt32();
                for(int group=0;group<3;group++)if(Bool())
                {
                    int length=r.ReadUInt16();Require(length<=4096);
                    for(int i=0;i<length;i++)if(group==1){if(Bool())Item(0);}else Stack();
                }
            }
            r.ReadUInt64();Bool();return count;
        }
    }
}
