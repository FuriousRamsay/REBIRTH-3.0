using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthVehicleHudStatBar : XUiC_HUDStatBar
{
    private float lastAmount=-1f, lastCapacity=-1f;
    private string lastEnergyCaption;
    private static EntityVehicle observedEnergyVehicle;
    private static float nextEnergyObservation;
    private static bool energyObserved;
    private static RebirthCamperEnergyMode observedMode;
    private static float observedTransition;
    internal static bool TryObserveEnergy(EntityVehicle selected,out RebirthCamperEnergyMode mode,out float transition)
    {
        mode=RebirthCamperEnergyMode.Travel;transition=0;
        if(selected==null||!RebirthCamperRadial.IsCamper(selected)||!RebirthCamperEnergyRuntime.IsRegistered)return false;
        float now=Time.realtimeSinceStartup;
        if(!ReferenceEquals(selected,observedEnergyVehicle)||now>=nextEnergyObservation||now<nextEnergyObservation-0.2f)
        {
            observedEnergyVehicle=selected;nextEnergyObservation=now+0.2f;
            energyObserved=RebirthCamperEnergyRuntime.TryObservePresentation(selected,out observedMode,out _,out _,out observedTransition);
        }
        mode=observedMode;transition=observedTransition;return energyObserved;
    }
    private static string FuelCaption(EntityVehicle selected)
    {
        string title=Localization.Get("xuiRebirthVehicleHudFuel");
        if(!TryObserveEnergy(selected,out var energyMode,out float transition))return title;
        string mode=Localization.Get("xuiRebirthEnergyHud"+energyMode);
        if(energyMode==RebirthCamperEnergyMode.Deploying||energyMode==RebirthCamperEnergyMode.Packing)
            mode=string.Format(CultureInfo.InvariantCulture,mode,Mathf.CeilToInt(transition));
        return title+" · "+mode;
    }
    internal static EntityVehicle ResolveVehicle(EntityPlayerLocal player)
    {
        if(player==null||player.IsDead())return null;
        var driven=player.AttachedToEntity as EntityVehicle;
        if(driven!=null&&RebirthCamperRadial.IsDriver(driven,player))return driven;
        return ResolveLookVehicle(player);
    }
    internal static bool IsDriving(EntityPlayerLocal player)
    {
        var driven=player?.AttachedToEntity as EntityVehicle;
        return driven!=null&&RebirthCamperRadial.IsDriver(driven,player);
    }
    internal static EntityVehicle ResolveLookVehicle(EntityPlayerLocal player)
    {
        if(player==null||player.IsDead())return null;
        // Match native target acquisition, without retaining its three-second fade.
        var hit=player.HitInfo;
        if(hit==null||!hit.bHitValid||hit.transform==null||hit.tag==null
            ||!hit.tag.StartsWith("E_",StringComparison.Ordinal))return null;
        var root=GameUtils.GetHitRootTransform(hit.tag,hit.transform);
        return root==null?null:root.GetComponent<EntityVehicle>();
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        LayoutCard();
        var selected=ResolveVehicle(xui?.playerUI?.entityPlayer);
        float fill=selected==null?0f:statType==HUDStatTypes.VehicleFuel
            ?selected.GetVehicle().GetFuelPercent():selected.GetVehicle().GetHealthPercent();
        bool changed=vehicle!=selected||!Mathf.Approximately(currentValue,fill);
        vehicle=selected;currentValue=fill;
        float amount=selected==null?0f:statType==HUDStatTypes.VehicleFuel?selected.GetVehicle().GetFuelLevel():selected.Health;
        float capacity=selected==null?0f:statType==HUDStatTypes.VehicleFuel?selected.GetVehicle().GetMaxFuelLevel():selected.GetMaxHealth();
        string energyCaption=statType==HUDStatTypes.VehicleFuel?FuelCaption(selected):null;
        // Absolute values can change while the fraction remains unchanged (capacity changes).
        if(changed||!Mathf.Approximately(lastAmount,amount)||!Mathf.Approximately(lastCapacity,capacity)||lastEnergyCaption!=energyCaption)RefreshBindings();
        lastAmount=amount;lastCapacity=capacity;
        lastEnergyCaption=energyCaption;
    }
    public override bool GetBindingValueInternal(ref string value,string name)
    {
        localPlayer=xui?.playerUI?.entityPlayer;
        vehicle=ResolveVehicle(localPlayer);
        currentValue=vehicle==null?0f:statType==HUDStatTypes.VehicleFuel
            ?vehicle.GetVehicle().GetFuelPercent():vehicle.GetVehicle().GetHealthPercent();
        if(name=="rebirthvehiclefuelcaption"){value=FuelCaption(vehicle);return true;}
        if(name=="statcurrentwithmax")
        {
            value=vehicle==null?string.Empty:statType==HUDStatTypes.VehicleFuel
                ?vehicle.GetVehicle().GetFuelLevel().ToString("0.##",CultureInfo.InvariantCulture)+"/"+vehicle.GetVehicle().GetMaxFuelLevel().ToString("0.##",CultureInfo.InvariantCulture)
                :vehicle.Health.ToString(CultureInfo.InvariantCulture)+"/"+vehicle.GetMaxHealth().ToString(CultureInfo.InvariantCulture);
            return true;
        }
        return base.GetBindingValueInternal(ref value,name);
    }
    private void LayoutCard()
    {
        int width=ViewComponent?.Size.x??180;
        bool fuel=statType==HUDStatTypes.VehicleFuel;
        string prefix=fuel?"vehicleFuel":"vehicleCondition";
        Resize(prefix+"Card",0,0,width,44);
        Resize(prefix+"Frame",0,0,width,44);
        Resize(prefix+"Caption",32,-3,Math.Max(1,width-148),18);
        Resize("TextContent",Math.Max(32,width-112),-3,104,18);
        // Icon occupies x=8..26. Keep both bar layers beyond its right edge.
        Resize(prefix+"Track",32,-27,Math.Max(1,width-40),10);
        Resize("BarContent",32,-27,Math.Max(1,width-40),10);
    }
    private void Resize(string id,int x,int y,int width,int height)
    {
        var view=GetChildById(id)?.ViewComponent;
        if(view==null)return;
        view.Position=new Vector2i(x,y);
        view.Size=new Vector2i(width,height);
    }
}
