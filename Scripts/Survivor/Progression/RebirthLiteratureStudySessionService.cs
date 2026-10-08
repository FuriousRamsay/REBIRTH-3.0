using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

/// <summary>
/// Server-authoritative physical-reading activity.
///
/// Full study speed is reserved for actually reading while idle, walking, or simply looking around.
/// Other allowed activity slows study progress to 50% rather than cancelling it. This includes
/// running, ordinary jumping, swimming, and client-reported inventory/container/workstation/menu interaction.
///
/// The exact literature item must remain held. Switching to a weapon/tool therefore cancels before
/// attack/harvest work can advance reading. Climbing cancels. The third distinct jump start inside
/// five seconds cancels and preserves partial progress.
/// </summary>
public static class RebirthLiteratureStudySessionService
{
    private sealed class Session
    {
        public int EntityId;
        public RebirthStudySessionSaveBinding SaveBinding;
        public string CreationId;
        public int ItemType;
        public ushort Seed;
        public string ItemId=string.Empty;
        public Vector3 LastPosition;
        public float LastHealth;
        public bool WasJumping;
        public float Progress;
        public float EffectiveSeconds;
        public float JumpWindowRemaining;
        public int JumpCount;
        public float SaveAccumulator;
        public float HudSyncAccumulator;
        public bool ClientSlowActivity;
        public float ClientActivityHintRemaining;
        public bool LastSlowState;
        public bool CompletedAwaitingSave;
        public bool CancelAwaitingSave;
        public string CompletionMessage=string.Empty;
    }

    private static readonly Dictionary<int,Session> Active=new Dictionary<int,Session>();
    // Snapshot IDs on the game thread: completion and cancellation can remove sessions.
    private static int[] updateIds=new int[4];
    private static readonly FastTags<TagGroup.Global> TagWalking=FastTags<TagGroup.Global>.Parse("walking");
    private static readonly FastTags<TagGroup.Global> TagRunning=FastTags<TagGroup.Global>.Parse("running");
    private static readonly FastTags<TagGroup.Global> TagSwimming=FastTags<TagGroup.Global>.Parse("swimming");
    private static readonly FastTags<TagGroup.Global> TagSwimmingRun=FastTags<TagGroup.Global>.Parse("swimmingRun");
    private static readonly FastTags<TagGroup.Global> TagClimbing=FastTags<TagGroup.Global>.Parse("climbing");
    private static readonly FastTags<TagGroup.Global> TagJumping=FastTags<TagGroup.Global>.Parse("jumping");

    private static bool installed;

    private const float JumpWindowSeconds=5f;
    private const int RepeatedJumpCancelCount=3;
    private const float BusyActivityStudyMultiplier=0.50f;
    private const float ClientHintLifetimeSeconds=2.25f;
    private const float ClientHintHeartbeatSeconds=1.25f;

    // Owning-client activity tracker. This never advances Knowledge; it only tells the server whether
    // a remote player is currently in an allowed-but-distracting UI/activity state.
    private static bool clientTracking;
    private static int clientItemType;
    private static ushort clientSeed;
    private static bool clientLastSlow;
    private static float clientHeartbeatRemaining;

    private static readonly string[] BusyWindowGroups=
    {
        XUiC_LootWindowGroup.ID,
        "workstation",
        "workstation_cntWoodBurningStove",
        "workstation_campfire",
        "workstation_forge",
        "workstation_forge_nosmelting",
        "workstation_cementMixer",
        "workstation_workbench",
        "workstation_chemistryStation",
        "workstation_WorkbenchMortarPestle001_FR",
        "workstation_WorkbenchGasStove001_FR",
        "workstation_WorkbenchIronOven001_FR",
        "inventory",
        "backpack",
        "character",
        "map",
        "vehicle",
        "crafting",
        "loot",
        "rebirthSurvivorCharacter",
        "rebirthProgressionExplorer",
        "rebirthBackpackLibrary",
        "rebirthBackpackSellStash",
        "rebirthMusicLibrary",
        "rebirthAudiobookLibrary",
        "rebirthJournal",
        "rebirthTeaching",
        "rebirthTeachingOffer",
        "rebirthLogistics",
        "rebirthCompanions",
        "rebirthCompanionInteraction",
        "rebirthNpcInteraction",
        "rebirthNpcDialogue",
        "rebirthNpcTrade",
        "rebirthNpcProgression",
        "looting",
        "trader",
        "questOffer",
        "questTurnIn",
        "quests"
    };

    public static string Install()
    {
        if(installed)return "[REBIRTH Literature] study sessions already installed.";
        installed=true;
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        return "[REBIRTH Literature] study sessions installed.";
    }

    public static void BeginClientTracking(EntityPlayer player,ItemValue itemValue)
    {
        // Quick Use has no physical-study session or activity heartbeat.
        if(!RebirthSandboxOptionManager.Current.RequireTimedReading)
        {clientTracking=false;return;}
        if(player==null||itemValue==null)return;
        clientTracking=true;
        clientItemType=itemValue.type;
        clientSeed=itemValue.Seed;
        clientLastSlow=false;
        clientHeartbeatRemaining=0f;
    }

    public static void SetActivityHint(EntityPlayer player,bool slow)
    {
        if(player==null)return;
        Session s;
        if(!Active.TryGetValue(player.entityId,out s)||s==null)return;
        s.ClientSlowActivity=slow;
        s.ClientActivityHintRemaining=ClientHintLifetimeSeconds;
    }

    public static bool TryBegin(EntityPlayer player,int itemType,ushort seed,out string message)
    {
        message=string.Empty;
        if(player==null||!RebirthWorldCharacterRepository.IsServerAuthority||!RebirthSurvivorMode.IsEnabledForCurrentWorld())
        {message=Localization.Get("xuiRebirthReadingUnavailable");return false;}
        if(RebirthCharacterCreationHoldService.IsHeld(player))
        {message=Localization.Get("xuiRebirthReadingFinishCreation");return false;}

        if(RebirthBackpackLibraryReservation.BlocksResourceUse(player))
        {message=Localization.Get("xuiRebirthLibraryTransferPending");return false;}

        ItemStack stack;
        if(!RebirthLiteratureService.TryFindMatchingInventoryStackPublic(player,itemType,seed,out stack)||stack==null||stack.IsEmpty()||stack.itemValue==null||stack.itemValue.ItemClass==null)
        {message=Localization.Get("xuiRebirthReadingItemMissing");return false;}

        if(!IsHoldingExactItem(player,itemType,seed))
        {
            message=Localization.Get("xuiRebirthReadingHoldItem");
            return false;
        }

        if(IsClimbing(player))
        {
            message=Localization.Get("xuiRebirthReadingCannotClimb");
            return false;
        }

        string itemId=stack.itemValue.ItemClass.GetItemName();
        RebirthLiteratureDefinition definition;
        if(!RebirthProgressionRuntimeConfig.TryGetLiterature(itemId,out definition)||definition==null)
        {message=Localization.Get("xuiRebirthReadingUnknownItem");return false;}

        if(RebirthLiteratureService.IsAlreadyCompleted(player,definition))
        {
            message=RebirthLiteratureService.GetAlreadyCompletedMessage(player,definition);
            return true;
        }

        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record)||record==null||record.Progression==null||!record.IsComplete||!RebirthSurvivorRequestScope.Matches(record.Origin?.CreationId,record.Origin?.CreationId))
        {message=Localization.Get("xuiRebirthReadingCharacterMissing");return false;}

        // Finalize the outgoing medium before reading the shared durable fraction. Otherwise a
        // 47% active audiobook with a 40% checkpoint can reopen physical study at stale 40%.
        // A same-medium restart/title change also replaces an active session.
        // Preserve its latest fraction before loading this title's checkpoint.
        if(!RebirthStudySessionSaveBinding.TryCapture(player,record,itemId,out var saveBinding))
        {message=Localization.Get("xuiRebirthReadingSavePending");return false;}
        Session outgoing;
        if(Active.TryGetValue(player.entityId,out outgoing) && outgoing!=null &&
            (outgoing.CompletedAwaitingSave||!Persist(player,record,outgoing)))
        {message=Localization.Get("xuiRebirthReadingSavePending");return false;}
        if(RebirthTheorySpecialistLessonService.IsActive(player)){message=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryFinishCurrent","Finish your current study session first.");return false;}
        RebirthTheorySoloService.PauseForOtherStudy(player);
        if(!RebirthAudiobookListeningSessionService.StopForStudyModeSwitch(player))
        {message=Localization.Get("xuiRebirthReadingSavePending");return false;}
        float progress=0f;
        record.Progression.LiteratureStudyProgress.TryGetValue(itemId,out progress);
        progress=Mathf.Clamp01(progress);

        float multiplier=RebirthSandboxOptionManager.Current.LiteratureStudyTimeMultiplier;
        float readerMultiplier=RebirthTraitGameplayModifierService.GetPhysicalLiteratureStudyTimeMultiplier(player);
        float teacherMultiplier=RebirthTeachingService.GetSoloStudyTimeMultiplier(player);
        float effective=Mathf.Max(2f,definition.StudySeconds*multiplier*readerMultiplier*teacherMultiplier);
        float health=player.Stats!=null&&player.Stats.Health!=null?player.Stats.Health.Value:0f;

        Active[player.entityId]=new Session
        {
            EntityId=player.entityId,CreationId=record.Origin.CreationId,SaveBinding=saveBinding,ItemType=itemType,Seed=seed,ItemId=itemId,
            LastPosition=player.position,LastHealth=health,
            WasJumping=player.CurrentMovementTag.Test_AnySet(TagJumping),
            Progress=progress,EffectiveSeconds=effective
        };

        int pct=Mathf.RoundToInt(progress*100f);
        float remaining=Mathf.Max(0f,effective*(1f-progress));
        RebirthStudyHudNetworkService.SendReading(player,itemId,progress,remaining,false);
        message=string.Format(Localization.Get(pct>0?"xuiRebirthReadingResumed":"xuiRebirthReadingStarted"),Friendly(itemId),pct,FormatSeconds(remaining));
        { if (RebirthLogSettings.LiteratureLoggingEnabled) RebirthLogSettings.TraceLiterature("study begin item="+itemId+" entity="+player.entityId+" progress="+progress.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" readerMultiplier="+readerMultiplier.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" effectiveSeconds="+effective.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)); }
        return true;
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        PumpOwningClientActivityHint();
        RebirthPendingRemoteStudy.Pump();

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
            if(player.IsDead()&&!s.CompletedAwaitingSave&&!s.CancelAwaitingSave){Cancel(player,s,Localization.Get("xuiRebirthReadingStopped"),true);continue;}

            RebirthWorldCharacterRecord record;
            if(!RebirthWorldCharacterService.TryGet(player,out record)||record==null||record.Progression==null||!record.IsComplete||!RebirthSurvivorRequestScope.Matches(s.CreationId,record.Origin?.CreationId))
            {Cancel(player,s,Localization.Get("xuiRebirthReadingCharacterStopped"),false);continue;}

            if(RetryCancelledSave(player,record,s,dt))continue;
            if(RetryCompletedSave(player,s,dt))continue;

            if(!RebirthLiteratureService.HasMatchingInventoryItem(player,s.ItemType,s.Seed))
            {Cancel(player,s,Localization.Get("xuiRebirthReadingItemStopped"),true);continue;}

            if(!IsHoldingExactItem(player,s.ItemType,s.Seed))
            {
                Cancel(player,s,BuildHeldItemCancellationReason(player),true);
                continue;
            }

            if(IsClimbing(player))
            {Cancel(player,s,Localization.Get("xuiRebirthReadingClimbing"),true);continue;}

            float health=player.Stats!=null&&player.Stats.Health!=null?player.Stats.Health.Value:s.LastHealth;
            if(health+0.01f<s.LastHealth){Cancel(player,s,Localization.Get("xuiRebirthReadingInjury"),true);continue;}
            s.LastHealth=health;

            if(s.JumpWindowRemaining>0f)s.JumpWindowRemaining=Mathf.Max(0f,s.JumpWindowRemaining-dt);
            else s.JumpCount=0;

            bool jumpingNow=player.CurrentMovementTag.Test_AnySet(TagJumping);
            if(jumpingNow&&!s.WasJumping)
            {
                if(s.JumpWindowRemaining<=0f)
                {
                    s.JumpCount=0;
                    s.JumpWindowRemaining=JumpWindowSeconds;
                }
                s.JumpCount++;
                { if (RebirthLogSettings.LiteratureLoggingEnabled) RebirthLogSettings.TraceLiterature("study jump item="+s.ItemId+" entity="+s.EntityId+" jumpCount="+s.JumpCount); }
                if(s.JumpCount>=RepeatedJumpCancelCount)
                {
                    Cancel(player,s,Localization.Get("xuiRebirthReadingJumping"),true);
                    continue;
                }
            }
            s.WasJumping=jumpingNow;
            s.LastPosition=player.position;

            if(s.ClientActivityHintRemaining>0f)
            {
                s.ClientActivityHintRemaining=Mathf.Max(0f,s.ClientActivityHintRemaining-dt);
                if(s.ClientActivityHintRemaining<=0f)s.ClientSlowActivity=false;
            }

            bool slow=IsServerObservableSlowActivity(player)||s.ClientSlowActivity;
            if(slow!=s.LastSlowState)
            {
                s.LastSlowState=slow;
                { if (RebirthLogSettings.LiteratureLoggingEnabled) RebirthLogSettings.TraceLiterature("study activity item="+s.ItemId+" entity="+s.EntityId+" speed="+(slow?"50%":"100%")); }
            }

            float activityMultiplier=slow?BusyActivityStudyMultiplier:1f;
            if(s.EffectiveSeconds<=0.001f)s.EffectiveSeconds=2f;
            s.Progress=Mathf.Clamp01(s.Progress+(dt/s.EffectiveSeconds)*activityMultiplier);
            s.SaveAccumulator+=dt;
            s.HudSyncAccumulator+=dt;

            if(s.HudSyncAccumulator>=RebirthStudyHudNetworkService.SnapshotIntervalSeconds)
            {
                float hudRemaining=Mathf.Max(0f,s.EffectiveSeconds*(1f-s.Progress)/Mathf.Max(0.01f,activityMultiplier));
                RebirthStudyHudNetworkService.SendReading(player,s.ItemId,s.Progress,hudRemaining,slow);
                s.HudSyncAccumulator=0f;
            }

            if(s.Progress>=0.9999f)
            {
                string result;
                if(RebirthLiteratureService.TryCompleteStudy(player,s.ItemType,s.Seed,out result))
                {
                    BeginCompletionSave(player,record,s,result);
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

    // Completion reward has already been applied. Retry only persistence, never study.
    private static void BeginCompletionSave(EntityPlayer player,RebirthWorldCharacterRecord record,Session s,string result)
    {
        record.Progression.LiteratureStudyProgress.Remove(s.ItemId);
        record.Touch("literature-study-complete:"+s.ItemId);
        s.CompletedAwaitingSave=true;s.CompletionMessage=result;s.SaveAccumulator=0f;
        if(SaveDirty(player,"literature-study-complete:"+s.ItemId))FinishCompleted(player,s);
    }
    private static bool RetryCompletedSave(EntityPlayer player,Session s,float dt)
    {
        if(!s.CompletedAwaitingSave)return false;
        s.SaveAccumulator+=dt;
        if(s.SaveAccumulator>=1f)
        {s.SaveAccumulator=0f;if(SaveDirty(player,"literature-completion-retry"))FinishCompleted(player,s);}
        return true;
    }
    private static void FinishCompleted(EntityPlayer player,Session s)
    {
        Active.Remove(s.EntityId);
        RebirthStudyHudNetworkService.SendClear(player);
        Send(player,true,s.CompletionMessage);
    }
    private static bool IsServerObservableSlowActivity(EntityPlayer player)
    {
        if(player==null)return false;
        FastTags<TagGroup.Global> tag=player.CurrentMovementTag;
        if(tag.Test_AnySet(TagRunning)||tag.Test_AnySet(TagJumping)||tag.Test_AnySet(TagSwimming)||tag.Test_AnySet(TagSwimmingRun))
            return true;

        // For a listen-server/local player the server can inspect its own UI directly. Remote clients
        // provide the same information through the bounded activity-hint heartbeat below.
        EntityPlayerLocal local=player as EntityPlayerLocal;
        return local!=null&&IsBusyUiOpen(local);
    }

    private static void PumpOwningClientActivityHint()
    {
        if(!RebirthSandboxOptionManager.Current.RequireTimedReading)
        {clientTracking=false;return;}
        if(!clientTracking||GameManager.Instance==null||GameManager.Instance.World==null)return;
        World world=GameManager.Instance.World;
        EntityPlayerLocal player=world.GetPrimaryPlayer();
        if(player==null)return;

        ItemValue held=player.inventory!=null?player.inventory.holdingItemItemValue:null;
        if(held==null||held.type!=clientItemType||held.Seed!=clientSeed)
        {
            clientTracking=false;
            SendClientActivityHint(player,false);
            return;
        }

        bool slow=IsServerObservableSlowActivity(player)||IsBusyUiOpen(player);
        float dt=Mathf.Clamp(Time.unscaledDeltaTime,0f,0.25f);
        clientHeartbeatRemaining-=dt;
        if(slow!=clientLastSlow||clientHeartbeatRemaining<=0f)
        {
            clientLastSlow=slow;
            clientHeartbeatRemaining=ClientHintHeartbeatSeconds;
            SendClientActivityHint(player,slow);
        }
    }

    private static bool IsBusyUiOpen(EntityPlayerLocal player)
    {
        if(player==null||player.PlayerUI==null||player.PlayerUI.windowManager==null)return false;
        GUIWindowManager manager=player.PlayerUI.windowManager;
        for(int i=0;i<BusyWindowGroups.Length;i++)
        {
            string id=BusyWindowGroups[i];
            if(string.IsNullOrEmpty(id))continue;
            try { if(manager.IsWindowOpen(id))return true; } catch { }
        }
        return false;
    }

    private static void SendClientActivityHint(EntityPlayerLocal player,bool slow)
    {
        if(player==null||player.world==null)return;
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!player.world.IsRemote())
        {
            if(c!=null&&c.IsServer)SetActivityHint(player,slow);
            return;
        }
        // Transient transport failures must not escape the recurring game-update callback.
        // The existing heartbeat retries current activity without a new queue or toast spam.
        try
        {
            var request=NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionRequest>();
            if(!RebirthMusicLibraryClient.CanSend(c,request))return;
            c.SendToServer(request.Setup(player.entityId,RebirthSurvivorSupportAction.SetLiteratureActivity,null,slow?"slow":"normal"));
        }
        catch { }
    }

    private static string BuildHeldItemCancellationReason(EntityPlayer player)
    {
        if(player==null||player.inventory==null)
            return Localization.Get("xuiRebirthReadingNotHeld");

        ItemValue held=player.inventory.holdingItemItemValue;
        if(held==null||held.ItemClass==null)
            return Localization.Get("xuiRebirthReadingNotHeld");

        try
        {
            ItemAction[] actions=held.ItemClass.Actions;
            if(actions!=null)
            {
                for(int i=0;i<actions.Length;i++)
                {
                    ItemAction action=actions[i];
                    if(action==null)continue;
                    if(action is ItemActionAttack)
                        return Localization.Get("xuiRebirthReadingWeapon");
                }
            }
        }
        catch { }

        return Localization.Get("xuiRebirthReadingSwitched");
    }

    private static bool IsHoldingExactItem(EntityPlayer player,int itemType,ushort seed)
    {
        if(player==null||player.inventory==null)return false;
        ItemValue held=player.inventory.holdingItemItemValue;
        return held!=null&&held.type==itemType&&held.Seed==seed;
    }

    private static bool IsClimbing(EntityPlayer player)
    {
        return player!=null&&player.CurrentMovementTag.Test_AnySet(TagClimbing);
    }

    private static bool Persist(EntityPlayer player,RebirthWorldCharacterRecord record,Session s)
    {
        if(player==null||record==null||record.Progression==null||s==null
            ||!record.IsComplete||!RebirthSurvivorRequestScope.Matches(s.CreationId,record.Origin?.CreationId))return false;
        if(s.CompletedAwaitingSave)return SaveDirty(player,"literature-completion-save");
        record.Progression.LiteratureStudyProgress[s.ItemId]=Mathf.Clamp(s.Progress,0.001f,0.999f);
        record.Touch("literature-study-progress:"+s.ItemId);
        return SaveDirty(player,"literature-study-progress:"+s.ItemId);
    }

    private static bool SaveDirty(EntityPlayer player,string reason)
    {
        RebirthStablePlayerIdentity identity;
        if(player==null||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out identity)||identity==null)return false;
        try{return RebirthWorldCharacterRepository.SaveIfDirty(identity,reason);}
        catch{return false;}
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
                bool first=!s.CancelAwaitingSave;s.CancelAwaitingSave=true;s.SaveAccumulator=0f;
                if(first){RebirthStudyHudNetworkService.SendClear(player);Send(player,false,message);}
                return;
            }
        }
        Active.Remove(s.EntityId);
        if(player!=null)RebirthStudyHudNetworkService.SendClear(player);
        if(player!=null)Send(player,false,message);
        { if (RebirthLogSettings.LiteratureLoggingEnabled) RebirthLogSettings.TraceLiterature("study cancel item="+s.ItemId+" entity="+s.EntityId+" progress="+s.Progress.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" reason="+(message??string.Empty)); }
    }

    private static void Send(EntityPlayer player,bool ok,string message)
    {
        if(player==null)return;
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(c!=null&&c.IsServer)c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionResult>().Setup(ok,message),_attachedToEntityId:player.entityId);
    }

    public static bool TryGetUiState(EntityPlayer player,out string itemId,out float progress,out float remainingSeconds,out bool slowAttention)
    {
        itemId=string.Empty;progress=0f;remainingSeconds=0f;slowAttention=false;
        if(player==null)return false;
        Session s;
        if(!Active.TryGetValue(player.entityId,out s)||s==null||s.CancelAwaitingSave)return false;
        progress=Mathf.Clamp01(s.Progress);
        itemId=s.ItemId??string.Empty;
        slowAttention=IsServerObservableSlowActivity(player)||s.ClientSlowActivity;
        float rate=slowAttention?BusyActivityStudyMultiplier:1f;
        remainingSeconds=Mathf.Max(0f,s.EffectiveSeconds*(1f-progress)/Mathf.Max(0.01f,rate));
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
        Cancel(player,s,Localization.Get("xuiRebirthReadingPaused"),false);
        return true;
    }

    public static string BuildDebugSummary(EntityPlayer player)
    {
        if(player==null)return "[REBIRTH Literature] player unavailable";
        Session s;
        if(!Active.TryGetValue(player.entityId,out s)||s==null)
            return "[REBIRTH Literature] entity="+player.entityId+" readingActive=false";
        bool serverSlow=IsServerObservableSlowActivity(player);
        bool slow=serverSlow||s.ClientSlowActivity;
        float pct=Mathf.Clamp01(s.Progress)*100f;
        return "[REBIRTH Literature] entity="+player.entityId
            +" readingActive=true item="+s.ItemId
            +" progress="+pct.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"%"
            +" effectiveSeconds="+s.EffectiveSeconds.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
            +" holdingExact="+IsHoldingExactItem(player,s.ItemType,s.Seed)
            +" climbing="+IsClimbing(player)
            +" attention="+(slow?"50%":"100%")
            +" serverSlow="+serverSlow
            +" clientSlow="+s.ClientSlowActivity
            +" jumpCount="+s.JumpCount
            +" jumpWindow="+s.JumpWindowRemaining.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);
    }

    public static bool TryGetDebugProgress(EntityPlayer player,out string itemId,out float progress,out float effectiveSeconds,out bool slow)
    {
        itemId=string.Empty;progress=0f;effectiveSeconds=0f;slow=false;
        if(player==null)return false;
        Session s;
        if(!Active.TryGetValue(player.entityId,out s)||s==null||s.CancelAwaitingSave)return false;
        itemId=s.ItemId??string.Empty;
        progress=Mathf.Clamp01(s.Progress);
        effectiveSeconds=s.EffectiveSeconds;
        slow=IsServerObservableSlowActivity(player)||s.ClientSlowActivity;
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
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        RebirthPendingRemoteStudy.Clear();
        Active.Clear();
        clientTracking=false;
    }

    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)
    {
        RebirthPendingRemoteStudy.Clear();
        Active.Clear();
        clientTracking=false;
    }

    private static string Friendly(string itemId)
    {
        string s=Localization.Get(itemId);
        return !string.IsNullOrEmpty(s)&&!string.Equals(s,itemId,StringComparison.OrdinalIgnoreCase)?s:itemId;
    }

    private static string FormatSeconds(float value)
    {
        int seconds=Math.Max(0,Mathf.CeilToInt(value));
        if(seconds<60)return string.Format(Localization.Get("xuiRebirthReadingSeconds"),seconds);
        return string.Format(Localization.Get("xuiRebirthReadingMinutesSeconds"),seconds/60,seconds%60);
    }
}
