using System.Runtime.CompilerServices;

// Presentation state only. The native window group still owns every crafting transaction.
public sealed class RebirthCraftingPresentation
{
    private static readonly ConditionalWeakTable<XUiC_CraftingWindowGroup,RebirthCraftingPresentation> hosts = new ConditionalWeakTable<XUiC_CraftingWindowGroup,RebirthCraftingPresentation>();
    public readonly XUiC_CraftingWindowGroup Controller;
    public XUiC_RebirthPersonalCrafting Personal => Controller as XUiC_RebirthPersonalCrafting;
    private readonly RebirthPersonalCraftingState stationState;
    private readonly RebirthPersonalCraftingCoordinator stationCoordinator;
    private long epoch;
    private readonly System.Collections.Generic.Dictionary<string,XUiController> controls=new System.Collections.Generic.Dictionary<string,XUiController>();
    private readonly System.Collections.Generic.Dictionary<System.Type,XUiController> typedControls=new System.Collections.Generic.Dictionary<System.Type,XUiController>();
    private RebirthCraftingPresentation(XUiC_CraftingWindowGroup controller)
    {
        Controller=controller;
        if(Personal==null){stationState=new RebirthPersonalCraftingState();stationCoordinator=new RebirthPersonalCraftingCoordinator(this,stationState);}
    }
    public static RebirthCraftingPresentation For(XUiC_CraftingWindowGroup controller) => controller==null?null:hosts.GetValue(controller,c=>new RebirthCraftingPresentation(c));
    public static RebirthCraftingPresentation Resolve(XUiController child)
    {
        var personal=child?.GetParentByType<XUiC_RebirthPersonalCrafting>();
        if(personal!=null)return For(personal);
        var station=child?.GetParentByType<XUiC_RebirthStationWorkspace>();
        if(station!=null)return For(station);
        var cooking=child?.GetParentByType<XUiC_RebirthCookingStation>();
        return cooking?.UsesSharedMillingPresentation==true?For(cooking):null;
    }
    public static implicit operator RebirthCraftingPresentation(XUiC_RebirthPersonalCrafting controller)=>For(controller);
    public static implicit operator XUiC_CraftingWindowGroup(RebirthCraftingPresentation host)=>host?.Controller;
    public XUi xui=>Controller.xui;
    public RebirthPersonalCraftingState State=>Personal?.State??stationState;
    public RebirthPersonalCraftingCoordinator Coordinator=>Personal?.Coordinator??stationCoordinator;
    public bool IsInventoryOnlyMode=>Personal?.IsInventoryOnlyMode??false;
    public long CraftIntentEpoch=>Personal?.CraftIntentEpoch??epoch;
    public string Workstation=>Personal!=null?string.Empty:Controller.Workstation;
    public T GetChildByType<T>() where T:XUiController
    {if(typedControls.TryGetValue(typeof(T),out var cached))return (T)cached;
     var found=Controller.GetChildByType<T>();if(found!=null)typedControls[typeof(T)]=found;return found;}
    public XUiController GetChildById(string id)
    {if(controls.TryGetValue(id,out var cached))return cached;
     var found=Controller.GetChildById(id);if(found!=null)controls[id]=found;return found;}
    public void AdvanceCraftIntentEpoch(){if(Personal!=null)Personal.AdvanceCraftIntentEpoch();else unchecked{++epoch;}}
    public void RequestLayoutAudit()=>Personal?.RequestLayoutAudit();
    public bool CraftingRequirementsValid(Recipe recipe)=>Controller.CraftingRequirementsValid(recipe);
}
