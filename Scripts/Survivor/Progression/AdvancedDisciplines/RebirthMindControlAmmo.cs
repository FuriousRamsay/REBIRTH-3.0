using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// 2.6 mind control arrows and bolts. A hit asks the Black Magic service to dominate the target,
/// so its Witch Doctor, tier, knowledge and capacity rules decide the outcome.
/// </summary>
[Preserve]
public class MinEventActionRebirthMindControlHit : MinEventActionTargetedBase
{
    public override void Execute(MinEventParams _params)
    {
        var player = _params.Self as EntityPlayer;
        if (player == null || targets == null) return;
        foreach (var entity in targets)
        {
            var target = entity as EntityAlive;
            if (target == null || target.IsDead() || target == player) continue;
            RebirthMindControlAmmo.Hit(player, target);
        }
    }
}

internal static class RebirthMindControlAmmo
{
    internal static void Hit(EntityPlayer player, EntityAlive target)
    {
        var world = player.world;
        if (world == null) return;
        if (world.IsRemote())
        {
            if (player is EntityPlayerLocal)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthMindControlHit>().Setup(player.entityId, target.entityId));
            return;
        }
        string reason;
        bool dominated = RebirthBlackMagicService.TryDominateFromAmmo(world, player, target, out reason);
        Log.Out("[REBIRTH MindControl] " + target.EntityClass?.entityClassName + " " + target.entityId + ": " + (dominated ? "turned" : "refused") + " " + reason);
        if (!string.IsNullOrEmpty(reason)) GameManager.ShowTooltipMP(player, reason, dominated ? "ui_success" : "ui_denied");
    }
}

[Preserve]
public sealed class NetPackageRebirthMindControlHit : NetPackage
{
    private const float MaximumRange = 150f;
    private int playerEntityId, targetEntityId;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRebirthMindControlHit Setup(int playerId, int targetId)
    {
        playerEntityId = playerId;
        targetEntityId = targetId;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        var b = (BinaryReader)reader;
        playerEntityId = b.ReadInt32();
        targetEntityId = b.ReadInt32();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        var b = (BinaryWriter)writer;
        b.Write(playerEntityId);
        b.Write(targetEntityId);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote()) return;
        // The sender may only speak for its own player.
        if (Sender == null || Sender.entityId != playerEntityId) return;
        var player = world.GetEntity(playerEntityId) as EntityPlayer;
        var target = world.GetEntity(targetEntityId) as EntityAlive;
        if (player == null || target == null || target.IsDead()) return;
        if ((player.position - target.position).sqrMagnitude > MaximumRange * MaximumRange) return;
        RebirthMindControlAmmo.Hit(player, target);
    }

    public int GetLength() => 8;
}

/// <summary>2.6 Mind Control Potion: where it lands, each undead in range is offered to the Black Magic service.</summary>
[Preserve]
public class MinEventActionRebirthMindControlArea : MinEventActionBase
{
    private float radius = 3f;

    public override void Execute(MinEventParams _params)
    {
        // A landed potion reports no thrower, so the nearest player is taken as the one who threw it.
        var world = GameManager.Instance?.World;
        var player = _params.Self as EntityPlayer ?? world?.GetClosestPlayer(_params.Position, 60f, false);
        Log.Out("[REBIRTH MindControl] potion impact at " + _params.Position + " by " + (player != null ? player.entityId.ToString() : "nobody"));
        if (player == null || player.world == null) return;
        var found = new System.Collections.Generic.List<Entity>();
        player.world.GetEntitiesInBounds(typeof(EntityEnemy), new Bounds(_params.Position, Vector3.one * radius * 2f), found);
        foreach (var entity in found)
        {
            var target = entity as EntityAlive;
            if (target == null || target.IsDead() || (target.position - _params.Position).sqrMagnitude > radius * radius) continue;
            RebirthMindControlAmmo.Hit(player, target);
        }
    }

    public override bool ParseXmlAttribute(System.Xml.Linq.XAttribute _attribute)
    {
        if (_attribute.Name.LocalName == "radius") { radius = StringParsers.ParseFloat(_attribute.Value); return true; }
        return base.ParseXmlAttribute(_attribute);
    }
}

/// <summary>2.6 brews and potions: only a Witch Doctor can use them.</summary>
[Preserve]
public class RebirthIsWitchDoctor : RequirementBase
{
    public override bool IsValid(MinEventParams _params)
    {
        bool witchDoctor = RebirthBlackMagicService.HasWitchDoctor(_params.Self as EntityPlayer);
        return invert ? !witchDoctor : witchDoctor;
    }
}
