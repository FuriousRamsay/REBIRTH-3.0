using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

// Uses the same native player grids and resource eligibility as other camper actions.
internal static class RebirthCamperTurretInventory
{
    internal static ItemStack[] Local(EntityPlayer player)=>player.bag.ItemGrid.CloneItems().Concat(player.inventory.ItemGrid.CloneItems()).ToArray();
    internal static ItemStack[] Uploaded(PlayerDataFile data)
    {
        var bag=RebirthGearPlayerDataInventory.ReadSlots(data,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(data,false);
        if(bag==null||belt==null)throw new InvalidDataException("Native actor inventories unavailable.");return bag.Concat(belt).ToArray();
    }
    internal static void Publish(EntityPlayer player,ItemStack[] items)
    {
        int count=player.bag.ItemGrid.items.Length;
        if(items.Length!=count+player.inventory.ItemGrid.items.Length)throw new InvalidDataException("Actor grid changed.");
        if(!RebirthVehicleInventoryWrite.CanMutate(player,true)||!RebirthVehicleInventoryWrite.CanMutate(player,false))throw new InvalidOperationException("Actor inventory busy.");
        if(player is EntityPlayerLocal local)
        {
            var bag=items.Take(count).ToArray();var belt=items.Skip(count).ToArray();
            if(!RebirthVehicleInventoryWrite.SameSlots(player.bag.ItemGrid.items,bag))local.PlayerUI.xui.PlayerInventory.SetBackpackItemStacks(bag);
            if(!RebirthVehicleInventoryWrite.SameSlots(player.inventory.ItemGrid.items,belt))local.PlayerUI.xui.PlayerInventory.SetToolbeltItemStacks(belt);
            return;
        }
        for(int i=0;i<items.Length;i++)
        {
            var old=i<count?player.bag.ItemGrid.items[i]:player.inventory.ItemGrid.items[i-count];
            if(RebirthCamperEnergyItems.Same(old,items[i]))continue;
            if(i<count)player.bag.SetSlot(i,items[i]);else player.inventory.SetItem(i-count,items[i]);
        }
    }
    internal static MemoryStream Rewrite(MemoryStream original,bool bag,ItemStack[] slots)
    {
        using(var input=new MemoryStream(original.ToArray(),false))using(var r=MemoryPools.poolBinaryReader.AllocSync(false))
        {
            r.SetBaseStream(input);byte version=r.ReadByte();if(version!=(bag?2:1))throw new InvalidDataException();
            var grid=ItemStackGrid.Read(r,StreamModeRead.Persistency);byte selected=bag?(byte)0:r.ReadByte();
            if(input.Position!=input.Length||grid.Length!=slots.Length)throw new InvalidDataException();
            for(int i=0;i<slots.Length;i++)grid.items[i]=slots[i].Clone();
            var output=new MemoryStream();using(var w=MemoryPools.poolBinaryWriter.AllocSync(false))
            {w.SetBaseStream(output);w.Write(version);grid.Write(w,StreamModeWrite.Persistency);if(!bag)w.Write(selected);w.Flush();}output.Position=0;return output;
        }
    }
    internal static string Apply(EntityPlayer player,RebirthCamperTurretState state,ItemStack[] items,int bagCount,int ownedBelt,RebirthCamperTurretAction action,int corner,int option,out Action commitSource)
    {
        commitSource=null;
        if(RebirthCamperTurretHeldTransfer.IsHeld(action))return RebirthCamperTurretHeldTransfer.Apply(state,items,action,corner,option);
        if(action==RebirthCamperTurretAction.InstallFromSlot&&(option<0||option>=bagCount+ownedBelt))return "xuiCamperTurretInvalid";
        if((action==RebirthCamperTurretAction.Load||action==RebirthCamperTurretAction.InstallScope)&&(option>>2)>=bagCount+ownedBelt)return "xuiCamperTurretInvalid";
        if(action==RebirthCamperTurretAction.InstallSilencer&&(option<0||option>=bagCount+ownedBelt))return "xuiCamperTurretInvalid";
        // Returning equipment is deliberately limited to backpack capacity.
        if(action==RebirthCamperTurretAction.Remove||action==RebirthCamperTurretAction.Unload||action==RebirthCamperTurretAction.RemoveSilencer||action==RebirthCamperTurretAction.RemoveScope)
        {
            var bag=items.Take(bagCount).ToArray();var error=RebirthCamperTurretTransfer.Apply(state,bag,action,corner,option);
            if(error==null)Array.Copy(bag,items,bagCount);return error;
        }
        if(action!=RebirthCamperTurretAction.Install)return RebirthCamperTurretTransfer.Apply(state,items,action,corner,option);
        if(corner<0||corner>=4||state.Slots[corner].HasTurret)return "xuiCamperTurretInvalid";
        int available=Array.FindIndex(items,0,bagCount+ownedBelt,s=>!s.IsEmpty()&&s.itemValue.ItemClass.GetItemName()==RebirthCamperTurretProfiles.ItemName);
        if(available>=0)return RebirthCamperTurretTransfer.Apply(state,items,action,corner,option);
        foreach(var source in Sources(player))
        {
            int revision=source.Revision;var slots=source.Slots;
            for(int i=0;i<slots.Length;i++)
            {
                if(source.SlotLocks!=null&&i<source.SlotLocks.Length&&source.SlotLocks[i])continue;
                var before=slots[i];if(before.IsEmpty()||before.itemValue.ItemClass.GetItemName()!=RebirthCamperTurretProfiles.ItemName)continue;
                before=before.Clone();var one=new[]{before.Clone()};var error=RebirthCamperTurretTransfer.Apply(state,one,action,corner,option);if(error!=null)return error;
                int index=i;var after=one[0];
                commitSource=()=>
                {
                    if(source.Revision!=revision||!Eligible(source,player)||!RebirthCamperEnergyItems.Same(source.Slots[index],before))throw new InvalidOperationException("Broadcast inventory changed.");
                    source.SetSlot(index,after);source.MarkModified();
                    if(!RebirthCamperEnergyItems.Same(source.Slots[index],after))throw new InvalidOperationException("Broadcast debit was refused.");
                    try{RemoteResourceLiveSync.NotifySourcesChanged(new List<IRemoteResourceSource>{source},"camper turret installed");}catch(Exception e){Log.Warning("[CamperTurrets] Installed turret; resource notification failed: "+e.Message);}
                };
                return null;
            }
        }
        return "xuiCamperTurretMissing";
    }
    private static bool Eligible(IRemoteResourceSource s,EntityPlayer p)=>s.Kind==RemoteResourceSourceKind.StaticContainer&&
        !RebirthRemoteResourceSourceCustody.BlocksSource(s)&&RemoteResourceEligibility.CanUse(s,p,true,false,out _)&&
        (s.Position-p.position).sqrMagnitude<=RemoteResourcesRuntimePolicy.Radius*RemoteResourcesRuntimePolicy.Radius;
    private static IEnumerable<IRemoteResourceSource> Sources(EntityPlayer player)=>
        RemoteResourceSourceDiscovery.ResolveNearby(player.world,player,RemoteResourcesRuntimePolicy.Radius).Where(s=>Eligible(s,player));
}
