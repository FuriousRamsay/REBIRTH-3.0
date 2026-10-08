using System;
using System.IO;
using System.Collections.Generic;
namespace UnityEngine.Scripting { public class PreserveAttribute:Attribute {} }
public enum NetPackageDirection { ToClient }
public class PooledBinaryReader:BinaryReader {public PooledBinaryReader(Stream s):base(s){} }
public class PooledBinaryWriter:BinaryWriter {public PooledBinaryWriter(Stream s):base(s){} }
public class NetPackage { public virtual NetPackageDirection PackageDirection=>NetPackageDirection.ToClient; public virtual void read(PooledBinaryReader r){}public virtual void write(PooledBinaryWriter w){} public virtual void ProcessPackage(World w,GameManager g){} }
public class World { public EntityRebirthHumanoidNPC Npc; public object GetEntity(int id)=>Npc; }
public class GameManager {}
public class ConnectionManager {public bool IsServer;}
public static class SingletonMonoBehaviour<T> where T:new() {public static T Instance=new T();}
public static class RebirthNpcNetworkEpoch {public static uint GetServerEpoch()=>1;public static bool AcceptClientEpoch(uint e)=>e==1;}
public class EntityRebirthHumanoidNPC {public int entityId=7;public RebirthNpcRuntimeState RebirthRuntimeState=new RebirthNpcRuntimeState(new RebirthNpcStableId(1,2),"person");public RebirthHumanNpcAppearanceDescriptor RebirthAppearance;public Equipment equipment;public bool ApplyReplicatedHumanAppearance(RebirthHumanNpcAppearanceDescriptor a,uint rev)=>true;public void RefreshRebirthModelEquipment(){} }
public class ItemValue {public int Value;public bool IsEmpty()=>Value==0;}
public class ItemStack {public ItemValue itemValue;public ItemStack(ItemValue value,int count){itemValue=value;}public static ItemStack Empty=new ItemStack(new ItemValue(),0);public ItemStack Clone()=>new ItemStack(itemValue,1);}
public class Equipment {public ItemStack[] ItemGrid={ItemStack.Empty.Clone(),ItemStack.Empty.Clone()};public List<int> m_unlockedCosmetics=new List<int>();int[] cosmetics={0,0};public int GetSlotCount()=>2;public ItemValue GetSlotItem(int i)=>ItemGrid[i].itemValue;public void SetCosmeticSlot(int i,int value){cosmetics[i]=value;}public int[] GetCosmeticIDs()=>cosmetics;}
public static class GameUtils {public static ItemValue[] ReadItemValueArray(BinaryReader r){int n=r.ReadInt32();var a=new ItemValue[n];for(int i=0;i<n;i++)a[i]=new ItemValue{Value=r.ReadInt32()};return a;}public static void WriteItemValueArray(BinaryWriter w,ItemValue[] a){w.Write(a.Length);foreach(var value in a)w.Write(value.Value);}}
public static class Utils {public static int FastMin(int a,int b)=>Math.Min(a,b);}
public static class RebirthNpcNetworkFraming {public const int MaxUnlockedCosmetics=64;public static int ReadCount(BinaryReader r,int max,string field){int n=r.ReadInt32();if(n<0||n>max)throw new InvalidDataException();return n;}}
static class PacketFixture
{
    static int n;static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);n++;}
    static byte[] Write(RebirthHumanNpcAppearanceDescriptor a,bool equip)
    {var npc=new EntityRebirthHumanoidNPC{RebirthAppearance=a,equipment=equip?new Equipment():null};using var m=new MemoryStream();using var w=new PooledBinaryWriter(m);new NetPackageRebirthNpcAppearanceEquipment().Setup(npc).write(w);w.Write(934567);return m.ToArray();}
    internal static int Run()
    {
        n=0;RebirthNpcResolvedAppearance.TryCreate(RebirthHumanNpcModelPipeline.SDCS,77,"base","cat","gen","Player",true,"race",1,"eye","","","","","",out var resolved);
        var legacy=new RebirthHumanNpcAppearanceDescriptor(resolved.Pipeline,77,"base");legacy.TryWithResolved(resolved,out var appearance);
        foreach(var a in new[]{legacy,appearance})foreach(var equipment in new[]{false,true})
        {using var r=new PooledBinaryReader(new MemoryStream(Write(a,equipment)));new NetPackageRebirthNpcAppearanceEquipment().read(r);Check(r.ReadInt32()==934567,"actual packet preserves equipment boundary schema"+a.SchemaVersion+" equipment"+equipment);}
        var unknown=Write(appearance,false);unknown[28]=3;unknown[29]=0;
        using(var r=new PooledBinaryReader(new MemoryStream(unknown)))
        {bool refused=false;try{new NetPackageRebirthNpcAppearanceEquipment().read(r);}catch(InvalidDataException){refused=true;}Check(refused&&r.BaseStream.Position==30,"unknown schema refuses before pipeline/equipment");}
        using(var m=new MemoryStream())
        {using(var w=new BinaryWriter(m,System.Text.Encoding.UTF8,true)){w.Write(7);w.Write(1ul);w.Write(2ul);w.Write(1u);w.Write(1u);w.Write((ushort)2);w.Write((byte)2);w.Write(77);w.Write("base");w.Write(true);w.Write(new byte[]{255,255,255,255,7});}m.Position=0;using var r=new PooledBinaryReader(m);bool refused=false;try{new NetPackageRebirthNpcAppearanceEquipment().read(r);}catch(InvalidDataException){refused=true;}Check(refused,"actual packet rejects huge payload length before allocation");}
        return n;
    }
}