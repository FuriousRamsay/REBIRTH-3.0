using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

public static partial class RebirthNpcInventoryTransactionService
{
    // Authoritative native owner observation. Explicit slot bindings retain identity
    // through wear/ammo changes; a prepared exchange retains its displaced stack in escrow.
    internal static bool ObserveHumanEquipment(EntityRebirthHumanoidNPC npc,XElement equipment)
    {
        if(!ThreadManager.IsMainThread()||npc?.world?.IsRemote()!=false||!ReferenceEquals(npc.world,GameManager.Instance?.World)||
            !ReferenceEquals(npc.world.GetEntity(npc.entityId),npc)||equipment==null||!RebirthHumanEquipment.Human(npc)||
            RebirthP2NpcEquipmentRestore.IsRestorePending(npc))return false;
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();
        var id=npc.RebirthRuntimeState.StableId;
        lock(Sync)
        {
            var state=GetOrCreate(id);bool first=(string)equipment.Attribute("nativeAdopted")!="1";
            var bindings=equipment.Element("bindings");if(bindings==null)
            {
                bindings=new XElement("bindings");equipment.Add(bindings);
                if(RebirthNpcAggregatePersistenceStore.TryGet(id,out var person))
                    foreach(var previous in person.NativeReconstruction?.Write().Element("slots")?.Elements("slot")??Enumerable.Empty<XElement>())
                        bindings.Add(new XElement("binding",new XAttribute("area",(string)previous.Attribute("area")),new XAttribute("index",(int)previous.Attribute("index")),new XAttribute("id",(string)previous.Attribute("stack"))));
            }
            var old=new List<RebirthNpcNativeStackRecord>();
            foreach(var node in state.NativeStacks?.Write().Elements()??Enumerable.Empty<XElement>())
            {if(!RebirthNpcNativeStackRecord.TryRead(node,out var record))return false;old.Add(record);}
            var managed=new HashSet<Guid>(bindings.Elements("binding").Select(b=>Guid.Parse((string)b.Attribute("id"))));
            foreach(var node in equipment.Elements("structureRepairMaterialDebit"))
            {
                if(!RebirthNpcStructureRepairOriginal.TryReadDebitManagedStack(id,node,out var repairStack))return false;
                managed.Add(repairStack);
            }
            foreach(var node in equipment.Elements("npcStationQueueDebit"))
            {
                if(!RebirthNpcStationQueueOriginal.TryReadManagedDebit(id,node,out var queueStack))return false;
                if(queueStack!=Guid.Empty)managed.Add(queueStack);
            }
            foreach(var node in equipment.Elements("workCredit"))
            {
                if(RebirthNpcWorkNativeCreditOriginal.TryReadNode(node,out var bagCredit)&&bagCredit.Npc==id&&
                    RebirthNpcWorkNativeCreditOriginal.TryParseBagSource(bagCredit.Source,out var boundStack))
                {
                    // Bag-source credit: no transfer original exists; the bound bag stack id is the managed one.
                    if(bagCredit.Phase>=5)managed.Add(boundStack);
                    continue;
                }
                if(!RebirthNpcWorkNativeCreditOriginal.TryReadNode(node,out var credit)||credit.Npc!=id||
                    !RebirthNpcWorkNativeTransferOriginal.TryReadSaved(id,credit.SourceOperation,out var source)||
                    source.Phase!=4||source.BagSlot!=credit.BagSlot||source.Frame!=credit.Frame)return false;
                if(credit.Phase>=5)managed.Add(source.CreditId);
            }
            var originalSwap=equipment.Element("swap");
            if(Guid.TryParse((string)originalSwap?.Attribute("oldStack"),out var displaced)&&displaced!=Guid.Empty)managed.Add(displaced);
            var next=old.Where(r=>!managed.Contains(r.StackId)).ToList();var seen=new HashSet<Guid>(next.Select(r=>r.StackId));
            var physical=new HashSet<Guid>();
            foreach(var area in new[]{"toolbelt","equipment","bag"})
            {
                var items=area=="toolbelt"?npc.inventory.ItemGrid.items:area=="equipment"?npc.equipment.ItemGrid.items:npc.bag.ItemGrid.items;
                for(int i=0;i<items.Length;i++)
                {
                    var stack=items[i];if(stack==null||stack.IsEmpty())continue;
                    var binding=bindings.Elements("binding").SingleOrDefault(b=>(string)b.Attribute("area")==area&&(int)b.Attribute("index")==i);
                    Guid stackId;
                    if(binding==null)
                    {
                        var matching=old.Where(r=>!physical.Contains(r.StackId)&&!managed.Contains(r.StackId)&&r.Count==stack.count&&r.Payload==RebirthNativeItemCodec.Encode(stack.itemValue)).ToArray();
                        if(matching.Length>1)return false;
                        stackId=matching.Length==1?matching[0].StackId:Guid.NewGuid();
                        binding=new XElement("binding",new XAttribute("area",area),new XAttribute("index",i),new XAttribute("id",stackId.ToString("N")));bindings.Add(binding);
                        next.RemoveAll(r=>r.StackId==stackId);seen.Remove(stackId);
                    }
                    else stackId=Guid.Parse((string)binding.Attribute("id"));
                    if(!seen.Add(stackId)||!physical.Add(stackId)||!RebirthNpcNativeStackRecord.TryCapture(stackId,id,stack,out var record))return false;next.Add(record);
                }
            }
            var swap=equipment.Element("swap");
            if(swap!=null&&(string)swap.Attribute("done")!="1"&&!string.IsNullOrEmpty((string)swap.Element("after")))
            {
                var stackId=Guid.Parse((string)swap.Attribute("oldStack"));
                next.RemoveAll(r=>r.StackId==stackId);seen.Remove(stackId);
                if(!seen.Add(stackId)||!RebirthNpcNativeStackRecord.TryCapture(stackId,id,RebirthHumanEquipment.Decode((string)swap.Element("after")),out var escrow))return false;next.Add(escrow);
            }
            var beforeCounts=old.GroupBy(r=>r.ItemKey,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.Sum(r=>r.Count),StringComparer.OrdinalIgnoreCase);
            var afterCounts=next.GroupBy(r=>r.ItemKey,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.Sum(r=>r.Count),StringComparer.OrdinalIgnoreCase);
            var quantities=new Dictionary<string,int>(state.Quantities,StringComparer.OrdinalIgnoreCase);
            foreach(string key in beforeCounts.Keys.Union(afterCounts.Keys,StringComparer.OrdinalIgnoreCase))
            {
                quantities.TryGetValue(key,out int quantity);beforeCounts.TryGetValue(key,out int before);afterCounts.TryGetValue(key,out int after);
                int desired=first?Math.Max(quantity,after):checked(quantity+after-before);
                state.Reservations.TryGetValue(key,out int reserved);if(desired<after||desired<(long)reserved+after)return false;
                if(desired==0)quantities.Remove(key);else quantities[key]=desired;
            }
            if(!RebirthNpcNativeStackSet.TryCreate(id,state.Revision,next,out var same))return false;
            bool changed=(state.NativeStacks?.Write().ToString(SaveOptions.DisableFormatting)??"")!=same.Write().ToString(SaveOptions.DisableFormatting)||
                quantities.Count!=state.Quantities.Count||quantities.Any(p=>!state.Quantities.TryGetValue(p.Key,out int count)||count!=p.Value);
            if(changed)
            {
                if(!RebirthNpcNativeStackSet.TryCreate(id,state.Revision+1,next,out var replacement)||!replacement.FitsQuantities(quantities))return false;
                state.Quantities.Clear();foreach(var pair in quantities)state.Quantities.Add(pair.Key,pair.Value);
                state.Revision++;state.NativeStacks=replacement;generationEpoch=new object();RebirthNpcInventoryPersistenceStore.MarkDirty();
            }
            equipment.SetAttributeValue("nativeAdopted",1);return true;
        }
    }
}
