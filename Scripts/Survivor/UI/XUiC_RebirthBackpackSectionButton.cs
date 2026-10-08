using UnityEngine.Scripting;

// The same entry point is used by every backpack/container lock toolbar.
[Preserve]
public sealed class XUiC_RebirthBackpackSectionButton : XUiController
{
    private string target;
    public override bool ParseAttribute(string name,string value)
    {
        if(name=="section_window"){target=value;return true;}
        return base.ParseAttribute(name,value);
    }
    public override void Init(){OnPress-=PressedSection;base.Init();OnPress+=PressedSection;}
    public override void Cleanup(){OnPress-=PressedSection;base.Cleanup();}
    private bool CanOpen()
    {
        var player=xui?.playerUI?.entityPlayer;

        var manager=xui?.playerUI?.windowManager;
        var world=player?.world;
        return manager!=null&&world!=null&&GameManager.Instance!=null
            &&ReferenceEquals(world,GameManager.Instance.World)&&ReferenceEquals(player,world.GetPrimaryPlayer())
            &&ReferenceEquals(player,world.GetEntity(player.entityId))&&ReferenceEquals(player.PlayerUI?.xui,xui)
            &&ReferenceEquals(player.PlayerUI,xui?.playerUI)&&player.IsSpawned()
            &&!player.IsDead()&&RebirthSurvivorMode.IsEnabledForCurrentWorld()
            &&RebirthBackpackLibraryPolicy.CapacityForBackpack(RebirthSurvivorClientState.GetProjectedGearItem(player,RebirthSurvivorGearService.BackpackSlotId))>0
            &&!(RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player))
            &&(target=="rebirthBackpackLibrary"||target=="rebirthBackpackSellStash")
            &&manager.GetWindow(target)!=null;
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        if(ViewComponent!=null)
        {
            var player=xui?.playerUI?.entityPlayer;
            bool equipped=player!=null&&RebirthBackpackLibraryPolicy.CapacityForBackpack(RebirthSurvivorClientState.GetProjectedGearItem(player,RebirthSurvivorGearService.BackpackSlotId))>0;
            ViewComponent.IsVisible=equipped;
            ViewComponent.Enabled=equipped&&CanOpen();
        }
    }
    private void PressedSection(XUiController sender,int button)
    {
        if((button!=0&&button!=-1)||!CanOpen()||RebirthConsoleInputGuardRuntime.BlocksGameplayInput())return;
        if(xui.DragAndDropWindow?.CurrentStack==null||!xui.DragAndDropWindow.CurrentStack.IsEmpty()||xui.IsUsingItemActionEntryUse)return;
        var source=xui;
        var playerUI=source?.playerUI;
        var player=playerUI?.entityPlayer;
        var manager=playerUI?.windowManager;
        var game=GameManager.Instance;
        var world=player?.world;
        var worldState=world?.worldState;
        var worldGuid=worldState?.Guid;
        if(!CanOpen()||!ReferenceEquals(source,xui)||!ReferenceEquals(playerUI,xui?.playerUI)
            ||!ReferenceEquals(player,playerUI?.entityPlayer)||!ReferenceEquals(manager,playerUI?.windowManager)
            ||game==null||world==null||!ReferenceEquals(game,GameManager.Instance)
            ||!ReferenceEquals(world,player?.world)||!ReferenceEquals(world,game.World)
            ||!ReferenceEquals(player,world.GetPrimaryPlayer()))return;
        if(RebirthConsoleInputGuardRuntime.BlocksGameplayInput()||source.IsUsingItemActionEntryUse
            ||source.DragAndDropWindow?.CurrentStack==null||!source.DragAndDropWindow.CurrentStack.IsEmpty())return;
        if(!ReferenceEquals(source,xui)||!ReferenceEquals(playerUI,source?.playerUI)
            ||!ReferenceEquals(player,playerUI?.entityPlayer)||!ReferenceEquals(manager,playerUI?.windowManager)
            ||!ReferenceEquals(game,GameManager.Instance)||!ReferenceEquals(world,game?.World)
            ||worldState==null||string.IsNullOrEmpty(worldGuid)||!ReferenceEquals(worldState,world?.worldState)
            ||!string.Equals(worldGuid,worldState.Guid,System.StringComparison.Ordinal)
            ||!ReferenceEquals(world,player?.world)||!ReferenceEquals(player,world?.GetPrimaryPlayer())
            ||!ReferenceEquals(player,world?.GetEntity(player.entityId))||!ReferenceEquals(playerUI,player?.PlayerUI)
            ||!ReferenceEquals(source,player?.PlayerUI?.xui)||!player.IsSpawned()||player.IsDead())return;
        manager.GetWindow(target).openWindowOnEsc=windowGroup?.Id??string.Empty; manager.Open(target,true);
    }
}