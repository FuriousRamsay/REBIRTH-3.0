using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Globalization;

// Positive delivery of the immutable unused paid payload. No source rollback,
// queue-absence inference, or general incoming owner snapshot authority.
internal static class RebirthRemoteResourceRepairRefund
{
    private static RebirthCamperTransferJournal NoUses()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteRepairNoUse"));
    private static RebirthCamperTransferJournal Refunds()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteRepairRefunds"));
    private static RebirthCamperTransferJournal Terminals()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteRepairRefundTerminals"));
    private static Guid ChildFor(RebirthRemoteResourceClientOriginal.Intent original,int deaths)=>RebirthCookingRemoteAdmissionBackend.StationKey(original.World,original.Operation.ToString("N")+(original.Kind==RemoteResourceClientOperation.Craft?"|unused-craft-refund":"|unused-repair-refund")+(deaths==original.Deaths?"":"|adopted:"+deaths.ToString(CultureInfo.InvariantCulture)));
    internal static Guid Child(RebirthRemoteResourceClientOriginal.Intent original)
    {return Read(Refunds(),original,out var request)?ChildFor(original,(int?)request.Attribute("deaths")??original.Deaths):ChildFor(original,original.Deaths);}
    internal static bool Current(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(RebirthRemoteResourceClientOriginal.Current(player,original))return true;
        try
        {
            if(!RebirthRemoteResourceRepairAdoption.QueryScope(player,original)||!Read(NoUses(),original,out var noUse)||
                !Guid.TryParseExact((string)noUse.Attribute("source"),"N",out var source)||
                !HasNoUse(original,source,(string)noUse.Attribute("binding"),(string)noUse.Attribute("proof"))||
                (string)noUse.Attribute("proof")!=RebirthRemoteResourceRepairAdoption.Proof(original,(string)noUse.Attribute("binding")))return false;
            return !Read(Refunds(),original,out var refund)||(int?)refund.Attribute("deaths")==player.Died;
        }
        catch{return false;}
    }
    private static bool Hex(string text)=>text?.Length==64&&text.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    private static bool Save(RebirthCamperTransferJournal journal,RebirthRemoteResourceClientOriginal.Intent original,XElement image)
    {
        byte[] bytes=Encoding.UTF8.GetBytes(image.ToString(SaveOptions.DisableFormatting));var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
        if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==RebirthCamperTransferJournal.Phase.Applied;
    }
    private static bool Read(RebirthCamperTransferJournal journal,RebirthRemoteResourceClientOriginal.Intent original,out XElement image)
    {
        image=null;var record=journal.Read(original.Operation);
        if(record==null||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||!record.Before.SequenceEqual(record.After)||record.Before.Length>24*1024*1024)return false;
        image=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),24*1024*1024);
        return (string)image.Attribute("original")==original.Digest;
    }
    internal static bool HasNoUse(RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof)
    {
        try{return original!=null&&Read(NoUses(),original,out var body)&&NoUses().Read(original.Operation).State==RebirthCamperTransferJournal.Phase.Applied&&body.Name=="repairNoUse"&&body.Attributes().Count()==4&&!body.Nodes().Any()&&
            (string)body.Attribute("source")==source.ToString("N")&&(string)body.Attribute("binding")==binding&&(string)body.Attribute("proof")==proof&&Hex(proof);}
        catch{return false;}
    }
    internal static bool Bootstrap(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Func<bool> originalScope)
    {
        if(!Read(NoUses(),original,out var noUse)||!Guid.TryParseExact((string)noUse.Attribute("source"),"N",out var source)||
            !HasNoUse(original,source,(string)noUse.Attribute("binding"),(string)noUse.Attribute("proof")))return false;
        if(Read(Refunds(),original,out var request))
        {
            var before=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("beforeActor").Value);var after=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("afterActor").Value);
            RebirthRemoteResourceOwnerAction.TryHoldRefund(player,original,source,(string)noUse.Attribute("binding"),before,after,originalScope);
        }
        // A known owed claim with no prepared native child must leave ordinary
        // owner work available so the owner can create room for its delivery.
        return true;
    }
    internal static bool AcceptNoUse(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof,Func<bool> authenticated)
    {
        try
        {
            if(!RebirthRemoteResourceRepairAdoption.QueryScope(player,original)||authenticated?.Invoke()!=true||!Hex(proof))return false;
            string expected;
            if(original.Kind==RemoteResourceClientOperation.Craft)expected=player.Died>original.Deaths?RebirthRemoteResourceRepairAdoption.Proof(original,binding):RebirthRemoteResourceCraftSettlement.UnusedProof(original,binding);
            else if(RebirthRemoteResourceClientOriginal.TryReadRepairAction(original,out var action))
            {
                if(!RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(action.Element("payment").Value,out var payment)||payment.SourceOperation!=source||payment.Binding!=binding)return false;
                expected=RebirthRemoteResourceDurableJournal.Digest("repair-unused|"+payment.Encode()+"|"+action.Element("queuedInput").Value+"|"+(int)action.Element("amount")+"|"+
                    action.Element("beforeActor").Value+"|"+payment.SourceKind+":"+payment.SourceSlot+"|"+payment.InputHash);
            }
            else
            {
                var root=original.Copy();var input=root.Element("action");
                expected=player.Died>original.Deaths?RebirthRemoteResourceRepairAdoption.Proof(original,binding):RebirthRemoteResourceDurableJournal.Digest("repair-ungranted|"+original.Digest+"|"+binding+"|"+root.Element("actorBefore").Value+"|"+
                    (int)input.Attribute("sourceKind")+":"+(int)input.Attribute("sourceSlot")+"|"+input.Element("nativeInput").Value);
            }
            if(expected!=proof||!Save(NoUses(),original,new XElement("repairNoUse",new XAttribute("original",original.Digest),new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),new XAttribute("proof",proof))))return false;
            if(Read(Refunds(),original,out _))return true;
            return RebirthRemoteResourceOwnerAction.ReleaseRepairNoUse(player,original,source,binding,proof);
        }
        catch{return false;}
    }
    private static bool Plan(RebirthCamperEnergyActorImage before,ItemStack[] paid,out RebirthCamperEnergyActorImage after)
    {
        after=null;
        if(before==null||paid==null||paid.Length<1||before.BagLocks==null||before.BagLocks.Length!=before.Bag.Length||
            !RebirthRecyclerOwnerDelivery.Plan(before.Bag,before.Belt,before.BagLocks,new bool[before.Belt.Length],0,paid,out var bag,out var belt))return false;
        after=before.Copy();after.Bag=bag;after.Belt=belt;return true;
    }
    internal static bool TryPrepare(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof,ItemStack[] paid,Func<bool> authenticated,out XElement request)
    {
        request=null;
        try
        {
            if(!Current(player,original)||authenticated?.Invoke()!=true||!HasNoUse(original,source,binding,proof)||
                !RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out var savedSource,out var savedBinding,out var savedPaid)||savedSource!=source||savedBinding!=binding||
                !RebirthCamperInventorySnapshot.Capture(savedPaid).SequenceEqual(RebirthCamperInventorySnapshot.Capture(paid)))return false;
            if(!Read(Refunds(),original,out request))
            {
                // A full bag leaves the canonical owed claim retained. The old
                // live hold is released only after positive no-use, so the owner
                // can free space without an unsolicited item drop or bag rewrite.
                var before=RebirthCamperEnergyActorImage.Capture(player);if(!Plan(before,paid,out var after))return false;
                request=new XElement("repairRefund",new XAttribute("original",original.Digest),new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),new XAttribute("noUseProof",proof),new XAttribute("child",Child(original).ToString("N")),
                    player.Died==original.Deaths?null:new XAttribute("deaths",player.Died),new XElement("beforeActor",RebirthRemoteResourceClientOriginal.ActorImage(before)),new XElement("afterActor",RebirthRemoteResourceClientOriginal.ActorImage(after)));
                request.SetAttributeValue("child",ChildFor(original,player.Died).ToString("N"));
                byte[] bytes=Encoding.UTF8.GetBytes(request.ToString(SaveOptions.DisableFormatting));Refunds().Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            }
            var b=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("beforeActor").Value);var a=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("afterActor").Value);
            if(!IsPrepared(original,source,binding,b,a)||!RebirthRemoteResourceOwnerAction.TryHoldRefund(player,original,source,binding,b,a,authenticated))return false;
            var current=RebirthCamperEnergyActorImage.Capture(player);
            if(b.Matches(current))
            {player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(Child(original)),1f,true);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);}
            return b.Matches(current)||a.Matches(current);
        }
        catch{request=null;return false;}
    }
    internal static bool IsPrepared(RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after)
    {
        try
        {
            if(!Read(Refunds(),original,out var body)||body.Name!="repairRefund"||body.Attributes().Count()!=(body.Attribute("deaths")==null?5:6)||body.Elements().Count()!=2||
                (string)body.Attribute("source")!=source.ToString("N")||(string)body.Attribute("binding")!=binding||(string)body.Attribute("child")!=Child(original).ToString("N")||
                !HasNoUse(original,source,binding,(string)body.Attribute("noUseProof"))||
                !RebirthRemoteResourceClientOriginal.ReadActor(body.Element("beforeActor").Value).Matches(before)||!RebirthRemoteResourceClientOriginal.ReadActor(body.Element("afterActor").Value).Matches(after)||
                !RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out var saved,out var savedBinding,out var paid)||saved!=source||savedBinding!=binding||!Plan(before,paid,out var planned))return false;
            return after.Matches(planned);
        }
        catch{return false;}
    }
    internal static bool Apply(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,XElement request,Func<bool> permission)
    {
        try
        {
            if(!Current(player,original)||permission?.Invoke()!=true||!Guid.TryParseExact((string)request.Attribute("source"),"N",out var source))return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("beforeActor").Value);var after=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("afterActor").Value);string binding=(string)request.Attribute("binding");
            if(!IsPrepared(original,source,binding,before,after)||!RebirthRemoteResourceOwnerAction.TryHoldRefund(player,original,source,binding,before,after,permission))return false;
            var journal=Refunds();var record=journal.Read(original.Operation);
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State!=RebirthCamperTransferJournal.Phase.Uncertain&&record.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
            for(int slot=0;slot<after.Bag.Length;slot++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(before.Bag[slot],after.Bag[slot])&&
                !RebirthRemoteResourceOwnerAction.WriteOriginalSlot(player,original.Operation,player.bag.ItemGrid,slot,after.Bag[slot]))return false;
            if(!after.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(Child(original)),2f,true);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
        }
        catch{return false;}
    }
    internal static bool IsApplied(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,XElement request)
    {
        try
        {
            if(!Current(player,original)||Refunds().Read(original.Operation)?.State!=RebirthCamperTransferJournal.Phase.Uncertain||
                !Guid.TryParseExact((string)request.Attribute("source"),"N",out var source))return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("beforeActor").Value);var after=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("afterActor").Value);
            return IsPrepared(original,source,(string)request.Attribute("binding"),before,after)&&after.Matches(RebirthCamperEnergyActorImage.Capture(player))&&
                player.Buffs.GetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(Child(original)))==2f;
        }
        catch{return false;}
    }
    internal static bool HasTerminal(RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof)
    {
        try{return original!=null&&Read(Terminals(),original,out var body)&&Terminals().Read(original.Operation).State==RebirthCamperTransferJournal.Phase.Applied&&body.Name=="repairRefundTerminal"&&body.Attributes().Count()==5&&!body.Nodes().Any()&&
            (string)body.Attribute("source")==source.ToString("N")&&(string)body.Attribute("binding")==binding&&(string)body.Attribute("proof")==proof&&Hex(proof)&&
            Read(Refunds(),original,out var refund)&&(string)body.Attribute("refundDigest")==RebirthRemoteResourceDurableJournal.Digest(refund.ToString(SaveOptions.DisableFormatting));}
        catch{return false;}
    }
    internal static bool Complete(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof,Func<bool> authenticated)
    {
        try
        {
            if(!Current(player,original)||authenticated?.Invoke()!=true||!Hex(proof)||!Read(Refunds(),original,out var request)||
                (string)request.Attribute("source")!=source.ToString("N")||(string)request.Attribute("binding")!=binding||!HasNoUse(original,source,binding,(string)request.Attribute("noUseProof")))return false;
            string digest=RebirthRemoteResourceDurableJournal.Digest(request.ToString(SaveOptions.DisableFormatting));
            if(proof!=RebirthRemoteResourceDurableJournal.Digest("repair-refund|"+binding+"|"+(string)request.Attribute("noUseProof")+"|"+digest)||
                !Save(Terminals(),original,new XElement("repairRefundTerminal",new XAttribute("original",original.Digest),new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),new XAttribute("proof",proof),new XAttribute("refundDigest",digest))))return false;
            var journal=Refunds();var record=journal.Read(original.Operation);
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            return RebirthRemoteResourceClientOriginal.CompleteRefundOriginal(player,original,source,binding,proof,authenticated);
        }
        catch{return false;}
    }
    internal static bool Host(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,XElement request,bool complete,out bool permission)
    {
        permission=false;
        try
        {
            int deliveryDeaths=(int?)request.Attribute("deaths")??original.Deaths;
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||actor.Died!=deliveryDeaths||actor.world.worldState.Guid!=original.World||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||identity.CanonicalId!=original.Owner||!RebirthOriginalActionScope.Matches(actor,identity.StorageKey,original.Creation)||
                !RemoteResourceWorldIdentity.TryGet(actor.world,out string sourceWorld)||!Guid.TryParseExact((string)request.Attribute("source"),"N",out var source)||
                source!=RebirthRemoteResourceDurableJournal.Operation(sourceWorld,identity.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind)||
                !RebirthRemoteResourceDurableJournal.TryRead(identity.StorageKey,source,out var retained,out _)||retained==null||retained.Binding!=(string)request.Attribute("binding"))return false;
            var root=retained.Copy();string noUse=(string)root.Attribute("ownerNoUseProof");
            if(RebirthRemoteResourceFetchedStationOriginal.IsCandidate(original)&&!RebirthRemoteResourceFetchedStationOriginal.ValidateFetchedNoUseRoot(root))return false;
            if(deliveryDeaths!=original.Deaths&&(deliveryDeaths<original.Deaths||root.Element("ownerAction")!=null||
                !RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(retained)||!RebirthRemoteResourceRepairAdoption.HostScope(actor,original)||noUse!=RebirthRemoteResourceRepairAdoption.Proof(original,retained.Binding)))return false;
            if(root.Element("ownerAction")!=null)
            {
                if(original.Kind==RemoteResourceClientOperation.Craft)
                {if(!RebirthRemoteResourceCraftSettlement.MatchesRootAction(original,retained))return false;}
                else if(!RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(root.Element("ownerAction").Element("payment")?.Value,out var payment)||payment.ClientOriginal!=original.Operation)return false;
            }
            if(request.Name!="repairRefund"||request.Attributes().Count()!=(request.Attribute("deaths")==null?5:6)||request.Elements().Count()!=2||(string)request.Attribute("original")!=original.Digest||
                !Hex(noUse)||noUse!=(string)request.Attribute("noUseProof")||(string)request.Attribute("child")!=ChildFor(original,deliveryDeaths).ToString("N")||((int?)root.Attribute("ownerRejecting")??0)!=1)return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("beforeActor").Value);var after=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("afterActor").Value);
            var paid=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("removed").Value));if(!Plan(before,paid,out var planned)||!planned.Matches(after))return false;
            string digest=RebirthRemoteResourceDurableJournal.Digest(request.ToString(SaveOptions.DisableFormatting));
            string proof=RebirthRemoteResourceDurableJournal.Digest("repair-refund|"+retained.Binding+"|"+noUse+"|"+digest);
            if(retained.Phase==4)return (string)root.Attribute("disposition")=="refunded"&&(string)root.Attribute("ownerProof")==proof&&XNode.DeepEquals(root.Element("ownerRefund")?.Element("repairRefund"),request);
            bool partialPersonal=original.Kind==RemoteResourceClientOperation.Craft&&retained.Phase==3&&(int)root.Attribute("outcome")==2&&root.Element("ownerAction")==null;
            if(retained.Phase!=2&&!partialPersonal)return false;
            if(!RebirthCamperEnergySavedOwner.TryRead(identity,out var savedActor,out var saved)||saved.id!=actor.entityId||saved.deaths!=deliveryDeaths||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(ChildFor(original,deliveryDeaths)),out float phase,out bool present)||!present)return false;
            var existing=root.Element("ownerRefund");
            if(existing==null)
            {
                if(complete||phase!=1f||!before.Matches(savedActor))return false;
                var wrapper=new XElement("ownerRefund",new XAttribute("nativeBeforeProof",RebirthRemoteResourceDurableJournal.Digest(digest+"|1|"+RebirthRemoteResourceClientOriginal.ActorImage(savedActor))),new XElement(request));
                bool Current()=>ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&!actor.IsDead()&&actor.Died==deliveryDeaths&&RebirthOriginalActionScope.Matches(actor,identity.StorageKey,original.Creation);
                if(!RebirthRemoteResourceDurableJournal.TryAdvance(retained,value=>value.Add(new XElement(wrapper)),Current,out var admitted))return false;
                permission=admitted!=null&&(admitted.Phase==2||partialPersonal&&admitted.Phase==3)&&XNode.DeepEquals(admitted.Copy().Element("ownerRefund"),wrapper);return permission;
            }
            if(!XNode.DeepEquals(existing.Element("repairRefund"),request)||!Hex((string)existing.Attribute("nativeBeforeProof")))return false;
            if(!complete){permission=phase==1f&&before.Matches(savedActor);return permission;}
            if(phase!=2f||!after.Matches(savedActor))return false;
            bool StillCurrent()=>ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&!actor.IsDead()&&actor.Died==deliveryDeaths&&RebirthOriginalActionScope.Matches(actor,identity.StorageKey,original.Creation);
            return RebirthRemoteResourceDurableJournal.TryAdvance(retained,value=>{value.SetAttributeValue("phase",4);value.SetAttributeValue("disposition","refunded");value.SetAttributeValue("ownerProof",proof);},StillCurrent,out var terminal)&&terminal?.Phase==4;
        }
        catch{permission=false;return false;}
    }
}
