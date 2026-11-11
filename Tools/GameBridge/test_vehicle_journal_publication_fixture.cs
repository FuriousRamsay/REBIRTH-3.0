using System;
using System.Collections.Generic;
using System.Threading;

public enum RebirthVehicleRequestOutcome { Applied, Indeterminate }

public class RebirthVehicleRequestResult
{
    public string ActorId, Fingerprint, Message, PickupNativeItem, Snapshot;
    public int Carrier, Position, EntityId;
    public RebirthVehicleRequestOutcome Outcome;
    public RebirthVehicleRequestResult Clone() => (RebirthVehicleRequestResult)MemberwiseClone();
}

static class RebirthVehicleAssemblySerializer
{
    public static string ToBase64(string snapshot) => snapshot;
}

static class GameIO
{
    public static string GetSaveGameDir() => "A";
}

static class Test
{
    private sealed class RequestIntent { internal string Actor, Fingerprint; }
    private static readonly object Sync = new object();
    private static Dictionary<Guid, RebirthVehicleRequestResult> Completed = new Dictionary<Guid, RebirthVehicleRequestResult>();
    private static Queue<Guid> Order = new Queue<Guid>();
    private static readonly Dictionary<Guid, RequestIntent> InFlight = new Dictionary<Guid, RequestIntent>();
    private const int MaxEntries = 512;
    private static readonly string loadedDirectory = "A";
    private static readonly long generation = 1;
    private static long? writingGeneration;
    private static bool WritingCurrentJournal => writingGeneration == generation;
    private static bool loadFailed, refuse;
    private static int saves;

    private static RebirthVehicleRequestResult CloneResult(RebirthVehicleRequestResult result) => result.Clone();

    private static bool Persist(string directory, Dictionary<Guid, RebirthVehicleRequestResult> completed, Queue<Guid> order, Guid settled)
    {
        if (!Monitor.IsEntered(Sync) || directory != "A" || !WritingCurrentJournal ||
            Completed.ContainsKey(settled) || !InFlight.ContainsKey(settled) ||
            !completed.ContainsKey(settled) || !order.Contains(settled))
            throw new Exception("Publication exposed a result before persistence.");

        saves++;
        if (!refuse) return true;
        loadFailed = true;
        return false;
    }

    // SOURCE

    private static RebirthVehicleRequestResult Begin(Guid id)
    {
        InFlight.Add(id, new RequestIntent { Actor = "owner", Fingerprint = "refuel" });
        return new RebirthVehicleRequestResult
        {
            ActorId = "owner", Fingerprint = "refuel", Message = "applied", PickupNativeItem = string.Empty, Snapshot = "saved"
        };
    }

    private static void Main()
    {
        var saved = Guid.NewGuid();
        var result = Begin(saved);
        if (!Remember(saved, result, generation) || saves != 1 || !Completed.ContainsKey(saved) || InFlight.ContainsKey(saved))
            throw new Exception("Durable result did not publish.");
        if (!Remember(saved, result, generation) || saves != 1)
            throw new Exception("Exact duplicate wrote again.");
        result.Snapshot = "different";
        if (Remember(saved, result, generation) || saves != 1)
            throw new Exception("Different result replayed as an exact duplicate.");

        var unsaved = Guid.NewGuid();
        refuse = true;
        if (Remember(unsaved, Begin(unsaved), generation) || saves != 2 || Completed.ContainsKey(unsaved) ||
            !InFlight.ContainsKey(unsaved) || !Completed.ContainsKey(saved) || Order.Count != 1)
            throw new Exception("Failed persistence changed the published result or intent.");

        Console.WriteLine("PASS: exact duplicate, divergent duplicate, and failed publication retaining the previous durable view.");
    }
}
