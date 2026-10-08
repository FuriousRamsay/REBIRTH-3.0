using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

internal static class RebirthNpcWorldIdentityLoad
{
    internal static bool TryRead(XmlElement root,out Dictionary<RebirthNpcStableId,RebirthNpcWorldIdentityRecord> identities,
        out Dictionary<string,RebirthNpcPendingSpawn> pending,out HashSet<string> replays)
    {return TryRead(root,out identities,out pending,out replays,out _);}
    internal static bool TryRead(XmlElement root,out Dictionary<RebirthNpcStableId,RebirthNpcWorldIdentityRecord> identities,
        out Dictionary<string,RebirthNpcPendingSpawn> pending,out HashSet<string> replays,
        out Dictionary<string,RebirthNpcSpawnCompletion> completions)
    {
        identities=null;pending=null;replays=null;completions=null;        if(root==null||root.Name!="rebirthNpcWorldIntegration"||root.Attributes.Count!=1||(root.GetAttribute("version")!="1"&&root.GetAttribute("version")!="2")||root.NamespaceURI.Length!=0)return false;
        if(!RebirthNpcPendingSpawnPersistence.TryRead(XElement.Parse(root.OuterXml),out var parsedPending)||
            !RebirthNpcSpawnReplayCodec.TryRead(root,out var parsedReplays)||
            !RebirthNpcSpawnCompletion.TryReadSection(XElement.Parse(root.OuterXml),root.GetAttribute("version")=="2",out var parsedCompletions)||
            root.GetAttribute("version")=="1"&&parsedCompletions.Count!=0)return false;
        // A request cannot simultaneously be pending and acknowledged complete.
        // Reject the whole snapshot before any cache publication; never guess
        // which side of an uncertain terminal write should win.
        foreach(var request in parsedPending.Values)
            if(parsedReplays.Contains(request.ReplayKey))return false;
        var parsed=new Dictionary<RebirthNpcStableId,RebirthNpcWorldIdentityRecord>();
        var ambient=new HashSet<int>();
        foreach(XmlNode node in root.ChildNodes)
        {
            if(node.NodeType==XmlNodeType.Whitespace||node.NodeType==XmlNodeType.SignificantWhitespace||
                node.NodeType==XmlNodeType.Text&&string.IsNullOrWhiteSpace(node.Value))continue;
            if(node.NodeType!=XmlNodeType.Element)return false;
            if(node.Name=="pendingSpawns"||node.Name=="replays"||node.Name=="completedSpawns"&&root.GetAttribute("version")=="2")continue;
            if(node.Name!="identity"||node.Attributes.Count!=5||node.HasChildNodes||parsed.Count>=65536||
                !RebirthNpcStableId.TryParse(node.Attributes["stableId"]?.Value,out var stable)||
                stable.ToString()!=node.Attributes["stableId"]?.Value||parsed.ContainsKey(stable)||
                !int.TryParse(node.Attributes["ambientEntityId"]?.Value,NumberStyles.Integer,CultureInfo.InvariantCulture,out int entity)||entity<0||
                entity>0&&!ambient.Add(entity)||!Text(node.Attributes["profileId"]?.Value,128)||!Text(node.Attributes["displayName"]?.Value,256)||
                !long.TryParse(node.Attributes["promotedUtcTicks"]?.Value,NumberStyles.Integer,CultureInfo.InvariantCulture,out long ticks)||
                ticks<=0||ticks>DateTime.MaxValue.Ticks)return false;
            parsed.Add(stable,new RebirthNpcWorldIdentityRecord{StableId=stable,AmbientEntityId=entity,
                ProfileId=node.Attributes["profileId"].Value,DisplayName=node.Attributes["displayName"].Value,PromotedUtcTicks=ticks,Persistent=true});
        }
        foreach(var completion in parsedCompletions.Values)
            if(!parsedReplays.Contains(completion.ReplayKey)||parsedPending.ContainsKey(completion.ReplayKey)||
                !parsed.TryGetValue(completion.Stable,out var identity)||identity.ProfileId!=completion.Profile)return false;
        identities=parsed;pending=parsedPending;replays=parsedReplays;completions=parsedCompletions;return true;
    }
    private static bool Text(string value,int limit)
    {
        if(string.IsNullOrWhiteSpace(value)||value.Length>limit||value!=value.Trim())return false;
        foreach(char ch in value)if(char.IsControl(ch))return false;
        return true;
    }
}