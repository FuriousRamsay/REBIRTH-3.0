using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

#nullable disable

/// <summary>
/// Explicit sequential schema migration registry for user-local Survivor Profiles.
/// There is no implicit "best effort" migration: an unknown old/future schema is rejected.
/// </summary>
public static class RebirthSurvivorProfileMigrationRegistry
{
    private static readonly Dictionary<int, Func<XDocument, string>> Steps =
        new Dictionary<int, Func<XDocument, string>>();

    public static void Register(int fromVersion, Func<XDocument, string> step)
    {
        if (fromVersion < 1 || step == null)
            throw new ArgumentException("Profile migration registration is invalid.");
        if (Steps.ContainsKey(fromVersion))
            throw new InvalidOperationException("Profile migration already registered from schema " + fromVersion.ToString(CultureInfo.InvariantCulture));
        Steps[fromVersion] = step;
    }

    public static bool TryMigrateToCurrent(XDocument document, out bool changed, out string error)
    {
        changed = false;
        error = string.Empty;
        if (document == null || document.Root == null || document.Root.Name != "rebirthSurvivorProfile")
        {
            error = "unexpected/missing profile root";
            return false;
        }

        int version;
        if (!int.TryParse((string)document.Root.Attribute("schemaVersion"), NumberStyles.Integer, CultureInfo.InvariantCulture, out version))
        {
            error = "profile schemaVersion is missing or invalid";
            return false;
        }
        if (version > RebirthSurvivorProfile.CurrentSchemaVersion)
        {
            error = "profile schema " + version.ToString(CultureInfo.InvariantCulture) + " is newer than supported schema " + RebirthSurvivorProfile.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture);
            return false;
        }

        while (version < RebirthSurvivorProfile.CurrentSchemaVersion)
        {
            Func<XDocument, string> step;
            if (!Steps.TryGetValue(version, out step) || step == null)
            {
                error = "no explicit profile migration registered from schema " + version.ToString(CultureInfo.InvariantCulture);
                return false;
            }
            string stepError = step(document);
            if (!string.IsNullOrEmpty(stepError))
            {
                error = "profile migration from schema " + version.ToString(CultureInfo.InvariantCulture) + " failed: " + stepError;
                return false;
            }
            int next;
            if (!int.TryParse((string)document.Root.Attribute("schemaVersion"), NumberStyles.Integer, CultureInfo.InvariantCulture, out next) || next <= version)
            {
                error = "profile migration from schema " + version.ToString(CultureInfo.InvariantCulture) + " did not advance schemaVersion";
                return false;
            }
            version = next;
            changed = true;
        }
        return true;
    }
}
