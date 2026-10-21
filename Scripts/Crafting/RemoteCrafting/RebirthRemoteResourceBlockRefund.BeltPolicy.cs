using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourceBlockRefund
{
    internal static XElement BeltPolicy(int width,int owned,bool[] locks)
    {
        if(width<1||width>20||owned<0||owned>width||locks?.Length!=width)throw new InvalidDataException();
        return new XElement("beltPolicy",new XAttribute("version",1),new XAttribute("width",width),new XAttribute("owned",owned),new string(locks.Select(value=>value?'1':'0').ToArray()));
    }
    internal static bool ReadBeltPolicy(XElement policy,out int width,out int owned,out bool[] locks)
    {
        width=owned=0;locks=null;
        try
        {
            if(policy?.Name!="beltPolicy"||(int?)policy.Attribute("version")!=1||policy.Attributes().Count()!=3||policy.Nodes().Any(node=>node is not XText))return false;
            width=(int)policy.Attribute("width");owned=(int)policy.Attribute("owned");string text=policy.Value;
            if(width<1||width>20||owned<0||owned>width||text.Length!=width||text.Any(value=>value!='0'&&value!='1'))return false;
            locks=text.Select(value=>value=='1').ToArray();return XNode.DeepEquals(policy,BeltPolicy(width,owned,locks));
        }
        catch{width=owned=0;locks=null;return false;}
    }
    private static XElement CaptureBeltPolicy(EntityPlayerLocal player)=>BeltPolicy(player.inventory.ItemGrid.Length,
        RebirthToolbeltCapacity.GetOwnedSlotCount(player,player.inventory.ItemGrid.Length),RebirthCamperEnergyItems.Locks(player.inventory.ItemGrid.SlotLocks)??new bool[player.inventory.ItemGrid.Length]);
    private static bool LiveBeltPolicy(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try{return !ClientRead("RemoteBlockRefundChildren",original,out var request,out _)||request.Element("beltPolicy")==null||
            XNode.DeepEquals(request.Element("beltPolicy"),CaptureBeltPolicy(player));}
        catch{return false;}
    }
    private static bool SavedBeltPolicy(EntityPlayer actor,PlayerDataFile saved,XElement policy)
    {
        if(policy==null)return true; // Previously frozen bag-only child.
        try
        {
            if(!ReadBeltPolicy(policy,out int width,out int owned,out var locks)||saved?.inventoryData==null||saved.inventoryData.Length<1||saved.inventoryData.Length>4*1024*1024||
                RebirthToolbeltCapacity.GetOwnedSlotCount(actor,width)!=owned||RebirthPlayerDataInventory.ReadSlots(saved,false)?.Length!=width)return false;
            using(var stream=new MemoryStream(saved.inventoryData.ToArray(),false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);if(reader.ReadByte()!=1)return false;
                var grid=ItemStackGrid.Read(reader,StreamModeRead.Persistency);reader.ReadByte(); // Native holding slot footer.
                return stream.Position==stream.Length&&grid?.Length==width&&(RebirthCamperEnergyItems.Locks(grid.SlotLocks)??new bool[width]).SequenceEqual(locks);
            }
        }
        catch{return false;}
    }
    internal static bool Plan(RebirthCamperEnergyActorImage before,ItemStack[] payload,XElement policy,out RebirthCamperEnergyActorImage after)
    {
        if(policy==null)return Plan(before,payload,out after);
        after=null;
        try
        {
            if(before?.BagLocks?.Length!=before.Bag.Length||before.Bag.Length>169||!ReadBeltPolicy(policy,out int width,out int owned,out var locks)||
                before.Belt?.Length!=width||payload==null||payload.Length<1||payload.Length>8192)return false;
            var next=before.Copy();
            foreach(var source in payload)
            {
                if(source==null||source.IsEmpty()||source.count<1||source.count>ushort.MaxValue)return false;
                var remaining=source.Clone();
                if(!RebirthToolbeltCapacity.IsRejectedIncoming(remaining)&&remaining.CanMoveTo(XUiC_ItemStack.StackLocationTypes.ToolBelt))Insert(next.Belt,locks,owned,remaining);
                if(remaining.count>0&&remaining.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack))Insert(next.Bag,before.BagLocks,next.Bag.Length,remaining);
                if(remaining.count!=0)return false;
            }
            after=next;return true;
        }
        catch{after=null;return false;}
    }
    private static void Insert(ItemStack[] slots,bool[] locks,int width,ItemStack remaining)
    {
        int maximum=Math.Min(ushort.MaxValue,remaining.itemValue.ItemClass?.MaxCount??0);if(maximum<1)throw new InvalidDataException();
        string payload=RebirthNativeItemCodec.Encode(remaining.itemValue);
        for(int pass=0;pass<2;pass++)for(int slot=0;slot<width&&remaining.count>0;slot++)
        {
            if(locks[slot])continue;var current=slots[slot];if(current==null)throw new InvalidDataException();
            int available;
            if(pass==0)
            {if(current.IsEmpty()||RebirthNativeItemCodec.Encode(current.itemValue)!=payload||!current.CanStackPartlyWith(remaining,out available))continue;available=Math.Min(available,Math.Max(0,maximum-current.count));}
            else{if(!current.IsEmpty())continue;available=maximum;}
            int quantity=Math.Min(remaining.count,available);if(quantity<1)continue;
            bool empty=current.IsEmpty();
            slots[slot]=new ItemStack((empty?remaining.itemValue:current.itemValue).Clone(),(empty?0:current.count)+quantity);remaining.count-=quantity;
        }
    }
}
