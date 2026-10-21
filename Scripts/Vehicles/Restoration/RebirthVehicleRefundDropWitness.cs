using System;
using HarmonyLib;
using UnityEngine;

// Observe the actual native spawn inside this one refund invocation. This is
// runtime publication evidence only, not proof of a completed disk save.
internal sealed class RebirthVehicleRefundDropWitness:IDisposable
{
    [ThreadStatic] private static RebirthVehicleRefundDropWitness current;
    private static bool installed;
    private readonly World world;
    private readonly EntityPlayer owner;
    private readonly ItemStack expected;
    private EntityItem spawned;
    private bool ambiguous;
    private RebirthVehicleRefundDropWitness(EntityPlayer player,ItemStack stack)
    {if(current!=null)throw new InvalidOperationException("Nested vehicle refund publication is unresolved.");owner=player;world=player.world as World;expected=stack.Clone();current=this;}
    internal static bool TryDrop(EntityPlayer player,ItemStack stack)
    {
        if(!ThreadManager.IsMainThread()||player?.world is not World world||world.IsRemote()||stack==null||stack.IsEmpty()||
            !ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(world.GetEntity(player.entityId),player))return false;
        if(!installed)
        {
            RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.vehicle.refund.spawn.witness"),typeof(SpawnObserver));
            installed=true;
        }
        using(var proof=new RebirthVehicleRefundDropWitness(player,stack))
        {
            GameManager.Instance.ItemDropServer(stack.Clone(),player.position+Vector3.up,Vector3.zero,player.entityId,60f,false);
            var item=proof.spawned;
            return !proof.ambiguous&&item!=null&&ReferenceEquals(GameManager.Instance.World,world)&&
                ReferenceEquals(world.GetEntity(item.entityId),item)&&!item.IsMarkedForUnload()&&
                item.belongsPlayerId==player.entityId&&RebirthStationGridIngredients.IsSameStackSnapshot(item.itemStack,proof.expected);
        }
    }
    private void Observe(World observed,Entity entity)
    {
        if(!ReferenceEquals(observed,world)||entity is not EntityItem item||item.belongsPlayerId!=owner.entityId||
            !ReferenceEquals(item.world,world)||!ReferenceEquals(world.GetEntity(item.entityId),item)||
            !RebirthStationGridIngredients.IsSameStackSnapshot(item.itemStack,expected))return;
        if(spawned!=null&&!ReferenceEquals(spawned,item))ambiguous=true;
        else spawned=item;
    }
    public void Dispose(){if(ReferenceEquals(current,this))current=null;}
    [HarmonyPatch(typeof(World),nameof(World.SpawnEntityInWorld),typeof(Entity))]
    private static class SpawnObserver
    {
        private static void Postfix(World __instance,Entity __0,bool __runOriginal)
        {if(__runOriginal&&ThreadManager.IsMainThread())current?.Observe(__instance,__0);}
    }
}
