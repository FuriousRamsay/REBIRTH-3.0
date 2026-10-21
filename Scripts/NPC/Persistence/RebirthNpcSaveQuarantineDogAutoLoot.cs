using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthDogAutoLootBridge
{
    private sealed class PendingTransfer
    {
        internal Guid Operation;internal World World;internal EntityPlayerLocal Actor;internal XUi Ui;internal TEFeatureStorage Source;
        internal uint Epoch,Connection;internal ulong Generation;internal string Item;internal int Count;
    }
    private static ulong AutoLootGeneration(World world)=>world.IsRemote()?RebirthNpcRecoveryProjectionScope.Current:RebirthNpcRecoveryGeneration.RequireCurrent();
    private static bool CanAdmitAutoLoot(XUi ui)
    {
        var world=GameManager.Instance?.World;var actor=ui?.playerUI?.entityPlayer;
        if(!ThreadManager.IsMainThread()||world==null||actor==null||!ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        lock(Sync){if(Pending.Count>=1024)return false;foreach(var p in Pending.Values)if(p==null||!ReferenceEquals(p.World,world)||p.Generation!=AutoLootGeneration(world))return false;return true;}
    }
    private static PendingTransfer CapturePendingAutoLoot(Guid operation,XUi ui,EntityPlayerLocal actor,TEFeatureStorage source,ItemStack input)
    {
        var world=actor.world;
        return new PendingTransfer{Operation=operation,World=world,Actor=actor,Ui=ui,Source=source,Epoch=world.IsRemote()?RebirthNpcNetworkEpoch.ClientEpoch:RebirthNpcNetworkEpoch.GetServerEpoch(),Connection=world.IsRemote()?RebirthNpcNetworkEpoch.ClientConnectionEpoch:0,Generation=AutoLootGeneration(world),Item=RebirthNativeItemCodec.Encode(input.itemValue),Count=input.count};
    }
    private static bool PendingAutoLootCurrent(PendingTransfer p)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||p==null||world==null||!ReferenceEquals(p.World,world)||!ReferenceEquals(p.Actor?.world,world)||!ReferenceEquals(world.GetEntity(p.Actor.entityId),p.Actor)||!ReferenceEquals(LocalPlayerUI.GetUIForPrimaryPlayer()?.xui,p.Ui)||!ReferenceEquals(p.Ui?.LootContainer,p.Source)||p.Generation!=AutoLootGeneration(world))return false;
        if(!ReferenceEquals(world.GetTileEntity(p.Source.ToWorldPos())?.GetSelfOrFeature<TEFeatureStorage>(),p.Source))return false;
        return p.Epoch==(world.IsRemote()?RebirthNpcNetworkEpoch.ClientEpoch:RebirthNpcNetworkEpoch.GetServerEpoch())&&p.Connection==(world.IsRemote()?RebirthNpcNetworkEpoch.ClientConnectionEpoch:0);
    }
    internal static bool CanRetireEmptyAutoLoot
    {get{lock(Sync)return ThreadManager.IsMainThread()&&Pending.Count==0&&!replayingVanilla;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareAutoLootQuarantine(World originalWorld,uint epoch,uint connection)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||originalWorld==null||replayingVanilla)throw new InvalidOperationException("Auto-loot original callback active");
            var records=Pending;var next=new Dictionary<PendingKey,PendingTransfer>();var doc=AutoLootImage(originalWorld,epoch,connection);
            return new RebirthNpcSaveQuarantineDomain("dog-auto-loot-callbacks",doc,
                ()=>{lock(Sync)return !replayingVanilla&&ReferenceEquals(Pending,records)&&AutoLootImage(originalWorld,epoch,connection).OuterXml==doc.OuterXml;},
                ()=>{lock(Sync)Pending=next;},
                ()=>{lock(Sync)Pending=records;});
        }
    }
    private static XmlDocument AutoLootImage(World world,uint epoch,uint connection)
    {
        if(Pending.Count>1024)throw new InvalidOperationException("Auto-loot callback retention bound exceeded");
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("autoLootCallbacks");doc.AppendChild(root);
        long characters=0;var rows=new List<KeyValuePair<PendingKey,PendingTransfer>>(Pending);rows.Sort((a,b)=>string.CompareOrdinal(a.Value?.Operation.ToString(),b.Value?.Operation.ToString()));
        foreach(var pair in rows)
        {
            var p=pair.Value;
            if(p==null||p.Operation==Guid.Empty||!ReferenceEquals(p.World,world)||!ReferenceEquals(p.Actor?.world,world)||p.Ui==null||p.Source==null||p.Epoch!=epoch||p.Connection!=connection||p.Count<=0||string.IsNullOrEmpty(p.Item)||p.Item.Length>1024*1024)throw new InvalidOperationException("Auto-loot original callback identity unknown");
            characters+=p.Item.Length;if(characters>4L*1024*1024)throw new InvalidOperationException("Auto-loot original callback evidence exceeds bounded preparation");
            var row=doc.CreateElement("pending");root.AppendChild(row);row.SetAttribute("operation",p.Operation.ToString("N"));row.SetAttribute("player",p.Actor.entityId.ToString(CultureInfo.InvariantCulture));row.SetAttribute("position",pair.Key.Position.ToString());row.SetAttribute("slot",pair.Key.Slot.ToString(CultureInfo.InvariantCulture));row.SetAttribute("item",p.Item);row.SetAttribute("count",p.Count.ToString(CultureInfo.InvariantCulture));row.SetAttribute("epoch",p.Epoch.ToString(CultureInfo.InvariantCulture));row.SetAttribute("connection",p.Connection.ToString(CultureInfo.InvariantCulture));row.SetAttribute("generation",p.Generation.ToString(CultureInfo.InvariantCulture));
        }
        return doc;
    }
}
