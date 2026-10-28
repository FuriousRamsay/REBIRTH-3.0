using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Platform;
using UnityEngine;

namespace Rebirth.VehicleStudio.Diagnostics
{
    // Runtime-only authority: never reconstructed from a receipt or saved world.
    internal sealed class PreparedCamperPad
    {
        internal static PreparedCamperPad Current;
        internal readonly WorldBase World;
        internal readonly int X, Z, FloorY, Width, Length;
        internal bool RepairAttempted;
        internal PreparedCamperPad(WorldBase world, int x, int z, int floorY, int width, int length)
        { World = world; X = x; Z = z; FloorY = floorY; Width = width; Length = length; }
        internal bool Matches(WorldBase world, int x, int z, int floorY, int width, int length)
        { return ReferenceEquals(World, world) && X == x && Z == z && FloorY == floorY && Width == width && Length == length; }
    }

    public sealed class ConsoleCmdCamperTrackFixture : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;
        public override DeviceFlag AllowedDeviceTypesClient => DeviceFlag.StandaloneWindows;
        public override string[] getCommands() => new[] { "camperfixture", "camperpad" };
        public override string getDescription() => "Explicit local CodexTest vehicle placement or flat concrete comparison pad preparation.";
        public override string getHelp() => "camperfixture ID x y z heading; camperpad [inspect] CodexTest x z floorBlockY length width (floor0=automatic). Local only. Pad preserves all tile entities/4m margin and refuses structures/water/unloaded chunks/entities; max80x40. Inspect never writes. Setup is not driving acceptance.";
        private static float Number(string value)
        {
            float result = float.Parse(value, CultureInfo.InvariantCulture);
            if (float.IsNaN(result) || float.IsInfinity(result)) throw new Exception("Finite coordinates required.");
            return result;
        }
        internal static bool FootprintsIntersect(int xMin, int zMin, int width, int length, Vector3i origin, Vector3i size)
        {
            if (size.x <= 0 || size.y <= 0 || size.z <= 0) throw new Exception("Invalid authoritative prefab bounds.");
            return (long)origin.x < (long)xMin + width + 4 && (long)origin.x + size.x > (long)xMin - 4
                && (long)origin.z < (long)zMin + length + 4 && (long)origin.z + size.z > (long)zMin - 4;
        }
        internal static bool IsVerifiedEmber(string name, bool nativeDecoration, bool hasTileEntity)
        {
            return nativeDecoration && !hasTileEntity && (name == "emberPileDeco1" || name == "emberPileDeco2"
                || name == "emberPileDeco3" || name == "emberPileDeco4" || name == "emberPileDeco5"
                || name == "emberPileDeco6" || name == "emberPileDeco7" || name == "emberPileDeco8");
        }
        internal static bool IsVerifiedFieldClutter(string name,bool nativeDecoration,bool hasTileEntity)
        {
            if(hasTileEntity)return false;
            // Exact definitions inspected in native blocks.xml; no prefix-wide
            // admission of cars, ores, crops, structures or unknown mod blocks.
            if(name=="rubblePileBricksSmall")return true; // Native debris; no terrain flag in its parent.
            if(!nativeDecoration)return false;
            return name=="rock01"||name=="rock02"||name=="oreLeadBoulder"||name=="plantShrubDead"||
                name=="mushroomBiome4"||name=="mushroomRadiated02"||name=="mushroomRadiated04"||
                name=="cinderBlocks01"||name=="cinderBlocks03"||name=="scrapMetalPile"||
                name=="driftwood2"||name=="hubcapNoMine"||name=="burntSedan01"||name=="abandonedPickupTruck01Purple";
        }
        public override void Execute(List<string> args, CommandSenderInfo sender)
        {
            try
            {
                var world = GameManager.Instance.World;
                var player = world == null ? null : world.GetPrimaryPlayer();
                if (GameManager.IsDedicatedServer || world == null || world.IsRemote() || player == null
                    || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest")
                    throw new Exception(getHelp());
                object evidence;
                PreparedCamperPad preparedPad = null;
                bool inspect=args.Count==7&&args[0]=="inspect";
                if(inspect){args=new List<string>(args);args.RemoveAt(0);}
                if (args.Count == 5)
                {
                    var entity = world.GetEntity(int.Parse(args[0])) as EntityVehicle;
                    if (entity == null) throw new Exception("Existing test vehicle required.");
                    string name = EntityClass.list[entity.entityClass].entityClassName;
                    if (name != "rebirthCamper80s" && name != "vehicleTruck4x4") throw new Exception("Camper or native4x4 only.");
                    if (entity.vehicleRB == null || entity.vehicleRB.velocity.magnitude > .1f || entity.GetFirstAttached() != null)
                        throw new Exception("Stop and dismount the vehicle before fixture placement.");
                    Vector3 target = new Vector3(Number(args[1]), Number(args[2]), Number(args[3]));
                    float heading = Number(args[4]);
                    if ((target - player.GetPosition()).sqrMagnitude > 150f * 150f || target.y < 5 || target.y > 250
                        || world.ChunkCache.GetChunkFromWorldPos((int)target.x, (int)target.y, (int)target.z) == null)
                        throw new Exception("Nearby loaded placement within world height required.");
                    Vector3 before = entity.GetPosition();
                    entity.SetPosition(target, true);
                    entity.SetRotation(new Vector3(0, heading, 0));
                    evidence = new { kind = "VEHICLE_PLACEMENT_FIXTURE", entity = entity.entityId, before, target, heading, drivingAcceptance = false };
                }
                else if (args.Count == 6 && args[0] == "CodexTest")
                {
                    int xCenter = (int)Number(args[1]), zCenter = (int)Number(args[2]);
                    int floorY = (int)Number(args[3]), length = (int)Number(args[4]), width = (int)Number(args[5]);
                    if (length < 20 || length > 80 || width < 8 || width > 40 || floorY < 0 || floorY > 245)
                        throw new Exception("Bounded pad dimensions required.");
                    int xMin = xCenter - width / 2, zMin = zCenter - length / 2;
                    if ((new Vector3(xCenter, player.GetPosition().y, zCenter) - player.GetPosition()).sqrMagnitude > 150f * 150f)
                        throw new Exception("Pad must be nearby and already loaded.");
                    var entityBlockers=new List<object>();
                    foreach (var entity in world.Entities.list)
                    {
                        Vector3 point = entity.GetPosition();
                        if (point.x >= xMin - 4 && point.x <= xMin + width + 4 && point.z >= zMin - 4 && point.z <= zMin + length + 4)
                            entityBlockers.Add(new{entityId=entity.entityId,position=point,entityType=entity.GetType().FullName,
                                blockName="<entity>",reason="preserved-entity-with-4m-margin"});
                    }
                    var ground = new int[width, length];
                    int min = 255, max = 0;
                    for (int x = 0; x < width; x++) for (int z = 0; z < length; z++)
                    {
                        float height = world.GetTerrainHeight(xMin + x, zMin + z);
                        if (float.IsNaN(height) || float.IsInfinity(height)) throw new Exception("Invalid terrain height.");
                        int y = Mathf.FloorToInt(height);
                        ground[x,z] = y; min = Math.Min(min, y); max = Math.Max(max, y);
                    }
                    if (floorY == 0) floorY = max + 1;
                    if (min < 5 || floorY < max || floorY - min > 12 || floorY > 245)
                    {
                        if(inspect)
                        {
                            Directory.CreateDirectory(CamperDiagnosticsMod.ExportFolder);
                            string refusal=Path.Combine(CamperDiagnosticsMod.ExportFolder,"pad_preflight_refused_"+Guid.NewGuid().ToString("N")+".json");
                            File.WriteAllText(refusal,JsonConvert.SerializeObject(new{
                                kind="CAMPER_PAD_READ_ONLY_PREFLIGHT",status="FAIL_NO_WORLD_CHANGES",xMin,zMin,width,length,
                                floorBlockY=floorY,originalTerrainMin=min,originalTerrainMax=max,
                                blockers=new[]{new{blockName="<unsafe-terrain>",reason="terrain-height-or-foundation-depth"}},
                                blockerNames=new Dictionary<string,int>{{"<unsafe-terrain>",1}},
                                proposedChanges=0,mutationLimitExceeded=false},Formatting.Indented));
                            SdtdConsole.Instance.Output("[CamperTrack] REFUSED_CAPTURE "+refusal);
                            return;
                        }
                        throw new Exception("Pad must sit above terrain with at most12m foundation depth.");
                    }
                    // The material selector has no collision shape. Resolve the actual
                    // cube variant used by native BlockUpdates.csv, not its selector.
                    BlockValue concrete = Block.GetBlockValue("concreteShapes:cube", false);
                    if (concrete.isair || concrete.Block == null || !concrete.Block.IsCollideMovement)
                        throw new Exception("Native solid concrete shape required.");
                    var changes = new List<BlockChangeInfo>();
                    var blockers = new List<object>(entityBlockers);
                    var blockerNames = new SortedDictionary<string,int>(StringComparer.Ordinal);
                    if(entityBlockers.Count!=0)blockerNames["<entity>"]=entityBlockers.Count;
                    var decorator = GameManager.Instance.GetDynamicPrefabDecorator();
                    if (decorator == null || decorator.allPrefabs == null) throw new Exception("Authoritative prefab list unavailable; no terrain changes applied.");
                    var prefabs = new List<PrefabInstance>();
                    decorator.GetAllPrefabs(prefabs); // Native method snapshots allPrefabs under its list lock.
                    foreach (var prefab in prefabs)
                    {
                        if (prefab == null || prefab.prefab == null) throw new Exception("Invalid authoritative prefab entry; no terrain changes applied.");
                        if (!FootprintsIntersect(xMin, zMin, width, length, prefab.boundingBoxPosition, prefab.boundingBoxSize)) continue;
                        string name = "<prefab>" + (prefab.prefab.PrefabName ?? prefab.name ?? "<unnamed>");
                        blockers.Add(new { position = prefab.boundingBoxPosition, size = prefab.boundingBoxSize, blockName = name, reason = "prefab-footprint-with-4m-margin" });
                        blockerNames.TryGetValue(name, out var count); blockerNames[name] = count + 1;
                    }
                    // Preserve every native TE, including empty/unopened loot.
                    // Its contents or decoration flag do not prove ownership.
                    // Inspect the horizontal safety halo as well as the pad.
                    var preservedTileEntities=new List<object>();
                    for(int x=xMin-4;x<xMin+width+4;x++)for(int z=zMin-4;z<zMin+length+4;z++)
                    {
                        float height=world.GetTerrainHeight(x,z);
                        if(float.IsNaN(height)||float.IsInfinity(height))throw new Exception("Invalid safety-margin terrain height; no changes applied.");
                        int low=Mathf.FloorToInt(height);
                        if(low<5||floorY-low>12||low>floorY+4)
                        {blockers.Add(new{position=new Vector3i(x,low,z),blockName="<unsafe-margin-terrain>",reason="invalid-tile-entity-margin-depth"});continue;}
                        for(int y=Math.Max(0,low);y<=floorY+4;y++)
                        {
                            var position=new Vector3i(x,y,z);
                            if(world.ChunkCache.GetChunkFromWorldPos(x,y,z)==null)
                            {
                                blockers.Add(new{position,blockName="<unloaded-margin>",reason="unloaded-tile-entity-margin"});
                                continue;
                            }
                            var tile=world.GetTileEntity(position);if(tile==null)continue;
                            string name=world.GetBlock(position).Block?.GetBlockName()??"<unknown>";
                            var retained=new{position,blockName=name,tileEntityType=tile.GetType().FullName,
                                reason="preserved-tile-entity-with-4m-margin",ownership="not-inferred",contents="not-modified"};
                            preservedTileEntities.Add(retained);blockers.Add(retained);
                            blockerNames.TryGetValue(name,out var count);blockerNames[name]=count+1;
                        }
                    }
                    // Preflight the complete volume before touching any world block.
                    for (int x = 0; x < width; x++) for (int z = 0; z < length; z++)
                        for (int y = ground[x,z]; y <= floorY + 4; y++)
                        {
                            var position = new Vector3i(xMin + x, y, zMin + z);
                            if (world.ChunkCache.GetChunkFromWorldPos(position.x, position.y, position.z) == null)
                            {
                                blockers.Add(new { position, blockName = "<unloaded>", reason = "unloaded" });
                                blockerNames.TryGetValue("<unloaded>", out var unloadedCount); blockerNames["<unloaded>"] = unloadedCount + 1;
                                continue;
                            }
                            BlockValue old = world.GetBlock(position);
                            string name = old.Block == null ? "" : old.Block.GetBlockName();
                            bool hasTileEntity = world.GetTileEntity(position) != null;
                            bool ember = IsVerifiedEmber(name, old.Block != null && old.Block.IsTerrainDecoration, hasTileEntity);
                            bool clutter=IsVerifiedFieldClutter(name,old.Block!=null&&old.Block.IsTerrainDecoration,hasTileEntity);
                            // Halo preflight already recorded every TE exactly once.
                            if(hasTileEntity)continue;
                            if (old.isWater || hasTileEntity || (!old.isair && !name.StartsWith("terr", StringComparison.Ordinal)
                                && !name.StartsWith("tree", StringComparison.Ordinal) && !ember&&!clutter))
                            {
                                blockers.Add(new { position, blockName = name, reason = hasTileEntity ? "tile-entity" : old.isWater ? "water" : "non-natural" });
                                blockerNames.TryGetValue(name, out var count); blockerNames[name] = count + 1;
                                continue;
                            }
                            BlockValue next = y <= floorY ? concrete : BlockValue.Air;
                            if (old.type != next.type) changes.Add(new BlockChangeInfo(new BlockValueRef(position), next));
                        }
                    if (inspect || blockers.Count != 0 || changes.Count > 50000)
                    {
                        Directory.CreateDirectory(CamperDiagnosticsMod.ExportFolder);
                        string refusal = Path.Combine(CamperDiagnosticsMod.ExportFolder, "pad_preflight_refused_" + Guid.NewGuid().ToString("N") + ".json");
                        File.WriteAllText(refusal, JsonConvert.SerializeObject(new {
                            kind = inspect?"CAMPER_PAD_READ_ONLY_PREFLIGHT":"CAMPER_PAD_PREFLIGHT_REFUSED",
                            status = blockers.Count==0&&changes.Count<=50000?"READY_NO_WORLD_CHANGES":"FAIL_NO_WORLD_CHANGES", xMin, zMin, width, length,
                            floorBlockY = floorY,originalTerrainMin=min,originalTerrainMax=max,
                            authoritativePrefabCount=prefabs.Count,blockers, blockerNames,preservedTileEntities,preservedEntities=entityBlockers, proposedChanges = changes.Count,
                            mutationLimitExceeded = changes.Count > 50000 }, Formatting.Indented));
                        SdtdConsole.Instance.Output("[CamperTrack] REFUSED_CAPTURE " + refusal);
                        if(inspect)return;
                        throw new Exception("Pad preflight refused " + blockers.Count + " bounded blockers; exact native names/counts "
                            + JsonConvert.SerializeObject(blockerNames) + "; no terrain changes applied. Evidence: " + refusal);
                    }
                    // A failed/uncertain newer mutation cannot retain authority
                    // to repair a previous pad. Register only after receipt write.
                    PreparedCamperPad.Current = null;
                    world.SetBlocksRPC(changes);
                    preparedPad = new PreparedCamperPad(world, xMin, zMin, floorY, width, length);
                    evidence = new { kind = "FLAT_NATIVE_CONCRETE_TEST_PAD", xMin, zMin, width, length, floorBlockY = floorY,
                        blockName = "concreteShapes:cube",
                        surfaceY = floorY + 1, originalTerrainMin = min, originalTerrainMax = max,
                        changedBlocks = changes.Count, colliderReadyAcceptance = false, drivingAcceptance = false };
                }
                else throw new Exception(getHelp());
                Directory.CreateDirectory(CamperDiagnosticsMod.ExportFolder);
                string file = Path.Combine(CamperDiagnosticsMod.ExportFolder, "track_fixture_" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(file, JsonConvert.SerializeObject(evidence, Formatting.Indented));
                if (preparedPad != null) PreparedCamperPad.Current = preparedPad;
                SdtdConsole.Instance.Output("[CamperTrack] PREPARED " + file);
            }
            catch (Exception error) { SdtdConsole.Instance.Output("*** ERROR [CamperTrack] " + error.Message); }
        }
    }
}
