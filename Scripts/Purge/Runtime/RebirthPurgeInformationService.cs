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
    }
    private static readonly Queue<Pending> pending=new Queue<Pending>();
    private static readonly List<Entity> nearby=new List<Entity>();
    private static float next;
    private static EntityPlayerLocal restoredPlayer;
    internal static void Reset(){pending.Clear();nearby.Clear();next=0;restoredPlayer=null;}
    internal static void Request(EntityPlayerLocal player,string id,string sprite,bool manual=false)
    {
        var entry=RebirthJournalGuideService.ObservePurgeGuide(player,id);
        if(entry==null || !manual && RebirthJournalGuideService.IsPurgeDismissed(entry))return;
        foreach(var request in pending)if(request.Player==player && request.Entry.Id==id)return;
        pending.Enqueue(new Pending{Player=player,Entry=entry,Sprite=sprite,Manual=manual});
    }
    internal static void Pulse()
    {
        if(!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.IsPurge)
        {if(pending.Count>0 || restoredPlayer!=null)Reset();return;}
        var currentPlayer=GameManager.Instance?.World?.GetPrimaryPlayer();
        if(pending.Count==0 && ReferenceEquals(restoredPlayer,currentPlayer))return;
        float now=Time.realtimeSinceStartup;
        if(now<next)return;
        next=now+.5f;
        if(currentPlayer!=null && !ReferenceEquals(restoredPlayer,currentPlayer))
        {
            List<RebirthJournalEntry> unread;
            if(RebirthJournalGuideService.TryGetUnlockedPurgeGuides(currentPlayer,out unread))
            {
                restoredPlayer=currentPlayer;
                foreach(var entry in unread)
                {
                    var topic=RebirthPurgeInformationTopics.Find(entry.Id);
                    if(topic==null)continue;
                    bool exists=false;
                    foreach(var queued in pending)if(queued.Player==currentPlayer && queued.Entry.Id==entry.Id){exists=true;break;}
                    if(!exists)pending.Enqueue(new Pending{Player=currentPlayer,Entry=entry,Sprite=topic.Sprite});
                }
            }
        }
        if(pending.Count==0)return;
        Pending request=pending.Peek();
        var player=request.Player;
        var world=GameManager.Instance?.World;
        if(player==null || !ReferenceEquals(player.world,world)){Reset();return;}
        if(!request.Manual && RebirthJournalGuideService.IsPurgeDismissed(request.Entry)){pending.Dequeue();return;}
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