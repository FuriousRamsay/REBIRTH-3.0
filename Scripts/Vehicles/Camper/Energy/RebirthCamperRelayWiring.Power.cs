using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

internal interface IRebirthCamperMobileEnergyConsumer { }
internal static partial class RebirthCamperRelayWiring
{
    private sealed class Supply {internal readonly float[] Until=new float[4];internal int Mask;internal bool Published;internal TurretLoad[] Loads;}
    private static readonly ConditionalWeakTable<EntityVehicle,Supply> suppliedTurrets=new ConditionalWeakTable<EntityVehicle,Supply>();
    private sealed class TurretLoad:IRebirthCamperEnergyConsumer,IRebirthCamperMobileEnergyConsumer
    {
        internal EntityVehicle Vehicle;internal int Corner;internal Supply Supply;
        public string Id=>"camper-turret:"+Corner;
        public int Priority=>0;
        public float Distance=>0;
        public int DemandWatts
        {
            get
            {
                var runtime=RebirthCamperTurretRuntime.For(Vehicle);var slot=runtime.Observe().Slots[Corner];
                // Mounted equipment uses the camper's internal paid supply;
                // only external world devices need a relay cable.
                if(!slot.HasTurret||(!slot.Enabled&&runtime.ControlOwner(Corner)<0))return 0;
                return Mathf.Max(1,Mathf.RoundToInt(RebirthCamperTurretProfiles.Number(RebirthCamperTurretProfiles.Block(slot),"RequiredPower",15)));
            }
        }
        public void SetSupply(EntityVehicle vehicle,int watts)
        {
            bool powered=DemandWatts>0&&watts>=DemandWatts;
            Supply.Until[Corner]=powered?Time.realtimeSinceStartup+1f:0;
            int mask=powered?Supply.Mask|(1<<Corner):Supply.Mask&~(1<<Corner);
            if(Supply.Published&&mask==Supply.Mask)return;
            Supply.Mask=mask;Supply.Published=true;
            vehicle.vehicle.GetUpdatedItemValue().SetMetadata("RebirthCamperTurretPowerV1",mask.ToString());vehicle.SendSyncData(4);
        }
    }
    internal static IEnumerable<IRebirthCamperEnergyConsumer> MountedConsumers(EntityVehicle vehicle)
    {
        if(!vehicle||vehicle.world.IsRemote())yield break;
        var supply=suppliedTurrets.GetValue(vehicle,_=>new Supply());
        if(supply.Loads==null){supply.Loads=new TurretLoad[4];for(int i=0;i<4;i++)supply.Loads[i]=new TurretLoad{Vehicle=vehicle,Supply=supply,Corner=i};}
        foreach(var load in supply.Loads)yield return load;
    }
    internal static bool TurretPowered(EntityVehicle vehicle,int corner)
    {
        if(!vehicle||corner<0||corner>3)return false;
        if(!vehicle.world.IsRemote())return suppliedTurrets.TryGetValue(vehicle,out var supply)&&Time.realtimeSinceStartup<supply.Until[corner];
        return vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperTurretPowerV1",out string text)&&int.TryParse(text,out int mask)&&(mask&(1<<corner))!=0;
    }
}
