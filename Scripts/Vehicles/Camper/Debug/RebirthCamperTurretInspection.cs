using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine.Scripting;

[Preserve]
public sealed class ConsoleCmdRebirthCamperTurretInspection:ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient=>true;
    public override string[] getCommands()=>new[]{"rbturretinspect"};
    public override string getDescription()=>"CodexTest camper articulation and ammunition observation.";
    public override string getHelp()=>"rbturretinspect vehicleId [corner yaw pitch] - expires after20 seconds, no shots during inspection";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        try
        {
            if(!RebirthGameBridge.IsRunning||!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")throw new InvalidOperationException(getHelp());
            if(args.Count>0&&args[0]=="source"){PrepareSource(args);return;}
            if(args.Count!=1&&args.Count!=4)throw new InvalidOperationException(getHelp());
            var vehicle=GameManager.Instance.World.GetEntity(int.Parse(args[0])) as EntityVehicle;
            if(vehicle==null||!RebirthCamperRadial.IsCamper(vehicle))throw new InvalidOperationException("Camper required.");
            var runtime=RebirthCamperTurretRuntime.For(vehicle);
            if(args.Count==4)
            {
                if(vehicle.world.IsRemote())throw new InvalidOperationException("Inspection movement requires host.");
                SdtdConsole.Instance.Output(runtime.Inspect(int.Parse(args[1]),float.Parse(args[2],CultureInfo.InvariantCulture),float.Parse(args[3],CultureInfo.InvariantCulture),true));
            }
            else for(int i=0;i<4;i++)
            {
                var slot=runtime.Observe().Slots[i];
                SdtdConsole.Instance.Output(runtime.Inspect(i,0,0,false)+" installed="+slot.HasTurret+" enabled="+slot.Enabled+" ammo="+string.Join(";",slot.Ammo.Select(s=>s.IsEmpty()?"empty":s.itemValue.ItemClass.GetItemName()+":"+s.count+":damage="+RebirthCamperTurretAmmoEffects.Damage(s.itemValue))));
            }
        }
        catch(Exception e){SdtdConsole.Instance.Output("ERROR "+e.Message);}
    }
    // Test setup only: move an existing item to a native inventory source.
    // Installation still requires the real management button and production transaction.
    private static void PrepareSource(List<string> args)
    {
        var world=GameManager.Instance.World;var player=world.GetPrimaryPlayer();
        if(world.IsRemote())throw new InvalidOperationException("Host test setup only");
        var items=RebirthCamperTurretInventory.Local(player);int bag=player.bag.ItemGrid.items.Length;
        int limit=bag+RebirthToolbeltCapacity.GetOwnedSlotCount(player,player.inventory.ItemGrid.items.Length);
        int from=Array.FindIndex(items,0,limit,s=>!s.IsEmpty()&&s.itemValue.ItemClass.GetItemName()==RebirthCamperTurretProfiles.ItemName);
        if(from<0)throw new InvalidOperationException("Give or remove one custom turret first");
        var one=items[from].Clone();one.count=1;
        if(args.Count==2&&args[1]=="belt")
        {
            int to=Array.FindIndex(items,bag,limit-bag,s=>s.IsEmpty());if(to<0)throw new InvalidOperationException("No empty owned toolbelt slot");
            items[from].count--;if(items[from].count==0)items[from]=ItemStack.Empty.Clone();items[to]=one;
            RebirthCamperTurretInventory.Publish(player,items);SdtdConsole.Instance.Output("SETUP moved one turret to toolbelt slot="+(to-bag));return;
        }
        if(args.Count!=4)throw new InvalidOperationException("source belt | source x y z");
        var pos=new Vector3i(int.Parse(args[1]),int.Parse(args[2]),int.Parse(args[3]));var source=new StaticContainerResourceSource(world,pos);
        if(!source.IsLoaded||!source.IsAuthorized(player,false,out _))throw new InvalidOperationException("Owned native storage required");
        int dest=Array.FindIndex(source.Slots,s=>s.IsEmpty());if(dest<0)throw new InvalidOperationException("Container full");
        RemoteResourceStateStore.Activate(source.StableId);
        if(!RemoteResourceEligibility.CanUse(source,player,false,false,out var reason))throw new InvalidOperationException(reason);
        source.SetSlot(dest,one);source.MarkModified();items[from].count--;if(items[from].count==0)items[from]=ItemStack.Empty.Clone();
        RebirthCamperTurretInventory.Publish(player,items);SdtdConsole.Instance.Output("SETUP moved one turret to broadcast source="+source.StableId+" slot="+dest+" radius="+RemoteResourcesRuntimePolicy.Radius);
    }
}
