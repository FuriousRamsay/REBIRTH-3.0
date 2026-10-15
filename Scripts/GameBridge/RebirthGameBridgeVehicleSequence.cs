using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using InControl;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Console dispatch uses the existing authenticated Bridge /console endpoint.
// Each phase feeds real native input and records actual physics at 10 Hz.
public sealed class ConsoleCmdRebirthVehicleSequence : ConsoleCmdAbstract
{
    private static bool running;
    private static string activeRunId;
    private static float supervisorLeaseUntil;
    public override string[] getCommands() => new[] { "rbvehiclesequence" };
    public override string getDescription() => "Run a bounded native vehicle input sequence in CodexTest.";
    public override string getHelp() => "rbvehiclesequence ENTITY [xMin xMax zMin zMax]; heartbeat RUN_ID; cancel RUN_ID. Supervisor must renew the exact run lease every <10 seconds.";
    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        int id;
        var world = GameManager.Instance.World;
        var player = world == null ? null : world.GetPrimaryPlayer() as EntityPlayerLocal;
        var vehicle = player == null ? null : player.AttachedToEntity as EntityVehicle;
        if (args.Count == 2 && (args[0] == "heartbeat" || args[0] == "cancel"))
        {
            if (!running || args[1] != activeRunId || player == null || world.IsRemote()
                || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest" || !RebirthGameBridge.IsRunning)
            { SdtdConsole.Instance.Output("*** ERROR vehicle sequence control does not identify the active local test run."); return; }
            supervisorLeaseUntil = args[0] == "cancel" ? 0f : Time.realtimeSinceStartup + 10f;
            SdtdConsole.Instance.Output("[VehicleSequence] " + args[0].ToUpperInvariant() + " " + activeRunId);
            return;
        }
        RebirthGameBridgeVehicleBounds bounds;
        if (running || !RebirthGameBridgeVehicleBounds.TryParse(args,out bounds) || !int.TryParse(args[0], out id) || vehicle == null
            || vehicle.entityId != id || !vehicle.hasDriver || !ReferenceEquals(vehicle.GetAttached(0), player) || vehicle.vehicleRB == null
            || world.IsRemote() || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest"
            || !RebirthGameBridge.IsRunning || AutomationEnabled() || HasZombies(player)
            || (bounds!=null&&!bounds.Contains(vehicle.position.x,vehicle.position.z)))
        { SdtdConsole.Instance.Output("*** ERROR vehicle sequence requires idle Bridge, CodexTest driver and all automation off."); return; }
        running = true;
        activeRunId = Guid.NewGuid().ToString("N");
        supervisorLeaseUntil = Time.realtimeSinceStartup + 10f;
        try { GameManager.Instance.StartCoroutine(Run(player, vehicle,bounds)); }
        catch { running = false; throw; }
    }

    private static bool AutomationEnabled() => RebirthGameBridgeCombat.GuardEnabled
        || RebirthGameBridgeCombat.InstinctBusy
        || RebirthGameBridgeCombat.BleedReflexEnabled
        || RebirthGameBridgeCombat.InstinctHeal || RebirthGameBridgeCombat.InstinctReload
        || RebirthGameBridgeCombat.InstinctSupply || RebirthGameBridgeCombat.GlanceWhenIdle
        || RebirthGameBridgeNeeds.Enabled || RebirthGameBridgeAgenda.Enabled
        || RebirthGameBridgeWorld.ExploreEnabled;

    private static bool HasZombies(EntityPlayerLocal player)
    {
        foreach (Entity entity in player.world.Entities.list)
            if (entity is EntityZombie && !entity.IsDead() && ((EntityAlive)entity).Health > 0 && Vector3.Distance(entity.position, player.position) < 80f) return true;
        return false;
    }

    private static string RelativePath(Transform target, Transform root)
    {
        string path = target.name;
        while (target.parent != null && target.parent != root)
        { target = target.parent; path = target.name + "/" + path; }
        return path;
    }

    private static bool CaptureFrame(string path, out string failure)
    {
        Texture2D frame = null;
        failure = null;
        try
        {
            if (Screen.width <= 0 || Screen.height <= 0) throw new InvalidOperationException("No rendered game frame.");
            frame = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            frame.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            frame.Apply();
            File.WriteAllBytes(path, frame.EncodeToPNG());
            if (new FileInfo(path).Length == 0) throw new IOException("Screenshot is empty.");
            return true;
        }
        catch (Exception error) { failure = error.ToString(); return false; }
        finally { if (frame != null) UnityEngine.Object.Destroy(frame); }
    }

    private static IEnumerator Run(EntityPlayerLocal player, EntityVehicle vehicle,RebirthGameBridgeVehicleBounds bounds)
    {
        var samples = new JArray();
        var screenshots = new JArray();
        string runId = activeRunId;
        string folder = Path.Combine(Path.GetDirectoryName(typeof(ConsoleCmdRebirthVehicleSequence).Assembly.Location), "Tools/GameBridge/out/vehicle_sequences", runId);
        string status = "FAIL_INTERRUPTED";
        Vector3 origin = vehicle.position;
        float quality = vehicle.vehicle.GetVehicleQuality();
        int startingHealth = vehicle.Health;
        int startingPlayerHealth = player.Health;
        string[] names = { "vehicle.MoveForward", "vehicle.MoveRight", "vehicle.MoveLeft", "vehicle.Brake", "vehicle.MoveBack",
            "vehicle.Brake", "vehicle.ToggleFlashlight", "vehicle.ToggleFlashlight", "vehicle.HonkHorn" };
        float[] durations = { 3f, 1.5f, 1.5f, 2f, 2f, 2f, .1f, .1f, .3f };
        var actions = new PlayerAction[names.Length];
        var wheels = vehicle.GetComponentsInChildren<WheelCollider>();
        var lights = vehicle.GetComponentsInChildren<Light>(true);
        string failure = null;
        try
        {
            Directory.CreateDirectory(folder);
            SdtdConsole.Instance.Output("[VehicleSequence] STARTED " + Path.Combine(folder,"result.json"));
            for (int i = 0; i < names.Length; i++)
            {
                actions[i] = RebirthGameBridgeInput.Find(names[i]);
                if (actions[i] == null) { status = "FAIL_NATIVE_ACTION_MISSING"; yield break; }
            }
            using (var scope = new RebirthGameBridgeInput.OwnedInputScope(player, () =>
                ReferenceEquals(player.AttachedToEntity, vehicle) && ReferenceEquals(vehicle.GetAttached(0), player)
                && activeRunId == runId && Time.realtimeSinceStartup < supervisorLeaseUntil
                && !AutomationEnabled() && !HasZombies(player)
                && GamePrefs.GetString(EnumGamePrefs.GameName) == "CodexTest"
                && vehicle.vehicle.GetVehicleQuality() == quality
                && vehicle.Health >= startingHealth && player.Health >= startingPlayerHealth
                && vehicle.vehicleRB.velocity.magnitude <= 16f
                && Vector3.Distance(origin, vehicle.position) <= 65f
                && (bounds==null||bounds.Contains(vehicle.position.x,vehicle.position.z))))
            {
                for (int phase = 0; phase < actions.Length; phase++)
                {
                    float end = Time.realtimeSinceStartup + durations[phase], nextSample = 0;
                    while (Time.realtimeSinceStartup < end)
                    {
                        if (!scope.TryHoldFrames(actions[phase], 1)) { failure = scope.Failure; yield break; }
                        if ((phase == 1 || phase == 2) && !scope.TryHoldFrames(actions[0], 1)) { failure = scope.Failure; yield break; }
                        if (Time.realtimeSinceStartup >= nextSample)
                        {
                            Vector3 pos = vehicle.position, velocity = vehicle.vehicleRB.velocity;
                            Quaternion rotation = vehicle.vehicleRB.rotation;
                            Vector3 angularVelocity = vehicle.vehicleRB.angularVelocity;
                            Vector3 localVelocity = Quaternion.Inverse(rotation) * velocity;
                            var wheelSamples = new JArray();
                            var lightSamples = new JArray();
                            foreach (Light light in lights)
                                lightSamples.Add(new JObject { ["name"] = light.name, ["path"] = RelativePath(light.transform, vehicle.transform), ["enabled"] = light.enabled,
                                    ["active"] = light.gameObject.activeInHierarchy, ["intensity"] = light.intensity,
                                    ["color"] = new JArray(light.color.r,light.color.g,light.color.b) });
                            foreach (WheelCollider wheel in wheels)
                            {
                                WheelHit hit;
                                bool grounded = wheel.GetGroundHit(out hit);
                                wheelSamples.Add(new JObject { ["name"] = wheel.name, ["grounded"] = grounded,
                                    ["rpm"] = wheel.rpm, ["steerAngle"] = wheel.steerAngle,
                                    ["motorTorque"] = wheel.motorTorque, ["brakeTorque"] = wheel.brakeTorque,
                                    ["forwardSlip"] = grounded ? hit.forwardSlip : 0,
                                    ["sidewaysSlip"] = grounded ? hit.sidewaysSlip : 0 });
                            }
                            samples.Add(new JObject { ["phase"] = names[phase], ["phaseIndex"] = phase, ["time"] = Time.realtimeSinceStartup,
                                ["position"] = new JArray(pos.x,pos.y,pos.z), ["velocity"] = new JArray(velocity.x,velocity.y,velocity.z),
                                ["rotation"] = new JArray(rotation.x,rotation.y,rotation.z,rotation.w),
                                ["angularVelocity"] = new JArray(angularVelocity.x,angularVelocity.y,angularVelocity.z),
                                ["localVelocity"] = new JArray(localVelocity.x,localVelocity.y,localVelocity.z),
                                ["tiltDegrees"] = Vector3.Angle(rotation * Vector3.up,Vector3.up),
                                ["quality"] = vehicle.vehicle.GetVehicleQuality(), ["steering"] = vehicle.movementInput.moveStrafe,
                                ["health"] = vehicle.Health, ["playerHealth"] = player.Health,
                                ["braking"] = vehicle.vehicle.CurrentIsBreak, ["forwardVelocity"] = vehicle.vehicle.CurrentForwardVelocity,
                                ["wheels"] = wheelSamples, ["lights"] = lightSamples });
                            nextSample = Time.realtimeSinceStartup + .1f;
                        }
                        yield return null;
                    }
                    // Capture the exercised state before releasing steering or
                    // braking. A post-release image can hide the exact defect.
                    if (!scope.TryHoldFrames(actions[phase], 3)) { failure = scope.Failure; yield break; }
                    if ((phase == 1 || phase == 2) && !scope.TryHoldFrames(actions[0], 3)) { failure = scope.Failure; yield break; }
                    string screenshot = Path.Combine(folder, phase + "_" + names[phase].Replace('.', '_') + ".png");
                    yield return new WaitForEndOfFrame();
                    if (!scope.Admitted) { failure = scope.Failure; yield break; }
                    if (!CaptureFrame(screenshot, out failure))
                    { status = "FAIL_SCREENSHOT"; yield break; }
                    screenshots.Add(screenshot);
                    scope.Release(actions[phase]);
                    if (phase == 1 || phase == 2) scope.Release(actions[0]);
                    float settle = Time.realtimeSinceStartup + .4f;
                    while (Time.realtimeSinceStartup < settle)
                    {
                        if (!scope.Admitted) { failure = scope.Failure; yield break; }
                        yield return null;
                    }
                }
                status = "CAPTURED_NATIVE_INPUT_SEQUENCE_NOT_ACCEPTANCE";
                failure = scope.Failure;
            }
        }
        finally
        {
            running = false;
            activeRunId = null;
            string file = Path.Combine(folder, "result.json");
            File.WriteAllText(file, JsonConvert.SerializeObject(new { schema = 3, status, failure, entity = vehicle.entityId,
                vehicleName = vehicle.vehicle.GetName(), mass = vehicle.vehicleRB.mass, samples, screenshots,
                startingHealth, finalHealth = vehicle.Health, startingPlayerHealth, finalPlayerHealth = player.Health,
                safeBounds = bounds==null?null:new { xMin=bounds.XMin,xMax=bounds.XMax,zMin=bounds.ZMin,zMax=bounds.ZMax },
                visualAcceptance = false, audibleHornAcceptance = false }, Formatting.Indented));
            SdtdConsole.Instance.Output("[VehicleSequence] " + status + " " + file);
        }
    }
}

// Explicit local qualification observation. No install/remove/debit/save mutation.
public sealed class ConsoleCmdRebirthCamperState : ConsoleCmdAbstract
{
    public override string[] getCommands() => new[] { "rbcamperstate" };
    public override string getDescription() => "Capture actual camper cosmetics, modules, cargo and native station state in CodexTest.";
    public override string getHelp() => "rbcamperstate ENTITY: nearby local authorized camper only; observation is not persistence acceptance.";
    private static JObject Item(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty()) return null;
        return new JObject { ["name"] = stack.itemValue.ItemClass.GetItemName(), ["count"] = stack.count };
    }
    private static JArray Slots(ItemStack[] stacks)
    {
        var result = new JArray();
        for (int i = 0; stacks != null && i < stacks.Length; i++)
            result.Add(new JObject { ["slot"] = i, ["item"] = Item(stacks[i]) });
        return result;
    }
    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        try
        {
            int id;
            var world = GameManager.Instance.World;
            var player = world == null ? null : world.GetPrimaryPlayer() as EntityPlayerLocal;
            if (args.Count != 1 || !int.TryParse(args[0], out id) || player == null || world.IsRemote()
                || GameManager.IsDedicatedServer || !RebirthGameBridge.IsRunning
                || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest") throw new InvalidOperationException(getHelp());
            var vehicle = world.GetEntity(id) as EntityVehicle;
            if (vehicle?.vehicle == null || vehicle.vehicle.GetName() != "rebirthCamper80s"
                || (vehicle.position-player.position).sqrMagnitude > 64f || !vehicle.AllowActivationCommand("service",player))
                throw new InvalidOperationException("Nearby permitted camper required.");
            var runtime = RebirthCamperModuleRuntime.For(vehicle);
            var storage = new JArray(); var stations = new JArray();
            for (int slot = 0; slot < RebirthCamperCraftingStation.ModuleSlots; slot++)
            {
                var container = runtime.Storage.GetSlot(slot);
                int capacity = 0;
                if (container != null && !RebirthCamperStorageContainer.TryGetCapacity(container,out capacity))
                    throw new InvalidOperationException("Installed container no longer resolves.");
                storage.Add(new JObject { ["slot"] = slot, ["item"] = Item(container), ["capacity"] = capacity });
                var installation = runtime.Stations.GetSlot(slot);
                if (installation == null) { stations.Add(new JObject { ["slot"] = slot }); continue; }
                var state = installation.State; var queue = new JArray();
                for (int index = 0; state.Queue != null && index < state.Queue.Length; index++)
                {
                    var job = state.Queue[index]; if (job?.Recipe == null) continue;
                    bool cancelReady = RebirthCamperStationQueuedAuthority.TryGetCurrent(state, job, out var authority)
                        && RebirthCamperStationCancellationCheckpoint.TryCreate(authority, out _);
                    queue.Add(new JObject { ["slot"] = index, ["recipe"] = job.Recipe.GetName(),
                        ["batches"] = job.Multiplier, ["secondsLeft"] = job.CraftingTimeLeft,
                        ["crafting"] = job.IsCrafting, ["cancelReady"] = cancelReady });
                }
                stations.Add(new JObject { ["slot"] = slot, ["identity"] = installation.Id.ToString("N"),
                    ["item"] = Item(installation.InstalledItem), ["burning"] = state.IsBurning,
                    ["fuelSeconds"] = state.BurnTotalTimeLeft,
                    ["tools"] = Slots(state.Tools), ["fuel"] = Slots(state.Fuel), ["input"] = Slots(state.Input),
                    ["output"] = Slots(state.Output), ["queue"] = queue });
            }
            var updated = vehicle.vehicle.GetUpdatedItemValue(); var dye = updated.GetCosmeticMod(0);
            string metadata; updated.TryGetMetadata("RebirthCamperModules",out metadata);
            var report = new JObject { ["schema"] = 1, ["entity"] = id, ["vehicleIdentity"] = runtime.Identity.ToString("N"),
                ["dye"] = dye == null || dye.IsEmpty() ? null : dye.ItemClass.GetItemName(),
                ["storageCapacity"] = runtime.Storage.Capacity, ["cargo"] = Slots(vehicle.bag.ItemGrid.items),
                ["storage"] = storage, ["stations"] = stations, ["transferPending"] = runtime.TransferPending,
                ["persistedModulesPresent"] = !string.IsNullOrEmpty(metadata), ["acceptance"] = false };
            string folder = Path.Combine(Path.GetDirectoryName(typeof(ConsoleCmdRebirthCamperState).Assembly.Location),"Tools/GameBridge/out/camper_state");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder,Guid.NewGuid().ToString("N")+".json");
            File.WriteAllText(file,report.ToString(Formatting.Indented));
            SdtdConsole.Instance.Output("[CamperState] CAPTURED " + file);
        }
        catch (Exception error) { SdtdConsole.Instance.Output("*** ERROR [CamperState] " + error); }
    }
}
