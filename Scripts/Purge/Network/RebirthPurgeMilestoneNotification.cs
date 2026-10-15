using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

// Live reward transitions only. Opening an existing receipt store is silent.
internal static class RebirthPurgeMilestoneNotification
{
    private sealed class Notice
    {
        internal World World;
        internal string Biome;
        internal int Tier;
        internal bool ReadingOnly;
        internal bool Primed;
        internal float NextPrime;
        internal float Due;
    }
    private static readonly Queue<Notice> pending = new Queue<Notice>();
    private static float nextDisplay, nextPrimeSweep;
    internal static void Reset() { pending.Clear(); nextDisplay=nextPrimeSweep=0; }

    internal static void Send(RebirthPurgeMilestoneStore.Receipt receipt)
    {
        if(receipt==null || !RebirthPurgeReleasePolicy.Enabled ||
            !RebirthSandboxOptionManager.Current.IsPurge)return;
        World world=GameManager.Instance?.World;
        if(world==null || world.IsRemote())return;
        // A dedicated server has no local journal/display recipient. Remote
        // notices still use the normal broadcast below.
        if(!GameManager.IsDedicatedServer)Receive(world,receipt.Biome,receipt.Tier);
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(connection!=null && connection.IsServer)
            connection.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPurgeMilestoneNotice>()
                .Setup(receipt.Biome,receipt.Tier));
    }
    internal static void Receive(World world,string biome,int tier,bool readingOnly=false)
    {
        if(world==null || !ReferenceEquals(world,GameManager.Instance?.World) ||
            !RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.IsPurge ||
            string.IsNullOrEmpty(biome) || biome.Length>128 || tier < -1 || tier>255)return;
        // Join packets can arrive before the local player exists. Keep them in the
        // existing world-bound queue until spawn, rather than losing earned notes.
        var notice=new Notice{World=world,Biome=biome,Tier=tier,ReadingOnly=readingOnly,
            Due=Time.realtimeSinceStartup+(readingOnly?0f:8f)};
        pending.Enqueue(notice);
        // Persist each live topic when received, including topics queued behind
        // another delayed notice. Head-of-queue timing must not lose later help.
        if(!readingOnly)Prime(notice,world.GetPrimaryPlayer(),Time.realtimeSinceStartup);
    }
    private static RebirthPurgeInformationTopics.Topic TeachingTopic(string biome,int tier)
    {
        string id=tier==0?"PurgeDiscovery":null;
        if(tier==5)switch(biome)
        {
            case "pine_forest":id="PurgeDesert";break;
            case "desert":id="PurgeSnow";break;
            case "snow":id="PurgeWasteland";break;
            case "wasteland":id="PurgeBurntForest";break;
        }
        return RebirthPurgeInformationTopics.Find(id);
    }
    private static bool Prime(Notice notice,EntityPlayerLocal player,float now)
    {
        if(notice.Primed)return true;
        if(player==null||!player.IsSpawned()||player.IsDead()||now<notice.NextPrime)return false;
        notice.NextPrime=now+.5f;
        var topic=TeachingTopic(notice.Biome,notice.Tier);
        if(topic!=null)
        {
            if(RebirthJournalGuideService.ObservePurgeGuide(player,topic.Id)==null)return false;
            RebirthPurgeInformationTopics.UnlockReadingRecipe(player,topic);
            RebirthPurgeInformationService.Request(player,topic.Id,topic.Sprite,notBefore:notice.Due);
        }
        notice.Primed=true;return true;
    }
    internal static bool RestoreReading(EntityPlayerLocal player,string biome,int tier)
    {
        var topic=TeachingTopic(biome,tier);
        if(topic==null)return true;
        List<RebirthJournalEntry> unlocked;
        if(!RebirthJournalGuideService.TryGetUnlockedPurgeGuides(player,out unlocked,true))return false;
        bool existed=unlocked.Exists(entry=>entry.Id==topic.Id);
        var guide=RebirthJournalGuideService.ObservePurgeGuide(player,topic.Id);
        if(guide==null)return false;
        // Only newly restored reading material is silent. Existing pending live
        // teaching must remain eligible for recovery after a reload.
        if(!existed||RebirthJournalGuideService.IsPurgeReadingOnly(guide))
            RebirthPurgeInformationService.RestoreReadingOnly(player,topic.Id);
        RebirthPurgeInformationTopics.UnlockReadingRecipe(player,topic);
        return true;
    }
    internal static void Pulse()
    {
        if(!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.IsPurge)
        { if(pending.Count>0)Reset();return; }
        if(pending.Count==0)return;
        Notice notice=pending.Peek();
        World world=GameManager.Instance?.World;
        if(!ReferenceEquals(world,notice.World)){Reset();return;}
        float now=Time.realtimeSinceStartup;
        if(notice.ReadingOnly&&now<notice.Due)return;
        var player=world?.GetPrimaryPlayer();
        if(player==null || !player.IsSpawned() || player.IsDead())return;
        // A live packet may arrive before the player/Journal exists. Persist all
        // waiting teaching topics once ready, including notices behind the head,
        // so quitting during another notice's delay cannot lose their teaching.
        if(now>=nextPrimeSweep)
        {
            nextPrimeSweep=now+.5f;
            foreach(var waiting in pending)
                if(!waiting.ReadingOnly&&ReferenceEquals(waiting.World,world))Prime(waiting,player,now);
        }
        if(!notice.ReadingOnly&&!Prime(notice,player,now))return;
        if(!notice.ReadingOnly&&(now<notice.Due||now<nextDisplay))return;
        if(notice.ReadingOnly)
        {
            if(!RestoreReading(player,notice.Biome,notice.Tier))
            {
                // Journal initialization can lag behind spawn; retry at the same
                // bounded cadence as information delivery, rather than every frame.
                notice.Due=now+0.5f;
                return;
            }
            pending.Dequeue();
            return;
        }
        string region=Localization.Get("biome_"+notice.Biome);
        string message=notice.Tier<0
            ? string.Format(Localization.Get("xuiRebirthPurgeBiomeCompleted"),region)
            : string.Format(Localization.Get("xuiRebirthPurgeTierDiscovered"),notice.Tier,region);
        GameManager.ShowTooltip(player,message);
        Audio.Manager.PlayInsidePlayerHead("purge_unlock");
        if(notice.Tier==0)RebirthPurgeInformationTopics.Request(player,"PurgeDiscovery");
        if(notice.Tier==5)RebirthPurgeInformationTopics.RequestBiomeBriefing(player,notice.Biome);
        pending.Dequeue();
        nextDisplay=now+8f;
    }
}
[Preserve]
public sealed class NetPackageRebirthPurgeMilestoneNotice : NetPackage
{
    private string biome;
    private int tier;
    private bool readingOnly;
    private Guid worldId,session;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthPurgeMilestoneNotice Setup(string region,int difficulty,bool restoreReading=false)
    { biome=region;tier=difficulty;readingOnly=restoreReading;worldId=RebirthPoiMapSync.LocalWorld;session=RebirthPoiMapSync.LocalSession;return this; }
    public override void read(PooledBinaryReader reader)
    { biome=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,128);tier=reader.ReadInt32();readingOnly=reader.ReadBoolean();worldId=new Guid(reader.ReadBytes(16));session=new Guid(reader.ReadBytes(16)); }
    public override void write(PooledBinaryWriter writer)
    { base.write(writer);RebirthSurvivorNetworkCodec.WriteString(writer,biome,128);writer.Write(tier);writer.Write(readingOnly);writer.Write(worldId.ToByteArray());writer.Write(session.ToByteArray()); }
    public int GetLength()=>41+(biome?.Length??0)*3;
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        // Copy pooled-package fields before the handshake may defer delivery.
        string region=biome;int difficulty=tier;bool restore=readingOnly;
        RebirthPurgeNoticeDelivery.Receive(world,worldId,session,
            ()=>RebirthPurgeMilestoneNotification.Receive(world,region,difficulty,restore));
    }
}
