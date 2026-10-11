using System;
using System.Xml.Linq;
using UnityEngine.Scripting;

/// <summary>Stable legacy reading-item identities routed to the shared Purge information window.</summary>
internal static class RebirthPurgeInformationTopics
{
    internal sealed class Topic
    {
        internal readonly string Id,Item,Sprite,TitleKey,BodyKey;
        internal Topic(string id,string item,string sprite,string title,string body)
        {Id=id;Item=item;Sprite=sprite;TitleKey=title;BodyKey=body;}
    }
    internal static readonly Topic[] All={
        new Topic("PurgeIntro","FuriousRamsayInfo_Purge1Intro","ThePurgeTitle","xuiRebirthPurgeIntroTitle","xuiRebirthPurgeIntroBody"),
        new Topic("PurgeSupplies","FuriousRamsayInfo_Purge2Supplies","SupplyDrop","rebirthJournalPurgeSuppliesTitle","rebirthJournalPurgeSuppliesBody"),
        new Topic("PurgeScanner","FuriousRamsayInfo_Purge2aThreatScanner","ThreatScanner","xuiRebirthPurgeScannerTitle","xuiRebirthPurgeScannerBody"),
        new Topic("PurgeSupplyUpdate","FuriousRamsayInfo_Purge3SuppliesUpdate","SupplyDropUpdate","xuiRebirthPurgeSupplyUpdateTitle","xuiRebirthPurgeSupplyUpdateBody"),
        new Topic("PurgeDiscovery","FuriousRamsayInfo_Purge4AutoDiscovery","AutoDiscovery","xuiRebirthPurgeDiscoveryHelpTitle","xuiRebirthPurgeDiscoveryHelpBody"),
        new Topic("PurgeDesert","FuriousRamsayInfo_Purge5BiomeUnlock_desert","purge_desert","xuiRebirthPurgeDesertTitle","xuiRebirthPurgeBiomeHelpBody"),
        new Topic("PurgeSnow","FuriousRamsayInfo_Purge5BiomeUnlock_snow","purge_snow","xuiRebirthPurgeSnowTitle","xuiRebirthPurgeBiomeHelpBody"),
        new Topic("PurgeWasteland","FuriousRamsayInfo_Purge5BiomeUnlock_wasteland","purge_wasteland","xuiRebirthPurgeWastelandTitle","xuiRebirthPurgeBiomeHelpBody"),
        new Topic("PurgeBurntForest","FuriousRamsayInfo_Purge5BiomeUnlock_burnt_forest","purge_burnt_forest","xuiRebirthPurgeBurntForestTitle","xuiRebirthPurgeBiomeHelpBody")
    };
    internal static Topic Find(string id)
    {
        foreach(var topic in All)
            if(string.Equals(topic.Id,id,StringComparison.Ordinal)||string.Equals(topic.Item,id,StringComparison.Ordinal))
                return topic;
        return null;
    }
    internal static void Request(EntityPlayerLocal player,string id,bool manual=false)
    {
        var topic=Find(id);
        if(topic!=null)RebirthPurgeInformationService.Request(player,topic.Id,topic.Sprite,manual);
    }
    internal static void RequestBiomeBriefing(EntityPlayerLocal player,string completedTierBiome)
    {
        // Original tier-five information sequence. These are field briefings, never entry gates.
        switch(completedTierBiome)
        {
            case "pine_forest":Request(player,"PurgeDesert");break;
            case "desert":Request(player,"PurgeSnow");break;
            case "snow":Request(player,"PurgeWasteland");break;
            case "wasteland":Request(player,"PurgeBurntForest");break;
        }
    }
}
[Preserve]
public sealed class MinEventActionRebirthPurgeInformation : MinEventActionBase
{
    private string topic;
    public override bool ParseXmlAttribute(XAttribute attribute)
    {
        if(attribute.Name.LocalName=="topic"){topic=attribute.Value;return true;}
        return base.ParseXmlAttribute(attribute);
    }
    public override void Execute(MinEventParams parameters)
    {
        if(parameters?.Self is EntityPlayerLocal player)
            RebirthPurgeInformationTopics.Request(player,topic,true);
    }
}