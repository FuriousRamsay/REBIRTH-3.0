using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RebirthProfiler;

/// <summary>
/// The repeatable stress scenarios: standing in open ground, riding a motorcycle through open country, standing in the densest city, riding
/// into the city, and a 100-zombie horde in the city. The player is in god mode for all of it (no death loop) and every phase is a labelled
/// segment of the Play recording, so frame rate and CPU cost can be compared between builds.
/// </summary>
static class ScenarioRunner
{
    public static string OutDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "out"));

    sealed record City(double X, double Z, string Label, int PoiId);

    /// <summary>Densest cluster of POI centres (most neighbours within 150 m) from the map, as a stand-in for "the big city".</summary>
    static async Task<City> FindCity(HitchRunner.BridgeClient br, CancellationToken ct)
    {
        string json = await br.Get("/pois?maxTier=6&radius=6000&limit=400&max=400&count=400", ct);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var pts = doc.RootElement.GetProperty("pois").EnumerateArray().Select(p => (
                id: p.GetProperty("id").GetInt32(), label: p.GetProperty("label").GetString() ?? "",
                x: p.GetProperty("center")[0].GetDouble(), z: p.GetProperty("center")[1].GetDouble())).ToList();
            var best = pts.OrderByDescending(a => pts.Count(b => Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z)) < 150)).First();
            return new City(best.x, best.z, best.label, best.id);
        }
        catch { return new City(-381, 594, "Uncle Ray's Supermarket (default)", 3723); }
    }

    static async Task<(double x, double y, double z)> Pos(HitchRunner.BridgeClient br, CancellationToken ct)
    {
        string s = await br.Get("/state?sections=player", ct);
        var m = Regex.Match(s, "\"position\":\\s*\\[\\s*(-?[\\d\\.]+)\\s*,\\s*(-?[\\d\\.]+)\\s*,\\s*(-?[\\d\\.]+)");
        return m.Success ? (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture)) : (0, 60, 0);
    }

    public static async Task<string> Run(string livePath, IProgress<string> log, CancellationToken ct)
    {
        var br = HitchRunner.ConnectBridge() ?? throw new InvalidOperationException("The game bridge is not running (launch without \"Let me play\").");
        if (!(await br.Get("/ping", ct)).Contains("\"state\":\"ingame\"")) throw new InvalidOperationException("The game is not in a save yet.");
        string F(double v) => v.ToString("F1", CultureInfo.InvariantCulture);
        async Task Mark(string label) { File.WriteAllText(livePath + ".mark", label); log.Report("segment: " + label); await Task.Delay(3000, ct); }
        async Task Console(string cmd) => await br.Post("/console?cmd=" + Uri.EscapeDataString(cmd), ct);
        var notes = new List<string>();

        await br.Post("/guard?heal=0&needs=0", ct);
        await br.Post("/godmode?on=1", ct);
        await Console("killall"); await Console("settime 1 9 0");
        var city = await FindCity(br, ct);
        notes.Add($"city: {city.Label} at {city.X:F0},{city.Z:F0}");

        async Task Ride(string label, double seconds, double heading)
        {
            string veh = await br.Post("/spawn?entity=vehicleMotorcycle&distance=3", ct);
            string id = Regex.Match(veh, "\"id\":(\\d+)").Groups[1].Value;
            await Task.Delay(2000, ct);
            await br.Post("/mount?entity=" + id, ct);
            await Mark(label);
            string r = await br.Post($"/drive?speed=28&seconds={F(seconds)}&heading={F(heading)}", ct);
            notes.Add($"ride {label}: {r}");
            await br.Post("/dismount", ct);
            await Console("kill " + id);
        }

        log.Report("baseline in the open field");
        await Console("teleport 40 60 1010"); await Task.Delay(12000, ct);
        await Mark("baseline_field"); await Task.Delay(40000, ct);

        await Ride("motorcycle_open_country", 75, 90);

        log.Report("city: standing");
        await Console($"teleport {F(city.X)} 90 {F(city.Z)}"); await Task.Delay(30000, ct);
        await Console("killall");
        await Mark("city_standing");
        for (int i = 0; i < 12; i++)
        {
            var p = await Pos(br, ct); double a = i * 30 * Math.PI / 180;
            await br.Post($"/lookat?x={F(p.x + Math.Cos(a) * 40)}&y={F(p.y + 4)}&z={F(p.z + Math.Sin(a) * 40)}", ct);
            await Task.Delay(5000, ct);
        }

        log.Report("city: riding in from the east");
        await Console($"teleport {F(city.X + 230)} 90 {F(city.Z + 16)}"); await Task.Delay(20000, ct);
        await Ride("motorcycle_in_city", 75, 270);

        log.Report("city: horde");
        await Console($"teleport {F(city.X)} 90 {F(city.Z)}"); await Task.Delay(25000, ct);
        await Console("killall");
        var hp = await Pos(br, ct);
        await Console($"spawnentityat zombieArlene {F(hp.x + 14)} {F(hp.y)} {F(hp.z)} 100");
        await Task.Delay(6000, ct);
        notes.Add("aggro: " + await br.Post("/aggro?radius=120", ct));
        await Mark("horde_100_in_city");
        for (int i = 0; i < 12; i++) { await Task.Delay(5000, ct); await br.Post("/aggro?radius=120", ct); }
        await Mark("end");
        await Console("killall");
        await Task.Delay(6000, ct);
        string notesPath = Path.Combine(OutDir, "scenarios_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
        File.WriteAllLines(notesPath, notes);
        return notesPath;
    }
}
