using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Frozen trade description, not a payment capability. The protocol must still
// prove the authenticated quote, original owner native action and independently
// saved NPC effect. Neither this record nor equal inventory frames grant those.
internal sealed class RebirthNpcTradeOriginal
{
    private const int MaximumFrame=4*1024*1024,MaximumImage=24*1024*1024;
    internal readonly Guid Operation;
    internal readonly string Account,Creation,World,Image,Digest;
    internal readonly int Deaths,NativeId,OwnedBeltSlots;
    private readonly byte[] beforeBag,beforeBelt,afterBag,afterBelt;
    private readonly bool[] bagLocks,beltLocks;
    private RebirthNpcTradeOriginal(Guid operation,string account,string creation,string world,int deaths,int nativeId,int owned,
        byte[] bag,byte[] belt,byte[] nextBag,byte[] nextBelt,bool[] bagLocks,bool[] beltLocks,string image)
    {
        Operation=operation;Account=account;Creation=creation;World=world;Deaths=deaths;NativeId=nativeId;OwnedBeltSlots=owned;
        beforeBag=bag;beforeBelt=belt;afterBag=nextBag;afterBelt=nextBelt;this.bagLocks=bagLocks;this.beltLocks=beltLocks;Image=image;Digest=RebirthRemoteResourceDurableJournal.Digest(image);
    }
    internal static RebirthNpcTradeOriginal Freeze(RebirthNpcTradeQuote quote,RebirthNpcTradeOwnerPlan plan,string account,string creation,string world,int deaths,int nativeId)
    {
        if(!ThreadManager.IsMainThread()||quote==null||plan==null||quote.QuoteId==Guid.Empty||string.IsNullOrWhiteSpace(account)||account.Length>512||
            string.IsNullOrWhiteSpace(creation)||creation.Length>512||!Guid.TryParse(world,out _)||deaths<0||nativeId<=0||
            quote.Quantity<=0||quote.UnitPrice<=0||(long)quote.Quantity*quote.UnitPrice!=quote.TotalPrice||
            quote.Direction!=RebirthNpcTradeDirection.BuyFromNpc&&quote.Direction!=RebirthNpcTradeDirection.SellToNpc)throw new InvalidDataException("Invalid frozen trade scope");
        var bags=Frames(plan.BeforeBag,plan.BeforeBelt,plan.AfterBag,plan.AfterBelt);
        var root=new XElement("npcTradeOriginal",new XAttribute("version",1),new XAttribute("operation",quote.QuoteId.ToString("N")),
            new XAttribute("account",account),new XAttribute("creation",creation),new XAttribute("world",world),new XAttribute("deaths",deaths),new XAttribute("nativeId",nativeId),
            new XAttribute("ownedBelt",plan.OwnedBeltSlots),new XAttribute("npc",quote.NpcId.ToString()),new XAttribute("npcRevision",quote.ExpectedNpcInventoryRevision),
            new XAttribute("recovery",quote.RecoveryGeneration),new XAttribute("actor",quote.ActorId),new XAttribute("endpoint",quote.EndpointId),
            new XAttribute("direction",(byte)quote.Direction),new XAttribute("item",quote.ItemKey),new XAttribute("currency",quote.CurrencyItemKey),
            new XAttribute("quantity",quote.Quantity),new XAttribute("unit",quote.UnitPrice),new XAttribute("total",quote.TotalPrice),new XAttribute("expires",quote.ExpiresClockTicks),
            new XElement("beforeBag",Convert.ToBase64String(bags[0])),new XElement("beforeBelt",Convert.ToBase64String(bags[1])),
            new XElement("afterBag",Convert.ToBase64String(bags[2])),new XElement("afterBelt",Convert.ToBase64String(bags[3])),
            new XElement("bagLocks",new string(plan.BagLocks.Select(value=>value?'1':'0').ToArray())),new XElement("beltLocks",new string(plan.BeltLocks.Select(value=>value?'1':'0').ToArray())));
        string image=root.ToString(SaveOptions.DisableFormatting);return Read(image);
    }
    private static byte[][] Frames(params ItemStack[][] slots)
    {
        var result=new byte[slots.Length][];
        for(int i=0;i<slots.Length;i++){result[i]=RebirthCamperInventorySnapshot.Capture(slots[i]);if(result[i].Length>MaximumFrame)throw new InvalidDataException("Trade frame bound exceeded");}
        return result;
    }
    internal static RebirthNpcTradeOriginal Read(string image)
    {
        if(!ThreadManager.IsMainThread()||image==null||image.Length<1||Encoding.UTF8.GetByteCount(image)>MaximumImage)throw new InvalidDataException();
        XElement root;using(var text=new StringReader(image))using(var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumImage}))root=XElement.Load(reader);
        string[] attributes={"version","operation","account","creation","world","deaths","nativeId","ownedBelt","npc","npcRevision","recovery","actor","endpoint","direction","item","currency","quantity","unit","total","expires"};
        string[] elements={"beforeBag","beforeBelt","afterBag","afterBelt","bagLocks","beltLocks"};
        if(root.Name!="npcTradeOriginal"||(int?)root.Attribute("version")!=1||!root.Attributes().Select(value=>value.Name.ToString()).SequenceEqual(attributes)||
            root.Elements().Count()!=elements.Length||root.Nodes().Count()!=elements.Length||elements.Where((name,index)=>root.Elements().ElementAt(index).Name!=name).Any()||
            root.ToString(SaveOptions.DisableFormatting)!=image)throw new InvalidDataException("Noncanonical trade original");
        Guid operation=Guid.ParseExact((string)root.Attribute("operation"),"N");string account=(string)root.Attribute("account"),creation=(string)root.Attribute("creation"),world=(string)root.Attribute("world");
        int deaths=(int)root.Attribute("deaths"),nativeId=(int)root.Attribute("nativeId"),owned=(int)root.Attribute("ownedBelt"),quantity=(int)root.Attribute("quantity"),unit=(int)root.Attribute("unit"),total=(int)root.Attribute("total");
        int direction=(int)root.Attribute("direction");
        if(operation==Guid.Empty||string.IsNullOrWhiteSpace(account)||account.Length>512||string.IsNullOrWhiteSpace(creation)||creation.Length>512||!Guid.TryParse(world,out _)||
            deaths<0||nativeId<=0||owned<0||direction<0||direction>1||quantity<=0||unit<=0||(long)quantity*unit!=total||
            !ulong.TryParse((string)root.Attribute("recovery"),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out _)||
            !RebirthNpcStableId.TryParse((string)root.Attribute("npc"),out _)||((string)root.Attribute("item"))?.Length<1||((string)root.Attribute("currency"))?.Length<1||
            new[]{"actor","endpoint","item","currency"}.Any(name=>string.IsNullOrWhiteSpace((string)root.Attribute(name))||((string)root.Attribute(name)).Length>512))throw new InvalidDataException("Invalid trade original binding");
        uint revision=(uint)root.Attribute("npcRevision");long expires=(long)root.Attribute("expires");if(revision==uint.MaxValue||expires<=0)throw new InvalidDataException();
        var frames=new byte[4][];var items=new ItemStack[4][];
        for(int i=0;i<frames.Length;i++)
        {
            var element=root.Element(elements[i]);if(element.HasElements||element.HasAttributes||element.Nodes().Count()!=1||element.FirstNode is not XText||element.Value.Length>((MaximumFrame+2L)/3)*4)throw new InvalidDataException();
            frames[i]=Convert.FromBase64String(element.Value);if(frames[i].Length<8||frames[i].Length>MaximumFrame||Convert.ToBase64String(frames[i])!=element.Value)throw new InvalidDataException();
            items[i]=RebirthCamperInventorySnapshot.Restore(frames[i]);
            if(!RebirthCamperInventorySnapshot.Capture(items[i]).SequenceEqual(frames[i]))throw new InvalidDataException("Trade native frame changed");
        }
        if(items[0].Length!=items[2].Length||items[1].Length!=items[3].Length||owned>items[1].Length)throw new InvalidDataException("Trade native dimensions changed");
        var locks=new bool[2][];
        for(int area=0;area<2;area++)
        {
            var element=root.Element(area==0?"bagLocks":"beltLocks");string text=element.Value;
            if(element.HasElements||element.HasAttributes||element.Nodes().Any(node=>node is not XText)||text.Length!=items[area].Length||text.Any(value=>value!='0'&&value!='1'))throw new InvalidDataException("Invalid original native locks");
            locks[area]=text.Select(value=>value=='1').ToArray();
        }
        if(locks[1].Where((value,index)=>index>=owned&&!value).Any())throw new InvalidDataException("Unowned trade slots unlocked");
        var goods=ItemClass.GetItem((string)root.Attribute("item"),false);var currency=ItemClass.GetItem((string)root.Attribute("currency"),false);
        if(goods==null||currency==null||goods.IsEmpty()||currency.IsEmpty()||goods.type==currency.type||
            !ConservedDelta(items,locks,owned,direction==0?currency.type:goods.type,direction==0?total:quantity,direction==0?goods.type:currency.type,direction==0?quantity:total))
            throw new InvalidDataException("Frozen trade native delta differs from quote");
        return new RebirthNpcTradeOriginal(operation,account,creation,world,deaths,nativeId,owned,frames[0],frames[1],frames[2],frames[3],locks[0],locks[1],image);
    }
    private static bool ConservedDelta(ItemStack[][] frames,bool[][] locks,int owned,int debitType,int debitCount,int creditType,int creditCount)
    {
        long removed=0,added=0;int creditCells=0;
        for(int area=0;area<2;area++)for(int slot=0;slot<frames[area].Length;slot++)
        {
            var before=frames[area][slot];var after=frames[area+2][slot];
            string oldItem=RebirthNativeItemCodec.Encode(before.itemValue),newItem=RebirthNativeItemCodec.Encode(after.itemValue);
            if(before.count==after.count&&oldItem==newItem)continue;
            if(locks[area][slot]||area==1&&slot>=owned)return false;
            bool same=oldItem==newItem;
            if(!before.IsEmpty())
            {
                if(before.itemValue.type==debitType)
                {
                    if(!RebirthNpcPlayerInventoryEndpoint.IsFungible(before.itemValue))return false;
                    if(same)
                    {if(after.count>=before.count)return false;removed+=before.count-after.count;continue;}
                    removed+=before.count;
                }
                else if(before.itemValue.type==creditType&&same&&after.count>before.count)
                {added+=after.count-before.count;creditCells++;if(!ValidCredit(after,area))return false;continue;}
                else return false;
            }
            if(after.IsEmpty())
            {
                // Only a consumed debit may become an empty slot.
                if(before.IsEmpty()||before.itemValue.type!=debitType)return false;
                continue;
            }
            if(after.itemValue.type!=creditType||!RebirthNpcPlayerInventoryEndpoint.IsFungible(after.itemValue)||!ValidCredit(after,area))return false;
            added+=after.count;creditCells++;
        }
        return removed==debitCount&&added==creditCount&&creditCells==1;
    }
    private static bool ValidCredit(ItemStack stack,int area)
    {
        var item=ItemClass.GetForId(stack.itemValue.type);
        return item!=null&&stack.count>0&&stack.count<=item.MaxCount&&stack.CanMoveTo(area==0?XUiC_ItemStack.StackLocationTypes.Backpack:XUiC_ItemStack.StackLocationTypes.ToolBelt);
    }
    internal ItemStack[] CopySlots(bool bag,bool after)=>RebirthCamperInventorySnapshot.Restore(after?(bag?afterBag:afterBelt):(bag?beforeBag:beforeBelt));
    internal bool[] CopyLocks(bool bag)=>(bool[])(bag?bagLocks:beltLocks).Clone();
    // Always return a detached description. In particular, the mutable quote
    // registry object must never become the original's retry authority.
    internal RebirthNpcTradeQuote CopyQuote()
    {
        var root=XElement.Parse(Image);
        if(!RebirthNpcStableId.TryParse((string)root.Attribute("npc"),out var npc))throw new InvalidDataException();
        return new RebirthNpcTradeQuote
        {
            QuoteId=Operation,NpcId=npc,ActorId=(string)root.Attribute("actor"),EndpointId=(string)root.Attribute("endpoint"),
            Direction=(RebirthNpcTradeDirection)(int)root.Attribute("direction"),ItemKey=(string)root.Attribute("item"),
            CurrencyItemKey=(string)root.Attribute("currency"),Quantity=(int)root.Attribute("quantity"),
            UnitPrice=(int)root.Attribute("unit"),TotalPrice=(int)root.Attribute("total"),
            ExpectedNpcInventoryRevision=(uint)root.Attribute("npcRevision"),RecoveryGeneration=(ulong)root.Attribute("recovery"),
            ExpiresClockTicks=(long)root.Attribute("expires")
        };
    }
    // Equality is only a binding check against a separately authenticated
    // authority quote. It grants no payment, stock reservation or save receipt.
    internal bool MatchesQuote(RebirthNpcTradeQuote quote)
    {
        if(quote==null)return false;
        var frozen=CopyQuote();
        return quote.QuoteId==frozen.QuoteId&&quote.NpcId.ToString()==frozen.NpcId.ToString()&&
            string.Equals(quote.ActorId,frozen.ActorId,StringComparison.Ordinal)&&string.Equals(quote.EndpointId,frozen.EndpointId,StringComparison.Ordinal)&&
            quote.Direction==frozen.Direction&&string.Equals(quote.ItemKey,frozen.ItemKey,StringComparison.Ordinal)&&
            string.Equals(quote.CurrencyItemKey,frozen.CurrencyItemKey,StringComparison.Ordinal)&&quote.Quantity==frozen.Quantity&&
            quote.UnitPrice==frozen.UnitPrice&&quote.TotalPrice==frozen.TotalPrice&&quote.ExpectedNpcInventoryRevision==frozen.ExpectedNpcInventoryRevision&&
            quote.RecoveryGeneration==frozen.RecoveryGeneration&&quote.ExpiresClockTicks==frozen.ExpiresClockTicks;
    }
    internal bool MatchesSlots(ItemStack[] bag,ItemStack[] belt,bool after)
    {
        try{return (after?afterBag:beforeBag).SequenceEqual(RebirthCamperInventorySnapshot.Capture(bag))&&
            (after?afterBelt:beforeBelt).SequenceEqual(RebirthCamperInventorySnapshot.Capture(belt));}catch{return false;}
    }
}
