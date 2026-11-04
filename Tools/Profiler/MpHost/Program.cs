using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using RebirthProfiler;

// Test-only two-peer host. Reuses the actual profiler launcher; never touches the
// ordinary launcher's no-duplicate guard or the user's GameBridge configuration.
internal static class Program
{
    const string Save = "CodexHelpRemote20261102";
    const string Data = "E:/_Haven/shared/CodexHelpRemote20261102";
    record Peer(int Pid, DateTime StartUtc, string Executable, string Role);
    static int Main(string[] args)
    {
        try
        {
            string mod = FindMod();
            bool purge = args.Contains("--fixture=purge");
            if (args.Any(a => a.StartsWith("--fixture=") && a != "--fixture=purge" && a != "--fixture=help"))
                throw new Exception("Unknown isolated fixture.");
            string save = purge ? "CodexPurgeRemote20261102" : Save;
            string data = purge ? "E:/_Haven/shared/CodexPurgeRemote20261102" : Data;
            string staging = Path.Combine(mod, purge ? "Tools/Profiler/out/purge-remote-20261102" : "Tools/Profiler/out/help-remote-20261102");
            string config = Path.Combine(staging, "serverconfig.xml");
            string role = args.FirstOrDefault(a => a.StartsWith("--role="))?.Substring(7) ?? "";
            if (role != "server" && role != "client") throw new Exception("Use --role=server|client [--validate].");
            string exe = Path.GetFullPath(Path.Combine(mod, "../..", "7DaysToDie.exe"));
            string module = Path.Combine(mod, "Tools/Profiler/native/mono-profiler-rebirthprof.dll");
            if (!File.Exists(exe) || !File.Exists(module)) throw new Exception("Exact game/sampler absent.");
            var settings = XDocument.Load(config).Root!.Elements("property")
                .ToDictionary(e => (string)e.Attribute("name")!, e => (string)e.Attribute("value")!);
            Require(settings, "GameName", save); Require(settings, "UserDataFolder", data);
            Require(settings, "ServerPort", purge ? "26903" : "26904"); Require(settings, "ServerVisibility", "0");
            if (purge && (string?)XDocument.Load(config).Root?.Element("RebirthSettings")?.Attribute("RebirthSandboxCode") != "RBXCTBAGAATCAUAAVACRA")
                throw new Exception("Expected exact qualified Purge sandbox snapshot.");
            Require(settings, "EACEnabled", "false"); Require(settings, "ServerMaxPlayerCount", "2");
            var running = Process.GetProcessesByName("7DaysToDie").Concat(Process.GetProcessesByName("7DaysToDieServer")).ToArray();
            if (role == "server")
            {
                if (running.Length != 0) throw new Exception("Existing game present; untouched.");
            }
            else
            {
                var peer = JsonSerializer.Deserialize<Peer>(File.ReadAllText(Path.Combine(staging, "server.owner.json")))!;
                if (peer.Role != "server" || running.Length != 1 || running[0].Id != peer.Pid ||
                    running[0].StartTime.ToUniversalTime() != peer.StartUtc ||
                    !string.Equals(running[0].MainModule!.FileName, peer.Executable, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(peer.Executable, exe, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Only the exact owned dedicated server may precede this client.");
            }
            string gameArgs = role == "server"
                ? $"-quit -batchmode -nographics -logfile \"{Path.Combine(staging, "server.log")}\" -configfile=\"{config}\" -rebirthbridge={(purge ? 8863 : 8864)} -dedicated"
                : $"-noeac -logfile \"{Path.Combine(staging, "client.log")}\" -rebirthbridge={(purge ? 8873 : 8874)} -SkipSpawnButton=false -SkipNewsScreen=true -skipintro -screen-fullscreen 0 -screen-width 1280 -screen-height 720";
            Console.WriteLine(JsonSerializer.Serialize(new { role, save, data, gameArgs, validateOnly = args.Contains("--validate") }));
            if (args.Contains("--validate")) return 0;
            Environment.SetEnvironmentVariable("REBIRTH_BRIDGE_NOCHORES", "1");
            Environment.SetEnvironmentVariable("PROFILER_TIMELINE", "1");
            Environment.SetEnvironmentVariable("SteamAppId", "251570");
            Environment.SetEnvironmentVariable("SteamGameId", "251570");
            string profile = Path.Combine(staging, $"profile_{role}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            int pid = GameLauncher.Launch(exe, gameArgs, module, profile, true);
            using var process = Process.GetProcessById(pid);
            // Retain the process handle before it exits; GetProcessById alone
            // cannot recover ExitCode after Windows removes the process.
            _ = process.Handle;
            File.WriteAllText(Path.Combine(staging, role + ".owner.json"),
                JsonSerializer.Serialize(new Peer(pid, process.StartTime.ToUniversalTime(), exe, role)));
            Console.WriteLine(JsonSerializer.Serialize(new { role, pid, profile }));
            process.WaitForExit();
            Console.WriteLine(JsonSerializer.Serialize(new { role, pid, exitCode = process.ExitCode }));
            return process.ExitCode;
        }
        catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
    }
    static void Require(Dictionary<string, string> settings, string key, string expected)
    { if (!settings.TryGetValue(key, out var actual) || actual != expected) throw new Exception("Unexpected isolated setting: " + key); }
    static string FindMod()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "RebirthUtils.csproj"))) return d.FullName;
        throw new Exception("Must run under the intended mod checkout.");
    }
}
