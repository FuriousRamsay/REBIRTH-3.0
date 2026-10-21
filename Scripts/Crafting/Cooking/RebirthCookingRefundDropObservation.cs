using System;
using HarmonyLib;
using UnityEngine;

// Runtime publication evidence only. The owning return retains its original
// when native drop publication is unavailable or has an unknown outcome.
internal sealed class RebirthCookingRefundDropObservation:IDisposable
{
    [ThreadStatic]private static RebirthCookingRefundDropObservation current;
    private static bool installed;
    private EntityPlayer owner;private World world;private ItemStack expected;private Func<bool> originalCurrent;
    private EntityItem spawned;private bool ambiguous;private int deaths;private string creation;
    private IDisposable allocation;
    private bool Current(){try{return CurrentCore();}catch{return false;}}
    private bool CurrentCore()=>ThreadManager.IsMainThread()&&owner!=null&&ReferenceEquals(GameManager.Instance?.World,world)&&
        ReferenceEquals(owner.world,world)&&(owner is not EntityPlayerLocal||ReferenceEquals(world.GetPrimaryPlayer(),owner))&&ReferenceEquals(world.GetEntity(owner.entityId),owner)&&
        !world.IsRemote()&&owner.IsSpawned()&&!owner.IsDead()&&owner.Died==deaths&&RebirthCamperCharacterAccess.CurrentCreation(owner)==creation&&originalCurrent?.Invoke()==true;
    internal static bool TryDrop(EntityPlayerLocal actor,ItemStack offer,out bool attempted)
        =>TryDrop(actor,offer,()=>true,out attempted,out _);
    // The caller already owns the immutable overflow original. A remote actor
    // needs its authenticated native action scope; this API never accepts a
    // client-authored drop payload or treats the live result as a saved receipt.
    internal static bool TryDrop(EntityPlayer actor,ItemStack offer,Func<bool> currentOriginal,out bool attempted,out EntityItem observed,Func<bool> beforeInvoke=null)
    {
        attempted=false;observed=null;
        if(current!=null||!ThreadManager.IsMainThread()||currentOriginal==null||actor?.world is not World world||world.IsRemote()||offer==null||offer.IsEmpty()||offer.count<1)return false;
        using(var proof=new RebirthCookingRefundDropObservation{owner=actor,world=world,deaths=actor.Died,creation=RebirthCamperCharacterAccess.CurrentCreation(actor),originalCurrent=currentOriginal})
        {
            if(string.IsNullOrEmpty(proof.creation)||!proof.Current())return false;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var memory))return false;
            proof.allocation=memory;
            if(!installed){RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.cooking.refund.spawn.observation"),typeof(SpawnObserver));installed=true;}
            // Use independently hydrated native frames for the expected output
            // and argument. Native mutation cannot change retained grid custody.
            long remaining=2L*1024*1024;
            byte[] image=RemoteResourceTransactions.CaptureOriginalNativeFrame(new[]{offer},ref remaining);
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(64L*1024*1024+2*RemoteResourceTransactions.MeasureOriginalNativeFrame(image))))return false;
            proof.expected=RebirthCamperInventorySnapshot.Restore(image)[0];
            var argument=RebirthCamperInventorySnapshot.Restore(image)[0];
            if(!proof.Current())return false;
            current=proof;
            if(beforeInvoke!=null&&!beforeInvoke())return false;
            if(!proof.Current())return false;
            attempted=true;
            GameManager.Instance.ItemDropServer(argument,actor.position,Vector3.zero,actor.entityId,120f,false);
            var item=proof.spawned;
            bool accepted=proof.Current()&&!proof.ambiguous&&item!=null&&ReferenceEquals(world.GetEntity(item.entityId),item)&&
                !item.IsMarkedForUnload()&&item.belongsPlayerId==actor.entityId&&ReferenceEquals(item.world,world)&&
                RebirthStationGridIngredients.IsSameStackSnapshot(item.itemStack,proof.expected);
            if(accepted)observed=item;return accepted;
        }
    }
    private void Observe(World observed,Entity entity)
    {
        if(!Current()||!ReferenceEquals(observed,world)||entity is not EntityItem item||item.belongsPlayerId!=owner.entityId||
            !ReferenceEquals(item.world,world)||!ReferenceEquals(world.GetEntity(item.entityId),item)||
            !RebirthStationGridIngredients.IsSameStackSnapshot(item.itemStack,expected))return;
        if(spawned!=null&&!ReferenceEquals(spawned,item))ambiguous=true;else spawned=item;
    }
    public void Dispose(){if(ReferenceEquals(current,this))current=null;allocation?.Dispose();allocation=null;owner=null;world=null;expected=null;spawned=null;originalCurrent=null;}
    [HarmonyPatch(typeof(World),nameof(World.SpawnEntityInWorld),typeof(Entity))]
    private static class SpawnObserver
    {
        private static void Postfix(World __instance,Entity __0,bool __runOriginal)
        {if(__runOriginal&&ThreadManager.IsMainThread())current?.Observe(__instance,__0);}
    }
}
