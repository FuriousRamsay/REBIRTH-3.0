using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

#nullable disable

// B14 Work-runtime wiring: purpose class (definition, shape, Allows*, AllowsOutcome), the volume/escape-aware
// target adapter, and the Mining work adapter that drives RebirthNpcB14HarvestExecutor. Pattern follows
// RebirthNpcSingleCropWork / RebirthNpcStorageOrganizationWork. Everything stays behind
// RebirthNpcBlockMiningAdapter.NativeHarvestQualified (false).

internal static class RebirthNpcB14MiningWork
{
    internal const string DefinitionId = "rebirth.mining.harvest-one";
    // RebirthNpcMiningWorkExecutor.ExecutorId (RebirthNpcConcreteWorkExecutors.cs).
    internal const string ExecutorId = "rebirth.work.mining";

    internal static bool IsDefinition(string definition) { return definition == DefinitionId; }

    internal static void Register()
    {
        RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(DefinitionId, RebirthNpcWorkKind.Mining,
            RebirthNpcWorkCapability.Mining, 300, 1, TimeSpan.FromSeconds(60), false), true);
    }

    // Target key: "b14:<M|L>:<desk target id N>:<expected block name, ':' as %3A>".
    internal static string BuildKey(RebirthNpcB14Kind kind, string volumeTargetId, string blockName)
    {
        return "b14:" + (kind == RebirthNpcB14Kind.Logging ? "L" : "M") + ":" + (volumeTargetId ?? string.Empty) + ":" +
            (blockName ?? string.Empty).Trim().Replace(":", "%3A");
    }

    internal static bool TryParse(string key, out RebirthNpcB14Kind kind, out string volume, out string block)
    {
        kind = RebirthNpcB14Kind.Mining; volume = string.Empty; block = string.Empty;
        if (key == null || key.Length > 256) return false;
        string[] p = key.Split(':');
        Guid id;
        if (p.Length != 4 || p[0] != "b14" || (p[1] != "M" && p[1] != "L") ||
            !Guid.TryParseExact(p[2], "N", out id) || id == Guid.Empty || p[3].Length == 0) return false;
        kind = p[1] == "L" ? RebirthNpcB14Kind.Logging : RebirthNpcB14Kind.Mining;
        volume = p[2]; block = p[3].Replace("%3A", ":");
        return true;
    }

    internal static bool Shape(string definition, string key, bool hasPosition, Vector3 position)
    {
        RebirthNpcB14Kind kind; string volume, block;
        return IsDefinition(definition) && hasPosition && !float.IsNaN(position.x) && !float.IsNaN(position.y) &&
            !float.IsNaN(position.z) && TryParse(key, out kind, out volume, out block);
    }

    internal static bool Allows(RebirthNpcWorkAssignment a)
    {
        return a != null && Shape(a.DefinitionId, a.TargetKey, a.HasTargetPosition, a.TargetPosition);
    }

    internal static bool Allows(RebirthNpcWorkAssignmentRequest a)
    {
        return a != null && Shape(a.DefinitionId, a.TargetKey, a.HasTargetPosition, a.TargetPosition);
    }

    internal static bool Allows(RebirthNpcEmbodiedWorkIntent intent)
    {
        RebirthNpcWorkAssignment assignment; RebirthNpcWorkReservation reservation;
        return intent != null && intent.Path == "concrete" && intent.PrimaryId != 0 && intent.SecondaryId != 0 &&
            intent.ExecutorId == ExecutorId && intent.Actor is EntityRebirthHumanoidNPC && intent.MatchesOriginal() &&
            RebirthNpcWorkAssignmentService.TryGet(intent.PrimaryId, out assignment) && Allows(assignment) &&
            assignment.NpcId == intent.StableId &&
            RebirthNpcWorkReservationService.TryGetForAssignment(intent.PrimaryId, out reservation) &&
            reservation.ReservationId == intent.SecondaryId;
    }

    internal static Vector3i Position(RebirthNpcWorkAssignment a)
    {
        return new Vector3i(Mathf.FloorToInt(a.TargetPosition.x), Mathf.FloorToInt(a.TargetPosition.y),
            Mathf.FloorToInt(a.TargetPosition.z));
    }

    internal static string TargetId(RebirthNpcWorkAssignment a)
    {
        Vector3i p = Position(a);
        return "block:" + p.x.ToString(CultureInfo.InvariantCulture) + ":" + p.y.ToString(CultureInfo.InvariantCulture) + ":" +
            p.z.ToString(CultureInfo.InvariantCulture);
    }

    internal static Guid OperationId(RebirthNpcWorkAssignment a)
    {
        return RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId);
    }

    // Directing player online, the block still inside their saved Mining/Logging volume, outside trader
    // areas and inside their own (or nobody's) land claim.
    internal static bool Authorized(RebirthNpcWorkAssignment a)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        RebirthNpcB14Kind kind; string volume, block;
        if (!Allows(a) || world == null || world.IsRemote() || !TryParse(a.TargetKey, out kind, out volume, out block)) return false;
        EntityPlayer owner = RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId);
        PersistentPlayerData persistent = owner == null || GameManager.Instance.GetPersistentPlayerList() == null ? null :
            GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(owner.entityId);
        Vector3i position = Position(a);
        if (owner == null || owner.IsDead() || persistent == null || persistent.PrimaryId == null ||
            !ReferenceEquals(owner.world, world) || !ReferenceEquals(world.GetEntity(owner.entityId), owner) ||
            world.IsWithinTraderArea(position)) return false;
        EnumLandClaimOwner claim = world.GetLandClaimOwner(position, persistent);
        if (claim != EnumLandClaimOwner.None && claim != EnumLandClaimOwner.Self) return false;
        return RebirthNpcB14VolumeAccess.CreateContains(world, persistent.PrimaryId, volume, kind)(position);
    }

    // Navigation goal: the block cell. A felled b259 tree falls AWAY from the worker (direction = tree minus
    // damager), so no escape point is needed; `escape` is kept for the adapter signatures and is always false.
    internal static Vector3 Goal(RebirthNpcWorkAssignment a, EntityRebirthNPC npc, out bool escape)
    {
        escape = false;
        Vector3i p = Position(a);
        return new Vector3(p.x + 0.5f, p.y, p.z + 0.5f);
    }

    // A block this purpose can work: a b259 tree (BlockModelTree, not already falling) for Logging; for Mining a
    // non-tree block with a Harvest drop list (ore, rock, ...).
    internal static bool IsWorkable(BlockValue value, RebirthNpcB14Kind kind)
    {
        if (value.isair || value.ischild) return false;
        bool tree = RebirthNpcB14Tree.IsTree(value);
        if (kind == RebirthNpcB14Kind.Logging) return tree && !RebirthNpcB14Tree.IsFalling(value);
        return !tree && value.Block.itemsToDrop != null && value.Block.itemsToDrop.ContainsKey(EnumDropEvent.Harvest);
    }

    // Harvest-tool fitness for a block, using the native rule (ItemActionAttack.calculateHarvestToolDamageBonus): the first
    // Harvest drop whose toolCategory the tool lists decides, and its Damage bonus is the score. A tool that lists no
    // matching category (an axe on ore), a non-harvest item or a broken tool scores -1. A block whose Harvest drops name no
    // category accepts any harvest tool (score 1).
    internal static float ToolScore(ItemValue tool, Block block)
    {
        if (tool == null || tool.IsEmpty() || tool.ItemClass == null || tool.ItemClass.Actions == null ||
            tool.ItemClass.Actions.Length == 0 || block == null || block.itemsToDrop == null) return -1f;
        if (tool.MaxUseTimes > 0 && tool.UseTimes >= tool.MaxUseTimes) return -1f;
        ItemActionAttack attack = tool.ItemClass.Actions[0] as ItemActionAttack;
        if (attack == null || attack.ToolBonuses == null || attack.ToolBonuses.Count == 0) return -1f;
        List<Block.SItemDropProb> drops;
        if (!block.itemsToDrop.TryGetValue(EnumDropEvent.Harvest, out drops) || drops == null) return -1f;
        bool anyCategory = false;
        for (int i = 0; i < drops.Count; i++)
        {
            string category = drops[i].toolCategory;
            if (category == null) continue;
            anyCategory = true;
            ItemActionAttack.Bonuses bonus;
            if (attack.ToolBonuses.TryGetValue(category, out bonus)) return Mathf.Max(bonus.Damage, 0.01f);
        }
        return anyCategory ? -1f : 1f;
    }

    // Toolbelt slot holding the best usable harvest tool for the block (the held slot wins ties), or -1.
    internal static int FindToolSlot(Inventory inventory, BlockValue value)
    {
        if (inventory == null || inventory.ItemGrid == null || inventory.ItemGrid.items == null || value.isair) return -1;
        ItemStack[] slots = inventory.ItemGrid.items;
        int held = inventory.holdingItemIdx, best = -1;
        float bestScore = 0f;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || slots[i].IsEmpty()) continue;
            float score = ToolScore(slots[i].itemValue, value.Block);
            if (score > bestScore || (score > 0f && score == bestScore && i == held)) { best = i; bestScore = score; }
        }
        return best;
    }

    // Admission/readiness probe (for the coordinator's readiness hook, which cannot call into this file until P6
    // adds the one-line call): permission, idle worker, a free bag cell, a held harvest tool, a workable block, and
    // no Follow. A saved B14 row always resumes.
    internal static bool PotentiallyReady(RebirthNpcWorkAssignment a, EntityRebirthHumanoidNPC npc)
    {
        RebirthNpcB14Kind kind; string volume, blockName; RebirthNpcB14Store.Row row;
        if (a == null || npc == null || npc.bag == null || npc.bag.ItemGrid == null || npc.GetAttackTarget() != null ||
            npc.Hand == null || npc.Hand.IsBusy || !TryParse(a.TargetKey, out kind, out volume, out blockName) || !Authorized(a))
            return false;
        if (RebirthNpcB14Store.TryRead(a.NpcId, out row)) return true;
        if (RebirthP6WorkFollowState.IsFollowing(npc) || npc.inventory == null) return false;
        World world = GameManager.Instance.World;
        if (!IsWorkable(world.GetBlock(Position(a)), kind)) return false;
        if (FindToolSlot(npc.inventory, world.GetBlock(Position(a))) < 0) return false; // no suitable harvest tool anywhere on the toolbelt
        for (int i = 0; i < npc.bag.SlotCount; i++)
            if (npc.bag.ItemGrid[i] == null || npc.bag.ItemGrid[i].IsEmpty()) return true;
        return false;
    }

    // Candidate blocks of a saved Desk Mining/Logging volume (exact containment per cell, bounded scan).
    internal static List<RebirthNpcMiningNode> EnumerateVolume(World world, PlatformUserIdentifierAbs owner,
        string volumeId, RebirthNpcB14Kind kind, int max)
    {
        List<RebirthNpcMiningNode> nodes = new List<RebirthNpcMiningNode>();
        Bounds bounds; long revision;
        string desk = kind == RebirthNpcB14Kind.Logging ? "Logging" : "Mining";
        if (world == null || owner == null || !RebirthCompanionDeskStore.TryResolveTarget(world, owner, volumeId, desk, out bounds, out revision))
            return nodes;
        Func<Vector3i, bool> contains = RebirthNpcB14VolumeAccess.CreateContains(world, owner, volumeId, kind);
        int x0 = Mathf.FloorToInt(bounds.min.x), x1 = Mathf.CeilToInt(bounds.max.x);
        int y0 = Mathf.FloorToInt(bounds.min.y), y1 = Mathf.CeilToInt(bounds.max.y);
        int z0 = Mathf.FloorToInt(bounds.min.z), z1 = Mathf.CeilToInt(bounds.max.z);
        if ((long)(x1 - x0) * (y1 - y0) * (z1 - z0) > 262144L) return nodes; // the Desk caps a side at 256; refuse absurd volumes
        for (int y = y1 - 1; y >= y0 && nodes.Count < max; y--)
            for (int x = x0; x < x1 && nodes.Count < max; x++)
                for (int z = z0; z < z1 && nodes.Count < max; z++)
                {
                    Vector3i cell = new Vector3i(x, y, z);
                    BlockValue value = world.GetBlock(cell);
                    if (!IsWorkable(value, kind) || !contains(cell)) continue;
                    nodes.Add(new RebirthNpcMiningNode { Position = cell, ExpectedBlockName = value.Block.GetBlockName(), Priority = 300 });
                }
        return nodes;
    }

    // Planner hook: for a native request (VolumeTargetId set) resolve the directing player from the session, install the
    // exact volume predicate and, when no nodes were given, scan the saved volume.
    internal static bool FillRequest(World world, RebirthNpcMiningPackageRequest request)
    {
        RebirthNpcInteractionContext context;
        if (world == null || request == null || string.IsNullOrWhiteSpace(request.VolumeTargetId) ||
            RebirthNpcInteractionSessionService.Authorize(request.SessionKey, RebirthNpcInteractionAction.AssignWork, out context) !=
                RebirthNpcInteractionResult.Allowed || context == null) return false;
        EntityPlayer actor = RebirthNpcInteractionMutationHandlers.FindActorPlayer(context.ActorId);
        PersistentPlayerData persistent = actor == null || GameManager.Instance.GetPersistentPlayerList() == null ? null :
            GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(actor.entityId);
        if (persistent == null || persistent.PrimaryId == null) return false;
        string volume = request.VolumeTargetId.Trim();
        if (request.VolumeContains == null)
            request.VolumeContains = RebirthNpcB14VolumeAccess.CreateContains(world, persistent.PrimaryId, volume, request.Kind);
        if (request.Nodes == null || request.Nodes.Count == 0)
            request.Nodes = EnumerateVolume(world, persistent.PrimaryId, volume, request.Kind, 128);
        return true;
    }

    // Single-block assignment (target selection by the owner), mirroring RebirthNpcSingleCropWork.Assign.
    internal static RebirthNpcWorkAssignmentResult Assign(string session, string volumeId, RebirthNpcB14Kind kind,
        Vector3i cell, out RebirthNpcWorkAssignment assignment, out string detail)
    {
        assignment = null; detail = "A workable block inside the saved Mining/Logging volume is required.";
        RebirthNpcInteractionContext context;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || RebirthNpcInteractionSessionService.Authorize(session, RebirthNpcInteractionAction.AssignWork, out context) !=
            RebirthNpcInteractionResult.Allowed || context == null) return RebirthNpcWorkAssignmentResult.PermissionDenied;
        BlockValue value = world.GetBlock(cell);
        if (!IsWorkable(value, kind)) return RebirthNpcWorkAssignmentResult.PermissionDenied;
        string key = BuildKey(kind, volumeId, value.Block.GetBlockName());
        Vector3 center = new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f);
        RebirthNpcWorkAssignment probe = new RebirthNpcWorkAssignment
        {
            DefinitionId = DefinitionId, TargetKey = key, HasTargetPosition = true, TargetPosition = center,
            ActorId = context.ActorId, NpcId = context.NpcId
        };
        if (!Authorized(probe)) return RebirthNpcWorkAssignmentResult.PermissionDenied;
        RebirthNpcWorkAssignmentRequest request = new RebirthNpcWorkAssignmentRequest
        {
            SessionKey = session, DefinitionId = DefinitionId, TargetKey = key, HasTargetPosition = true,
            TargetPosition = center, ExpectedRuntimeRevision = context.RuntimeRevision
        };
        return RebirthNpcWorkAssignmentService.Create(request, out assignment, out detail);
    }

    // A committed outcome is accepted while the saved row agrees with it, or after the row was acknowledged
    // (absent row), mirroring P10's completion-stamp ring.
    internal static bool AllowsOutcome(RebirthNpcWorkOutcomeRecord outcome)
    {
        RebirthNpcWorkAssignment a; RebirthNpcB14Store.Row row;
        if (outcome == null || !IsDefinition(outcome.DefinitionId) ||
            !RebirthNpcWorkAssignmentService.TryGet(outcome.AssignmentId, out a) || !Allows(a) ||
            a.NpcId != outcome.NpcId || outcome.OutcomeId != OperationId(a) || outcome.StartedUtcTicks != a.CreatedUtcTicks ||
            outcome.CompletedUtcTicks <= a.CreatedUtcTicks) return false;
        return !RebirthNpcB14Store.TryRead(a.NpcId, out row) || row.CompletedUtcTicks == 0 ||
            row.CompletedUtcTicks == outcome.CompletedUtcTicks;
    }
}

// Target adapter: validates ownership/volume and supplies the navigation goal (block, or escape point).
public sealed class RebirthNpcB14TargetAdapter : IRebirthNpcWorkTargetAdapter
{
    public string AdapterId { get { return "rebirth.target.b14-mining"; } }
    public int Priority { get { return 420; } } // above storage-leg (410) and position (generic)

    public bool CanResolve(RebirthNpcWorkAssignment a, RebirthNpcWorkDefinition d)
    {
        return RebirthNpcB14MiningWork.Allows(a);
    }

    public bool TryResolve(RebirthNpcWorkAssignment a, RebirthNpcWorkDefinition d,
        out RebirthNpcWorkTargetSnapshot target, out string detail)
    {
        target = null; detail = "B14 target is unavailable.";
        if (!RebirthNpcB14MiningWork.Allows(a)) return false;
        EntityRebirthNPC npc;
        RebirthNpcWorkNavigationService.TryResolveNpc(a.NpcId, out npc);
        bool escape;
        Vector3 goal = RebirthNpcB14MiningWork.Goal(a, npc, out escape);
        // Revision is constant: the escape flips on a saved row and must not read as a changed target.
        target = new RebirthNpcWorkTargetSnapshot
        {
            AdapterId = AdapterId, StableTargetId = RebirthNpcB14MiningWork.TargetId(a), TargetRevision = "b14-1",
            Position = goal, IsLoaded = true, IsAvailable = true, InteractionRange = RebirthNpcWorkRuntime.WorkRadius,
            Detail = escape ? "Clearing the tree fall footprint." : "Mining/logging block inside the saved volume."
        };
        detail = target.Detail;
        return true;
    }

    public bool Validate(RebirthNpcConcreteWorkContext context, out RebirthNpcWorkFailureCategory failure, out string detail)
    {
        failure = RebirthNpcWorkFailureCategory.InvalidTarget; detail = "B14 target is invalid.";
        if (context == null || context.Assignment == null || !RebirthNpcB14MiningWork.Allows(context.Assignment)) return false;
        RebirthNpcWorkAssignment a = context.Assignment;
        if (!RebirthNpcB14MiningWork.Authorized(a))
        {
            // Owner offline or volume/claim changed: retryable only while the owner is merely absent.
            failure = RebirthNpcWorkFailureCategory.AuthorizationDenied;
            detail = "Directing player, saved volume or land claim no longer authorizes this block.";
            return false;
        }
        RebirthNpcB14Store.Row row;
        RebirthNpcB14Kind kind; string volume, blockName;
        RebirthNpcB14MiningWork.TryParse(a.TargetKey, out kind, out volume, out blockName);
        World world = GameManager.Instance.World;
        BlockValue current = world.GetBlock(RebirthNpcB14MiningWork.Position(a));
        bool saved = RebirthNpcB14Store.TryRead(a.NpcId, out row);
        if (!saved && (current.isair || current.type != Block.GetBlockValue(blockName).type))
        { failure = RebirthNpcWorkFailureCategory.TargetChanged; detail = "Block changed before the strike."; return false; }
        failure = RebirthNpcWorkFailureCategory.None; detail = "B14 target remains valid.";
        return true;
    }

    public bool IsNpcInRange(RebirthNpcConcreteWorkContext context, out string detail)
    {
        detail = "Worker must reach the block.";
        EntityRebirthNPC npc;
        if (context == null || context.Assignment == null || !RebirthNpcWorkNavigationService.TryResolveNpc(context.Assignment.NpcId, out npc))
            return false;
        bool escape;
        Vector3 goal = RebirthNpcB14MiningWork.Goal(context.Assignment, npc, out escape);
        if (escape) { detail = "Worker must clear the tree fall footprint."; return false; }
        Vector3i p = RebirthNpcB14MiningWork.Position(context.Assignment);
        bool near = (npc.position - new Vector3(p.x + 0.5f, p.y + 0.5f, p.z + 0.5f)).sqrMagnitude <= RebirthNpcWorkRuntime.WorkRadiusSquared;
        if (near) detail = "Worker is within reach of the block.";
        return near;
    }
}

// Work adapter for the Mining executor. Begin/Tick/Cancel hooks; the executor does navigation and Follow
// suspension through the EAI/IsNpcInRange path, and this adapter additionally refuses to strike under Follow.
public sealed class RebirthNpcB14MiningWorkAdapter : IRebirthNpcMiningWorkAdapter
{
    public string AdapterId { get { return "rebirth.mining.b14-native-harvest"; } }
    public int Priority { get { return 800; } }

    public bool CanHandle(RebirthNpcConcreteWorkContext context)
    {
        return RebirthNpcBlockMiningAdapter.NativeHarvestQualified && context != null && context.Assignment != null &&
            RebirthNpcB14MiningWork.Allows(context.Assignment);
    }

    private static RebirthNpcWorkMutationResult Hold(string detail)
    {
        return new RebirthNpcWorkMutationResult
        {
            Disposition = RebirthNpcWorkExecutionDisposition.Suspend,
            FailureCategory = RebirthNpcWorkFailureCategory.InventoryUnavailable, Detail = detail
        };
    }

    private static RebirthNpcWorkMutationResult Failed(RebirthNpcWorkFailureCategory category, string detail)
    {
        return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Failed, FailureCategory = category, Detail = detail };
    }

    public RebirthNpcWorkMutationResult BeginMining(RebirthNpcConcreteWorkContext context)
    {
        if (!CanHandle(context)) return Failed(RebirthNpcWorkFailureCategory.ExecutorFault, "Native mining is not qualified.");
        return RebirthNpcWorkMutationResult.Continue(0f, "B14 mining accepted; awaiting arrival.");
    }

    public RebirthNpcWorkMutationResult TickMining(RebirthNpcConcreteWorkContext context)
    {
        if (!CanHandle(context)) return Failed(RebirthNpcWorkFailureCategory.ExecutorFault, "Native mining is not qualified.");
        RebirthNpcEmbodiedWorkIntent intent = RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent;
        EntityRebirthHumanoidNPC npc = intent != null ? intent.Actor as EntityRebirthHumanoidNPC : null;
        if (intent == null || npc == null || !RebirthNpcEmbodiedWorkOwner.IsDispatching(intent) ||
            !RebirthNpcB14MiningWork.Allows(intent) || npc.inventory == null || npc.GetAttackTarget() != null)
            return Hold("Original mining EAI custody is unavailable.");
        RebirthNpcWorkAssignment a = context.Assignment;
        RebirthNpcB14Kind kind; string volume, blockName;
        if (!RebirthNpcB14MiningWork.TryParse(a.TargetKey, out kind, out volume, out blockName))
            return Failed(RebirthNpcWorkFailureCategory.InvalidTarget, "B14 target key is invalid.");
        // A NEW operation (no saved row, or between strikes of one block: Planned) needs the best harvest tool for THIS block
        // in hand. It is chosen from the worker's own toolbelt with the native holster switch (the same call combat weapon
        // selection uses) and only here, before anything is armed, so the wear binding of a saved Armed/Damaged operation
        // never sees the slot change. A saved operation in any later phase resumes with whatever it recorded.
        RebirthNpcB14Store.Row existing;
        bool fresh = !RebirthNpcB14Store.TryRead(a.NpcId, out existing) || existing.Original == null ||
            existing.Original.Phase == RebirthNpcB14Phase.Planned;
        if (fresh)
        {
            World world = GameManager.Instance.World;
            BlockValue target = world.GetBlock(RebirthNpcB14MiningWork.Position(a));
            if (!target.isair)
            {
                int slot = RebirthNpcB14MiningWork.FindToolSlot(npc.inventory, target);
                if (slot < 0) return Hold("No usable pickaxe/axe for this block on the worker's toolbelt.");
                if (slot != npc.inventory.holdingItemIdx)
                {
                    if (npc.Hand == null || npc.Hand.IsBusy || npc.inventory.IsHandSwitching() || npc.inventory.IsHoldingItemActionRunning())
                        return RebirthNpcWorkMutationResult.Continue(0f, "Waiting to equip the harvest tool.");
                    npc.inventory.SetHoldingItemIdx(slot);
                    return RebirthNpcWorkMutationResult.Continue(0f, "Equipping the harvest tool.");
                }
            }
        }
        ItemValue tool = npc.inventory.holdingItemItemValue;
        if (tool == null || tool.IsEmpty()) return Hold("The worker holds no attack tool.");
        if (npc.inventory.IsHandSwitching()) return RebirthNpcWorkMutationResult.Continue(0f, "Waiting for the harvest tool to be ready.");
        // Owned-storage fallback is HELD (no destination selection designed); a full bag keeps the stack at its
        // physical source through the no-loss overflow re-drop.
        RebirthNpcB14StepResult step = RebirthNpcB14HarvestExecutor.Step(npc, a.AssignmentId, kind,
            RebirthNpcB14MiningWork.Position(a), tool);
        if (step.State == RebirthNpcB14StepState.Failed)
            return Failed(RebirthNpcWorkFailureCategory.ExecutorFault, step.Detail);
        if (step.State == RebirthNpcB14StepState.Continue)
            return RebirthNpcWorkMutationResult.Continue(0f, step.Detail);

        // Completed: the saved receipt (with the ONE completion time) is returned on every retry.
        Dictionary<string, int> produced = new Dictionary<string, int>(step.Produced, StringComparer.OrdinalIgnoreCase);
        RebirthNpcWorkOutcomeRecord outcome = new RebirthNpcWorkOutcomeRecord
        {
            OutcomeId = RebirthNpcB14MiningWork.OperationId(a), AssignmentId = a.AssignmentId, NpcId = a.NpcId,
            DefinitionId = RebirthNpcB14MiningWork.DefinitionId, TargetId = RebirthNpcB14MiningWork.TargetId(a),
            DurableCompletion = true,
            DurableWitness = "b14-native-harvest:" + RebirthNpcB14MiningWork.OperationId(a).ToString("N") + ":" +
                step.CompletedUtcTicks.ToString(CultureInfo.InvariantCulture),
            StartedUtcTicks = a.CreatedUtcTicks, CompletedUtcTicks = step.CompletedUtcTicks,
            State = RebirthNpcWorkOutcomeState.Completed, FailureCategory = RebirthNpcWorkFailureCategory.None,
            Produced = produced, Detail = "Native NPC strike harvested the block; custody settled exactly once."
        };
        if (!RebirthNpcWorkOutcomeService.TryRecordCommitted(outcome))
            return Hold("Harvest is awaiting its committed outcome.");
        RebirthP1AwardQueueResult metric = RebirthNpcB14HarvestExecutor.SupplyEffort(outcome, step);
        if (metric == RebirthP1AwardQueueResult.Unavailable) return Hold("Owner award metric is awaiting the journal.");
        // Invalid = nothing to award (no positive effort); Journaled/Acknowledged = supplied.
        // The worker's own shared Mining/Logging skill, once per saved outcome (stable receipt); only Unavailable retries.
        RebirthP1AwardQueueResult own = RebirthNpcB14HarvestExecutor.SupplyNpcSkillAward(outcome, step, kind);
        if (own == RebirthP1AwardQueueResult.Unavailable) return Hold("NPC skill award is awaiting the journal.");
        if (!RebirthNpcB14HarvestExecutor.Acknowledge(npc)) return Hold("Harvest receipt could not be retired yet.");
        return new RebirthNpcWorkMutationResult
        {
            Disposition = RebirthNpcWorkExecutionDisposition.Completed, MutationCommitted = true,
            ProgressDelta = 1f, Produced = produced, Detail = outcome.Detail
        };
    }

    // Cancellation keeps the saved original (restart-safe recovery owns it); nothing is undone here.
    public void CancelMining(RebirthNpcConcreteWorkContext context, string reason) { }
}
