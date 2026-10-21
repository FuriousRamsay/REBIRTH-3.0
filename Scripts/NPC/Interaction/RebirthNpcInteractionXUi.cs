using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Native in-game interaction surface for authoritative NPC projections and mutations.
/// The controller never mutates runtime state directly; every action passes through the
/// interaction session and command router with an expected runtime revision.
/// </summary>
public static class RebirthNpcInteractionUiService
{
    public const string WindowGroupName = RebirthCompanionInteractionUiService.Group;
    private const float SearchRadius = RebirthNpcTradeReach.SearchRadius;

    public static bool OpenNearest(XUi xui, out string error)
    {
        error = string.Empty;
        EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        if (player == null) { error = "A local player is required."; return false; }
        List<Entity> entities = new List<Entity>();
        Bounds bounds = new Bounds(player.position, new Vector3(SearchRadius * 2f, SearchRadius * 2f, SearchRadius * 2f));
        GameManager.Instance.World.GetEntitiesInBounds(typeof(EntityRebirthNPC), bounds, entities);
        EntityRebirthNPC nearest = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < entities.Count; i++)
        {
            EntityRebirthNPC candidate = entities[i] as EntityRebirthNPC;
            if (candidate == null || candidate.IsDead()) continue;
            float distance = Vector3.SqrMagnitude(candidate.position - player.position);
            if (distance < nearestDistance) { nearestDistance = distance; nearest = candidate; }
        }
        if (nearest == null) { error = "No REBIRTH NPC is within " + SearchRadius.ToString("0", CultureInfo.InvariantCulture) + " metres."; return false; }
        return Open(xui, nearest, out error);
    }

    public static bool Open(XUi xui,EntityRebirthNPC npc,out string error)
    {
        error=string.Empty;
        var controller=xui?.FindWindowGroupByName(RebirthCompanionInteractionUiService.Group)?.GetChildByType<XUiC_RebirthCompanionInteractionWindow>();
        if(controller==null||npc==null||npc.IsDead()){error=Localization.Get("xuiRebirthNpcTargetUnavailable");return false;}
        ulong navigation=RebirthNpcWindowInput.OpenRevision;
        RebirthNpcInteractionNetworkClient.RequestOpen(npc.entityId,delegate(RebirthNpcInteractionProjection projection,RebirthNpcInteractionCommandResponse ignored,string networkError)
        {
            if(projection==null){Log.Out("[REBIRTH NPC] "+networkError);return;}
            if(navigation!=RebirthNpcWindowInput.OpenRevision||npc.IsDead())
            {RebirthNpcInteractionNetworkClient.RequestClose(projection.SessionKey);return;}
            if((projection.Permissions&RebirthNpcInteractionPermissions.Dismiss)!=0)
            {
                RebirthNpcInteractionNetworkClient.RequestClose(projection.SessionKey);
                RebirthCompanionUiService.Open(xui,"N:"+projection.NpcId,"Overview");return;
            }
            controller.PrepareHuman(npc.entityId,projection);
            xui.playerUI.windowManager.Open(RebirthCompanionInteractionUiService.Group,true,true);
        });
        return true;
    }
}
