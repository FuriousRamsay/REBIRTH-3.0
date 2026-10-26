using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

// B09 "Organize storage": client picker for one exact source item cell and one empty destination cell.
// While a pick is active, a press on a presented container slot of the Rebirth context workspace is
// routed to the server selection adapter (RebirthNpcB09SelectionAdapter, via the Desk endpoint) and the
// native item-stack handlers are skipped, so choosing a cell never picks up, swaps, drops or moves an item.
// The selection is intent only; the single mutating step is the commit (Enter), which the server
// revalidates and forwards to the existing exact-cell storage executor.
internal static class RebirthCompanionB09Selection
{
    private static EntityPlayerLocal player;
    private static string companionId = "";
    private static bool hasSource, hasDestination, requestPending, sawLoot;
    // move = B09 one-item move; craft = choose the ingredient cell for a P7 staging assignment; deposit = choose the empty cell for finished output.
    private static string mode = "move", craftRecipe = "", assignmentId = "";
    private static Vector3i craftStation;
    private static float requestAt, startedAt;
    private static int craftUnitsLeft = 1;
    private static string armedOperation = "", orgType = "", orgTag = "";
    private static XElement armedPayload;
    private static RebirthCompanionB09SelectionRunner runner;

    internal static bool Active => runner != null && player != null;

    internal static void Begin(EntityPlayerLocal actor, string companion)
    {
        End(false);
        if (actor == null || string.IsNullOrEmpty(companion)) return;
        player = actor; companionId = companion; hasSource = hasDestination = requestPending = false; mode = "move";
        startedAt = Time.unscaledTime; armedPayload = null;
        runner = new GameObject("REBIRTH B09 storage selection").AddComponent<RebirthCompanionB09SelectionRunner>();
        Tip(RebirthCompanionDeskStore.L("B09PickSource"));
    }

    internal static void BeginCraft(EntityPlayerLocal actor, string companion, string recipe, Vector3i station)
    {
        Begin(actor, companion);
        if (!Active) return;
        mode = "craft"; craftRecipe = recipe ?? ""; craftStation = station;
        // One staging assignment per one-unit ingredient frame. Gate off (or a single-unit recipe): exactly one pick, as before.
        craftUnitsLeft = 1;
        if (RebirthNpcRecipeIngredientSet.Qualified)
        {
            var native = CraftingManager.GetRecipe(craftRecipe);
            int units = 0;
            if (native?.ingredients != null) foreach (var ingredient in native.ingredients) if (ingredient != null) units += ingredient.count;
            craftUnitsLeft = Math.Max(1, Math.Min(RebirthNpcRecipeIngredientSet.MaxUnits, units));
        }
        Tip(RebirthCompanionDeskStore.L("CraftPickSource"));
    }

    internal static void BeginDeposit(EntityPlayerLocal actor, string companion, string assignment)
    {
        Begin(actor, companion);
        if (!Active) return;
        mode = "deposit"; assignmentId = assignment ?? "";
        Tip(RebirthCompanionDeskStore.L("B09PickDepositDestination"));
    }

    // Pick for a saved Organize rule: orgKind container (tag a container), tagitem (item -> tag) or pin (item -> container).
    internal static void BeginOrg(EntityPlayerLocal actor, string companion, string orgKind, string tag)
    {
        Begin(actor, companion);
        if (!Active) return;
        mode = "org"; orgType = orgKind ?? ""; orgTag = tag ?? "";
        Tip(RebirthCompanionDeskStore.L(orgType == "container" || orgType == "default" ? "OrgPickContainer" : "OrgPickItem"));
    }

    internal static void End(bool clearServer)
    {
        if (clearServer && mode == "move" && player != null && !string.IsNullOrEmpty(companionId))
            XUiC_RebirthCompanionDesk.RequestOperation(player, "b09clear",
                new XElement("command", new XAttribute("npc", companionId)), 0, ignored => { });
        if (runner != null) UnityEngine.Object.Destroy(runner.gameObject);
        runner = null; player = null; companionId = ""; hasSource = hasDestination = requestPending = sawLoot = false;
        armedPayload = null; armedOperation = "";
    }

    private static void Tip(string text)
    {
        if (player != null && !string.IsNullOrEmpty(text)) GameManager.ShowTooltip(player, text, true, false, 3f);
    }

    // Used by the click patches. Only presented container slots of the local actor's own workspace qualify.
    internal static bool Intercepts(XUiC_ItemStack stack)
    {
        var slot = stack as XUiC_RebirthContextSlot;
        return Active && slot != null && slot.IsPresented && !slot.IsBackpack &&
            ReferenceEquals(slot.xui?.playerUI?.entityPlayer, player) && LootOpen();
    }

    internal static void OnSlotPress(XUiC_RebirthContextSlot slot, int button)
    {
        if (!Intercepts(slot) || PendingBlocks() || (button != 0 && button != -1)) return;
        var te = XUiC_LootWindowGroup.GetInstance(slot.xui)?.lootWindow?.te;
        if (te == null) { Tip(RebirthCompanionDeskStore.L("B09OpenContainer")); return; }
        Vector3i position = te.ToWorldPos();
        // An occupied cell chooses (or replaces) the source; an empty cell chooses the destination.
        bool destination = slot.ItemStack == null || slot.ItemStack.IsEmpty();
        string operation;
        var payload = new XElement("command", new XAttribute("npc", companionId),
            new XAttribute("x", position.x.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("y", position.y.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("z", position.z.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("slot", slot.SlotNumber.ToString(CultureInfo.InvariantCulture)));
        if (mode == "craft")
        {
            if (destination) { Tip(RebirthCompanionDeskStore.L("CraftPickSource")); return; }
            operation = "craftStage";
            payload.SetAttributeValue("recipe", craftRecipe);
            payload.SetAttributeValue("sx", craftStation.x.ToString(CultureInfo.InvariantCulture));
            payload.SetAttributeValue("sy", craftStation.y.ToString(CultureInfo.InvariantCulture));
            payload.SetAttributeValue("sz", craftStation.z.ToString(CultureInfo.InvariantCulture));
        }
        else if (mode == "org")
        {
            // Saved Organize policy rule: a container (any cell), or an occupied cell that names the item.
            bool needsItem = orgType == "tagitem" || orgType == "pin" || orgType == "preserve";
            if (needsItem && destination) { Tip(RebirthCompanionDeskStore.L("OrgPickItem")); return; }
            operation = "orgRule";
            payload.SetAttributeValue("rtype", orgType);
            payload.SetAttributeValue("tag", orgTag);
            if (needsItem) payload.SetAttributeValue("item", slot.ItemStack.itemValue.ItemClass.GetItemName());
        }
        else if (mode == "deposit")
        {
            if (!destination) { Tip(RebirthCompanionDeskStore.L("B09PickDepositDestination")); return; }
            operation = "b09deposit";
            payload.SetAttributeValue("assignment", assignmentId);
        }
        else
        {
            if (destination && !hasSource) { Tip(RebirthCompanionDeskStore.L("B09PickSource")); return; }
            operation = destination ? "b09dest" : "b09source";
        }
        if (mode != "move")
        {
            // One click only arms the assignment; Enter confirms it.
            armedOperation = operation; armedPayload = payload;
            Tip(RebirthCompanionDeskStore.L("PickConfirm"));
            return;
        }
        requestPending = true; requestAt = Time.unscaledTime;
        if (!XUiC_RebirthCompanionDesk.RequestOperation(player, operation, payload, 0, Apply)) requestPending = false;
    }

    internal static void Commit()
    {
        if (!Active || PendingBlocks()) return;
        if (mode != "move")
        {
            // Craft and deposit send their single assignment only after this explicit confirmation.
            if (armedPayload == null) return;
            var payloadToSend = armedPayload; string operationToSend = armedOperation; armedPayload = null;
            requestPending = true; requestAt = Time.unscaledTime;
            if (!XUiC_RebirthCompanionDesk.RequestOperation(player, operationToSend, payloadToSend, 0, Apply)) requestPending = false;
            return;
        }
        if (!hasSource) { Tip(RebirthCompanionDeskStore.L("B09PickSource")); return; }
        if (!hasDestination) { Tip(RebirthCompanionDeskStore.L("B09PickDestination")); return; }
        requestPending = true; requestAt = Time.unscaledTime;
        var actor = player;
        if (!XUiC_RebirthCompanionDesk.RequestOperation(actor, "b09commit",
            new XElement("command", new XAttribute("npc", companionId)), 0, response =>
            {
                requestPending = false;
                Tip(MessageOf(response));
                if (response?.Attribute("b09ok")?.Value == "true") End(false); else Resync(response);
            })) requestPending = false;
    }

    // A lost reply must not freeze the pick: give up on it after 8 s.
    private static bool PendingBlocks()
    {
        if (requestPending && Time.unscaledTime - requestAt > 8f) requestPending = false;
        return requestPending;
    }

    private static string MessageOf(XElement response)
    {
        string error = (string)response?.Attribute("error");
        return !string.IsNullOrEmpty(error) ? error : (string)response?.Attribute("message") ?? "";
    }

    private static void Apply(XElement response)
    {
        requestPending = false;
        if (!Active) return;
        Tip(MessageOf(response));
        if (mode != "move")
        {
            if (mode == "craft" && (string)response?.Attribute("ok") == "true" && --craftUnitsLeft > 0)
            {
                // More one-unit ingredient frames to stage for this recipe: stay in the pick.
                Tip(RebirthCompanionDeskStore.L("CraftPickNext"));
                return;
            }
            bool saved = mode == "org" && string.IsNullOrEmpty((string)response?.Attribute("error"));
            if (saved || (string)response?.Attribute("ok") == "true" || (string)response?.Attribute("b09ok") == "true") End(false);
            return;
        }
        Resync(response);
    }

    // The server selection is authoritative: adopt its flags after every reply, including a failed commit.
    private static void Resync(XElement response)
    {
        var selection = response?.Element("b09selection");
        if (selection == null) { hasSource = hasDestination = false; return; }
        hasSource = (string)selection.Attribute("hasSource") == "true";
        hasDestination = (string)selection.Attribute("hasDestination") == "true";
        if (hasSource && hasDestination) Tip(RebirthCompanionDeskStore.L("B09PressEnter"));
    }

    internal static void Verify()
    {
        if (!Active) return;
        var world = GameManager.Instance?.World;
        if (player.IsDead() || !ReferenceEquals(player.world, world)) { End(false); return; }
        // The pick belongs to the container the player opened for it: end it when that window closes.
        if (LootOpen()) sawLoot = true;
        else if (sawLoot) End(true);
        // The server selection session lasts two minutes; stop a little earlier so the flags never outlive it.
        else if (Time.unscaledTime - startedAt > 110f) { Tip(RebirthCompanionDeskStore.L("PickExpired")); End(true); }
    }

    private static bool LootOpen() =>
        LocalPlayerUI.GetUIForPlayer(player)?.windowManager?.IsWindowOpen(XUiC_LootWindowGroup.ID) == true;
}

[Preserve]
internal sealed class RebirthCompanionB09SelectionRunner : MonoBehaviour
{
    private void Update()
    {
        RebirthCompanionB09Selection.Verify();
        if (!RebirthCompanionB09Selection.Active) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { RebirthCompanionB09Selection.End(true); return; }
        // Enter typed into a focused text field (for example a container search) must never confirm a move.
        if (UIInput.selection != null) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) RebirthCompanionB09Selection.Commit();
    }
}

// Skips the native inventory handlers for a pick press. Parameterless XUiC_ItemStack members, as already
// patched by RebirthInventoryGestureBoundaryTrace; a missing member is skipped rather than failing the patch.
[HarmonyPatch]
internal static class RebirthCompanionB09NonMutatingClickPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "HandleStackSwap", "HandlePartialStackPickup", "HandleDropOne", "SwapItem", "HandleMoveToPreferredLocation" })
        {
            var method = AccessTools.DeclaredMethod(typeof(XUiC_ItemStack), name, Type.EmptyTypes);
            if (method != null) yield return method;
        }
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_ItemStack __instance)
    {
        if (!RebirthCompanionB09Selection.Intercepts(__instance)) return true;
        // Clear the native click latch too, so HandleClickComplete cannot act on this press after the pick ends.
        __instance.lastClicked = false;
        return false;
    }
}
