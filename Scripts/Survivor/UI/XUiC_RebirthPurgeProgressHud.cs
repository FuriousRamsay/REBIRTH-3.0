using UnityEngine;
using UnityEngine.Scripting;

// Legacy lower-left Purge presentation; counters remain server-owned.
[Preserve]
public sealed class XUiC_RebirthPurgeProgressHud : XUiController
{
    private float elapsed;
    private bool closed;
    public override void Init()
    {
        base.Init();
        foreach(string id in new[]{"purgeProgressBackground","purgeBorderTop","purgeBorderBottom","purgeBorderLeft","purgeBorderRight"})
        {
            var texture=GetChildById(id)?.ViewComponent as XUiV_Texture;
            if(texture!=null)texture.Texture=Texture2D.whiteTexture;
        }
    }
    public override void OnOpen(){base.OnOpen();closed=false;elapsed=.5f;}
    public override void OnClose(){closed=true;RebirthHudTrackingLayoutMetrics.SetPurgeCardVisible(false);base.OnClose();}
    public override void Cleanup(){closed=true;RebirthHudTrackingLayoutMetrics.SetPurgeCardVisible(false);base.Cleanup();}
    private bool Visible()=>!closed&&RebirthPurgeHudProgress.Visible(xui?.playerUI?.entityPlayer)&&
        RebirthPurgeHudProgress.HasBiomeSummary(xui?.playerUI?.entityPlayer)&&
        !RebirthPersonalCraftingHudSuppressionInstaller.ShouldSuppress&&
        xui?.playerUI?.windowManager!=null&&!xui.playerUI.windowManager.IsModalWindowOpen();
    public override void Update(float dt)
    {
        base.Update(dt);
        elapsed+=dt;
        if(elapsed<.5f)return;
        elapsed=0;RebirthHudTrackingLayoutMetrics.SetPurgeCardVisible(Visible());IsDirty=true;RefreshBindings();
    }
    public override bool GetBindingValueInternal(ref string value,string bindingName)
    {
        var player=xui?.playerUI?.entityPlayer;
        switch(bindingName)
        {
            case "purgeprogressvisible":
                value=Visible().ToString();return true;
            case "purgebiome":
            case "purgepercentage":
            case "purgecounts":
                string biome,percentage,counts;
                RebirthPurgeHudProgress.BiomeSummary(player,out biome,out percentage,out counts);
                value=bindingName=="purgebiome"?biome:bindingName=="purgepercentage"?percentage:counts;return true;
            case "purgefill":value=RebirthPurgeHudProgress.BiomeFill(player);return true;
            case "purgecolor":value=RebirthPurgeHudProgress.BiomeTextColor(player);return true;
            case "purgesupply":
                value=player!=null&&player.Buffs.GetCustomVar("_rbPurgeSupplyKnown")==1
                    ?player.Buffs.GetCustomVar("_rbPurgeSupplyRemaining").ToString("0")+"/"+
                        player.Buffs.GetCustomVar("_rbPurgeSupplyTarget").ToString("0")
                    :"—";return true;
            case "purgekills":value=RebirthPurgeScanner.LifetimeText(player);return true;
        }
        return base.GetBindingValueInternal(ref value,bindingName);
    }
}
