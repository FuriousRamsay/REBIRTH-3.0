using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Immutable library intent. Caller authenticates owner and persists this before dispatch.
// This is not a live transaction or proof that native player inventory reached disk.
public sealed class RebirthBackpackLibraryReceipt
{
    public string TransactionId {get;private set;}
    public string CreationId {get;private set;}
    public long ExpectedGearRevision {get;private set;}
    public bool IsBag {get;private set;}
    public bool IsCursor {get;private set;}
    public int InventorySlot {get;private set;}
    public int LibrarySlot {get;private set;}
    public int Quantity {get;private set;}
    public bool Deposit {get;private set;}
    public bool IsSellStash {get;private set;}
    private readonly XElement image;
    private RebirthBackpackLibraryReceipt(XElement xml,Guid transaction,string creation,long revision,
        bool bag,int inventory,int library,int quantity,bool deposit,bool sell)
    {
        image=new XElement(xml);TransactionId=transaction.ToString("N");CreationId=creation;
        ExpectedGearRevision=revision;IsBag=bag;InventorySlot=inventory;LibrarySlot=library;Quantity=quantity;Deposit=deposit;IsSellStash=sell;IsCursor=(string)xml.Attribute("cursor")=="true";
        image.SetAttributeValue("transaction",TransactionId);image.SetAttributeValue("creation",CreationId);
    }
    public XElement ToXml()=>new XElement(image);
    private static XElement Stack(string name,ItemStack stack)
        =>new XElement(name,new XAttribute("count",stack.count),new XAttribute("data",stack.count==0?"":RebirthNativeItemCodec.Encode(stack.itemValue)));
    private static bool Shape(XElement node,string name,string attributes,string children)
    {
        if(node==null||node.Name!=name)return false;
        var a=attributes.Length==0?new string[0]:attributes.Split(',');
        var c=children.Length==0?new string[0]:children.Split(',');
        return node.Attributes().Count()==a.Length&&a.All(k=>node.Attribute(k)!=null)&&
            node.Elements().Count()==c.Length&&c.All(k=>node.Elements(k).Count()==1)&&
            !node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)));
    }
    private static bool Number(XElement xml,string key,out int value)
        =>int.TryParse((string)xml.Attribute(key),NumberStyles.Integer,CultureInfo.InvariantCulture,out value);
    private static bool ReadStack(XElement xml,string name,out ItemStack stack)
    {
        stack=null;
        if(!Shape(xml,name,"count,data","")||!Number(xml,"count",out int count)||count<0||count>ushort.MaxValue)return false;
        string data=(string)xml.Attribute("data");
        if(count==0){if(data!="")return false;stack=ItemStack.Empty.Clone();return true;}
        if(!RebirthNativeItemCodec.TryDecode(data,out var value)||count>value.ItemClass.MaxCount)return false;
        stack=new ItemStack(value,count);return true;
    }
    public bool TryGetImages(out ItemValue beforePack,out ItemValue afterPack,out ItemStack beforeSlot,out ItemStack afterSlot)
    {
        beforePack=null;afterPack=null;beforeSlot=null;afterSlot=null;
        if(!ReadStack(image.Element("packBefore"),"packBefore",out var a)||a.count!=1||
            !ReadStack(image.Element("packAfter"),"packAfter",out var b)||b.count!=1||
            !ReadStack(image.Element("slotBefore"),"slotBefore",out var c)||
            !ReadStack(image.Element("slotAfter"),"slotAfter",out var d))return false;
        beforePack=a.itemValue;afterPack=b.itemValue;beforeSlot=c;afterSlot=d;return true;
    }
    public static bool TryCreate(Guid transaction,Guid creation,long revision,bool bag,int inventory,int library,
        int quantity,bool deposit,ItemValue backpack,ItemStack slot,out RebirthBackpackLibraryReceipt receipt)
        =>TryCreate(transaction,creation.ToString("N"),revision,bag,inventory,library,quantity,deposit,backpack,slot,out receipt);
    public static bool TryCreate(Guid transaction,string creation,long revision,bool bag,int inventory,int library,
        int quantity,bool deposit,ItemValue backpack,ItemStack slot,out RebirthBackpackLibraryReceipt receipt)
        =>TryCreateSection(transaction,creation,revision,bag,inventory,library,quantity,deposit,backpack,slot,false,out receipt);
    public static bool TryCreateSellStash(Guid transaction,string creation,long revision,bool bag,int inventory,int stash,
        int quantity,bool deposit,ItemValue backpack,ItemStack slot,out RebirthBackpackLibraryReceipt receipt)
        =>TryCreateSection(transaction,creation,revision,bag,inventory,stash,quantity,deposit,backpack,slot,true,out receipt);
    public static bool TryCreateCursor(Guid transaction,string creation,long revision,int sectionSlot,
        int quantity,bool deposit,ItemValue backpack,ItemStack cursor,bool sell,out RebirthBackpackLibraryReceipt receipt)
        =>TryCreateSection(transaction,creation,revision,false,0,sectionSlot,quantity,deposit,backpack,cursor,sell,out receipt,true);
    private static bool Plan(bool sell,bool deposit,ItemValue backpack,ItemStack slot,int sectionSlot,int quantity,
        out ItemValue nextPack,out ItemStack nextSlot,bool cursor=false)
    {
        if(cursor&&deposit&&slot!=null&&!slot.IsEmpty()&&quantity==slot.count)
        {
            ItemStack[] cells;
            bool decoded=sell?RebirthBackpackSellStashContents.TryRead(backpack,out cells):RebirthBackpackLibraryContents.TryRead(backpack,out cells);
            if(decoded&&sectionSlot>=0&&sectionSlot<cells.Length&&cells[sectionSlot]!=null&&!cells[sectionSlot].IsEmpty()&&
                !RebirthBackpackLibraryTransfer.CanMerge(slot,cells[sectionSlot]))
            {
                nextPack=null;nextSlot=null;
                if(!sell&&!RebirthBackpackLibraryPolicy.IsLearningMaterial(slot.itemValue))return false;
                var incoming=slot.Clone();var outgoing=cells[sectionSlot].Clone();cells[sectionSlot]=incoming;
                bool written=sell?RebirthBackpackSellStashContents.TryWrite(backpack,cells,out nextPack):RebirthBackpackLibraryContents.TryWrite(backpack,cells,out nextPack);
                if(!written)return false;
                if(!(sell?RebirthBackpackSellStashConservation.IsConserved(backpack,nextPack,slot,outgoing):
                    RebirthBackpackLibraryConservation.IsConserved(backpack,nextPack,slot,outgoing)))return false;
                nextSlot=outgoing;return true;
            }
        }
        if(sell)return deposit?RebirthBackpackSellStashTransfer.TryDeposit(backpack,slot,sectionSlot,quantity,out nextPack,out nextSlot)
            :RebirthBackpackSellStashTransfer.TryWithdraw(backpack,sectionSlot,quantity,slot,out nextPack,out nextSlot);
        return deposit?RebirthBackpackLibraryTransfer.TryDeposit(backpack,slot,sectionSlot,quantity,out nextPack,out nextSlot)
            :RebirthBackpackLibraryTransfer.TryWithdraw(backpack,sectionSlot,quantity,slot,out nextPack,out nextSlot);
    }
    private static bool TryCreateSection(Guid transaction,string creation,long revision,bool bag,int inventory,int library,
        int quantity,bool deposit,ItemValue backpack,ItemStack slot,bool sell,out RebirthBackpackLibraryReceipt receipt,bool cursor=false)
    {
        receipt=null;
        try
        {
            ItemValue nextPack;ItemStack nextSlot;
            bool planned=Plan(sell,deposit,backpack,slot,library,quantity,out nextPack,out nextSlot,cursor);
            if(!planned)return false;
            var image=new XElement("libraryTransfer",new XAttribute("version",cursor?(sell?4:3):(sell?2:1)),new XAttribute("transaction",transaction.ToString("N")),
                new XAttribute("creation",creation),new XAttribute("revision",revision),new XAttribute("bag",bag),
                new XAttribute("inventory",inventory),new XAttribute("library",library),new XAttribute("quantity",quantity),new XAttribute("deposit",deposit),
                Stack("packBefore",new ItemStack(backpack,1)),Stack("packAfter",new ItemStack(nextPack,1)),
                Stack("slotBefore",slot),Stack("slotAfter",nextSlot));
            if(sell)image.SetAttributeValue("section","sell");
            if(cursor)image.SetAttributeValue("cursor","true");
            return TryRead(image,out receipt);
        }
        catch{return false;}
    }
    public static bool TryRead(XElement xml,out RebirthBackpackLibraryReceipt receipt)
    {
        receipt=null;
        try
        {
            string version=(string)xml?.Attribute("version");
            bool cursor=version=="3"||version=="4";
            bool sell=version=="2"||version=="4";
            string attributes="version,transaction,creation,revision,bag,inventory,library,quantity,deposit"+(sell?",section":"")+(cursor?",cursor":"");
            if(!Shape(xml,"libraryTransfer",attributes,"packBefore,packAfter,slotBefore,slotAfter")||
                (version!="1"&&version!="2"&&version!="3"&&version!="4")||
                (sell&&(string)xml.Attribute("section")!="sell")||(cursor&&(string)xml.Attribute("cursor")!="true")||!Guid.TryParse((string)xml.Attribute("transaction"),out var transaction)||transaction==Guid.Empty||
                !RebirthSurvivorRequestScope.TryNormalize((string)xml.Attribute("creation"),out var creation)||
                !long.TryParse((string)xml.Attribute("revision"),NumberStyles.Integer,CultureInfo.InvariantCulture,out long revision)||revision<0||revision==long.MaxValue||
                !bool.TryParse((string)xml.Attribute("bag"),out bool bag)||!bool.TryParse((string)xml.Attribute("deposit"),out bool deposit)||
                !Number(xml,"inventory",out int inventory)||inventory<0||inventory>=(cursor?1:bag?169:18)||(cursor&&bag)||
                !Number(xml,"library",out int library)||library<0||library>=(sell?RebirthBackpackSellStashPolicy.MaxSlots:RebirthBackpackLibraryPolicy.MaxSlots)||
                !Number(xml,"quantity",out int quantity)||quantity<1||quantity>ushort.MaxValue)return false;
            var candidate=new RebirthBackpackLibraryReceipt(xml,transaction,creation,revision,bag,inventory,library,quantity,deposit,sell);
            if(!candidate.TryGetImages(out var beforePack,out var afterPack,out var beforeSlot,out var afterSlot))return false;
            ItemValue expectedPack;ItemStack expectedSlot;
            bool planned=Plan(sell,deposit,beforePack,beforeSlot,library,quantity,out expectedPack,out expectedSlot,cursor);
            if(!planned||!string.Equals(RebirthNativeItemCodec.Encode(expectedPack),RebirthNativeItemCodec.Encode(afterPack),StringComparison.Ordinal)||
                !XNode.DeepEquals(Stack("slotAfter",expectedSlot),Stack("slotAfter",afterSlot)))return false;
            receipt=candidate;return true;
        }
        catch{return false;}
    }
}
