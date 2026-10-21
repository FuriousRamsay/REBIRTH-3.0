using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class AdvancedFarmingWaterExchangeOriginal
{
    private sealed class SlotPublication:IDisposable
    {
        internal Attempt Attempt;internal ItemStackGrid Grid;internal ItemStack Value;
        internal int Slot,Deaths,AttemptIndex;internal bool Bag,Entered,Observed;
        internal string From,To,BeforeBag,BeforeBelt,AfterBag,AfterBelt,Cursor;
        internal IDisposable Memory;
        public void Dispose(){Attempt=null;Grid=null;Value=null;From=To=BeforeBag=BeforeBelt=AfterBag=AfterBelt=Cursor=null;Memory?.Dispose();Memory=null;}
    }
    private static string StackImage(ItemStack value)=>value==null?"null":value.count+":"+Native(value.itemValue);
    private static string GridImage(ItemStack[] values)
    {
        if(values==null||values.Length>512)throw new InvalidDataException();
        var image=new StringBuilder();image.Append(values.Length).Append('|');
        foreach(var value in values){string text=StackImage(value);if(image.Length+text.Length>Maximum)throw new InvalidDataException();image.Append(text.Length).Append(':').Append(text);}
        return image.ToString();
    }
    private static string PlannedGrid(ItemStack[] values,int slot,ItemStack replacement)
    {var clone=(ItemStack[])values.Clone();if(slot<0||slot>=clone.Length)throw new InvalidDataException();clone[slot]=replacement;return GridImage(clone);}
    private static bool BeginSlotPublication(Attempt a,string from,string to,ItemStack value)
    {
        IDisposable memory=null;bool admitted=false;
        try
        {
            if(from==a.Record.Reserved&&to==a.Record.Output&&ReadSlot(a.Record,to,out _))return false;
            if(a.PendingSlot?.Observed==true&&!PublishSlot(a,a.PendingSlot.To)){admitted=true;return false;}
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out memory))return false;
            a.PendingSlot?.Dispose();
            var bag=a.Player.bag.ItemGrid.items;var belt=a.Player.inventory.ItemGrid.items;
            a.PendingSlot=new SlotPublication{Attempt=a,Grid=a.CustodyBag?a.Player.bag.ItemGrid:a.Player.inventory.ItemGrid,Value=value,
                Slot=Slot(a),Bag=a.CustodyBag,Deaths=a.Player.Died,From=from,To=to,BeforeBag=GridImage(bag),BeforeBelt=GridImage(belt),
                AfterBag=a.CustodyBag?PlannedGrid(bag,Slot(a),value):GridImage(bag),
                AfterBelt=a.CustodyBag?GridImage(belt):PlannedGrid(belt,Slot(a),value),Cursor=StackImage(a.Player.DragAndDropItem),Memory=memory};memory=null;
            var call=a.PendingSlot;
            for(int index=0;index<64;index++)
            {
                string path=SlotPath(a.Record,to)+".attempt."+index;
                if(File.Exists(path+".tmp"))return false;
                if(File.Exists(path))
                {
                    if(new FileInfo(path).Length>18L*Maximum)return false;string old=File.ReadAllText(path,new UTF8Encoding(false,true));
                    bool ExactReceipt(string suffix)=>File.Exists(path+suffix)&&!File.Exists(path+suffix+".tmp")&&new FileInfo(path+suffix).Length<=18L*Maximum&&File.ReadAllText(path+suffix,new UTF8Encoding(false,true))==old;
                    if(!ExactReceipt(".unused")&&!ExactReceipt(".accepted"))return false;
                    continue;
                }
                call.AttemptIndex=index;admitted=WriteSlot(path,SlotImage(call).ToString(SaveOptions.DisableFormatting),false);return admitted;
            }
            return false;
        }
        catch{return false;}
        finally{memory?.Dispose();if(!admitted){a.PendingSlot?.Dispose();a.PendingSlot=null;}}
    }
    internal static object EnterSlotPublication(ItemStackGrid grid,int slot,ItemStack value)
    {
        var a=permittedAttempt;var call=a?.PendingSlot;
        if(call==null||call.Entered||!Current(a)||!ReferenceEquals(call.Grid,grid)||call.Slot!=slot||!ReferenceEquals(call.Value,value)||
            call.BeforeBag!=GridImage(a.Player.bag.ItemGrid.items)||call.BeforeBelt!=GridImage(a.Player.inventory.ItemGrid.items)||call.Cursor!=StackImage(a.Player.DragAndDropItem))return null;
        call.Entered=true;return call;
    }
    internal static void ObserveSlotPublication(ItemStackGrid grid,int slot,ItemStack value,bool run,object state)
    {
        if(state is not SlotPublication call)return;
        try
        {
            var a=call.Attempt;
            if(!run)
            {
                if(ReferenceEquals(a.PendingSlot,call)&&Current(a)&&call.BeforeBag==GridImage(a.Player.bag.ItemGrid.items)&&
                    call.BeforeBelt==GridImage(a.Player.inventory.ItemGrid.items)&&call.Cursor==StackImage(a.Player.DragAndDropItem))
                    WriteSlot(SlotPath(a.Record,call.To)+".attempt."+call.AttemptIndex+".unused",SlotImage(call).ToString(SaveOptions.DisableFormatting),false);
                return;
            }
            if(!ReferenceEquals(a.PendingSlot,call)||!Current(a)||!call.Entered||!ReferenceEquals(grid,call.Grid)||slot!=call.Slot||!ReferenceEquals(value,call.Value)||
                call.AfterBag!=GridImage(a.Player.bag.ItemGrid.items)||call.AfterBelt!=GridImage(a.Player.inventory.ItemGrid.items)||
                call.Cursor!=StackImage(a.Player.DragAndDropItem)||!Exact(Held(a),call.To,a.Record.Count))return;
            call.Observed=true;
        }
        catch{/* A skipped or uncertain setter cannot become a publication. */}
    }
    private static string SlotPath(Record r,string to)=>PathFor(r,false)+".slot."+Hash(Encoding.UTF8.GetBytes(to));
    private static XElement SlotImage(SlotPublication call)=>new XElement("waterNativeSlot",new XAttribute("version",1),
        new XAttribute("operation",call.Attempt.Record.Operation.ToString("N")),new XAttribute("binding",Binding(call.Attempt.Record)),
        new XAttribute("deaths",call.Deaths),new XAttribute("bag",call.Bag),new XAttribute("slot",call.Slot),new XAttribute("attempt",call.AttemptIndex),
        new XElement("from",call.From),new XElement("to",call.To),new XElement("bagBefore",call.BeforeBag),new XElement("beltBefore",call.BeforeBelt),
        new XElement("bagAfter",call.AfterBag),new XElement("beltAfter",call.AfterBelt),new XElement("cursor",call.Cursor));
    private static bool WriteSlot(string path,string image,bool terminalMetadata)
    {
        string temporary=path+".tmp";
        try
        {
            if(image.Length>6L*Maximum)return false;
            if(File.Exists(path)){if(File.ReadAllText(path,new UTF8Encoding(false,true))==image)return true;if(!terminalMetadata)return false;}
            if(File.Exists(temporary))return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path));byte[] bytes=new UTF8Encoding(false,true).GetBytes(image);
            using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
            return File.ReadAllText(temporary,new UTF8Encoding(false,true))==image&&RebirthDurableFileCommit.TryPublish(temporary,path,out _)&&File.ReadAllText(path,new UTF8Encoding(false,true))==image;
        }
        catch{return false;}
    }
    private static bool PublishSlot(Attempt a,string to)
    {
        var call=a.PendingSlot;
        if(call==null||!call.Observed||call.To!=to||!Current(a)||call.Deaths!=a.Player.Died||
            call.AfterBag!=GridImage(a.Player.bag.ItemGrid.items)||call.AfterBelt!=GridImage(a.Player.inventory.ItemGrid.items)||call.Cursor!=StackImage(a.Player.DragAndDropItem))return false;
        bool metadata=a.Record.State==3&&call.From==a.Record.Reserved&&to==a.Record.Before;
        if(a.Record.State==2&&call.From==a.Record.Output){var clean=Item(a.Record.Output);clean.RemoveMetaData(Marker);metadata=to==Native(clean);}
        string image=SlotImage(call).ToString(SaveOptions.DisableFormatting);
        if(!WriteSlot(SlotPath(a.Record,to)+".attempt."+call.AttemptIndex+".accepted",image,false)||!WriteSlot(SlotPath(a.Record,to),image,metadata))return false;
        a.PendingSlot=null;call.Dispose();return true;
    }
    private static bool ReadSlot(Record r,string to,out XElement image)
    {
        image=null;try
        {
            string path=SlotPath(r,to);if(File.Exists(path+".tmp")||new FileInfo(path).Length>18L*Maximum)return false;
            using(var text=new StringReader(File.ReadAllText(path,new UTF8Encoding(false,true))))using(var reader=XmlReader.Create(text,
                new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=6L*Maximum}))image=XElement.Load(reader);
            return image.Name=="waterNativeSlot"&&(int?)image.Attribute("version")==1&&(string)image.Attribute("operation")==r.Operation.ToString("N")&&
                (string)image.Attribute("binding")==Binding(r)&&(string)image.Element("to")==to;
        }
        catch{image=null;return false;}
    }
    private static bool HasSlotPublication(Attempt a,string to)
    {
        if(a.PendingSlot?.Observed==true&&a.PendingSlot.To==to&&!PublishSlot(a,to))return false;
        if(!ReadSlot(a.Record,to,out var image)||!Exact(Held(a),to,a.Record.Count))return false;
        // A positively reclaimed whole marked original retains its historical
        // setter event; the new slot/life does not claim a fresh conversion.
        if((to==a.Record.Reserved||to==a.Record.Output)&&Item(to).TryGetMetadata(Marker,out string marker)&&marker==a.Record.Operation.ToString("N")&&
            (int?)image.Attribute("deaths") is int originalDeaths&&originalDeaths<=a.Player.Died&&TryLocateCustody(a.Record,a.Player.bag.ItemGrid.items,a.Player.inventory.ItemGrid.items,a.Player.DragAndDropItem,out bool bag,out int slot)&&
            bag==a.CustodyBag&&slot==Slot(a))return true;
        return (int?)image.Attribute("deaths")==a.Player.Died&&(bool?)image.Attribute("bag")==a.CustodyBag&&(int?)image.Attribute("slot")==Slot(a)&&
            (string)image.Element("bagAfter")==GridImage(a.Player.bag.ItemGrid.items)&&(string)image.Element("beltAfter")==GridImage(a.Player.inventory.ItemGrid.items)&&
            (string)image.Element("cursor")==StackImage(a.Player.DragAndDropItem);
    }
    private static bool RebindTerminalCleanup(Attempt a)
    {
        if(a.Record.State<2||!SameOwner(a.Player,a.Record))return false;
        var clean=Item(a.Record.State==2?a.Record.Output:a.Record.Before);if(a.Record.State==2)clean.RemoveMetaData(Marker);
        string target=Native(clean);if(!ReadSlot(a.Record,target,out var proof)||(int?)proof.Attribute("deaths")!=a.Player.Died||
            (string)proof.Element("bagAfter")!=GridImage(a.Player.bag.ItemGrid.items)||(string)proof.Element("beltAfter")!=GridImage(a.Player.inventory.ItemGrid.items)||
            (string)proof.Element("cursor")!=StackImage(a.Player.DragAndDropItem))return false;
        bool bag=(bool)proof.Attribute("bag");int slot=(int)proof.Attribute("slot");var rows=bag?a.Player.bag.ItemGrid.items:a.Player.inventory.ItemGrid.items;
        if(slot<0||slot>=rows.Length||!Exact(rows[slot],target,a.Record.Count))return false;
        a.CustodyBag=bag;a.CustodySlot=slot;a.CustodyDeaths=a.Player.Died;return true;
    }
    private static bool SavedTerminalCleanup(Attempt a,string to)
    {
        if(!Current(a)||!HasSlotPublication(a,to)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.CanonicalId!=a.Record.Owner||
            !RebirthCamperEnergySavedOwner.TryRead(owner,out var actor,out var file)||file.id!=a.Player.entityId||file.deaths!=a.Player.Died||
            !Buff(file,a.Record,out float phase)||phase!=3f||!ReadSlot(a.Record,to,out var proof))return false;
        return GridImage(actor.Bag)==(string)proof.Element("bagAfter")&&GridImage(actor.Belt)==(string)proof.Element("beltAfter")&&
            StackImage(actor.Cursor)==(string)proof.Element("cursor");
    }
}
