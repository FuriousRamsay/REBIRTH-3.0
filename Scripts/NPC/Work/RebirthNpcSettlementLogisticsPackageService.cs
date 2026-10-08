using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable disable

public enum RebirthNpcLogisticsEndpointKind : byte
{
    Settlement = 0,
    StaticContainer = 1,
    WorkstationOutput = 2
}

public sealed class RebirthNpcLogisticsEndpointRef
{
    public RebirthNpcLogisticsEndpointKind Kind { get; set; }
    public string SettlementId { get; set; }
    public Vector3i Position { get; set; }

    public string ToPlanToken()
    {
        if (Kind == RebirthNpcLogisticsEndpointKind.Settlement)
            return (SettlementId ?? string.Empty).Trim();
        return (Kind == RebirthNpcLogisticsEndpointKind.WorkstationOutput ? "W@" : "S@") +
            Position.x.ToString(CultureInfo.InvariantCulture) + "," +
            Position.y.ToString(CultureInfo.InvariantCulture) + "," +
            Position.z.ToString(CultureInfo.InvariantCulture);
    }

    public string StableId
    {
        get
        {
            return Kind == RebirthNpcLogisticsEndpointKind.Settlement
                ? "settlement:" + (SettlementId ?? string.Empty).Trim()
                : "resource:" + ToPlanToken();
        }
    }
}

public sealed class RebirthNpcLogisticsDemand
{
    public RebirthNpcLogisticsEndpointRef Source { get; set; }
    public RebirthNpcLogisticsEndpointRef Destination { get; set; }
    public string ItemKey { get; set; }
    public int Quantity { get; set; }
    public bool AllowPartial { get; set; }
    public int Priority { get; set; }
    public Vector3 TargetPosition { get; set; }
    public bool HasTargetPosition { get; set; }
}

public sealed class RebirthNpcLogisticsPackageRequest
{
    public string SessionKey { get; set; }
    public IList<RebirthNpcLogisticsDemand> Demands { get; set; }
    public int MaxQuantityPerTrip { get; set; }
    public int MaxAssignments { get; set; }
    public bool StartImmediately { get; set; }
}

public sealed class RebirthNpcLogisticsTripPlan
{
    public int DemandIndex { get; internal set; }
    public string DefinitionId { get; internal set; }
    public string TargetKey { get; internal set; }
    public string ItemKey { get; internal set; }
    public int Quantity { get; internal set; }
    public int Priority { get; internal set; }
    public Vector3 TargetPosition { get; internal set; }
    public bool HasTargetPosition { get; internal set; }
    public string SourceId { get; internal set; }
    public string DestinationId { get; internal set; }
}

public sealed class RebirthNpcLogisticsPackageResult
{
    public Guid PackageId { get; internal set; }
    public int Demands { get; internal set; }
    public int TripsPlanned { get; internal set; }
    public int AssignmentsCreated { get; internal set; }
    public int AssignmentsStarted { get; internal set; }
    public int Conflicts { get; internal set; }
    public int Rejected { get; internal set; }
    public ulong[] AssignmentIds { get; internal set; }
    public string Detail { get; internal set; }
}


public sealed class RebirthNpcMixedEndpointHaulingPlanProvider : IRebirthNpcHaulingPlanProvider
{
    public int Priority { get { return 700; } }

    public bool TryPlan(RebirthNpcConcreteWorkContext context, out RebirthNpcHaulingPlan plan,
        out string detail)
    {
        plan = null;
        detail = string.Empty;
        string key = context != null && context.Assignment != null
            ? context.Assignment.TargetKey ?? string.Empty : string.Empty;
        if (!key.StartsWith("haul:bridge:", StringComparison.OrdinalIgnoreCase)) return false;
        string[] p = key.Split(':');
        if (p.Length < 9)
        {
            detail = "Mixed hauling target must be haul:bridge:<settlement|resource>:<token>:<settlement|resource>:<token>:<item>:<quantity>:<partial|exact>.";
            return false;
        }
        string sourceId, destinationId, error;
        if (!TryResolve(p[2], p[3], true, out sourceId, out error))
        { detail = "Mixed hauling source invalid: " + error; return false; }
        if (!TryResolve(p[4], p[5], false, out destinationId, out error))
        { detail = "Mixed hauling destination invalid: " + error; return false; }
        int quantity;
        if (string.IsNullOrWhiteSpace(p[6]) || !int.TryParse(p[7], NumberStyles.Integer,
            CultureInfo.InvariantCulture, out quantity) || quantity <= 0)
        { detail = "Mixed hauling item or quantity is invalid."; return false; }
        bool partial = string.Equals(p[8], "partial", StringComparison.OrdinalIgnoreCase);
        if (!partial && !string.Equals(p[8], "exact", StringComparison.OrdinalIgnoreCase))
        { detail = "Mixed hauling mode must be partial or exact."; return false; }
        plan = new RebirthNpcHaulingPlan
        {
            SourceEndpointId = sourceId,
            DestinationEndpointId = destinationId,
            ItemKey = p[6].Trim(),
            Quantity = quantity,
            AllowPartial = partial
        };
        detail = "Mixed settlement/physical hauling plan resolved.";
        return true;
    }

    private static bool TryResolve(string kind, string token, bool source,
        out string endpointId, out string error)
    {
        endpointId = string.Empty;
        error = string.Empty;
        if (string.Equals(kind, "settlement", StringComparison.OrdinalIgnoreCase))
        {
            RebirthNpcSettlementInventoryEndpoint endpoint =
                RebirthNpcSettlementInventoryEndpointRegistry.Ensure(token);
            if (endpoint == null) { error = "Settlement endpoint registration failed."; return false; }
            endpointId = endpoint.EndpointId;
            return true;
        }
        if (!string.Equals(kind, "resource", StringComparison.OrdinalIgnoreCase))
        { error = "Endpoint kind must be settlement or resource."; return false; }
        RebirthNpcPhysicalResourceEndpoint physical =
            RebirthNpcPhysicalResourceEndpointRegistry.Ensure(token, out error);
        if (physical == null) return false;
        if (!source && physical.Kind == RemoteResourceSourceKind.WorkstationOutput)
        { error = "Workstation output endpoints are debit-only."; return false; }
        endpointId = physical.EndpointId;
        return true;
    }
}

/// <summary>
/// Bounded, server-authoritative logistics planner layered over the existing hauling executor and
/// transactional external inventory coordinator. It splits demand into capacity-sized trips,
/// creates ordinary work assignments, and reconciles package state without bypassing reservations.
/// </summary>
public static class RebirthNpcSettlementLogisticsPackageService
{
    public const string HaulingDefinitionId = "rebirth.logistics.haul";

    private sealed class ActivePackage
    {
        public Guid PackageId;
        public readonly List<ulong> AssignmentIds = new List<ulong>();
        public long CreatedUtcTicks;
        public long UpdatedUtcTicks;
        public int ConsecutiveStalledReconciliations;
        public bool StartImmediately;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<Guid, ActivePackage> Active = new Dictionary<Guid, ActivePackage>();
    private static readonly RebirthNpcFairRoundRobin<Guid> Schedule = new RebirthNpcFairRoundRobin<Guid>();
    private static long planRequests, demandsAccepted, tripsPlanned, packagesCreated;
    private static long assignmentsCreated, assignmentsStarted, conflicts, rejected;
    private static long reconciliations, completedPackages, stalledPackages, startRetries;

    public static void RegisterDefinitions()
    {
        RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(
            HaulingDefinitionId, RebirthNpcWorkKind.Hauling,
            RebirthNpcWorkCapability.Hauling, 260, 1,
            TimeSpan.FromSeconds(60), false), true);
    }

    public static RebirthNpcLogisticsTripPlan[] Plan(RebirthNpcLogisticsPackageRequest request,
        out string detail)
    {
        detail = string.Empty;
        Interlocked.Increment(ref planRequests);
        if (request == null || request.Demands == null || request.Demands.Count == 0)
            return RejectPlan("At least one logistics demand is required.", out detail);

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.IsRemote())
            return RejectPlan("Authoritative world is unavailable.", out detail);

        int tripLimit = Math.Max(1, Math.Min(100000,
            request.MaxQuantityPerTrip <= 0 ? 1000 : request.MaxQuantityPerTrip));
        int assignmentLimit = Math.Max(1, Math.Min(512,
            request.MaxAssignments <= 0 ? 64 : request.MaxAssignments));
        List<RebirthNpcLogisticsTripPlan> result = new List<RebirthNpcLogisticsTripPlan>();

        for (int i = 0; i < request.Demands.Count && result.Count < assignmentLimit; i++)
        {
            RebirthNpcLogisticsDemand demand = request.Demands[i];
            string validation;
            if (!ValidateDemand(demand, out validation))
            {
                Interlocked.Increment(ref rejected);
                continue;
            }

            string sourceId, destinationId, targetKey;
            if (!TryResolveDemand(demand, out sourceId, out destinationId, out targetKey, out validation))
            {
                Interlocked.Increment(ref rejected);
                continue;
            }

            int transferable = demand.Quantity;
            if (demand.AllowPartial)
            {
                transferable = RebirthNpcExternalInventoryEndpointRegistry.GetNegotiatedQuantity(
                    sourceId, demand.ItemKey, transferable, true);
                transferable = RebirthNpcExternalInventoryEndpointRegistry.GetNegotiatedQuantity(
                    destinationId, demand.ItemKey, transferable, false);
            }
            if (transferable <= 0)
            {
                Interlocked.Increment(ref rejected);
                continue;
            }

            Interlocked.Increment(ref demandsAccepted);
            int remaining = transferable;
            int tripOrdinal = 0;
            while (remaining > 0 && result.Count < assignmentLimit)
            {
                int quantity = Math.Min(remaining, tripLimit);
                result.Add(new RebirthNpcLogisticsTripPlan
                {
                    DemandIndex = i,
                    DefinitionId = HaulingDefinitionId,
                    TargetKey = ReplaceQuantity(targetKey, quantity),
                    ItemKey = demand.ItemKey.Trim(),
                    Quantity = quantity,
                    Priority = demand.Priority == 0 ? 260 : demand.Priority,
                    TargetPosition = demand.TargetPosition,
                    HasTargetPosition = demand.HasTargetPosition,
                    SourceId = sourceId,
                    DestinationId = destinationId
                });
                remaining -= quantity;
                tripOrdinal++;
            }
        }

        result.Sort(CompareTrips);
        Interlocked.Add(ref tripsPlanned, result.Count);
        detail = "Planned " + result.Count + " logistics trips from " +
            request.Demands.Count + " demands with per-trip limit " + tripLimit + ".";
        return result.ToArray();
    }

    public static RebirthNpcLogisticsPackageResult CreateAndStart(
        RebirthNpcLogisticsPackageRequest request)
    {
        string planningDetail;
        RebirthNpcLogisticsTripPlan[] trips = Plan(request, out planningDetail);
        RebirthNpcLogisticsPackageResult result = new RebirthNpcLogisticsPackageResult
        {
            PackageId = Guid.NewGuid(),
            Demands = request != null && request.Demands != null ? request.Demands.Count : 0,
            TripsPlanned = trips.Length,
            Detail = planningDetail
        };
        if (request == null || string.IsNullOrWhiteSpace(request.SessionKey) || trips.Length == 0)
        {
            result.Rejected = Math.Max(1, trips.Length);
            result.AssignmentIds = new ulong[0];
            Interlocked.Add(ref rejected, result.Rejected);
            return result;
        }

        ActivePackage package = new ActivePackage
        {
            PackageId = result.PackageId,
            CreatedUtcTicks = DateTime.UtcNow.Ticks,
            UpdatedUtcTicks = DateTime.UtcNow.Ticks,
            StartImmediately = request.StartImmediately
        };
        for (int i = 0; i < trips.Length; i++)
        {
            RebirthNpcLogisticsTripPlan trip = trips[i];
            RebirthNpcWorkAssignment assignment;
            string error;
            RebirthNpcWorkAssignmentResult creation = RebirthNpcWorkAssignmentService.Create(
                new RebirthNpcWorkAssignmentRequest
                {
                    SessionKey = request.SessionKey,
                    DefinitionId = trip.DefinitionId,
                    TargetKey = trip.TargetKey,
                    TargetPosition = trip.TargetPosition,
                    HasTargetPosition = trip.HasTargetPosition,
                    Priority = trip.Priority
                }, out assignment, out error);
            if (creation != RebirthNpcWorkAssignmentResult.Succeeded || assignment == null)
            {
                if (creation == RebirthNpcWorkAssignmentResult.Conflict) result.Conflicts++;
                else result.Rejected++;
                continue;
            }
            package.AssignmentIds.Add(assignment.AssignmentId);
            result.AssignmentsCreated++;
            if (request.StartImmediately)
            {
                string startDetail;
                if (RebirthNpcWorkAssignmentCoordinator.Start(assignment.AssignmentId, out startDetail))
                    result.AssignmentsStarted++;
            }
        }

        result.AssignmentIds = package.AssignmentIds.ToArray();
        if (package.AssignmentIds.Count > 0)
        {
            lock (Sync)
            {
                Active[package.PackageId] = package;
                Schedule.Add(package.PackageId);
            }
            Interlocked.Increment(ref packagesCreated);
        }
        Interlocked.Add(ref assignmentsCreated, result.AssignmentsCreated);
        Interlocked.Add(ref assignmentsStarted, result.AssignmentsStarted);
        Interlocked.Add(ref conflicts, result.Conflicts);
        Interlocked.Add(ref rejected, result.Rejected);
        result.Detail = planningDetail + " Created=" + result.AssignmentsCreated +
            " started=" + result.AssignmentsStarted + " conflicts=" + result.Conflicts +
            " rejected=" + result.Rejected + ".";
        return result;
    }

    public static void Tick(int budget)
    {
        if (budget <= 0) return;
        for(int processed=0;processed<budget;processed++)
        {
            Guid packageId;
            lock(Sync) if(!Schedule.TryTake(out packageId))break;
            Reconcile(packageId);
            lock(Sync) if(Active.ContainsKey(packageId))Schedule.Return(packageId);
        }
    }


    private static void Reconcile(Guid packageId)
    {
        ActivePackage package;
        lock (Sync) if (!Active.TryGetValue(packageId, out package)) return;
        Interlocked.Increment(ref reconciliations);
        bool allTerminal = true;
        bool progressed = false;
        for (int i = 0; i < package.AssignmentIds.Count; i++)
        {
            RebirthNpcWorkAssignment assignment;
            if (!RebirthNpcWorkAssignmentService.TryGet(package.AssignmentIds[i], out assignment))
                continue;
            if (IsTerminal(assignment.Status)) continue;
            allTerminal = false;
            if (package.StartImmediately && assignment.Status == RebirthNpcWorkAssignmentStatus.Ready)
            {
                string detail;
                Interlocked.Increment(ref startRetries);
                if (RebirthNpcWorkAssignmentCoordinator.Start(assignment.AssignmentId, out detail))
                    progressed = true;
            }
            else if (assignment.UpdatedUtcTicks > package.UpdatedUtcTicks) progressed = true;
        }

        lock (Sync)
        {
            if (!Active.TryGetValue(packageId, out package)) return;
            if (allTerminal)
            {
                Active.Remove(packageId);
                Schedule.Remove(packageId);
                Interlocked.Increment(ref completedPackages);
                return;
            }
            if (progressed)
            {
                package.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
                package.ConsecutiveStalledReconciliations = 0;
            }
            else
            {
                package.ConsecutiveStalledReconciliations++;
                if (package.ConsecutiveStalledReconciliations == 30)
                    Interlocked.Increment(ref stalledPackages);
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
                    PackageKind = "logistics", PackageId = package.PackageId,
                    AssignmentIds = package.AssignmentIds.ToArray(),
                    CreatedUtcTicks = package.CreatedUtcTicks, UpdatedUtcTicks = package.UpdatedUtcTicks,
                    ConsecutiveStalledReconciliations = package.ConsecutiveStalledReconciliations
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
            StringBuilder b = new StringBuilder("[REBIRTH NPC Logistics Packages] active=")
                .Append(Active.Count).Append(" planRequests=").Append(Interlocked.Read(ref planRequests))
                .Append(" demandsAccepted=").Append(Interlocked.Read(ref demandsAccepted))
                .Append(" tripsPlanned=").Append(Interlocked.Read(ref tripsPlanned))
                .Append(" packagesCreated=").Append(Interlocked.Read(ref packagesCreated))
                .Append(" assignmentsCreated=").Append(Interlocked.Read(ref assignmentsCreated))
                .Append(" assignmentsStarted=").Append(Interlocked.Read(ref assignmentsStarted))
                .Append(" conflicts=").Append(Interlocked.Read(ref conflicts))
                .Append(" rejected=").Append(Interlocked.Read(ref rejected))
                .Append(" reconciliations=").Append(Interlocked.Read(ref reconciliations))
                .Append(" completedPackages=").Append(Interlocked.Read(ref completedPackages))
                .Append(" stalledPackages=").Append(Interlocked.Read(ref stalledPackages))
                .Append(" startRetries=").Append(Interlocked.Read(ref startRetries));
            foreach (ActivePackage package in Active.Values)
                b.AppendLine().Append("  package=").Append(package.PackageId)
                    .Append(" assignments=").Append(package.AssignmentIds.Count)
                    .Append(" stalledReconciliations=").Append(package.ConsecutiveStalledReconciliations);
            return b.ToString();
        }
    }

    private static bool ValidateDemand(RebirthNpcLogisticsDemand demand, out string error)
    {
        error = string.Empty;
        if (demand == null || demand.Source == null || demand.Destination == null)
        { error = "Logistics demand requires source and destination endpoints."; return false; }
        if (string.IsNullOrWhiteSpace(demand.ItemKey) || demand.Quantity <= 0)
        { error = "Logistics demand requires a valid item and positive quantity."; return false; }
        if (!demand.HasTargetPosition)
        { error = "Logistics demand requires an authoritative navigation target position."; return false; }
        if (demand.Source.Kind == RebirthNpcLogisticsEndpointKind.Settlement &&
            string.IsNullOrWhiteSpace(demand.Source.SettlementId))
        { error = "Settlement source id is required."; return false; }
        if (demand.Destination.Kind == RebirthNpcLogisticsEndpointKind.Settlement &&
            string.IsNullOrWhiteSpace(demand.Destination.SettlementId))
        { error = "Settlement destination id is required."; return false; }
        if (demand.Destination.Kind == RebirthNpcLogisticsEndpointKind.WorkstationOutput)
        { error = "Workstation output cannot be a logistics destination."; return false; }
        if (string.Equals(demand.Source.StableId, demand.Destination.StableId,
            StringComparison.OrdinalIgnoreCase))
        { error = "Logistics source and destination must differ."; return false; }
        return true;
    }

    private static bool TryResolveDemand(RebirthNpcLogisticsDemand demand,
        out string sourceId, out string destinationId, out string targetKey, out string error)
    {
        sourceId = destinationId = targetKey = string.Empty;
        error = string.Empty;
        bool settlementRoute = demand.Source.Kind == RebirthNpcLogisticsEndpointKind.Settlement &&
            demand.Destination.Kind == RebirthNpcLogisticsEndpointKind.Settlement;
        if (settlementRoute)
        {
            RebirthNpcSettlementInventoryEndpoint source =
                RebirthNpcSettlementInventoryEndpointRegistry.Ensure(demand.Source.SettlementId);
            RebirthNpcSettlementInventoryEndpoint destination =
                RebirthNpcSettlementInventoryEndpointRegistry.Ensure(demand.Destination.SettlementId);
            if (source == null || destination == null)
            { error = "Settlement endpoint registration failed."; return false; }
            sourceId = source.EndpointId;
            destinationId = destination.EndpointId;
            targetKey = "haul:settlement:" + demand.Source.ToPlanToken() + ":" +
                demand.Destination.ToPlanToken() + ":" + demand.ItemKey.Trim() + ":" +
                demand.Quantity.ToString(CultureInfo.InvariantCulture) + ":" +
                (demand.AllowPartial ? "partial" : "exact");
            return true;
        }

        if (demand.Source.Kind == RebirthNpcLogisticsEndpointKind.Settlement ||
            demand.Destination.Kind == RebirthNpcLogisticsEndpointKind.Settlement)
        {
            string endpointError;
            if (!TryEnsureEndpoint(demand.Source, out sourceId, out endpointError))
            { error = "Source endpoint invalid: " + endpointError; return false; }
            if (!TryEnsureEndpoint(demand.Destination, out destinationId, out endpointError))
            { error = "Destination endpoint invalid: " + endpointError; return false; }
            targetKey = "haul:bridge:" + EndpointKindToken(demand.Source) + ":" +
                demand.Source.ToPlanToken() + ":" + EndpointKindToken(demand.Destination) + ":" +
                demand.Destination.ToPlanToken() + ":" + demand.ItemKey.Trim() + ":" +
                demand.Quantity.ToString(CultureInfo.InvariantCulture) + ":" +
                (demand.AllowPartial ? "partial" : "exact");
            return true;
        }

        string physicalEndpointError;
        RebirthNpcPhysicalResourceEndpoint physicalSource =
            RebirthNpcPhysicalResourceEndpointRegistry.Ensure(demand.Source.ToPlanToken(), out physicalEndpointError);
        if (physicalSource == null) { error = physicalEndpointError; return false; }
        RebirthNpcPhysicalResourceEndpoint physicalDestination =
            RebirthNpcPhysicalResourceEndpointRegistry.Ensure(demand.Destination.ToPlanToken(), out physicalEndpointError);
        if (physicalDestination == null) { error = physicalEndpointError; return false; }
        sourceId = physicalSource.EndpointId;
        destinationId = physicalDestination.EndpointId;
        targetKey = "haul:resource:" + demand.Source.ToPlanToken() + ":" +
            demand.Destination.ToPlanToken() + ":" + demand.ItemKey.Trim() + ":" +
            demand.Quantity.ToString(CultureInfo.InvariantCulture) + ":" +
            (demand.AllowPartial ? "partial" : "exact");
        return true;
    }

    private static bool TryEnsureEndpoint(RebirthNpcLogisticsEndpointRef endpoint,
        out string endpointId, out string error)
    {
        endpointId = string.Empty;
        error = string.Empty;
        if (endpoint.Kind == RebirthNpcLogisticsEndpointKind.Settlement)
        {
            RebirthNpcSettlementInventoryEndpoint settlement =
                RebirthNpcSettlementInventoryEndpointRegistry.Ensure(endpoint.SettlementId);
            if (settlement == null) { error = "Settlement endpoint registration failed."; return false; }
            endpointId = settlement.EndpointId;
            return true;
        }
        RebirthNpcPhysicalResourceEndpoint physical =
            RebirthNpcPhysicalResourceEndpointRegistry.Ensure(endpoint.ToPlanToken(), out error);
        if (physical == null) return false;
        endpointId = physical.EndpointId;
        return true;
    }

    private static string EndpointKindToken(RebirthNpcLogisticsEndpointRef endpoint)
    {
        return endpoint.Kind == RebirthNpcLogisticsEndpointKind.Settlement
            ? "settlement" : "resource";
    }

    private static string ReplaceQuantity(string targetKey, int quantity)
    {
        string[] parts = (targetKey ?? string.Empty).Split(':');
        if (parts.Length >= 9 && string.Equals(parts[1], "bridge", StringComparison.OrdinalIgnoreCase))
            parts[7] = quantity.ToString(CultureInfo.InvariantCulture);
        else if (parts.Length >= 7)
            parts[5] = quantity.ToString(CultureInfo.InvariantCulture);
        return string.Join(":", parts);
    }

    private static RebirthNpcLogisticsTripPlan[] RejectPlan(string reason, out string detail)
    {
        detail = reason;
        Interlocked.Increment(ref rejected);
        return new RebirthNpcLogisticsTripPlan[0];
    }

    private static int CompareTrips(RebirthNpcLogisticsTripPlan a, RebirthNpcLogisticsTripPlan b)
    {
        int p = b.Priority.CompareTo(a.Priority);
        if (p != 0) return p;
        p = string.Compare(a.SourceId, b.SourceId, StringComparison.OrdinalIgnoreCase);
        if (p != 0) return p;
        p = string.Compare(a.DestinationId, b.DestinationId, StringComparison.OrdinalIgnoreCase);
        if (p != 0) return p;
        p = string.Compare(a.ItemKey, b.ItemKey, StringComparison.OrdinalIgnoreCase);
        return p != 0 ? p : a.DemandIndex.CompareTo(b.DemandIndex);
    }

    private static bool IsTerminal(RebirthNpcWorkAssignmentStatus status)
    {
        return status == RebirthNpcWorkAssignmentStatus.Completed ||
            status == RebirthNpcWorkAssignmentStatus.Cancelled ||
            status == RebirthNpcWorkAssignmentStatus.Failed;
    }
}
