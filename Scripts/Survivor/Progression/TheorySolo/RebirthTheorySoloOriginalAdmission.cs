using System;
using System.Collections.Generic;
using UnityEngine;

// Original server preparation driven by an authenticated native post-save upload.
// No native craft suppression, enqueue mutation or outcome credit occurs here.
internal static class RebirthTheorySoloOriginalAdmission
{
    private sealed class Pending
    {
        internal World World;internal ClientInfo Sender;internal EntityPlayer Player;internal Guid Request,WorldId;
        internal string Creation,Recipe,Before;internal int Tier,Count;internal float Expires;
        internal Action<RebirthTheorySoloOriginalTaskLedger.Task> Reply;
    }
    private static World activeWorld;
    private static readonly Dictionary<ClientInfo,Pending> Waiting=new Dictionary<ClientInfo,Pending>();
    private static readonly Dictionary<ClientInfo,float> LastRequest=new Dictionary<ClientInfo,float>();
    internal static bool Begin(World world,ClientInfo sender,EntityPlayer player,Guid worldId,string creation,Guid request,string recipe,string before,int tier,int count,Action<RebirthTheorySoloOriginalTaskLedger.Task> reply)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||!Guid.TryParse(world.Guid,out var currentWorld)||currentWorld!=worldId||worldId==Guid.Empty||sender==null||player==null||request==Guid.Empty||reply==null||tier<0||tier>255||count<1||count>10000||string.IsNullOrEmpty(recipe)||recipe.Length>128||before==null||before.Length!=64)return false;
        foreach(char c in before)if(!(c>='0'&&c<='9'||c>='a'&&c<='f'))return false;
        if(!ReferenceEquals(activeWorld,world)){Waiting.Clear();LastRequest.Clear();activeWorld=world;}
        float now=Time.realtimeSinceStartup;if(float.IsNaN(now)||float.IsInfinity(now))return false;
        if(LastRequest.TryGetValue(sender,out var last)&&now>=last&&now-last<.25f)return false;
        if(LastRequest.Count>=128&&!LastRequest.ContainsKey(sender)){var expired=new List<ClientInfo>();foreach(var pair in LastRequest)if(now-pair.Value>30f)expired.Add(pair.Key);foreach(var key in expired)LastRequest.Remove(key);if(LastRequest.Count>=128)return false;}
        LastRequest[sender]=now;
        if(Waiting.TryGetValue(sender,out var previous)&&now<=previous.Expires)
        {
            if(previous.Request!=request||previous.WorldId!=worldId||previous.Creation!=creation||previous.Recipe!=recipe||previous.Before!=before||previous.Tier!=tier||previous.Count!=count||!ReferenceEquals(previous.Player,player))return false;
            return RebirthTheorySoloNativeUploadReceipt.Arm(sender,player,creation,request);
        }
        if(Waiting.Count>=128&&!Waiting.ContainsKey(sender))
        {
            var expired=new List<ClientInfo>();foreach(var pair in Waiting)if(now>pair.Value.Expires)expired.Add(pair.Key);foreach(var key in expired)Waiting.Remove(key);if(Waiting.Count>=128)return false;
        }
        if(!RebirthTheorySoloNativeUploadReceipt.Arm(sender,player,creation,request))return false;
        Waiting[sender]=new Pending{World=world,Sender=sender,Player=player,WorldId=worldId,Creation=creation,Request=request,Recipe=recipe,Before=before,Tier=tier,Count=count,Expires=now+10f,Reply=reply};return true;
    }
    internal static void Observe(RebirthTheorySoloNativeUploadReceipt.Receipt receipt,PlayerDataFile native)
    {
        if(receipt==null||native==null||!RebirthTheorySoloNativeUploadReceipt.MatchesNative(receipt,native)||!Waiting.TryGetValue(receipt.Sender,out var pending)||receipt.ClaimedRequest!=pending.Request||receipt.InventoryHash!=pending.Before||receipt.Creation!=pending.Creation||!ReferenceEquals(receipt.Player,pending.Player)||!ReferenceEquals(receipt.World,pending.World))return;
        RebirthTheorySoloOriginalTaskLedger.Task task=null;
        try{TryPublish(pending,receipt,native,out task);}catch{task=null;}
        // Only the exact native observation finishes this original request. No retry fabricates a new ordinal.
        if(Waiting.TryGetValue(pending.Sender,out var original)&&ReferenceEquals(original,pending)){Waiting.Remove(pending.Sender);if(RebirthTheorySoloNativeUploadReceipt.Current(receipt))pending.Reply(task);}
    }
    private static bool TryPublish(Pending pending,RebirthTheorySoloNativeUploadReceipt.Receipt receipt,PlayerDataFile native,out RebirthTheorySoloOriginalTaskLedger.Task task)
    {
        task=null;
        if(!Current(pending,receipt)||!RebirthSkillAwardService.TryGetEligible(pending.Player,out var owner,out var record)||record?.Progression==null||record.Origin?.CreationId!=pending.Creation||owner.CanonicalId!=receipt.Owner||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(record))return false;
        Recipe canonical=CraftingManager.GetRecipe(pending.Recipe);
        if(canonical==null||canonical.GetName()!=pending.Recipe||!RebirthCapabilityService.EvaluateRecipe(pending.Player,pending.Recipe).IsAllowed||!RebirthTheorySoloNativeRecipeProjection.TryCreate(pending.Player,canonical,pending.Tier,pending.Count,RebirthPlayerDataInventory.ReadSlots(native,true),RebirthPlayerDataInventory.ReadSlots(native,false),out var projected,out var payment,out var training)||payment.BeforeHash!=pending.Before||!RebirthTheorySoloRegistry.TryGet(training.SkillId,out var rule)||(rule.Family!="recipe_process"&&rule.Family!="recipe_repair")||!RebirthTheorySoloTaskMarker.TryRecipeBinding(projected,out var binding))return false;
        lock(RebirthSkillKnowledgeService.SyncRoot)
        {
            var progression=record.Progression;
            Func<bool> current=()=>Current(pending,receipt)&&RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)&&ReferenceEquals(record.Progression,progression)&&record.Origin?.CreationId==pending.Creation;
            if(!current())return false;
            if(progression.SoloTheory==null)progression.SoloTheory=new RebirthTheorySoloState{CreationId=pending.Creation};
            var solo=progression.SoloTheory;if(solo.CreationId!=pending.Creation)return false;
            if(solo.OriginalTasks==null)solo.OriginalTasks=new RebirthTheorySoloOriginalTaskLedger();
            var ledger=solo.OriginalTasks;
            var prepared=ledger.Prepare(pending.Request.ToString("N"),pending.Player.entityId,"personal",binding,payment.BeforeHash,payment.AfterHash,pending.Creation,training.SkillId,pending.Count,training.Difficulty);
            if(prepared==null||prepared.Held||prepared.Committed||prepared.Completed!=0)return false;
            if(!prepared.Published){if(!ledger.Publish(prepared))return false;record.Touch("solo-original-task-publish");}
            if(!ledger.TryGet(prepared.Ordinal,prepared.Creation,prepared.Lease,out prepared))return false;
            // Save return values do not prove publication; exact original final-file witness does.
            RebirthWorldCharacterRepository.SaveIfDirty(owner,"solo-original-task-publish");
            if(!current()||!RebirthWorldCharacterRepository.HasSavedSoloOriginalTasks(owner,solo)||!current())return false;
            task=prepared;return true;
        }
    }
    private static bool Current(Pending pending,RebirthTheorySoloNativeUploadReceipt.Receipt receipt)
    {
        float now=Time.realtimeSinceStartup;
        return pending!=null&&receipt!=null&&!float.IsNaN(now)&&!float.IsInfinity(now)&&now<=pending.Expires&&RebirthTheorySoloNativeUploadReceipt.Current(receipt)&&ReferenceEquals(GameManager.Instance?.World,pending.World)&&Guid.TryParse(pending.World.Guid,out var id)&&id==pending.WorldId&&!pending.Player.IsDead()&&!RebirthCharacterCreationHoldService.IsHeld(pending.Player)&&!RebirthBackpackLibraryReservation.BlocksResourceUse(pending.Player);
    }
    internal static void Reset(){Waiting.Clear();LastRequest.Clear();activeWorld=null;}
}