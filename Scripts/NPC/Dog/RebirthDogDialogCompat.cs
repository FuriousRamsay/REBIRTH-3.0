using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

internal static class RebirthDogDialogContext
{
    public static EntityRebirthDogCompanion GetDog(EntityPlayer player, EntityNPC explicitNpc = null)
    {
        EntityRebirthDogCompanion dog = explicitNpc as EntityRebirthDogCompanion;
        if (dog != null) return dog;
        EntityPlayerLocal local = player as EntityPlayerLocal;
        XUi xui = local != null ? local.PlayerUI?.xui : LocalPlayerUI.GetUIForPlayer(player as EntityPlayerLocal)?.xui;
        return xui?.Dialog?.Respondent as EntityRebirthDogCompanion;
    }

    public static string TargetId(EntityRebirthDogCompanion dog)
    {
        RebirthNpcRuntimeState s = dog?.RebirthRuntimeState;
        return s == null || s.StableId.IsEmpty ? string.Empty : "N:" + s.StableId;
    }

    public static RebirthDogLifecycleKind Lifecycle(EntityRebirthDogCompanion dog)
    {
        RebirthNpcRuntimeState s = dog?.RebirthRuntimeState;
        if (s == null) return RebirthDogLifecycleKind.Active;
        RebirthNpcPersistentRecordView r; RebirthDogPersistentRecordView d;
        return RebirthDogStateService.TryGetView(s.StableId, out r, out d) ? d.Lifecycle : RebirthDogLifecycleKind.Active;
    }
}

[Preserve]
public sealed class DialogRequirementRebirthDogOwner : BaseDialogRequirement
{
    public override bool CheckRequirement(EntityPlayer player, EntityNPC talkingTo)
    {
        return RebirthDogLifecycleService.IsOwnedBy(talkingTo as EntityRebirthDogCompanion, player);
    }
    public override BaseDialogRequirement Clone() { var c = new DialogRequirementRebirthDogOwner(); c.ID=ID; c.Value=Value; c.Tag=Tag; c.Owner=Owner; c.RequirementVisibilityType=RequirementVisibilityType; return c; }
}

[Preserve]
public sealed class DialogRequirementRebirthDogLifecycle : BaseDialogRequirement
{
    public override bool CheckRequirement(EntityPlayer player, EntityNPC talkingTo)
    {
        RebirthDogLifecycleKind wanted;
        if (!Enum.TryParse(Value ?? string.Empty, true, out wanted)) return false;
        return RebirthDogDialogContext.Lifecycle(talkingTo as EntityRebirthDogCompanion) == wanted;
    }
    public override BaseDialogRequirement Clone() { var c = new DialogRequirementRebirthDogLifecycle(); c.ID=ID; c.Value=Value; c.Tag=Tag; c.Owner=Owner; c.RequirementVisibilityType=RequirementVisibilityType; return c; }
}

[Preserve]
public sealed class DialogRequirementRebirthDogPickupReady : BaseDialogRequirement
{
    public override bool CheckRequirement(EntityPlayer player, EntityNPC talkingTo)
    {
        EntityRebirthDogCompanion dog = talkingTo as EntityRebirthDogCompanion;
        if (dog == null || dog.IsDead()) return false;
        // 2.6 used a delayed-pickup CVar. Preserve it if other systems set it;
        // otherwise a newly spawned 3.1 dog is immediately eligible.
        return dog.Buffs == null || (!dog.Buffs.HasBuff("delayPickup") && dog.Buffs.GetCustomVar("$delayedPickup") <= 0f);
    }
    public override BaseDialogRequirement Clone() { var c = new DialogRequirementRebirthDogPickupReady(); c.ID=ID; c.Value=Value; c.Tag=Tag; c.Owner=Owner; c.RequirementVisibilityType=RequirementVisibilityType; return c; }
}

[Preserve]
public sealed class DialogRequirementRebirthDogOrderNot : BaseDialogRequirement
{
    public override bool CheckRequirement(EntityPlayer player, EntityNPC talkingTo)
    {
        EntityRebirthDogCompanion dog = talkingTo as EntityRebirthDogCompanion;
        RebirthNpcRuntimeState s = dog?.RebirthRuntimeState;
        RebirthNpcOrderState wanted;
        if (s == null || !Enum.TryParse(Value ?? string.Empty, true, out wanted)) return false;
        return s.Order != wanted;
    }
    public override BaseDialogRequirement Clone() { var c = new DialogRequirementRebirthDogOrderNot(); c.ID=ID; c.Value=Value; c.Tag=Tag; c.Owner=Owner; c.RequirementVisibilityType=RequirementVisibilityType; return c; }
}

[Preserve]
public sealed class DialogRequirementRebirthDogInventoryEnabled : BaseDialogRequirement
{
    public override bool CheckRequirement(EntityPlayer player, EntityNPC talkingTo)
    {
        return RebirthDogInventoryPolicy.InventoryEnabled;
    }
    public override BaseDialogRequirement Clone() { var c = new DialogRequirementRebirthDogInventoryEnabled(); c.ID=ID; c.Value=Value; c.Tag=Tag; c.Owner=Owner; c.RequirementVisibilityType=RequirementVisibilityType; return c; }
}

[Preserve]
public sealed class DialogRequirementRebirthDogAdmin : BaseDialogRequirement
{
    public override BaseDialogRequirement.RequirementTypes RequirementType => BaseDialogRequirement.RequirementTypes.Admin;
    public override bool CheckRequirement(EntityPlayer player, EntityNPC talkingTo)
    {
        if (player == null) return false;
        try { if (player.IsGodMode.Value) return true; } catch { }
        try { return GamePrefs.GetBool(EnumGamePrefs.DebugMenuEnabled); } catch { return false; }
    }
    public override BaseDialogRequirement Clone() { var c = new DialogRequirementRebirthDogAdmin(); c.ID=ID; c.Value=Value; c.Tag=Tag; c.Owner=Owner; c.RequirementVisibilityType=RequirementVisibilityType; return c; }
}

[Preserve]
public sealed class DialogActionRebirthDog : BaseDialogAction
{
    public override void PerformAction(EntityPlayer player)
    {
        EntityRebirthDogCompanion dog = RebirthDogDialogContext.GetDog(player);
        EntityPlayerLocal local = player as EntityPlayerLocal;
        if (dog == null || local == null) return;
        string targetId = RebirthDogDialogContext.TargetId(dog);
        if (string.IsNullOrEmpty(targetId)) return;
        switch ((ID ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "follow": RebirthCompanionService.Request(targetId, RebirthCompanionCommand.Follow); break;
            case "stay": RebirthCompanionService.Request(targetId, RebirthCompanionCommand.Stay); break;
            case "guard": RebirthCompanionService.Request(targetId, RebirthCompanionCommand.Guard); break;
            case "setrespawn":
            case "setrespawnpoint":
            case "respawnpoint": RebirthCompanionService.Request(targetId, RebirthCompanionCommand.SetRespawnPoint); break;
            case "pickup": RebirthCompanionService.Request(targetId, RebirthCompanionCommand.PickUp); break;
            case "reportforduty": RebirthCompanionService.Request(targetId, RebirthCompanionCommand.ReportForDuty); break;
            case "inventory": RebirthCompanionUiService.Open(local.PlayerUI?.xui, targetId, "Inventory"); break;
            case "stats": RebirthCompanionUiService.Open(local.PlayerUI?.xui, targetId, "Stats"); break;
            case "rename": RebirthCompanionUiService.Open(local.PlayerUI?.xui, targetId, "Overview"); break;
            case "adminremove": RebirthDogLifecycleService.TryAdminRemove(local.world, local, dog); break;
        }
    }
    public override BaseDialogAction Clone() { var c = new DialogActionRebirthDog(); CopyValues(c); c.OwnerDialog=OwnerDialog; c.Owner=Owner; return c; }
}
