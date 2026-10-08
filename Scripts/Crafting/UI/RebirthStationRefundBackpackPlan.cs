using System;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using System.Collections.Generic;

// Detached all-or-nothing backpack credit plan. No delivery or settlement authority.
internal sealed class RebirthStationRefundBackpackPlan
{
    private readonly ItemStack[] before,after;
    private readonly bool[] locks;
    private readonly int owned;
    private RebirthStationRefundBackpackPlan(ItemStack[] b,ItemStack[] a,bool[] l,int slots){before=b;after=a;locks=l;owned=slots;}
    internal static bool TryCreate(ItemStack[] live,int ownedSlots,Func<int,bool> isLocked,
        RebirthStationCancellationRefund refund,out RebirthStationRefundBackpackPlan plan)
    {
        plan=null;
        try
        {
            if(live==null||live.Length<1||live.Length>255||ownedSlots<1||ownedSlots>live.Length||refund==null)return false;
            var incoming=refund.CopyRefunds();if(incoming==null||incoming.Length<1||incoming.Length>9)return false;
            var before=Copy(live);var after=Copy(live);var locks=new bool[ownedSlots];
            for(int i=0;i<ownedSlots;i++)locks[i]=isLocked!=null&&isLocked(i);
            foreach(var stack in incoming)
            {
                if(stack?.itemValue==null||stack.itemValue.type<=0||stack.count<1||stack.count>ushort.MaxValue||
                    !stack.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack))return false;
                int maximum=Math.Min(ushort.MaxValue,stack.itemValue.ItemClass?.MaxCount??0);if(maximum<1)return false;
                string payload=RebirthNativeItemCodec.Encode(stack.itemValue);if(string.IsNullOrEmpty(payload))return false;
                int remaining=stack.count;
                for(int i=0;i<ownedSlots&&remaining>0;i++)
                {
                    var slot=after[i];
                    if(locks[i]||slot.IsEmpty()||RebirthNativeItemCodec.Encode(slot.itemValue)!=payload||
                        !slot.CanStackPartlyWith(stack,out var available))continue;
                    int added=Math.Min(remaining,Math.Min(available,Math.Max(0,maximum-slot.count)));
                    if(added<0)return false;slot.count+=added;remaining-=added;
                }
                for(int i=0;i<ownedSlots&&remaining>0;i++)
                {
                    if(locks[i]||!after[i].IsEmpty())continue;
                    int added=Math.Min(remaining,maximum);after[i]=new ItemStack(stack.itemValue.Clone(),added);remaining-=added;
                }
                if(remaining!=0)return false;
            }
            if(!Conserves(before,incoming,after))return false;
            plan=new RebirthStationRefundBackpackPlan(before,after,locks,ownedSlots);return true;
        }
        catch{return false;}
    }
    internal bool MatchesBefore(ItemStack[] current,int ownedSlots,Func<int,bool> isLocked)
    {
        try
        {
            if(current==null||current.Length!=before.Length||ownedSlots!=owned)return false;
            for(int i=0;i<current.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(current[i],before[i]))return false;
            for(int i=0;i<owned;i++)if((isLocked!=null&&isLocked(i))!=locks[i])return false;
            return true;
        }
        catch{return false;}
    }
    internal bool MatchesAfter(ItemStack[] current)
    {if(current==null||current.Length!=after.Length)return false;for(int i=0;i<current.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(current[i],after[i]))return false;return true;}
    internal ItemStack[] CopyAfter()=>Copy(after);
    private static ItemStack[] Copy(ItemStack[] slots)
    {
        var copy=new ItemStack[slots.Length];
        for(int i=0;i<copy.Length;i++)
        {
            if(slots[i]?.itemValue==null||slots[i].count<0||slots[i].count>ushort.MaxValue)throw new InvalidOperationException("Invalid refund destination slot");
            copy[i]=slots[i].Clone();
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(slots[i],copy[i]))throw new InvalidOperationException("Refund destination clone changed payload");
        }
        return copy;
    }
    private static bool Conserves(ItemStack[] before,ItemStack[] incoming,ItemStack[] after)
    {
        var counts=new Dictionary<string,long>(StringComparer.Ordinal);
        foreach(var slots in new[]{before,incoming,after})foreach(var stack in slots)
        {
            if(stack.count==0)continue;if(stack.itemValue.type<=0)return false;
            var key=RebirthNativeItemCodec.Encode(stack.itemValue);counts.TryGetValue(key,out var value);
            counts[key]=value+(ReferenceEquals(slots,after)?-stack.count:stack.count);
        }
        foreach(var value in counts.Values)if(value!=0)return false;
        return true;
    }
    internal XElement Write()
    {
        var node=new XElement("stationRefundBackpackPlan",new XAttribute("version",1),new XAttribute("owned",owned));
        long size=0;
        for(int i=0;i<before.Length;i++)
        {
            string b=EncodeSlot(before[i]),a=EncodeSlot(after[i]);
            if((size+=b.Length+a.Length)>2*1024*1024)throw new InvalidDataException("Refund backpack plan exceeds bound");
            node.Add(new XElement("slot",new XAttribute("before",b),new XAttribute("beforeCount",before[i].count),
                new XAttribute("after",a),new XAttribute("afterCount",after[i].count),new XAttribute("locked",i<owned&&locks[i]?"1":"0")));
        }
        if(System.Text.Encoding.UTF8.GetByteCount(node.ToString(SaveOptions.DisableFormatting))>2*1024*1024)throw new InvalidDataException("Refund backpack plan exceeds bound");
        return node;
    }
    // Stored payment and destination are authoritative; changed current native capacity holds recovery.
    internal static bool TryRead(XElement node,RebirthStationCancellationRefund refund,out RebirthStationRefundBackpackPlan plan)
    {
        plan=null;
        try
        {
            if(node==null||node.Name!="stationRefundBackpackPlan"||node.Attributes().Count()!=2||(string)node.Attribute("version")!="1"||
                node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
                System.Text.Encoding.UTF8.GetByteCount(node.ToString(SaveOptions.DisableFormatting))>2*1024*1024)return false;
            var slots=node.Elements().ToArray();int owned=(int)node.Attribute("owned");
            if(slots.Length<1||slots.Length>255||owned<1||owned>slots.Length)return false;
            var before=new ItemStack[slots.Length];var after=new ItemStack[slots.Length];var locks=new bool[owned];
            for(int i=0;i<slots.Length;i++)
            {
                var slot=slots[i];string b=(string)slot.Attribute("before"),a=(string)slot.Attribute("after"),locked=(string)slot.Attribute("locked");
                if(slot.Name!="slot"||slot.Attributes().Count()!=5||slot.HasElements||
                    slot.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
                    locked!="0"&&locked!="1"||i>=owned&&locked!="0"||
                    !TryReadValue(b,(int)slot.Attribute("beforeCount"),out var bv)||!TryReadValue(a,(int)slot.Attribute("afterCount"),out var av)||
                    EncodeSlot(new ItemStack(bv,(int)slot.Attribute("beforeCount")))!=b||EncodeSlot(new ItemStack(av,(int)slot.Attribute("afterCount")))!=a)return false;
                int bc=(int)slot.Attribute("beforeCount"),ac=(int)slot.Attribute("afterCount");
                if(bc<0||bc>ushort.MaxValue||ac<0||ac>ushort.MaxValue)return false;
                before[i]=new ItemStack(bv,bc);after[i]=new ItemStack(av,ac);if(i<owned)locks[i]=locked=="1";
            }
            if(!TryCreate(before,owned,i=>locks[i],refund,out var expected))return false;
            for(int i=0;i<after.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(after[i],expected.after[i]))return false;
            plan=expected;return true;
        }
        catch{return false;}
    }
    private static string EncodeSlot(ItemStack stack)
    {
        if(stack.count==0&&stack.itemValue.type==0)
        {
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(stack,ItemStack.Empty))throw new InvalidDataException("Noncanonical empty destination");
            return string.Empty;
        }
        string encoded=RebirthNativeItemCodec.Encode(stack.itemValue);
        if(string.IsNullOrEmpty(encoded)||!RebirthNativeItemCodec.TryDecode(encoded,out var value)||
            !RebirthStationGridIngredients.IsSameStackSnapshot(stack,new ItemStack(value,stack.count)))throw new InvalidDataException("Refund destination item does not roundtrip");
        return encoded;
    }
    private static bool TryReadValue(string data,int count,out ItemValue value)
    {
        value=null;
        if(data==string.Empty&&count==0){value=ItemStack.Empty.itemValue.Clone();return true;}
        return RebirthNativeItemCodec.TryDecode(data,out value);
    }
}