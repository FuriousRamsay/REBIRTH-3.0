using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Per-original native inventory images for the existing vehicle offer. These
// images authorize no world mutation and are never substituted for native PDS.
internal static class RebirthVehicleOwnerOriginal
{
    internal sealed class Original
    {
        internal ItemStack[] BeforeBag,BeforeBelt,AfterBag,AfterBelt;
        internal RebirthVehicleTransferItem[] Items;
        internal string World,Owner,Creation;internal int Deaths;
        internal RebirthCamperTransferJournal Journal;internal Guid Operation,Transfer;
    }
    private static byte[] Slots(ItemStack[] slots)=>RebirthCamperInventorySnapshot.Capture(slots);
    private static XElement Frame(RebirthVehicleTransferPlan plan,EntityPlayerLocal player,RebirthVehicleTransferItem[] items,ItemStack[] bag,ItemStack[] belt)
        =>new XElement("vehicleOwner",new XAttribute("version",1),new XAttribute("world",player.world.Guid),new XAttribute("owner",plan.ActorId),
            new XAttribute("creation",plan.CreationId),new XAttribute("deaths",player.Died),new XAttribute("transfer",plan.TransactionId),
            new XElement("items",items.Select(item=>item.ToXml())),new XElement("bag",Convert.ToBase64String(Slots(bag))),new XElement("belt",Convert.ToBase64String(Slots(belt))));
    private static byte[] Bytes(XElement element)=>Encoding.UTF8.GetBytes(element.ToString(SaveOptions.DisableFormatting));
    private static XElement Read(byte[] bytes)
    {
        if(bytes==null||bytes.Length<1||bytes.Length>16*1024*1024)throw new InvalidDataException();
        using(var stream=new MemoryStream(bytes))using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16*1024*1024}))
        {var frame=XElement.Load(reader);if(frame.Name!="vehicleOwner"||(int)frame.Attribute("version")!=1||frame.Attributes().Count()!=6||frame.Elements().Count()!=3||!Bytes(frame).SequenceEqual(bytes))throw new InvalidDataException();return frame;}
    }
    private static string PathFor(EntityPlayerLocal player,RebirthVehicleTransferPlan plan,Guid operation)
    {
        if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.CanonicalId!=plan.ActorId||!Guid.TryParse(player.world.Guid,out var world))throw new InvalidDataException();
        // The original nested path can exceed Mono's Windows MAX_PATH once the
        // journal appends its receipt and temporary-file suffix. Continue any
        // existing original there, but create new originals at the short path.
        string legacy=Path.GetFullPath(Path.Combine(GameIO.GetPlayerDataDir(),"REBIRTH","VehicleOwnerOriginals",world.ToString("N"),owner.StorageKey));
        string path=Path.GetFullPath(Path.Combine(GameIO.GetPlayerDataDir(),"RVO",owner.StorageKey));
        if(Directory.Exists(legacy)&&Directory.EnumerateFiles(legacy,operation.ToString("N")+"*").Any())path=legacy;
        for(var ancestor=new DirectoryInfo(path);ancestor!=null;ancestor=ancestor.Parent)
            if(ancestor.Exists&&(ancestor.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Vehicle owner path is redirected.");
        return path;
    }
    internal static bool TryRead(EntityPlayerLocal player,RebirthVehicleTransferPlan plan,RebirthVehicleTransferItem[] items,out Original original)
    {
        original=null;
        try
        {
            if(!ThreadManager.IsMainThread()||!RebirthVehicleOwnerCustody.Matches(player,plan)||items==null||items.Length==0)return false;
            var journal=new RebirthCamperTransferJournal(PathFor(player,plan,items[0].ReceiptId));var record=journal.Read(items[0].ReceiptId);
            if(record==null)return true;
            var before=Read(record.Before);var after=Read(record.After);
            bool Header(XElement frame)=>frame.Attribute("world")?.Value==player.world.Guid&&frame.Attribute("owner")?.Value==plan.ActorId&&frame.Attribute("creation")?.Value==plan.CreationId&&
                (int)frame.Attribute("deaths")==player.Died&&Guid.Parse(frame.Attribute("transfer").Value)==plan.TransactionId&&
                frame.Element("items").ToString(SaveOptions.DisableFormatting)==new XElement("items",items.Select(item=>item.ToXml())).ToString(SaveOptions.DisableFormatting);
            if(!Header(before)||!Header(after)||record.Vehicle!=plan.TransactionId||record.OwnerCreation!=plan.OwnerScope||record.Revision!=player.Died)return false;
            original=new Original{Items=items,World=player.world.Guid,Owner=plan.ActorId,Creation=plan.CreationId,Deaths=player.Died,Journal=journal,Operation=items[0].ReceiptId,Transfer=plan.TransactionId,
                BeforeBag=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(before.Element("bag").Value)),BeforeBelt=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(before.Element("belt").Value)),
                AfterBag=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(after.Element("bag").Value)),AfterBelt=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(after.Element("belt").Value))};
            return ValidDelta(player,original)&&RebirthVehicleOwnerCustody.Matches(player,plan);
        }
        catch{return false;}
    }
    private static bool ValidDelta(EntityPlayerLocal player,Original original)
    {
        if(original.Items.Any(item=>!item.Debit))
        {
            if(original.Items.Length!=1||original.Items[0].Debit)return false;
            return RebirthVehicleOwnerCreditPlan.Prepare(original.BeforeBag,original.BeforeBelt,RebirthToolbeltCapacity.GetOwnedSlotCount(player,original.BeforeBelt.Length),original.Items[0],out var bag,out var belt,
                index=>RebirthVehicleNativePolicy.IsLocked(player,true,index),index=>RebirthVehicleNativePolicy.IsLocked(player,false,index))==RebirthVehicleInventoryPlanResult.Ready&&
                RebirthVehicleInventoryWrite.SameSlots(bag,original.AfterBag)&&RebirthVehicleInventoryWrite.SameSlots(belt,original.AfterBelt);
        }
        var afterBag=RebirthVehicleInventoryWrite.Snapshot(original.BeforeBag);var afterBelt=RebirthVehicleInventoryWrite.Snapshot(original.BeforeBelt);
        if(original.Items.Select(item=>item.Source).Distinct().Count()!=1||original.Items.Select(item=>item.Slot).Distinct().Count()!=original.Items.Length)return false;
        foreach(var item in original.Items)
        {
            var cells=item.Source==RebirthVehiclePartSourceLocation.Backpack?afterBag:afterBelt;
            if(item.Slot<0||item.Slot>=cells.Length||RebirthVehicleNativePolicy.IsLocked(player,item.Source==RebirthVehiclePartSourceLocation.Backpack,item.Slot)||
                item.Source==RebirthVehiclePartSourceLocation.Toolbelt&&item.Slot>=RebirthToolbeltCapacity.GetOwnedSlotCount(player,cells.Length)||
                cells[item.Slot].IsEmpty()||cells[item.Slot].count<item.Count||RebirthNativeItemCodec.Encode(cells[item.Slot].itemValue)!=item.ItemData)return false;
            cells[item.Slot].count-=item.Count;if(cells[item.Slot].count==0)cells[item.Slot]=ItemStack.Empty.Clone();
        }
        return RebirthVehicleInventoryWrite.SameSlots(afterBag,original.AfterBag)&&RebirthVehicleInventoryWrite.SameSlots(afterBelt,original.AfterBelt);
    }
    internal static bool Compatible(ItemStack[] before,ItemStack[] after,ItemStack[] actual)
    {
        if(before==null||after==null||actual==null||before.Length!=after.Length||before.Length!=actual.Length)return false;
        for(int index=0;index<before.Length;index++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(before[index],actual[index])&&!RebirthStationGridIngredients.IsSameStackSnapshot(after[index],actual[index]))return false;
        return true;
    }
    internal static bool Prepare(EntityPlayerLocal player,RebirthVehicleTransferPlan plan,RebirthVehicleTransferItem[] items,bool bag,ItemStack[] candidate)
        =>PrepareAll(player,plan,items,bag?candidate:player.bag.ItemGrid.items,bag?player.inventory.ItemGrid.items:candidate);
    internal static bool PrepareAll(EntityPlayerLocal player,RebirthVehicleTransferPlan plan,RebirthVehicleTransferItem[] items,ItemStack[] afterBag,ItemStack[] afterBelt)
    {
        try
        {
            if(!TryRead(player,plan,items,out var retained))
            {Log.Warning("[REBIRTH Vehicle Recovery] original id="+plan.TransactionId+" retained read refused");return false;}
            if(retained!=null)return RebirthVehicleInventoryWrite.SameSlots(retained.BeforeBag,player.bag.ItemGrid.items)&&RebirthVehicleInventoryWrite.SameSlots(retained.BeforeBelt,player.inventory.ItemGrid.items)&&
                RebirthVehicleInventoryWrite.SameSlots(retained.AfterBag,afterBag)&&RebirthVehicleInventoryWrite.SameSlots(retained.AfterBelt,afterBelt);
            var beforeBag=RebirthVehicleInventoryWrite.Snapshot(player.bag.ItemGrid.items);var beforeBelt=RebirthVehicleInventoryWrite.Snapshot(player.inventory.ItemGrid.items);
            var before=Frame(plan,player,items,beforeBag,beforeBelt);var after=Frame(plan,player,items,afterBag,afterBelt);
            var journal=new RebirthCamperTransferJournal(PathFor(player,plan,items[0].ReceiptId));
            var record=journal.Prepare(items[0].ReceiptId,plan.TransactionId,plan.OwnerScope,player.Died,Bytes(before),Bytes(after));
            bool equal=record.Before.SequenceEqual(Bytes(before))&&record.After.SequenceEqual(Bytes(after));
            bool custody=RebirthVehicleOwnerCustody.Matches(player,plan);
            bool bagSame=RebirthVehicleInventoryWrite.SameSlots(beforeBag,player.bag.ItemGrid.items);
            bool beltSame=RebirthVehicleInventoryWrite.SameSlots(beforeBelt,player.inventory.ItemGrid.items);
            if(!equal||!custody||!bagSame||!beltSame)Log.Warning("[REBIRTH Vehicle Recovery] original id="+plan.TransactionId+" journal="+equal+" custody="+custody+" bag="+bagSame+" belt="+beltSame);
            return equal&&custody&&
                RebirthVehicleInventoryWrite.SameSlots(beforeBag,player.bag.ItemGrid.items)&&RebirthVehicleInventoryWrite.SameSlots(beforeBelt,player.inventory.ItemGrid.items);
        }
        catch(Exception error){Log.Warning("[REBIRTH Vehicle Recovery] original prepare threw "+error);return false;}
    }
    internal static bool ResolveApplying(EntityPlayerLocal player,RebirthVehicleTransferPlan plan,RebirthVehicleTransferItem[] items,out bool retryBefore)
    {
        retryBefore=false;
        try
        {
            if(!TryRead(player,plan,items,out var original)||original==null||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||
                !RebirthGearNativePlayerFile.TryRead(identity,out var file)||file.deaths!=original.Deaths||file.buffData==null||file.buffData.Length<1||file.buffData.Length>1024*1024||
                !RebirthVehicleReceiptReader.TryRead(file.buffData.ToArray(),items.Select(item=>item.ReceiptId),out var receipts)||
                items.Any(item=>receipts[item.ReceiptId]==RebirthVehicleOwnerTransferResult.Rejected))return false;
            bool Exact(ItemStack[] bag,ItemStack[] belt)=>RebirthVehicleInventoryWrite.SameSlots(bag,player.bag.ItemGrid.items)&&RebirthVehicleInventoryWrite.SameSlots(belt,player.inventory.ItemGrid.items)&&
                RebirthVehicleInventoryWrite.SameSlots(bag,RebirthGearPlayerDataInventory.ReadSlots(file,true))&&RebirthVehicleInventoryWrite.SameSlots(belt,RebirthGearPlayerDataInventory.ReadSlots(file,false))&&RebirthVehicleOwnerCustody.Matches(player,plan);
            if(Exact(original.BeforeBag,original.BeforeBelt))
            {
                if(items.Any(item=>receipts[item.ReceiptId]==RebirthVehicleOwnerTransferResult.Applied||player.Buffs.GetCustomVar("rbVehicle_"+item.ReceiptId.ToString("N"))==1f))return false;
                foreach(var item in items)
                {float stage=player.Buffs.GetCustomVar("rbVehicle_"+item.ReceiptId.ToString("N"));if(stage!=0f&&stage!=2f)return false;}
                foreach(var item in items)player.Buffs.SetCustomVar("rbVehicle_"+item.ReceiptId.ToString("N"),2f,true);
                bool LiveBefore()=>RebirthVehicleOwnerCustody.Matches(player,plan)&&RebirthVehicleInventoryWrite.SameSlots(original.BeforeBag,player.bag.ItemGrid.items)&&RebirthVehicleInventoryWrite.SameSlots(original.BeforeBelt,player.inventory.ItemGrid.items);
                if(!RebirthVehicleOwnerCheckpoint.Persist(player,items.Select(item=>item.ReceiptId),2f,LiveBefore))return false;
                retryBefore=true;return true;
            }
            if(!Exact(original.AfterBag,original.AfterBelt))
            {
                // The immutable native original identifies every cell, including
                // unchanged cells. Only an actual saved applying marker permits
                // completion of a prefix interrupted between native setters.
                bool CompatibleNow()=>RebirthVehicleOwnerCustody.Matches(player,plan)&&
                    Compatible(original.BeforeBag,original.AfterBag,player.bag.ItemGrid.items)&&
                    Compatible(original.BeforeBelt,original.AfterBelt,player.inventory.ItemGrid.items);
                bool liveAfter=RebirthVehicleInventoryWrite.SameSlots(original.AfterBag,player.bag.ItemGrid.items)&&
                    RebirthVehicleInventoryWrite.SameSlots(original.AfterBelt,player.inventory.ItemGrid.items);
                if(!Compatible(original.BeforeBag,original.AfterBag,RebirthGearPlayerDataInventory.ReadSlots(file,true))||
                    !Compatible(original.BeforeBelt,original.AfterBelt,RebirthGearPlayerDataInventory.ReadSlots(file,false))||!CompatibleNow()||
                    items.Any(item=>liveAfter?
                        receipts[item.ReceiptId]==RebirthVehicleOwnerTransferResult.Pending||
                        (player.Buffs.GetCustomVar("rbVehicle_"+item.ReceiptId.ToString("N"))!=1f&&player.Buffs.GetCustomVar("rbVehicle_"+item.ReceiptId.ToString("N"))!=2f):
                        receipts[item.ReceiptId]!=RebirthVehicleOwnerTransferResult.Indeterminate||
                        player.Buffs.GetCustomVar("rbVehicle_"+item.ReceiptId.ToString("N"))!=2f))return false;
                // Persist the observed prefix before finishing its remaining
                // absolute postimages. A third native value stays indeterminate.
                if(!liveAfter&&!RebirthVehicleOwnerCheckpoint.Persist(player,items.Select(item=>item.ReceiptId),2f,CompatibleNow))return false;
                if(!RebirthVehicleInventoryWrite.SameSlots(original.AfterBag,player.bag.ItemGrid.items))
                    RebirthVehicleOwnerCustody.WriteSlots(player,true,RebirthVehicleInventoryWrite.Snapshot(original.AfterBag),items,CompatibleNow);
                if(!CompatibleNow())return false;
                if(!RebirthVehicleInventoryWrite.SameSlots(original.AfterBelt,player.inventory.ItemGrid.items))
                    RebirthVehicleOwnerCustody.WriteSlots(player,false,RebirthVehicleInventoryWrite.Snapshot(original.AfterBelt),items,CompatibleNow);
                if(!RebirthVehicleInventoryWrite.SameSlots(original.AfterBag,player.bag.ItemGrid.items)||
                    !RebirthVehicleInventoryWrite.SameSlots(original.AfterBelt,player.inventory.ItemGrid.items)||!CompatibleNow())return false;
            }
            if(items.Any(item=>receipts[item.ReceiptId]==RebirthVehicleOwnerTransferResult.Pending||
                (player.Buffs.GetCustomVar("rbVehicle_"+item.ReceiptId.ToString("N"))!=1f&&player.Buffs.GetCustomVar("rbVehicle_"+item.ReceiptId.ToString("N"))!=2f)))return false;
            foreach(var item in items)player.Buffs.SetCustomVar("rbVehicle_"+item.ReceiptId.ToString("N"),1f,true);
            bool LiveAfter()=>RebirthVehicleOwnerCustody.Matches(player,plan)&&RebirthVehicleInventoryWrite.SameSlots(original.AfterBag,player.bag.ItemGrid.items)&&RebirthVehicleInventoryWrite.SameSlots(original.AfterBelt,player.inventory.ItemGrid.items);
            return RebirthVehicleOwnerCheckpoint.Persist(player,items.Select(item=>item.ReceiptId),1f,LiveAfter);
        }
        catch{return false;}
    }
}
