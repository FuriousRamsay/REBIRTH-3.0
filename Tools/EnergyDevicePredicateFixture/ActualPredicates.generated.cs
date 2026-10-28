using System;
using System.Linq;
partial class Adapter
{
private static bool Eligible(PowerItem node)=>node is PowerConsumer&&node.TileEntity is TileEntityPoweredBlock&&node.Parent==null&&
        node.Children.Count==0&&!node.TileEntity.HasParent()&&node.TileEntity.ChildCount==0&&node.RequiredPower>0&&
        (Block.list[node.BlockID] is BlockPoweredLight||Block.list[node.BlockID] is BlockPoweredTrap||Block.list[node.BlockID] is BlockRanged||Block.list[node.BlockID] is BlockLauncher);
private static bool MatchesExpected(Session s,PowerItem node)
    {
        if(!s.Baselines.TryGetValue(node.Position,out var b)||node.BlockID!=b.Block)return false;
        var block=s.World.GetBlock(node.Position);bool toggle=(node as PowerConsumerToggle)?.IsToggled??false;
        return (block.meta==b.ExpectedMeta&&(!b.HasToggle||toggle==b.ExpectedToggle))||
            (!b.Applied&&block.meta==b.PreviousMeta&&(!b.HasToggle||toggle==b.PreviousToggle));
    }
private static bool DeviceOwnerAuthorized(Session s,PowerItem node)
    {
        var owner=s.Vehicle.GetOwner();if(owner==null)return false;
        if(node.TileEntity is TileEntityPoweredRangedTrap ranged)return ranged.IsOwner(owner);
        // Generic native power tile entities have no placement owner field.
        // Require the camper owner's actual native land claim; do not guess
        // ownership from mere proximity or IsPlayerPlaced.
        var player=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerData(owner);
        return player!=null&&s.World.GetLandClaimOwner(node.Position,player)==EnumLandClaimOwner.Self;
    }
private static bool PoiOwnerAuthorized(EntityVehicle vehicle,PowerItem node)
    {
        var owner=vehicle.GetOwner();var player=owner==null?null:GameManager.Instance.GetPersistentPlayerList()?.GetPlayerData(owner);if(player==null)return false;
        var claim=vehicle.world.GetLandClaimOwner(node.Position,player);return claim==EnumLandClaimOwner.None||claim==EnumLandClaimOwner.Self;
    }
private static bool Inside(PrefabInstance poi,Vector3i p)
    {var a=poi.boundingBoxPosition;var b=poi.boundingBoxSize;return p.x>=a.x&&p.x<a.x+b.x&&p.y>=a.y&&p.y<a.y+b.y&&p.z>=a.z&&p.z<a.z+b.z;}
private static bool EnemyNear(World world,Vector3i position)
    {
        float square=DefenseRange*DefenseRange;var center=position.ToVector3Center();
        foreach(var entity in world.Entities.list)
            if(entity is EntityEnemy enemy&&!enemy.IsDead()&&!enemy.IsSleeping&&(enemy.position-center).sqrMagnitude<=square)return true;
        return false;
    }
}
