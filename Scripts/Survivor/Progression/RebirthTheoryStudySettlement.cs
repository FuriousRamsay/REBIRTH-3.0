using System;
using System.Linq;

// Internal completion coordinator. Callers must validate timed lesson/evidence eligibility
// before reserving an outcome; this processor never treats an arbitrary request as a lesson.
internal static class RebirthTheoryStudySettlement
{
    private sealed class StudyCounts{internal long Completed;}
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<RebirthWorldCharacterRecord,StudyCounts> Counts=new System.Runtime.CompilerServices.ConditionalWeakTable<RebirthWorldCharacterRecord,StudyCounts>();
    internal static Newtonsoft.Json.Linq.JObject CaptureAuditState(RebirthWorldCharacterRecord record)
    {
        lock(RebirthSkillKnowledgeService.SyncRoot)
        {
            var outcome=record?.Progression?.PendingTheoryStudy;
            StudyCounts counts=null;if(record!=null)Counts.TryGetValue(record,out counts);
            return new Newtonsoft.Json.Linq.JObject{["creation"]=record?.Origin?.CreationId,["pending"]=outcome!=null,
                ["id"]=outcome?.Id,["receipt"]=outcome?.Receipt,["applied"]=outcome!=null&&record.Progression.SkillAwardReceipts.Contains(outcome.Receipt),["dirty"]=record?.Dirty??false,["completions"]=counts?.Completed??0};
        }
    }
    internal static bool TrySettle(EntityPlayer student,out string reason)
    {
        reason="Study completion is waiting to be saved.";
        if(student==null||student.world==null||student.world.IsRemote()||student.IsDead()||
            !RebirthSkillAwardService.TryGetEligible(student,out var identity,out var record)||record?.Progression==null)return false;
        var world=student.world;string ownerKey=identity.StorageKey;
        lock(RebirthSkillKnowledgeService.SyncRoot)
        {
            var state=record.Progression;var outcome=state.PendingTheoryStudy;
            if(outcome==null||!RebirthTheoryStudyPersistence.MatchesOwner(outcome,record.Origin?.CreationId))return false;
            if(!state.SkillKnowledge.TryGetValue(outcome.SkillId,out var theory)||theory==null||
                float.IsNaN(theory.Value)||float.IsInfinity(theory.Value)||theory.Value<0||theory.Value>100)return false;
            if(outcome.Mode=="solo"&&!state.SkillAwardReceipts.Contains(outcome.Receipt)&&theory.Value!=outcome.Before)return false;
            Func<bool,bool> current=cleared=>ReferenceEquals(student.world,world)&&!world.IsRemote()&&!student.IsDead()&&
                RebirthSkillAwardService.TryGetEligible(student,out var liveIdentity,out var liveRecord)&&ReferenceEquals(liveRecord,record)&&
                liveIdentity!=null&&liveIdentity.StorageKey==ownerKey&&ReferenceEquals(record.Progression,state)&&
                RebirthTheoryStudyPersistence.MatchesOwner(outcome,record.Origin?.CreationId)&&
                (cleared?state.PendingTheoryStudy==null:ReferenceEquals(state.PendingTheoryStudy,outcome));
            if(!current(false)||!RebirthTheorySoloService.ValidateOutcome(state.SoloTheory,outcome,state.SkillAwardReceipts.Contains(outcome.Receipt),false))return false;
            try
            {
                // Persist the completion intent before any Theory/history effect.
                RebirthWorldCharacterRepository.SaveIfDirty(identity,"theory-study-prepare");
                if(!RebirthWorldCharacterRepository.HasSavedTheoryStudy(identity,outcome,state.SkillAwardReceipts.Contains(outcome.Receipt))||!current(false))return false;
                if(!state.SkillAwardReceipts.Contains(outcome.Receipt))
                {
                    if(outcome.Mode=="solo")state.SoloTheory.Session.Consumed=true;
                    theory.Value=Math.Max(theory.Value,outcome.Target);
                    if(!state.TeachingHistory.TryGetValue(outcome.HistoryKey,out var history)||history==null)
                        state.TeachingHistory[outcome.HistoryKey]=history=new RebirthTeachingHistoryRuntimeState
                        {Key=outcome.HistoryKey,StudentStorageKey=identity.StorageKey,SkillId=outcome.SkillId};
                    history.CompletionCount=Math.Max(history.CompletionCount,outcome.HistoryCount);
                    history.LastCompletedUtcTicks=Math.Max(history.LastCompletedUtcTicks,outcome.CompletedTicks);
                    state.SkillAwardReceipts.Add(outcome.Receipt);
                    RebirthTeachingOutcomeStore.PruneAwardReceipts(state.SkillAwardReceipts,outcome.Receipt);
                    record.Touch("theory-study-apply:"+outcome.SkillId);
                }
                RebirthWorldCharacterRepository.SaveIfDirty(identity,"theory-study-apply");
                if(!RebirthWorldCharacterRepository.HasSavedTheoryStudy(identity,outcome,true)||!current(false))return false;
                var originalSolo=outcome.Mode=="solo"?state.SoloTheory.Clone():null;
                if(originalSolo!=null){var solo=state.SoloTheory;var session=solo.Session;solo.Evidence.RemoveAll(e=>e.Subject==session.Subject&&session.Ordinals.Contains(e.Ordinal));solo.LastSettlement[session.Subject]=record.Condition.ActivePlaySeconds;solo.LastSettledId=outcome.Id;solo.Session=null;}
                state.PendingTheoryStudy=null;record.Touch("theory-study-settle:"+outcome.SkillId);
                bool saved=false;
                try{RebirthWorldCharacterRepository.SaveIfDirty(identity,"theory-study-settle");saved=current(true)&&RebirthWorldCharacterRepository.HasSavedTheoryStudy(identity,outcome,true,true)&&current(true);}
                finally{if(!saved&&state.PendingTheoryStudy==null){state.PendingTheoryStudy=outcome;if(originalSolo!=null)state.SoloTheory=originalSolo;record.Touch("theory-study-settle-retry");}}
                if(!saved||!current(true))return false;
                RebirthSkillAwardService.QueueOwnerPublication(student);
                                RebirthTeachingService.NotifyStudy(student,true,string.Format(Localization.Get("xuiRebirthStudyCompleted"),
                    RebirthTeachingService.FriendlySkill(outcome.SkillId),(outcome.Target-outcome.Before).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)));
                Counts.GetOrCreateValue(record).Completed++;reason=string.Empty;return true;
            }
            catch{return false;}
        }
    }
}

// Read-only complete-item observation. A remote server replica is deliberately
// unavailable here; pair the actual native owning endpoint for inventory proof.
public static class RebirthAgentCNativeAudit
{
    public static Newtonsoft.Json.Linq.JObject CaptureInventory(EntityPlayer player)
    {
        var result=new Newtonsoft.Json.Linq.JObject{["available"]=false,["actor"]=player?.entityId??-1};
        if(!ThreadManager.IsMainThread()||!(player is EntityPlayerLocal local)||local.world==null||
            !ReferenceEquals(GameManager.Instance?.World,local.world)||!ReferenceEquals(local.world.GetPrimaryPlayer(),local))return result;
        try
        {
            var image=RebirthCamperEnergyActorImage.Capture(local);
            result["actorFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(RebirthRemoteResourceClientOriginal.ActorImage(image));
            result["fetchHeld"]=RebirthRemoteResourceOwnerAction.IsHeld(local);
            result["fetchMixedCraftBlockingOnly"]=RebirthRemoteResourceOwnerAction.HasBlockingOnlyCraft(local);
            result["deaths"]=local.Died;
            var personal=local.PlayerUI?.xui?.GetCraftingData()?.RecipeQueueItems;
            result["personalQueueReadable"]=personal!=null&&personal.Length<=255;
            if(personal!=null&&personal.Length<=255)
            {
                result["personalQueueCount"]=personal.Count(row=>row?.Recipe!=null);
                result["personalFetchMarkers"]=personal.Count(row=>row?.Recipe?.ingredients?.Any(item=>item?.itemValue?.HasMetadata(RebirthRemoteResourceOwnerAction.Marker)==true)==true);
                result["personalQueueFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(RebirthRemoteResourceClientOriginal.QueueImage(local.PlayerUI.xui.GetCraftingData()));
            }
            result["bagEmptySlots"]=image.Bag.Count(stack=>stack.IsEmpty());
            result["beltEmptySlots"]=image.Belt.Take(local.inventory.Length).Count(stack=>stack.IsEmpty());
            result["bagLocks"]=new Newtonsoft.Json.Linq.JArray(image.BagLocks??new bool[0]);
            if(image.Bag.Length+image.Belt.Length>1024){result["reason"]="Native slot observation limit";return result;}
            var counts=new Newtonsoft.Json.Linq.JObject();
            Action<ItemStack> count=stack=>
            {
                if(stack?.itemValue?.ItemClass==null||stack.count<=0)return;
                string name=stack.itemValue.ItemClass.GetItemName();
                if(string.IsNullOrEmpty(name))return;
                counts[name]=((long?)counts[name]??0L)+stack.count;
            };
            foreach(var stack in image.Bag)count(stack);
            foreach(var stack in image.Belt)count(stack);
            count(image.Cursor);result["itemsByName"]=counts;
            using(var stream=new System.IO.MemoryStream())using(var writer=new System.IO.BinaryWriter(stream))
            {
                image.Write(writer);writer.Write(local.inventory.Length);writer.Flush();
                using(var hash=System.Security.Cryptography.SHA256.Create())result["fingerprint"]=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-",string.Empty);
            }
            result["available"]=true;result["bagSlots"]=image.Bag.Length;result["beltSlots"]=image.Belt.Length;result["cursorCount"]=image.Cursor.count;
        }
        catch(Exception error){result["reason"]=error.GetType().Name;}
        return result;
    }
}
