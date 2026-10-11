using Newtonsoft.Json.Linq;

// Explicit presentation fixture. Does not enable Purge, award a milestone, or mark a real guide read.
internal static class RebirthGameBridgePurgeInformationPreview
{
    internal static void Apply(BridgeRequest req)
    {
        var world=GameManager.Instance?.World;
        var player=world?.GetPrimaryPlayer();
        if(req.Method!="POST" || world==null || world.IsRemote() || player==null ||
            GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest" ||
            req.QueryString("confirm")!="CodexTest")
        {req.Fail("Preview requires the local disposable CodexTest save and confirmation.",403);return;}
        string topic=req.QueryString("topic","discovery");
        var definition=RebirthPurgeInformationTopics.Find(topic=="discovery"?"PurgeDiscovery":topic=="supplies"?"PurgeSupplies":topic);
        if(definition==null)
        {req.Fail("Unknown Purge information topic.",400);return;}
        var ui=LocalPlayerUI.GetUIForPlayer(player);
        if(ui?.windowManager==null || ui.windowManager.IsInputActive() || ui.windowManager.IsModalWindowOpen())
        {req.Fail("Close other windows before opening the preview.",409);return;}
        var entry=new RebirthJournalEntry {
            Id="bridge-purge-preview",Authored=false,
            Title=Localization.Get(definition.TitleKey),
            Body=Localization.Get(definition.BodyKey)
        };
        XUiC_RebirthPurgeInformation.Open(ui.xui,entry,definition.Sprite);
        req.Complete(new JObject{["fixtureOnly"]=true,["topic"]=topic,["window"]="rebirthPurgeInformation",
            ["note"]="Inspect after the native window-open frame. This does not test reward delivery."});
    }
}