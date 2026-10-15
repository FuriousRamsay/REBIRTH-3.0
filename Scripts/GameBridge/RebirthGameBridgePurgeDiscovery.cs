using Newtonsoft.Json.Linq;

// Maintenance diagnostics, not a scenario or evidence of gameplay success.
internal static class RebirthGameBridgePurgeDiscovery
{
    internal static void Apply(BridgeRequest req)
    {
        var world=GameManager.Instance?.World;
        var player=world?.GetPrimaryPlayer();
        if(world==null||world.IsRemote()||player==null||
            GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")
        {req.Fail("Discovery diagnostics require the local disposable CodexTest save.",403);return;}
        string action=req.QueryString("action","status");
        if(action=="status")
        {Reply(req);return;}
        if(req.Method!="POST"||req.QueryString("confirm")!="CodexTest")
        {req.Fail("Mutation requires POST and disposable-save confirmation.",403);return;}
        if(action=="cancel")
        {if(!RebirthPurgeDiscoveryMaintenance.Cancel(player)){req.Fail("Another player owns the running maintenance job.",403);return;}Reply(req);return;}
        if(action!="preview"&&action!="replay")
        {req.Fail("Expected status, preview, replay or cancel.",400);return;}
        string reason;
        if(!RebirthPurgeDiscoveryMaintenance.Start(player,action=="preview",req.QueryInt("range",15000),out reason))
        {req.Fail(reason,409);return;}
        Reply(req);
    }
    private static void Reply(BridgeRequest req)
    {
        req.Complete(new JObject{["running"]=RebirthPurgeDiscoveryMaintenance.Running,
            ["status"]=RebirthPurgeDiscoveryMaintenance.Status,["maintenanceOnly"]=true});
    }
}
