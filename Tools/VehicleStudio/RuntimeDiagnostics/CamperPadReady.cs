using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Platform;
using UnityEngine;

namespace Rebirth.VehicleStudio.Diagnostics
{
    public sealed class ConsoleCmdCamperPadReady : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;
        public override DeviceFlag AllowedDeviceTypesClient => DeviceFlag.StandaloneWindows;
        public override string[] getCommands() => new[] { "camperpadready" };
        public override string getDescription() => "Read-only exact pad readiness; explicit one-shot air-hole repair for the current prepared pad.";
        public override void Execute(List<string> args, CommandSenderInfo sender)
        {
            try
            {
                var world = GameManager.Instance.World;
                bool repair = args.Count == 6 && args[0] == "repair";
                if (repair) { args = new List<string>(args); args.RemoveAt(0); }
                if (args.Count != 5 || world == null || world.IsRemote() || GameManager.IsDedicatedServer ||
                    GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest") throw new Exception("camperpadready [repair] xMin zMin floorY width length; local CodexTest only.");
                int x = int.Parse(args[0], CultureInfo.InvariantCulture), z = int.Parse(args[1], CultureInfo.InvariantCulture),
                    y = int.Parse(args[2], CultureInfo.InvariantCulture), width = int.Parse(args[3]), length = int.Parse(args[4]);
                if (width < 8 || width > 40 || length < 20 || length > 80 || y < 5 || y > 245) throw new Exception("Bounded pad required.");
                var concrete = Block.GetBlockValue("concreteShapes:cube", false);
                if (repair)
                {
                    Repair(world, x, z, y, width, length, concrete);
                    return;
                }
                bool ready = true;
                var samples = new List<object>();
                var missingBlocks = new List<object>();
                // Full native surface verification; sampled collider hits include
                // every fourth cell plus the terminal edges (no vehicle present).
                for (int dx = 0; dx < width; dx++) for (int dz = 0; dz < length; dz++)
                {
                    var point = new Vector3i(x + dx, y, z + dz);
                    bool loaded = world.ChunkCache.GetChunkFromWorldPos(point.x, point.y, point.z) != null;
                    BlockValue actual = loaded ? world.GetBlock(point) : BlockValue.Air;
                    bool blockReady = loaded && actual.type == concrete.type;
                    if (!blockReady)
                    {
                        ready = false;
                        missingBlocks.Add(new { position = point, loaded, expectedType = concrete.type,
                            actualType = actual.type, actualBlock = loaded && actual.Block != null ? actual.Block.GetBlockName() : null });
                    }
                    if (dx % 4 != 0 && dx != width - 1 || dz % 4 != 0 && dz != length - 1) continue;
                    Vector3 start = new Vector3(point.x + .5f, y + 3, point.z + .5f) - Origin.position;
                    RaycastHit hit;
                    bool collided = Physics.Raycast(start, Vector3.down, out hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    float top = collided ? hit.point.y + Origin.position.y : float.NaN;
                    bool colliderReady = collided && hit.collider != null && Mathf.Abs(top - (y + 1)) < .05f;
                    ready &= colliderReady;
                    samples.Add(new { position = point, blockReady, colliderReady, collider = collided ? hit.collider.name : null,
                        layer = collided ? hit.collider.gameObject.layer : -1, surfaceY = collided ? (float?)top : null });
                }
                Directory.CreateDirectory(CamperDiagnosticsMod.ExportFolder);
                string file = Path.Combine(CamperDiagnosticsMod.ExportFolder, "pad_ready_" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(file, JsonConvert.SerializeObject(new { kind = "NATIVE_CONCRETE_PAD_READINESS", ready, xMin = x, zMin = z, floorBlockY = y, width, length, samples, missingBlocks }, Formatting.Indented));
                SdtdConsole.Instance.Output("[CamperPadReady] CAPTURED " + file);
            }
            catch (Exception error) { SdtdConsole.Instance.Output("*** ERROR [CamperPadReady] " + error.Message); }
        }

        private static void Repair(World world, int x, int z, int y, int width, int length, BlockValue concrete)
        {
            var authority = PreparedCamperPad.Current;
            if (authority == null || !authority.Matches(world, x, z, y, width, length) || authority.RepairAttempted)
                throw new Exception("One unspent exact pad preparation in this World instance required; saved receipts cannot authorize repair.");
            if (concrete.isair || concrete.Block == null || !concrete.Block.IsCollideMovement)
                throw new Exception("Exact native solid concrete shape required.");
            var player = world.GetPrimaryPlayer();
            if (player == null || (new Vector3(x + width / 2f, player.GetPosition().y, z + length / 2f) - player.GetPosition()).sqrMagnitude > 150f * 150f)
                throw new Exception("Prepared pad must remain nearby.");
            Vector3 playerPoint = player.GetPosition();
            if (float.IsNaN(playerPoint.x) || float.IsInfinity(playerPoint.x) || float.IsNaN(playerPoint.y) || float.IsInfinity(playerPoint.y)
                || float.IsNaN(playerPoint.z) || float.IsInfinity(playerPoint.z)
                || (playerPoint.x >= x - 4 && playerPoint.x <= x + width + 4 && playerPoint.z >= z - 4 && playerPoint.z <= z + length + 4))
                throw new Exception("Player must remain outside the protected pad footprint.");

            // Revalidate current protection evidence; no saved/preflight snapshot
            // proves that ownership, occupancy or loading remains unchanged.
            foreach (var entity in world.Entities.list)
            {
                Vector3 point = entity.GetPosition();
                if (float.IsNaN(point.x) || float.IsInfinity(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.y)
                    || float.IsNaN(point.z) || float.IsInfinity(point.z))
                    throw new Exception("Finite current entity positions required.");
                if (point.x >= x - 4 && point.x <= x + width + 4 && point.z >= z - 4 && point.z <= z + length + 4)
                    throw new Exception("Pad repair entity blocker: " + JsonConvert.SerializeObject(new {
                        id = entity.entityId, nativeType = entity.GetType().FullName, position = point })
                        + "; no repair changes applied.");
            }
            var decorator = GameManager.Instance.GetDynamicPrefabDecorator();
            if (decorator == null || decorator.allPrefabs == null)
                throw new Exception("Authoritative prefab list unavailable; repair refused.");
            var prefabs = new List<PrefabInstance>();
            decorator.GetAllPrefabs(prefabs);
            foreach (var prefab in prefabs)
            {
                if (prefab == null || prefab.prefab == null)
                    throw new Exception("Invalid authoritative prefab entry; repair refused.");
                if (ConsoleCmdCamperTrackFixture.FootprintsIntersect(x, z, width, length, prefab.boundingBoxPosition, prefab.boundingBoxSize))
                    throw new Exception("Authoritative prefab overlaps the protected pad footprint; repair refused.");
            }
            // Native 3-coordinate chunk lookup delegates to x/z lookup; one
            // loaded chunk includes all world heights0..255. Enumerate each
            // actual native TE collection once instead of a million cell calls.
            var checkedChunks = new HashSet<Chunk>();
            for (int hx = x - 4; hx <= x + width + 4; hx++)
                for (int hz = z - 4; hz <= z + length + 4; hz++)
                {
                    var chunk = world.ChunkCache.GetChunkFromWorldPos(hx, y, hz) as Chunk;
                    if (chunk == null) throw new Exception("Entire tile-entity protection halo must remain loaded; repair refused.");
                    if (!checkedChunks.Add(chunk)) continue;
                    var tiles = chunk.GetTileEntities();
                    if (tiles == null || tiles.list == null) throw new Exception("Actual native tile-entity collection unavailable; repair refused.");
                    foreach (var tile in tiles.list)
                    {
                        if (tile == null) throw new Exception("Invalid native tile-entity entry; repair refused.");
                        Vector3i point = tile.ToWorldPos();
                        if (point.x >= x - 4 && point.x <= x + width + 4 && point.z >= z - 4 && point.z <= z + length + 4)
                            throw new Exception("Tile entity in full-height protection halo; repair refused.");
                    }
                }

            var changes = new List<BlockChangeInfo>();
            var repairedPositions = new List<Vector3i>();
            for (int dx = 0; dx < width; dx++) for (int dz = 0; dz < length; dz++)
            {
                var point = new Vector3i(x + dx, y, z + dz);
                if (world.ChunkCache.GetChunkFromWorldPos(point.x, point.y, point.z) == null)
                    throw new Exception("Whole pad surface must remain loaded; repair refused.");
                var actual = world.GetBlock(point);
                if (actual.Equals(concrete)) continue;
                if (!actual.isair || actual.isWater || actual.ischild)
                    throw new Exception("Surface contains a non-concrete/non-air block; no unknown, water or user block may be overwritten.");
                var below = new Vector3i(point.x, y - 1, point.z);
                if (world.ChunkCache.GetChunkFromWorldPos(below.x, below.y, below.z) == null || !world.GetBlock(below).Equals(concrete))
                    throw new Exception("Every air hole requires exact loaded concrete directly beneath it; repair refused.");
                repairedPositions.Add(point);
                changes.Add(new BlockChangeInfo(new BlockValueRef(point), concrete));
                if (changes.Count > 32) throw new Exception("At most32 supported surface air holes may be repaired.");
            }
            if (changes.Count == 0) throw new Exception("No supported surface air holes; repair budget remains unspent.");
            // Consume before dispatch: an uncertain native mutation or receipt
            // write must never allow a replay of this pad's repair.
            authority.RepairAttempted = true;
            world.SetBlocksRPC(changes);
            Directory.CreateDirectory(CamperDiagnosticsMod.ExportFolder);
            string file = Path.Combine(CamperDiagnosticsMod.ExportFolder, "pad_air_hole_repair_" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(file, JsonConvert.SerializeObject(new { kind = "CAMPER_PAD_AIR_HOLE_REPAIR", xMin = x, zMin = z,
                floorBlockY = y, width, length, repairedPositions, repairedCount = changes.Count,
                sameWorldPreparation = true, repairBudgetConsumed = true, readinessAcceptance = false, drivingAcceptance = false }, Formatting.Indented));
            SdtdConsole.Instance.Output("[CamperPadRepair] CAPTURED " + file);
        }
    }
}
