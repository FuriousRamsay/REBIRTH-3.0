using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class ConsoleCmdRebirthListPrefabs : ConsoleCmdAbstract
{
    private const int PageSize = 30;
    private static readonly FastTags<TagGroup.Poi> Excluded = FastTags<TagGroup.Poi>.Parse("rwgonly,streettile,hideui,part");
    public override bool IsExecuteOnClient => false;
    public override int DefaultPermissionLevel => 1000;
    public override string[] getCommands() => new[] { "listprefabs" };
    public override string getDescription() => "Lists map POI types or nearby instances, bounds, tags and sleeper-room counts.";
    public override string getHelp() => "listprefabs [map|radius] [nameFilter=*] [page=1]\nDefault map: unique playable POI types, ordered by tier (legacy behavior). Radius: 1..1024 blocks, individual nearby instances. Excludes hidden/street/part prefabs and types without sleeper rooms. 30 rows/page. Authenticated in-game player required. Read-only; does not load or activate rooms.";

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        try
        {
            if (args != null && args.Count == 1 && args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
            { SdtdConsole.Instance.Output(getHelp()); return; }
            int radius = 256, page = 1;
            bool map = args == null || args.Count == 0 || args[0].Equals("map", StringComparison.OrdinalIgnoreCase);
            if (args != null && (args.Count > 3 || args.Count > 0 && !map && !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out radius)
                || args.Count > 2 && !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out page))
                || radius < 1 || radius > 1024 || page < 1 || page > 10000)
            { SdtdConsole.Instance.Output(getHelp()); return; }
            string filter = args != null && args.Count > 1 && args[1] != "*" ? args[1] : string.Empty;
            var world = GameManager.Instance?.World;
            var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() || connection == null || !connection.IsServer)
            { SdtdConsole.Instance.Output("listprefabs: an authoritative loaded world is required."); return; }
            EntityPlayer player = null;
            var client = sender.RemoteClientInfo;
            if (client != null)
            {
                if (client.loginDone && ReferenceEquals(connection.Clients.ForEntityId(client.entityId), client)
                    && world.Entities.dict.TryGetValue(client.entityId, out var entity)) player = entity as EntityPlayer;
            }
            else if (sender.IsLocalGame && !GameManager.IsDedicatedServer) player = world.GetPrimaryPlayer();
            if (player == null || !ReferenceEquals(player.world, world) || !player.IsSpawned())
            { SdtdConsole.Instance.Output("listprefabs: run from your authenticated in-game player console."); return; }
            var position = player.position;
            if (float.IsNaN(position.x) || float.IsNaN(position.z) || Math.Abs((double)position.x) > int.MaxValue - 2048d
                || Math.Abs((double)position.z) > int.MaxValue - 2048d)
            { SdtdConsole.Instance.Output("listprefabs: invalid player position."); return; }
            int x = Mathf.FloorToInt(position.x), z = Mathf.FloorToInt(position.z);
            List<PrefabInstance> candidates;
            if (map)
            {
                candidates = GameManager.Instance.GetDynamicPrefabDecorator()?.allPrefabs;
                if (candidates == null) { SdtdConsole.Instance.Output("listprefabs: native map prefab index is unavailable."); return; }
                if (candidates.Count > 131072) { SdtdConsole.Instance.Output("listprefabs: map index exceeds the one-shot scan budget; use a radius query."); return; }
            }
            else
            {
                candidates = new List<PrefabInstance>();
                world.GetPOIsAtXZ(x - radius, x + radius, z - radius, z + radius, candidates);
                if (candidates.Count > 4096)
                { SdtdConsole.Instance.Output("listprefabs: more than 4096 indexed instances; choose a smaller radius."); return; }
            }
            var rows = new List<string>();
            var seen = new HashSet<PrefabInstance>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var poi in candidates)
            {
                if (poi?.prefab == null || !seen.Add(poi) || poi.prefab.Tags.Test_AnySet(Excluded)
                    || poi.prefab.SleeperVolumeList == null || poi.prefab.SleeperVolumeList.Count == 0
                    || (filter.Length > 0 && (poi.name ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    || map && !names.Add(poi.prefab.PrefabName)) continue;
                rows.Add(string.Format(CultureInfo.InvariantCulture, "T{3:00} {0} | representativeOrigin={1} | size={2} | tier={3} | rooms={4} | tags={5}",
                    Clean(poi.name), poi.boundingBoxPosition, poi.boundingBoxSize, poi.prefab.DifficultyTier,
                    poi.prefab.SleeperVolumeList?.Count ?? 0, Clean(poi.prefab.Tags.ToString())));
            }
            rows.Sort(StringComparer.Ordinal);
            int pages = Math.Max(1, (rows.Count + PageSize - 1) / PageSize);
            SdtdConsole.Instance.Output(string.Format(CultureInfo.InvariantCulture,
                "listprefabs: {6}; indexed square +/-{0} at {1},{2} for radius mode; {3} rows; page {4}/{5}", radius, x, z, rows.Count, page, pages, map ? "map unique playable POI types" : "nearby playable instances"));
            if (page > pages) { SdtdConsole.Instance.Output("listprefabs: page is outside the result set."); return; }
            for (int i = (page - 1) * PageSize; i < rows.Count && i < page * PageSize; i++) SdtdConsole.Instance.Output(rows[i]);
        }
        catch (Exception error) { SdtdConsole.Instance.Output("listprefabs: " + error.Message); }
    }
    private static string Clean(string value) => (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
}
