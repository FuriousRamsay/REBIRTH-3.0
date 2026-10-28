using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Platform;
using UnityEngine;

namespace Rebirth.VehicleStudio.Diagnostics
{
    public sealed class ConsoleCmdCamperRouteCheck : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;
        public override DeviceFlag AllowedDeviceTypesClient => DeviceFlag.StandaloneWindows;
        public override string[] getCommands() => new[] { "camperroute" };
        public override string getDescription() => "Read-only full vehicle corridor terrain/block screening in CodexTest.";
        public override string getHelp() => "camperroute x z heading length width: centre, compass heading, metres. No terrain edits or vehicle movement.";
        public override void Execute(List<string> args, CommandSenderInfo sender)
        {
            try
            {
                var world = GameManager.Instance.World;
                if (GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest" || world == null || world.IsRemote())
                    throw new InvalidOperationException("Local disposable CodexTest only.");
                if (args.Count != 5) throw new ArgumentException(getHelp());
                float[] values = args.ConvertAll(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                foreach (float value in values) if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Finite values required.");
                float length = values[3], width = values[4];
                if (Mathf.Abs(values[0]) > 100000 || Mathf.Abs(values[1]) > 100000) throw new ArgumentException("Route centre is outside the supported bounded scan.");
                if (length < 5 || length > 120 || width < 4 || width > 40) throw new ArgumentException("Length5..120 and width4..40 metres required.");
                Vector3 forward = new Vector3(Mathf.Sin(values[2] * Mathf.Deg2Rad), 0, Mathf.Cos(values[2] * Mathf.Deg2Rad));
                Vector3 side = new Vector3(forward.z, 0, -forward.x);
                var samples = new List<object>();
                float min = float.MaxValue, max = float.MinValue;
                int blocked = 0, unloaded = 0;
                float extentX = Mathf.Abs(forward.x) * length / 2 + Mathf.Abs(side.x) * width / 2;
                float extentZ = Mathf.Abs(forward.z) * length / 2 + Mathf.Abs(side.z) * width / 2;
                float alongMargin = .5f * (Mathf.Abs(forward.x) + Mathf.Abs(forward.z));
                float acrossMargin = .5f * (Mathf.Abs(side.x) + Mathf.Abs(side.z));
                // Enumerate voxel columns rather than sample a rotated one-metre lattice,
                // which can miss columns between samples. Include intersecting edge cells.
                for (int x = Mathf.FloorToInt(values[0] - extentX) - 1; x <= Mathf.CeilToInt(values[0] + extentX) + 1; x++)
                    for (int z = Mathf.FloorToInt(values[1] - extentZ) - 1; z <= Mathf.CeilToInt(values[1] + extentZ) + 1; z++)
                    {
                        Vector3 delta = new Vector3(x + .5f - values[0], 0, z + .5f - values[1]);
                        if (Mathf.Abs(Vector3.Dot(delta, forward)) > length / 2 + alongMargin ||
                            Mathf.Abs(Vector3.Dot(delta, side)) > width / 2 + acrossMargin) continue;
                        float height = world.GetTerrainHeight(x, z);
                        // GetTerrainHeight identifies the occupied terrain cell, not its
                        // upper face. Headroom begins in the cell above that ground block.
                        int y = Mathf.FloorToInt(height) + 1;
                        if (float.IsNaN(height) || float.IsInfinity(height) || world.ChunkCache.GetChunkFromWorldPos(x,y,z) == null)
                        { unloaded++; samples.Add(new { x, z, unavailable = true }); continue; }
                        min = Mathf.Min(min, height); max = Mathf.Max(max, height);
                        var obstacles = new List<string>();
                        for (int layer = y; layer < y + 3; layer++)
                        {
                            BlockValue block = world.GetBlock(x, layer, z);
                            if (block.isWater || !block.isair && block.Block != null && block.Block.IsCollideMovement)
                                obstacles.Add(layer + ":" + block.Block.GetBlockName());
                        }
                        if (obstacles.Count != 0) blocked++;
                        samples.Add(new { x, z, height, obstacles });
                    }
                bool screened = unloaded == 0 && blocked == 0 && max - min <= .15f;
                Directory.CreateDirectory(CamperDiagnosticsMod.ExportFolder);
                string file = Path.Combine(CamperDiagnosticsMod.ExportFolder,"route_" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(file, JsonConvert.SerializeObject(new {
                    status = screened ? "PASS_ROUTE_SCREENING_NOT_DRIVING_ACCEPTANCE" : "REJECT_ROUTE",
                    centre = new[] { values[0], values[1] }, heading = values[2], length, width,
                    heightSpread = min == float.MaxValue ? (float?)null : max - min, blockedColumns = blocked, unloadedColumns = unloaded,
                    scope = "Loaded voxel ground/headroom grid; dynamic entities and actual collider/vehicle envelope still need verification.", samples
                }, Formatting.Indented));
                SdtdConsole.Instance.Output("[CamperRoute] " + (screened ? "SCREENED " : "REJECTED ") + file);
            }
            catch (Exception error) { SdtdConsole.Instance.Output("*** ERROR [CamperRoute] " + error); }
        }
    }
}
