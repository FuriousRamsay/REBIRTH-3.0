using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Saved intent for the second, exact-stack half of a storage-organization move.
// This record grants no native mutation by itself. A destination lease must
// bind its immutable image, perform the one-use slot write, and publish proof.
internal static class RebirthNpcWorkNativeCreditOriginal
{
    internal sealed class Record
    {
        internal Guid Operation,SourceOperation;
        internal RebirthNpcStableId Npc;
        internal string Owner,Creation,World,Source,Destination,Incarnation,Frame,Binding,CreditProof;
        internal int Deaths,BagSlot,DestinationSlot,Phase,Attempted,Published;
        internal ItemStack[] BagBefore,Before,After;
        internal bool[] Locks;
        internal bool WriteAhead;
        internal bool Merge; // merge into an identical-payload partial stack; requires WriteAhead (versioned: absent = false)
    }
    private static string Image(ItemStack[] items)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(items));
    private static bool Same(ItemStack a,ItemStack b)=>RebirthStationGridIngredients.IsSameStackSnapshot(a,b);
    private static string Digest(string text)=>RebirthRemoteResourceDurableJournal.Digest(text);
    private static string Immutable(Record r)=>string.Join("|",new[]{
        r.Operation.ToString("N"),r.SourceOperation.ToString("N"),r.Npc.ToString(),r.Owner,r.Creation,r.World,
        r.Deaths.ToString(CultureInfo.InvariantCulture),r.Source,r.Destination,r.Incarnation,
        r.BagSlot.ToString(CultureInfo.InvariantCulture),r.DestinationSlot.ToString(CultureInfo.InvariantCulture),
        r.Frame,Image(r.BagBefore),Image(r.Before),Image(r.After),
        new string(r.Locks.Select(x=>x?'1':'0').ToArray())})+(r.WriteAhead?"|native-credit-write-ahead-v1":string.Empty)+
        (r.Merge?"|native-credit-merge-v1":string.Empty);
    // Destination cell shape. Plain credit: empty cell receives the moved stack (old rule, unchanged).
    // Merge (versioned, WriteAhead only): the cell already holds the SAME exact ItemValue payload and receives exactly the
    // one moved item (the transfer original removes one item), so After.count == Before.count + 1 within the stack limit.
    // A merge never relaxes the "all other cells identical" rule and never accepts a bag-source row.
    private static bool DestinationShapeOk(Record r)
    {
        var before=r.Before[r.DestinationSlot];var after=r.After[r.DestinationSlot];
        if(!r.Merge)
            return before.IsEmpty()&&after.count>=1&&(after.count==1||TryParseBagSource(r.Source,out _))&&
                RebirthHumanEquipment.Frame(after)==r.Frame;
        if(!r.WriteAhead||before.IsEmpty()||after.IsEmpty()||TryParseBagSource(r.Source,out _))return false;
        var moved=RebirthHumanEquipment.Decode(r.Frame);
        if(moved==null||moved.IsEmpty()||moved.count!=1||moved.itemValue?.ItemClass==null)return false;
        string payload=RebirthNativeItemCodec.Encode(moved.itemValue);
        return RebirthNativeItemCodec.Encode(before.itemValue)==payload&&RebirthNativeItemCodec.Encode(after.itemValue)==payload&&
            after.count==before.count+1&&after.count<=Math.Max(1,after.itemValue.ItemClass.Stacknumber.Value);
    }
    internal static bool Valid(Record r)
    {
        try
        {
            if(r==null||r.Operation==Guid.Empty||r.SourceOperation==Guid.Empty||
                r.Operation==r.SourceOperation||r.Npc.IsEmpty||r.Owner?.Length!=64||
                !RebirthOriginalActionScope.Validate(r.Owner,r.Creation)||r.World?.Length!=32||
                r.Deaths<0||r.Source?.StartsWith("S:",StringComparison.Ordinal)!=true||
                r.Destination?.StartsWith("S:",StringComparison.Ordinal)!=true||
                r.Source==r.Destination&&!r.WriteAhead||
                !Guid.TryParseExact(r.Incarnation,"N",out var incarnation)||incarnation==Guid.Empty||
                r.BagBefore==null||r.Before==null||r.After==null||r.Locks==null||
                r.BagBefore.Length<1||r.BagBefore.Length>169||
                r.Before.Length<1||r.Before.Length>256||
                r.Before.Length!=r.After.Length||r.Locks.Length!=r.Before.Length||
                r.DestinationSlot<0||r.DestinationSlot>=r.Before.Length||r.Locks[r.DestinationSlot]||
                r.BagSlot<0||r.BagSlot>=r.BagBefore.Length||
                r.BagBefore.Any(x=>x==null)||
                RebirthHumanEquipment.Frame(r.BagBefore[r.BagSlot])!=r.Frame||
                r.Before.Any(x=>x==null)||r.After.Any(x=>x==null)||
                !DestinationShapeOk(r)||
                r.Before.Where((x,i)=>i!=r.DestinationSlot&&!Same(x,r.After[i])).Any()||
                r.Phase<0||r.Phase>6||r.Attempted<0||r.Attempted>2||
                r.Published<0||r.Published>2||r.Published>r.Attempted||
                r.Phase==0&&(r.Attempted!=0||r.Published!=0)||
                r.Phase==1&&(r.Attempted!=1||r.Published!=0)||
                r.Phase==2&&(r.Attempted!=1||r.Published!=1)||
                r.Phase==3&&(r.Attempted!=2||r.Published!=1)||
                r.Phase>=4&&(r.Attempted!=2||r.Published!=2)||
                r.Phase>=4&&string.IsNullOrEmpty(r.CreditProof)||
                r.Binding!=Digest(Immutable(r)))return false;
            return string.IsNullOrEmpty(r.CreditProof)||r.CreditProof.Length==64&&
                r.CreditProof.All(Uri.IsHexDigit)&&r.CreditProof==r.CreditProof.ToLowerInvariant();
        }
        catch{return false;}
    }
    private static XElement Write(Record r)
    {
        if(!Valid(r))throw new InvalidDataException("Invalid exact NPC Work credit original.");
        return new XElement("workCredit",new XAttribute("version",1),
            new XAttribute("operation",r.Operation.ToString("N")),
            new XAttribute("sourceOperation",r.SourceOperation.ToString("N")),
            new XAttribute("npc",r.Npc.ToString()),new XAttribute("owner",r.Owner),
            new XAttribute("creation",r.Creation),new XAttribute("world",r.World),
            new XAttribute("deaths",r.Deaths),new XAttribute("source",r.Source),
            new XAttribute("destination",r.Destination),new XAttribute("incarnation",r.Incarnation),
            new XAttribute("bagSlot",r.BagSlot),new XAttribute("destinationSlot",r.DestinationSlot),
            new XAttribute("binding",r.Binding),new XAttribute("phase",r.Phase),
            new XAttribute("attempted",r.Attempted),new XAttribute("published",r.Published),
            new XAttribute("creditProof",r.CreditProof??string.Empty),
            r.WriteAhead?new XAttribute("writeAhead",true):null,
            r.Merge?new XAttribute("merge",true):null,
            new XElement("stack",r.Frame),new XElement("bagBefore",Image(r.BagBefore)),
            new XElement("before",Image(r.Before)),
            new XElement("after",Image(r.After)),
            new XElement("locks",new string(r.Locks.Select(x=>x?'1':'0').ToArray())));
    }
    private static bool TryRead(XElement node,out Record result)
    {
        result=null;
        try
        {
            if(node?.Name!="workCredit"||node.Attribute("version")?.Value!="1"||
                node.Attributes().Count()!=18+(node.Attribute("writeAhead")==null?0:1)+(node.Attribute("merge")==null?0:1)||
                node.Attribute("writeAhead")!=null&&(bool?)node.Attribute("writeAhead")!=true||
                node.Attribute("merge")!=null&&((bool?)node.Attribute("merge")!=true||node.Attribute("writeAhead")==null)||
                !node.Elements().Select(x=>x.Name.LocalName)
                    .SequenceEqual(new[]{"stack","bagBefore","before","after","locks"})||
                !RebirthNpcStableId.TryParse((string)node.Attribute("npc"),out var npc))return false;
            var r=new Record{
                Operation=Guid.ParseExact((string)node.Attribute("operation"),"N"),
                SourceOperation=Guid.ParseExact((string)node.Attribute("sourceOperation"),"N"),
                Npc=npc,Owner=(string)node.Attribute("owner"),
                Creation=(string)node.Attribute("creation"),World=(string)node.Attribute("world"),
                Deaths=(int)node.Attribute("deaths"),Source=(string)node.Attribute("source"),
                Destination=(string)node.Attribute("destination"),
                Incarnation=(string)node.Attribute("incarnation"),
                BagSlot=(int)node.Attribute("bagSlot"),
                DestinationSlot=(int)node.Attribute("destinationSlot"),
                Binding=(string)node.Attribute("binding"),Phase=(int)node.Attribute("phase"),
                Attempted=(int)node.Attribute("attempted"),Published=(int)node.Attribute("published"),
                CreditProof=(string)node.Attribute("creditProof"),WriteAhead=(bool?)node.Attribute("writeAhead")==true,Merge=(bool?)node.Attribute("merge")==true,Frame=node.Element("stack")?.Value,
                BagBefore=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Element("bagBefore")?.Value??"")),
                Before=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Element("before")?.Value??"")),
                After=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Element("after")?.Value??""))};
            var locks=node.Element("locks")?.Value;
            if(locks==null||locks.Any(x=>x!='0'&&x!='1'))return false;
            r.Locks=locks.Select(x=>x=='1').ToArray();
            if(!Valid(r)||!XNode.DeepEquals(Write(r),node))return false;
            result=r;return true;
        }
        catch{return false;}
    }
    internal static bool TryReadNode(XElement node,out Record result)=>TryRead(node,out result);
    internal static bool TryPrepare(EntityPlayer owner,EntityRebirthHumanoidNPC npc,
        RebirthNpcWorkNativeTransferOriginal.Record source,
        RebirthNpcPhysicalResourceEndpoint.ExactCreditObservation seen,Guid operation,out Record result,
        bool merge=false,bool sameContainer=false)
    {
        result=null;
        try
        {
            // sameContainer (in-container sort): only for a distinct cell of the very container the saved debit came from.
            bool sameOk=sameContainer&&source!=null&&seen!=null&&source.Source==seen.DestinationId&&
                source.Incarnation==seen.Incarnation&&source.SourceSlot!=seen.DestinationSlot;
            if(owner==null||npc?.RebirthRuntimeState==null||source==null||seen==null||
                source.Phase!=4||source.Npc!=npc.RebirthRuntimeState.StableId||
                source.Owner!=RebirthStablePlayerIdentity.ComputeStorageKey(seen.OwnerId)||(source.Source==seen.DestinationId&&!sameOk)||
                seen.NpcId!=npc.RebirthRuntimeState.StableId.ToString()||
                source.BagSlot!=seen.BagSlot||source.Frame!=seen.StackFrame||
                RebirthHumanEquipment.Frame(npc.bag.ItemGrid[source.BagSlot])!=source.Frame||
                !RebirthOriginalActionScope.Matches(owner,source.Owner,source.Creation)||
                source.World!=RebirthLogisticsTransferJournal.WorldKey(owner.world)||
                operation==Guid.Empty||operation==source.Operation)return false;
            var after=ItemStack.Clone(seen.DestinationBefore);
            if(merge)
            {
                // Same exact payload + capacity are re-proved by Valid; this only builds the merged image (one moved item).
                var moved=RebirthHumanEquipment.Decode(source.Frame);var merged=seen.DestinationBefore[seen.DestinationSlot]?.Clone();
                if(moved==null||moved.IsEmpty()||moved.count!=1||merged==null||merged.IsEmpty())return false;
                merged.count+=1;after[seen.DestinationSlot]=merged;
            }
            else after[seen.DestinationSlot]=RebirthHumanEquipment.Decode(source.Frame);
            var r=new Record{Operation=operation,SourceOperation=source.Operation,Merge=merge,
                Npc=source.Npc,Owner=source.Owner,Creation=source.Creation,
                World=source.World,Deaths=source.Deaths,Source=source.Source,
                Destination=seen.DestinationId,Incarnation=seen.Incarnation,
                BagSlot=source.BagSlot,DestinationSlot=seen.DestinationSlot,
                Frame=source.Frame,BagBefore=ItemStack.Clone(npc.bag.ItemGrid.items),
                Before=ItemStack.Clone(seen.DestinationBefore),
                After=after,Locks=(bool[])seen.DestinationLocks.Clone(),WriteAhead=true};
            r.Binding=Digest(Immutable(r));
            if(!Valid(r))return false;result=r;return true;
        }
        catch{return false;}
    }
    // Bag-source variant (no schema change): the source is an exact bound NPC bag stack, encoded in
    // the immutable Source field as "S:npcbag:<bound native-stack id>"; SourceOperation is the operation
    // that produced the stack (e.g. P7 station pickup). Whole-stack credit into one empty cell.
    private const string BagSourcePrefix="S:npcbag:";
    internal static string BagSourceId(Guid boundStackId)=>BagSourcePrefix+boundStackId.ToString("N");
    internal static bool TryParseBagSource(string source,out Guid boundStackId)
    {
        boundStackId=Guid.Empty;
        return source!=null&&source.StartsWith(BagSourcePrefix,StringComparison.Ordinal)&&
            Guid.TryParseExact(source.Substring(BagSourcePrefix.Length),"N",out boundStackId)&&boundStackId!=Guid.Empty;
    }
    internal static bool TryPrepareBag(EntityPlayer owner,EntityRebirthHumanoidNPC npc,Guid sourceOperation,
        Guid boundStackId,RebirthNpcPhysicalResourceEndpoint.ExactCreditObservation seen,Guid operation,out Record result)
    {
        result=null;
        try
        {
            if(owner==null||npc?.RebirthRuntimeState==null||seen==null||sourceOperation==Guid.Empty||
                boundStackId==Guid.Empty||operation==Guid.Empty||operation==sourceOperation||
                owner.world?.IsRemote()!=false||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity)||
                identity.CanonicalId!=seen.OwnerId||
                npc.RebirthRuntimeState.OwnershipKind!=RebirthNpcOwnershipKind.Player||
                !string.Equals(identity.CanonicalId,npc.RebirthRuntimeState.OwnerId,StringComparison.OrdinalIgnoreCase)||
                seen.NpcId!=npc.RebirthRuntimeState.StableId.ToString()||
                seen.BagSlot<0||seen.BagSlot>=npc.bag.SlotCount||
                RebirthHumanEquipment.Frame(npc.bag.ItemGrid[seen.BagSlot])!=seen.StackFrame||
                string.IsNullOrEmpty(seen.StackFrame)||
                BagSourceId(boundStackId)==seen.DestinationId||
                RebirthLogisticsTransferJournal.WorldKey(owner.world) is not string world)return false;
            string creation=RebirthCamperCharacterAccess.CurrentCreation(owner);
            if(!RebirthOriginalActionScope.Matches(owner,identity.StorageKey,creation))return false;
            var after=ItemStack.Clone(seen.DestinationBefore);
            after[seen.DestinationSlot]=RebirthHumanEquipment.Decode(seen.StackFrame);
            var r=new Record{Operation=operation,SourceOperation=sourceOperation,
                Npc=npc.RebirthRuntimeState.StableId,Owner=identity.StorageKey,Creation=creation,
                World=world,Deaths=owner.Died,Source=BagSourceId(boundStackId),
                Destination=seen.DestinationId,Incarnation=seen.Incarnation,
                BagSlot=seen.BagSlot,DestinationSlot=seen.DestinationSlot,
                Frame=seen.StackFrame,BagBefore=ItemStack.Clone(npc.bag.ItemGrid.items),
                Before=ItemStack.Clone(seen.DestinationBefore),
                After=after,Locks=(bool[])seen.DestinationLocks.Clone(),WriteAhead=true};
            r.Binding=Digest(Immutable(r));
            if(!Valid(r))return false;result=r;return true;
        }
        catch{return false;}
    }
    internal static bool TryReadSaved(RebirthNpcStableId npc,Guid operation,out Record result)
    {
        result=null;
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var person))return false;
            var rows=(person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse)
                .Where(x=>x.Name=="humanEquipment").SelectMany(x=>x.Elements("workCredit"))
                .Where(x=>(string)x.Attribute("operation")==operation.ToString("N")).ToArray();
            if(rows.Length!=1||!TryRead(rows[0],out var saved)||saved.Npc!=npc)return false;
            result=saved;return true;
        }
        catch{return false;}
    }
    internal static bool TryRead(string owner,Guid operation,string destination,out Record result)
    {
        result=null;
        try
        {
            var ids=RebirthNpcAggregatePersistenceStore.Snapshot()
                .Select(x=>x.Identity.StableNpcId).ToArray();
            Record found=null;
            foreach(var id in ids)
                if(TryReadSaved(id,operation,out var row)&&row.Owner==owner&&
                    row.Destination==destination)
                {if(found!=null)return false;found=row;}
            result=found;return found!=null;
        }
        catch{return false;}
    }
    internal static bool TryReadPending(World world,out Record[] records)
    {
        records=null;
        try
        {
            if(world?.IsRemote()!=false)return false;
            var found=new List<Record>();var operations=new HashSet<Guid>();
            foreach(var person in RebirthNpcAggregatePersistenceStore.Snapshot())
            foreach(var equipment in (person.UnknownOptionalElements??Array.Empty<string>())
                .Select(XElement.Parse).Where(x=>x.Name=="humanEquipment"))
            foreach(var node in equipment.Elements("workCredit"))
            {
                if(!TryRead(node,out var row)||row.Npc!=person.Identity.StableNpcId||
                    row.World!=RebirthLogisticsTransferJournal.WorldKey(world)||
                    !operations.Add(row.Operation))return false;
                if(row.Phase<6)found.Add(row);
            }
            if(found.Count>4096)return false;
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
            var equipment=RebirthHumanEquipment.Read(npc);
            var prior=equipment.Elements("workCredit")
                .SingleOrDefault(x=>(string)x.Attribute("operation")==next.Operation.ToString("N"));
            if(prior==null)
            {
                if(next.Phase!=0||next.Attempted!=0||next.Published!=0||
                    equipment.Elements("workCredit").Count()>=128||
                    equipment.Elements("workCredit").Any(x=>!TryRead(x,out var old)||old.Phase!=6))return false;
                equipment.Add(Write(next));
            }
            else
            {
                if(!TryRead(prior,out var old)||old.Binding!=next.Binding||
                    next.Phase<old.Phase||next.Phase>old.Phase+1||
                    next.Attempted<old.Attempted||next.Attempted>old.Attempted+1||
                    next.Published<old.Published||next.Published>old.Published+1)return false;
                prior.ReplaceWith(Write(next));
            }
            RebirthHumanEquipment.Save(npc,equipment);
            return TryReadSaved(next.Npc,next.Operation,out var saved)&&
                XNode.DeepEquals(Write(next),Write(saved));
        }
        catch{return false;}
    }
    internal static bool HasPending(RebirthNpcStableId npc)
    {
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var person))
            {
                if(!RebirthNpcAggregatePersistenceStore.TryGet(npc,out person))return false;
            }
            foreach(var equipment in (person.UnknownOptionalElements??Array.Empty<string>())
                .Select(XElement.Parse).Where(x=>x.Name=="humanEquipment"))
            foreach(var row in equipment.Elements("workCredit"))
                if(!TryRead(row,out var credit)||credit.Phase<6)return true;
            return false;
        }
        catch{return true;}
    }
}
