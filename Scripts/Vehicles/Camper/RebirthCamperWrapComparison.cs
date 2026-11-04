using System;

// Owner-requested, creative-only size/quality comparison copies. These do not
// promote new artwork into the approved production collection.
internal static class RebirthCamperWrapComparison
{
    internal static bool Contains(string id) =>
        id == "NavezganeExplorerCompare100" || id == "NavezganeExplorerCompare75" ||
        id == "NavezganeExplorerCompare50" || id == "NavezganeExplorerCompare35";

    internal static string FromItem(string name)
    {
        const string prefix = "modVehicleWrap";
        if (name == null || !name.StartsWith(prefix, StringComparison.Ordinal)) return null;
        string id = name.Substring(prefix.Length);
        return Contains(id) ? id : null;
    }
}
