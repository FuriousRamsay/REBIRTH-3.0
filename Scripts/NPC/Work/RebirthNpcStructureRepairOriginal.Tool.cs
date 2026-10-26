using System;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

internal static partial class RebirthNpcStructureRepairOriginal
{
    private const string ToolReceiptElement="structureRepairToolReturned";
    private static XElement ToolReceipt(XElement row)=>new XElement(ToolReceiptElement,
        new XAttribute("operation",(string)row.Attribute("operation")),new XAttribute("binding",(string)row.Attribute("nativeBinding")),
        new XAttribute("stack",(string)row.Attribute("nativeToolStack")),new XAttribute("before",(string)row.Attribute("nativeToolBefore")),
        new XAttribute("after",(string)row.Attribute("nativeToolAfter")),new XElement(row.Element("nativeToolPolicyAfter")));

    // This snapshot is captured only while SavedDebit still compares the real
    // saved native ledger. It is retained before any tool mutation is entered.
    private static bool TryWearNativeTool(RebirthNpcConcreteWorkContext context)
    {
        try
        {
            var id=context.Assignment.NpcId;
            if(!NativeRead(id,context.OperationId,out var row)||(string)row.Attribute("phase")!="native-debit-attempted"||
                !NativeContext(context,row,out var npc,out _)||!SavedDebit(id,row)||!BagMatches(npc,row,true)||
                !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(id,out var inventory)||inventory.NativeStacks==null||inventory.Revision==uint.MaxValue)return false;
            var equipment=RebirthHumanEquipment.Read(npc);int slot=npc.inventory.SelectedSlot;
            if(npc.inventory.ItemGrid.SlotLocks!=null&&(slot>=npc.inventory.ItemGrid.SlotLocks.Length||npc.inventory.ItemGrid.SlotLocks[slot]))return false;
            var binding=equipment.Element("bindings")?.Elements("binding").SingleOrDefault(x=>(string)x.Attribute("area")=="toolbelt"&&(int)x.Attribute("index")==slot);
            if(binding==null||!Guid.TryParseExact((string)binding.Attribute("id"),"N",out var toolId)||toolId==Guid.Empty)return false;
            var before=npc.inventory.GetItem(slot);string beforeFrame=RebirthHumanEquipment.Frame(before);
            if(beforeFrame!=(string)row.Element("nativeRepairPolicy").Attribute("tool"))return false;
            var after=before.Clone();after.itemValue.UseTimes+=1f;
            if(float.IsNaN(after.itemValue.UseTimes)||float.IsInfinity(after.itemValue.UseTimes))return false;
            var toolNative=inventory.NativeStacks.Write().Elements().SingleOrDefault(x=>(string)x.Attribute("id")==toolId.ToString("N"));
            if(toolNative==null||!RebirthNpcNativeStackRecord.TryRead(toolNative,out var record)||record.Count!=before.count||record.Payload!=RebirthNativeItemCodec.Encode(before.itemValue))return false;
            var attempted=new XElement(row);attempted.SetAttributeValue("phase","native-tool-attempted");
            attempted.SetAttributeValue("nativeToolStack",toolId.ToString("N"));attempted.SetAttributeValue("nativeToolSlot",slot);
            attempted.SetAttributeValue("nativeToolBefore",beforeFrame);attempted.SetAttributeValue("nativeToolAfter",RebirthHumanEquipment.Frame(after));
            attempted.Add(new XElement("materialInventoryPublished",inventory.NativeStacks.Write()));
            // Reserve the immutable archive's existing byte bound before an
            // effect. Two native ledgers and returned policy are still owed.
            if(System.Text.Encoding.UTF8.GetByteCount(attempted.ToString(SaveOptions.DisableFormatting))+2L*System.Text.Encoding.UTF8.GetByteCount(inventory.NativeStacks.Write().ToString(SaveOptions.DisableFormatting))+131072L>6*1024*1024)return false;
            if(!NativeReplace(id,row,attempted)||!NativeContext(context,attempted,out npc,out _)||!BagMatches(npc,attempted,true))return false;
            // Actual held native ItemValue mutation. No player action is called
            // and no detached clone is installed as an apparent native return.
            var held=npc.inventory.holdingItemItemValue;
            if(!ReferenceEquals(held,npc.inventory.GetItem(slot).itemValue)||npc.inventory.SelectedSlot!=slot||RebirthHumanEquipment.Frame(npc.inventory.GetItem(slot))!=beforeFrame)return false;
            int actionIndex=(int)row.Element("nativeRepairPolicy").Attribute("action");
            var data=npc.inventory.holdingItemData.actionData[actionIndex] as ItemActionRepair.InventoryDataRepair;
            if(data==null||!ReferenceEquals(data.invData,npc.inventory.holdingItemData))return false;
            data.lastUseTime=Time.time;data.lastHitPosition=NativePosition(row);
            data.lastHitBlockValue=new BlockValue{rawData=(uint)row.Attribute("blockRaw"),damage=(ushort)(int)row.Attribute("damage")};
            data.lastRepairItems=data.lastHitBlockValue.Block.RepairItems;data.lastRepairItemsPercents=new float[]{0f};
            held.UseTimes+=1f;
            if(RebirthHumanEquipment.Frame(npc.inventory.GetItem(slot))!=(string)attempted.Attribute("nativeToolAfter")||
                !RebirthNpcSingleRepairPolicy.TryObserve(npc,NativePosition(row),data.lastHitBlockValue,true,out var policy,false))return false;
            policy.Name="nativeToolPolicyAfter";attempted.Add(policy);
            // Save a positive returned field-write receipt only after all exact
            // native writes returned. Any earlier entered prefix stays held.
            var returned=new XElement(attempted);returned.SetAttributeValue("nativeToolReturned",1);
            if(!NativeRead(id,context.OperationId,out var savedAttempt)||!NativeReplace(id,savedAttempt,returned))return false;
            if(!NativeContext(context,returned,out npc,out _)||!BagMatches(npc,returned,true))return false;
            equipment.Add(ToolReceipt(returned));RebirthHumanEquipment.Save(npc,equipment);
            if(!SavedTool(id,returned))return false;
            var published=new XElement(returned);published.SetAttributeValue("phase","native-tool-published");
            if(!RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(id,out var savedInventory)||savedInventory.NativeStacks==null)return false;
            published.Add(new XElement("toolInventoryPublished",savedInventory.NativeStacks.Write()));
            published.SetAttributeValue("nativeToolProof",RebirthRemoteResourceDurableJournal.Digest(ToolReceipt(published).ToString(SaveOptions.DisableFormatting)+"|"+published.Element("toolInventoryPublished").ToString(SaveOptions.DisableFormatting)));
            return NativeReplace(id,returned,published);
        }
        catch{return false;}
    }
    private static bool SavedTool(RebirthNpcStableId id,XElement row)
    {
        try
        {
            if((int?)row.Attribute("nativeToolReturned")!=1||row.Element("nativeToolPolicyAfter")==null||
                !RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var person)||
                !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(id,out var actual)||actual.NativeStacks==null)return false;
            var equipment=person.UnknownOptionalElements.Select(XElement.Parse).SingleOrDefault(x=>x.Name=="humanEquipment");
            if(!XNode.DeepEquals(equipment?.Elements(ToolReceiptElement).SingleOrDefault(x=>(string)x.Attribute("operation")== (string)row.Attribute("operation")),ToolReceipt(row)))return false;
            var expected=new XElement(row.Element("materialInventoryPublished").Elements().Single());uint revision=(uint)expected.Attribute("revision");
            if(revision==uint.MaxValue||actual.Revision!=revision+1)return false;
            var tool=expected.Elements().Single(x=>(string)x.Attribute("id")== (string)row.Attribute("nativeToolStack"));
            tool.SetAttributeValue("payload",RebirthNativeItemCodec.Encode(RebirthHumanEquipment.Decode((string)row.Attribute("nativeToolAfter")).itemValue));
            expected.SetAttributeValue("revision",actual.Revision);
            return XNode.DeepEquals(expected,actual.NativeStacks.Write());
        }
        catch{return false;}
    }
    private static bool PublishedTool(XElement row)
    {
        try
        {
            if((int?)row.Attribute("nativeToolReturned")!=1||row.Element("nativeToolPolicyAfter")==null||row.Element("toolInventoryPublished")==null)return false;
            var expected=new XElement(row.Element("materialInventoryPublished").Elements().Single());uint revision=(uint)expected.Attribute("revision");
            if(revision==uint.MaxValue)return false;
            expected.Elements().Single(x=>(string)x.Attribute("id")== (string)row.Attribute("nativeToolStack"))
                .SetAttributeValue("payload",RebirthNativeItemCodec.Encode(RebirthHumanEquipment.Decode((string)row.Attribute("nativeToolAfter")).itemValue));
            expected.SetAttributeValue("revision",revision+1);
            return XNode.DeepEquals(expected,row.Element("toolInventoryPublished").Elements().Single())&&
                (string)row.Attribute("nativeToolProof")==RebirthRemoteResourceDurableJournal.Digest(ToolReceipt(row).ToString(SaveOptions.DisableFormatting)+"|"+row.Element("toolInventoryPublished").ToString(SaveOptions.DisableFormatting));
        }
        catch{return false;}
    }
}
