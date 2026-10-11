using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

public sealed partial class RebirthBackpackLibraryReceipt
{
    internal int BatchTraderId=>int.TryParse((string)image.Attribute("trader"),out var id)?id:-1;
    public bool IsBatchSale=>(string)image.Attribute("version")=="5";
    public sealed class WalletChange { public int Slot;public ItemStack Before,After; }
    public bool TryGetWallet(out List<WalletChange> changes)
    {
        changes=new List<WalletChange>();if(!IsBatchSale)return false;
        var wallet=image.Element("wallet");if(wallet==null)return false;
        var seen=new HashSet<int>();
        foreach(var cell in wallet.Elements())
        {
            if(!Shape(cell,"cell","slot","before,after")||!Number(cell,"slot",out int slot)||slot<0||slot>=169||!seen.Add(slot)||
                !ReadStack(cell.Element("before"),"before",out var before)||!ReadStack(cell.Element("after"),"after",out var after))return false;
            changes.Add(new WalletChange{Slot=slot,Before=before,After=after});
        }
        return changes.Count>0&&changes.Count<=169;
    }
    internal static bool TryCreateSale(string creation,long revision,ItemValue pack,ItemValue afterPack,ItemStack[] bag,PackedBoolArray locks,int money,out RebirthBackpackLibraryReceipt receipt,int traderId=-1)
    {
        receipt=null;var currency=ItemClass.GetItem("casinoCoin",false);if(currency.IsEmpty()||bag==null||money<=0)return false;
        var wallet=new XElement("wallet");int remaining=money,first=-1;ItemStack firstBefore=null,firstAfter=null;
        for(int pass=0;pass<2;pass++)for(int i=0;i<bag.Length&&remaining>0;i++)
        {
            var before=bag[i];if(before==null||locks!=null&&i<locks.Length&&locks[i])continue;
            bool empty=before.IsEmpty();if(pass==0?empty:!empty)continue;
            if(!empty&&!RebirthBackpackLibraryTransfer.CanMerge(before,new ItemStack(currency,1)))continue;
            int add=Math.Min(remaining,currency.ItemClass.MaxCount-(empty?0:before.count));if(add<=0)continue;
            var next=empty?new ItemStack(currency.Clone(),add):before.Clone();if(!empty)next.count+=add;
            wallet.Add(new XElement("cell",new XAttribute("slot",i),Stack("before",before),Stack("after",next)));
            if(first<0){first=i;firstBefore=before;firstAfter=next;}remaining-=add;
        }
        if(remaining!=0)return false;
        var xml=new XElement("libraryTransfer",new XAttribute("version",5),new XAttribute("transaction",Guid.NewGuid().ToString("N")),new XAttribute("creation",creation),
            new XAttribute("revision",revision),new XAttribute("bag",true),new XAttribute("inventory",first),new XAttribute("library",0),new XAttribute("quantity",money),new XAttribute("deposit",false),new XAttribute("section","sell"),
            Stack("packBefore",new ItemStack(pack,1)),Stack("packAfter",new ItemStack(afterPack,1)),Stack("slotBefore",firstBefore),Stack("slotAfter",firstAfter),wallet);
        if(traderId>=0)xml.SetAttributeValue("trader",traderId);
        return TryReadSale(xml,out receipt);
    }
    // These are successive images of one stack, not two stacks being combined.
    // Native CanStackWith includes combined-count capacity and rejects legitimate before/after images.
    private static bool SameSaleItem(ItemStack before, ItemStack after)
        => before != null && after != null && !before.IsEmpty() && !after.IsEmpty()
            && string.Equals(RebirthNativeItemCodec.Encode(before.itemValue),
                RebirthNativeItemCodec.Encode(after.itemValue), StringComparison.Ordinal);
    private static bool TryReadSale(XElement xml,out RebirthBackpackLibraryReceipt receipt)
    {
        receipt=null;
        if(!Shape(xml,"libraryTransfer","version,transaction,creation,revision,bag,inventory,library,quantity,deposit,section"+(xml.Attribute("trader")==null?"":",trader"),"packBefore,packAfter,slotBefore,slotAfter,wallet")||
            (string)xml.Attribute("version")!="5"||(string)xml.Attribute("library")!="0"||(string)xml.Attribute("section")!="sell"||(string)xml.Attribute("bag")!="true"||(string)xml.Attribute("deposit")!="false"||
            !Guid.TryParse((string)xml.Attribute("transaction"),out var transaction)||transaction==Guid.Empty||
            !RebirthSurvivorRequestScope.TryNormalize((string)xml.Attribute("creation"),out var creation)||
            !long.TryParse((string)xml.Attribute("revision"),out long revision)||revision<0||revision==long.MaxValue||
            !Number(xml,"inventory",out int index)||!Number(xml,"quantity",out int money)||money<=0)return false;
        if(xml.Attribute("trader")!=null&&(!Number(xml,"trader",out int trader)||trader<0))return false;
        var candidate=new RebirthBackpackLibraryReceipt(xml,transaction,creation,revision,true,index,0,money,false,true);
        if(!candidate.TryGetImages(out var beforePack,out var afterPack,out var beforeFirst,out var afterFirst)||!candidate.TryGetWallet(out var changes)||
            changes[0].Slot!=index||!RebirthStationGridIngredients.IsSameStackSnapshot(changes[0].Before,beforeFirst)||!RebirthStationGridIngredients.IsSameStackSnapshot(changes[0].After,afterFirst))return false;
        long sum=0;
        foreach(var change in changes)
        {
            if(change.After.IsEmpty()||change.After.itemValue.ItemClass.GetItemName()!="casinoCoin"||change.After.count<=change.Before.count||
                !change.Before.IsEmpty()&&!SameSaleItem(change.Before,change.After))return false;
            sum+=change.After.count-change.Before.count;
        }
        if(sum!=money||!RebirthBackpackSellStashContents.TryRead(beforePack,out var oldCells)||!RebirthBackpackSellStashContents.TryRead(afterPack,out var newCells)||oldCells.Length!=newCells.Length)return false;
        bool removed=false;
        for(int i=0;i<oldCells.Length;i++)
        {
            if(newCells[i].count>oldCells[i].count||!newCells[i].IsEmpty()&&!SameSaleItem(oldCells[i],newCells[i]))return false;
            removed|=oldCells[i].count>newCells[i].count;
        }
        // Only stash contents may differ: reconstruct using the original equipped item.
        if(!removed||!RebirthBackpackSellStashContents.TryWrite(beforePack,newCells,out var expected)||RebirthNativeItemCodec.Encode(expected)!=RebirthNativeItemCodec.Encode(afterPack))return false;
        receipt=candidate;return true;
    }
}
