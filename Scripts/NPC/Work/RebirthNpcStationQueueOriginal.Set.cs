using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

// Staged-set (multi-ingredient) support. Unit 0 stays in the row's original attributes (so single-unit rows and every
// existing reader are unchanged); units 1..N-1 are `unit` child elements, covered by the row Binding digest.
internal static partial class RebirthNpcStationQueueOriginal
{
    private sealed class Unit
    {
        internal ulong Assignment;internal Guid Operation;internal string Binding,Credit,Frame,Target;internal int BagSlot;internal long StartedTicks;
    }
    private static List<Unit> Units(XElement row)
    {
        var list=new List<Unit>{new Unit{Assignment=(ulong)row.Attribute("sourceAssignment"),Operation=Guid.ParseExact((string)row.Attribute("source"),"N"),
            Binding=(string)row.Attribute("sourceBinding"),Credit=(string)row.Attribute("credit"),Frame=(string)row.Attribute("frame"),
            Target=(string)row.Attribute("sourceTarget"),BagSlot=(int)row.Attribute("bagSlot"),StartedTicks=(long)row.Attribute("sourceStartedTicks")}};
        foreach(var x in row.Elements("unit"))
            list.Add(new Unit{Assignment=(ulong)x.Attribute("assignment"),Operation=Guid.ParseExact((string)x.Attribute("source"),"N"),
                Binding=(string)x.Attribute("binding"),Credit=(string)x.Attribute("credit"),Frame=(string)x.Attribute("frame"),
                Target=(string)x.Attribute("target"),BagSlot=(int)x.Attribute("bagSlot"),StartedTicks=(long)x.Attribute("startedTicks")});
        return list;
    }
    private static XElement UnitElement(RebirthNpcRecipeQueueWork.Resolved r)=>new XElement("unit",new XAttribute("assignment",r.Staged.AssignmentId),
        new XAttribute("source",r.Source.Operation.ToString("N")),new XAttribute("binding",r.Source.Binding),new XAttribute("credit",r.Source.CreditId.ToString("N")),
        new XAttribute("frame",r.Source.Frame),new XAttribute("bagSlot",r.Source.BagSlot),new XAttribute("startedTicks",r.Staged.CreatedUtcTicks),
        new XAttribute("target",r.Staged.TargetKey));
    private static bool UnitMatches(Unit u,RebirthNpcRecipeQueueWork.Resolved r)=>u.Assignment==r.Staged.AssignmentId&&u.Operation==r.Source.Operation&&
        u.Credit==r.Source.CreditId.ToString("N")&&u.Binding==r.Source.Binding&&u.Frame==r.Source.Frame&&u.BagSlot==r.Source.BagSlot&&
        u.StartedTicks==r.Staged.CreatedUtcTicks&&u.Target==r.Staged.TargetKey;
    // The row's unit ids must equal the assignment key's ids, in order.
    private static bool UnitsMatchKey(RebirthNpcWorkAssignment a,XElement row)=>RebirthNpcRecipeIngredientSet.TryParseKey(a.TargetKey,out var ids)&&
        Units(row).Select(x=>x.Assignment).SequenceEqual(ids);
    private static bool UnitsCurrent(EntityRebirthHumanoidNPC actual,RebirthNpcWorkAssignment a,XElement row,IList<RebirthNpcRecipeQueueWork.Resolved> set)
    {
        var units=Units(row);if(units.Count!=set.Count)return false;
        var locks=actual.bag.ItemGrid.SlotLocks;
        for(int i=0;i<units.Count;i++)
        {
            var u=units[i];
            if(u.BagSlot<0||u.BagSlot>=actual.bag.SlotCount||locks==null||u.BagSlot>=locks.Length||locks[u.BagSlot]||
                !RebirthNpcRecipeStagingWork.SavedStage(set[i].Staged,set[i].Source)||!UnitMatches(u,set[i])||
                RebirthCompanionInventoryLockService.IsNpcItemLocked(a.NpcId,RebirthHumanEquipment.Decode(u.Frame).itemValue.ItemClass.GetItemName()))return false;
        }
        return units.Select(x=>x.BagSlot).Distinct().Count()==units.Count;
    }
    // Native bag image with exactly the first `cleared` units' slots emptied (crash/permit checks while debiting in order).
    private static bool PhysicalCleared(EntityRebirthHumanoidNPC npc,XElement row,int cleared)
    {
        var expected=new XElement(row.Element("physical"));var units=Units(row);
        for(int i=0;i<cleared&&i<units.Count;i++)expected.Element("bag").Elements().ElementAt(units[i].BagSlot).SetAttributeValue("frame",string.Empty);
        return XNode.DeepEquals(expected,NativePhysical(npc));
    }
    // Receipt 0 is the original single-unit receipt; further units carry a `unit` index and their own credit.
    private static XElement ReceiptFor(XElement row,int index)
    {
        if(index==0)return Receipt(row);
        var unit=Units(row)[index];
        return new XElement(Debit,new XAttribute("operation",(string)row.Attribute("operation")),new XAttribute("binding",(string)row.Attribute("binding")),
            new XAttribute("credit",unit.Credit),new XAttribute("unit",index));
    }
    private static Dictionary<string,int> ConsumedMap(XElement row)
    {
        var map=new Dictionary<string,int>();
        foreach(var u in Units(row))
        {
            string name=RebirthHumanEquipment.Decode(u.Frame).itemValue.ItemClass.GetItemName();map.TryGetValue(name,out int n);map[name]=n+1;
        }
        return map;
    }
    // Read-only compact description of this NPC's saved queue originals for the Game Bridge (no mutation, no save):
    // operation|phase|units|outCount|pickupBag|creditSaved|awarded.
    internal static string[] DiagnosticRows(RebirthNpcStableId id)
    {
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var person))return Array.Empty<string>();
            return (person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse).Where(x=>x.Name==Element).Select(x=>
                (string)x.Attribute("operation")+"|"+(string)x.Attribute("phase")+"|"+(1+x.Elements("unit").Count())+"|"+((string)x.Attribute("outCount")??"1")+"|"+
                ((string)x.Attribute("pickupBag")??"")+"|"+((string)x.Attribute("creditSaved")!=null?"1":"0")+"|"+((string)x.Attribute("awarded")=="1"?"1":"0")).ToArray();
        }
        catch{return Array.Empty<string>();}
    }
    private static bool SameMap(Dictionary<string,int> a,Dictionary<string,int> b)=>a!=null&&b!=null&&a.Count==b.Count&&a.All(x=>b.TryGetValue(x.Key,out int n)&&n==x.Value);
}
