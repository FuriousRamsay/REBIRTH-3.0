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
        internal string Sprite;
        internal bool Manual;
        internal float NotBefore;
    }
    private static readonly Queue<Pending> pending=new Queue<Pending>();
    private static readonly List<Entity> nearby=new List<Entity>();
    private static float next;
    private static EntityPlayerLocal restoredPlayer;
    private static EntityPlayerLocal restoredSupplyPlayer;
    private static object restoredReceipts;
    private static EntityPlayerLocal readingPlayer;
    private static readonly HashSet<string> readingOnly=new HashSet<string>(StringComparer.Ordinal);
    internal static void Reset(){pending.Clear();nearby.Clear();next=0;restoredPlayer=null;restoredSupplyPlayer=null;restoredReceipts=null;readingPlayer=null;readingOnly.Clear();}
    internal static void RestoreReadingOnly(EntityPlayerLocal player,string id)
    {
        if(!ReferenceEquals(readingPlayer,player)){readingPlayer=player;readingOnly.Clear();}
        // A late join-restoration packet must not cancel teaching already queued
        // by a live milestone or an explicit manual read.
        foreach(var request in pending)
            if(ReferenceEquals(request.Player,player)&&request.Entry.Id==id)return;
        readingOnly.Add(id);
        RebirthJournalGuideService.SetPurgeReadingOnly(player,id,true);
    }
    internal static void Request(EntityPlayerLocal player,string id,string sprite,bool manual=false,float notBefore=0)
    {
        var entry=RebirthJournalGuideService.ObservePurgeGuide(player,id);
        if(entry==null || !manual && (RebirthJournalGuideService.IsPurgeDismissed(entry)||RebirthJournalGuideService.IsRead(entry)))return;
        RebirthJournalGuideService.SetPurgeReadingOnly(player,id,false);
        // Explicit live teaching/manual reading takes precedence over silent join restoration.
        if(ReferenceEquals(readingPlayer,player))readingOnly.Remove(id);
        foreach(var request in pending)if(request.Player==player && request.Entry.Id==id)
        {
            if(manual){request.Manual=true;request.NotBefore=0;}
            else request.NotBefore=Math.Max(request.NotBefore,notBefore);
            return;
        }
        pending.Enqueue(new Pending{Player=player,Entry=entry,Sprite=sprite,Manual=manual,
            NotBefore=manual?0:Math.Max(notBefore,id=="PurgeIntro"?Time.realtimeSinceStartup+5:0)});
    }
    internal static void Pulse()
    {
        if(!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.IsPurge)
        {if(pending.Count>0 || restoredPlayer!=null)Reset();return;}
        var currentPlayer=GameManager.Instance?.World?.GetPrimaryPlayer();
        var receipts=RebirthPurgeMilestoneService.PublishedAll;
        bool supplyRecovery=currentPlayer!=null&&!ReferenceEquals(restoredSupplyPlayer,currentPlayer)&&
            currentPlayer.Buffs.GetCustomVar("_rbPurgeSupplyKnown")==1&&
            currentPlayer.Buffs.GetCustomVar("_rbPurgeSupplyDeliveredAny")==1;
        if(pending.Count==0 && ReferenceEquals(restoredPlayer,currentPlayer) &&
            (receipts==null || ReferenceEquals(restoredReceipts,receipts))&&!supplyRecovery)return;
        float now=Time.realtimeSinceStartup;
        if(now<next)return;
        next=now+.5f;
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
            // The legacy startup quest opened the introduction after five seconds.
            // Journal unlock/dismissal supplies the existing per-save once-only state.
            RebirthJournalGuideService.ObservePurgeGuide(currentPlayer,"PurgeIntro");
            List<RebirthJournalEntry> unread;
            if(RebirthJournalGuideService.TryGetUnlockedPurgeGuides(currentPlayer,out unread,true))
            {
                restoredPlayer=currentPlayer;
                foreach(var entry in unread)
                {
                    var topic=RebirthPurgeInformationTopics.Find(entry.Id);
                    if(topic==null)continue;
                    RebirthPurgeInformationTopics.UnlockReadingRecipe(currentPlayer,topic);
                    // Dismissal hides the automatic window, not the earned reading recipe.
                    if(RebirthJournalGuideService.IsPurgeDismissed(entry)||RebirthJournalGuideService.IsRead(entry)||RebirthJournalGuideService.IsPurgeReadingOnly(entry)||
                        ReferenceEquals(readingPlayer,currentPlayer)&&readingOnly.Contains(entry.Id))continue;
                    bool exists=false;
                    foreach(var queued in pending)if(queued.Player==currentPlayer && queued.Entry.Id==entry.Id){exists=true;break;}
                    if(!exists)pending.Enqueue(new Pending{Player=currentPlayer,Entry=entry,Sprite=topic.Sprite,
                        NotBefore=entry.Id=="PurgeIntro"?now+5:0});
                }
            }
        }
        if(pending.Count==0)return;
        Pending request=pending.Peek();
        if(now<request.NotBefore)return;
        var player=request.Player;
        var world=GameManager.Instance?.World;
        if(player==null || !ReferenceEquals(player.world,world)){Reset();return;}
        if(!request.Manual && (RebirthJournalGuideService.IsPurgeDismissed(request.Entry)||RebirthJournalGuideService.IsRead(request.Entry))){pending.Dequeue();return;}
        if(!request.Manual && ReferenceEquals(readingPlayer,player)&&readingOnly.Contains(request.Entry.Id))
        {pending.Dequeue();return;}
        var ui=LocalPlayerUI.GetUIForPlayer(player);
        var manager=ui?.windowManager;
        if(manager==null || player.IsDead() || !player.Spawned ||
            manager.IsInputActive() || manager.IsModalWindowOpen())return;
        // Do not interrupt a player near a live hostile, even if their HUD is the only UI.
        nearby.Clear();
        world.GetEntitiesInBounds(typeof(EntityAlive),
            new Bounds(player.position,new Vector3(64,32,64)),nearby);
        bool unsafeToRead=false;
        foreach(var entity in nearby)
        {
            var alive=entity as EntityAlive;
            if(alive!=null && alive!=player && !alive.IsDead() &&
                (alive.EntityClass?.bIsEnemyEntity==true || alive.GetAttackTarget()==player))
            {unsafeToRead=true;break;}
        }
        nearby.Clear();
        if(unsafeToRead)return;
        if(XUiC_RebirthPurgeInformation.Open(ui.xui,request.Entry,request.Sprite))
            pending.Dequeue();
    }
}

[Preserve]
public sealed class XUiC_RebirthPurgeInformation : XUiController
{
    private RebirthJournalEntry entry;
    private string sprite;
    private XUiV_Label title,body;
    private XUiV_Sprite art;
    private XUiController close;
    public override void Init()
    {
        base.Init();
        title=GetChildById("purgeInformationTitle")?.ViewComponent as XUiV_Label;
        body=GetChildById("purgeInformationBody")?.ViewComponent as XUiV_Label;
        art=GetChildById("purgeInformationArt")?.ViewComponent as XUiV_Sprite;
        close=GetChildById("purgeInformationClose");
        if(close!=null)close.OnPress+=ClosePressed;
    }
    public override void Cleanup()
    {
        if(close!=null)close.OnPress-=ClosePressed;
        base.Cleanup();
    }
    public override void OnOpen()
    {
        base.OnOpen();
        if(title!=null)title.Text=entry?.Title??string.Empty;
        if(body!=null)body.Text=entry?.Body??string.Empty;
        if(art!=null)art.SpriteName=sprite??"ThePurgeTitle";
        GetChildByType<XUiC_RebirthReadableText>()?.ResetReadingPosition();
    }
    public override void OnClose()
    {
        if(entry?.Authored==true)RebirthJournalGuideService.DismissPurge(entry);
        entry=null;
        base.OnClose();
    }
    private void ClosePressed(XUiController sender,int button)
    {
        if(button!=0 && button!=-1)return;
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
        if(windowGroup?.isShowing==true && XUiUtils.HotkeysAllowedFor(viewComponent) &&
            xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased)
            ClosePressed(this,-1);
    }
    internal static bool Open(XUi ui,RebirthJournalEntry guide,string image)
    {
        var controller=ui?.GetChildByType<XUiC_RebirthPurgeInformation>();
        if(controller==null || controller.windowGroup==null)return false;
        controller.entry=guide;controller.sprite=image;
        ui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup,true);
        // WindowManager schedules opening; isShowing can remain false until its next update.
        // Accept the request now so manual rereading cannot leave a duplicate queued.
        return true;
    }
}
