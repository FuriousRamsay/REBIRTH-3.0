using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Focused dog deployment diagnostics. This is intentionally independent of the
/// broad NPC diagnostics switch so a placement reproduction can be captured
/// without enabling noisy NPC AI/runtime logging.
/// </summary>
public static class RebirthDogDeploymentDebug
{
    private static string lastGateMessage = string.Empty;
    private static float lastGateTime = -999f;

    public static bool Enabled { get; set; }

    public static void Trace(string message)
    {
        if (!Enabled) return;
        Log.Out("[REBIRTH DogDeploy] " + (message ?? string.Empty));
    }

    public static void TraceServer(bool forced, string message)
    {
        if (!Enabled && !forced) return;
        Log.Out("[REBIRTH DogDeploy] SERVER " + (message ?? string.Empty));
    }

    public static void TraceGate(string message)
    {
        if (!Enabled) return;
        float now = Time.realtimeSinceStartup;
        string safe = message ?? string.Empty;
        if (string.Equals(lastGateMessage, safe, StringComparison.Ordinal) && now - lastGateTime < 1f)
            return;
        lastGateMessage = safe;
        lastGateTime = now;
        Log.Out("[REBIRTH DogDeploy] " + safe);
    }

    public static string GetStatus()
    {
        RebirthDogDefinitions.EnsureInitialized();
        StringBuilder b = new StringBuilder();
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayer player = world != null ? world.GetPrimaryPlayer() as EntityPlayer : null;
        PersistentPlayerData persistent = player != null && GameManager.Instance != null
            ? GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)
            : null;

        b.Append("[REBIRTH DogDeploy] status trace=").Append(Enabled)
            .Append(" connectionServer=").Append(connection != null && connection.IsServer)
            .Append(" world=").Append(world != null)
            .Append(" worldRemote=").Append(world != null && world.IsRemote())
            .Append(" player=").Append(player != null ? player.entityId.ToString() : "<none>")
            .Append(" persistentId=").Append(persistent?.PrimaryId != null ? persistent.PrimaryId.ToString() : "<none>")
            .AppendLine();

        int listCount = EntityClass.list != null ? EntityClass.list.Count : -1;
        int dictCount = EntityClass.list != null && EntityClass.list.Dict != null ? EntityClass.list.Dict.Count : -1;
        b.Append("  EntityClass.list Count=").Append(listCount)
            .Append(" Dict.Count=").Append(dictCount).AppendLine();

        ItemValue held = player?.inventory?.holdingItemItemValue;
        b.Append("  heldItem=")
            .Append(held != null && !held.IsEmpty() && held.ItemClass != null ? held.ItemClass.GetItemName() : "<none>")
            .AppendLine();

        int ownedDogs, ownedTotal, globalCap;
        if (RebirthDogLifecycleService.TryGetOwnershipCounts(player, out ownedDogs, out ownedTotal, out globalCap))
        {
            string acquireReason;
            bool canAcquire = RebirthDogLifecycleService.CanAcquire(player, out acquireReason);
            b.Append("  ownership ownedDogs=").Append(ownedDogs)
                .Append("/").Append(globalCap)
                .Append(" ownedTotal=").Append(ownedTotal)
                .Append(" canAcquire=").Append(canAcquire)
                .Append(" reason='").Append(acquireReason ?? string.Empty).Append("'")
                .AppendLine();
        }
        else
        {
            b.Append("  ownership=<unavailable: no persistent owner id>").AppendLine();
        }

        string bundleOwner = FindAssetOwner("Resources/FR_Animals.unity3d");
        b.Append("  FR_Animals.unity3d=").Append(bundleOwner ?? "<missing from loaded mods>").AppendLine();

        RebirthDogBreedDefinition[] breeds = RebirthDogDefinitions.GetSnapshot();
        for (int i = 0; i < breeds.Length; i++)
        {
            RebirthDogBreedDefinition breed = breeds[i];
            EntityClass ec = null;
            bool entityResolved = EntityClass.list != null &&
                EntityClass.list.TryGetValue(EntityClass.FromString(breed.EntityClassName), out ec) && ec != null;
            string rawMesh = GetEntityProperty(ec, "Mesh");
            string normalizedMesh = RebirthDogAuthoringValidator.NormalizeModFolderUri(rawMesh);

            BlockValue bv = Block.GetBlockValue(breed.SpawnDeployId);
            Block block = !bv.isair ? bv.Block : null;
            string blockBreed = GetBlockProperty(block, "RebirthDogBreedId");
            string customIcon = GetBlockProperty(block, "CustomIcon");
            string model = GetBlockProperty(block, "Model");
            string iconOwner = FindAssetOwner("UIAtlases/ItemIconAtlas/" + breed.IconName + ".png");

            b.Append("  [").Append(breed.BreedId).Append("] entity=").Append(breed.EntityClassName)
                .Append(" resolved=").Append(entityResolved)
                .Append(" id=").Append(entityResolved ? EntityClass.FromString(breed.EntityClassName).ToString() : "<missing>")
                .Append(" clr=").Append(entityResolved && ec.classname != null ? ec.classname.FullName : "<missing>")
                .AppendLine();
            b.Append("      meshRaw='").Append(rawMesh).Append("'").AppendLine();
            b.Append("      meshNormalized='").Append(normalizedMesh)
                .Append("' expected='").Append(RebirthDogAuthoringValidator.NormalizeModFolderUri(breed.MeshUri)).Append("'")
                .AppendLine();
            b.Append("      block=").Append(breed.SpawnDeployId)
                .Append(" resolved=").Append(block != null)
                .Append(" clr=").Append(block != null ? block.GetType().FullName : "<missing>")
                .Append(" breedProperty='").Append(blockBreed).Append("'")
                .Append(" customIcon='").Append(customIcon).Append("'")
                .AppendLine();
            b.Append("      previewModel='").Append(model).Append("'").AppendLine();
            b.Append("      iconFile=").Append(iconOwner ?? "<missing from loaded mods>").AppendLine();
        }

        return b.ToString().TrimEnd();
    }

    private static string FindAssetOwner(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return null;
        try
        {
            foreach (Mod mod in ModManager.GetLoadedMods())
            {
                if (mod == null || string.IsNullOrEmpty(mod.Path)) continue;
                string path = mod.Path.TrimEnd('/', '\\') + "/" + relativePath;
                if (SdFile.Exists(path))
                    return mod.Name + " -> " + path;
            }
        }
        catch (Exception ex)
        {
            return "<asset scan failed: " + ex.GetType().Name + ">";
        }
        return null;
    }

    private static string GetEntityProperty(EntityClass definition, string key)
    {
        if (definition?.Properties?.Values == null) return string.Empty;
        string value;
        return definition.Properties.Values.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
    }

    private static string GetBlockProperty(Block block, string key)
    {
        if (block?.Properties?.Values == null) return string.Empty;
        string value;
        return block.Properties.Values.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
    }
}

[Preserve]
public sealed class ConsoleCmdRebirthDogDebug : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;

    public override string[] getCommands()
    {
        return new[] { "rbdogtrace", "rbdogdebug" };
    }

    public override string getDescription()
    {
        return "Traces REBIRTH dog deployment blocks, assets, validation and server requests.";
    }

    public override string getHelp()
    {
        return "Usage: rbdogtrace on|off|status|validate\n"
             + "  on       Enable focused dog placement tracing. Remote deploy requests carry the trace flag to the server.\n"
             + "  off      Disable tracing.\n"
             + "  status   Dump ownership/cap state plus entity classes, mesh URIs, deploy blocks, icons and FR_Animals asset presence.\n"
             + "  validate Run NPC, dog breed and dog deploy-block authoring validators.";
    }

    public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
    {
        string op = parameters == null || parameters.Count == 0
            ? "status"
            : (parameters[0] ?? string.Empty).Trim().ToLowerInvariant();

        switch (op)
        {
            case "on":
            case "true":
                RebirthDogDeploymentDebug.Enabled = true;
                Log.Out("[REBIRTH DogDeploy] trace=ON. Place one dog block, then run: rbdogtrace status");
                return;
            case "off":
            case "false":
                RebirthDogDeploymentDebug.Enabled = false;
                Log.Out("[REBIRTH DogDeploy] trace=OFF");
                return;
            case "validate":
                Log.Out(RebirthNpcAuthoringValidator.ValidateLoadedEntityClasses().ToReport());
                Log.Out(RebirthDogAuthoringValidator.ValidateLoadedEntityClasses().ToReport());
                Log.Out(RebirthDogP2AuthoringValidator.Validate());
                return;
            case "status":
                Log.Out(RebirthDogDeploymentDebug.GetStatus());
                return;
            default:
                Log.Out(getHelp());
                return;
        }
    }
}
