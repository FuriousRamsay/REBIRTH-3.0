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
        foreach(string id in new[]{"purgeProgressBackground","purgeBorderTop","purgeBorderBottom","purgeBorderLeft","purgeBorderRight",
            "stageRowBackground","stageBorderTop","stageBorderBottom","stageBorderLeft","stageBorderRight","stageDivider"})
        {
            var texture=GetChildById(id)?.ViewComponent as XUiV_Texture;
            if(texture!=null)texture.Texture=Texture2D.whiteTexture;
        }
    }
    public override void OnOpen(){base.OnOpen();closed=false;elapsed=.5f;}
    public override void OnClose(){closed=true;RebirthHudTrackingLayoutMetrics.SetLowerCardsVisible(false,false);base.OnClose();}
    public override void Cleanup(){closed=true;RebirthHudTrackingLayoutMetrics.SetLowerCardsVisible(false,false);base.Cleanup();}
    // Game stage and loot stage are shown whenever the HUD is (no window open), in every scenario.
    private bool StageRowVisible()=>!closed&&xui?.playerUI?.entityPlayer!=null&&
        !RebirthPersonalCraftingHudSuppressionInstaller.ShouldSuppress&&
        xui?.playerUI?.windowManager!=null&&!xui.playerUI.windowManager.IsModalWindowOpen();
    private bool Visible()=>!closed&&RebirthPurgeHudProgress.Visible(xui?.playerUI?.entityPlayer)&&
        RebirthPurgeHudProgress.HasBiomeSummary(xui?.playerUI?.entityPlayer)&&
        !RebirthPersonalCraftingHudSuppressionInstaller.ShouldSuppress&&
        xui?.playerUI?.windowManager!=null&&!xui.playerUI.windowManager.IsModalWindowOpen();
    public override void Update(float dt)
    {
        base.Update(dt);
        elapsed+=dt;
        if(elapsed<.5f)return;
        elapsed=0;TrackDayStart();RebirthHudTrackingLayoutMetrics.SetLowerCardsVisible(Visible(),StageRowVisible());IsDirty=true;RefreshBindings();
    }
    // Today's change: each stage's value at the start of the current game day, kept on the player so it survives reloads.
    private const string StageDayVar="$rbStageDay",GameStageStartVar="$rbGameStageDayStart",LootStageStartVar="$rbLootStageDayStart";
    private void TrackDayStart()
    {
        var player=xui?.playerUI?.entityPlayer;
        if(player?.Buffs==null||player.world==null)return;
        int day=GameUtils.WorldTimeToDays(player.world.worldTime);
        if((int)player.Buffs.GetCustomVar(StageDayVar)==day)return;
        player.Buffs.SetCustomVar(StageDayVar,day);
        player.Buffs.SetCustomVar(GameStageStartVar,player.gameStage);
        player.Buffs.SetCustomVar(LootStageStartVar,player.GetLootStage(0f,0f));
    }
    private static string Delta(int change)=>change>0?"[7fd17f]+"+change+"[-]":change<0?"[d67a7a]"+change+"[-]":string.Empty;

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
            case "stagerowvisible":value=StageRowVisible().ToString();return true;
            case "gamestage":value=player!=null?player.gameStage.ToString():"";return true;
            case "lootstage":value=player!=null?player.GetLootStage(0f,0f).ToString():"";return true;
            case "gamestagedelta":value=player!=null?Delta(player.gameStage-(int)player.Buffs.GetCustomVar(GameStageStartVar)):"";return true;
            case "lootstagedelta":value=player!=null?Delta(player.GetLootStage(0f,0f)-(int)player.Buffs.GetCustomVar(LootStageStartVar)):"";return true;
        }
        return base.GetBindingValueInternal(ref value,bindingName);
    }
}
