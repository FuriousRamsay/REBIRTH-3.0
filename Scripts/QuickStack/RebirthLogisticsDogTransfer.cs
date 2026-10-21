using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

// Dog storage remains in its existing canonical aggregate. This known bounded
// optional element is an original operation receipt, never another inventory.
internal static class RebirthLogisticsDogTransfer
{
    private const string Element="rebirthLogisticsInventoryReceipt";
    internal static bool CanAdmit(EntityPlayer actor,RebirthLogisticsTransferJournal.Record original,string legId)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||original==null||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||actor.Died!=original.Deaths||
                !RebirthOriginalActionScope.Matches(actor,original.Owner,original.Creation)||RebirthLogisticsTransferJournal.WorldKey(actor.world)!=original.World||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||owner.StorageKey!=original.Owner||
                !RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||retained.Phase!=0||retained.Rejecting||
                RebirthLogisticsTransferJournal.Encode(original)!=RebirthLogisticsTransferJournal.Encode(retained))return false;
            var leg=retained.Legs.SingleOrDefault(value=>value.Id==legId);
            if(!Original(leg,out var id,out uint revision)||leg.Attempted!=0||leg.Published!=0||
                !RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entityId)||!(actor.world.GetEntity(entityId) is EntityRebirthDogCompanion dog)||dog.IsDead()||
                !RemoteNpcResourceSource.IsAuthorized(dog,actor,false,out _)||RebirthDogStateService.IsTamedWild(id)||RebirthDogStorageService.IsOpenServer(entityId)||
                RebirthNpcInteractionSessionService.IsInventoryAccessing(id)||(dog.position-actor.position).sqrMagnitude>LogisticsPreviewService.CompanionRadiusForPlanning*LogisticsPreviewService.CompanionRadiusForPlanning)return false;
            if(retained.Action==QuickStackRadialAction.CompanionInventoryPull)
            {
                var user=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor.entityId)?.PrimaryId;
                if(!RebirthCompanionService.TryResolveInventoryTransferTarget(actor.world,actor.entityId,user,retained.Target,out var target)||!target.IsDog||target.StableId!=id.ToString())return false;
            }
            if(!RebirthNpcAggregatePersistenceStore.TryGet(id,out var row)||!RebirthDogStateService.IsDog(row)||
                !RebirthRemoteResourceDogTransfer.Matches(row,leg.Before,leg.Locks,revision)||!MayReplaceReceipt(row,retained,leg,false))return false;
            RebirthNpcAggregatePersistenceStore.Save();
            return RebirthNpcAggregatePersistenceStore.TryReadSavedLogisticsDog(id,out var saved)&&RebirthRemoteResourceDogTransfer.Matches(saved,leg.Before,leg.Locks,revision)&&
                MayReplaceReceipt(saved,retained,leg,false)&&RebirthNpcOriginalEffectAdmission.TryReserveLogistics(actor,retained,out var lease)&&lease.Current();
        }
        catch{return false;}
    }
    internal static bool TryApply(EntityPlayer actor,RebirthLogisticsTransferJournal.Record original,string legId,bool credit,string transferAuthority,
        Func<bool> originalCurrent,out bool replayed,out string error,bool preflightOnly=false)
    {
        replayed=false;error="original dog transfer unavailable";
        if(actor==null||original==null||originalCurrent?.Invoke()!=true||!ThreadManager.IsMainThread()||actor.world?.IsRemote()!=false||
            !RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||retained.Phase!=1||retained.Outcome!=0||retained.Legs.Any(value=>value.Restored)||string.IsNullOrEmpty(retained.OwnerDebitProof)||
            RebirthLogisticsTransferJournal.Encode(retained)!=RebirthLogisticsTransferJournal.Encode(original))return false;
        var leg=retained.Legs.SingleOrDefault(value=>value.Id==legId);
        if(!Original(leg,out var id,out _)||(!preflightOnly&&leg.Attempted!=(credit?2:1))||!RebirthNpcOriginalEffectAdmission.IsReservedLogistics(retained,id)||credit&&!preflightOnly&&(retained.Legs.Any(value=>value.Published<1)||retained.Npcs.Any(value=>value.Published<1))||
            !RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entityId)||!(actor.world.GetEntity(entityId) is EntityRebirthDogCompanion dog))return false;
        var world=actor.world;
        bool Current()
        {
            if(originalCurrent?.Invoke()!=true||!ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||!(RebirthLogisticsDeathSettlement.ActorCurrent(actor,retained)||RebirthLogisticsDeathSettlement.MatchesPaidSourceContinuation(actor,retained))||
                !ReferenceEquals(world.GetEntity(entityId),dog)||dog.IsDead()||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||owner.StorageKey!=retained.Owner||
                !RebirthNpcInventoryAuthorityService.Validate(transferAuthority,RebirthNpcInventoryAuthorityOperations.Transfer,id)||
                !RemoteNpcResourceSource.IsAuthorized(dog,actor,false,out _)||RebirthDogStateService.IsTamedWild(id)||
                RebirthNpcInteractionSessionService.IsInventoryAccessing(id)||RebirthDogStorageService.IsOpenServer(entityId)||
                (dog.position-actor.position).sqrMagnitude>LogisticsPreviewService.CompanionRadiusForPlanning*LogisticsPreviewService.CompanionRadiusForPlanning)return false;
            if(retained.Action==QuickStackRadialAction.CompanionInventoryPull)
            {
                var user=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor.entityId)?.PrimaryId;
                if(!RebirthCompanionService.TryResolveInventoryTransferTarget(world,actor.entityId,user,retained.Target,out var target)||!target.IsDog||target.StableId!=id.ToString())return false;
            }
            return true;
        }
        if(!Current())return false;
        return RebirthDogInventoryService.CommitLogisticsOriginalSlots(retained,leg,credit,Current,out replayed,out error,preflightOnly);
    }
    internal static bool Saved(RebirthLogisticsTransferJournal.Record record,RebirthLogisticsTransferJournal.Leg leg,bool credit,Func<bool> originalCurrent)
    {
        try
        {
            if(originalCurrent?.Invoke()!=true||!Original(leg,out var id,out uint revision)||!RebirthNpcAggregatePersistenceStore.TryReadSavedLogisticsDog(id,out var saved)||
                !TryReceipt(saved,out var receipt)||!MatchesReceipt(record,leg,credit,receipt,out uint appliedRevision)||saved.Inventory==null||saved.Inventory.InventoryRevision<appliedRevision||
                saved.Inventory.SlotRecords?.Length!=RebirthDogInventoryService.Capacity)return false;
            // A later canonical mutation preserves the atomically installed
            // original image receipt; it does not undo that original effect.
            if(saved.Inventory.InventoryRevision>appliedRevision||credit&&appliedRevision>revision+2)return originalCurrent();
            var image=Stage(leg,credit);
            for(int i=0;i<image.Length;i++)
            {
                var slot=saved.Inventory.SlotRecords[i];if(slot==null||slot.SlotIndex!=i||slot.Locked!=leg.Locks[i])return false;
                if(image[i].IsEmpty()){if(slot.ItemCount!=0||!string.IsNullOrEmpty(slot.ItemValueSerialization))return false;continue;}
                if(slot.ItemCount!=image[i].count||!RebirthNativeItemCodec.TryDecode(slot.ItemValueSerialization,out var item)||
                    !RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(item,slot.ItemCount),image[i]))return false;
            }
            return originalCurrent();
        }
        catch{return false;}
    }
    internal static string Identity(RebirthNpcStableId id,uint revision)=>"dog:"+id+":"+revision.ToString(CultureInfo.InvariantCulture);
    internal static bool HasSavedDebit(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsTransferJournal.Leg leg)
    {
        return Original(leg,out var id,out uint revision)&&RebirthNpcAggregatePersistenceStore.TryReadSavedLogisticsDog(id,out var saved)&&saved.Inventory!=null&&saved.Inventory.InventoryRevision>=revision+1&&
            TryReceipt(saved,out var receipt)&&MatchesReceipt(original,leg,false,receipt,out _);
    }
    internal static bool TryPositiveCredit(RebirthNpcPersistentRecord row,RebirthLogisticsTransferJournal.Leg leg,out ItemStack[] before,out ItemStack[] after,out bool[] locks)
    {
        before=null;after=null;locks=null;
        if(row.Inventory?.SlotRecords?.Length!=RebirthDogInventoryService.Capacity||row.Inventory.OverflowRecords==null||leg.After?.Length!=RebirthDogInventoryService.Capacity)return false;
        before=new ItemStack[RebirthDogInventoryService.Capacity];after=new ItemStack[before.Length];locks=new bool[before.Length];var paidBefore=Stage(leg,false);
        for(int i=0;i<before.Length;i++)
        {
            var cell=row.Inventory.SlotRecords[i];if(cell==null||cell.SlotIndex!=i||cell.ItemCount<0)return false;locks[i]=cell.Locked;
            var original=leg.After[i];int delta=original.count-paidBefore[i].count;
            if(delta<0)return false;if(delta==0)continue; // Unrelated opaque native frames remain untouched.
            if(cell.ItemCount==0){if(!string.IsNullOrEmpty(cell.ItemValueSerialization))return false;before[i]=ItemStack.Empty.Clone();}
            else{if(!RebirthNativeItemCodec.TryDecode(cell.ItemValueSerialization,out var item))return false;before[i]=new ItemStack(item,cell.ItemCount);}
            after[i]=before[i].Clone();
            if(original.IsEmpty()||cell.Reserved||before[i].IsEmpty()&&cell.Locked)return false;
            var incoming=original.Clone();incoming.count=delta;
            if(before[i].IsEmpty())
            {if(delta>Math.Max(1,incoming.itemValue.ItemClass.Stacknumber.Value))return false;after[i]=incoming;}
            else
            {
                var a=before[i].Clone();var b=incoming.Clone();a.count=1;b.count=1;
                if(!RebirthStationGridIngredients.IsSameStackSnapshot(a,b)||!before[i].CanStackPartlyWith(incoming,out int capacity)||capacity<delta)return false;
                long total=(long)before[i].count+delta;if(total>ushort.MaxValue)return false;after[i].count=(int)total;
            }
        }
        return true;
    }
    internal static bool Original(RebirthLogisticsTransferJournal.Leg leg,out RebirthNpcStableId id,out uint revision)
    {
        id=default;revision=0;
        if(leg==null||leg.Kind!=RemoteResourceSourceKind.NpcStorage||leg.Id?.StartsWith("N:",StringComparison.Ordinal)!=true||
            !RebirthNpcStableId.TryParse(leg.Id.Substring(2),out id)||id.IsEmpty||leg.NativeIdentity==null)return false;
        string prefix="dog:"+id+":";
        return leg.NativeIdentity.StartsWith(prefix,StringComparison.Ordinal)&&uint.TryParse(leg.NativeIdentity.Substring(prefix.Length),NumberStyles.None,CultureInfo.InvariantCulture,out revision)&&
            revision<=uint.MaxValue-2&&leg.NativeIdentity==Identity(id,revision)&&leg.Before?.Length==RebirthDogInventoryService.Capacity&&leg.After?.Length==leg.Before.Length&&leg.Locks?.Length==leg.Before.Length;
    }
    internal static ItemStack[] Stage(RebirthLogisticsTransferJournal.Leg leg,bool credit)
    {
        if(credit)return ItemStack.Clone(leg.After);
        var result=new ItemStack[leg.Before.Length];
        for(int i=0;i<result.Length;i++)
        {
            var before=leg.Before[i];var after=leg.After[i];
            if(before.IsEmpty()){result[i]=ItemStack.Empty.Clone();continue;}
            var a=before.Clone();var b=after.Clone();a.count=1;b.count=1;
            if(!after.IsEmpty()&&RebirthStationGridIngredients.IsSameStackSnapshot(a,b)){result[i]=before.Clone();result[i].count=Math.Min(before.count,after.count);}
            else result[i]=ItemStack.Empty.Clone();
        }
        return result;
    }
    private static string Image(ItemStack[] items,bool[] locks)
    {
        using(var hash=System.Security.Cryptography.SHA256.Create())using(var stream=new MemoryStream())
        {
            var bytes=RebirthCamperInventorySnapshot.Capture(items);stream.Write(bytes,0,bytes.Length);
            foreach(bool value in locks)stream.WriteByte((byte)(value?1:0));
            return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
        }
    }
    internal static string Receipt(RebirthLogisticsTransferJournal.Record record,RebirthLogisticsTransferJournal.Leg leg,bool credit,uint? appliedRevision=null)
    {
        if(!Original(leg,out var id,out uint revision))throw new InvalidDataException();
        uint actualRevision=appliedRevision??revision+(credit?2U:1U);
        if(credit?actualRevision<revision+2:actualRevision!=revision+1)throw new InvalidDataException();
        var document=new XmlDocument{XmlResolver=null};var element=document.CreateElement(Element);document.AppendChild(element);
        element.SetAttribute("version","1");element.SetAttribute("operation",record.Operation.ToString("N"));element.SetAttribute("world",record.World);
        element.SetAttribute("owner",record.Owner);element.SetAttribute("creation",record.Creation);element.SetAttribute("deaths",record.Deaths.ToString(CultureInfo.InvariantCulture));
        element.SetAttribute("dog",id.ToString());element.SetAttribute("originalRevision",revision.ToString(CultureInfo.InvariantCulture));element.SetAttribute("phase",credit?"2":"1");
        element.SetAttribute("revision",actualRevision.ToString(CultureInfo.InvariantCulture));element.SetAttribute("image",Image(Stage(leg,credit),leg.Locks));
        string text=element.OuterXml;if(text.Length>4096)throw new InvalidDataException();return text;
    }
    internal static bool MatchesReceipt(RebirthLogisticsTransferJournal.Record record,RebirthLogisticsTransferJournal.Leg leg,bool credit,string receipt,out uint appliedRevision)
    {
        appliedRevision=0;
        try{return Parse(receipt,out var element)&&uint.TryParse(element.GetAttribute("revision"),NumberStyles.None,CultureInfo.InvariantCulture,out appliedRevision)&&Receipt(record,leg,credit,appliedRevision)==receipt;}
        catch{appliedRevision=0;return false;}
    }
    private static bool Parse(string text,out XmlElement element)
    {
        element=null;
        try
        {
            if(string.IsNullOrEmpty(text)||text.Length>4096)return false;
            var document=new XmlDocument{XmlResolver=null};using(var input=new StringReader(text))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096}))document.Load(reader);
            element=document.DocumentElement;
            return element?.Name==Element&&element.NamespaceURI.Length==0&&element.Attributes.Count==11&&!element.HasChildNodes&&element.GetAttribute("version")=="1"&&element.OuterXml==text;
        }
        catch{element=null;return false;}
    }
    internal static bool TryReceipt(RebirthNpcPersistentRecord candidate,out string receipt)
    {
        receipt=null;
        foreach(string text in candidate.UnknownOptionalElements??new string[0])
        {
            // Unknown unrelated optional elements are retained, not interpreted.
            var document=new XmlDocument{XmlResolver=null};
            try{using(var input=new StringReader(text))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1048576}))document.Load(reader);}
            catch{return false;}
            if(document.DocumentElement?.Name!=Element)continue;
            if(receipt!=null||!Parse(text,out _))return false;receipt=text;
        }
        return true;
    }
    internal static bool MayReplaceReceipt(RebirthNpcPersistentRecord candidate,RebirthLogisticsTransferJournal.Record record,RebirthLogisticsTransferJournal.Leg leg,bool credit)
    {
        if(!TryReceipt(candidate,out string previous))return false;
        if(credit)return previous==Receipt(record,leg,false);
        if(previous==null)return true;
        if(!Parse(previous,out var element)||!Guid.TryParseExact(element.GetAttribute("operation"),"N",out var operation))return false;
        // Another original operation may only replace a terminal old receipt.
        return operation!=record.Operation&&RebirthLogisticsTransferJournal.TryRead(element.GetAttribute("owner"),operation,out var old)&&old.Phase==4&&
            old.World==record.World&&element.GetAttribute("world")==old.World&&old.Creation==element.GetAttribute("creation")&&
            old.Legs.Any(oldLeg=>Original(oldLeg,out var id,out _)&&id==candidate.Identity.StableNpcId&&MatchesReceipt(old,oldLeg,true,previous,out _));
    }
    internal static void SetReceipt(RebirthNpcPersistentRecord candidate,string receipt)
    {
        if(!Parse(receipt,out _))throw new InvalidDataException();
        var values=new List<string>();
        foreach(string text in candidate.UnknownOptionalElements??new string[0])
        {
            var document=new XmlDocument{XmlResolver=null};document.LoadXml(text);
            if(document.DocumentElement?.Name!=Element)values.Add(text);
        }
        values.Add(receipt);candidate.UnknownOptionalElements=values.ToArray();
    }
}
