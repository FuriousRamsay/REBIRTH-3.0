using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class ConsoleCmdRebirthRemoteResources : ConsoleCmdAbstract
{
    public override string[] getCommands()
    {
        return new[] { "rbremoteresources", "rbrr" };
    }

    public override string getDescription()
    {
        return "Diagnoses REBIRTH Remote Resources discovery, eligibility, live source state, and cache refreshes.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbrr status [itemName]\n"
             + "  rbrr refresh\n"
             + "  rbrr debug on|off\n"
             + "\n"
             + "status  : lists every discovered source with distance, kind, revision, busy/eligible state and optional item count.\n"
             + "refresh : invalidates this player's snapshot and pushes a fresh ingredient projection.\n"
             + "debug   : enables/disables detailed [RemoteResources] transaction/source/event logging.\n";
    }

    public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
    {
        string command = parameters == null || parameters.Count == 0
            ? "status"
            : (parameters[0] ?? string.Empty).Trim().ToLowerInvariant();
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayer player = ResolveRequester(world, senderInfo);

        if (command == "help" || command == "?")
        {
            OutputMultiline(getHelp());
            return;
        }
        if (command == "debug")
        {
            if (parameters != null && parameters.Count > 1)
            {
                string value = (parameters[1] ?? string.Empty).Trim().ToLowerInvariant();
                if (value == "on" || value == "true" || value == "1") RemoteResourceDiagnostics.Enabled = true;
                else if (value == "off" || value == "false" || value == "0") RemoteResourceDiagnostics.Enabled = false;
            }
            Output("[RemoteResources] debug=" + RemoteResourceDiagnostics.Enabled);
            return;
        }
        if (player == null)
        {
            Output("[RemoteResources] No player could be resolved. Run the command from a client or provide a player-connected console session.");
            return;
        }
        if (command == "refresh")
        {
            RemoteResourceSnapshotCache.InvalidatePlayer(player.entityId);
            RemoteResourceAvailabilitySnapshot refreshed = RemoteResourceSnapshotCache.ForceRebuild(player);
            RemoteResourceLiveSync.PushSnapshot(player, "rbrr refresh");
            Output("[RemoteResources] refreshed player=" + player.entityId + " eligibleSources=" + refreshed.SourceCount);
            return;
        }
        if (command != "status")
        {
            Output("[RemoteResources] Unknown subcommand: " + command);
            OutputMultiline(getHelp());
            return;
        }

        string itemName = parameters != null && parameters.Count > 1 ? (parameters[1] ?? string.Empty).Trim() : string.Empty;
        ItemValue itemValue = string.IsNullOrEmpty(itemName) ? null : ItemClass.GetItem(itemName, false);
        if (!string.IsNullOrEmpty(itemName) && (itemValue == null || itemValue.IsEmpty()))
        {
            Output("[RemoteResources] Unknown item: " + itemName);
            return;
        }

        Output("[RemoteResources] player=" + player.entityId
            + " enabled=" + RemoteResourcesRuntimePolicy.Enabled
            + " radius=" + RemoteResourcesRuntimePolicy.Radius.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "m"
            + " quickStackRadius=" + QuickStackService.Radius.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "m"
            + (itemValue != null ? " item=" + itemName : string.Empty));

        List<IRemoteResourceSource> sources = RemoteResourceSourceDiscovery.ResolveNearby(world, player, RemoteResourcesRuntimePolicy.Radius);
        int eligible = 0;
        int totalQueried = 0;
        for (int i = 0; i < sources.Count; i++)
        {
            IRemoteResourceSource source = sources[i];
            string reason;
            bool canUse = RemoteResourceEligibility.CanUse(source, player, false, false, out reason);
            int sourceItemCount = itemValue != null ? CountItem(source, itemValue) : CountAll(source);
            if (canUse) eligible++;
            if (canUse && itemValue != null) totalQueried += sourceItemCount;
            float distance = Vector3.Distance(player.position, source.Position);
            Output("[RemoteResources] source=" + source.StableId
                + " kind=" + source.Kind
                + " name='" + source.DisplayName + "'"
                + " distance=" + distance.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "m"
                + " revision=" + source.Revision
                + " busy=" + source.IsBusy
                + " activated=" + RemoteResourceEligibility.IsActivatedForUse(source, player)
                + " eligible=" + canUse
                + " count=" + sourceItemCount
                + " reason='" + (canUse ? "eligible" : reason) + "'");
        }
        RemoteResourceAvailabilitySnapshot snapshot = RemoteResourceSnapshotCache.ForceRebuild(player);
        Output("[RemoteResources] summary discovered=" + sources.Count + " eligible=" + eligible
            + " snapshotSources=" + snapshot.SourceCount
            + (itemValue != null ? " liveItemTotal=" + totalQueried + " snapshotItemTotal=" + snapshot.GetCount(itemValue) : string.Empty));
    }

    private static int CountItem(IRemoteResourceSource source, ItemValue itemValue)
    {
        int result = 0;
        ItemStack[] slots = source != null ? source.Slots : null;
        PackedBoolArray locks = source != null ? source.SlotLocks : null;
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            if (locks != null && i < locks.Length && locks[i]) continue;
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty() || stack.count <= 0 || stack.itemValue.type != itemValue.type) continue;
            if (stack.itemValue.HasModSlots && stack.itemValue.HasMods()) continue;
            result += stack.count;
        }
        return result;
    }

    private static int CountAll(IRemoteResourceSource source)
    {
        int result = 0;
        ItemStack[] slots = source != null ? source.Slots : null;
        PackedBoolArray locks = source != null ? source.SlotLocks : null;
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            if (locks != null && i < locks.Length && locks[i]) continue;
            ItemStack stack = slots[i];
            if (stack != null && !stack.IsEmpty() && stack.count > 0) result += stack.count;
        }
        return result;
    }

    private static EntityPlayer ResolveRequester(World world, CommandSenderInfo senderInfo)
    {
        if (world == null) return null;
        if (senderInfo.RemoteClientInfo != null)
            return world.GetEntity(senderInfo.RemoteClientInfo.entityId) as EntityPlayer;
        return world.GetPrimaryPlayer();
    }

    private static void Output(string text)
    {
        SdtdConsole.Instance.Output(text);
        Log.Out(text);
    }

    private static void OutputMultiline(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        string[] lines = text.Replace("\r", string.Empty).Split('\n');
        for (int i = 0; i < lines.Length; i++) if (lines[i].Length > 0) Output(lines[i]);
    }
}
