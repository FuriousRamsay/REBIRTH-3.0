using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable disable

public enum RebirthNpcFarmingPlannedAction : byte
{
    Harvest = 0,
    Water = 1,
    Plant = 2
}

public sealed class RebirthNpcFarmingPlanCandidate
{
    public RebirthNpcFarmingPlannedAction Action { get; internal set; }
    public Vector3i Position { get; internal set; }
    public string DefinitionId { get; internal set; }
    public string TargetKey { get; internal set; }
    public int Priority { get; internal set; }
    public string Detail { get; internal set; }
}


public sealed class RebirthNpcWorkPackageSnapshot
{
    public string PackageKind { get; internal set; }
    public Guid PackageId { get; internal set; }
    public ulong[] AssignmentIds { get; internal set; }
    public long CreatedUtcTicks { get; internal set; }
    public long UpdatedUtcTicks { get; internal set; }
    public int ConsecutiveStalledReconciliations { get; internal set; }
}

public sealed class RebirthNpcFarmingWorkPackageRequest
{
    public string SessionKey { get; set; }
    public Vector3i Center { get; set; }
    public int HorizontalRadius { get; set; }
    public int VerticalRadius { get; set; }
    public string SeedItemKey { get; set; }
    public int MaxAssignments { get; set; }
    public bool IncludeHarvest { get; set; }
    public bool IncludeWatering { get; set; }
    public bool IncludePlanting { get; set; }
    public bool StartImmediately { get; set; }
}

public sealed class RebirthNpcFarmingWorkPackageResult
{
    public Guid PackageId { get; internal set; }
    public int Planned { get; internal set; }
    public int Created { get; internal set; }
    public int Started { get; internal set; }
    public int Conflicts { get; internal set; }
    public int Rejected { get; internal set; }
    public ulong[] AssignmentIds { get; internal set; }
    public string Detail { get; internal set; }
}

/// <summary>
/// Area planner and bounded multi-assignment orchestrator for Advanced Farming NPC work.
/// It creates ordinary authoritative assignments; it does not bypass interaction, capability,
/// reservation, navigation, mutation, persistence, or outcome services.
/// </summary>
public static class RebirthNpcFarmingWorkPackageService
{
    public const string HarvestDefinitionId = "rebirth.farming.harvest";
    public const string WaterDefinitionId = "rebirth.farming.water";
    public const string PlantDefinitionId = "rebirth.farming.plant";

    private sealed class ActivePackage
    {
        public Guid PackageId;
        public readonly List<ulong> AssignmentIds = new List<ulong>();
        public long CreatedUtcTicks;
        public long UpdatedUtcTicks;
        public bool StartImmediately;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<Guid, ActivePackage> Active = new Dictionary<Guid, ActivePackage>();
    private static readonly RebirthNpcFairRoundRobin<Guid> Schedule = new RebirthNpcFairRoundRobin<Guid>();
    private static long plans, packages, candidates, created, started, conflicts, rejected, reconciled, completedPackages;

    public static void RegisterDefinitions()
    {
        RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(
            HarvestDefinitionId, RebirthNpcWorkKind.Farming,
            RebirthNpcWorkCapability.Farming | RebirthNpcWorkCapability.Harvesting,
            320, 1, TimeSpan.FromSeconds(45), false), true);
        RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(
            WaterDefinitionId, RebirthNpcWorkKind.Farming,
            RebirthNpcWorkCapability.Farming,
            280, 1, TimeSpan.FromSeconds(45), false), true);
        RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(
            PlantDefinitionId, RebirthNpcWorkKind.Farming,
            RebirthNpcWorkCapability.Farming,
            240, 1, TimeSpan.FromSeconds(45), false), true);
    }

    public static RebirthNpcFarmingPlanCandidate[] Plan(RebirthNpcFarmingWorkPackageRequest request,
        out string detail)
    {
        detail = string.Empty;
        Interlocked.Increment(ref plans);
        if (request == null)
        {
            detail = "Farming work-package request is required.";
            Interlocked.Increment(ref rejected);
            return new RebirthNpcFarmingPlanCandidate[0];
        }
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.IsRemote())
        {
            detail = "Authoritative world is unavailable.";
            Interlocked.Increment(ref rejected);
            return new RebirthNpcFarmingPlanCandidate[0];
        }
        if (!AdvancedFarmingRuntimePolicy.Enabled)
        {
            detail = "Advanced Farming is disabled.";
            Interlocked.Increment(ref rejected);
            return new RebirthNpcFarmingPlanCandidate[0];
        }

        int horizontal = Math.Max(0, Math.Min(32, request.HorizontalRadius));
        int vertical = Math.Max(0, Math.Min(8, request.VerticalRadius));
        int limit = Math.Max(1, Math.Min(256, request.MaxAssignments <= 0 ? 32 : request.MaxAssignments));
        string seedKey = (request.SeedItemKey ?? string.Empty).Trim();
        BlockValue seedValue = BlockValue.Air;
        BlockPlantGrowingRebirth seedBlock = null;
        if (request.IncludePlanting && seedKey.Length > 0)
        {
            seedValue = Block.GetBlockValue(seedKey);
            seedBlock = seedValue.Block as BlockPlantGrowingRebirth;
            if (seedValue.isair || seedBlock == null)
            {
                detail = "Planting seed key does not resolve to an Advanced Farming seed-stage block.";
                Interlocked.Increment(ref rejected);
                return new RebirthNpcFarmingPlanCandidate[0];
            }
        }

        List<RebirthNpcFarmingPlanCandidate> result = new List<RebirthNpcFarmingPlanCandidate>();
        for (int y = request.Center.y - vertical; y <= request.Center.y + vertical; y++)
        for (int x = request.Center.x - horizontal; x <= request.Center.x + horizontal; x++)
        for (int z = request.Center.z - horizontal; z <= request.Center.z + horizontal; z++)
        {
            if (result.Count >= limit) break;
            Vector3i pos = new Vector3i(x, y, z);
            BlockValue value = world.GetBlock(pos);
            BlockPlantGrowingRebirth crop = value.Block as BlockPlantGrowingRebirth;
            if (crop != null)
            {
                if (request.IncludeHarvest && crop.IsFullyGrownCrop(value) &&
                    RebirthUtilities.IsPlayerGrownHarvestCrop(value))
                {
                    result.Add(Candidate(RebirthNpcFarmingPlannedAction.Harvest, pos,
                        HarvestDefinitionId, "farm:harvest", 320,
                        "Fully grown player crop is ready for harvest and replant."));
                    continue;
                }
                if (request.IncludeWatering && !crop.IsFullyGrownCrop(value))
                {
                    TileEntityPlantGrowingRebirth plant = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
                    if (plant != null && !plant.bWatered)
                    {
                        result.Add(Candidate(RebirthNpcFarmingPlannedAction.Water, pos,
                            WaterDefinitionId, "farm:water", 280,
                            "Active crop requires water for its current growth cycle."));
                        continue;
                    }
                }
            }

            if (request.IncludePlanting && seedBlock != null && value.isair)
            {
                BlockValue below = world.GetBlock(new Vector3i(x, y - 1, z));
                if (!(below.Block is BlockFarmPlotRebirth)) continue;
                if (!seedBlock.CanPlaceBlockAt(world, pos, seedValue, false)) continue;
                result.Add(Candidate(RebirthNpcFarmingPlannedAction.Plant, pos,
                    PlantDefinitionId, "farm:plant:" + seedKey, 240,
                    "Empty Advanced Farming plot position accepts the requested seed."));
            }
        }

        result.Sort(CompareCandidates);
        Interlocked.Add(ref candidates, result.Count);
        detail = "Planned " + result.Count + " farming assignments within radius " + horizontal + ".";
        return result.ToArray();
    }

    public static RebirthNpcFarmingWorkPackageResult CreateAndStart(
        RebirthNpcFarmingWorkPackageRequest request)
    {
        string planningDetail;
        RebirthNpcFarmingPlanCandidate[] plan = Plan(request, out planningDetail);
        RebirthNpcFarmingWorkPackageResult result = new RebirthNpcFarmingWorkPackageResult
        {
            PackageId = Guid.NewGuid(), Planned = plan.Length, Detail = planningDetail
        };
        if (plan.Length == 0 || request == null || string.IsNullOrWhiteSpace(request.SessionKey))
        {
            result.Rejected = plan.Length == 0 ? 1 : plan.Length;
            Interlocked.Add(ref rejected, result.Rejected);
            result.AssignmentIds = new ulong[0];
            return result;
        }

        ActivePackage package = new ActivePackage
        {
            PackageId = result.PackageId,
            CreatedUtcTicks = DateTime.UtcNow.Ticks,
            UpdatedUtcTicks = DateTime.UtcNow.Ticks,
            StartImmediately = request.StartImmediately
        };
        for (int i = 0; i < plan.Length; i++)
        {
            RebirthNpcFarmingPlanCandidate candidate = plan[i];
            RebirthNpcWorkAssignment assignment;
            string error;
            RebirthNpcWorkAssignmentResult createResult = RebirthNpcWorkAssignmentService.Create(
                new RebirthNpcWorkAssignmentRequest
                {
                    SessionKey = request.SessionKey,
                    DefinitionId = candidate.DefinitionId,
                    TargetKey = candidate.TargetKey,
                    TargetPosition = new Vector3(candidate.Position.x + 0.5f,
                        candidate.Position.y, candidate.Position.z + 0.5f),
                    HasTargetPosition = true,
                    Priority = candidate.Priority
                }, out assignment, out error);
            if (createResult != RebirthNpcWorkAssignmentResult.Succeeded || assignment == null)
            {
                if (createResult == RebirthNpcWorkAssignmentResult.Conflict) result.Conflicts++;
                else result.Rejected++;
                continue;
            }
            package.AssignmentIds.Add(assignment.AssignmentId);
            result.Created++;
            if (request.StartImmediately)
            {
                string startDetail;
                if (RebirthNpcWorkAssignmentCoordinator.Start(assignment.AssignmentId, out startDetail))
                    result.Started++;
            }
        }

        result.AssignmentIds = package.AssignmentIds.ToArray();
        lock (Sync)
        {
            if (package.AssignmentIds.Count > 0)
            {
                Active[package.PackageId] = package;
                Schedule.Add(package.PackageId);
            }
        }
        Interlocked.Increment(ref packages);
        Interlocked.Add(ref created, result.Created);
        Interlocked.Add(ref started, result.Started);
        Interlocked.Add(ref conflicts, result.Conflicts);
        Interlocked.Add(ref rejected, result.Rejected);
        result.Detail = planningDetail + " Created=" + result.Created +
            " started=" + result.Started + " conflicts=" + result.Conflicts +
            " rejected=" + result.Rejected + ".";
        return result;
    }

    public static void Tick(int budget)
    {
        if (budget <= 0) return;
        for (int processed=0; processed<budget; processed++)
        {
            Guid packageId;
            ActivePackage package;
            lock (Sync)
            {
                if(!Schedule.TryTake(out packageId))break;
                if(!Active.TryGetValue(packageId,out package))continue;
            }
            bool anyNonTerminal=false;
            for(int n=0;n<package.AssignmentIds.Count;n++)
            {
                RebirthNpcWorkAssignment assignment;
                if(!RebirthNpcWorkAssignmentService.TryGet(package.AssignmentIds[n],out assignment))continue;
                if(package.StartImmediately && assignment.Status==RebirthNpcWorkAssignmentStatus.Ready)
                {
                    string ignored;
                    RebirthNpcWorkAssignmentCoordinator.Start(assignment.AssignmentId,out ignored);
                }
                if(assignment.Status!=RebirthNpcWorkAssignmentStatus.Completed &&
                    assignment.Status!=RebirthNpcWorkAssignmentStatus.Cancelled &&
                    assignment.Status!=RebirthNpcWorkAssignmentStatus.Failed) anyNonTerminal=true;
            }
            package.UpdatedUtcTicks=DateTime.UtcNow.Ticks;
            Interlocked.Increment(ref reconciled);
            lock (Sync)
            {
                if(!Active.ContainsKey(packageId))continue;
                if(!anyNonTerminal)
                {
                    Active.Remove(packageId);
                    Schedule.Remove(packageId);
                    Interlocked.Increment(ref completedPackages);
                }
                else Schedule.Return(packageId);
            }
        }
    }


    public static RebirthNpcWorkPackageSnapshot[] CaptureActivePackages()
    {
        lock (Sync)
        {
            RebirthNpcWorkPackageSnapshot[] result = new RebirthNpcWorkPackageSnapshot[Active.Count];
            int index = 0;
            foreach (ActivePackage package in Active.Values)
                result[index++] = new RebirthNpcWorkPackageSnapshot
                {
                    PackageKind = "farming", PackageId = package.PackageId,
                    AssignmentIds = package.AssignmentIds.ToArray(),
                    CreatedUtcTicks = package.CreatedUtcTicks, UpdatedUtcTicks = package.UpdatedUtcTicks
                };
            return result;
        }
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) { Active.Clear(); Schedule.Clear(); }
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            return "[REBIRTH NPC Farming Work Packages] active=" + Active.Count +
                " plans=" + Interlocked.Read(ref plans) +
                " packages=" + Interlocked.Read(ref packages) +
                " candidates=" + Interlocked.Read(ref candidates) +
                " created=" + Interlocked.Read(ref created) +
                " started=" + Interlocked.Read(ref started) +
                " conflicts=" + Interlocked.Read(ref conflicts) +
                " rejected=" + Interlocked.Read(ref rejected) +
                " reconciled=" + Interlocked.Read(ref reconciled) +
                " completedPackages=" + Interlocked.Read(ref completedPackages);
        }
    }

    private static RebirthNpcFarmingPlanCandidate Candidate(RebirthNpcFarmingPlannedAction action,
        Vector3i position, string definitionId, string targetKey, int priority, string detail)
    {
        return new RebirthNpcFarmingPlanCandidate { Action = action, Position = position,
            DefinitionId = definitionId, TargetKey = targetKey, Priority = priority, Detail = detail };
    }

    private static int CompareCandidates(RebirthNpcFarmingPlanCandidate left,
        RebirthNpcFarmingPlanCandidate right)
    {
        int priority = right.Priority.CompareTo(left.Priority);
        if (priority != 0) return priority;
        int y = left.Position.y.CompareTo(right.Position.y);
        if (y != 0) return y;
        int x = left.Position.x.CompareTo(right.Position.x);
        return x != 0 ? x : left.Position.z.CompareTo(right.Position.z);
    }
}
