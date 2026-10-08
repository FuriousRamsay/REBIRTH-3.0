using System;
using System.IO;
using System.Xml;
public struct RebirthNpcStableId { public ulong High,Low; public RebirthNpcStableId(ulong high,ulong low){High=high;Low=low;} }
public enum RebirthNpcPresenceState:byte { Active=0 }
public enum RebirthNpcOwnershipKind:byte { None=0 }
public enum RebirthNpcOrderState:byte { None=0 }
public enum RebirthNpcTravelState:byte { None=0 }
public struct Vector3 { public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;} }
public class RebirthNpcRuntimeState
{
    public bool PreparedRestorationPending;
    public const ushort CurrentSchemaVersion=4;
    public RebirthNpcStableId StableId;public string ProfileId,OwnerId="";public RebirthNpcPresenceState Presence;public RebirthNpcOwnershipKind OwnershipKind;
    public RebirthNpcOrderState Order;public RebirthNpcTravelState Travel;public bool HasGuardPosition,HasHumanAppearance;public Vector3 GuardPosition;
    public uint Revision;public RebirthHumanNpcAppearanceDescriptor HumanAppearance;public bool Persisted;
    public RebirthNpcRuntimeState(RebirthNpcStableId id,string profile){StableId=id;ProfileId=profile;}public void MarkPersisted(){Persisted=true;}
}
static class Program
{
    static int n;static void Check(bool ok,string label){if(!ok)throw new Exception(label);n++;}
    static void Reject(Action call,string label){try{call();}catch(Exception e)when(e is IOException||e is InvalidDataException||e is ArgumentException){n++;return;}throw new Exception(label);}
    static byte[] Encode(RebirthHumanNpcAppearanceDescriptor value){using var m=new MemoryStream();using var w=new BinaryWriter(m);RebirthNpcAppearanceBinaryCodec.Write(w,value);return m.ToArray();}
    static void Main()
    {
        Check(RebirthNpcResolvedAppearance.TryCreate(RebirthHumanNpcModelPipeline.SDCS,-1,"base","catalogue","generator","model",true,"race",1,"eye","hair","colour","","","",out var resolved),"qualified domain input");
        var old=new RebirthHumanNpcAppearanceDescriptor(resolved.Pipeline,resolved.LegacySeed,resolved.Archetype);
        Check(old.SchemaVersion==1&&old.Resolved==null,"default legacy layout preserved");
        Check(old.TryWithResolved(resolved,out var value)&&value.SchemaVersion==2&&value.Resolved==resolved,"composed upgrade");
        var input=Encode(value);using(var r=new BinaryReader(new MemoryStream(input)))Check(RebirthNpcAppearanceBinaryCodec.Read(r).Equals(value),"binary resolved roundtrip");
        using(var r=new BinaryReader(new MemoryStream(Encode(old))))Check(RebirthNpcAppearanceBinaryCodec.Read(r).Equals(old),"binary schema1 roundtrip");
        using(var r=new BinaryReader(new MemoryStream(input)))Reject(()=>RebirthNpcAppearanceBinaryCodec.Read(r,false),"old runtime refuses new nested schema");
        input[0]=9;using(var r=new BinaryReader(new MemoryStream(input)))Reject(()=>RebirthNpcAppearanceBinaryCodec.Read(r),"unknown schema");
        using(var r=new BinaryReader(new MemoryStream(new byte[]{255,255,255,255,127})))Reject(()=>RebirthNpcAppearanceBinaryCodec.ReadString(r,5),"huge varint preallocation bound");
        using(var r=new BinaryReader(new MemoryStream(new byte[]{2,255,255})))Reject(()=>RebirthNpcAppearanceBinaryCodec.ReadString(r,5),"strict UTF8");
        using(var r=new BinaryReader(new MemoryStream(new byte[]{5,1})))Reject(()=>RebirthNpcAppearanceBinaryCodec.ReadString(r,5),"truncated string");
        var document=new XmlDocument();document.LoadXml("<record/>");document.DocumentElement.AppendChild(RebirthNpcAppearanceBinaryCodec.WriteAggregate(document,value));
        Check(RebirthNpcAppearanceBinaryCodec.TryReadAggregate(document.DocumentElement,out var saved)&&saved.Value.Equals(value),"actual aggregate helper roundtrip");
        var copy=new XmlDocument();copy.LoadXml(document.OuterXml);Check(RebirthNpcAppearanceBinaryCodec.TryReadAggregate(copy.DocumentElement,out saved)&&saved.Value.Equals(value),"disk text reconstruction");
        copy.DocumentElement.AppendChild(copy.DocumentElement.FirstChild.CloneNode(true));Check(!RebirthNpcAppearanceBinaryCodec.TryReadAggregate(copy.DocumentElement,out _),"duplicate aggregate extension");
        copy.LoadXml("<record/>");Check(RebirthNpcAppearanceBinaryCodec.TryReadAggregate(copy.DocumentElement,out saved)&&saved==null,"format4 old absence unresolved");
        foreach(var field in new[]{"version","required","payload"}){copy.LoadXml(document.OuterXml);((XmlElement)copy.DocumentElement.FirstChild).RemoveAttribute(field);Check(!RebirthNpcAppearanceBinaryCodec.TryReadAggregate(copy.DocumentElement,out _),"missing envelope "+field);}
        copy.LoadXml(document.OuterXml);((XmlElement)copy.DocumentElement.FirstChild).SetAttribute("version","2");Check(!RebirthNpcAppearanceBinaryCodec.TryReadAggregate(copy.DocumentElement,out _),"unknown envelope");
        copy.LoadXml(document.OuterXml);((XmlElement)copy.DocumentElement.FirstChild).SetAttribute("payload",Convert.ToBase64String(Encode(value))+" ");Check(!RebirthNpcAppearanceBinaryCodec.TryReadAggregate(copy.DocumentElement,out _),"noncanonical base64");
        for(ushort version=1;version<=3;version++)
        {
            using var m=new MemoryStream();using(var w=new BinaryWriter(m,System.Text.Encoding.UTF8,true)){w.Write(0x52424E50u);w.Write(version);w.Write(1ul);w.Write(2ul);w.Write("person");w.Write((byte)0);w.Write((byte)0);w.Write("");if(version>=3){w.Write((byte)0);w.Write((byte)0);w.Write(false);}w.Write(5u);if(version>=2){w.Write(true);RebirthNpcAppearanceBinaryCodec.Write(w,old);}w.Write(123456);}
            m.Position=0;using var r=new BinaryReader(m);var state=RebirthNpcPersistenceCodec.Read(r);Check(state.HasHumanAppearance==(version>=2)&&state.HumanAppearance.Resolved==null&&r.ReadInt32()==123456,"old runtime consumes exact layout"+version);
        }
        var current=new RebirthNpcRuntimeState(new RebirthNpcStableId(1,2),"person"){HasHumanAppearance=true,HumanAppearance=value,Revision=8,PreparedRestorationPending=true};
        using(var m=new MemoryStream()){using(var w=new BinaryWriter(m,System.Text.Encoding.UTF8,true)){RebirthNpcPersistenceCodec.Write(w,current);w.Write(654321);}m.Position=0;using var r=new BinaryReader(m);var state=RebirthNpcPersistenceCodec.Read(r);Check(!state.PreparedRestorationPending,"transient hold excluded from actual runtime codec");Check(state.HumanAppearance.Equals(value)&&state.Persisted&&r.ReadInt32()==654321,"runtime4 actual codec exact boundary");}
        Check(value.TryWithResolved(null,out var repeat)&&repeat.Equals(value),"resolved source stays original");
        copy.LoadXml(document.OuterXml.Replace("humanAppearance","HumanAppearance"));Check(!RebirthNpcAppearanceBinaryCodec.TryReadAggregate(copy.DocumentElement,out _),"case altered envelope cannot become old absence");
        using(var r=new BinaryReader(new MemoryStream(new byte[]{128,0})))Reject(()=>RebirthNpcAppearanceBinaryCodec.ReadString(r,5),"noncanonical varint");
        n += PacketFixture.Run();
        n += ResolverFixture.Run();
        n += CommitFixture.Run();
        Console.WriteLine("PASS "+n+" actual appearance/runtime/commit helper, authority resolver, linked resolved domain and packet checks; native/store infrastructure doubled; real helper disk witness covered, full production aggregate writer/model/socket transport not simulated.");
    }
}