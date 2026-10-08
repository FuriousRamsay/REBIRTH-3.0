using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public sealed class RebirthProgressionExplorerReturnContext
{
    public readonly string CallerWindowGroupId;
    public readonly string ReturnToken;
    public readonly string ReturnLabel;
    public RebirthProgressionExplorerReturnContext(string callerWindowGroupId,string returnToken,string returnLabel)
    {
        CallerWindowGroupId=(callerWindowGroupId??string.Empty).Trim();ReturnToken=returnToken??string.Empty;ReturnLabel=returnLabel??string.Empty;
    }
    public bool HasCaller{get{return !string.IsNullOrEmpty(CallerWindowGroupId);}}
}

public sealed class RebirthProgressionExplorerLaunchRequest
{
    public readonly string FocusId;
    public readonly RebirthProgressionExplorerMode Mode;
    public readonly string LaunchReason;
    public readonly RebirthProgressionExplorerReturnContext ReturnContext;
    public readonly RebirthSurvivorCreationResult CreatorPreviewResult;
    public RebirthProgressionExplorerLaunchRequest(string focusId,RebirthProgressionExplorerMode mode,string launchReason=null,RebirthProgressionExplorerReturnContext returnContext=null,RebirthSurvivorCreationResult creatorPreviewResult=null)
    {
        FocusId=(focusId??string.Empty).Trim();Mode=mode;LaunchReason=launchReason??string.Empty;ReturnContext=returnContext;CreatorPreviewResult=creatorPreviewResult;
    }
}

/// <summary>
/// Local UI-only return handlers used by deep-link callers that own state beyond a window-group id.
/// Nothing here mutates Survivor progression or crosses the network.
/// </summary>
public static class RebirthProgressionExplorerReturnRegistry
{
    private const int MaxHandlers=128;
    private static readonly Dictionary<string,Action<XUi,string>> handlers=new Dictionary<string,Action<XUi,string>>(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<string> order=new Queue<string>();
    public static void Register(string token,Action<XUi,string> handler)
    {
        token=(token??string.Empty).Trim();if(token.Length==0||handler==null)return;
        if(!handlers.ContainsKey(token))order.Enqueue(token);handlers[token]=handler;
        while(handlers.Count>MaxHandlers&&order.Count>0){string stale=order.Dequeue();handlers.Remove(stale);}
    }
    public static void Unregister(string token){token=(token??string.Empty).Trim();if(token.Length>0)handlers.Remove(token);}
    public static bool TryHandle(XUi xui,RebirthProgressionExplorerReturnContext context)
    {
        if(context==null||string.IsNullOrEmpty(context.ReturnToken))return false;Action<XUi,string> handler;if(!handlers.TryGetValue(context.ReturnToken,out handler)||handler==null)return false;handlers.Remove(context.ReturnToken);handler(xui,context.ReturnToken);return true;
    }
}

public sealed class RebirthProgressionExplorerNavigationState
{
    private const int MaxHistoryEntries=128;
    private readonly List<string> history=new List<string>();
    private int index=-1;
    private string homeId=string.Empty;
    public string CurrentId{get{return index>=0&&index<history.Count?history[index]:homeId;}}
    public string HomeId{get{return homeId;}}
    public bool CanBack{get{return index>0;}}
    public bool CanForward{get{return index>=0&&index<history.Count-1;}}
    public int Count{get{return history.Count;}}

    public void Reset(string initialFocusId)
    {
        history.Clear();homeId=(initialFocusId??string.Empty).Trim();if(!string.IsNullOrEmpty(homeId)){history.Add(homeId);index=0;}else index=-1;
    }
    public bool Navigate(string id)
    {
        id=(id??string.Empty).Trim();if(string.IsNullOrEmpty(id))return false;if(string.Equals(CurrentId,id,StringComparison.OrdinalIgnoreCase))return false;
        if(index<history.Count-1)history.RemoveRange(index+1,history.Count-index-1);history.Add(id);index=history.Count-1;
        if(history.Count>MaxHistoryEntries){int remove=history.Count-MaxHistoryEntries;history.RemoveRange(0,remove);index-=remove;}
        return true;
    }
    public bool Back(){if(!CanBack)return false;index--;return true;}
    public bool Forward(){if(!CanForward)return false;index++;return true;}
    public bool Home(){if(string.IsNullOrEmpty(homeId))return false;return Navigate(homeId);}
    public string[] BreadcrumbIds(int maxEntries)
    {
        if(maxEntries<1)maxEntries=1;if(index<0)return new string[0];int start=Math.Max(0,index-maxEntries+1);List<string> result=new List<string>();for(int i=start;i<=index;i++)result.Add(history[i]);return result.ToArray();
    }
    public string DebugSummary(){return "home="+homeId+" current="+CurrentId+" index="+index+" count="+history.Count+" back="+CanBack+" forward="+CanForward;}
}
