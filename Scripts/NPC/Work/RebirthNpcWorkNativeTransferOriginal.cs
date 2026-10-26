using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// A saved humanEquipment child is the original for one exact static-container
// debit into one native human bag cell. The source lease owns all source writes.
// This class only validates and publishes the original's phase transitions.
internal static class RebirthNpcWorkNativeTransferOriginal
{
    // Native source custody asks for the saved original several times per
    // transition. Resolve its owner NPC once per world, then always re-read
    // the saved record; this cache is only an index, never authority.
    private static World indexedWorld;
    private static string indexedDirectory;
    private static readonly Dictionary<Guid,RebirthNpcStableId> indexedNpc=new Dictionary<Guid,RebirthNpcStableId>();
    private static void ScopeIndex()
    {
        var world=GameManager.Instance?.World;
        string directory=GameIO.GetSaveGameDir();
        if(ReferenceEquals(indexedWorld,world)&&indexedDirectory==directory)return;
        indexedWorld=world;indexedDirectory=directory;indexedNpc.Clear();
    }
    internal sealed class Record
    {
        internal Guid Operation,CreditId;
        internal RebirthNpcStableId Npc;
        internal string Owner,Creation,World,Source,Incarnation,Frame,Binding,SourceProof;
        internal int Deaths,SourceSlot,BagSlot,Phase,Attempted,Published;
        internal ItemStack[] Before,After;
        internal bool[] Locks;
    }
    private static string Image(ItemStack[] value)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(value));
    private static bool Same(ItemStack a,ItemStack b)=>RebirthStationGridIngredients.IsSameStackSnapshot(a,b);
    private static string Digest(string text)=>RebirthRemoteResourceDurableJournal.Digest(text);
    private static string Immutable(Record r)=>string.Join("|",new[]{r.Operation.ToString("N"),r.CreditId.ToString("N"),r.Npc.ToString(),r.Owner,r.Creation,r.World,
        r.Deaths.ToString(System.Globalization.CultureInfo.InvariantCulture),r.Source,r.Incarnation,
        r.SourceSlot.ToString(System.Globalization.CultureInfo.InvariantCulture),r.BagSlot.ToString(System.Globalization.CultureInfo.InvariantCulture),
        r.Frame,Image(r.Before),Image(r.After),new string(r.Locks.Select(x=>x?'1':'0').ToArray())});
    internal static bool Valid(Record r)
    {
        try
        {
            if(r==null||r.Operation==Guid.Empty||r.CreditId==Guid.Empty||r.Npc.IsEmpty||r.Owner?.Length!=64||r.World?.Length!=32||
                !RebirthOriginalActionScope.Validate(r.Owner,r.Creation)||r.Deaths<0||r.Source==null||r.Source.Length>512||
                !r.Source.StartsWith("S:",StringComparison.Ordinal)||!Guid.TryParseExact(r.Incarnation,"N",out var token)||token==Guid.Empty||
                r.Before==null||r.After==null||r.Locks==null||r.Before.Length<1||r.Before.Length>256||
                r.Before.Length!=r.After.Length||r.Locks.Length!=r.Before.Length||r.SourceSlot<0||r.SourceSlot>=r.Before.Length||
                r.BagSlot<0||r.BagSlot>168||r.Locks[r.SourceSlot]||r.Phase<0||r.Phase>4||
                r.Attempted<0||r.Attempted>1||r.Published<0||r.Published>1||r.Published>r.Attempted||
                r.Phase==0&&(r.Attempted!=0||r.Published!=0)||r.Phase>=2&&r.Published!=1||
                r.Phase==1&&r.Attempted!=1||r.Binding!=Digest(Immutable(r))||
                r.Before.Any(x=>x==null)||r.After.Any(x=>x==null)||
                r.Before.Where((x,i)=>i!=r.SourceSlot&&!Same(x,r.After[i])).Any()||
                r.Before[r.SourceSlot].IsEmpty()||r.Before[r.SourceSlot].count<1||
                r.Frame!=RebirthHumanEquipment.Frame(new ItemStack(r.Before[r.SourceSlot].itemValue.Clone(),1)))return false;
            var expected=r.Before[r.SourceSlot].Clone();expected.count--;
            if(expected.count==0)expected=ItemStack.Empty.Clone();
            return Same(expected,r.After[r.SourceSlot])&&
                (string.IsNullOrEmpty(r.SourceProof)||r.SourceProof.Length==64&&
                    r.SourceProof.All(Uri.IsHexDigit)&&r.SourceProof==r.SourceProof.ToLowerInvariant())&&
                (r.Published==0||r.SourceProof?.Length==64);
        }
        catch{return false;}
    }
    private static XElement Write(Record r)
    {
        if(!Valid(r))throw new InvalidDataException("Invalid NPC Work native transfer original.");
        return new XElement("workMove",new XAttribute("version",1),new XAttribute("operation",r.Operation.ToString("N")),
            new XAttribute("creditId",r.CreditId.ToString("N")),
            new XAttribute("npc",r.Npc.ToString()),new XAttribute("owner",r.Owner),new XAttribute("creation",r.Creation),
            new XAttribute("world",r.World),new XAttribute("deaths",r.Deaths),new XAttribute("source",r.Source),
            new XAttribute("incarnation",r.Incarnation),new XAttribute("sourceSlot",r.SourceSlot),
            new XAttribute("bagSlot",r.BagSlot),new XAttribute("binding",r.Binding),new XAttribute("phase",r.Phase),
            new XAttribute("attempted",r.Attempted),new XAttribute("published",r.Published),
            new XAttribute("sourceProof",r.SourceProof??string.Empty),new XElement("stack",r.Frame),
            new XElement("before",Image(r.Before)),new XElement("after",Image(r.After)),
            new XElement("locks",new string(r.Locks.Select(x=>x?'1':'0').ToArray())));
    }
    private static bool TryRead(XElement node,out Record r)
    {
        r=null;
        try
        {
            if(node?.Name!="workMove"||node.Attribute("version")?.Value!="1"||node.Attributes().Count()!=17||
                node.Elements().Select(x=>x.Name.LocalName).SequenceEqual(new[]{"stack","before","after","locks"})==false||
                !RebirthNpcStableId.TryParse((string)node.Attribute("npc"),out var npc))return false;
            var value=new Record{Operation=Guid.ParseExact((string)node.Attribute("operation"),"N"),
                CreditId=Guid.ParseExact((string)node.Attribute("creditId"),"N"),Npc=npc,
                Owner=(string)node.Attribute("owner"),Creation=(string)node.Attribute("creation"),World=(string)node.Attribute("world"),
                Deaths=(int)node.Attribute("deaths"),Source=(string)node.Attribute("source"),Incarnation=(string)node.Attribute("incarnation"),
                SourceSlot=(int)node.Attribute("sourceSlot"),BagSlot=(int)node.Attribute("bagSlot"),Binding=(string)node.Attribute("binding"),
                Phase=(int)node.Attribute("phase"),Attempted=(int)node.Attribute("attempted"),Published=(int)node.Attribute("published"),
                SourceProof=(string)node.Attribute("sourceProof"),Frame=node.Element("stack")?.Value,
                Before=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Element("before")?.Value??"")),
                After=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Element("after")?.Value??""))};
            string locks=node.Element("locks")?.Value;
            if(locks==null||locks.Any(x=>x!='0'&&x!='1'))return false;
            value.Locks=locks.Select(x=>x=='1').ToArray();
            if(!Valid(value)||!XNode.DeepEquals(Write(value),node))return false;
            r=value;return true;
        }
        catch{return false;}
    }
    internal static bool TryPrepare(EntityPlayer owner,EntityRebirthHumanoidNPC npc,
        RebirthNpcPhysicalResourceEndpoint.ExactStackObservation seen,Guid operation,out Record record)
    {
        record=null;
        try
        {
            if(owner?.world?.IsRemote()!=false||npc==null||seen==null||seen.Stack?.count<1||operation==Guid.Empty||
                npc.RebirthRuntimeState.StableId.ToString()!=seen.NpcId||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity)||identity.CanonicalId!=seen.OwnerId||
                !RebirthOriginalActionScope.Validate(identity.StorageKey,RebirthCamperCharacterAccess.CurrentCreation(owner))||
                !RebirthOriginalActionScope.Matches(owner,identity.StorageKey,RebirthCamperCharacterAccess.CurrentCreation(owner))||
                RebirthLogisticsTransferJournal.WorldKey(owner.world) is not string world||
                npc.bag.ItemGrid[seen.BagSlot]!=null&&!npc.bag.ItemGrid[seen.BagSlot].IsEmpty())return false;
            var before=ItemStack.Clone(seen.SourceBefore);var after=ItemStack.Clone(before);
            var removed=before[seen.SourceSlot].Clone();removed.count=1;
            var remaining=after[seen.SourceSlot].Clone();remaining.count--;
            after[seen.SourceSlot]=remaining.count>0?remaining:ItemStack.Empty.Clone();
            var candidate=new Record{Operation=operation,CreditId=Guid.NewGuid(),Npc=npc.RebirthRuntimeState.StableId,Owner=identity.StorageKey,
                Creation=RebirthCamperCharacterAccess.CurrentCreation(owner),
                World=world,Deaths=owner.Died,Source=seen.SourceId,Incarnation=seen.Incarnation,
                SourceSlot=seen.SourceSlot,BagSlot=seen.BagSlot,Frame=RebirthHumanEquipment.Frame(removed),
                Before=before,After=after,Locks=(bool[])seen.SourceLocks.Clone()};
            candidate.Binding=Digest(Immutable(candidate));
            if(!Valid(candidate))return false;record=candidate;return true;
        }
        catch{return false;}
    }
    internal static bool TryReadSaved(RebirthNpcStableId npc,Guid operation,out Record record)
    {
        record=null;
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var person))return false;
            var moves=(person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse)
                .Where(x=>x.Name=="humanEquipment").SelectMany(x=>x.Elements("workMove"))
                .Where(x=>(string)x.Attribute("operation")==operation.ToString("N")).ToArray();
            if(moves.Length!=1||!TryRead(moves[0],out var found)||found.Npc!=npc||found.Operation!=operation)return false;
            record=found;return true;
        }
        catch{return false;}
    }
    internal static bool HasPending(RebirthNpcStableId npc)=>HasPendingExcept(npc,null);
    // Same answer, except the P7 station-queue row `stationQueueOperation` (the withdrawal/refund asking about itself).
    internal static bool HasPendingExcept(RebirthNpcStableId npc,string stationQueueOperation)
    {
        try
        {
            if(RebirthNpcStructureRepairOriginal.HasNativePending(npc))return true;
            if(RebirthNpcStationQueueOriginal.HasPendingExcept(npc,stationQueueOperation))return true;
            if(RebirthNpcWorkNativeCreditOriginal.HasPending(npc))return true;
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var person))
            {
                if(!RebirthNpcAggregatePersistenceStore.TryGet(npc,out person))return false;
            }
            foreach(var equipment in (person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse).Where(x=>x.Name=="humanEquipment"))
            foreach(var move in equipment.Elements("workMove"))
                if(!TryRead(move,out var row)||row.Phase<4)return true;
            return false;
        }
        catch{return true;}
    }
    internal static bool TryRead(string owner,Guid operation,string source,out Record record)
    {
        record=null;
        try
        {
            ScopeIndex();
            if(indexedNpc.TryGetValue(operation,out var indexed))
            {
                if(!TryReadSaved(indexed,operation,out var saved))return false;
                record=saved.Owner==owner&&saved.Source==source?saved:null;
                return record!=null;
            }
            var ids=RebirthNpcAggregatePersistenceStore.Snapshot().Select(x=>x.Identity.StableNpcId).ToArray();
            Record found=null;
            foreach(var id in ids)if(TryReadSaved(id,operation,out var row)&&row.Owner==owner&&row.Source==source)
            {if(found!=null)return false;found=row;}
            record=found;if(found!=null)indexedNpc[operation]=found.Npc;return found!=null;
        }
        catch{return false;}
    }
    internal static bool TryRead(string owner,Guid operation,out Record record)
    {
        record=null;
        try
        {
            ScopeIndex();
            if(indexedNpc.TryGetValue(operation,out var indexed))
            {
                if(!TryReadSaved(indexed,operation,out var saved))return false;
                record=saved.Owner==owner?saved:null;
                return record!=null;
            }
            var ids=RebirthNpcAggregatePersistenceStore.Snapshot().Select(x=>x.Identity.StableNpcId).ToArray();
            Record found=null;
            foreach(var id in ids)if(TryReadSaved(id,operation,out var row)&&row.Owner==owner)
            {if(found!=null)return false;found=row;}
            record=found;if(found!=null)indexedNpc[operation]=found.Npc;return found!=null;
        }
        catch{return false;}
    }
    internal static bool TryReadPending(World world,out Record[] records)
    {
        records=null;
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world))return false;
            ScopeIndex();var found=new List<Record>();var unique=new HashSet<Guid>();
            foreach(var person in RebirthNpcAggregatePersistenceStore.Snapshot())
            foreach(var equipment in (person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse).Where(x=>x.Name=="humanEquipment"))
            foreach(var move in equipment.Elements("workMove"))
            {
                if(!TryRead(move,out var row)||!unique.Add(row.Operation)||row.Npc!=person.Identity.StableNpcId||
                    row.World!=RebirthLogisticsTransferJournal.WorldKey(world))return false;
                indexedNpc[row.Operation]=row.Npc;
                if(row.Phase<4)found.Add(row);
            }
            if(found.Count>4096||found.Select(x=>x.Operation).Distinct().Count()!=found.Count)return false;
            records=found.ToArray();return true;
        }
        catch{return false;}
    }
    internal static bool TryPublish(EntityRebirthHumanoidNPC npc,Record next)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||npc?.world?.IsRemote()!=false||next==null||
                npc.RebirthRuntimeState.StableId!=next.Npc||!Valid(next))return false;
            var x=RebirthHumanEquipment.Read(npc);
            var previous=x.Elements("workMove").SingleOrDefault(m=>(string)m.Attribute("operation")==next.Operation.ToString("N"));
            if(previous!=null)
            {
                if(!TryRead(previous,out var old)||old.Binding!=next.Binding||old.Operation!=next.Operation||
                    next.Phase<old.Phase||next.Phase>old.Phase+1||next.Attempted<old.Attempted||
                    next.Attempted>old.Attempted+1||next.Published<old.Published||next.Published>old.Published+1)return false;
                previous.ReplaceWith(Write(next));
            }
            else if(next.Phase==0&&next.Attempted==0&&next.Published==0&&
                x.Elements("workMove").Count()<128&&x.Elements("workMove").All(m=>
                    TryRead(m,out var existing)&&existing.Phase==4))x.Add(Write(next));
            else return false;
            RebirthHumanEquipment.Save(npc,x);
            if(!TryReadSaved(next.Npc,next.Operation,out var saved)||!XNode.DeepEquals(Write(saved),Write(next)))return false;
            ScopeIndex();indexedNpc[next.Operation]=next.Npc;return true;
        }
        catch{return false;}
    }
}
