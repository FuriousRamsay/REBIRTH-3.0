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
        internal float Due;
    }
    private static readonly Queue<Notice> pending = new Queue<Notice>();
    private static float nextDisplay;
    internal static void Reset() { pending.Clear(); nextDisplay=0; }

    internal static void Send(RebirthPurgeMilestoneStore.Receipt receipt)
    {
        if(receipt==null || !RebirthPurgeReleasePolicy.Enabled ||
            !RebirthSandboxOptionManager.Current.IsPurge)return;
        World world=GameManager.Instance?.World;
        if(world==null || world.IsRemote())return;
        Receive(world,receipt.Biome,receipt.Tier);
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(connection!=null && connection.IsServer)
            connection.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPurgeMilestoneNotice>()
                .Setup(receipt.Biome,receipt.Tier));
    }
    internal static void Receive(World world,string biome,int tier)
    {
        if(world==null || !ReferenceEquals(world,GameManager.Instance?.World) ||
            !RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.IsPurge ||
            string.IsNullOrEmpty(biome) || biome.Length>128 || tier < -1 || tier>255 ||
            world.GetPrimaryPlayer()==null)return;
        pending.Enqueue(new Notice{World=world,Biome=biome,Tier=tier,Due=Time.realtimeSinceStartup+8f});
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
        if(now<notice.Due || now<nextDisplay)return;
        var player=world?.GetPrimaryPlayer();
        if(player==null || player.IsDead())return;
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
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthPurgeMilestoneNotice Setup(string region,int difficulty)
    { biome=region;tier=difficulty;return this; }
    public override void read(PooledBinaryReader reader)
    { biome=reader.ReadString();tier=reader.ReadInt32(); }
    public override void write(PooledBinaryWriter writer)
    { base.write(writer);writer.Write(biome);writer.Write(tier); }
    public int GetLength()=>8+(biome?.Length??0)*3;
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world!=null && world.IsRemote() && ReferenceEquals(world,GameManager.Instance?.World))
            RebirthPurgeMilestoneNotification.Receive(world,biome,tier);
    }
}