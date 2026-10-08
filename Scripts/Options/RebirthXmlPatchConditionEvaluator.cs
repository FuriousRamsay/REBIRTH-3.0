using HarmonyLib;
using NCalc;
using System;
using System.Globalization;

#nullable disable

/// <summary>
/// Restores the REBIRTH ability to query typed sandbox options from XML conditional patches.
/// This intentionally uses an explicit typed map instead of reflection: option renames/additions
/// must be deliberate, compile-visible changes and cannot fail silently through dynamic member lookup.
/// </summary>
public static class RebirthXmlOptionConditionResolver
{
    public static bool IsOptionAvailable(string optionName, string optionType, string expectedValue)
    {
        bool result;
        return TryEvaluateOption(optionName, optionType, expectedValue, out result) && result;
    }

    public static bool TryEvaluateOption(string optionName, string optionType, string expectedValue, out bool result)
    {
        result = false;
        RebirthSandboxState state = ResolveState();
        object actualValue;
        if (!TryGetOptionValue(state, NormalizeOptionName(optionName), out actualValue))
        {
            Log.Error("[RebirthXmlOptions] Unknown REBIRTH sandbox option requested by XML: '" + (optionName ?? string.Empty) + "'.");
            return false;
        }

        string normalizedType = (optionType ?? string.Empty).Trim().ToLowerInvariant();
        string expected = (expectedValue ?? string.Empty).Trim();
        try
        {
            if (normalizedType == "bool" || normalizedType == "boolean")
            {
                bool expectedBool;
                if (!bool.TryParse(expected, out expectedBool)) { Log.Error("[RebirthXmlOptions] Invalid boolean value '"+expected+"' for option '"+(optionName??string.Empty)+"'."); return false; }
                if (!(actualValue is bool)) { Log.Error("[RebirthXmlOptions] Type mismatch: option '"+(optionName??string.Empty)+"' is not boolean."); return false; }
                result = (bool)actualValue == expectedBool; return true;
            }
            if (normalizedType == "int" || normalizedType == "integer")
            {
                int expectedInt;
                if (!int.TryParse(expected, NumberStyles.Integer, CultureInfo.InvariantCulture, out expectedInt)) { Log.Error("[RebirthXmlOptions] Invalid integer value '"+expected+"' for option '"+(optionName??string.Empty)+"'."); return false; }
                if (actualValue is bool || actualValue == null || (!actualValue.GetType().IsEnum && !(actualValue is byte) && !(actualValue is sbyte) && !(actualValue is short) && !(actualValue is ushort) && !(actualValue is int) && !(actualValue is uint) && !(actualValue is long) && !(actualValue is ulong))) { Log.Error("[RebirthXmlOptions] Type mismatch: option '"+(optionName??string.Empty)+"' is not integer-compatible."); return false; }
                result = Convert.ToInt32(actualValue, CultureInfo.InvariantCulture) == expectedInt; return true;
            }
            if (normalizedType == "string" || normalizedType == "enum")
            {
                if (actualValue == null) return false;
                Type actualType=actualValue.GetType();
                if (normalizedType=="enum" && !actualType.IsEnum) { Log.Error("[RebirthXmlOptions] Type mismatch: option '"+(optionName??string.Empty)+"' is not an enum."); return false; }
                string actual = actualValue.ToString() ?? string.Empty;
                if (actualType.IsEnum && !Enum.IsDefined(actualType, actualValue)) { Log.Error("[RebirthXmlOptions] Invalid enum state for option '"+(optionName??string.Empty)+"'."); return false; }
                result = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase); return true;
            }
        }
        catch(Exception ex)
        {
            Log.Error("[RebirthXmlOptions] Failed typed comparison for option '"+(optionName??string.Empty)+"': "+ex.Message);
            return false;
        }
        Log.Error("[RebirthXmlOptions] Unsupported XML option type '" + normalizedType + "' for option '" + (optionName ?? string.Empty) + "'.");
        return false;
    }

    private static bool TryGetOptionValue(RebirthSandboxState s, string optionName, out object value)
    {
        value = null;
        if (s == null)
            return false;

        switch ((optionName ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "playerprogression": value = s.PlayerProgression; return true;
            case "advancedfarming": value = s.AdvancedFarming; return true;
            case "instantblockpickup": value = s.InstantBlockPickup; return true;
            case "quickstack": value = s.QuickStack; return true;
            case "quickstackdistance": value = s.QuickStackDistance; return true;
            case "requiretimedreading": value = s.RequireTimedReading; return true;
            case "literaturestudytime": value = s.LiteratureStudyTime; return true;
            case "remoteresources": value = s.RemoteResources; return true;
            case "remoteresourcesdistance": value = s.RemoteResourcesDistance; return true;
            case "guarddistance": value = s.GuardDistance; return true;
            case "fullcontroldistance": value = s.FullControlDistance; return true;
            case "huntingdistance": value = s.HuntingDistance; return true;
            case "companioncardstyle": value = s.CompanionCardStyle; return true;
            case "targetnamecolorr": value = s.TargetNameColorR; return true;
            case "targetnamecolorg": value = s.TargetNameColorG; return true;
            case "targetnamecolorb": value = s.TargetNameColorB; return true;
            case "secureaccesssharing": value = s.SecureAccessSharing; return true;
            case "hornactivateddoors": value = s.HornActivatedDoors; return true;
            case "spawnprogression": value = s.SpawnProgression; return true;
            case "autoreplanttrees": value = s.AutoReplantTrees; return true;
            case "vehicleblockrespawndays": value = s.VehicleBlockRespawnDays; return true;
            case "hybridpathsmoothing": value = s.HybridPathSmoothing; return true;
            case "zombiesdestroyareas": value = s.ZombiesDestroyAreas; return true;
            case "rancherrangedattack": value = s.RancherRangedAttack; return true;
            case "sleeperrespawns": value = s.SleeperRespawns; return true;
            case "loottraderareas": value = s.LootTraderAreas; return true;
            case "sleeperspawnmultiplier": value = s.SleeperSpawnMultiplier; return true;
            case "infestedsleeperspawnmultiplier": value = s.InfestedSleeperSpawnMultiplier; return true;
            case "suppressconsoleerrorpopups": value = s.SuppressConsoleErrorPopups; return true;
            case "treedensitymultiplier": value = s.TreeDensityMultiplier; return true;
            case "vehicledensitymultiplier": value = s.VehicleDensityMultiplier; return true;
            case "blockscatchfire": value = s.BlocksCatchFire; return true;
            case "fireaffectsheatmap": value = s.FireAffectsHeatmap; return true;
            case "fireblockdamagespeed": value = s.FireBlockDamageSpeed; return true;
            case "maxjobs": value = s.MaxJobs; return true;
            case "jobstonexttier": value = s.JobsToNextTier; return true;
            case "repeatpoijobs": value = s.RepeatPoiJobs; return true;
            case "infestedjobs": value = s.InfestedJobs; return true;
            case "traderjoblist": value = s.TraderJobList; return true;
            case "traderjobrecoverygrace": value = s.TraderJobRecoveryGrace; return true;
            case "traderjobrecoverygraceduration": value = s.TraderJobRecoveryGraceDuration; return true;
            case "preventhealingoverlap": value = s.PreventHealingOverlap; return true;
            case "alwaysstagger": value = s.AlwaysStagger; return true;
            case "weatherfogbehavior": value = s.WeatherFogBehavior; return true;
            case "weatherfogintensity": value = s.WeatherFogIntensity; return true;
            case "uniformatmosphere": value = s.UniformAtmosphere; return true;
            case "poirisk": value = s.PoiRisk; return true;
            case "poisenseschedule": value = s.PoiSenseSchedule; return true;
            case "poisenseintensity": value = s.PoiSenseIntensity; return true;
            case "pitchblack": value = s.PitchBlack; return true;
            case "bossevents":
            case "bosseventstartlevel": value = s.BossEventStartLevel; return true;
            case "bosseventfrequency": value = s.BossEventFrequency; return true;
            case "bosseventmaximumperday": value = s.BossEventMaximumPerDay; return true;
            case "bosseventsize": value = s.BossEventSize; return true;
            case "bosseventdifficulty": value = s.BossEventDifficulty; return true;
            case "bosseventtime": value = s.BossEventTime; return true;
            case "bosseventbloodmoonday": value = s.BossEventBloodMoonDay; return true;
            case "bosseventrestriction": value = s.BossEventRestriction; return true;
            case "bosseventrewards": value = s.BossEventRewards; return true;
            case "bosseventnotifications": value = s.BossEventNotifications; return true;
            case "protectcrate": value = s.ProtectCrate; return true;
            default: return false;
        }
    }

    public static bool IsCharacterProgression(string expectedValue)
    {
        RebirthSandboxState state = ResolveState();
        string normalized = (expectedValue ?? string.Empty).Trim()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty);

        RebirthPlayerProgressionMode expected;
        if (string.Equals(normalized, "BaseGame", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Base", StringComparison.OrdinalIgnoreCase))
        {
            expected = RebirthPlayerProgressionMode.BaseGame;
        }
        else if (string.Equals(normalized, "Rebirth", StringComparison.OrdinalIgnoreCase))
        {
            expected = RebirthPlayerProgressionMode.Rebirth;
        }
        else
        {
            Log.Error("[RebirthXmlOptions] Unknown Character Progression value requested by XML: '" +
                (expectedValue ?? string.Empty) + "'. Expected BaseGame or Rebirth.");
            return false;
        }

        bool result = state != null && state.PlayerProgression == expected;
        if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) Log.Out("[RebirthXmlOptions][CharacterProgression] expected=" + (expectedValue ?? string.Empty)
            + " actual=" + (state != null ? state.PlayerProgression.ToString() : "unavailable")
            + " result=" + result);
        return result;
    }

    private static string NormalizeOptionName(string optionName)
    {
        string value = (optionName ?? string.Empty).Trim();
        return value.StartsWith("Custom", StringComparison.OrdinalIgnoreCase)
            ? value.Substring("Custom".Length)
            : value;
    }

    private static RebirthSandboxState ResolveState()
    {
        RebirthSandboxState state;

        // Live-world authority first. TryDecode is non-throwing; do not hide failures behind
        // catch-all blocks that make configuration problems invisible.
        if (GameManager.Instance != null
            && GameManager.Instance.World != null
            && RebirthSandboxOptionManager.TryDecode(RebirthSandboxOptionManager.Current.CurrentCode, out state))
            return state;

        // Main-menu session before a world object exists.
        if (RebirthSandboxOptionManager.TryDecode(RebirthSandboxUiSession.Code, out state))
            return state;

        // Dedicated/config-load fallback.
        if (RebirthSandboxOptionManager.TryDecode(RebirthSandboxOptionManager.Current.CurrentCode, out state))
            return state;

        Log.Warning("[RebirthXmlOptions] No valid REBIRTH sandbox code was available; using safe default state for XML option evaluation.");
        return new RebirthSandboxState();
    }
}

[HarmonyPatch(typeof(XmlPatchConditionEvaluator), "NCalcEvaluateFunction", new Type[] { typeof(string), typeof(FunctionArgs), typeof(bool) })]
public static class RebirthXmlPatchConditionEvaluatorPatch
{
    [HarmonyPrefix]
    public static bool Prefix(string _name, FunctionArgs _args, bool _ignoreCase)
    {
        if (_name.EqualsCaseInsensitive("isoption"))
        {
            EvaluateOption(_args, false, "isoption");
            return false;
        }

        if (_name.EqualsCaseInsensitive("not_isoption"))
        {
            EvaluateOption(_args, true, "not_isoption");
            return false;
        }

        if (_name.EqualsCaseInsensitive("character_progression"))
        {
            EvaluateCharacterProgression(_args);
            return false;
        }

        return true;
    }

    private static void EvaluateCharacterProgression(FunctionArgs args)
    {
        if (args.Parameters.Length != 1)
            throw new ArgumentException(
                "Calling function character_progression with invalid number of arguments (" +
                args.Parameters.Length + ", expected 1)");

        string expectedValue = args.Parameters[0].Evaluate() as string;
        if (expectedValue == null)
            throw new ArgumentException("Calling function character_progression: Expected a string argument");

        args.Result = RebirthXmlOptionConditionResolver.IsCharacterProgression(expectedValue);
    }

    private static void EvaluateOption(FunctionArgs args, bool invert, string functionName)
    {
        if (args.Parameters.Length != 3)
            throw new ArgumentException(
                "Calling function " + functionName + " with invalid number of arguments (" +
                args.Parameters.Length + ", expected 3)");

        string optionName = args.Parameters[0].Evaluate() as string;
        string optionType = args.Parameters[1].Evaluate() as string;
        string expectedValue = args.Parameters[2].Evaluate() as string;
        if (optionName == null || optionType == null || expectedValue == null)
            throw new ArgumentException("Calling function " + functionName + ": Expected string arguments");

        bool result;
        bool valid = RebirthXmlOptionConditionResolver.TryEvaluateOption(optionName, optionType, expectedValue, out result);
        args.Result = valid && (invert ? !result : result);
    }
}

public sealed class RebirthXmlOptionConditionModApi : IModApi
{
    private static bool installed;
    private static readonly Harmony XmlOptionHarmony = new Harmony("rebirth.xml-option-conditions.3.1");

    public void InitMod(Mod modInstance)
    {
        if (installed)
            return;

        try
        {
            RebirthHarmonyBootstrap.PatchClassOnce(XmlOptionHarmony, typeof(RebirthXmlPatchConditionEvaluatorPatch));
            installed = true;
            { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[RebirthXmlOptions] Installed typed isoption/not_isoption/character_progression conditional functions (no dynamic field/method lookup)."); }
        }
        catch (Exception ex)
        {
            Log.Error("[RebirthXmlOptions] Failed to install XML option conditions: " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }
}
