using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using HarmonyLib;

// Read-only final native owner file. The scoped reader records native item
// boundaries so the detached cursor retains TextureFullArray just like the
// established canonical bag/belt reader. No hydrated live value is replaced.
internal static class RebirthCamperEnergySavedOwner
{
    [ThreadStatic] private static Capture current;
    private sealed class StackReferenceComparer:IEqualityComparer<ItemStack>
    {
        public bool Equals(ItemStack a,ItemStack b)=>ReferenceEquals(a,b);
        public int GetHashCode(ItemStack value)=>RuntimeHelpers.GetHashCode(value);
    }
    internal sealed class Capture : IDisposable
    {
        internal readonly Dictionary<ItemStack,ItemStack> Items=new Dictionary<ItemStack,ItemStack>(new StackReferenceComparer());
        internal readonly List<KeyValuePair<ItemStack,ItemStack>> Reads=new List<KeyValuePair<ItemStack,ItemStack>>();
        internal bool Failed;
        internal Capture(){if(current!=null)throw new InvalidOperationException("Nested native owner read.");current=this;}
        public void Dispose(){if(ReferenceEquals(current,this))current=null;}
    }
    internal static long Begin(PooledBinaryReader reader)
    {
        if(current==null)return -1;
        try{return reader.BaseStream.CanSeek?reader.BaseStream.Position:-1;}
        catch{current.Failed=true;return -1;}
    }
    internal static void End(PooledBinaryReader reader,long start,ItemStack stack)
    {
        var capture=current;if(capture==null)return;
        try
        {
            if(start<0||stack?.itemValue==null)throw new InvalidDataException("Native stack boundary absent.");
            var stream=reader.BaseStream;long end=stream.Position;
            if(end-start<2||end-start>196610)throw new InvalidDataException("Native stack boundary invalid.");
            byte[] bytes=new byte[checked((int)(end-start))];
            try
            {
                stream.Position=start;int offset=0,read;
                while(offset<bytes.Length&&(read=stream.Read(bytes,offset,bytes.Length-offset))>0)offset+=read;
                if(offset!=bytes.Length)throw new EndOfStreamException();
            }
            finally{stream.Position=end;}
            int amount=bytes[0]|(bytes[1]<<8);ItemValue value;
            if(amount!=stack.count)throw new InvalidDataException("Native count changed.");
            if(amount==0)
            {
                if(bytes.Length!=2||!stack.itemValue.IsEmpty())throw new InvalidDataException("Native empty changed.");
                value=ItemValue.None.Clone();
            }
            else if(!RebirthNativeItemCodec.TryDecode(Convert.ToBase64String(bytes,2,bytes.Length-2),out value)||
                value==null||value.IsEmpty())throw new InvalidDataException("Native cursor image refused.");
            if(capture.Items.Count>=512||capture.Items.ContainsKey(stack))throw new InvalidDataException("Duplicate or unbounded native stack read.");
            var decoded=new ItemStack(value,amount);capture.Items.Add(stack,decoded);
            capture.Reads.Add(new KeyValuePair<ItemStack,ItemStack>(stack,decoded));
        }
        catch{capture.Failed=true;}
    }
    internal static int BeginArray()=>current==null?-1:current.Reads.Count;
    internal static void EndArray(int start,ItemStack[] array)
    {
        var capture=current;if(capture==null||start<0)return;
        try
        {
            if(array==null||start>capture.Reads.Count||capture.Reads.Count-start!=array.Length)
                throw new InvalidDataException("Native array boundaries changed.");
            for(int i=0;i<array.Length;i++)
            {
                var read=capture.Reads[start+i];var returned=array[i];
                if(ReferenceEquals(read.Key,returned))continue;
                // Exact native GameUtils.ReadItemStack replaces empty type0
                // with a new ItemStack.Empty after ItemStack.Read. Map only a
                // proven raw count0/empty boundary to that detached replacement.
                // Invalid nonempty values collapsed by the native reader never
                // become empty proof and must continue to refuse qualification.
                if(returned?.itemValue==null||returned.count!=0||!returned.itemValue.IsEmpty()||
                    read.Value.count!=0||!read.Value.itemValue.IsEmpty()||capture.Items.ContainsKey(returned))
                    throw new InvalidDataException("Native nonempty replacement refused.");
                capture.Items.Add(returned,read.Value);
            }
        }
        catch{capture.Failed=true;}
    }
    internal static bool TryRead(RebirthStablePlayerIdentity identity,out RebirthCamperEnergyActorImage actor,out PlayerDataFile saved)
        =>TryRead(identity,out actor,out saved,out _);
    internal static bool TryRead(RebirthStablePlayerIdentity identity,out RebirthCamperEnergyActorImage actor,out PlayerDataFile saved,
        out IReadOnlyDictionary<ItemStack,ItemStack> capturedItems)
    {
        actor=null;saved=null;capturedItems=null;
        if(!ThreadManager.IsMainThread()||identity==null)return false;
        try
        {
            ItemStack cursor;
            var items=new Dictionary<ItemStack,ItemStack>(new StackReferenceComparer());
            using(var capture=new Capture())
            {
                if(!RebirthGearNativePlayerFile.TryRead(identity,out saved)||capture.Failed||saved.dragAndDropItem==null||
                    !capture.Items.TryGetValue(saved.dragAndDropItem,out cursor))return false;
                // Keys belong only to this detached native file read. Values are
                // canonical full item clones, never hydrated live inventories.
                foreach(var pair in capture.Items)items.Add(pair.Key,pair.Value.Clone());
            }
                var bag=RebirthGearPlayerDataInventory.ReadSlots(saved,true);
                var belt=RebirthGearPlayerDataInventory.ReadSlots(saved,false);
                if(bag==null||belt==null||saved.bagData==null)return false;
                bool[] locks=null;
                using(var stream=new MemoryStream(saved.bagData.ToArray(),false))
                using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
                {
                    reader.SetBaseStream(stream);if(reader.ReadByte()!=2)return false;
                    var grid=ItemStackGrid.Read(reader,StreamModeRead.Persistency);
                    if(grid==null||grid.Length!=bag.Length||stream.Position!=stream.Length)return false;
                    locks=RebirthCamperEnergyItems.Locks(grid.SlotLocks);
                }
                actor=new RebirthCamperEnergyActorImage {Bag=bag,Belt=belt,Cursor=cursor,BagLocks=locks};
                capturedItems=new System.Collections.ObjectModel.ReadOnlyDictionary<ItemStack,ItemStack>(items);return true;
        }
        catch{actor=null;saved=null;capturedItems=null;return false;}
    }
    internal static bool HasSaved(RebirthStablePlayerIdentity identity,Guid operation,int phase,
        RebirthCamperEnergyActorImage expected)
    {
        if(operation==Guid.Empty||(phase!=1&&phase!=2&&phase!=3)||(expected==null&&phase!=3))return false;
        try
        {
            if(!TryRead(identity,out var actor,out var saved)||(expected!=null&&!expected.Matches(actor))||saved.buffData==null||
                saved.buffData.Length>1024*1024)return false;
            using(var stream=new MemoryStream(saved.buffData.ToArray(),false))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);int version=reader.ReadByte();
                if(version!=EntityBuffs.Version||version<2)return false;
                int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);
                count=reader.ReadUInt16();string key="rbEnergy_"+operation.ToString("N");bool found=false;
                for(int i=0;i<count;i++)
                {
                    string name=reader.ReadString();float value=reader.ReadSingle();
                    if(name!=key)continue;if(found||value!=phase)return false;found=true;
                }
                return found&&stream.Position==stream.Length;
            }
        }
        catch{return false;}
    }
}

[HarmonyPatch(typeof(ItemStack),nameof(ItemStack.Read),typeof(PooledBinaryReader))]
internal static class RebirthCamperEnergyDetachedStackRead
{
    private static void Prefix(PooledBinaryReader _br,out long __state)=>__state=RebirthCamperEnergySavedOwner.Begin(_br);
    private static void Postfix(PooledBinaryReader _br,ItemStack __result,long __state)
    {if(__state>=0)RebirthCamperEnergySavedOwner.End(_br,__state,__result);}
}

[HarmonyPatch(typeof(GameUtils),nameof(GameUtils.ReadItemStack),typeof(PooledBinaryReader))]
internal static class RebirthCamperEnergyDetachedArrayRead
{
    private static void Prefix(out int __state)=>__state=RebirthCamperEnergySavedOwner.BeginArray();
    private static void Postfix(ItemStack[] __result,int __state)
    {if(__state>=0)RebirthCamperEnergySavedOwner.EndArray(__state,__result);}
}
