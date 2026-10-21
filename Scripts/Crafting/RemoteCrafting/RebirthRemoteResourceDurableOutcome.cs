using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

// Authoritative readers for retained fetch originals. They do not debit a
// source, publish a payment, refund an owner, or reconstruct a missing original.
internal static partial class RebirthRemoteResourceDurableOutcome
{
    internal static bool RegisterNpcAdmissionReaders()=>RebirthNpcOriginalEffectAdmission.RegisterRemoteResourceReaders(
        TryReadPendingNpcOriginals,TryReadNpcEffectPlan,IsSavedNpcTerminal);

    internal static bool AcknowledgeNoPayment(EntityPlayer actor,Guid operation,string binding)
    {
        try
        {
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||
                !RebirthRemoteResourceDurableJournal.TryRead(identity.StorageKey,operation,out var original,out _)||original==null||
                !NoPaymentActorScope(actor,original)||original.Binding!=binding)return false;
            var root=original.Copy();
            if(original.Phase==4)return (string)root.Attribute("disposition")=="unused-no-payment";
            // Phase3 is already an immutable refusal: no delayed dispatcher
            // may attempt another source or publish a successful grant.
            if(original.Phase!=3||(int)root.Attribute("outcome")!=2||root.Element("ownerAction")!=null||ReadSlots(root.Element("removed")).Length!=0||
                !VerifyNoPaymentSources(actor,original))return false;
            return RebirthRemoteResourceDurableJournal.TryAdvance(original,value=>{value.SetAttributeValue("phase",4);value.SetAttributeValue("disposition","unused-no-payment");
                value.SetAttributeValue("ownerProof",RebirthRemoteResourceDurableJournal.Digest("authenticated-no-payment|"+original.Owner+"|"+original.Binding));},()=>NoPaymentActorScope(actor,original),out _);
        }
        catch{return false;}
    }
    private static bool VerifyNoPaymentSources(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record original)
    {
        foreach(var source in original.Copy().Element("sources").Elements("source"))
        {
            if((int)source.Attribute("attempted")==0)continue;
            // No named positive NPC no-effect receipt currently exists. In
            // particular, absence of an actual-effect receipt proves nothing.
            if((int)source.Attribute("kind")==5)return false;
            if(((int?)source.Attribute("restored")??0)!=1||
                !RebirthRemoteResourceSourceCustody.TryReserveRemoteResource(actor,original.Operation,(string)source.Attribute("id"),()=>ActorScope(actor,original),out var lease)||
                !lease.TryObserveSaved(3,out string proof)||proof!=(string)source.Attribute("restoreProof"))return false;
        }
        return true;
    }

    // Called only after GridCustody verified the exact phase receipt and actor
    // from one final native owner file. The retained source original, not the
    // incoming grid image, supplies the paid items.
    internal static bool WitnessCookingGrid(EntityPlayer actor,Guid child,int phase,string expectedActor,string body)
    {
        try
        {
            if(child==Guid.Empty||(phase!=1&&phase!=2)||body==null||System.Text.Encoding.UTF8.GetByteCount(body)>8*1024*1024||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string world))return false;
            var image=RebirthCookingRemoteAdmission.ReadProtocolXml(body,8*1024*1024);
            if(image.Name!="cookingGridOriginal"||(int?)image.Attribute("version")!=1||(long?)image.Attribute("fetchId") is not long request||request<=0||
                !new[]{"fetched","fetched-return"}.Contains((string)image.Attribute("action"))||
                (string)image.Attribute("world")!=actor.world.worldState.Guid||(int?)image.Attribute("deaths")!=actor.Died||
                !RebirthWorldCharacterService.TryGetIdentity(actor,out var character)||(string)image.Attribute("owner")!=character.CanonicalId||
                (string)image.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
                image.Element(phase==1?"beforeActor":"afterActor")?.Value!=expectedActor)return false;
            Guid operation=RebirthRemoteResourceDurableJournal.Operation(world,identity.StorageKey,(string)image.Attribute("creation"),1,(ulong)request,RemoteResourceClientOperation.CookingPull);
            if(!RebirthRemoteResourceDurableJournal.TryRead(identity.StorageKey,operation,out var original,out _)||original==null||!ActorScope(actor,original)||original.Phase<2)return false;
            var root=original.Copy();string action=new XElement("ownerAction",new XAttribute("domain","cooking-grid"),new XAttribute("child",child.ToString("N")),new XElement("original",body)).ToString(SaveOptions.DisableFormatting);
            if(root.Element("ownerAction")!=null&&root.Element("ownerAction").ToString(SaveOptions.DisableFormatting)!=action)return false;
            if(original.Phase==4)return root.Element("ownerAction")?.ToString(SaveOptions.DisableFormatting)==action;
            var paid=ReadSlots(root.Element("removed"));var fetched=ReadSlots(image.Element("fetched"));
            if(!SameTotals(Totals(paid),Totals(fetched)))return false;
            RebirthCamperEnergyActorImage Actor(string text)
            {
                if(text==null||text.Length>6*1024*1024)throw new InvalidDataException();
                using(var stream=new MemoryStream(Convert.FromBase64String(text),false))using(var reader=new BinaryReader(stream,System.Text.Encoding.UTF8,true))
                {var value=RebirthCamperEnergyActorImage.Read(reader);if(stream.Position!=stream.Length)throw new InvalidDataException();return value;}
            }
            var before=Actor(image.Element("beforeActor")?.Value);var after=Actor(image.Element("afterActor")?.Value);
            var beforeGrid=ReadSlots(image.Element("beforeGrid"));var afterGrid=ReadSlots(image.Element("afterGrid"));
            bool detached=(string)image.Attribute("location")=="fetch-return:"+request&&actor is EntityPlayerLocal local&&ReferenceEquals(actor.world.GetPrimaryPlayer(),local)&&
                beforeGrid.Length>=12&&beforeGrid.Length<=4096&&afterGrid.Length==beforeGrid.Length;
            if(!detached&&(beforeGrid.Length!=12||afterGrid.Length!=12)||before.Bag.Length!=after.Bag.Length||before.Belt.Length!=after.Belt.Length||
                before.BagLocks==null||after.BagLocks==null||!before.BagLocks.SequenceEqual(after.BagLocks)||
                !SameTotals(Totals(before.Bag.Concat(before.Belt).Concat(new[]{before.Cursor}).Concat(beforeGrid).Concat(paid)),
                    Totals(after.Bag.Concat(after.Belt).Concat(new[]{after.Cursor}).Concat(afterGrid))))return false;
            if(root.Element("ownerAction")==null)
            {
                var scope=original;
                if(phase!=1||!RebirthRemoteResourceDurableJournal.TryAdvance(scope,value=>value.Add(XElement.Parse(action)),()=>ActorScope(actor,scope),out var retained))return false;
                original=retained;
            }
            if(phase==1)return true;
            return RebirthRemoteResourceDurableJournal.TryAdvance(original,value=>{value.SetAttributeValue("phase",4);value.SetAttributeValue("disposition",(string)image.Attribute("action")=="fetched"?"grid-owned":"refunded");
                value.SetAttributeValue("ownerProof",RebirthRemoteResourceDurableJournal.Digest(child.ToString("N")+"|2|"+expectedActor+"|"+action));},()=>ActorScope(actor,original),out _);
        }
        catch{return false;}
    }

    private static bool ActorScope(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record original)
    {
        return ThreadManager.IsMainThread()&&actor!=null&&actor.world?.IsRemote()==false&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&
            ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&!actor.IsDead()&&actor.Died==(int)original.Copy().Attribute("deaths")&&
            RebirthOriginalActionScope.Matches(actor,original.Owner,original.Creation)&&RemoteResourceWorldIdentity.TryGet(actor.world,out string world)&&world==original.World;
    }
    private static Dictionary<string,int> ReadMap(XElement node)
    {
        if(node==null||node.HasAttributes||node.Elements().Count()>4096)throw new InvalidDataException();
        var result=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);string previous=null;
        foreach(var entry in node.Elements())
        {
            string key=(string)entry.Attribute("key");
            if(entry.Name!="item"||entry.Attributes().Count()!=2||entry.HasElements||!string.IsNullOrWhiteSpace(entry.Value)||
                string.IsNullOrEmpty(key)||key.Length>256||key!=key.Trim()||
                previous!=null&&StringComparer.Ordinal.Compare(previous,key)>=0||
                !int.TryParse((string)entry.Attribute("count"),NumberStyles.None,CultureInfo.InvariantCulture,out int count)||count<1||result.ContainsKey(key))throw new InvalidDataException();
            var item=ItemClass.GetItem(key,false);if(item==null||item.IsEmpty()||item.ItemClass?.GetItemName()!=key)throw new InvalidDataException();
            result.Add(key,count);previous=key;
        }
        return result;
    }
    private static ItemStack[] ReadSlots(XElement node)
    {
        if(node==null||node.HasAttributes||node.HasElements)throw new InvalidDataException();
        var result=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Value));
        if(result==null||result.Length>short.MaxValue||result.Any(stack=>stack==null||stack.count<0||!stack.IsEmpty()&&stack.count<1))throw new InvalidDataException();
        return result;
    }
    private static Dictionary<string,long> Totals(IEnumerable<ItemStack> slots)
    {
        var result=new Dictionary<string,long>(StringComparer.Ordinal);
        foreach(var stack in slots.Where(stack=>!stack.IsEmpty()))
        {string key=RebirthNativeItemCodec.Encode(stack.itemValue);result.TryGetValue(key,out long count);result[key]=checked(count+stack.count);}
        return result;
    }
    private static bool SameTotals(Dictionary<string,long> a,Dictionary<string,long> b)=>a.Count==b.Count&&a.All(pair=>b.TryGetValue(pair.Key,out long count)&&count==pair.Value);
    private static bool ReadNative(string text,out RebirthNpcNativeStackSet native)
    {
        native=null;if(string.IsNullOrEmpty(text)||text.Length>2097152)return false;
        using(var input=new StringReader(text))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2097152}))
            return RebirthNpcNativeStackSet.TryRead(XElement.Load(reader,LoadOptions.None),out native);
    }

    // Planning captures an existing authoritative source. No working source is
    // mutated and no journal/source receipt is published here.
    internal static XElement CaptureNpcSource(EntityPlayer actor,RemoteNpcResourceSource source,ItemStack[] after,ItemStack[] paid,Guid mutation)
    {
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||source==null||mutation==Guid.Empty||source.NativeActor?.world!=actor.world||
            !ReferenceEquals(actor.world.GetEntity(source.NativeActor.entityId),source.NativeActor)||!source.IsLoaded||source.IsBusy||!source.IsSnapshotCurrent()||
            !source.IsAuthorized(actor,false,out _)||after==null||paid==null)throw new InvalidDataException();
        var before=ItemStack.Clone(source.Slots);if(before.Length!=after.Length)throw new InvalidDataException();
        var locks=new bool[before.Length];for(int index=0;index<locks.Length;index++)locks[index]=source.SlotLocks!=null&&index<source.SlotLocks.Length&&source.SlotLocks[index];
        var quantities=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);var next=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        var reserved=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);string nativeBefore="",nativeAfter="";
        if(!source.IsDogStorage)
        {
            var snapshot=RebirthNpcInventoryTransactionService.GetSnapshot(source.NativeIdentity);
            if(snapshot.Revision!=source.OriginalRevision||snapshot.Revision==uint.MaxValue)throw new InvalidDataException();
            string Key(string text)
            {var item=ItemClass.GetItem(text,false);if(item==null||item.IsEmpty())throw new InvalidDataException();return item.ItemClass.GetItemName();}
            foreach(var pair in snapshot.Quantities){if(pair.Value<0)throw new InvalidDataException();if(pair.Value>0){string key=Key(pair.Key);quantities.Add(key,pair.Value);next.Add(key,pair.Value);}}
            foreach(var pair in snapshot.Reservations){if(pair.Value<0)throw new InvalidDataException();if(pair.Value>0)reserved.Add(Key(pair.Key),pair.Value);}
            nativeBefore=snapshot.NativeStacks?.Write().ToString(SaveOptions.DisableFormatting)??"";
            nativeAfter=snapshot.NativeStacks?.WithRevision(snapshot.Revision+1).Write().ToString(SaveOptions.DisableFormatting)??"";
            for(int index=0;index<before.Length;index++)
            {
                if(before[index].IsEmpty())continue;string key=before[index].itemValue.ItemClass.GetItemName();
                locks[index]=RebirthCompanionInventoryLockService.IsNpcItemLocked(source.NativeIdentity,key);
                int debit=before[index].count-(after[index]?.count??0);if(debit<0||debit>0&&locks[index]||!next.TryGetValue(key,out int count)||debit>count)throw new InvalidDataException();
                int remaining=count-debit;if(remaining==0)next.Remove(key);else next[key]=remaining;
            }
        }
        XElement Map(string name,Dictionary<string,int> values)=>new XElement(name,values.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>new XElement("item",new XAttribute("key",pair.Key),new XAttribute("count",pair.Value))));
        string Image(ItemStack[] items)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(items));
        return new XElement("source",new XAttribute("id",source.StableId),new XAttribute("kind",(int)RemoteResourceSourceKind.NpcStorage),
            new XAttribute("attempted",0),new XAttribute("published",0),new XAttribute("beforeProof",""),new XAttribute("proof",""),
            new XElement("before",Image(before)),new XElement("after",Image(after)),new XElement("paid",Image(paid)),new XElement("locks",new string(locks.Select(value=>value?'1':'0').ToArray())),
            new XElement("npc",new XAttribute("stable",source.NativeIdentity.ToString()),new XAttribute("revision",source.OriginalRevision),new XAttribute("mutation",mutation.ToString("N")),new XAttribute("dog",source.IsDogStorage?1:0),
                Map("beforeQuantities",quantities),Map("afterQuantities",next),Map("reservations",reserved),new XElement("nativeBefore",nativeBefore),new XElement("nativeAfter",nativeAfter)));
    }

    // The immutable source schema is deliberately separate from the journal's
    // monotonic stages. A structurally valid journal never grants a malformed
    // NPC plan permission to call an inventory mutation service.
    private static RebirthRemoteResourceNpcEffectPlan ReadNpc(RebirthRemoteResourceDurableJournal.Record original,XElement source,Func<bool> current)
    {
        if((int?)source.Attribute("kind") is not int kind||kind<(int)RemoteResourceSourceKind.StaticContainer||kind>(int)RemoteResourceSourceKind.NpcStorage)throw new InvalidDataException();
        if(kind!=(int)RemoteResourceSourceKind.NpcStorage){if(source.Element("npc")!=null)throw new InvalidDataException();return null;}
        string sourceId=(string)source.Attribute("id");var npc=source.Element("npc");
        if(npc==null||npc.Attributes().Count()!=4||npc.Elements().Count()!=5||
            !RebirthNpcStableId.TryParse((string)npc.Attribute("stable"),out var id)||id.IsEmpty||id.ToString()!=(string)npc.Attribute("stable")||sourceId!="N:"+id||
            !uint.TryParse((string)npc.Attribute("revision"),NumberStyles.None,CultureInfo.InvariantCulture,out uint revision)||revision==uint.MaxValue||
            !Guid.TryParseExact((string)npc.Attribute("mutation"),"N",out var mutation)||mutation==Guid.Empty||mutation.ToString("N")!=(string)npc.Attribute("mutation")||
            !int.TryParse((string)npc.Attribute("dog"),out int dog)||dog<0||dog>1)throw new InvalidDataException();
        var before=ReadMap(npc.Element("beforeQuantities"));var after=ReadMap(npc.Element("afterQuantities"));var reservations=ReadMap(npc.Element("reservations"));
        string nativeBefore=npc.Element("nativeBefore")?.Value??throw new InvalidDataException();
        string nativeAfter=npc.Element("nativeAfter")?.Value??throw new InvalidDataException();
        if(npc.Element("nativeBefore").HasElements||npc.Element("nativeAfter").HasElements)throw new InvalidDataException();
        RebirthNpcNativeStackSet custody=null;
        if(nativeBefore.Length!=0)
        {
            if(!ReadNative(nativeBefore,out custody)||custody.Owner!=id.ToString()||custody.Revision!=revision||
                custody.Write().ToString(SaveOptions.DisableFormatting)!=nativeBefore||!custody.FitsQuantities(before)||
                custody.WithRevision(revision+1).Write().ToString(SaveOptions.DisableFormatting)!=nativeAfter)throw new InvalidDataException();
        }
        else if(nativeAfter.Length!=0)throw new InvalidDataException();
        var beforeSlots=ReadSlots(source.Element("before"));var afterSlots=ReadSlots(source.Element("after"));var paid=ReadSlots(source.Element("paid"));
        string lockText=source.Element("locks")?.Value??throw new InvalidDataException();
        if(beforeSlots.Length!=afterSlots.Length||lockText.Length!=beforeSlots.Length||lockText.Any(value=>value!='0'&&value!='1'))throw new InvalidDataException();
        var locks=lockText.Select(value=>value=='1').ToArray();bool changed=false;
        for(int index=0;index<beforeSlots.Length;index++)
        {
            var old=beforeSlots[index];var next=afterSlots[index];
            if(next.count>old.count||!next.IsEmpty()&&(old.IsEmpty()||RebirthNativeItemCodec.Encode(next.itemValue)!=RebirthNativeItemCodec.Encode(old.itemValue)))throw new InvalidDataException();
            if(next.count!=old.count){if(locks[index])throw new InvalidDataException();changed=true;}
        }
        if(!changed||!SameTotals(Totals(beforeSlots),Totals(afterSlots.Concat(paid))))throw new InvalidDataException();
        if(dog==0)
        {
            if(before.Keys.Any(key=>!after.ContainsKey(key)&&reservations.TryGetValue(key,out int reserved)&&reserved>0)||after.Keys.Any(key=>!before.ContainsKey(key)))throw new InvalidDataException();
            var logicalPaid=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
            foreach(var key in before.Keys)
            {
                after.TryGetValue(key,out int next);reservations.TryGetValue(key,out int reserved);
                if(next>before[key]||next<(long)reserved+(custody?.GetQuantity(key)??0))throw new InvalidDataException();
                if(before[key]!=next)logicalPaid.Add(key,(long)before[key]-next);
            }
            foreach(var reservation in reservations)if(!before.TryGetValue(reservation.Key,out int quantity)||reservation.Value>quantity-(custody?.GetQuantity(reservation.Key)??0))throw new InvalidDataException();
            var actual=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
            foreach(var stack in paid.Where(stack=>!stack.IsEmpty()))
            {
                string key=stack.itemValue.ItemClass?.GetItemName();var canonical=ItemClass.GetItem(key,false);
                if(canonical==null||canonical.IsEmpty()||RebirthNativeItemCodec.Encode(canonical)!=RebirthNativeItemCodec.Encode(stack.itemValue))throw new InvalidDataException();
                actual.TryGetValue(key,out long count);actual[key]=checked(count+stack.count);
            }
            if(actual.Count!=logicalPaid.Count||logicalPaid.Any(pair=>!actual.TryGetValue(pair.Key,out long count)||count!=pair.Value))throw new InvalidDataException();
        }
        else if(before.Count!=0||after.Count!=0||reservations.Count!=0||nativeBefore.Length!=0||nativeAfter.Length!=0)throw new InvalidDataException();
        return new RebirthRemoteResourceNpcEffectPlan(original.Operation,mutation,id,original.World,original.Owner,original.Creation,sourceId,original.Binding,
            revision,(int)original.Copy().Attribute("deaths"),(int)source.Attribute("attempted"),dog==1,before,after,reservations,nativeBefore,nativeAfter,beforeSlots,afterSlots,locks,current);
    }

    internal static bool TryReadNpcEffectPlan(EntityPlayer actor,Guid operation,string sourceId,out RebirthRemoteResourceNpcEffectPlan plan)
    {
        plan=null;
        try
        {
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||!RebirthRemoteResourceDurableJournal.TryRead(identity.StorageKey,operation,out var original,out _)||
                original==null||original.Phase>=4||!ActorScope(actor,original))return false;
            var source=original.Copy().Element("sources").Elements("source").SingleOrDefault(node=>(string)node.Attribute("id")==sourceId);if(source==null)return false;
            long revision=original.Revision;string binding=original.Binding;
            bool Current()=>ActorScope(actor,original)&&RebirthRemoteResourceDurableJournal.TryRead(original.Owner,operation,out var saved,out _)&&saved!=null&&saved.Revision==revision&&saved.Binding==binding&&saved.Phase<4;
            plan=ReadNpc(original,source,Current);return plan!=null&&plan.IsCurrent();
        }
        catch{plan=null;return false;}
    }
    internal static bool TryObserveSavedNpc(RebirthRemoteResourceNpcEffectPlan plan,bool after,out string proof)
    {
        proof=null;
        try
        {
            if(plan?.DogStorage==true)return RebirthRemoteResourceDogTransfer.TryObserveSaved(plan,after,out proof);
            if(plan==null||!plan.IsCurrent()||after&&plan.Attempted!=1||
                !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(plan.NpcId,out var saved))return false;
            bool Equal(IReadOnlyDictionary<string,int> expected,Dictionary<string,int> actual)=>actual!=null&&expected.Count==actual.Count&&expected.All(pair=>actual.TryGetValue(pair.Key,out int count)&&count==pair.Value);
            if(!after)
            {
                if(saved.Revision!=plan.Revision||!Equal(plan.BeforeQuantities,saved.Quantities)||!Equal(plan.Reservations,saved.Reservations)||
                    (saved.NativeStacks?.Write().ToString(SaveOptions.DisableFormatting)??"")!=plan.BeforeNative||saved.LogisticsEffects?.ContainsKey(plan.MutationId)==true)return false;
                proof=RebirthRemoteResourceDurableJournal.Digest("before|"+plan.Binding+"|"+plan.SourceId);return plan.IsCurrent();
            }
            string receipt=RebirthLogisticsNpcEffectReceipt.ExpectedRemoteResource(plan);
            if(saved.Revision<plan.Revision+1||saved.LogisticsEffects==null||!saved.LogisticsEffects.TryGetValue(plan.MutationId,out string actualReceipt)||actualReceipt!=receipt)return false;
            // At the immediate revision the complete state must match. Later
            // legitimate NPC writes may change it, but cannot manufacture or
            // replace this persisted typed actual-effect receipt.
            if(saved.Revision==plan.Revision+1&&(!Equal(plan.AfterQuantities,saved.Quantities)||!Equal(plan.Reservations,saved.Reservations)||
                (saved.NativeStacks?.Write().ToString(SaveOptions.DisableFormatting)??"")!=plan.AfterNative))return false;
            proof=RebirthRemoteResourceDurableJournal.Digest(receipt);return plan.IsCurrent();
        }
        catch{proof=null;return false;}
    }
    private static RebirthNpcOriginalEffectAdmission.Pending Pending(RebirthRemoteResourceDurableJournal.Record original)
    {
        var claims=new List<RebirthNpcOriginalEffectAdmission.Claim>();var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var source in original.Copy().Element("sources").Elements("source"))
        {
            var plan=ReadNpc(original,source,()=>false);if(plan==null)continue;
            if(!ids.Add(plan.NpcId.ToString()))throw new InvalidDataException();
            claims.Add(new RebirthNpcOriginalEffectAdmission.Claim(plan.NpcId,plan.Binding,plan.DogStorage,plan.DogStorage?new Guid[0]:new[]{plan.MutationId}));
        }
        return new RebirthNpcOriginalEffectAdmission.Pending("remoteResource",original.World,original.Owner,original.Creation,(int)original.Copy().Attribute("deaths"),original.Operation,claims);
    }
    internal static bool TryReadPendingNpcOriginals(World world,out RebirthNpcOriginalEffectAdmission.Pending[] pending)
    {
        pending=null;
        try
        {
            if(!RebirthRemoteResourceDurableJournal.TryReadPending(world,out var records))return false;
            pending=records.Select(Pending).ToArray();return true;
        }
        catch{pending=null;return false;}
    }
    private static bool IsSavedNpcTerminal(RebirthNpcOriginalEffectAdmission.Pending expected)
    {
        try
        {
            if(expected?.Domain!="remoteResource"||!RebirthRemoteResourceDurableJournal.TryRead(expected.Owner,expected.Operation,out var original,out _)||original?.Phase!=4)return false;
            var actual=Pending(original);if(actual.World!=expected.World||actual.Owner!=expected.Owner||actual.Creation!=expected.Creation||actual.Deaths!=expected.Deaths)return false;
            var before=expected.CopyClaims();var after=actual.CopyClaims();return before.Length==after.Length&&before.All(claim=>after.Any(other=>other.Npc==claim.Npc&&other.Binding==claim.Binding&&other.Dog==claim.Dog&&other.CopyEffects().SequenceEqual(claim.CopyEffects())));
        }
        catch{return false;}
    }
    internal static bool IsSavedTerminalEffectReceipt(string receipt)
    {
        try
        {
            if(!RebirthLogisticsNpcEffectReceipt.TryRead(receipt,out var effect)||effect.Domain!="remoteResource"||effect.Phase!=1||
                !RebirthRemoteResourceDurableJournal.TryRead(effect.Owner,effect.Operation,out var original,out _)||original?.Phase!=4||
                !RemoteResourceWorldIdentity.TryGet(GameManager.Instance?.World,out string world)||world!=original.World)return false;
            foreach(var source in original.Copy().Element("sources").Elements("source"))
            {
                var plan=ReadNpc(original,source,()=>false);
                if(plan!=null&&!plan.DogStorage&&(int)source.Attribute("published")==1&&plan.NpcId.ToString()==effect.Npc&&plan.MutationId==effect.Transaction&&RebirthLogisticsNpcEffectReceipt.ExpectedRemoteResource(plan)==receipt)return true;
            }
            return false;
        }
        catch{return false;}
    }
    // Retirement observation only. IsCurrent deliberately remains false: a
    // terminal/offline original can never be reapplied as a fresh NPC effect.
    internal static bool TryReadSavedTerminalNpcEffectPlan(string owner,Guid operation,string sourceId,out RebirthRemoteResourceNpcEffectPlan plan)
    {
        plan=null;
        try
        {
            if(!RebirthRemoteResourceDurableJournal.TryRead(owner,operation,out var original,out _)||original?.Phase!=4||
                !RemoteResourceWorldIdentity.TryGet(GameManager.Instance?.World,out string world)||world!=original.World)return false;
            var source=original.Copy().Element("sources").Elements("source").SingleOrDefault(value=>(string)value.Attribute("id")==sourceId);
            if(source==null||(int)source.Attribute("published")!=1)return false;
            plan=ReadNpc(original,source,()=>false);return plan!=null;
        }
        catch{plan=null;return false;}
    }
}
