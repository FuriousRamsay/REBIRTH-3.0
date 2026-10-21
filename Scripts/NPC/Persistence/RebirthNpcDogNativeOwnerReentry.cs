using System;
using System.IO;
using System.Linq;

public static partial class RebirthDogLifecycleService
{
    // Actual native owner load lineage, independent of hire/trade action stamps.
    internal sealed class NativeOwnerReentry
    {
        private readonly HireNativeOwnerLoad source;private readonly string creation;
        private NativeOwnerReentry(HireNativeOwnerLoad source,string creation){this.source=source;this.creation=creation;}
        internal World World=>source.World;internal string Directory=>source.Save;internal EntityPlayer Actor=>source.Actor;
        internal string Account=>source.Owner;internal string Creation=>creation;internal int Deaths=>source.Deaths;
        internal bool Current()
        {
            try
            {
                var actor=source.Actor;
                if(!ThreadManager.IsMainThread()||!source.Loaded||!source.ReadReturned||!source.Hydrated||!source.HydrationEntered||source.Dead||
                    source.Spawn!=null&&!source.Spawn.Returned||source.World?.IsRemote()!=false||!ReferenceEquals(source.World,GameManager.Instance?.World)||
                    !ReferenceEquals(actor?.world,source.World)||!ReferenceEquals(source.World.GetEntity(actor.entityId),actor)||!actor.IsSpawned()||actor.IsDead()||actor.Died!=source.Deaths||
                    !SameHirePath(source.Save,GameIO.GetSaveGameDir())||RebirthCamperCharacterAccess.CurrentCreation(actor)!=creation||
                    !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||owner.CanonicalId!=source.Owner||
                    !ReferenceEquals(actor.inventory?.ItemGrid.items,source.BeltGrid)||!ReferenceEquals(actor.bag?.ItemGrid.items,source.BagGrid))return false;
                var belt=RebirthGearPlayerDataInventory.ReadSlots(source.Data,false);var bag=RebirthGearPlayerDataInventory.ReadSlots(source.Data,true);
                return belt!=null&&bag!=null&&RebirthCamperInventorySnapshot.Capture(belt).SequenceEqual(RebirthCamperInventorySnapshot.Capture(source.BeltGrid))&&
                    RebirthCamperInventorySnapshot.Capture(bag).SequenceEqual(RebirthCamperInventorySnapshot.Capture(source.BagGrid));
            }
            catch{return false;}
        }
        internal static NativeOwnerReentry Capture(object state)
        {
            if(state is not HireNativeOwnerLoad source||source.Actor==null)return null;string creation=RebirthCamperCharacterAccess.CurrentCreation(source.Actor);
            if(string.IsNullOrWhiteSpace(creation))return null;var value=new NativeOwnerReentry(source,creation);return value.Current()?value:null;
        }
    }
    internal static void ObserveNativeOwnerReentries()
    {
        if(!ThreadManager.IsMainThread())return;
        lock(HireColdGate)
        {
            foreach(var loaded in HireNativeOwners.Values.Where(value=>value.Hydrated).Take(8).ToArray())
            {
                var source=NativeOwnerReentry.Capture(loaded);
                if(source!=null)RebirthNpcSaveQuarantineBundle.ObserveServerNativeOwnerReentry(source);
            }
        }
    }
}
