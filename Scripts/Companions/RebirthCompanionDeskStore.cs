using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

// Saved UI intent only. Never enables the held work executors or alters inventory custody.
internal static class RebirthCompanionDeskStore
{
    private sealed class TargetImage { internal World World; internal XElement Desk; internal float RefreshAt; }
    private static readonly Dictionary<string, TargetImage> TargetImages = new Dictionary<string, TargetImage>(StringComparer.Ordinal);
    internal static XElement Parse(string value)
    {
        if (value == null || value.Length > 120000) throw new InvalidDataException();
        using (var reader = XmlReader.Create(new StringReader(value), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 120000 }))
            return XElement.Load(reader);
    }
    internal static string Text(XElement e, string name) => (string)e?.Attribute(name) ?? "";
    internal static long Revision(XElement e) => (long?)e.Attribute("revision") ?? 0;
    internal static string L(string id) => Localization.Get("xuiRebirthDesk" + id);
    private static XElement WithCropAssignments(XElement desk, RebirthNpcStableId npc, string actorId, string companionId = null)
    {
        var view = new XElement(desk);
        // Saved Organize policy comes from P10's per-NPC store; any desk-file orgRule rows from an older build are not shown.
        view.Elements("orgRule").Remove();
        RebirthNpcB09Policy orgPolicy; bool orgEnabled;
        if (RebirthNpcB09PolicyStore.TryRead(npc, out orgPolicy, out orgEnabled))
        {
            view.SetAttributeValue("orgEnabled", orgEnabled ? "1" : "0");
            view.SetAttributeValue("orgMerge", orgPolicy.Merge ? "1" : "0");
            view.SetAttributeValue("orgSortWithin", orgPolicy.SortWithinContainers ? "1" : "0");
            view.SetAttributeValue("orgDistant", orgPolicy.AllowDistant ? "1" : "0");
            view.SetAttributeValue("orgSort", orgPolicy.Sort != RebirthNpcB09SortOrder.None ? "1" : "0");
            // The executor gates: merge and within-container sort stay refused until their custody proofs are qualified.
            view.SetAttributeValue("orgMergeReady", RebirthNpcStorageOrganizationWork.MergeCustodyReady ? "1" : "0");
            view.SetAttributeValue("orgSortReady", RebirthNpcStorageOrganizationWork.SortCustodyReady ? "1" : "0");
            foreach (var row in OrgRuleRows(orgPolicy)) view.Add(row);
        }
        foreach (var assignment in RebirthNpcWorkAssignmentService.GetForNpc(npc, true)
            .Where(a => a.DefinitionId == RebirthNpcSingleCropWork.DefinitionId &&
                string.Equals(a.ActorId, actorId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.UpdatedUtcTicks).ThenByDescending(a => a.AssignmentId).Take(8))
            view.Add(new XElement("cropAssignment", new XAttribute("id", assignment.AssignmentId),
                new XAttribute("status", assignment.Status), new XAttribute("x", RebirthNpcSingleCropWork.Position(assignment).x),
                new XAttribute("y", RebirthNpcSingleCropWork.Position(assignment).y),
                new XAttribute("z", RebirthNpcSingleCropWork.Position(assignment).z)));
        // The same authoritative assignment service holds the other physical job kinds. Report them
        // with their real saved status so the dashboard and the Assignments tab never invent progress.
        // One assignment snapshot per read serves both the row list and the deposit lookup.
        var snapshot = RebirthNpcWorkAssignmentService.GetForNpc(npc, true).ToArray();
        foreach (var assignment in snapshot
            .Where(a => a.DefinitionId != RebirthNpcSingleCropWork.DefinitionId &&
                string.Equals(a.ActorId, actorId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.UpdatedUtcTicks).ThenByDescending(a => a.AssignmentId).Take(12))
            view.Add(new XElement("workAssignment", new XAttribute("id", assignment.AssignmentId),
                new XAttribute("def", assignment.DefinitionId), new XAttribute("status", assignment.Status),
                new XAttribute("deposit", assignment.DefinitionId == RebirthNpcRecipeQueueWork.DefinitionId && RebirthNpcStationQueueOriginal.TryReadPickupReceipt(npc, assignment.AssignmentId, out _) &&!HasDepositFor(snapshot, assignment.AssignmentId) ? "true" : "false"),
                new XAttribute("pos", assignment.HasTargetPosition ? "true" : "false"),
                new XAttribute("x", Mathf.FloorToInt(assignment.TargetPosition.x)),
                new XAttribute("y", Mathf.FloorToInt(assignment.TargetPosition.y)),
                new XAttribute("z", Mathf.FloorToInt(assignment.TargetPosition.z)),
                new XAttribute("detail", new string((assignment.Detail ?? "").Where(ch => !char.IsControl(ch)).Take(120).ToArray()))));
        view.SetAttributeValue("waterPercent", RebirthP8NpcWaterPolicy.GetPercent(npc));
        bool waterAuto = RebirthP8NpcWaterPolicy.GetEnabled(npc, out string waterFarm);
        view.SetAttributeValue("waterEnabled", waterAuto ? "1" : "0");
        view.SetAttributeValue("waterFarm", waterFarm ?? "");
        view.SetAttributeValue("waterStatus", new string((RebirthP8NpcWaterMaintainer.Status(npc) ?? "").Where(ch => !char.IsControl(ch)).Take(120).ToArray()));
        // Saved repair shortfall rows (P9): shown with the Repair job and in the Desk report detail, never invented.
        foreach (string line in RebirthP9RepairShortfall.ReadLines(npc).Take(8))
            view.Add(new XElement("shortfall", new string((line ?? "").Where(ch => !char.IsControl(ch)).Take(160).ToArray())));
        // N10: per-job shortfall / blocked-reason rows for Craft, Food, Farm and Organize jobs (job + kind attributes).
        foreach (var jobRow in RebirthCompanionJobShortfall.Build(view, npc, companionId, actorId).ToArray()) view.Add(jobRow);
        return view;
    }
    private static string PathFor(PlatformUserIdentifierAbs owner)
    {
        string root = GameIO.GetSaveGameDir();
        if (string.IsNullOrEmpty(root)) throw new InvalidOperationException(L("NoSave"));
        string key;
        using (var hash = SHA256.Create()) key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(owner.ToString()))).Replace("-", "");
        return Path.Combine(root, "RebirthCompanionDesk", key + ".xml");
    }
    private static XElement Load(string path)
    {
        if (!File.Exists(path)) return new XElement("desk", new XAttribute("schema", 1), new XAttribute("revision", 0));
        var doc = Parse(File.ReadAllText(path));
        if (doc.Name != "desk" || (int?)doc.Attribute("schema") != 1) throw new InvalidDataException();
        return doc;
    }
    internal static string Process(World world, EntityPlayer player, PlatformUserIdentifierAbs owner, long expected, string operation, string xml)
    {
        var response = new XElement("response");
        try
        {
            if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() || !ReferenceEquals(world, GameManager.Instance?.World)
                || player == null || player.IsDead() || RebirthNpcPersistenceCoordinator.IsRecoveryInstalling) throw new InvalidOperationException(L("Unavailable"));
            var roster = RebirthCompanionSnapshotService.Build(world, player, owner, "");
            string path = PathFor(owner);
            var doc = Load(path);
            if (operation == "recipes")
            {
                var query=Parse(xml);
                var companion=roster.Companions.FirstOrDefault(c=>c.Id==Text(query,"npc"));
                if(companion==null || companion.IsDog || companion.IsBoundUndead || companion.Kind!=RebirthCompanionTargetKind.Npc ||
                    !RebirthNpcStableId.TryParse(companion.StableId,out var stable) || !RebirthHumanProgression.TryGet(stable,out var subject))
                    throw new InvalidOperationException(L("HumanRequired"));
                string search=Text(query,"search");
                if(search.Length>64)throw new InvalidOperationException(L("Invalid"));
                int.TryParse(Text(query,"offset"),out var offset);offset=Math.Max(0,Math.Min(100000,offset));
                var known=XUiM_Recipes.GetRecipes().Where(recipe=>recipe!=null &&
                    (search.Length==0 || recipe.GetName().IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0 || Localization.Get(recipe.GetName()).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0) &&
                    RebirthCapabilityService.EvaluateNpcRecipe(subject,recipe.GetName()).IsAllowed).OrderBy(recipe=>recipe.GetName(),StringComparer.Ordinal).ToArray();
                offset=Math.Min(offset,Math.Max(0,known.Length-1));
                var page=new XElement("recipes",new XAttribute("offset",offset),new XAttribute("total",known.Length),new XAttribute("npc",companion.Id));
                foreach(var recipe in known.Skip(offset).Take(7)) page.Add(new XElement("recipe",new XAttribute("id",recipe.GetName())));
                response.Add(page);
            }
            else if (IsLiveOperation(operation))
            {
                // Live operations act on the worker's authoritative state and never edit the saved desk rows.
                var live = Parse(xml);
                var worker = roster.Companions.FirstOrDefault(c => c.Id == Text(live, "npc"));
                if (worker == null || worker.IsDog || worker.IsBoundUndead || worker.Kind != RebirthCompanionTargetKind.Npc ||
                    !RebirthNpcStableId.TryParse(worker.StableId, out var liveId))
                    throw new InvalidOperationException(L("HumanRequired"));
                ProcessLive(world, player, owner, operation, live, worker.StableId, liveId, response);
            }
            else if (operation != "read")
            {
                if (Revision(doc) != expected) throw new InvalidOperationException(L("Conflict"));
                var payload = Parse(xml);
                string npc = Text(payload, "npc");
                var companion = roster.Companions.FirstOrDefault(c => c.Id == npc);
                if (operation == "harvestOne" || operation == "waterAssign")
                {
                    if (companion == null || companion.IsDog || companion.IsBoundUndead ||
                        companion.Kind != RebirthCompanionTargetKind.Npc ||
                        !RebirthNpcStableId.TryParse(companion.StableId, out var cropWorker) ||
                        !RebirthNpcTradeReach.CanBegin(player, cropWorker) ||
                        !ReferenceEquals(world.GetEntity(player.entityId), player) ||
                        GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)?.PrimaryId?.Equals(owner) != true ||
                        !Guid.TryParseExact(Text(payload, "target"), "N", out _) ||
                        !doc.Elements("target").Any(t => Text(t, "id") == Text(payload, "target") && Text(t, "kind") == "Farm") ||
                        !int.TryParse(Text(payload, "x"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                        !int.TryParse(Text(payload, "y"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
                        !int.TryParse(Text(payload, "z"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var z) ||
                        !TryResolveTarget(world, owner, Text(payload, "target"), "Farm", out _, out var targetRevision) ||
                        targetRevision != expected)
                        throw new InvalidOperationException(L("Invalid"));
                    string session = null;
                    try
                    {
                        if (RebirthNpcInteractionSessionService.Open(owner.ToString(), cropWorker,
                            TimeSpan.FromSeconds(15), out var context) != RebirthNpcInteractionResult.Allowed || context == null)
                            throw new InvalidOperationException(L("Unavailable"));
                        session = context.SessionKey;
                        if (!RebirthNpcTradeReach.CanBegin(player, cropWorker) || Revision(Load(path)) != expected ||
                            !TryResolveTarget(world, owner, Text(payload, "target"), "Farm", out _, out targetRevision) ||
                            targetRevision != expected ||
                            AssignFarmWork(operation, session, Text(payload, "target"),
                                new Vector3i(x, y, z), out _, out _) != RebirthNpcWorkAssignmentResult.Succeeded)
                            throw new InvalidOperationException(L("Invalid"));
                    }
                    finally { if (session != null) RebirthNpcInteractionSessionService.Close(session); }
                    response.SetAttributeValue("message", L(operation == "waterAssign" ? "WaterAssigned" : "CropAssigned"));
                    response.Add(WithCropAssignments(doc, cropWorker, owner.ToString(), companion.Id));
                    var cropRoster = new XElement("companions");
                    foreach (var c in roster.Companions.Take(512))
                        cropRoster.Add(new XElement("npc", new XAttribute("id", c.Id), new XAttribute("name", c.Name ?? ""),
                            new XAttribute("status", c.Status ?? ""), new XAttribute("human", c.Kind == RebirthCompanionTargetKind.Npc && !c.IsDog && !c.IsBoundUndead),
                            new XAttribute("entity", c.EntityId), new XAttribute("order", c.Order ?? "")));
                    response.Add(cropRoster);
                    return response.ToString(SaveOptions.DisableFormatting);
                }
                if (operation == "save" || operation == "remove" || operation == "move")
                {
                    if (companion == null || companion.IsDog || companion.IsBoundUndead || companion.Kind != RebirthCompanionTargetKind.Npc)
                        throw new InvalidOperationException(L("HumanRequired"));
                }
                if(operation=="save" && Text(payload,"kind")=="Craft")
                {
                    if(!RebirthNpcStableId.TryParse(companion.StableId,out var stable) || !RebirthHumanProgression.TryGet(stable,out var subject) ||
                        !XUiM_Recipes.GetRecipes().Any(recipe=>recipe!=null && recipe.GetName()==Text(payload,"recipe") && RebirthCapabilityService.EvaluateNpcRecipe(subject,recipe.GetName()).IsAllowed))
                        throw new InvalidOperationException(L("ChooseKnownRecipe"));
                }
                if (operation == "removeTarget" && TargetReferencedByWork(roster.Companions, owner.ToString(), Text(payload, "id")))
                    throw new InvalidOperationException(L("TargetInUse"));
                var next = new XElement(doc);
                Mutate(next, operation, payload);
                // Only a redefinition (corners present) rebinds a camper; a rename keeps the saved witness untouched.
                if (operation == "target" && Text(payload, "camper").Length != 0 && (Text(payload, "id").Length == 0 || Text(payload, "ax").Length > 0 || Text(payload, "rebind") == "1"))
                    BindCamperTarget(world, player, owner, EditedOrLast(next, payload), payload);
                next.SetAttributeValue("revision", checked(Revision(doc) + 1));
                if (next.ToString(SaveOptions.DisableFormatting).Length > 90000) throw new InvalidOperationException(L("Capacity"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    { writer.Write(next.ToString(SaveOptions.DisableFormatting)); writer.Flush(); stream.Flush(true); }
                    if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                TargetImages.Remove(path);
                doc = next;
            }
            var requestedNpc = Text(Parse(xml), "npc");
            var projected = roster.Companions.FirstOrDefault(c => c.Id == requestedNpc &&
                c.Kind == RebirthCompanionTargetKind.Npc && !c.IsDog && !c.IsBoundUndead);
            response.Add(projected != null && RebirthNpcStableId.TryParse(projected.StableId, out var projectedId)
                ? WithCropAssignments(doc, projectedId, owner.ToString(), projected.Id) : new XElement(doc));
            var entries = new XElement("companions");
            foreach (var c in roster.Companions.Take(512))
                entries.Add(new XElement("npc", new XAttribute("id", c.Id), new XAttribute("name", c.Name ?? ""), new XAttribute("status", c.Status ?? ""),
                    new XAttribute("human", c.Kind == RebirthCompanionTargetKind.Npc && !c.IsDog && !c.IsBoundUndead), new XAttribute("entity", c.EntityId), new XAttribute("order", c.Order ?? "")));
            response.Add(entries);
            response.SetAttributeValue("message", operation == "read" || operation == "recipes" ? "" : IsLiveOperation(operation) ? Text(response, "message") : L("Saved"));
        }
        catch (Exception error)
        {
            response.SetAttributeValue("error", error is InvalidOperationException ? error.Message : L("StorageError"));
            Log.Warning("[REBIRTH Companion Desk] request failed: " + error.GetType().Name);
        }
        return response.ToString(SaveOptions.DisableFormatting);
    }
    private static RebirthNpcWorkAssignmentResult AssignFarmWork(string operation, string session, string target,
        Vector3i position, out RebirthNpcWorkAssignment assignment, out string detail)
    {
        string key = "desk:" + target + ":Farm";
        return operation == "waterAssign"
            ? RebirthP8NpcWaterRefillWork.Assign(session, key, position, out assignment, out detail)
            : RebirthNpcSingleCropWork.Assign(session, key, position, out assignment, out detail);
    }

    private static readonly HashSet<string> LiveOperations = new HashSet<string>(StringComparer.Ordinal)
    {
        "b09source", "b09dest", "b09commit", "b09clear", "b09deposit", "b09plan", "b09step",
        "orgRule", "orgRuleRemove", "orgEnabled", "orgFlag", "waterPolicy", "waterAuto", "resumeBase", "cancelAssignment", "craftCheck", "craftStage", "craftQueue"
    };
    internal static bool IsLiveOperation(string operation) => operation != null && LiveOperations.Contains(operation);

    // True when a deposit assignment (not cancelled or failed) already exists for this finished craft pickup.
    internal static bool HasDepositFor(RebirthNpcStableId npc, ulong queueAssignment) =>
        HasDepositFor(RebirthNpcWorkAssignmentService.GetForNpc(npc, true), queueAssignment);

    internal static bool HasDepositFor(IEnumerable<RebirthNpcWorkAssignment> assignments, ulong queueAssignment)
    {
        string token = RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(queueAssignment).ToString("N");
        return assignments.Any(a =>
            a.DefinitionId == RebirthNpcStorageOrganizationWork.DefinitionId && a.TargetKey != null &&
            a.TargetKey.StartsWith("storage-deposit:", StringComparison.Ordinal) && a.TargetKey.EndsWith(token, StringComparison.Ordinal) &&
            a.Status != RebirthNpcWorkAssignmentStatus.Cancelled && a.Status != RebirthNpcWorkAssignmentStatus.Failed);
    }

    private static string LF(string id, string fallback)
    {
        string value = L(id);
        return string.IsNullOrEmpty(value) || value == "xuiRebirthDesk" + id ? fallback : value;
    }

    // B09 selection is stored server side by interaction-session key, so keep one session per owner and
    // worker. A session lasts at most two minutes; an expired one is replaced and the selection restarts.
    private static readonly Dictionary<string, string> LiveSessions = new Dictionary<string, string>(StringComparer.Ordinal);
    private static string LiveSession(string owner, string stable, RebirthNpcStableId id)
    {
        string key = owner + "|" + stable;
        if (LiveSessions.TryGetValue(key, out var existing) &&
            RebirthNpcInteractionSessionService.Authorize(existing, owner, RebirthNpcInteractionAction.AssignWork, out var current) ==
                RebirthNpcInteractionResult.Allowed && current != null) return existing;
        DropLiveSession(key);
        // Cap per owner so one player cannot evict another's idle pick; evict only that owner's oldest entry.
        string ownerPrefix = owner + "|";
        while (LiveSessionOrder.Count(k => k.StartsWith(ownerPrefix, StringComparison.Ordinal)) >= 8)
            DropLiveSession(LiveSessionOrder.First(k => k.StartsWith(ownerPrefix, StringComparison.Ordinal)));
        if (RebirthNpcInteractionSessionService.Open(owner, id, TimeSpan.FromMinutes(2), out var context) !=
            RebirthNpcInteractionResult.Allowed || context == null) throw new InvalidOperationException(L("Unavailable"));
        LiveSessions[key] = context.SessionKey;
        LiveSessionOrder.Add(key);
        return context.SessionKey;
    }

    private static readonly List<string> LiveSessionOrder = new List<string>();

    // World change: close every live session and forget cached target images.
    internal static void ResetForWorldChange()
    {
        foreach (string key in LiveSessionOrder.ToArray()) DropLiveSession(key);
        LiveSessions.Clear(); LiveSessionOrder.Clear(); TargetImages.Clear();
    }
    private static void DropLiveSession(string key)
    {
        if (LiveSessions.TryGetValue(key, out var session)) RebirthNpcInteractionSessionService.Close(session);
        LiveSessions.Remove(key);
        LiveSessionOrder.Remove(key);
    }

    // Cancels one unstarted physical assignment (P6 CancelUnstarted). Started work keeps its custody and is refused.
    private static string CancelAssignment(string ownerText, string stable, RebirthNpcStableId id, string text)
    {
        if (!ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong assignmentId) ||
            !RebirthNpcWorkAssignmentService.TryGet(assignmentId, out var assignment) || assignment.NpcId != id ||
            !string.Equals(assignment.ActorId, ownerText, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(L("Invalid"));
        if (assignment.Status == RebirthNpcWorkAssignmentStatus.Completed || assignment.Status == RebirthNpcWorkAssignmentStatus.Cancelled ||
            assignment.Status == RebirthNpcWorkAssignmentStatus.Failed) return L("CancelFinished");
        // CancelUnstarted works under the release hold and refuses whenever any session, reservation, embodied owner,
        // retained custody or saved original exists, so no custody check is repeated here.
        var result = RebirthNpcWorkAssignmentService.CancelUnstarted(LiveSession(ownerText, stable, id), assignmentId, 0U, out _, out string detail);
        string reason = string.IsNullOrEmpty(detail) ? "" : ": " + detail;
        if (result == RebirthNpcWorkAssignmentResult.Succeeded) return L("CancelDone");
        // Coupling: CancelUnstarted reports "busy" and "custody held" both as Conflict (P6 RebirthP6AssignmentCancel.cs);
        // only its English error text tells them apart, so this match must follow that text.
        if (result == RebirthNpcWorkAssignmentResult.Conflict)
            return L(detail != null && detail.IndexOf("busy", StringComparison.OrdinalIgnoreCase) >= 0 ? "CancelHeldBusy" : "CancelHeldCustody") + reason;
        return L("CancelRefused") + reason;
    }

    // Recipe-only part of P7 RebirthNpcRecipeQueueWork.Eligible (the staged-ingredient part is checked by the API itself).
    private static string CraftIneligibleReason(RebirthNpcStableId id, string name)
    {
        var recipe = string.IsNullOrEmpty(name) ? null : CraftingManager.GetRecipe(name);
        if (recipe == null || !RebirthHumanRecipeReading.Knows(id, name)) return L("CraftNotKnown");
        // P7 owns the recipe-shape rules; its exact English refusal text is the single source (no local re-derivation).
        string refusal = RebirthNpcRecipeQueueWork.RefusalReason(recipe);
        return refusal == null ? "" : L("CraftNotEligible") + ": " + refusal;
    }

    private static RebirthNpcB09SelectionResult Deposit(string session, RebirthNpcStableId id, XElement query, Vector3i cell, int slot)
    {
        if (!ulong.TryParse(Text(query, "assignment"), NumberStyles.None, CultureInfo.InvariantCulture, out ulong assignment))
            throw new InvalidOperationException(L("Invalid"));
        // One deposit per finished pickup: a second request for the same receipt is refused here as well as by the executor.
        if (HasDepositFor(id, assignment))
            return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.AssignFailed, LF("B09DepositExists", "A deposit already exists for this output."));
        return RebirthNpcB09SelectionAdapter.TryCommitDeposit(session, RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(assignment), cell, slot, out _);
    }

    private static bool TryCell(XElement query, string prefix, out Vector3i cell)
    {
        cell = default(Vector3i);
        if (!int.TryParse(Text(query, prefix + "x"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ||
            !int.TryParse(Text(query, prefix + "y"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int y) ||
            !int.TryParse(Text(query, prefix + "z"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int z)) return false;
        cell = new Vector3i(x, y, z);
        return true;
    }

    // ---- Saved Organize policy: P10's per-NPC store (RebirthNpcB09PolicyStore) is the only storage. The Desk rows UI is a
    // projection of it (orgRule elements with synthesized ids) and every edit is a read-modify-write of that store.
    private static IEnumerable<XElement> OrgRuleRows(RebirthNpcB09Policy policy)
    {
        var rows = new List<XElement>();
        foreach (var spec in policy.Containers ?? new RebirthNpcB09ContainerSpec[0])
        {
            var tags = spec.PlayerTags != null && spec.PlayerTags.Length > 0 ? spec.PlayerTags : new[] { "" };
            foreach (string tag in tags) rows.Add(OrgRow("container", tag, "", spec.Position, true));
        }
        foreach (var pin in policy.ItemRules) rows.Add(OrgRow("pin", "", pin.Key, pin.Value, true));
        foreach (var rule in policy.TagRules) rows.Add(OrgRow("tagitem", rule.Value, rule.Key, default(Vector3i), false));
        foreach (string item in policy.PreserveItems) rows.Add(OrgRow("preserve", "", item, default(Vector3i), false));
        if (policy.HasDefaultContainer) rows.Add(OrgRow("default", "", "", policy.DefaultContainer, true));
        return rows;
    }

    private static XElement OrgRow(string rtype, string tag, string item, Vector3i at, bool hasPosition)
    {
        string position = hasPosition ? at.x.ToString(CultureInfo.InvariantCulture) + "," + at.y.ToString(CultureInfo.InvariantCulture) + "," + at.z.ToString(CultureInfo.InvariantCulture) : "";
        var row = new XElement("orgRule", new XAttribute("id", rtype + "|" + tag + "|" + item + "|" + position),
            new XAttribute("rtype", rtype), new XAttribute("tag", tag), new XAttribute("item", item));
        if (hasPosition) { row.SetAttributeValue("x", at.x); row.SetAttributeValue("y", at.y); row.SetAttributeValue("z", at.z); }
        return row;
    }

    // Same test P7's RebirthNpcRecipeQueueWork.StagedStillBound applies (that member is private): the saved staging still
    // describes cleanly and its credited bag stack is still bound, i.e. it is live and not yet consumed by a queue.
    private static bool StagedLive(RebirthNpcStableId npc, string owner, ulong stagedId, out RebirthNpcRecipeStagingWork.Target target,
        out RebirthNpcWorkNativeTransferOriginal.Record source)
    {
        target = null; source = null;
        try
        {
            RebirthNpcWorkAssignment described;
            RebirthNpcPersistentRecord person;
            if (!RebirthNpcRecipeStagingWork.TryDescribeSavedStationStage(npc, owner, stagedId, out described, out target, out source) ||
                !RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc, out person)) return false;
            var equipment = (person.UnknownOptionalElements ?? new string[0]).Select(XElement.Parse).SingleOrDefault(x => x.Name == "humanEquipment");
            string credit = source.CreditId.ToString("N");
            return equipment?.Element("bindings")?.Elements("binding").Any(b => (string)b.Attribute("area") == "bag" && (string)b.Attribute("id") == credit) == true;
        }
        catch (Exception) { return false; }
    }

    // Chooses the staged one-unit assignments to queue together. Gate off: only the clicked stage (unchanged behaviour).
    // Gate on: candidates are LIVE stagings of this worker at the same station and incarnation, same recipe and owner as the
    // clicked one (older consumed or stale-incarnation stagings never compete for a slot). Subsets that contain the clicked
    // stage are tried smallest first, then by ascending ids (max MaxUnits), and the first whose frames are an exact cover of
    // the recipe (RebirthNpcRecipeIngredientSet.ValidateStagedSet) wins. If none covers, refusal carries the reason.
    private static ulong[] StagedSetFor(RebirthNpcStableId id, string owner, ulong staged, out string refusal)
    {
        refusal = null;
        if (!RebirthNpcRecipeIngredientSet.Qualified) return new[] { staged };
        RebirthNpcRecipeStagingWork.Target clickedTarget; RebirthNpcWorkNativeTransferOriginal.Record clickedSource;
        RebirthNpcWorkAssignment first;
        if (!StagedLive(id, owner, staged, out clickedTarget, out clickedSource) || !RebirthNpcWorkAssignmentService.TryGet(staged, out first))
            return new[] { staged };   // AssignSet gives the exact refusal for a stage that is not live
        var others = new List<KeyValuePair<ulong, string>>();
        foreach (var candidate in RebirthNpcWorkAssignmentService.GetForNpc(id, false))
        {
            RebirthNpcRecipeStagingWork.Target target; RebirthNpcWorkNativeTransferOriginal.Record source;
            if (candidate.AssignmentId == staged || candidate.DefinitionId != RebirthNpcRecipeStagingWork.DefinitionId ||
                candidate.Status != RebirthNpcWorkAssignmentStatus.Completed || candidate.TargetPosition != first.TargetPosition ||
                !StagedLive(id, owner, candidate.AssignmentId, out target, out source)) continue;
            if (target.Incarnation != clickedTarget.Incarnation || target.Station != clickedTarget.Station ||
                target.Recipe.GetName() != clickedTarget.Recipe.GetName() || source.Owner != clickedSource.Owner) continue;
            others.Add(new KeyValuePair<ulong, string>(candidate.AssignmentId, source.Frame));
        }
        others.Sort((a, b) => a.Key.CompareTo(b.Key));
        // Bound the search by what the recipe can actually use: per ingredient item type keep at most the needed count of
        // the lowest-id live candidates (the clicked stage already fills one slot of its own type); drop unneeded types.
        // Needed units total at most MaxUnits (4), so at most 3 others remain: at most 2^3 = 8 subsets are ever validated.
        var need = new Dictionary<int, int>();
        foreach (var ingredient in clickedTarget.Recipe.ingredients)
        {
            int count;
            need.TryGetValue(ingredient.itemValue.type, out count);
            need[ingredient.itemValue.type] = count + ingredient.count;
        }
        var kept = new Dictionary<int, int>();
        kept[FrameItemType(clickedSource.Frame)] = 1;
        var filtered = new List<KeyValuePair<ulong, string>>();
        foreach (var live in others)
        {
            int type = FrameItemType(live.Value), have;
            if (!need.ContainsKey(type)) continue;
            kept.TryGetValue(type, out have);
            if (have >= need[type]) continue;
            kept[type] = have + 1;
            filtered.Add(live);
        }
        others = filtered;
        int max = Math.Min(RebirthNpcRecipeIngredientSet.MaxUnits - 1, others.Count);
        string lastReason = null;
        for (int size = 0; size <= max; size++)
        {
            var pick = new int[size];
            var found = FirstCover(clickedTarget.Recipe, clickedSource.Frame, others, pick, 0, 0, ref lastReason);
            if (found != null)
            {
                var ids = new List<ulong> { staged };
                foreach (int index in found) ids.Add(others[index].Key);
                ids.Sort();
                return ids.ToArray();
            }
        }
        refusal = lastReason ?? RebirthNpcRecipeIngredientSet.ValidateStagedSet(clickedTarget.Recipe, new List<string> { clickedSource.Frame });
        return new[] { staged };
    }

    private static int FrameItemType(string frame)
    {
        var stack = RebirthHumanEquipment.Decode(frame);
        return stack != null && stack.itemValue != null ? stack.itemValue.type : -1;
    }

    // Lexicographic search over index combinations of the given size; returns the first exact-cover combination.
    private static int[] FirstCover(Recipe recipe, string clickedFrame, List<KeyValuePair<ulong, string>> others, int[] pick, int slot, int start, ref string lastReason)
    {
        if (slot == pick.Length)
        {
            var frames = new List<string> { clickedFrame };
            foreach (int index in pick) frames.Add(others[index].Value);
            string reason = RebirthNpcRecipeIngredientSet.ValidateStagedSet(recipe, frames);
            if (reason == null) return (int[])pick.Clone();
            lastReason = reason;
            return null;
        }
        for (int i = start; i < others.Count; i++)
        {
            pick[slot] = i;
            var found = FirstCover(recipe, clickedFrame, others, pick, slot + 1, i + 1, ref lastReason);
            if (found != null) return found;
        }
        return null;
    }

    private static RebirthNpcB09ContainerSpec OrgSpec(RebirthNpcB09Policy policy, Vector3i at)
    {
        var list = new List<RebirthNpcB09ContainerSpec>(policy.Containers ?? new RebirthNpcB09ContainerSpec[0]);
        var spec = list.FirstOrDefault(s => s.Position == at);
        if (spec == null)
        {
            spec = new RebirthNpcB09ContainerSpec { Position = at, PlayerTags = new string[0] };
            list.Add(spec); policy.Containers = list.ToArray();
        }
        return spec;
    }

    // Read-modify-write one saved rule or the enabled flag. TryWrite validates every container against the live world
    // (named, owned, loaded) and its error text is shown through the normal message path.
    private static void ProcessOrg(EntityPlayer player, string operation, XElement query, RebirthNpcStableId id, XElement response)
    {
        RebirthNpcB09Policy policy; bool enabled;
        if (!RebirthNpcB09PolicyStore.TryRead(id, out policy, out enabled))
            throw new InvalidOperationException(LF("OrgPolicyUnreadable", "The saved organize policy could not be read."));
        if (operation == "orgEnabled") enabled = Text(query, "enabled") == "1";
        else if (operation == "orgFlag")
        {
            // Planner switches stored with the policy. Turning merge or within-container sort ON is refused with the
            // real reason while the executor's custody gates are closed; turning them OFF is always allowed.
            string flag = Text(query, "flag");
            bool on = Text(query, "value") == "1";
            if (flag == "merge")
            {
                if (on && !RebirthNpcStorageOrganizationWork.MergeCustodyReady)
                    throw new InvalidOperationException(LF("OrgMergeHeld", "Merging into partial stacks is held until its custody proof is qualified."));
                policy.Merge = on;
            }
            else if (flag == "sortWithin")
            {
                if (on && !RebirthNpcStorageOrganizationWork.SortCustodyReady)
                    throw new InvalidOperationException(LF("OrgSortHeld", "Sorting inside a container is held until its custody proof is qualified."));
                policy.SortWithinContainers = on;
            }
            else if (flag == "distant") policy.AllowDistant = on;
            else if (flag == "sort") policy.Sort = on ? RebirthNpcB09SortOrder.ByGroupThenName : RebirthNpcB09SortOrder.None;
            else throw new InvalidOperationException(L("Invalid"));
        }
        else if (operation == "orgRule")
        {
            string rtype = Text(query, "rtype"), tag = Text(query, "tag").Trim(), item = Text(query, "item").Trim();
            Vector3i at;
            bool hasPosition = TryCell(query, "", out at);
            if (rtype == "container" && hasPosition)
            {
                var spec = OrgSpec(policy, at);
                var tags = new List<string>(spec.PlayerTags ?? new string[0]);
                if (tag.Length > 0 && !tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))) tags.Add(tag);
                spec.PlayerTags = tags.ToArray();
            }
            else if (rtype == "pin" && hasPosition && item.Length > 0) { OrgSpec(policy, at); policy.ItemRules[item] = at; }
            else if (rtype == "tagitem" && tag.Length > 0 && item.Length > 0) policy.TagRules[item] = tag;
            else if (rtype == "preserve" && item.Length > 0) policy.PreserveItems.Add(item);
            else if (rtype == "default" && hasPosition) { OrgSpec(policy, at); policy.HasDefaultContainer = true; policy.DefaultContainer = at; }
            else throw new InvalidOperationException(L("Invalid"));
        }
        else
        {
            var row = OrgRuleRows(policy).FirstOrDefault(r => Text(r, "id") == Text(query, "id"));
            if (row == null) throw new InvalidOperationException(L("Conflict"));
            string rtype = Text(row, "rtype");
            if (rtype == "tagitem") policy.TagRules.Remove(Text(row, "item"));
            else if (rtype == "pin") policy.ItemRules.Remove(Text(row, "item"));
            else if (rtype == "preserve") policy.PreserveItems.Remove(Text(row, "item"));
            else if (rtype == "default") policy.HasDefaultContainer = false;
            else
            {
                Vector3i at;
                if (!TryCell(row, "", out at)) throw new InvalidOperationException(L("Invalid"));
                var spec = OrgSpec(policy, at);
                string tag = Text(row, "tag");
                // Removing a container's last visible row (its listing) unlists the container; pins to it must go first.
                if (tag.Length == 0) policy.Containers = policy.Containers.Where(s => s.Position != at).ToArray();
                else spec.PlayerTags = spec.PlayerTags.Where(t => !string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)).ToArray();
            }
        }
        string error;
        if (!RebirthNpcB09PolicyStore.TryWrite(player, id, policy, enabled, out error))
            throw new InvalidOperationException(string.IsNullOrEmpty(error) ? L("Invalid") : error);
        response.SetAttributeValue("message", LF("OrgSaved", "Organize policy saved."));
    }

    private static void ProcessPhysical(EntityPlayer player, PlatformUserIdentifierAbs owner, string operation, XElement query,
        string session, RebirthNpcStableId id, XElement response)
    {
        string message;
        bool ok = false;
        if (operation == "craftStage")
        {
            string recipe = Text(query, "recipe");
            string reason = CraftIneligibleReason(id, recipe);
            if (reason.Length > 0) message = reason;
            else if (!TryCell(query, "s", out var station) || !TryCell(query, "", out var source) ||
                !int.TryParse(Text(query, "slot"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int slot) || slot < 0 || slot > 255)
                throw new InvalidOperationException(L("Invalid"));
            else
            {
                var result = RebirthNpcRecipeStagingWork.Assign(session, recipe, station, source, slot, out _, out string detail);
                ok = result == RebirthNpcWorkAssignmentResult.Succeeded;
                message = ok ? L("CraftStaged") : L("CraftRefused") + (string.IsNullOrEmpty(detail) ? "" : ": " + detail);
            }
        }
        else if (operation == "craftQueue")
        {
            if (!ulong.TryParse(Text(query, "assignment"), NumberStyles.None, CultureInfo.InvariantCulture, out ulong staged))
                throw new InvalidOperationException(L("Invalid"));
            // A single staged id stays as is; with the set gate on, every completed staging of the same recipe and station, which
            // AssignSet validates as an exact cover of the recipe's ingredients (and refuses otherwise, with its reason).
            string selectorRefusal;
            ulong[] stagedIds = StagedSetFor(id, owner.ToString(), staged, out selectorRefusal);
            string detail = selectorRefusal;
            // No exact-cover subset exists: report why instead of calling the API with a set it would refuse.
            var result = selectorRefusal != null ? RebirthNpcWorkAssignmentResult.PermissionDenied
                : stagedIds.Length == 1 ? RebirthNpcRecipeQueueWork.Assign(session, staged, out _, out detail)
                : RebirthNpcRecipeQueueWork.AssignSet(session, stagedIds, out _, out detail);
            ok = result == RebirthNpcWorkAssignmentResult.Succeeded;
            message = ok ? L("CraftQueued") : L("CraftRefused") + (string.IsNullOrEmpty(detail) ? "" : ": " + detail);
        }
        else
        {
            RebirthNpcB09Policy policy; bool policyEnabled;
            if (!RebirthNpcB09PolicyStore.TryRead(id, out policy, out policyEnabled))
                throw new InvalidOperationException(LF("OrgPolicyUnreadable", "The saved organize policy could not be read."));
            RebirthNpcB09Plan plan;
            if (operation == "b09step")
            {
                // Runs exactly what the player saved (RebirthNpcB09PolicyStore), only while that saved policy is enabled.
                ok = RebirthNpcB09OrganizationDriver.TryDispatchSaved(session, out plan, out string detail);
                message = ok ? L("B09StepStarted") : L("B09StepNone") + (string.IsNullOrEmpty(detail) ? "" : ": " + detail);
            }
            else
            {
                plan = RebirthNpcB09OrganizationPlanner.Plan(player, policy, RebirthNpcB09SelectionAdapter.ClaimedCells(), 12);
                ok = true;
                message = string.Format(L("B09PlanSummary"), plan.Steps.Count, plan.Held.Count);
            }
            var preview = new XElement("b09plan");
            if (plan != null)
            {
                foreach (var step in plan.Steps.Take(12))
                    preview.Add(new XElement("step", new XAttribute("item", step.ItemName ?? ""), new XAttribute("rule", step.Rule.ToString()), new XAttribute("far", step.Far ? "true" : "false")));
                foreach (var held in plan.Held.Take(12))
                    preview.Add(new XElement("held", new XAttribute("item", held.ItemName ?? ""), new XAttribute("reason", held.Reason.ToString())));
            }
            response.Add(preview);
        }
        response.SetAttributeValue("ok", ok ? "true" : "false");
        response.SetAttributeValue("message", message);
    }

    private static void ProcessLive(World world, EntityPlayer player, PlatformUserIdentifierAbs owner, string operation,
        XElement query, string stable, RebirthNpcStableId id, XElement response)
    {
        string ownerText = owner.ToString();
        if (operation == "cancelAssignment")
        {
            response.SetAttributeValue("message", CancelAssignment(ownerText, stable, id, Text(query, "assignment")));
            return;
        }
        if (operation == "waterPolicy")
        {
            if (!int.TryParse(Text(query, "percent"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int percent) ||
                !RebirthP8NpcWaterPolicy.TrySetPercent(id, percent))
                throw new InvalidOperationException(L("Invalid"));
            response.SetAttributeValue("message", L("WaterPolicySaved"));
            return;
        }
        if (operation == "orgRule" || operation == "orgRuleRemove" || operation == "orgEnabled" || operation == "orgFlag")
        {
            ProcessOrg(player, operation, query, id, response);
            return;
        }
        if (operation == "waterAuto")
        {
            // Standing provider-water maintenance: the Farm target key must have exactly the shape
            // RebirthP8NpcWaterRefillWork.Shape accepts: desk:<saved Farm target id>:Farm.
            bool enable = Text(query, "enabled") == "1";
            bool wasEnabled = RebirthP8NpcWaterPolicy.GetEnabled(id, out string savedFarm);
            string farmKey = wasEnabled ? savedFarm : "";
            if (enable)
            {
                var farms = Load(PathFor(owner)).Elements("target").Where(t => Text(t, "kind") == "Farm" &&
                    Guid.TryParseExact(Text(t, "id"), "N", out _)).ToArray();
                string wanted = Text(query, "target");
                // A target id that was sent must still exist; only a request without an id falls back to the saved or first target.
                var chosen = wanted.Length > 0 ? farms.FirstOrDefault(t => Text(t, "id") == wanted) :
                    farms.FirstOrDefault(t => "desk:" + Text(t, "id") + ":Farm" == savedFarm) ?? farms.FirstOrDefault();
                if (chosen == null)
                {
                    response.SetAttributeValue("message", wanted.Length > 0 ? LF("WaterAutoTargetMissing", "That Farm target no longer exists.") :
                        LF("WaterAutoNoFarm", "Define a Farm target first."));
                    return;
                }
                farmKey = "desk:" + Text(chosen, "id") + ":Farm";
            }
            if (!RebirthP8NpcWaterPolicy.TryConfigure(id, RebirthP8NpcWaterPolicy.GetPercent(id), enable, farmKey))
                throw new InvalidOperationException(L("Invalid"));
            response.SetAttributeValue("message", enable ? LF("WaterAutoOn", "Auto-maintain water is on.") : LF("WaterAutoOff", "Auto-maintain water is off."));
            return;
        }
        if (operation == "resumeBase")
        {
            // Releases only the Follow override. Saved jobs, assignments and reports are left untouched.
            if (!RebirthNpcRuntimeRegistry.TryGetEntityId(id, out int entityId) ||
                !RebirthNpcRuntimeRegistry.TryGet(entityId, out var state) || !(world.GetEntity(entityId) is EntityRebirthNPC npc))
                throw new InvalidOperationException(L("Unavailable"));
            if (state.Order != RebirthNpcOrderState.Follow) { response.SetAttributeValue("message", L("ResumeBaseNotFollowing")); return; }
            var submitted = RebirthNpcCommandGateway.SubmitAuthenticated(npc, ownerText, player.entityId, RebirthNpcCommandKind.Clear,
                Vector3.zero, false, state.Revision);
            response.SetAttributeValue("message", submitted.Accepted ? L("ResumeBaseRequested") :
                LF("ResumeBaseRefused", "Follow could not be released") + (string.IsNullOrEmpty(submitted.Error) ? "" : ": " + submitted.Error));
            return;
        }
        if ((operation == "craftCheck" || operation == "craftStage" || operation == "craftQueue") && !CraftPickupAvailable)
        {
            // The pickup leg is held, so a staged craft could never deliver its output: do not admit it.
            response.SetAttributeValue("ok", "false");
            response.SetAttributeValue("message", LF("CraftPickupHeld", "Craft is held: collecting the finished item is not enabled yet."));
            return;
        }
        if (operation == "craftCheck")
        {
            string reason = CraftIneligibleReason(id, Text(query, "recipe"));
            response.SetAttributeValue("ok", reason.Length == 0 ? "true" : "false");
            response.SetAttributeValue("message", reason.Length == 0 ? L("CraftEligible") : reason);
            return;
        }
        if (operation == "craftStage" || operation == "craftQueue" || operation == "b09plan" || operation == "b09step")
        {
            ProcessPhysical(player, owner, operation, query, LiveSession(ownerText, stable, id), id, response);
            return;
        }
        string session = LiveSession(ownerText, stable, id);
        RebirthNpcB09SelectionResult result;
        if (operation == "b09clear") { RebirthNpcB09SelectionAdapter.Clear(session); DropLiveSession(ownerText + "|" + stable); result = RebirthNpcB09SelectionResult.Success(""); }
        else if (operation == "b09commit")
        {
            result = RebirthNpcB09SelectionAdapter.TryCommit(session, out _);
            if (result.Ok) DropLiveSession(ownerText + "|" + stable);
        }
        else
        {
            if (!int.TryParse(Text(query, "x"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ||
                !int.TryParse(Text(query, "y"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int y) ||
                !int.TryParse(Text(query, "z"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int z) ||
                !int.TryParse(Text(query, "slot"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int slot) || slot < 0 || slot > 1023)
                throw new InvalidOperationException(L("Invalid"));
            var cell = new Vector3i(x, y, z);
            result = operation == "b09source" ? RebirthNpcB09SelectionAdapter.TrySelectSourceCell(session, cell, slot)
                : operation == "b09dest" ? RebirthNpcB09SelectionAdapter.TrySelectDestinationCell(session, cell, slot)
                : operation == "b09deposit" ? Deposit(session, id, query, cell, slot)
                : throw new InvalidOperationException(L("Unavailable"));
        }
        response.SetAttributeValue("b09ok", result.Ok ? "true" : "false");
        response.SetAttributeValue("b09code", result.Code.ToString());
        response.SetAttributeValue("message", L(result.Ok
            ? (operation == "b09source" ? "B09SourceSelected" : operation == "b09dest" ? "B09DestinationSelected" :
               operation == "b09deposit" ? "B09DepositAssigned" : operation == "b09clear" ? "B09Cleared" : "B09Assigned")
            : "B09" + result.Code) + (!result.Ok && !string.IsNullOrEmpty(result.Detail) && result.Code == RebirthNpcB09SelectionCode.AssignFailed ? ": " + result.Detail : ""));
        var selection = new XElement("b09selection");
        if (RebirthNpcB09SelectionAdapter.TryGetSelection(session, out var snapshot))
        {
            selection.SetAttributeValue("hasSource", snapshot.HasSource ? "true" : "false");
            selection.SetAttributeValue("hasDestination", snapshot.HasDestination ? "true" : "false");
        }
        response.Add(selection);
    }

    private static void Mutate(XElement doc, string operation, XElement p)
    {
        string id = Text(p, "id"), npc = Text(p, "npc");
        XElement existing = doc.Elements("job").FirstOrDefault(j => Text(j, "id") == id && Text(j, "npc") == npc);
        if (operation == "save")
        {
            if (doc.Elements("job").Count() >= 256 && existing == null) throw new InvalidOperationException(L("Capacity"));
            string kind = Text(p, "kind");
            if (!new[] { "Craft", "Food", "Farm", "Repair", "Organize" }.Contains(kind)) throw new InvalidOperationException(L("Invalid"));
            if(kind=="Craft")
            {
                if(string.IsNullOrWhiteSpace(Text(p,"recipe")) || !int.TryParse(Text(p,"quantity"),out var quantity) || quantity<1 || quantity>10000 ||
                    !new[]{"Once","Repeat","Stock"}.Contains(Text(p,"mode"))) throw new InvalidOperationException(L("Invalid"));
            }
            string target = Text(p, "target");
            if ((kind == "Farm" || kind == "Repair") && !doc.Elements("target").Any(t => Text(t, "id") == target && Text(t, "kind") == (kind == "Farm" ? "Farm" : "Structure")))
                throw new InvalidOperationException(L("ChooseTarget"));
            if (kind == "Food" && Text(p, "meals") != "true" && Text(p, "drinks") != "true") throw new InvalidOperationException(L("ChooseFood"));
            string water = Text(p, "water");
            if (water.Length > 0)
            {
                var parts = water.Split(',');
                if (parts.Length != 3 || parts.Any(part => !int.TryParse(part, out var value) || value < -100000 || value > 100000))
                    throw new InvalidOperationException(L("Invalid"));
            }
            var row = new XElement("job", new XAttribute("id", existing == null ? Guid.NewGuid().ToString("N") : id), new XAttribute("npc", npc));
            foreach (string field in new[] { "kind", "target", "meals", "drinks", "limit", "mealCount", "drinkCount", "water", "fallback", "tag", "preserve", "merge", "sort", "storage", "recipe", "quantity", "mode" })
            {
                string value = Text(p, field); if (value.Length > 256) throw new InvalidOperationException(L("Invalid"));
                row.SetAttributeValue(field, value);
            }
            row.SetAttributeValue("priority", existing == null ? doc.Elements("job").Count(j => Text(j, "npc") == npc) + 1 : (int?)existing.Attribute("priority") ?? 1);
            bool changed = existing != null && !XNode.DeepEquals(existing, row);
            if (existing == null) doc.Add(row); else existing.ReplaceWith(row);
            string jobId = Text(row, "id");
            // Report the real release hold, not fabricated inventory shortfalls. A closed report
            // is retained so resaving the same assignment cannot immediately recreate it.
            if (!RebirthNpcWorkReleaseGate.Enabled && !doc.Elements("report").Any(r => Text(r, "job") == jobId && Text(r, "condition") == "releaseHold"))
                doc.Add(new XElement("report", new XAttribute("id", Guid.NewGuid().ToString("N")), new XAttribute("job", jobId), new XAttribute("npc", npc),
                    new XAttribute("condition", "releaseHold"), new XAttribute("state", "Unresolved"), new XAttribute("title", L("ExecutionHeld")),
                    new XAttribute("detail", L("ExecutionHeld")), new XAttribute("updated", DateTime.UtcNow.Ticks)));
            else if (changed && !RebirthNpcWorkReleaseGate.Enabled)
                foreach (var report in doc.Elements("report").Where(r => Text(r, "job") == jobId &&
                    Text(r, "condition") == "releaseHold" && Text(r, "state") == "Closed"))
                {
                    // A changed saved job is a new occurrence. A routine
                    // re-save of the identical row cannot reopen Force Close.
                    Transition(report, "Unresolved", "AssignmentChanged");
                    report.SetAttributeValue("read", false);
                    report.SetAttributeValue("clearWitness", null);
                }
        }
        else if (operation == "remove")
        {
            if (existing == null) throw new InvalidOperationException(L("Conflict"));
            existing.Remove();
            foreach (var r in doc.Elements("report").Where(r => Text(r, "job") == id && Text(r, "state") == "Unresolved"))
            { Transition(r, "Resolved", "AssignmentRemoved"); }
        }
        else if (operation == "move")
        {
            if (existing == null) throw new InvalidOperationException(L("Conflict"));
            var jobs = doc.Elements("job").Where(j => Text(j, "npc") == npc).OrderBy(j => (int?)j.Attribute("priority") ?? 1).ToList();
            int index = jobs.IndexOf(existing), other = index + (Text(p, "direction") == "up" ? -1 : 1);
            if (other >= 0 && other < jobs.Count) { jobs[index] = jobs[other]; jobs[other] = existing; }
            for (int i = 0; i < jobs.Count; i++) jobs[i].SetAttributeValue("priority", i + 1);
        }
        else if (operation == "target")
        {
            // A payload id edits the selected target in place (GUID preserved); no id creates a new row.
            if (Text(p, "id").Length > 0) { EditTarget(doc, p); return; }
            if (doc.Elements("target").Count() >= 128) throw new InvalidOperationException(L("Capacity"));
            string name = Text(p, "name").Trim(), kind = Text(p, "kind");
            if (name.Length < 1 || name.Length > 48 || !IsVolumeKind(kind)) throw new InvalidOperationException(L("Invalid"));
            var target = new XElement("target", new XAttribute("id", Guid.NewGuid().ToString("N")), new XAttribute("name", name), new XAttribute("kind", kind));
            foreach (string field in new[] { "ax", "ay", "az", "bx", "by", "bz" })
            {
                int value; if (!int.TryParse(Text(p, field), out value) || value < -100000 || value > 100000) throw new InvalidOperationException(L("Invalid"));
                target.SetAttributeValue(field, value);
            }
            foreach (string axis in new[] { "x", "y", "z" })
                if (Math.Abs((int)target.Attribute("a" + axis) - (int)target.Attribute("b" + axis)) > 256) throw new InvalidOperationException(L("Invalid"));
            doc.Add(target);
        }
        else if (operation == "removeTarget")
        {
            var row = doc.Elements("target").FirstOrDefault(t => Text(t, "id") == id);
            if (row == null) throw new InvalidOperationException(L("Conflict"));
            // A saved job that points at this target keeps working only if the target stays: refuse, never orphan it.
            if (doc.Elements("job").Any(j => Text(j, "target") == id)) throw new InvalidOperationException(L("TargetInUse"));
            row.Remove();
        }
        else if (operation == "recheckReport")
        {
            var report = doc.Elements("report").FirstOrDefault(r => Text(r, "id") == id);
            if (report == null) throw new InvalidOperationException(L("Conflict"));
            // Acknowledgement never resolves an issue. Recheck the actual condition.
            bool removed = !doc.Elements("job").Any(j => Text(j, "id") == Text(report, "job"));
            bool cleared = removed || (Text(report, "condition") == "releaseHold" && RebirthNpcWorkReleaseGate.Enabled);
            if (Text(report, "state") == "Unresolved" && cleared) Transition(report, "Resolved", removed ? "AssignmentRemoved" : "ConditionCleared");
            else if (Text(report, "state") == "Closed" && cleared)
                report.SetAttributeValue("clearWitness", 1);
            else if (!cleared && Text(report, "condition") == "releaseHold" &&
                (Text(report, "state") == "Resolved" ||
                 Text(report, "state") == "Closed" && Text(report, "clearWitness") == "1"))
            {
                // A saved clear observation followed by the real hold again
                // is a new occurrence. An unchanged closed issue stays closed.
                Transition(report, "Unresolved", "ConditionRecurred");
                report.SetAttributeValue("read", false);
                report.SetAttributeValue("clearWitness", null);
            }
            report.SetAttributeValue("checked", DateTime.UtcNow.Ticks);
        }
        else if (operation == "readReport" || operation == "closeReport" || operation == "archiveReport")
        {
            var report = doc.Elements("report").FirstOrDefault(r => Text(r, "id") == id);
            if (report == null) throw new InvalidOperationException(L("Conflict"));
            if (operation == "readReport") report.SetAttributeValue("read", true);
            else if (operation == "closeReport") { Transition(report, "Closed", "PlayerClosed"); }
            else { if (Text(report, "state") == "Unresolved") throw new InvalidOperationException(L("Invalid")); Transition(report, "Archived", "PlayerArchived"); }
        }
        else if (operation == "clearResolved")
            foreach (var r in doc.Elements("report").Where(r => Text(r, "state") == "Resolved")) Transition(r, "Archived", "ClearResolved");
        else throw new InvalidOperationException(L("Unavailable"));
        foreach (var group in doc.Elements("job").GroupBy(j => Text(j, "npc")))
        { int i = 0; foreach (var j in group.OrderBy(j => (int?)j.Attribute("priority") ?? 1)) j.SetAttributeValue("priority", ++i); }
    }
    private static void Transition(XElement report, string state, string reason)
    {
        string previous = Text(report, "state");
        if (previous == state) return;
        long now = DateTime.UtcNow.Ticks;
        report.Add(new XElement("transition", new XAttribute("from", previous), new XAttribute("to", state), new XAttribute("reason", reason), new XAttribute("at", now)));
        report.SetAttributeValue("state", state); report.SetAttributeValue("reason", reason); report.SetAttributeValue("updated", now);
    }

    // A camper binding is stored on the same desk target row. Fixed targets have
    // no camper attribute and retain their original absolute block coordinates.
    private static void BindCamperTarget(World world, EntityPlayer player, PlatformUserIdentifierAbs owner,
        XElement target, XElement payload)
    {
        if (!Guid.TryParse(Text(payload, "camper"), out var identity) || identity == Guid.Empty)
            throw new InvalidOperationException(L("Invalid"));
        var playerData = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if (playerData?.PrimaryId == null || !playerData.PrimaryId.Equals(owner))
            throw new InvalidOperationException(L("Invalid"));
        EntityVehicle camper = null;
        foreach (var entity in world.Entities.list)
        {
            if (!(entity is EntityVehicle candidate) || !RebirthCamperRadial.IsCamper(candidate) ||
                candidate.IsDead() || !ReferenceEquals(candidate.world, world) ||
                !ReferenceEquals(RebirthCamperClaimIntegration.OwnerOf(candidate), playerData) ||
                candidate.vehicle?.GetUpdatedItemValue() == null ||
                !candidate.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity", out string raw) ||
                !Guid.TryParse(raw, out var found) || found != identity) continue;
            if (camper != null) throw new InvalidOperationException(L("Invalid"));
            camper = candidate;
        }
        if (camper?.RootTransform == null) throw new InvalidOperationException(L("ChooseTarget"));
        var min = new Vector3((int)target.Attribute("ax"), (int)target.Attribute("ay"), (int)target.Attribute("az"));
        var max = new Vector3((int)target.Attribute("bx"), (int)target.Attribute("by"), (int)target.Attribute("bz"));
        // The picker records block coordinates. The second corner's entire
        // block belongs to the volume, including when both corners are equal.
        var low = Vector3.Min(min, max); var high = Vector3.Max(min, max) + Vector3.one;
        var localLow = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var localHigh = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? low.x : high.x,
                (i & 2) == 0 ? low.y : high.y, (i & 4) == 0 ? low.z : high.z);
            var local = camper.RootTransform.InverseTransformPoint(corner - Origin.position);
            localLow = Vector3.Min(localLow, local); localHigh = Vector3.Max(localHigh, local);
        }
        if (localLow.magnitude > 64f || localHigh.magnitude > 64f)
            throw new InvalidOperationException(L("Invalid"));
        target.SetAttributeValue("camper", identity.ToString("N"));
        string[] names = { "lax", "lay", "laz", "lbx", "lby", "lbz" };
        float[] values = { localLow.x, localLow.y, localLow.z, localHigh.x, localHigh.y, localHigh.z };
        for (int i = 0; i < names.Length; i++) target.SetAttributeValue(names[i],
            values[i].ToString("R", CultureInfo.InvariantCulture));
        // A local AABB is only a navigation envelope after a camper rotates.
        // Retain the initial transform so an action can map a current world
        // point back into the owner's exact original block selection.
        var initial = camper.RootTransform.localToWorldMatrix;
        var originAtBind = Origin.position;
        float[] originOffset = { originAtBind.x, originAtBind.y, originAtBind.z };
        for (int row = 0; row < 3; row++)
            for (int column = 0; column < 4; column++)
                target.SetAttributeValue("m" + row + column,
                    (initial[row, column] + (column == 3 ? originOffset[row] : 0f))
                        .ToString("R", CultureInfo.InvariantCulture));
    }

    // Mining and Logging volumes (B14) reuse the Farm/Structure row, camper binding and exact containment.
    // They only say where work may happen; destination and Follow policy stay undecided and held.
    // Gate for the physical Craft actions: the P7 pickup leg must be enabled, otherwise a staged craft could not deliver.
    internal static bool CraftPickupAvailable => RebirthNpcStationQueueOriginal.PickupAvailable;

    // Beyond saved jobs, a target is also in use when a worker's standing water policy names it or a live
    // (non-terminal) assignment's TargetKey is "desk:<id>:...".
    private static bool TargetReferencedByWork(IEnumerable<RebirthCompanionListEntry> companions, string ownerText, string targetId)
    {
        if (targetId.Length == 0) return false;
        string prefix = "desk:" + targetId + ":";
        foreach (var worker in companions)
        {
            if (worker.IsDog || worker.IsBoundUndead || worker.Kind != RebirthCompanionTargetKind.Npc ||
                !RebirthNpcStableId.TryParse(worker.StableId, out var workerId)) continue;
            if (RebirthP8NpcWaterPolicy.GetEnabled(workerId, out string farm) && farm != null && farm.StartsWith(prefix, StringComparison.Ordinal)) return true;
            foreach (var assignment in RebirthNpcWorkAssignmentService.GetForNpc(workerId, true))
                if (string.Equals(assignment.ActorId, ownerText, StringComparison.OrdinalIgnoreCase) && assignment.TargetKey != null &&
                    assignment.TargetKey.StartsWith(prefix, StringComparison.Ordinal) &&
                    assignment.Status != RebirthNpcWorkAssignmentStatus.Completed && assignment.Status != RebirthNpcWorkAssignmentStatus.Cancelled &&
                    assignment.Status != RebirthNpcWorkAssignmentStatus.Failed) return true;
        }
        return false;
    }

    private static XElement EditedOrLast(XElement doc, XElement payload)
    {
        string id = Text(payload, "id");
        return id.Length == 0 ? doc.Elements("target").Last() : doc.Elements("target").First(t => Text(t, "id") == id);
    }

    // Rename and/or redefine one saved target. The id (GUID) never changes, so jobs, the water policy and
    // assignment keys that name it keep pointing at it. Rename-only (no coordinates) keeps geometry and the camper
    // witness exactly; a redefinition replaces the corners and drops every camper field, which BindCamperTarget
    // then recomputes in the same atomic save when the request carries a camper (otherwise the target becomes fixed).
    private static readonly string[] CamperFields = BuildCamperFields();
    private static string[] BuildCamperFields()
    {
        var names = new List<string> { "camper", "lax", "lay", "laz", "lbx", "lby", "lbz" };
        for (int row = 0; row < 3; row++) for (int column = 0; column < 4; column++) names.Add("m" + row + column);
        return names.ToArray();
    }

    private static void EditTarget(XElement doc, XElement p)
    {
        string id = Text(p, "id");
        var target = doc.Elements("target").FirstOrDefault(t => Text(t, "id") == id);
        if (target == null) throw new InvalidOperationException(L("Conflict"));
        string name = Text(p, "name").Trim(), kind = Text(p, "kind");
        if (name.Length < 1 || name.Length > 48 || (kind.Length > 0 && kind != Text(target, "kind")))
            throw new InvalidOperationException(L("Invalid"));
        bool redefine = Text(p, "ax").Length > 0 || Text(p, "bx").Length > 0;
        // Owner-confirmed one-time upgrade of a legacy camper volume (saved without the m00..m23 matrix): corners stay,
        // BindCamperTarget recomputes the matrix from the camper's current transform. Only for that exact legacy shape and camper.
        if (Text(p, "rebind") == "1")
        {
            if (redefine || Text(target, "camper").Length == 0 || Text(target, "m00").Length != 0 ||
                !string.Equals(Text(target, "camper"), Text(p, "camper"), StringComparison.OrdinalIgnoreCase) ||
                !IsVolumeKind(Text(target, "kind"))) throw new InvalidOperationException(L("Invalid"));
        }
        var values = new Dictionary<string, int>();
        if (redefine)
        {
            foreach (string field in new[] { "ax", "ay", "az", "bx", "by", "bz" })
            {
                int value;
                if (!int.TryParse(Text(p, field), out value) || value < -100000 || value > 100000) throw new InvalidOperationException(L("Invalid"));
                values[field] = value;
            }
            foreach (string axis in new[] { "x", "y", "z" })
                if (Math.Abs(values["a" + axis] - values["b" + axis]) > 256) throw new InvalidOperationException(L("Invalid"));
        }
        target.SetAttributeValue("name", name);
        if (!redefine) return;
        foreach (var pair in values) target.SetAttributeValue(pair.Key, pair.Value);
        foreach (string field in CamperFields) target.SetAttributeValue(field, null);
    }

    internal static bool IsVolumeKind(string kind) =>
        kind == "Farm" || kind == "Structure" || kind == "Mining" || kind == "Logging";

    internal static bool TryResolveTarget(World world, PlatformUserIdentifierAbs owner, string targetId,
        string requiredKind, out Bounds bounds, out long deskRevision)
        => TryResolveTargetCore(world, owner, targetId, requiredKind, out bounds, out deskRevision,
            out _, out _);

    internal static bool TryContainsTargetPoint(World world, PlatformUserIdentifierAbs owner, string targetId,
        string requiredKind, Vector3 point, out long deskRevision)
    {
        deskRevision = 0;
        if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z) ||
            float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z) ||
            !TryResolveTargetCore(world, owner, targetId, requiredKind, out var bounds,
                out deskRevision, out var row, out var camper) || !bounds.Contains(point)) return false;
        if (camper == null) return true;
        // Older camper targets have only the enlarged local AABB. They need
        // redefinition before physical work can trust exact containment.
        var matrix = new float[12];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 4; c++)
            {
                if (!float.TryParse(Text(row, "m" + r + c), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var value) ||
                    float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 10000000f)
                    return false;
                matrix[r * 4 + c] = value;
            }
        var local = camper.RootTransform.InverseTransformPoint(point - Origin.position);
        var original = new Vector3(
            matrix[0] * local.x + matrix[1] * local.y + matrix[2] * local.z + matrix[3],
            matrix[4] * local.x + matrix[5] * local.y + matrix[6] * local.z + matrix[7],
            matrix[8] * local.x + matrix[9] * local.y + matrix[10] * local.z + matrix[11]);
        if (float.IsNaN(original.x) || float.IsNaN(original.y) || float.IsNaN(original.z) ||
            float.IsInfinity(original.x) || float.IsInfinity(original.y) || float.IsInfinity(original.z)) return false;
        var min = new Vector3(Math.Min((int)row.Attribute("ax"), (int)row.Attribute("bx")),
            Math.Min((int)row.Attribute("ay"), (int)row.Attribute("by")),
            Math.Min((int)row.Attribute("az"), (int)row.Attribute("bz")));
        var max = new Vector3(Math.Max((int)row.Attribute("ax"), (int)row.Attribute("bx")) + 1,
            Math.Max((int)row.Attribute("ay"), (int)row.Attribute("by")) + 1,
            Math.Max((int)row.Attribute("az"), (int)row.Attribute("bz")) + 1);
        return original.x >= min.x && original.x <= max.x &&
            original.y >= min.y && original.y <= max.y &&
            original.z >= min.z && original.z <= max.z;
    }

    private static bool TryResolveTargetCore(World world, PlatformUserIdentifierAbs owner, string targetId,
        string requiredKind, out Bounds bounds, out long deskRevision, out XElement target, out EntityVehicle vehicle)
    {
        bounds = default(Bounds); deskRevision = 0; target = null; vehicle = null;
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() ||
            !ReferenceEquals(world, GameManager.Instance?.World) || owner == null ||
            !Guid.TryParseExact(targetId, "N", out _) ||
            !IsVolumeKind(requiredKind) ||
            RebirthNpcPersistenceCoordinator.IsRecoveryInstalling) return false;
        try
        {
            string path = PathFor(owner);
            if (!TargetImages.TryGetValue(path, out var image) ||
                !ReferenceEquals(image.World, world) || Time.unscaledTime >= image.RefreshAt)
            {
                // Re-read the owner-scoped row at most four times a second.
                // Camper movement still resolves against its live transform below.
                if (TargetImages.Count >= 16) TargetImages.Clear();
                image = new TargetImage { World = world, Desk = Load(path), RefreshAt = Time.unscaledTime + .25f };
                TargetImages[path] = image;
            }
            var doc = image.Desk;
            var row = doc.Elements("target").SingleOrDefault(t => Text(t, "id") == targetId);
            if (row == null || Text(row, "kind") != requiredKind) return false;
            target = row;
            deskRevision = Revision(doc);
            int[] c = new int[6]; string[] fields = { "ax", "ay", "az", "bx", "by", "bz" };
            for (int i = 0; i < 6; i++)
                if (!int.TryParse(Text(row, fields[i]), NumberStyles.Integer, CultureInfo.InvariantCulture, out c[i]) ||
                    c[i] < -100000 || c[i] > 100000) return false;
            for (int i = 0; i < 3; i++) if (Math.Abs(c[i] - c[i + 3]) > 256) return false;
            var low = Vector3.Min(new Vector3(c[0], c[1], c[2]), new Vector3(c[3], c[4], c[5]));
            var high = Vector3.Max(new Vector3(c[0], c[1], c[2]), new Vector3(c[3], c[4], c[5])) + Vector3.one;
            string camperId = Text(row, "camper");
            if (camperId.Length == 0) { bounds.SetMinMax(low, high); return true; }
            if (!Guid.TryParseExact(camperId, "N", out var id) || id == Guid.Empty) return false;
            float[] local = new float[6]; string[] localFields = { "lax", "lay", "laz", "lbx", "lby", "lbz" };
            for (int i = 0; i < 6; i++)
                if (!float.TryParse(Text(row, localFields[i]), NumberStyles.Float, CultureInfo.InvariantCulture, out local[i]) ||
                    float.IsNaN(local[i]) || float.IsInfinity(local[i]) || Math.Abs(local[i]) > 64f) return false;
            for (int i = 0; i < 3; i++) if (local[i] > local[i + 3]) return false;
            EntityVehicle camper = null;
            foreach (var entity in world.Entities.list)
            {
                if (!(entity is EntityVehicle candidate) || !RebirthCamperRadial.IsCamper(candidate) ||
                    candidate.IsDead() || candidate.RootTransform == null ||
                    candidate.vehicle?.GetUpdatedItemValue() == null ||
                    !candidate.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity", out string raw) ||
                    !Guid.TryParse(raw, out var found) || found != id) continue;
                var playerData = RebirthCamperClaimIntegration.OwnerOf(candidate);
                if (playerData?.PrimaryId == null || !playerData.PrimaryId.Equals(owner) || camper != null) return false;
                camper = candidate;
            }
            if (camper == null) return false;
            vehicle = camper;
            low = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            high = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? local[0] : local[3],
                    (i & 2) == 0 ? local[1] : local[4], (i & 4) == 0 ? local[2] : local[5]);
                var point = camper.RootTransform.TransformPoint(corner) + Origin.position;
                low = Vector3.Min(low, point); high = Vector3.Max(high, point);
            }
            bounds.SetMinMax(low, high);
            return true;
        }
        catch (Exception error) when (error is IOException || error is XmlException ||
            error is InvalidDataException || error is InvalidOperationException ||
            error is FormatException || error is OverflowException) { return false; }
    }

}

[Preserve]
public sealed class NetPackageRebirthCompanionDeskRequest : NetPackage
{
    private int actor; private PlatformUserIdentifierAbs owner; private long token, revision; private string op, payload;
    private uint worldEpoch, connectionEpoch; private ulong generation;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public NetPackageRebirthCompanionDeskRequest Setup(int a, PlatformUserIdentifierAbs o, long t, long r, string operation, string xml)
    { actor = a; owner = o; token = t; revision = r; op = operation; payload = xml; worldEpoch = RebirthNpcNetworkEpoch.ClientEpoch; connectionEpoch = RebirthNpcNetworkEpoch.ClientConnectionEpoch; generation = RebirthNpcRecoveryProjectionScope.Current; return this; }
    public override void read(PooledBinaryReader reader)
    { var b = (BinaryReader)reader; worldEpoch = b.ReadUInt32(); connectionEpoch = b.ReadUInt32(); generation = b.ReadUInt64(); actor = b.ReadInt32(); owner = PlatformUserIdentifierAbs.FromStream(b); token = b.ReadInt64(); revision = b.ReadInt64(); op = RebirthSurvivorNetworkCodec.ReadBoundedString(b, 32); payload = RebirthSurvivorNetworkCodec.ReadBoundedString(b, 8000); }
    public override void write(PooledBinaryWriter writer)
    { base.write(writer); var b = (BinaryWriter)writer; b.Write(worldEpoch); b.Write(connectionEpoch); b.Write(generation); b.Write(actor); owner.ToStream(b); b.Write(token); b.Write(revision); RebirthSurvivorNetworkCodec.WriteString(b, op, 32); RebirthSurvivorNetworkCodec.WriteString(b, payload, 8000); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() || owner == null || !ValidEntityIdForSender(actor) || !ValidUserIdForSender(owner)
            || worldEpoch != RebirthNpcNetworkEpoch.GetServerEpoch() || !RebirthNpcNetworkEpoch.ValidateConnectionEpoch(actor, connectionEpoch)
            || generation != RebirthNpcRecoveryGeneration.RequireCurrent()) return;
        var player = world.GetEntity(actor) as EntityPlayer;
        var persistent = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor);
        if (persistent?.PrimaryId == null || !persistent.PrimaryId.Equals(owner)) return;
        string result = RebirthCompanionDeskStore.Process(world, player, owner, revision, op, payload);
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCompanionDeskResponse>().Setup(token, result, worldEpoch, connectionEpoch, generation), _attachedToEntityId: actor);
    }
    public int GetLength() => 0;
}

[Preserve]
public sealed class NetPackageRebirthCompanionDeskResponse : NetPackage
{
    private long token; private string payload;
    private uint worldEpoch, connectionEpoch; private ulong generation;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public NetPackageRebirthCompanionDeskResponse Setup(long t, string p, uint w, uint c, ulong g) { token = t; payload = p; worldEpoch = w; connectionEpoch = c; generation = g; return this; }
    public override void read(PooledBinaryReader reader) { var b = (BinaryReader)reader; worldEpoch = b.ReadUInt32(); connectionEpoch = b.ReadUInt32(); generation = b.ReadUInt64(); token = b.ReadInt64(); payload = RebirthSurvivorNetworkCodec.ReadBoundedString(b, 120000); }
    public override void write(PooledBinaryWriter writer) { base.write(writer); var b = (BinaryWriter)writer; b.Write(worldEpoch); b.Write(connectionEpoch); b.Write(generation); b.Write(token); RebirthSurvivorNetworkCodec.WriteString(b, payload, 120000); }
    public override void ProcessPackage(World world, GameManager callbacks)
    { if (world != null && world.IsRemote() && RebirthNpcNetworkEpoch.AcceptClientEpoch(worldEpoch) && connectionEpoch == RebirthNpcNetworkEpoch.ClientConnectionEpoch && RebirthNpcRecoveryProjectionScope.Observe(generation)) XUiC_RebirthCompanionDesk.Receive(world, token, payload); }
    public int GetLength() => 0;
}
