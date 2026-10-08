using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public sealed class RebirthNpcAuthoringValidationResult
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();

    public int CheckedClassCount { get; internal set; }
    public int ErrorCount => errors.Count;
    public int WarningCount => warnings.Count;
    public bool Succeeded => errors.Count == 0;

    internal void Error(string message) { errors.Add(message); }
    internal void Warning(string message) { warnings.Add(message); }

    public string ToReport()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("[REBIRTH NPC] authoring validation classes=")
            .Append(CheckedClassCount)
            .Append(" errors=").Append(ErrorCount)
            .Append(" warnings=").Append(WarningCount);

        for (int i = 0; i < errors.Count; i++)
            builder.AppendLine().Append("  ERROR: ").Append(errors[i]);
        for (int i = 0; i < warnings.Count; i++)
            builder.AppendLine().Append("  WARN: ").Append(warnings[i]);
        return builder.ToString();
    }
}

/// <summary>
/// Validates the loaded entity-class authoring contract without mutating
/// content. This runs once after XML loading and is also exposed through the
/// consolidated rbnpc diagnostic command.
/// </summary>
public static class RebirthNpcAuthoringValidator
{
    private const string RebirthEntityClassPrefix = "EntityRebirth";

    public static RebirthNpcAuthoringValidationResult ValidateLoadedEntityClasses()
    {
        RebirthNpcProfileRegistry.EnsureInitialized();
        RebirthNpcAuthoringValidationResult result = new RebirthNpcAuthoringValidationResult();

        if (EntityClass.list == null)
        {
            result.Error("EntityClass.list is unavailable.");
            return result;
        }

        // EntityClass.list is hash-keyed in 3.1. Iterating integer indices from
        // 0..Count-1 silently returns null for almost every entry and produced the
        // misleading classes=0 startup warning. Walk the authoritative dictionary.
        foreach (KeyValuePair<int, EntityClass> pair in EntityClass.list.Dict)
        {
            EntityClass definition = pair.Value;
            if (definition == null || definition.Properties == null || definition.Properties.Values == null)
                continue;

            IDictionary<string, string> values = definition.Properties.Values;
            string classValue = Get(values, "Class");
            if (!IsRebirthNpcClass(classValue))
                continue;

            result.CheckedClassCount++;
            string displayName = string.IsNullOrEmpty(definition.entityClassName)
                ? "entityClassId=" + pair.Key
                : definition.entityClassName;

            ValidateProfile(displayName, values, result);
            ValidateHumanoidModel(displayName, classValue, values, result);
        }

        if (result.CheckedClassCount == 0)
            result.Warning("No loaded entity class uses an EntityRebirth* implementation.");
        return result;
    }

    private static void ValidateProfile(
        string displayName,
        IDictionary<string, string> values,
        RebirthNpcAuthoringValidationResult result)
    {
        string profileId = Get(values, "RebirthProfile").Trim();
        if (profileId.Length == 0)
        {
            result.Warning(displayName + " relies on its C# default profile; add RebirthProfile for explicit authoring.");
            return;
        }

        RebirthNpcProfile profile;
        if (!RebirthNpcProfileRegistry.TryResolve(profileId, out profile))
            result.Error(displayName + " references unknown RebirthProfile='" + profileId + "'.");
    }

    private static void ValidateHumanoidModel(
        string displayName,
        string classValue,
        IDictionary<string, string> values,
        RebirthNpcAuthoringValidationResult result)
    {
        if (classValue.IndexOf("Humanoid", StringComparison.OrdinalIgnoreCase) < 0 &&
            classValue.IndexOf("Survivor", StringComparison.OrdinalIgnoreCase) < 0 &&
            classValue.IndexOf("Bandit", StringComparison.OrdinalIgnoreCase) < 0)
            return;

        string pipelineText = Get(values, "ModelPipeline");
        RebirthHumanNpcModelPipeline pipeline;
        if (!RebirthHumanNpcModelPipelineResolver.TryParsePipeline(pipelineText, out pipeline))
        {
            result.Error(displayName + " has invalid ModelPipeline='" + pipelineText + "'.");
            return;
        }

        string modelType = Get(values, "ModelType");
        string controller = Get(values, "AvatarController");
        if (pipeline == RebirthHumanNpcModelPipeline.SDCS)
        {
            if (modelType.IndexOf("EModelRebirthSdcsNpc", StringComparison.OrdinalIgnoreCase) < 0)
                result.Error(displayName + " selects SDCS but ModelType is not EModelRebirthSdcsNpc.");
            if (controller.IndexOf("AvatarSdcsNpcController", StringComparison.OrdinalIgnoreCase) < 0)
                result.Error(displayName + " selects SDCS but AvatarController is not AvatarSdcsNpcController.");
        }
        else if (pipeline == RebirthHumanNpcModelPipeline.Custom &&
                 modelType.IndexOf("EModelRebirthSdcsNpc", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            result.Error(displayName + " selects Custom but uses the REBIRTH SDCS model type.");
        }

        string fallback = Get(values, "ModelPipelineFallback");
        if (pipeline == RebirthHumanNpcModelPipeline.SDCS &&
            string.Equals(fallback.Trim(), "Custom", StringComparison.OrdinalIgnoreCase) &&
            modelType.IndexOf("EModelRebirthSdcsNpc", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            result.Warning(displayName + " requests Custom fallback, but XML still fixes ModelType to SDCS; fallback cannot replace the CLR model after spawn.");
        }
    }

    private static bool IsRebirthNpcClass(string classValue)
    {
        if (string.IsNullOrWhiteSpace(classValue)) return false;
        string typeName = classValue.Split(',')[0].Trim();
        return typeName.StartsWith(RebirthEntityClassPrefix, StringComparison.Ordinal);
    }

    private static string Get(IDictionary<string, string> values, string key)
    {
        string value;
        return values.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
    }
}
