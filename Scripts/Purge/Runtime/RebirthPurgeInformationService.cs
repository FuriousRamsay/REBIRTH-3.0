using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

// Teaching windows use the existing Journal's saved read state, not another save format.
internal static class RebirthPurgeInformationService
{
    private sealed class Pending
    {
        internal EntityPlayerLocal Player;
        internal RebirthJournalEntry Entry;
        internal string TopicId;
        internal string Id => Entry?.Id ?? TopicId;
        internal string Sprite;
        internal bool Manual;
        internal bool AtReveal;
        internal float NotBefore;
        // Native OnOpen can acknowledge synchronously; presentation follows in
        // the manager update. Keep the request until that callback is received.
        internal float RequestedAt;
        internal int Attempts;
        internal bool Acknowledged;
        internal float ExpiresAt;
    }
    private static readonly Queue<Pending> pending=new Queue<Pending>();
    private static readonly List<Entity> nearby=new List<Entity>();
    private static float next;
    private static EntityPlayerLocal restoredPlayer;
    private static EntityPlayerLocal restoredSupplyPlayer;
    private static EntityPlayerLocal restoredManualPlayer;
    private static object restoredReceipts;
    private static EntityPlayerLocal readingPlayer;
    private static readonly HashSet<string> readingOnly=new HashSet<string>(StringComparer.Ordinal);
    private static string lastBlockedReason;
    private static float nextBlockedTrace;
    // Retries after external closes are bounded per session; explicit player closes never retry.
    internal static int EarlyCloseRequeues;
    internal static string Diagnostic(EntityPlayerLocal player)
    {
        var ui=LocalPlayerUI.GetUIForPlayer(player)?.windowManager;
        var head=pending.Count>0?pending.Peek():null;
        return "pending="+pending.Count+", head="+(head?.Id??"none")+", atReveal="+(head?.AtReveal??false)+
            ", notBefore="+(head?.NotBefore??0)+", requestedAt="+(head?.RequestedAt??0)+", attempts="+(head?.Attempts??0)+
            ", acknowledged="+(head?.Acknowledged??false)+", expiresAt="+(head?.ExpiresAt??0)+
            ", lastBlock="+(lastBlockedReason??"none")+", spawned="+(player?.IsSpawned()??false)+
            ", introAnimation="+(player?.bIntroAnimActive??false)+", handoff="+RebirthPurgeTruckStart.StartHandoffActive(player)+
            ", modal="+(ui?.IsModalWindowOpen()??false)+", input="+(ui?.IsInputActive()??false)+
            ", time="+Time.realtimeSinceStartup+", frame="+Time.frameCount;
    }
    internal static void Acknowledge(string id)
    {
        foreach(var request in pending)if(request.Id==id&&request.RequestedAt>0)request.Acknowledged=true;
    }
    internal static void Reset(){EarlyCloseRequeues=0;pending.Clear();nearby.Clear();next=0;restoredPlayer=null;restoredSupplyPlayer=null;restoredManualPlayer=null;restoredReceipts=null;readingPlayer=null;readingOnly.Clear();lastBlockedReason=null;nextBlockedTrace=0;}
    private static void TraceBlocked(Pending request,string reason,float now)
    {
        if(request==null||!RebirthPurgeIntroDiagnostics.Enabled||
           (!request.AtReveal&&now<request.NotBefore+10f)||
           (lastBlockedReason==reason&&now<nextBlockedTrace))return;
        lastBlockedReason=reason;nextBlockedTrace=now+30f;
        Log.Out("[REBIRTH Purge Information] queued="+request.Id+" blocked="+reason);
    }
    internal static void RestoreReadingOnly(EntityPlayerLocal player,string id)
    {
        if(!ReferenceEquals(readingPlayer,player)){readingPlayer=player;readingOnly.Clear();}
        // A late join-restoration packet must not cancel teaching already queued
        // by a live milestone or an explicit manual read.
        foreach(var request in pending)
            if(ReferenceEquals(request.Player,player)&&request.Id==id)return;
        readingOnly.Add(id);
        RebirthJournalGuideService.SetPurgeReadingOnly(player,id,true);
    }
    internal static void Request(EntityPlayerLocal player,string id,string sprite,bool manual=false,float notBefore=0)
    {
        // A late UI close can outlive its world or primary player. Reject it
        // before the Journal resolves the current recipient's saved context.
        if(player==null||!ReferenceEquals(player.world,GameManager.Instance?.World)||
            !ReferenceEquals(player.world?.GetPrimaryPlayer(),player))return;
        var entry=RebirthJournalGuideService.ObservePurgeGuide(player,id);
        if(entry==null)
        {
            // Preserve consumed-note intent and the one-shot first-start intro
            // while the Journal is unavailable. Intro keeps its original deadline.
            if((!manual&&id!="PurgeIntro")||!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||
                player==null||!ReferenceEquals(player.world,GameManager.Instance?.World)||
                !ReferenceEquals(player.world?.GetPrimaryPlayer(),player))return;
            var topic=RebirthPurgeInformationTopics.Find(id);
            if(topic==null)return;
            id=topic.Id;
        }
        else if(!manual && (RebirthJournalGuideService.IsPurgeDismissed(entry)||RebirthJournalGuideService.IsRead(entry)))return;
        if(manual)RebirthPurgeTeachingIntent.RetainManual(player,id);
        if(entry!=null)RebirthJournalGuideService.SetPurgeReadingOnly(player,id,false);
        // Explicit live teaching/manual reading takes precedence over silent join restoration.
        if(ReferenceEquals(readingPlayer,player))readingOnly.Remove(id);
        foreach(var request in pending)if(request.Player==player && request.Id==id)
        {
            if(request.Entry==null&&entry!=null)request.Entry=entry;
            // Automatic duplicates must not rearm an already opened manual
            // reading (or postpone one which is still waiting to open).
            if(!manual&&request.Manual)return;
            if(request.Acknowledged){request.Acknowledged=false;request.RequestedAt=0;request.Attempts=0;} // re-offer after a close that arrived before Pulse dequeued it
            if(manual){request.Manual=true;request.NotBefore=0;request.ExpiresAt=0;} // Manual reading is not subject to the first-start expiry.
            else request.NotBefore=Math.Max(request.NotBefore,notBefore);
            return;
        }
        float scheduled=manual?0:Math.Max(Time.realtimeSinceStartup,notBefore);
        pending.Enqueue(new Pending{Player=player,Entry=entry,TopicId=id,Sprite=sprite,Manual=manual,
            NotBefore=scheduled,ExpiresAt=!manual&&id=="PurgeIntro"?scheduled+60f:0});
    }
    // Called only after the first-entry loading/attachment lease has retired.
    internal static void RevealIntro(EntityPlayerLocal player)
    {
        var topic=RebirthPurgeInformationTopics.Find("PurgeIntro");
        if(topic==null)return;
        RebirthPurgeInformationTopics.Request(player,"PurgeIntro");
        // First-entry briefing owns this reveal. Keep other queued teaching in order behind it.
        var queued=pending.ToArray();pending.Clear();
        foreach(var request in queued)if(ReferenceEquals(request.Player,player)&&request.Id=="PurgeIntro"&&!request.Manual)
        {request.AtReveal=true;request.NotBefore=Time.realtimeSinceStartup;request.ExpiresAt=request.NotBefore+60f;pending.Enqueue(request);}
        foreach(var request in queued)if(!(ReferenceEquals(request.Player,player)&&request.Id=="PurgeIntro"&&!request.Manual))pending.Enqueue(request);
        if(RebirthPurgeIntroDiagnostics.Enabled)Log.Out("[REBIRTH Purge Intro] reveal entity="+player?.entityId+" queued="+pending.Count);
        next=0;Pulse();
    }
    internal static void Pulse()
    {
        if(!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.IsPurge)
        {if(pending.Count>0 || restoredPlayer!=null || restoredManualPlayer!=null)Reset();return;}
        var currentPlayer=GameManager.Instance?.World?.GetPrimaryPlayer();
        var receipts=RebirthPurgeMilestoneService.PublishedAll;
        bool manualRecovery=currentPlayer!=null&&currentPlayer.IsSpawned()&&!ReferenceEquals(restoredManualPlayer,currentPlayer);
        bool supplyRecovery=currentPlayer!=null&&!ReferenceEquals(restoredSupplyPlayer,currentPlayer)&&
            currentPlayer.Buffs.GetCustomVar("_rbPurgeSupplyKnown")==1&&
            currentPlayer.Buffs.GetCustomVar("_rbPurgeSupplyDeliveredAny")==1;
        if(pending.Count==0 && ReferenceEquals(restoredPlayer,currentPlayer) &&
            (receipts==null || ReferenceEquals(restoredReceipts,receipts))&&!supplyRecovery&&!manualRecovery)return;
        float now=Time.realtimeSinceStartup;
        // Closing native progress is queued. Until the reveal request is submitted, retry on
        // the next UI frame instead of adding the ordinary half-second teaching poll delay.
        bool revealWaiting=pending.Count>0&&pending.Peek().AtReveal&&pending.Peek().RequestedAt==0;
        if(now<next&&!revealWaiting)return;
        next=now+.5f;
        if(manualRecovery)
        {
            // An explicit consumed-note reread survives normal session reset,
            // including already read/dismissed guides and the manual intro.
            foreach(var topic in RebirthPurgeInformationTopics.All)
                if(RebirthPurgeTeachingIntent.ManualPending(currentPlayer,topic.Id))
                    Request(currentPlayer,topic.Id,topic.Sprite,true);
            restoredManualPlayer=currentPlayer;
        }
        if(supplyRecovery)
        {
            var entry=RebirthJournalGuideService.ObservePurgeGuide(currentPlayer,"PurgeSupplyUpdate");
            if(entry!=null)
            {
                RebirthPurgeInformationTopics.Request(currentPlayer,"PurgeSupplyUpdate");
                restoredSupplyPlayer=currentPlayer;
            }
        }
        if(currentPlayer!=null && receipts!=null &&
            (!ReferenceEquals(restoredPlayer,currentPlayer)||!ReferenceEquals(restoredReceipts,receipts)))
        {
            bool restored=true;
            foreach(var receipt in receipts.Values)
                restored &= RebirthPurgeMilestoneNotification.RestoreReading(currentPlayer,receipt.Biome,receipt.Tier);
            if(restored)restoredReceipts=receipts;
        }
        if(currentPlayer!=null && !ReferenceEquals(restoredPlayer,currentPlayer))
        {
            // The first-start handoff opens the introduction at gameplay reveal.
            // Journal unlock/dismissal supplies the existing per-save once-only state.
            RebirthJournalGuideService.ObservePurgeGuide(currentPlayer,"PurgeIntro");
            List<RebirthJournalEntry> unread;
            if(RebirthJournalGuideService.TryGetUnlockedPurgeGuides(currentPlayer,out unread,true))
            {
                // 2.6 gives these two reading notes on the first Purge spawn.
                // Keep them available through the existing saved Journal instead
                // of introducing inventory awards or extra automatic popups.
                foreach(string id in new[]{"PurgeSupplies","PurgeScanner"})
                    if(!unread.Exists(entry=>entry.Id==id))
                    {
                        var restored=RebirthJournalGuideService.ObservePurgeGuide(currentPlayer,id);
                        if(restored==null)return; // Journal readiness can lag behind spawn.
                        RestoreReadingOnly(currentPlayer,id);
                        unread.Add(restored);
                    }
                restoredPlayer=currentPlayer;
                foreach(var entry in unread)
                {
                    var topic=RebirthPurgeInformationTopics.Find(entry.Id);
                    if(topic==null)continue;
                    RebirthPurgeInformationTopics.UnlockReadingRecipe(currentPlayer,topic);
                    // World-GUID-scoped unread introductions remain pending until actually dismissed.
                    if(entry.Id=="PurgeIntro")
                    {
                        if(RebirthJournalGuideService.IsPurgeDismissed(entry)||RebirthJournalGuideService.IsRead(entry))continue;
                        bool introQueued=false;
                        foreach(var queued in pending)if(queued.Player==currentPlayer&&queued.Id==entry.Id){introQueued=true;break;}
                        if(!introQueued)pending.Enqueue(new Pending{Player=currentPlayer,Entry=entry,Sprite=topic.Sprite,AtReveal=true});
                        continue;
                    }
                    // Dismissal hides the automatic window, not the earned reading recipe.
                    if(RebirthJournalGuideService.IsPurgeDismissed(entry)||RebirthJournalGuideService.IsRead(entry)||RebirthJournalGuideService.IsPurgeReadingOnly(entry)||
                        ReferenceEquals(readingPlayer,currentPlayer)&&readingOnly.Contains(entry.Id))continue;
                    bool exists=false;
                    foreach(var queued in pending)if(queued.Player==currentPlayer && queued.Id==entry.Id){exists=true;break;}
                    if(!exists)pending.Enqueue(new Pending{Player=currentPlayer,Entry=entry,Sprite=topic.Sprite,
                        NotBefore=0});
                }
            }
        }
        if(pending.Count==0)return;
        Pending request=pending.Peek();
        if(request.Acknowledged){pending.Dequeue();lastBlockedReason=null;nextBlockedTrace=0;return;}
        if(request.ExpiresAt>0&&now>request.ExpiresAt)
        {pending.Dequeue();lastBlockedReason=null;nextBlockedTrace=0;return;}
        // A scheduled open that never reached OnOpen (another UI transition in between) is retried, bounded.
        if(request.RequestedAt>0)
        {
            if(now-request.RequestedAt<2f)return;
            if(request.Attempts>=3){pending.Dequeue();Log.Out("[REBIRTH Purge Information] open never acknowledged id="+request.Id+"; offered again on next load");return;}
        }
        if(now<request.NotBefore)return;
        var player=request.Player;
        var world=GameManager.Instance?.World;
        if(player==null || !ReferenceEquals(player.world,world)){Reset();return;}
        if(request.Entry==null)
        {
            if(!ReferenceEquals(world?.GetPrimaryPlayer(),player)){pending.Dequeue();return;}
            request.Entry=RebirthJournalGuideService.ObservePurgeGuide(player,request.Id);
            if(request.Entry==null){TraceBlocked(request,"journal-unavailable",now);return;}
            RebirthJournalGuideService.SetPurgeReadingOnly(player,request.Id,false);
            if(ReferenceEquals(readingPlayer,player))readingOnly.Remove(request.Id);
        }
        if(!request.Manual && (RebirthJournalGuideService.IsPurgeDismissed(request.Entry)||RebirthJournalGuideService.IsRead(request.Entry))){pending.Dequeue();return;}
        if(!request.Manual && ReferenceEquals(readingPlayer,player)&&readingOnly.Contains(request.Entry.Id))
        {pending.Dequeue();return;}
        // Opening during the starter-truck handoff (loading cover, intro animation) lets that startup close the
        // window unseen, and its close would then record the introduction as dismissed.
        // AtReveal is already protected by the truck loading/attachment lease.
        // A lingering native revive-animation flag must not postpone visible gameplay's briefing.
        if(!request.Manual && ((!request.AtReveal&&player.bIntroAnimActive)||RebirthPurgeTruckStart.StartHandoffActive(player)))
        {TraceBlocked(request,"start-handoff",now);return;}
        var ui=LocalPlayerUI.GetUIForPlayer(player);
        var manager=ui?.windowManager;
        if(manager==null || player.IsDead() || !player.Spawned ||
            manager.IsInputActive() || manager.IsModalWindowOpen())
        {
            TraceBlocked(request,manager==null?"window-manager-unavailable":player.IsDead()?"player-dead":
                !player.Spawned?"player-not-spawned":manager.IsInputActive()?"input-active":"modal-open",now);
            return;
        }
        // The owner requested the first-entry briefing at reveal, including when a
        // hostile happens to be nearby. Ordinary teaching/manual reading keeps its safety gate.
        if(!request.AtReveal)
        {
            // Do not interrupt a player near a live hostile, even if their HUD is the only UI.
            nearby.Clear();
            world.GetEntitiesInBounds(typeof(EntityAlive),
                new Bounds(player.position,new Vector3(100,100,100)),nearby);
            bool unsafeToRead=false;
            foreach(var entity in nearby)
            {
                var alive=entity as EntityAlive;
                if(alive!=null && alive!=player && !alive.IsDead() &&
                    (alive.EntityClass?.bIsEnemyEntity==true || alive.GetAttackTarget()==player ||
                     alive is EntityRebirthNPC && FactionManager.Instance!=null &&
                     RebirthNpcFactionCombatResolver.Classify(FactionManager.Instance.GetRelationshipValue(alive,player))==RebirthNpcCombatDisposition.Hostile))
                {unsafeToRead=true;break;}
            }
            nearby.Clear();
            if(unsafeToRead){TraceBlocked(request,"hostile-nearby",now);return;}
        }
        // Native openInternal invokes OnOpen synchronously even while isShowing
        // is queued for the next frame. Arm the acknowledgement BEFORE Open.
        float previousRequest=request.RequestedAt;
        int previousAttempts=request.Attempts;
        request.RequestedAt=now;request.Attempts++;
        if(XUiC_RebirthPurgeInformation.Open(ui.xui,request.Entry,request.Sprite,request.Manual))
        {lastBlockedReason=null;nextBlockedTrace=0;}
        else
        {
            request.RequestedAt=previousRequest;request.Attempts=previousAttempts;
            TraceBlocked(request,"window-controller-unavailable",now);
        }
    }
}

[Preserve]
public sealed class XUiC_RebirthPurgeInformation : XUiController
{
    private RebirthJournalEntry entry;
    private string sprite;
    private float openedAt;
    private bool playerClosed;
    private bool manualReading;
    private XUiV_Label title,body;
    private XUiV_Sprite art;
    private XUiC_SimpleButton close;
    public override void Init()
    {
        base.Init();
        title=GetChildById("purgeInformationTitle")?.ViewComponent as XUiV_Label;
        body=GetChildById("purgeInformationBody")?.ViewComponent as XUiV_Label;
        art=GetChildById("purgeInformationArt")?.ViewComponent as XUiV_Sprite;
        close=GetChildById("purgeInformationClose") as XUiC_SimpleButton;
        if(close!=null)close.OnPressed+=ClosePressed;
    }
    public override void Cleanup()
    {
        if(close!=null)close.OnPressed-=ClosePressed;
        base.Cleanup();
    }
    public override void OnOpen()
    {
        base.OnOpen();
        openedAt=Time.realtimeSinceStartup;playerClosed=false;
        if(entry!=null)RebirthPurgeInformationService.Acknowledge(entry.Id);
        if(entry?.Id=="PurgeIntro")if(RebirthPurgeIntroDiagnostics.Enabled)Log.Out("[REBIRTH Purge Intro] opened entity="+xui?.playerUI?.entityPlayer?.entityId+" frame="+Time.frameCount);
        if(title!=null)title.Text=entry?.Title??string.Empty;
        if(body!=null)body.Text=entry?.Body??string.Empty;
        if(art!=null)art.SpriteName=sprite??"ThePurgeTitle";
        GetChildByType<XUiC_RebirthReadableText>()?.ResetReadingPosition();
    }
    public override void OnClose()
    {
        if(entry?.Id=="PurgeIntro")if(RebirthPurgeIntroDiagnostics.Enabled)Log.Out("[REBIRTH Purge Intro] closed explicit="+playerClosed+" visibleSeconds="+(Time.realtimeSinceStartup-openedAt));
        // Dismissal is an explicit player action; see below.
        var player=xui?.playerUI?.entityPlayer;
        if(entry?.Authored==true&&player!=null&&
            ReferenceEquals(player.world,GameManager.Instance?.World)&&
            ReferenceEquals(player.world?.GetPrimaryPlayer(),player))
        {
            // Only the player's Close button/Escape dismisses, whatever the elapsed time. An external close (other UI,
            // loading cover) leaves the entry unread: re-queue it (twice per session); past the cap it stays
            // undismissed and is offered on the next load.
            if(playerClosed)
            {
                RebirthJournalGuideService.DismissPurge(entry);
                RebirthPurgeTeachingIntent.Retire(player,entry.Id);
            }
            else if((entry.Id!="PurgeIntro"||manualReading)&&RebirthPurgeInformationService.EarlyCloseRequeues<2)
            {
                RebirthPurgeInformationService.EarlyCloseRequeues++;
                // A manual reread may already be read/dismissed. Preserve its
                // explicit intent instead of rejecting the retry as automatic teaching.
                RebirthPurgeInformationService.Request(player,entry.Id,sprite,manualReading);
            }
        }
        entry=null;
        manualReading=false;
        base.OnClose();
    }
    private void ClosePressed(XUiController sender,int button)
    {
        if(button!=0 && button!=-1)return;
        playerClosed=true;
        xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        if(windowGroup?.isShowing==true && entry?.Id=="PurgeDiscovery")
        {
            // A remote objective projection can arrive after the reading window opens.
            // Refresh the shared entry at the journal's bounded poll cadence.
            RebirthJournalGuideService.Poll(xui.playerUI.entityPlayer);
            if(body!=null && body.Text!=entry.Body)body.Text=entry.Body;
        }
        if(windowGroup?.isShowing==true && !RebirthConsoleInputGuardRuntime.BlocksGameplayInput() &&
            XUiUtils.HotkeysAllowedFor(viewComponent) && Time.realtimeSinceStartup-openedAt>0.25f &&
            xui.playerUI.playerInput.PermanentActions.Cancel.WasPressed)
            ClosePressed(this,-1);
    }
    internal static bool Open(XUi ui,RebirthJournalEntry guide,string image,bool manual=false)
    {
        var controller=ui?.GetChildByType<XUiC_RebirthPurgeInformation>();
        if(controller==null || controller.windowGroup==null || ui.playerUI.windowManager.IsFullHUDDisabled())return false;
        controller.entry=guide;controller.sprite=image;controller.manualReading=manual;
        // Own the explicit Cancel press below: native auto-Escape closes on press,
        // before a release handler can mark this as a deliberate dismissal.
        ui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup,true,true);
        // OnOpen acknowledges the admitted request; isShowing may still remain
        // false until the manager's next update.
        return true;
    }
}

internal static class RebirthPurgeIntroDiagnostics { internal static bool Enabled=true; }
