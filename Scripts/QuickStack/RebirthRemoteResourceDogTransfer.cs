using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Named fetch producer only. Native dog slots and the actual effect receipt
// publish in the existing aggregate; no logical inventory is substituted.
internal static class RebirthRemoteResourceDogTransfer
{
    private const string Element="rebirthRemoteResourceDogReceipt";
    private static string Hash(string value)
    {using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
    private static string Image(ItemStack[] slots,bool[] locks)
    {
        if(slots?.Length!=RebirthDogInventoryService.Capacity||locks?.Length!=slots.Length)throw new InvalidDataException();
        return Hash(Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(slots))+":"+Convert.ToBase64String(locks.Select(value=>(byte)(value?1:0)).ToArray()));
    }
    internal static string Receipt(RebirthRemoteResourceNpcEffectPlan plan)
    {
        if(plan==null||!plan.DogStorage||plan.Revision==uint.MaxValue)throw new InvalidDataException();
        return new XElement(Element,new XAttribute("version",1),new XAttribute("operation",plan.Operation.ToString("N")),new XAttribute("mutation",plan.MutationId.ToString("N")),
            new XAttribute("world",plan.World),new XAttribute("owner",plan.Owner),new XAttribute("creation",plan.Creation),new XAttribute("deaths",plan.Deaths),new XAttribute("dog",plan.NpcId.ToString()),
            new XAttribute("source",plan.SourceId),new XAttribute("originalRevision",plan.Revision),new XAttribute("revision",plan.Revision+1),new XAttribute("binding",plan.Binding),
            new XAttribute("before",Image(plan.CopyBeforeSlots(),plan.CopyLocks())),new XAttribute("after",Image(plan.CopyAfterSlots(),plan.CopyLocks()))).ToString(SaveOptions.DisableFormatting);
    }
    private static bool Parse(string text,out XElement node)
    {
        node=null;
        try
        {
            if(string.IsNullOrEmpty(text)||text.Length>8192)return false;
            using(var input=new StringReader(text))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8192}))node=XElement.Load(reader,LoadOptions.None);
            return node.Name==Element&&node.Attributes().Count()==14&&!node.Nodes().Any()&&(string)node.Attribute("version")=="1"&&node.ToString(SaveOptions.DisableFormatting)==text;
        }
        catch{node=null;return false;}
    }
    internal static bool TryReceipt(RebirthNpcPersistentRecord record,out string receipt)
    {
        receipt=null;
        try
        {
            foreach(string text in record.UnknownOptionalElements??new string[0])
            {
                XElement node;using(var input=new StringReader(text))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1048576}))node=XElement.Load(reader);
                if(node.Name.LocalName!=Element)continue;
                if(receipt!=null||!Parse(text,out _))return false;receipt=text;
            }
            return true;
        }
        catch{return false;}
    }
    internal static bool MayReplaceReceipt(RebirthNpcPersistentRecord record,RebirthRemoteResourceNpcEffectPlan plan)
    {
        if(!TryReceipt(record,out var previous))return false;if(previous==null)return true;
        if(!Parse(previous,out var node)||!Guid.TryParseExact((string)node.Attribute("operation"),"N",out var operation)||operation==plan.Operation)return false;
        return RebirthRemoteResourceDurableOutcome.TryReadSavedTerminalNpcEffectPlan((string)node.Attribute("owner"),operation,(string)node.Attribute("source"),out var old)&&
            old.DogStorage&&old.NpcId==plan.NpcId&&Receipt(old)==previous;
    }
    internal static void SetReceipt(RebirthNpcPersistentRecord record,string receipt)
    {
        if(!Parse(receipt,out _))throw new InvalidDataException();
        var values=(record.UnknownOptionalElements??new string[0]).Where(text=>XElement.Parse(text).Name.LocalName!=Element).ToList();values.Add(receipt);record.UnknownOptionalElements=values.ToArray();
    }
    internal static bool Matches(RebirthNpcPersistentRecord row,ItemStack[] slots,bool[] locks,uint revision)
    {
        if(slots?.Length!=RebirthDogInventoryService.Capacity||locks?.Length!=slots.Length)return false;
        if(row.Inventory==null)return revision==0&&slots.All(item=>item!=null&&item.IsEmpty())&&locks.All(value=>!value);
        if(row.Inventory.InventoryRevision!=revision||row.Inventory.SlotRecords?.Length!=slots.Length||row.Inventory.OverflowRecords==null)return false;
        for(int i=0;i<slots.Length;i++)
        {
            var cell=row.Inventory.SlotRecords[i];var wanted=slots[i];if(cell==null||wanted==null||cell.SlotIndex!=i||cell.Locked!=locks[i])return false;
            if(wanted.IsEmpty()){if(cell.ItemCount!=0||!string.IsNullOrEmpty(cell.ItemValueSerialization))return false;continue;}
            if(cell.ItemCount!=wanted.count||!RebirthNativeItemCodec.TryDecode(cell.ItemValueSerialization,out var value)||!RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(value,cell.ItemCount),wanted))return false;
        }
        return true;
    }
    internal static bool Current(EntityPlayer actor,RebirthRemoteResourceNpcEffectPlan plan)
    {
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||plan==null||!plan.DogStorage||!plan.IsCurrent()||!RemoteResourcesRuntimePolicy.Enabled||
            !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||actor.Died!=plan.Deaths||
            RebirthCamperCharacterAccess.CurrentCreation(actor)!=plan.Creation||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||identity.StorageKey!=plan.Owner||
            !RebirthNpcRuntimeRegistry.TryGetEntityId(plan.NpcId,out int entityId)||!(actor.world.GetEntity(entityId) is EntityRebirthDogCompanion dog)||dog.IsDead()||
            !RemoteNpcResourceSource.IsAuthorized(dog,actor,false,out _)||RebirthDogStateService.IsTamedWild(plan.NpcId)||RebirthNpcInteractionSessionService.IsInventoryAccessing(plan.NpcId)||
            RebirthDogStorageService.IsOpenServer(entityId)||(dog.position-actor.position).sqrMagnitude>RemoteResourcesRuntimePolicy.Radius*RemoteResourcesRuntimePolicy.Radius)return false;
        return true;
    }
    internal static bool CanAdmit(EntityPlayer actor,Guid operation,string sourceId)
    {
        try
        {
            if(!RebirthRemoteResourceDurableOutcome.TryReadNpcEffectPlan(actor,operation,sourceId,out var plan)||!Current(actor,plan)||
                !RebirthNpcAggregatePersistenceStore.TryGet(plan.NpcId,out var row)||!RebirthDogStateService.IsDog(row))return false;
            string receipt=Receipt(plan);
            if(TryReceipt(row,out var old)&&old==receipt&&row.Inventory?.InventoryRevision>=plan.Revision+1)return true;
            return Matches(row,plan.CopyBeforeSlots(),plan.CopyLocks(),plan.Revision)&&MayReplaceReceipt(row,plan)&&
                RebirthNpcOriginalEffectAdmission.TryReserveRemoteResource(actor,operation,sourceId,out var lease)&&lease.Current()&&plan.IsCurrent();
        }
        catch{return false;}
    }
    internal static bool TryApply(EntityPlayer actor,Guid operation,string sourceId,string authorityKey,out bool replayed,out string error)
        =>RebirthDogInventoryService.CommitRemoteResourceOriginalSlots(actor,operation,sourceId,authorityKey,out replayed,out error);
    internal static bool TryObserveSaved(RebirthRemoteResourceNpcEffectPlan plan,bool after,out string proof)
    {
        proof=null;
        try
        {
            if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||plan==null||!plan.DogStorage||!plan.IsCurrent()||
                !RebirthNpcAggregatePersistenceStore.TryReadSavedLogisticsDog(plan.NpcId,out var saved))return false;
            if(after)
            {
                if(!TryReceipt(saved,out var receipt)||receipt!=Receipt(plan)||saved.Inventory==null||saved.Inventory.InventoryRevision<plan.Revision+1||
                    saved.Inventory.InventoryRevision==plan.Revision+1&&!Matches(saved,plan.CopyAfterSlots(),plan.CopyLocks(),plan.Revision+1))return false;
            }
            else if(!Matches(saved,plan.CopyBeforeSlots(),plan.CopyLocks(),plan.Revision)||!MayReplaceReceipt(saved,plan))return false;
            proof=Hash(plan.Binding+":"+after+":"+saved.AggregateRevision+":"+saved.AggregateChecksum);return plan.IsCurrent();
        }
        catch{proof=null;return false;}
    }
}
