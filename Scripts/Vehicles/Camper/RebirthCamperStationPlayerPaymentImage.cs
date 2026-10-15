using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Explicit actor custody, independent of the legacy nine-cell station grid.
// Immutable exact native ItemValue images; parsing grants no debit authority.
internal sealed class RebirthCamperStationPlayerPaymentImage
{
    private const int MaximumText=2*1024*1024;
    private readonly XElement image;
    private readonly ItemStack[] bagBefore,bagAfter,beltBefore,beltAfter,stationInput,paid;
    private readonly bool[] bagLocks,beltLocks;
    internal string OwnerKey {get;}
    internal string OwnerId {get;}
    internal int OwnedBeltSlots {get;}
    private RebirthCamperStationPlayerPaymentImage(XElement node,ItemStack[] oldBag,ItemStack[] newBag,
        ItemStack[] oldBelt,ItemStack[] newBelt,ItemStack[] input,ItemStack[] fragments,bool[] locksBag,bool[] locksBelt)
    {
        image=new XElement(node);OwnerKey=(string)node.Attribute("ownerKey");OwnerId=(string)node.Attribute("ownerId");
        OwnedBeltSlots=(int)node.Attribute("ownedBelt");bagBefore=oldBag;bagAfter=newBag;beltBefore=oldBelt;beltAfter=newBelt;
        stationInput=input;paid=fragments;bagLocks=locksBag;beltLocks=locksBelt;
    }
    internal XElement Write()=>new XElement(image);
    internal ItemStack[] PaidSnapshots()=>Copy(paid);
    internal ItemStack[] StationInputBefore()=>Copy(stationInput);
    internal void CopyImages(out ItemStack[] oldBag,out ItemStack[] newBag,out ItemStack[] oldBelt,out ItemStack[] newBelt)
    {oldBag=Copy(bagBefore);newBag=Copy(bagAfter);oldBelt=Copy(beltBefore);newBelt=Copy(beltAfter);}
    internal bool MatchesBefore(ItemStack[] bag,ItemStack[] belt,bool[] locksBag,bool[] locksBelt,int owned)
        =>owned==OwnedBeltSlots&&Same(bagBefore,bag)&&Same(beltBefore,belt)&&SameLocks(bagLocks,locksBag)&&SameLocks(beltLocks,locksBelt);
    internal bool MatchesAfter(ItemStack[] bag,ItemStack[] belt)=>Same(bagAfter,bag)&&Same(beltAfter,belt);
    internal bool MatchesBeforeSlots(ItemStack[] bag,ItemStack[] belt)=>Same(bagBefore,bag)&&Same(beltBefore,belt);
    internal bool MatchesStation(IList<ItemStack> input)=>Same(stationInput,input);
    internal bool MatchesPaid(IList<ItemStack> fragments)
    {
        if(fragments==null||fragments.Count!=paid.Length)return false;
        for(int i=0;i<paid.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(paid[i],RebirthStationGridQueue.RefundSnapshot(fragments[i])))return false;
        return true;
    }
    internal static bool TryCreate(RebirthStablePlayerIdentity owner,RebirthStationPlayerIngredients.Plan plan,
        IList<ItemStack> station,bool[] locksBag,bool[] locksBelt,out RebirthCamperStationPlayerPaymentImage result)
    {
        result=null;
        if(owner==null||plan==null||!plan.TryGetImages(out var oldBag,out var newBag,out var oldBelt,out var newBelt)||
            !RebirthStationGridSnapshotCodec.TryWrite(station,out var stationImage)||
            !plan.Matches(oldBag,oldBelt,locksBag,locksBelt,plan.OwnedBeltSlots))return false;
        try
        {
            var node=new XElement("playerPayment",new XAttribute("version",1),new XAttribute("source","ownerInventory"),
                new XAttribute("ownerKey",owner.StorageKey),new XAttribute("ownerId",owner.CanonicalId),
                new XAttribute("ownedBelt",plan.OwnedBeltSlots));
            int budget=MaximumText;
            if(!WriteArray("bagBefore",oldBag,169,ref budget,out var a)||!WriteArray("bagAfter",newBag,169,ref budget,out var b)||
                !WriteArray("beltBefore",oldBelt,20,ref budget,out var c)||!WriteArray("beltAfter",newBelt,20,ref budget,out var d))return false;
            node.Add(a,b,c,d,WriteLocks("bagLocks",locksBag),WriteLocks("beltLocks",locksBelt));
            var allocations=new XElement("allocations");
            foreach(var allocation in plan.Inputs)
                allocations.Add(new XElement("take",new XAttribute("source",allocation.IsBag?"bag":"belt"),
                    new XAttribute("slot",allocation.Slot),new XAttribute("count",allocation.Count)));
            node.Add(allocations,new XElement("stationInput",stationImage));
            return TryRead(node,out result);
        }
        catch{return false;}
    }
    internal static bool TryRead(XElement node,out RebirthCamperStationPlayerPaymentImage result)
    {
        result=null;
        try
        {
            if(!Shape(node,"playerPayment","version,source,ownerKey,ownerId,ownedBelt","bagBefore,bagAfter,beltBefore,beltAfter,bagLocks,beltLocks,allocations,stationInput")||
                (string)node.Attribute("version")!="1"||(string)node.Attribute("source")!="ownerInventory"||
                !Identity((string)node.Attribute("ownerKey"))||!Identity((string)node.Attribute("ownerId"))||
                !Int(node,"ownedBelt",out int owned)||owned<0||owned>20)return false;
            int budget=MaximumText;
            if(!ReadArray(node.Element("bagBefore"),169,ref budget,out var oldBag)||!ReadArray(node.Element("bagAfter"),169,ref budget,out var newBag)||
                !ReadArray(node.Element("beltBefore"),20,ref budget,out var oldBelt)||!ReadArray(node.Element("beltAfter"),20,ref budget,out var newBelt)||
                oldBag.Length!=newBag.Length||oldBelt.Length!=newBelt.Length||owned>oldBelt.Length||
                !ReadLocks(node.Element("bagLocks"),oldBag.Length,out var locksBag)||!ReadLocks(node.Element("beltLocks"),oldBelt.Length,out var locksBelt))return false;
            var station=node.Element("stationInput");
            if(!Shape(station,"stationInput","","grid")||!RebirthStationGridSnapshotCodec.TryRead(station.Element("grid"),out var input))return false;
            var allocations=node.Element("allocations");
            if(allocations.HasAttributes||allocations.Elements().Count()<1||allocations.Elements().Count()>9||!WhitespaceOnly(allocations))return false;
            var expectedBag=Copy(oldBag);var expectedBelt=Copy(oldBelt);var fragments=new List<ItemStack>();
            foreach(var take in allocations.Elements())
            {
                if(!Shape(take,"take","source,slot,count","")||!Int(take,"slot",out int slot)||!Int(take,"count",out int count)||count<1)return false;
                string source=(string)take.Attribute("source");
                var cells=source=="bag"?expectedBag:source=="belt"?expectedBelt:null;
                if(cells==null||slot<0||slot>=(source=="bag"?cells.Length:owned)||cells[slot].IsEmpty()||cells[slot].count<count)return false;
                var fragment=cells[slot].Clone();fragment.count=count;fragments.Add(fragment);
                cells[slot].count-=count;if(cells[slot].count==0)cells[slot]=ItemStack.Empty.Clone();
            }
            if(!Same(expectedBag,newBag)||!Same(expectedBelt,newBelt))return false;
            result=new RebirthCamperStationPlayerPaymentImage(node,oldBag,newBag,oldBelt,newBelt,input,fragments.ToArray(),locksBag,locksBelt);return true;
        }
        catch{return false;}
    }
    private static bool Identity(string value)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=256&&!value.Any(char.IsControl);
    private static bool Int(XElement node,string name,out int value)=>int.TryParse((string)node.Attribute(name),NumberStyles.None,CultureInfo.InvariantCulture,out value);
    private static bool WhitespaceOnly(XElement node)=>!node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)));
    private static bool Shape(XElement node,string name,string attributes,string children)
    {
        if(node==null||node.Name!=name||!WhitespaceOnly(node))return false;
        var a=attributes.Length==0?new string[0]:attributes.Split(',');var c=children.Length==0?new string[0]:children.Split(',');
        return node.Attributes().Count()==a.Length&&a.All(k=>node.Attribute(k)!=null)&&node.Elements().Count()==c.Length&&c.All(k=>node.Elements(k).Count()==1);
    }
    private static ItemStack[] Copy(IList<ItemStack> cells){var copy=new ItemStack[cells.Count];for(int i=0;i<copy.Length;i++)copy[i]=cells[i].Clone();return copy;}
    private static bool Same(IList<ItemStack> a,IList<ItemStack> b)
    {if(a==null||b==null||a.Count!=b.Count)return false;for(int i=0;i<a.Count;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(a[i],b[i]))return false;return true;}
    private static bool SameLocks(bool[] a,bool[] b)
    {if(a==null||b==null)return a==b;return a.SequenceEqual(b);}
    private static XElement WriteLocks(string name,bool[] locks)=>new XElement(name,new XAttribute("data",locks==null?"null":new string(locks.Select(v=>v?'1':'0').ToArray())));
    private static bool ReadLocks(XElement node,int count,out bool[] locks)
    {
        locks=null;if(!Shape(node,node?.Name.LocalName,"data",""))return false;
        string data=(string)node.Attribute("data");if(data=="null")return true;
        if(data.Length!=count||data.Any(c=>c!='0'&&c!='1'))return false;locks=data.Select(c=>c=='1').ToArray();return true;
    }
    // Separate bounded actor codec; registered native reader restores complete item bytes.
    private static bool WriteArray(string name,IList<ItemStack> cells,int maximum,ref int budget,out XElement image)
    {
        image=null;if(cells==null||cells.Count>maximum)return false;var node=new XElement(name);
        foreach(var cell in cells)
        {
            if(cell?.itemValue==null||cell.count<0||cell.count>ushort.MaxValue||cell.count>0&&cell.itemValue.IsEmpty())return false;
            string data=RebirthNativeItemCodec.Encode(cell.itemValue.Clone());
            if(string.IsNullOrEmpty(data)||data.Length>262144||(budget-=data.Length)<0)return false;
            node.Add(new XElement("slot",new XAttribute("count",cell.count),new XAttribute("data",data)));
        }
        image=node;return true;
    }
    private static bool ReadArray(XElement node,int maximum,ref int budget,out ItemStack[] cells)
    {
        cells=null;if(node==null||node.HasAttributes||node.Elements().Count()>maximum||!WhitespaceOnly(node))return false;
        var result=new List<ItemStack>();
        foreach(var cell in node.Elements())
        {
            if(!Shape(cell,"slot","count,data","")||!Int(cell,"count",out int count)||count>ushort.MaxValue)return false;
            string data=(string)cell.Attribute("data");
            if(string.IsNullOrEmpty(data)||data.Length>262144||(budget-=data.Length)<0)return false;
            ItemValue value;
            if(data==RebirthNativeItemCodec.Encode(ItemStack.Empty.Clone().itemValue))
            {if(count!=0)return false;value=ItemStack.Empty.Clone().itemValue;}
            else if(!RebirthNativeItemCodec.TryDecode(data,out value)||!string.Equals(data,RebirthNativeItemCodec.Encode(value),StringComparison.Ordinal))return false;
            result.Add(new ItemStack(value,count));
        }
        cells=result.ToArray();return true;
    }
}
