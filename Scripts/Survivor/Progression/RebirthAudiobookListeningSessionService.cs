using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

/// <summary>
/// Server-authoritative audiobook listening.
///
/// Audio reuses the exact same per-character progress key as the paired physical literature.
/// Unlike physical reading, listening is deliberately untethered: walking, running, jumping,
/// climbing, vehicles, combat, looting, crafting and other ordinary activity do not slow or cancel
/// audiobook progress.
///
/// Playback requires the exact cassette in the player's bag/toolbelt and the combined
/// Walkman/headphones equipped. Losing either pauses the session and preserves shared progress.
/// </summary>
public static class RebirthAudiobookListeningSessionService
{
    public const string WalkmanItemId="FuriousRamsayWalkman";
    public const string HeadphonesItemId="FuriousRamsayHeadphones";

    private sealed class Session
    {
        public int EntityId;
        public RebirthStudySessionSaveBinding SaveBinding;
        public string CreationId;
        public int CassetteItemType;
        public ushort CassetteSeed;
        public string StoredSlotId=string.Empty;
        public string StoredItemData=string.Empty;
        public string AudiobookItemId=string.Empty;
        public string SourceLiteratureId=string.Empty;
        public float Progress;
        public float EffectiveSeconds;
        public float SaveAccumulator;
        public float HudSyncAccumulator;
        public bool Paused;
        public bool CompletedAwaitingSave;
        public bool CancelAwaitingSave;
        public string CompletionMessage=string.Empty;
    }

    private static readonly Dictionary<int,Session> Active=new Dictionary<int,Session>();
    // GameUpdate runs on the game thread. Snapshot IDs because completion/cancellation removes sessions.
    private static int[] updateIds=new int[4];
    private static bool installed;

    public static string Install()
    {
        if(installed)return "[REBIRTH Audiobook] listening sessions already installed.";
        installed=true;
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        return "[REBIRTH Audiobook] listening sessions installed.";
    }

    public static bool TryPause(EntityPlayer player,out string message)
    {
        message=Localization.Get("xuiRebirthAudiobookUnavailable");Session session;RebirthWorldCharacterRecord record;
        if(player==null||!RebirthWorldCharacterRepository.IsServerAuthority||
            !Active.TryGetValue(player.entityId,out session)||session==null||session.CompletedAwaitingSave||session.CancelAwaitingSave||
            !RebirthWorldCharacterService.TryGet(player,out record)||
            !RebirthSurvivorRequestScope.Matches(session.CreationId,record?.Origin?.CreationId))return false;
        if(!Persist(player,record,session))return false;
        session.Paused=true;RebirthStudyHudNetworkService.SendClear(player);
        message=Localization.Get("xuiRebirthAudiobookPaused");return true;
    }
    public static bool TryResume(EntityPlayer player,out string message)
    {
        message=Localization.Get("xuiRebirthAudiobookUnavailable");Session session;RebirthWorldCharacterRecord record;
        if(player==null||player.IsDead()||!RebirthWorldCharacterRepository.IsServerAuthority||
            !RebirthWorldCharacterService.TryGet(player,out record)||record==null||!record.IsComplete)return false;
        if(Active.TryGetValue(player.entityId,out session)&&session!=null)
        {
            if(session.CompletedAwaitingSave||session.CancelAwaitingSave||!HasSessionCassette(player,record,session)||
                !RebirthSurvivorGearService.HasEquippedWalkman(player))return false;
            session.Paused=false;
            RebirthStudyHudNetworkService.SendAudiobook(player,session.SourceLiteratureId,session.Progress,
                Mathf.Max(0f,session.EffectiveSeconds*(1f-session.Progress)));
            message=Localization.Get("xuiRebirthAudioResume");return true;
        }
        return TryBeginNextPending(player,out message);
    }

    public static bool TryBeginNextPending(EntityPlayer player,out string message)
    {
        message=string.Empty;RebirthWorldCharacterRecord record;
        if(player==null||player.IsDead()||!RebirthWorldCharacterRepository.IsServerAuthority||
            !RebirthWorldCharacterService.TryGet(player,out record)||record?.Support==null||!record.IsComplete)return false;
        Session active;
        if(Active.TryGetValue(player.entityId,out active)&&active!=null&&(active.CompletedAwaitingSave||active.CancelAwaitingSave))return false;
        if(Active.TryGetValue(player.entityId,out active)&&active!=null&&HasSessionCassette(player,record,active))
        {
            RebirthLiteratureDefinition current;
            if(RebirthProgressionRuntimeConfig.TryGetLiterature(active.SourceLiteratureId,out current)&&current!=null&&
                !RebirthLiteratureService.IsAlreadyCompleted(player,current))return TryResume(player,out message);
        }
        foreach(var entry in record.Support.AudiobookCassettes)
        {
            RebirthAudiobookDefinition audio;RebirthLiteratureDefinition source;
            if(entry==null||!RebirthProgressionRuntimeConfig.TryGetAudiobook(entry.ItemId,out audio)||audio==null||
                !RebirthProgressionRuntimeConfig.TryGetLiterature(audio.SourceLiteratureId,out source)||source==null||
                RebirthLiteratureService.IsAlreadyCompleted(player,source))continue;
            return TryBeginStored(player,entry.SlotId,out message);
        }
        return false;
    }

    public static bool TryBeginStored(EntityPlayer player,string slotId,out string message)
    {
        ItemStack cassette;string encoded;
        if(!TryFindStored(player,slotId,out cassette,out encoded))
        {message=Localization.Get("xuiRebirthAudiobookCassetteMissing");return false;}
        return TryBeginCore(player,cassette.itemValue.type,cassette.itemValue.Seed,slotId,out message);
    }
    public static bool TryBegin(EntityPlayer player,int itemType,ushort seed,out string message)
    {return TryBeginCore(player,itemType,seed,null,out message);}

    private static bool TryBeginCore(EntityPlayer player,int itemType,ushort seed,string storedSlotId,out string message)
    {
        message=string.Empty;
        if(player==null||!RebirthWorldCharacterRepository.IsServerAuthority||!RebirthSurvivorMode.IsEnabledForCurrentWorld())
        {message=Localization.Get("xuiRebirthAudiobookUnavailable");return false;}
        if(RebirthCharacterCreationHoldService.IsHeld(player))
        {message=Localization.Get("xuiRebirthAudiobookFinishCreation");return false;}

        if(RebirthBackpackLibraryReservation.BlocksResourceUse(player))
        {message=Localization.Get("xuiRebirthLibraryTransferPending");return false;}

        ItemStack cassette;
        string storedItemData=string.Empty;
        bool found=string.IsNullOrEmpty(storedSlotId)
            ? RebirthLiteratureService.TryFindMatchingInventoryStackPublic(player,itemType,seed,out cassette)
            : TryFindStored(player,storedSlotId,out cassette,out storedItemData);
        if(!found||cassette==null||cassette.IsEmpty()||cassette.itemValue==null||cassette.itemValue.ItemClass==null)
        {message=Localization.Get("xuiRebirthAudiobookCassetteMissing");return false;}

        string audiobookItemId=cassette.itemValue.ItemClass.GetItemName();
        RebirthAudiobookDefinition audio;
        if(!RebirthProgressionRuntimeConfig.TryGetAudiobook(audiobookItemId,out audio)||audio==null)
        {message=Localization.Get("xuiRebirthAudiobookUnknownCassette");return false;}

        if(!RebirthSurvivorGearService.HasEquippedWalkman(player))
        {message=Localization.Get("xuiRebirthAudiobookNeedWalkman");return false;}

        RebirthLiteratureDefinition source;
        if(!RebirthProgressionRuntimeConfig.TryGetLiterature(audio.SourceLiteratureId,out source)||source==null)
        {message=Localization.Get("xuiRebirthAudiobookMaterialMissing");return false;}

        if(RebirthLiteratureService.IsAlreadyCompleted(player,source))
        {
            message=RebirthLiteratureService.GetAlreadyCompletedMessage(player,source);
            return true;
        }

        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record)||record==null||record.Progression==null||!record.IsComplete||!RebirthSurvivorRequestScope.Matches(record.Origin?.CreationId,record.Origin?.CreationId))
        {message=Localization.Get("xuiRebirthAudiobookCharacterMissing");return false;}

        // Finalize physical reading before sampling the shared progress key. This makes same-title
        // media handoff monotonic and same-mode restart idempotent.
        // A same-medium restart/title change also replaces an active session.
        // Preserve its latest fraction before loading this title's checkpoint.
        if(!RebirthStudySessionSaveBinding.TryCapture(player,record,audio.SourceLiteratureId,out var saveBinding))
        {message=Localization.Get("xuiRebirthAudiobookSavePending");return false;}
        Session outgoing;
        if(Active.TryGetValue(player.entityId,out outgoing) && outgoing!=null)
        {
            if(outgoing.CompletedAwaitingSave)
            {message=Localization.Get("xuiRebirthAudiobookSavePending");return false;}
            if(!Persist(player,record,outgoing))
            {message=Localization.Get("xuiRebirthAudiobookSavePending");return false;}
        }
        if(RebirthTheorySpecialistLessonService.IsActive(player)){message=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryFinishCurrent","Finish your current study session first.");return false;}
        RebirthTheorySoloService.PauseForOtherStudy(player);
        if(!RebirthLiteratureStudySessionService.StopForStudyModeSwitch(player))
        {message=Localization.Get("xuiRebirthAudiobookSavePending");return false;}
        float progress=0f;
        record.Progression.LiteratureStudyProgress.TryGetValue(audio.SourceLiteratureId,out progress);
        progress=Mathf.Clamp01(progress);

        float globalMultiplier=RebirthSandboxOptionManager.Current.LiteratureStudyTimeMultiplier;
        float listenerMultiplier=RebirthTraitGameplayModifierService.GetAudioLiteratureStudyTimeMultiplier(player);
        float teacherMultiplier=RebirthTeachingService.GetSoloStudyTimeMultiplier(player);
        float effective=Mathf.Max(2f,audio.AudioSeconds*globalMultiplier*listenerMultiplier*teacherMultiplier);

        Active[player.entityId]=new Session
        {
            EntityId=player.entityId,CreationId=record.Origin.CreationId,SaveBinding=saveBinding,
            CassetteItemType=itemType,
            CassetteSeed=seed,
            StoredSlotId=storedSlotId??string.Empty,StoredItemData=storedItemData,
            AudiobookItemId=audiobookItemId,
            SourceLiteratureId=audio.SourceLiteratureId,
            Progress=progress,
            EffectiveSeconds=effective
        };

        int pct=Mathf.RoundToInt(progress*100f);
        float remaining=Mathf.Max(0f,effective*(1f-progress));
        RebirthStudyHudNetworkService.SendAudiobook(player,audio.SourceLiteratureId,progress,remaining);
        message=string.Format(Localization.Get(pct>0?"xuiRebirthAudiobookResumed":"xuiRebirthAudiobookStarted"),pct,FormatSeconds(remaining));
        { if (RebirthLogSettings.LiteratureLoggingEnabled) RebirthLogSettings.TraceLiterature("audiobook begin item="+audiobookItemId+" source="+audio.SourceLiteratureId+" entity="+player.entityId+" progress="+progress.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" effectiveSeconds="+effective.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)); }
        return true;
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        if(Active.Count==0||GameManager.Instance==null||GameManager.Instance.World==null)return;
        World world=GameManager.Instance.World;
        if(world.IsRemote())return;

        float dt=RebirthStudyClock.ElapsedThisFrame();
        if(dt<=0f)return;
        int count=Active.Count;
        if(updateIds.Length<count)Array.Resize(ref updateIds,Math.Max(count,updateIds.Length*2));
        Active.Keys.CopyTo(updateIds,0);
        for(int i=0;i<count;i++)
        {
            Session s;
            if(!Active.TryGetValue(updateIds[i],out s)||s==null)continue;
            EntityPlayer player=world.GetEntity(s.EntityId) as EntityPlayer;
            if(player==null)
            {
                s.CancelAwaitingSave=!s.CompletedAwaitingSave;
                s.SaveAccumulator+=dt;
                if(s.SaveAccumulator>=1f)
                {s.SaveAccumulator=0f;if(s.SaveBinding!=null&&s.SaveBinding.TrySave(s.Progress,s.CompletedAwaitingSave))Active.Remove(s.EntityId);}
                continue;
            }
            if(player.IsDead()&&!s.CompletedAwaitingSave&&!s.CancelAwaitingSave){Cancel(player,s,Localization.Get("xuiRebirthAudiobookStopped"),true);continue;}

            RebirthWorldCharacterRecord record;
            if(!RebirthWorldCharacterService.TryGet(player,out record)||record==null||record.Progression==null||!record.IsComplete||!RebirthSurvivorRequestScope.Matches(s.CreationId,record.Origin?.CreationId))
            {Cancel(player,s,Localization.Get("xuiRebirthAudiobookCharacterStopped"),false);continue;}

            if(RetryCancelledSave(player,record,s,dt))continue;
            if(s.CompletedAwaitingSave)
            {
                s.SaveAccumulator+=dt;
                if(s.SaveAccumulator>=1f)
                {s.SaveAccumulator=0f;if(SaveDirty(player,"audiobook-completion-retry"))FinishCompleted(player,s);}
                continue;
            }
            if(!HasSessionCassette(player,record,s))
            {Cancel(player,s,Localization.Get("xuiRebirthAudiobookCassetteStopped"),true);continue;}
            // Use the live record validated immediately above; avoid a second identity
            // and repository lookup for every listener on every server update.
            if(!RebirthSurvivorGearService.IsEquipped(record,RebirthSurvivorGearService.WalkmanSlotId,RebirthSurvivorGearService.WalkmanGearItemId))
            {Cancel(player,s,Localization.Get("xuiRebirthAudiobookWalkmanStopped"),true);continue;}

            if(s.Paused)continue;
            if(s.EffectiveSeconds<=0.001f)s.EffectiveSeconds=2f;
            s.Progress=Mathf.Clamp01(s.Progress+dt/s.EffectiveSeconds);
            s.SaveAccumulator+=dt;
            s.HudSyncAccumulator+=dt;

            if(s.HudSyncAccumulator>=RebirthStudyHudNetworkService.SnapshotIntervalSeconds)
            {
                float hudRemaining=Mathf.Max(0f,s.EffectiveSeconds*(1f-s.Progress));
                RebirthStudyHudNetworkService.SendAudiobook(player,s.SourceLiteratureId,s.Progress,hudRemaining);
                s.HudSyncAccumulator=0f;
            }

            if(s.Progress>=0.9999f)
            {
                string sourceId; string result;
                if(RebirthLiteratureService.TryCompleteAudiobook(player,s.AudiobookItemId,out sourceId,out result))
                {
                    record.Progression.LiteratureStudyProgress.Remove(s.SourceLiteratureId);
                    record.Touch("audiobook-study-complete:"+s.SourceLiteratureId);
                    s.CompletedAwaitingSave=true;s.CompletionMessage=result;s.SaveAccumulator=0f;
                    if(SaveDirty(player,"audiobook-study-complete:"+s.SourceLiteratureId))FinishCompleted(player,s);
                }
                else Cancel(player,s,result,true);
                continue;
            }

            if(s.SaveAccumulator>=5f)
            {
                Persist(player,record,s);
                s.SaveAccumulator=0f;
            }
        }
    }

    private static bool TryFindStored(EntityPlayer player,string slotId,out ItemStack cassette,out string encoded)
    {
        cassette=null;encoded=string.Empty;Guid parsed;
        RebirthWorldCharacterRecord record;
        if(player==null||!RebirthWorldCharacterRepository.IsServerAuthority||
            !Guid.TryParseExact(slotId,"N",out parsed)||parsed==Guid.Empty||
            !RebirthWorldCharacterService.TryGet(player,out record)||record?.Support==null||!record.IsComplete)return false;
        var pending=record.Support.PendingMusicTransfer;
        if(pending!=null&&pending.IsAudiobook&&pending.Operation==2&&pending.AudiobookSlotId==slotId)return false;
        foreach(var entry in record.Support.AudiobookCassettes)
        {
            if(entry==null||entry.SlotId!=slotId)continue;
            ItemValue value;
            if(!RebirthNativeItemCodec.TryDecode(entry.ItemData,out value)||value.ItemClass.GetItemName()!=entry.ItemId)return false;
            cassette=new ItemStack(value,1);encoded=entry.ItemData;return true;
        }
        return false;
    }
    private static bool HasSessionCassette(EntityPlayer player,RebirthWorldCharacterRecord record,Session session)
    {
        if(player==null||session==null||record==null||!record.IsComplete||
            !RebirthSurvivorRequestScope.Matches(session.CreationId,record.Origin?.CreationId))return false;
        if(string.IsNullOrEmpty(session.StoredSlotId))
            return RebirthLiteratureService.HasMatchingInventoryItem(player,session.CassetteItemType,session.CassetteSeed);
        if(record?.Support==null||!RebirthSurvivorRequestScope.Matches(session.CreationId,record.Origin?.CreationId))return false;
        var pending=record.Support.PendingMusicTransfer;
        if(pending!=null&&pending.IsAudiobook&&pending.Operation==2&&pending.AudiobookSlotId==session.StoredSlotId)return false;
        foreach(var entry in record.Support.AudiobookCassettes)
            if(entry!=null&&entry.SlotId==session.StoredSlotId)
                return entry.ItemId==session.AudiobookItemId&&entry.ItemData==session.StoredItemData;
        return false;
    }

    private static bool HasNamedInventoryItem(EntityPlayer player,string itemId)
    {
        if(player==null||string.IsNullOrEmpty(itemId))return false;
        if(HasNamed(player.bag!=null?player.bag.ItemGrid.items:null,int.MaxValue,itemId))return true;
        var belt=player.inventory!=null?player.inventory.ItemGrid.items:null;
        return HasNamed(belt,RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt!=null?belt.Length:0),itemId);
    }

    private static bool HasNamed(ItemStack[] slots,int limit,string itemId)
    {
        if(slots==null)return false;
        for(int i=0;i<Math.Min(limit,slots.Length);i++)
        {
            ItemStack stack=slots[i];
            if(stack==null||stack.IsEmpty()||stack.itemValue==null||stack.itemValue.ItemClass==null)continue;
            if(string.Equals(stack.itemValue.ItemClass.GetItemName(),itemId,StringComparison.OrdinalIgnoreCase))return true;
        }
        return false;
    }

    private static bool Persist(EntityPlayer player,RebirthWorldCharacterRecord record,Session s)
    {
        if(player==null||record==null||record.Progression==null||s==null
            ||!record.IsComplete||!RebirthSurvivorRequestScope.Matches(s.CreationId,record.Origin?.CreationId))return false;
        if(s.CompletedAwaitingSave)return SaveDirty(player,"audiobook-completion-save");
        record.Progression.LiteratureStudyProgress[s.SourceLiteratureId]=Mathf.Clamp(s.Progress,0.001f,0.999f);
        record.Touch("audiobook-study-progress:"+s.SourceLiteratureId);
        return SaveDirty(player,"audiobook-study-progress:"+s.SourceLiteratureId);
    }

    private static bool SaveDirty(EntityPlayer player,string reason)
    {
        RebirthStablePlayerIdentity identity;
        if(player==null||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out identity)||identity==null)return false;
        try { return RebirthWorldCharacterRepository.SaveIfDirty(identity,reason); }
        catch(Exception){return false;}
    }
    private static void FinishCompleted(EntityPlayer player,Session session)
    {
        Active.Remove(session.EntityId);
        RebirthStudyHudNetworkService.SendClear(player);
        Send(player,true,session.CompletionMessage);
        if(!string.IsNullOrEmpty(session.StoredSlotId))
        {string message;TryBeginNextPending(player,out message);}
    }
    private static bool RetryCancelledSave(EntityPlayer player,RebirthWorldCharacterRecord record,Session s,float dt)
    {
        if(!s.CancelAwaitingSave)return false;
        s.SaveAccumulator+=dt;
        if(s.SaveAccumulator>=1f)
        {s.SaveAccumulator=0f;if(Persist(player,record,s))Active.Remove(s.EntityId);}
        return true;
    }
    private static void Cancel(EntityPlayer player,Session s,string message,bool save)
    {
        if(s==null)return;
        if(save&&player!=null)
        {
            RebirthWorldCharacterRecord record;
            if(RebirthWorldCharacterService.TryGet(player,out record)&&record!=null&&record.IsComplete&&
                RebirthSurvivorRequestScope.Matches(s.CreationId,record.Origin?.CreationId)&&!Persist(player,record,s))
            {
                bool first=!s.CancelAwaitingSave;s.CancelAwaitingSave=true;s.Paused=true;s.SaveAccumulator=0f;
                if(first){RebirthStudyHudNetworkService.SendClear(player);Send(player,false,message);}
                return;
            }
        }
        Active.Remove(s.EntityId);
        if(player!=null)RebirthStudyHudNetworkService.SendClear(player);
        if(player!=null)Send(player,false,message);
        { if (RebirthLogSettings.LiteratureLoggingEnabled) RebirthLogSettings.TraceLiterature("audiobook cancel item="+s.AudiobookItemId+" entity="+s.EntityId+" progress="+s.Progress.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" reason="+(message??string.Empty)); }
    }

    private static void Send(EntityPlayer player,bool ok,string message)
    {
        if(player==null)return;
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(c!=null&&c.IsServer)c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionResult>().Setup(ok,message),_attachedToEntityId:player.entityId);
    }

    public static bool TryGetUiState(EntityPlayer player,out string audiobookItemId,out string sourceLiteratureId,out float progress,out float remainingSeconds)
    {
        audiobookItemId=string.Empty;sourceLiteratureId=string.Empty;progress=0f;remainingSeconds=0f;
        if(player==null)return false;
        Session s;
        if(!Active.TryGetValue(player.entityId,out s)||s==null||s.Paused||s.CancelAwaitingSave)return false;
        audiobookItemId=s.AudiobookItemId??string.Empty;
        sourceLiteratureId=s.SourceLiteratureId??string.Empty;
        progress=Mathf.Clamp01(s.Progress);
        remainingSeconds=Mathf.Max(0f,s.EffectiveSeconds*(1f-progress));
        return true;
    }

    public static bool StopForStudyModeSwitch(EntityPlayer player)
    {
        if(player==null)return false;
        Session s;
        if(!Active.TryGetValue(player.entityId,out s)||s==null)return true;
        if(s.CompletedAwaitingSave)return false;
        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record)||!Persist(player,record,s))return false;
        // A successful checkpoint permits removal; never perform a second uncertain save.
        Cancel(player,s,Localization.Get("xuiRebirthAudiobookPaused"),false);
        return true;
    }

    public static string BuildDebugSummary(EntityPlayer player)
    {
        if(player==null)return "[REBIRTH Audiobook] player unavailable";
        bool equippedWalkman=RebirthSurvivorGearService.HasEquippedWalkman(player);
        Session s;
        if(!Active.TryGetValue(player.entityId,out s)||s==null)
            return "[REBIRTH Audiobook] entity="+player.entityId+" listeningActive=false equippedWalkman="+equippedWalkman;
        float pct=Mathf.Clamp01(s.Progress)*100f;
        RebirthWorldCharacterRecord record;
        bool cassette=RebirthWorldCharacterService.TryGet(player,out record)&&HasSessionCassette(player,record,s);
        return "[REBIRTH Audiobook] entity="+player.entityId
            +" listeningActive=true cassette="+s.AudiobookItemId
            +" source="+s.SourceLiteratureId
            +" progress="+pct.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"%"
            +" effectiveSeconds="+s.EffectiveSeconds.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
            +" cassettePresent="+cassette
            +" equippedWalkman="+equippedWalkman;
    }

    public static bool TryGetDebugProgress(EntityPlayer player,out string audiobookItemId,out string sourceLiteratureId,out float progress,out float effectiveSeconds)
    {
        audiobookItemId=string.Empty;sourceLiteratureId=string.Empty;progress=0f;effectiveSeconds=0f;
        if(player==null)return false;
        Session s;
        if(!Active.TryGetValue(player.entityId,out s)||s==null)return false;
        audiobookItemId=s.AudiobookItemId??string.Empty;
        sourceLiteratureId=s.SourceLiteratureId??string.Empty;
        progress=Mathf.Clamp01(s.Progress);
        effectiveSeconds=s.EffectiveSeconds;
        return true;
    }

    // Called by the persistence lifecycle BEFORE its character cache reset. Event
    // registration order must not discard the latest in-memory study fraction.
    internal static int CheckpointForShutdown()
    {
        int failed=0;
        var sessions=new Session[Active.Count];Active.Values.CopyTo(sessions,0);
        foreach(var session in sessions)
            if(session!=null&&(session.SaveBinding==null||!session.SaveBinding.TrySave(session.Progress,session.CompletedAwaitingSave)))failed++;
        return failed;
    }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data){Active.Clear();}
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data){Active.Clear();}

    private static string FormatSeconds(float value)
    {
        int seconds=Math.Max(0,Mathf.CeilToInt(value));
        if(seconds<60)return string.Format(Localization.Get("xuiRebirthAudiobookSeconds"),seconds);
        return string.Format(Localization.Get("xuiRebirthAudiobookMinutesSeconds"),seconds/60,seconds%60);
    }
}
