using System;
using HarmonyLib;
using UnityEngine;

#nullable disable

/// <summary>
/// Restores the silent quest-cancellation/shared-quest behavior from REBIRTH 2.6.
///
/// Important distinction:
/// - voluntary/cross-party REMOVAL is silent;
/// - genuine quest FAILURE (for example walking out of the POI alive) still uses
///   Quest.CloseQuest(Failed) and therefore still gives the normal failure feedback.
/// </summary>
public static class RebirthQuestNotificationPolicy
{
    public static bool ShouldSilentlyRemove(Quest q)
    {
        if (q == null || q.QuestClass == null)
            return false;

        // All shared-quest removal/cancellation paths were silent in 2.6.
        if (q.SharedOwnerID != -1)
            return true;

        // User-cancelled normal trader jobs should also be silent. Genuine objective
        // failures do not call QuestJournal.RemoveQuest, so they are unaffected.
        return RebirthTraderJobCompletionStats.IsRecognizedNormalTraderJob(q);
    }

    public static void SilentRemoveQuest(
        QuestJournal journal,
        Quest q,
        bool propagatePartyRemoval)
    {
        if (journal == null || q == null)
            return;

        if (journal.FindActiveQuest(q.QuestCode) == null)
            return;

        // Preserve 2.6's non-removable StaticQuest behavior.
        if (q.QuestClass != null &&
            q.QuestClass.Properties != null &&
            q.QuestClass.Properties.Values.ContainsKey("StaticQuest"))
            return;

        ConnectionManager connection =
            SingletonMonoBehaviour<ConnectionManager>.Instance;

        // Shared copy tells the owning player it is no longer held by this member.
        if (q.SharedOwnerID != -1 && connection != null)
        {
            NetPackageSharedQuest package =
                NetPackageManager.GetPackage<NetPackageSharedQuest>()
                    .Setup(
                        q.QuestUniqueId,
                        q.QuestCode,
                        q.SharedOwnerID,
                        journal.OwnerPlayer.entityId,
                        false);

            if (connection.IsServer)
                connection.SendPackage(
                    package,
                    _attachedToEntityId: q.SharedOwnerID);
            else
                connection.SendToServer(package);
        }

        // ForceRemoveQuest performs the actual removal/unhook/event without
        // Quest.CloseQuest(Failed), so there is no "Quest Failed" toast or sound.
        journal.ForceRemoveQuest(q);

        // Personal quest owner cancellation must still remove copies from party members.
        if (propagatePartyRemoval && q.SharedOwnerID == -1)
            journal.HandlePartyRemoveQuest(q);
    }
}

/// <summary>
/// Normal quest-list Cancel/Remove and shared removals use ForceRemoveQuest instead of
/// CloseQuest(Failed). Actual gameplay failure paths still call CloseQuest directly.
/// </summary>
[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.RemoveQuest))]
public static class RebirthSilentQuestRemovePatch
{
    [HarmonyPrefix]
    public static bool Prefix(QuestJournal __instance, Quest q)
    {
        if (!RebirthQuestNotificationPolicy.ShouldSilentlyRemove(q))
            return true;

        RebirthQuestNotificationPolicy.SilentRemoveQuest(
            __instance,
            q,
            true);

        return false;
    }
}

/// <summary>
/// 2.6 parity: a shared quest removed by its owner disappears silently for the receiver.
/// </summary>
[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.RemoveSharedQuestByOwner))]
public static class RebirthSilentSharedQuestByOwnerPatch
{
    [HarmonyPrefix]
    public static bool Prefix(QuestJournal __instance, int questCode)
    {
        if (__instance == null || __instance.quests == null)
            return true;

        for (int i = __instance.quests.Count - 1; i >= 0; i--)
        {
            Quest q = __instance.quests[i];
            if (q == null ||
                q.QuestCode != questCode ||
                q.SharedOwnerID == -1 ||
                q.CurrentState != Quest.QuestState.InProgress)
                continue;

            __instance.ForceRemoveQuest(q);
            return false;
        }

        return true;
    }
}

/// <summary>
/// 2.6 parity: when a party member/owner disappears, remove their shared copies silently.
/// </summary>
[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.RemoveSharedQuestForOwner))]
public static class RebirthSilentSharedQuestForOwnerPatch
{
    [HarmonyPrefix]
    public static bool Prefix(QuestJournal __instance, int entityID)
    {
        if (__instance == null || __instance.quests == null)
            return true;

        bool removed = false;

        for (int i = __instance.quests.Count - 1; i >= 0; i--)
        {
            Quest q = __instance.quests[i];
            if (q == null ||
                q.SharedOwnerID != entityID ||
                q.CurrentState != Quest.QuestState.InProgress)
                continue;

            __instance.ForceRemoveQuest(q);
            removed = true;
        }

        return !removed;
    }
}

/// <summary>
/// A shared job that this player does not own may be activated by the owner while this
/// player is elsewhere. Base 3.1 then fails the receiver's copy as soon as StayWithin
/// evaluates it. 2.6 suppressed that failure presentation. Remove the receiver copy
/// silently instead; no grace applies because this is an ordinary boundary miss.
/// </summary>
[HarmonyPatch(typeof(ObjectivePOIStayWithin), nameof(ObjectivePOIStayWithin.UpdateState_Update))]
public static class RebirthSilentSharedPoiOutsidePatch
{
    [HarmonyPrefix]
    public static bool Prefix(ObjectivePOIStayWithin __instance)
    {
        if (__instance == null ||
            __instance.OwnerQuest == null ||
            __instance.OwnerQuest.SharedOwnerID == -1 ||
            !__instance.positionSet)
            return true;

        Quest quest = __instance.OwnerQuest;

        if (RebirthTraderQuestGraceManager.IsGraceActive(quest))
            return true;

        QuestJournal journal = quest.OwnerJournal;
        EntityPlayerLocal owner =
            journal != null ? journal.OwnerPlayer : null;

        if (owner == null)
            return true;

        Vector3 p = owner.position;
        p.y = p.z;

        if (__instance.outerRect.Contains(p))
            return true;

        journal.ForceRemoveQuest(quest);
        return false;
    }
}

[HarmonyPatch(typeof(ObjectiveStayWithin), nameof(ObjectiveStayWithin.Update))]
public static class RebirthSilentSharedStayWithinOutsidePatch
{
    [HarmonyPrefix]
    public static bool Prefix(ObjectiveStayWithin __instance)
    {
        if (__instance == null ||
            __instance.OwnerQuest == null ||
            __instance.OwnerQuest.SharedOwnerID == -1)
            return true;

        Quest quest = __instance.OwnerQuest;

        if (RebirthTraderQuestGraceManager.IsGraceActive(quest))
            return true;

        QuestJournal journal = quest.OwnerJournal;
        EntityPlayerLocal owner =
            journal != null ? journal.OwnerPlayer : null;

        if (owner == null)
            return true;

        Vector3 center = quest.Position;

        if (!__instance.positionSetup)
        {
            if (quest.GetPositionData(
                    out center,
                    Quest.PositionDataTypes.Location) ||
                quest.GetPositionData(
                    out center,
                    Quest.PositionDataTypes.POIPosition))
            {
                quest.Position = center;
                __instance.positionSetup = true;
            }
            else
            {
                return true;
            }
        }

        Vector3 playerPos = owner.position;
        playerPos.y = 0f;
        center.y = 0f;

        if ((playerPos - center).magnitude <= __instance.maxDistance)
            return true;

        journal.ForceRemoveQuest(quest);
        return false;
    }
}

/// <summary>
/// 2.6 parity: do not show the sharer's "shared with party" tooltip.
/// </summary>
[HarmonyPatch(typeof(PartyQuests), nameof(PartyQuests.ShareQuestWithParty))]
public static class RebirthSilentShareTooltipPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref bool _showTooltips)
    {
        _showTooltips = false;
    }
}

/// <summary>
/// 2.6 parity: AutoAccept/shared acceptance adds the receiver's quest without the
/// quest-started toast and sound.
/// </summary>
[HarmonyPatch(typeof(PartyQuests), nameof(PartyQuests.AcceptSharedQuest))]
public static class RebirthSilentAcceptSharedQuestPatch
{
    [HarmonyPrefix]
    public static bool Prefix(
        SharedQuestEntry _sharedQuest,
        EntityPlayerLocal _localPlayer)
    {
        if (_sharedQuest == null || _localPlayer == null)
            return false;

        QuestJournal journal = _localPlayer.QuestJournal;
        Quest quest = _sharedQuest.Quest;

        quest.RemoveMapObject();

        // Exact 2.6 behavior: notify:false.
        journal.AddQuest(quest, Quest.QuestSource.PartyShare, notify: false);

        journal.RemoveSharedQuestEntry(_sharedQuest);
        quest.AddSharedLocation(
            _sharedQuest.Position,
            _sharedQuest.Size);
        quest.SetPositionData(
            Quest.PositionDataTypes.QuestGiver,
            _sharedQuest.ReturnPos);
        quest.Position = _sharedQuest.Position;

        NetPackageSharedQuest package =
            NetPackageManager.GetPackage<NetPackageSharedQuest>()
                .Setup(
                    quest.QuestUniqueId,
                    quest.QuestCode,
                    _sharedQuest.SharedByPlayerID,
                    _localPlayer.entityId,
                    true);

        ConnectionManager connection =
            SingletonMonoBehaviour<ConnectionManager>.Instance;

        if (connection != null)
        {
            if (connection.IsServer)
                connection.SendPackage(
                    package,
                    _attachedToEntityId:
                        _sharedQuest.SharedByPlayerID);
            else
                connection.SendToServer(package);
        }

        return false;
    }
}

/// <summary>
/// 2.6 parity: mass shared-quest failures are removals, not audible/visible failures.
/// </summary>
[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.FailAllSharedQuests))]
public static class RebirthSilentFailAllSharedQuestsPatch
{
    [HarmonyPrefix]
    public static bool Prefix(QuestJournal __instance)
    {
        if (__instance == null || __instance.quests == null)
            return true;

        for (int i = __instance.quests.Count - 1; i >= 0; i--)
        {
            Quest q = __instance.quests[i];
            if (q == null ||
                q.SharedOwnerID == -1 ||
                q.CurrentState != Quest.QuestState.InProgress ||
                (int)q.CurrentPhase >=
                    (int)q.QuestClass.HighestPhase)
                continue;

            __instance.ForceRemoveQuest(q);
        }

        return false;
    }
}

/// <summary>
/// 3.1 added owner-side "player accepted/removed shared quest" toasts inside the
/// network package itself. Reproduce the 3.1 package state changes without those
/// presentation-only notifications.
/// </summary>
[HarmonyPatch(typeof(NetPackageSharedQuest), nameof(NetPackageSharedQuest.ProcessPackage))]
public static class RebirthSilentSharedQuestNetworkPatch
{
    [HarmonyPrefix]
    public static bool Prefix(
        NetPackageSharedQuest __instance,
        World _world,
        GameManager _callbacks)
    {
        if (__instance == null || _world == null)
            return false;

        NetPackageSharedQuest.SharedQuestData data =
            __instance.sharedQuestData;

        int questCode = data.questCode;
        int sharedBy = data.sharedByEntityID;
        int sharedWith = data.sharedWithEntityID;

        ConnectionManager connection =
            SingletonMonoBehaviour<ConnectionManager>.Instance;
        int authenticatedSender = __instance.Sender != null ? __instance.Sender.entityId : -1;
        if (connection != null && connection.IsServer && authenticatedSender != -1 &&
            (data.questEvent == NetPackageSharedQuest.SharedQuestData.SharedQuestEvents.RemoveQuest ||
             data.questEvent == NetPackageSharedQuest.SharedQuestData.SharedQuestEvents.AddSharedMember ||
             data.questEvent == NetPackageSharedQuest.SharedQuestData.SharedQuestEvents.RemoveSharedMember) &&
            authenticatedSender != sharedBy)
            return false;

        switch (data.questEvent)
        {
            case NetPackageSharedQuest.SharedQuestData.SharedQuestEvents.ShareQuest:
                if (connection.IsServer)
                    GameManager.Instance.QuestShareServer(data);
                else
                    GameManager.Instance.QuestShareClient(data);
                return false;

            case NetPackageSharedQuest.SharedQuestData.SharedQuestEvents.RemoveQuest:
                if (connection.IsServer)
                {
                    EntityPlayer owner =
                        GameManager.Instance.World.GetEntity(sharedBy)
                        as EntityPlayer;

                    if (owner == null || owner.Party == null)
                        return false;

                    for (int i = 0;
                         i < owner.Party.MemberList.Count;
                         i++)
                    {
                        EntityPlayer member =
                            owner.Party.MemberList[i];

                        if ((UnityEngine.Object)member ==
                            (UnityEngine.Object)owner)
                            continue;

                        if (member is EntityPlayerLocal)
                        {
                            member.QuestJournal
                                .RemoveSharedQuestByOwner(questCode);
                            member.QuestJournal
                                .RemoveSharedQuestEntry(questCode);
                        }
                        else
                        {
                            connection.SendPackage(
                                NetPackageManager
                                    .GetPackage<NetPackageSharedQuest>()
                                    .Setup(data.questUniqueId, questCode, sharedBy),
                                _attachedToEntityId:
                                    member.entityId);
                        }
                    }

                    return false;
                }

                System.Collections.Generic.List<EntityPlayerLocal>
                    locals =
                        GameManager.Instance.World.GetLocalPlayers();

                if (locals != null && locals.Count > 0)
                {
                    EntityPlayerLocal local = locals[0];
                    local.QuestJournal
                        .RemoveSharedQuestByOwner(questCode);
                    local.QuestJournal
                        .RemoveSharedQuestEntry(questCode);
                }

                return false;

            case NetPackageSharedQuest.SharedQuestData.SharedQuestEvents.AddSharedMember:
                if (connection.IsServer)
                {
                    EntityPlayer owner =
                        GameManager.Instance.World.GetEntity(sharedBy)
                        as EntityPlayer;

                    if (owner == null || owner.Party == null)
                        return false;

                    if (owner is EntityPlayerLocal localOwner)
                    {
                        EntityPlayer member =
                            GameManager.Instance.World
                                .GetEntity(sharedWith)
                            as EntityPlayer;

                        if (member == null)
                            return false;

                        Quest shared =
                            localOwner.QuestJournal
                                .GetSharedQuest(questCode);

                        if (shared != null)
                            shared.AddSharedWith(member);

                        return false;
                    }

                    connection.SendPackage(
                        NetPackageManager
                            .GetPackage<NetPackageSharedQuest>()
                            .Setup(
                                data.questUniqueId,
                                questCode,
                                sharedBy,
                                sharedWith,
                                true),
                        _attachedToEntityId: sharedBy);

                    return false;
                }

                EntityPlayer clientOwner =
                    GameManager.Instance.World
                        .GetEntity(sharedBy)
                    as EntityPlayer;

                EntityPlayerLocal clientLocal =
                    clientOwner as EntityPlayerLocal;

                if (clientLocal == null)
                    return false;

                EntityPlayer clientMember =
                    GameManager.Instance.World
                        .GetEntity(sharedWith)
                    as EntityPlayer;

                if (clientMember == null)
                    return false;

                Quest clientShared =
                    clientLocal.QuestJournal
                        .GetSharedQuest(questCode);

                if (clientShared != null)
                    clientShared.AddSharedWith(clientMember);

                return false;

            case NetPackageSharedQuest.SharedQuestData.SharedQuestEvents.RemoveSharedMember:
                if (connection.IsServer)
                {
                    EntityPlayer owner =
                        GameManager.Instance.World
                            .GetEntity(sharedBy)
                        as EntityPlayer;

                    if (owner == null)
                        return false;

                    if (owner is EntityPlayerLocal localOwner)
                    {
                        EntityPlayer member =
                            GameManager.Instance.World
                                .GetEntity(sharedWith)
                            as EntityPlayer;

                        if (member == null)
                            return false;

                        Quest shared =
                            localOwner.QuestJournal
                                .GetSharedQuest(questCode);

                        if (shared != null)
                            shared.RemoveSharedWith(member);

                        return false;
                    }

                    connection.SendPackage(
                        NetPackageManager
                            .GetPackage<NetPackageSharedQuest>()
                            .Setup(
                                data.questUniqueId,
                                questCode,
                                sharedBy,
                                sharedWith,
                                false),
                        _attachedToEntityId: sharedBy);

                    return false;
                }

                EntityPlayer clientOwner2 =
                    GameManager.Instance.World
                        .GetEntity(sharedBy)
                    as EntityPlayer;

                EntityPlayerLocal clientLocal2 =
                    clientOwner2 as EntityPlayerLocal;

                if (clientLocal2 == null)
                    return false;

                EntityPlayer clientMember2 =
                    GameManager.Instance.World
                        .GetEntity(sharedWith)
                    as EntityPlayer;

                if (clientMember2 == null)
                    return false;

                Quest clientShared2 =
                    clientLocal2.QuestJournal
                        .GetSharedQuest(questCode);

                if (clientShared2 != null)
                    clientShared2.RemoveSharedWith(clientMember2);

                return false;
        }

        return false;
    }
}
