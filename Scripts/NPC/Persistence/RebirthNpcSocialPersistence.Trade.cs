using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

public static partial class RebirthNpcSocialPersistenceStore
{
    private const int MaxTradeCompletions=65536;
    private static Dictionary<Guid,string> tradeCompletions=new Dictionary<Guid,string>();
    internal static bool TrySaveTradeCompletion(Guid eventId,string identity,Func<bool> publish)
    {
        if(eventId==Guid.Empty||!ValidTradeCompletion(identity)||publish==null||!ThreadManager.IsMainThread()||!HasAuthoritativeWorld())return false;
        EnsureLoaded();
        lock(Sync)
        {
            if(!loaded||flushing||!ReferenceEquals(boundWorld,GameManager.Instance?.World)||boundPath!=PathName)return false;
            if(tradeCompletions.TryGetValue(eventId,out var retained))
            {if(retained!=identity)return false;}
            else
            {
                // Never evict completed identities: an old ACK must not turn
                // into another social effect after the general replay window.
                if(tradeCompletions.Count>=MaxTradeCompletions||!publish())return false;
                var next=new Dictionary<Guid,string>(tradeCompletions);next.Add(eventId,identity);tradeCompletions=next;
                ObserveOwnedMutation();
            }
            return TryFlush()&&IsTradeEventSaved(eventId);
        }
    }
    private static Dictionary<Guid,string> ReadTradeCompletions(XmlDocument document)
    {
        var result=new Dictionary<Guid,string>();
        foreach(XmlElement row in document.SelectNodes("/rebirthNpcSocialState/tradeCompletions/event"))
        {
            if(!Guid.TryParseExact(row.GetAttribute("id"),"N",out var id)||id==Guid.Empty||result.Count>=MaxTradeCompletions||
                !ValidTradeCompletion(row.GetAttribute("identity"))||result.ContainsKey(id))throw new InvalidOperationException("Invalid trade social completion");
            result.Add(id,row.GetAttribute("identity"));
        }
        return result;
    }
    private static bool ValidTradeCompletion(string identity)
    {
        var parts=(identity??string.Empty).Split(':');
        return parts.Length==3&&Guid.TryParseExact(parts[0],"N",out var operation)&&operation!=Guid.Empty&&
            parts.Skip(1).All(value=>value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f'));
    }
    // A successful mutation alone is not a durable completion witness. Require
    // the actual current world's atomic store and an independent saved readback.
    internal static bool IsTradeEventSaved(Guid eventId)
    {
        if(eventId==Guid.Empty||!ThreadManager.IsMainThread()||!HasAuthoritativeWorld())return false;
        lock(Sync)
        {
            return loaded&&!flushing&&ReferenceEquals(boundWorld,GameManager.Instance?.World)&&boundPath==PathName&&
                tradeCompletions.ContainsKey(eventId)&&CanReset;
        }
    }
    internal static bool IsTradeCompletionSaved(Guid eventId,string identity)
    {
        if(!ValidTradeCompletion(identity)||!ThreadManager.IsMainThread())return false;
        lock(Sync)return tradeCompletions.TryGetValue(eventId,out var saved)&&saved==identity&&IsTradeEventSaved(eventId);
    }
}
