using System;
using Newtonsoft.Json.Linq;

// Fixture preparation only. Normal station actions remain real input driven.
internal static class RebirthGameBridgeStationToolSetup
{
    internal static void Apply(BridgeRequest req)
    {
        var world = GameManager.Instance?.World;
        if (req.Method != "POST" || world == null || world.IsRemote() ||
            GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest" || req.QueryString("confirm") != "CodexTest")
        { req.Fail("Tool fixture setup requires the local disposable CodexTest save and confirmation.",403); return; }
        int x,y,z,slot;
        if (!int.TryParse(req.QueryString("x"),out x) || !int.TryParse(req.QueryString("y"),out y) ||
            !int.TryParse(req.QueryString("z"),out z) || !int.TryParse(req.QueryString("slot"),out slot))
        { req.Fail("Explicit integer x,y,z and tool slot required.",400); return; }
        float remaining = req.QueryFloat("durability",float.NaN);
        if (float.IsNaN(remaining) || float.IsInfinity(remaining) || remaining < 0f || remaining > 1f)
        { req.Fail("durability must be a finite fraction from0 to1.",400); return; }
        var tile=world.GetTileEntity(new Vector3i(x,y,z)) as TileEntityWorkstation;
        if(tile==null || tile.bUserAccessing)
        { req.Fail("A loaded, closed workstation is required.",409); return; }
        foreach(var job in tile.Queue)
            if(job?.Recipe!=null && job.Multiplier>0)
            { req.Fail("Empty the queue before preparing tool durability.",409); return; }
        var tools=tile.Tools;
        if(tools==null || slot<0 || slot>=tools.Length || tools[slot]==null || tools[slot].IsEmpty())
        { req.Fail("Occupied tool slot required.",409); return; }
        var item=tools[slot].itemValue;
        string expected=req.QueryString("item");
        if(string.IsNullOrEmpty(expected) || item.ItemClass.GetItemName()!=expected || item.MaxUseTimes<=0)
        { req.Fail("Exact matching degradable tool item required.",409); return; }
        float before=item.UseTimes;
        var candidate=ItemStack.Clone(tools);
        candidate[slot].itemValue.UseTimes=item.MaxUseTimes*(1f-remaining);
        tile.Tools=candidate;
        req.Complete(new JObject{["fixtureOnly"]=true,["item"]=expected,["slot"]=slot,
            ["beforeUseTimes"]=before,["afterUseTimes"]=tile.Tools[slot].itemValue.UseTimes,["maxUseTimes"]=item.MaxUseTimes});
    }
}