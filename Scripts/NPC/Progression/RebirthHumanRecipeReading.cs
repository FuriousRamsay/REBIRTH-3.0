using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// NPC literature follows the player's reusable-book rule: the actual native item
// remains in custody, and only a completed, saved physical study grants knowledge.
internal static class RebirthHumanRecipeReading
{
    internal sealed class Status
    {
        internal readonly string ItemId;
        internal readonly string KnowledgeId;
        internal readonly float Progress;
        internal Status(string itemId,string knowledgeId,float progress)
        { ItemId=itemId; KnowledgeId=knowledgeId; Progress=progress; }
    }
    private sealed class Session
    {
        internal int EntityId,ToolbeltSlot,ItemType;
        internal bool CancelRequested;
        internal ushort Seed;
        internal string ItemId,KnowledgeId;
        internal float Progress,Seconds,LastTime,LastHealth,LastSave;
    }
    private static readonly Dictionary<RebirthNpcStableId,Session> Active=new Dictionary<RebirthNpcStableId,Session>();
    private static readonly Dictionary<RebirthNpcStableId,float> Pending=new Dictionary<RebirthNpcStableId,float>();
    private static World currentWorld;
    private static float nextRecoveryScan;
    private static int recoveryCursor;

    internal static bool Knows(RebirthNpcStableId id,string recipeName)
    {
        return RebirthHumanProgression.TryGet(id,out var record)&&
            RebirthCapabilityService.EvaluateNpcRecipe(record,recipeName).IsAllowed;
    }

    // A work executor may request reading only after obtaining the book in the NPC's
    // native toolbelt and selecting it. Never accept an item ID alone as ownership.
    internal static bool TryBegin(EntityRebirthHumanoidNPC npc,out string reason)
    {
        reason="unavailable";
        if(!Valid(npc)||npc.Hand.IsBusy||npc.GetAttackTarget()!=null)return false;
        var id=npc.RebirthRuntimeState.StableId;
        int slot=npc.inventory.SelectedSlot;
        if(slot<0||slot>=npc.inventory.SlotCount)return false;
        var stack=npc.inventory.GetItem(slot);
        if(stack==null||stack.IsEmpty()||stack.itemValue?.ItemClass==null)return false;
        string itemId=stack.itemValue.ItemClass.GetItemName();
        if(!RebirthProgressionRuntimeConfig.TryGetLiterature(itemId,out var book)||book==null||
            !string.Equals(book.Kind,"discovery",StringComparison.OrdinalIgnoreCase)||
            string.IsNullOrEmpty(book.KnowledgeId)||book.StudySeconds<5f)return false;
        if(!RebirthHumanProgression.TryGet(id,out var record))return false;
        string marker=RebirthLiteratureService.RecipeReadMarker(book.KnowledgeId);
        if(record.Progression.KnowledgeIds.Contains(marker)){reason="already-read";return false;}
        if(Active.TryGetValue(id,out var previous))
        {
            if(previous.CancelRequested){reason="return-pending";return false;}
            if(previous.EntityId==npc.entityId&&previous.ToolbeltSlot==slot&&
                previous.ItemType==stack.itemValue.type&&previous.Seed==stack.itemValue.Seed)
            {reason="reading";return true;}
            if(!SaveProgress(id,null,previous,false))
            {reason="save-pending";return false;}
            Active.Remove(id);
            if(!RebirthHumanProgression.TryGet(id,out record))return false;
        }
        float progress=0f;record.Progression.LiteratureStudyProgress.TryGetValue(itemId,out progress);
        Active[id]=new Session{EntityId=npc.entityId,ToolbeltSlot=slot,ItemType=stack.itemValue.type,
            Seed=stack.itemValue.Seed,ItemId=itemId,KnowledgeId=book.KnowledgeId,
            Progress=Mathf.Clamp01(progress),Seconds=Mathf.Max(2f,book.StudySeconds*
                RebirthSandboxOptionManager.Current.LiteratureStudyTimeMultiplier*
                RebirthTraitGameplayModifierService.GetPhysicalLiteratureStudyTimeMultiplier(record)*
                RebirthTeachingService.GetSoloStudyTimeMultiplier(record)),
            LastTime=Time.unscaledTime,LastHealth=Health(npc)};
        reason="reading";return true;
    }

    internal static bool TryBeginFromOwnedBag(EntityPlayer owner,EntityRebirthHumanoidNPC npc,int bagIndex,out string reason)
    {
        reason="unavailable";
        if(owner==null||!Valid(npc)||owner.IsDead()||
            npc.RebirthRuntimeState.OwnershipKind!=RebirthNpcOwnershipKind.Player||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity)||
            !string.Equals(identity.CanonicalId,npc.RebirthRuntimeState.OwnerId,StringComparison.OrdinalIgnoreCase)||
            (owner.position-npc.position).sqrMagnitude>
            RebirthCompanionService.StorageDistance*RebirthCompanionService.StorageDistance||
            !RebirthHumanAiPolicy.Ready(npc))return false;
        if(!RebirthHumanEquipment.TryHoldBagLiterature(npc,bagIndex,out reason))return false;
        Pending[npc.RebirthRuntimeState.StableId]=Time.unscaledTime+5f;
        reason="preparing";return true;
    }

    // Work uses the same native hand/bag move after its EAI-owned callback has
    // proved arrival. The owner authenticates custody but need not stand beside
    // a working companion; interaction entry above keeps its proximity rule.
    internal static bool TryBeginFromWorkBag(EntityPlayer owner,EntityRebirthHumanoidNPC npc,
        int bagIndex,RebirthNpcEmbodiedWorkIntent intent,out string reason)
    {
        reason="unavailable";
        if(owner==null||!Valid(npc)||owner.IsDead()||intent==null||
            !ReferenceEquals(owner.world,npc.world)||
            !ReferenceEquals(npc.world.GetEntity(owner.entityId),owner)||
            !ReferenceEquals(intent.Actor,npc)||!RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||
            npc.RebirthRuntimeState.OwnershipKind!=RebirthNpcOwnershipKind.Player||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity)||
            !string.Equals(identity.CanonicalId,npc.RebirthRuntimeState.OwnerId,StringComparison.OrdinalIgnoreCase)||
            !RebirthHumanAiPolicy.Ready(npc))return false;
        var cancellation=RebirthHumanEquipment.Read(npc).Elements("literatureCancel")
            .Where(x=>(string)x.Attribute("operation")==
                RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(intent.PrimaryId).ToString("N")).ToArray();
        if(cancellation.Length!=0)return false;
        if(!RebirthHumanEquipment.TryHoldBagLiterature(npc,bagIndex,out reason))return false;
        Pending[npc.RebirthRuntimeState.StableId]=Time.unscaledTime+5f;
        reason="preparing";return true;
    }

    internal static bool TryStatus(RebirthNpcStableId id,out Status status)
    {
        status=null;
        if(!Active.TryGetValue(id,out var session)||!TryActor(id,session,out var npc)||
            !StillHolding(npc,session))return false;
        status=new Status(session.ItemId,session.KnowledgeId,session.Progress);return true;
    }

    internal static bool IsPreparing(RebirthNpcStableId id)=>Pending.ContainsKey(id);

    internal static void Cancel(RebirthNpcStableId id)
    {
        TryCancelWithReceipt(id);
    }
    private static bool TryCancelWithReceipt(RebirthNpcStableId id)
    {
        if(!Active.TryGetValue(id,out var session))return false;
        session.CancelRequested=true;
        if(TryActor(id,session,out var heldActor)&&HasWorkBookMove(heldActor)&&
            !RebirthHumanEquipment.Read(heldActor).Elements("literatureCancel").Any(x=>
                (string)x.Attribute("phase")=="prepared"))return false;
        // The earned fraction predates the transition. A changed held slot or an
        // unloaded body must not erase the last checkpoint interval.
        if(!SaveProgress(id,null,session,false)||!TryActor(id,session,out var npc)||
            !RebirthHumanEquipment.TryReturnHeldLiterature(npc))return false;
        Active.Remove(id);return true;
    }
    private static bool HasWorkBookMove(EntityRebirthHumanoidNPC npc)
    {
        var equipment=RebirthHumanEquipment.Read(npc);
        var move=equipment.Element("bookMove");
        if(move==null)return false;
        string frame=(string)move.Attribute("stack");int? bag=(int?)move.Attribute("bag");
        return bag.HasValue&&equipment.Elements("workMove").Any(x=>
            (int?)x.Attribute("bagSlot")==bag&&x.Element("stack")?.Value==frame&&
            (int?)x.Attribute("phase")>=4);
    }
    private static bool CancelReader(RebirthNpcStableId id,EntityRebirthHumanoidNPC npc)
    {
        if(RebirthNpcEmbodiedWorkOwner.TryGetLiteratureIntent(npc,out var intent))
            return TryCancelWorkWithReceipt(intent);
        return !HasWorkBookMove(npc)&&TryCancelWithReceipt(id);
    }
    internal static bool TryCancelWorkWithReceipt(RebirthNpcEmbodiedWorkIntent intent)
    {
        if(intent?.Actor is not EntityRebirthHumanoidNPC npc||
            npc.RebirthRuntimeState?.StableId!=intent.StableId||
            !RebirthNpcWorkNativeTransferOriginal.TryReadSaved(intent.StableId,
                RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(intent.PrimaryId),out var original)||
            original.Npc!=intent.StableId||original.Phase<4)return false;
        if(!RebirthNpcLiteratureWorkAdapter.Book(original,out var knowledge)||
            !RebirthHumanProgression.TryGet(intent.StableId,out var record)||
            record.Progression.KnowledgeIds.Contains(RebirthLiteratureService.RecipeReadMarker(knowledge)))return false;
        var equipment=RebirthHumanEquipment.Read(npc);
        string operation=original.Operation.ToString("N");
        var receipts=equipment.Elements("literatureCancel").Where(x=>(string)x.Attribute("operation")==operation).ToArray();
        if(receipts.Length>1||equipment.Elements("literatureCancel").Any(x=>
            (string)x.Attribute("operation")!=operation&&(string)x.Attribute("phase")!="returned"))return false;
        var receipt=receipts.SingleOrDefault();
        if(receipt!=null&&(
            (int?)receipt.Attribute("bag")!=original.BagSlot||
            (string)receipt.Attribute("frame")!=original.Frame))return false;
        if(receipt==null)
        {
            var move=equipment.Element("bookMove");
            if(move==null||(string)move.Attribute("phase")!="held"||
                (int?)move.Attribute("bag")!=original.BagSlot||
                (string)move.Attribute("stack")!=original.Frame||
                string.IsNullOrEmpty((string)move.Attribute("id")))return false;
            receipt=new XElement("literatureCancel",new XAttribute("operation",operation),
                new XAttribute("bag",original.BagSlot),new XAttribute("frame",original.Frame),
                new XAttribute("binding",(string)move.Attribute("id")),new XAttribute("phase","prepared"));
            equipment.Add(receipt);
            try{RebirthHumanEquipment.Save(npc,equipment);}catch{return false;}
        }
        if((string)receipt.Attribute("phase")!="prepared"&&
            (string)receipt.Attribute("phase")!="returned")return false;
        Pending.Remove(intent.StableId);
        if(Active.ContainsKey(intent.StableId)&&!TryCancelWithReceipt(intent.StableId))return false;
        if(!RebirthHumanEquipment.TryReturnHeldLiterature(npc))return false;
        equipment=RebirthHumanEquipment.Read(npc);
        receipts=equipment.Elements("literatureCancel").Where(x=>(string)x.Attribute("operation")==operation).ToArray();
        if(receipts.Length!=1)return false;
        receipt=receipts[0];
        if(receipt==null||(string)receipt.Attribute("operation")!=operation||
            (int?)receipt.Attribute("bag")!=original.BagSlot||
            (string)receipt.Attribute("frame")!=original.Frame||
            string.IsNullOrEmpty((string)receipt.Attribute("binding"))||
            equipment.Element("bookMove")!=null||npc.bag==null||
            original.BagSlot<0||original.BagSlot>=npc.bag.SlotCount||
            RebirthHumanEquipment.Frame(npc.bag.ItemGrid[original.BagSlot])!=original.Frame||
            equipment.Element("bindings")?.Elements("binding").Count(binding=>
                (string)binding.Attribute("id")== (string)receipt.Attribute("binding")&&
                (string)binding.Attribute("area")=="bag"&&
                (int?)binding.Attribute("index")==original.BagSlot)!=1)return false;
        if((string)receipt.Attribute("phase")=="returned")return true;
        receipt.SetAttributeValue("phase","returned");
        try{RebirthHumanEquipment.Save(npc,equipment);return true;}catch{return false;}
    }
    // Terminal proof for interruption settlement: the operation-bound receipt
    // reached phase=returned, no bookMove remains, and the exact native book is
    // back in its bag cell. A saved read marker disqualifies it (completion wins).
    internal static bool HasReturnedCancelReceipt(RebirthNpcEmbodiedWorkIntent intent)
    {
        if(intent?.Actor is not EntityRebirthHumanoidNPC npc||
            npc.RebirthRuntimeState?.StableId!=intent.StableId||npc.bag==null||
            HasSavedWorkRead(intent)||
            !RebirthNpcWorkNativeTransferOriginal.TryReadSaved(intent.StableId,
                RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(intent.PrimaryId),out var original)||
            original.Npc!=intent.StableId||original.Phase<4||
            original.BagSlot<0||original.BagSlot>=npc.bag.SlotCount)return false;
        var equipment=RebirthHumanEquipment.Read(npc);
        string operation=original.Operation.ToString("N");
        var receipts=equipment.Elements("literatureCancel").Where(x=>(string)x.Attribute("operation")==operation).ToArray();
        if(receipts.Length!=1||equipment.Element("bookMove")!=null)return false;
        var receipt=receipts[0];
        return (string)receipt.Attribute("phase")=="returned"&&
            (int?)receipt.Attribute("bag")==original.BagSlot&&
            (string)receipt.Attribute("frame")==original.Frame&&
            RebirthHumanEquipment.Frame(npc.bag.ItemGrid[original.BagSlot])==original.Frame;
    }
    // A saved read marker is completion evidence, never cancellation evidence.
    // Reset leaves this owned route intact so the literature adapter can
    // checkpoint its outcome and return the reusable book if still held.
    internal static bool HasSavedWorkRead(RebirthNpcEmbodiedWorkIntent intent)
    {
        if(intent?.Actor is not EntityRebirthHumanoidNPC npc||
            npc.RebirthRuntimeState?.StableId!=intent.StableId||
            !RebirthNpcWorkNativeTransferOriginal.TryReadSaved(intent.StableId,
                RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(intent.PrimaryId),out var original)||
            original.Npc!=intent.StableId||original.Phase<4||
            !RebirthNpcLiteratureWorkAdapter.Book(original,out var knowledge)||
            !RebirthHumanProgression.TryGet(intent.StableId,out var record))return false;
        return record.Progression.KnowledgeIds.Contains(RebirthLiteratureService.RecipeReadMarker(knowledge));
    }

    // Cheap when idle. The shared progression tick calls this from the game thread.
    internal static void Tick()
    {
        var world=GameManager.Instance?.World;
        if(!ReferenceEquals(world,currentWorld)){Active.Clear();Pending.Clear();currentWorld=world;nextRecoveryScan=0f;recoveryCursor=0;}
        if(world==null||world.IsRemote()||!ThreadManager.IsMainThread()||
            RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return;
        foreach(var id in new List<RebirthNpcStableId>(Pending.Keys))
        {
            if(!Pending.TryGetValue(id,out float deadline))continue;
            if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entity)||
                world.GetEntity(entity) is not EntityRebirthHumanoidNPC npc||Time.unscaledTime>deadline)
            {
                Pending.Remove(id);
                if(world.GetEntity(entity) is EntityRebirthHumanoidNPC failed)
                {
                    if(RebirthNpcEmbodiedWorkOwner.TryGetLiteratureIntent(failed,out var intent))
                        TryCancelWorkWithReceipt(intent);
                    else if(!HasWorkBookMove(failed))RebirthHumanEquipment.TryReturnHeldLiterature(failed);
                }
                continue;
            }
            if(npc.Hand.IsBusy)continue;
            if(TryBegin(npc,out _))Pending.Remove(id);
            else
            {
                Pending.Remove(id);
                if(RebirthNpcEmbodiedWorkOwner.TryGetLiteratureIntent(npc,out var intent))
                    TryCancelWorkWithReceipt(intent);
                else if(!HasWorkBookMove(npc))RebirthHumanEquipment.TryReturnHeldLiterature(npc);
            }
        }
        if(Time.unscaledTime>=nextRecoveryScan)
        {
            nextRecoveryScan=Time.unscaledTime+1f;
            var residents=RebirthNpcRuntimeRegistry.GetSnapshot();
            int count=Math.Min(8,residents.Length);
            for(int i=0;i<count;i++)
            {
                var runtime=residents[(recoveryCursor+i)%residents.Length];
                if(Active.ContainsKey(runtime.StableId)||Pending.ContainsKey(runtime.StableId)||
                    !RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId,out int entity)||
                    world.GetEntity(entity) is not EntityRebirthHumanoidNPC npc||npc.IsPreparedRestorationPending)continue;
                if(!HasWorkBookMove(npc))RebirthHumanEquipment.TryReturnHeldLiterature(npc);
            }
            if(residents.Length>0)recoveryCursor=(recoveryCursor+count)%residents.Length;
        }
        if(Active.Count==0)return;
        foreach(var id in new List<RebirthNpcStableId>(Active.Keys))
        {
            if(!Active.TryGetValue(id,out var s))continue;
            try
            {
            if(s.CancelRequested)
            {
                if(TryActor(id,s,out var cancelling))CancelReader(id,cancelling);
                continue;
            }
            if(!TryActor(id,s,out var npc)||!StillHolding(npc,s)||npc.GetAttackTarget()!=null||
                npc.Hand.IsBusy||npc.IsDead()||Health(npc)<s.LastHealth)
            {
                if(npc!=null)CancelReader(id,npc);
                else s.CancelRequested=true;
                continue;
            }
            float now=Time.unscaledTime;
            float delta=Mathf.Clamp(now-s.LastTime,0f,.25f);
            s.LastTime=now;s.LastHealth=Health(npc);
            if(delta<=0f)continue;
            s.Progress=Mathf.Min(1f,s.Progress+delta/s.Seconds);
            s.LastSave+=delta;
            if(s.Progress>=1f)
            {
                if(SaveProgress(id,npc,s,true))
                {Active.Remove(id);RebirthHumanEquipment.TryReturnHeldLiterature(npc);}
                else s.Progress=.999f;
            }
            else if(s.LastSave>=5f&&SaveProgress(id,npc,s,false))s.LastSave=0f;
            }
            catch(Exception error)
            {
                // Retain the session and retry its save; no failed read may grant knowledge.
                Log.Warning("[REBIRTH NPC Reading] retained: "+error.Message);
            }
        }
    }

    private static bool SaveProgress(RebirthNpcStableId id,EntityRebirthHumanoidNPC npc,Session s,bool complete)
    {
        if(complete&&!StillHolding(npc,s)||!RebirthHumanProgression.TryGet(id,out var record))return false;
        string marker=RebirthLiteratureService.RecipeReadMarker(s.KnowledgeId);
        if(complete)
        {
            record.Progression.KnowledgeIds.Add(s.KnowledgeId);
            record.Progression.KnowledgeIds.Add(marker);
            record.Progression.LiteratureStudyProgress.Remove(s.ItemId);
        }
        else
        {
            record.Progression.LiteratureStudyProgress.TryGetValue(s.ItemId,out float previous);
            record.Progression.LiteratureStudyProgress[s.ItemId]=Mathf.Max(previous,Mathf.Min(.999f,s.Progress));
        }
        return RebirthHumanProgression.Save(id,record);
    }

    private static bool TryActor(RebirthNpcStableId id,Session s,out EntityRebirthHumanoidNPC npc)
    {
        npc=null;var world=GameManager.Instance?.World;
        return RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entityId)&&entityId==s.EntityId&&
            (npc=world?.GetEntity(entityId) as EntityRebirthHumanoidNPC)!=null&&Valid(npc);
    }
    private static bool StillHolding(EntityRebirthHumanoidNPC npc,Session s)
    {
        if(!Valid(npc)||npc.inventory.SelectedSlot!=s.ToolbeltSlot)return false;
        var stack=npc.inventory.GetItem(s.ToolbeltSlot);
        return stack!=null&&!stack.IsEmpty()&&stack.itemValue?.ItemClass!=null&&
            stack.itemValue.type==s.ItemType&&stack.itemValue.Seed==s.Seed&&
            string.Equals(stack.itemValue.ItemClass.GetItemName(),s.ItemId,StringComparison.OrdinalIgnoreCase);
    }
    private static bool Valid(EntityRebirthHumanoidNPC npc)
    {
        var world=GameManager.Instance?.World;
        return npc!=null&&npc.world==world&&world!=null&&!world.IsRemote()&&
            ThreadManager.IsMainThread()&&!npc.IsPreparedRestorationPending&&
            !RebirthNpcLiveRecoveryRuntime.IsNativeActivationDeferred(npc)&&
            npc.inventory!=null&&npc.RebirthRuntimeState!=null;
    }
    private static float Health(EntityRebirthHumanoidNPC npc)=>npc.Stats?.Health?.Value??0f;
}
