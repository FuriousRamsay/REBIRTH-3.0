using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;

[Preserve]
public sealed class ConsoleCmdRebirthTextureAudit : ConsoleCmdAbstract
{
    private const int PageSize = 30;
    public override bool IsExecuteOnClient => true;
    public override int DefaultPermissionLevel => 1000;
    public override bool AllowedInMainMenu => true;
    public override string[] getCommands() => new[] { "rta", "rebirthtexaudit" };
    public override string getDescription() => "Read-only resident texture and loaded asset-bundle inspection.";
    public override string getHelp() => "rta textures [nameFilter=*] [page=1]\nrta bundles [nameFilter=*] [page=1]\nrta assets <exactLoadedBundleName> [nameFilter=*] [page=1]\nCSV output, 30 rows/page. Textures are resident objects on this process; bundle assets are exact manifest paths, not a guessed texture-to-bundle mapping. No assets are loaded, unloaded or changed.";

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        try
        {
            if (!ThreadManager.IsMainThread()) { SdtdConsole.Instance.Output("rta: main thread required."); return; }
            if (args == null || args.Count == 0) { SdtdConsole.Instance.Output(getHelp()); return; }
            string mode = args[0].ToLowerInvariant();
            bool assets = mode == "assets";
            if (mode != "textures" && mode != "bundles" && !assets || args.Count > (assets ? 4 : 3) || assets && args.Count < 2)
            { SdtdConsole.Instance.Output(getHelp()); return; }
            int filterIndex = assets ? 2 : 1, pageIndex = assets ? 3 : 2, page = 1;
            if (args.Count > pageIndex && !int.TryParse(args[pageIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out page)
                || page < 1 || page > 10000) { SdtdConsole.Instance.Output(getHelp()); return; }
            string filter = args.Count > filterIndex && args[filterIndex] != "*" ? args[filterIndex] : string.Empty;
            var rows = new List<string>();
            string header;
            if (mode == "textures")
            {
                var textures = new List<KeyValuePair<long, string>>();
                header = "instanceId,name,width,height,format,mipCount,readable,unityRuntimeBytes,streaming,loadedMip,desiredMip";
                SdtdConsole.Instance.Output(string.Format(CultureInfo.InvariantCulture,
                    "rta memory: current={0}, desired={1}, target={2}, nonStreaming={3} bytes (Unity reported)",
                    Texture.currentTextureMemory, Texture.desiredTextureMemory, Texture.targetTextureMemory, Texture.nonStreamingTextureMemory));
                // Explicit one-shot residency snapshot; never a periodic scan or cleanup.
                foreach (var texture in Resources.FindObjectsOfTypeAll<Texture2D>())
                {
                    if (texture == null || !Matches(texture.name, filter)) continue;
                    long bytes = Profiler.GetRuntimeMemorySizeLong(texture);
                    textures.Add(new KeyValuePair<long, string>(bytes, string.Join(",", texture.GetInstanceID().ToString(CultureInfo.InvariantCulture), Csv(texture.name),
                        texture.width.ToString(CultureInfo.InvariantCulture), texture.height.ToString(CultureInfo.InvariantCulture), texture.format.ToString(),
                        texture.mipmapCount.ToString(CultureInfo.InvariantCulture), texture.isReadable ? "true" : "false",
                        bytes.ToString(CultureInfo.InvariantCulture), texture.streamingMipmaps ? "true" : "false",
                        texture.loadedMipmapLevel.ToString(CultureInfo.InvariantCulture), texture.desiredMipmapLevel.ToString(CultureInfo.InvariantCulture))));
                }
                textures.Sort((a, b) => a.Key == b.Key ? StringComparer.Ordinal.Compare(a.Value, b.Value) : b.Key.CompareTo(a.Key));
                rows.AddRange(textures.Select(texture => texture.Value));
            }
            else
            {
                var bundles = AssetBundle.GetAllLoadedAssetBundles().Where(bundle => bundle != null).ToArray();
                header = assets ? "bundle,assetPath" : "bundle";
                if (assets)
                {
                    var matching = bundles.Where(bundle => string.Equals(bundle.name, args[1], StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (matching.Length != 1) { SdtdConsole.Instance.Output("rta: exact loaded bundle name must identify one bundle. Use rta bundles."); return; }
                    foreach (var path in matching[0].GetAllAssetNames())
                        if (Matches(path, filter)) rows.Add(Csv(matching[0].name) + "," + Csv(path));
                }
                else foreach (var bundle in bundles) if (Matches(bundle.name, filter)) rows.Add(Csv(bundle.name));
            }
            if (mode != "textures") rows.Sort(StringComparer.Ordinal);
            int pages = Math.Max(1, (rows.Count + PageSize - 1) / PageSize);
            SdtdConsole.Instance.Output(string.Format(CultureInfo.InvariantCulture, "rta {0}: {1} rows; page {2}/{3}; read-only local process snapshot", mode, rows.Count, page, pages));
            if (page > pages) { SdtdConsole.Instance.Output("rta: page is outside the result set."); return; }
            SdtdConsole.Instance.Output(header);
            for (int i = (page - 1) * PageSize; i < rows.Count && i < page * PageSize; i++) SdtdConsole.Instance.Output(rows[i]);
        }
        catch (Exception error) { SdtdConsole.Instance.Output("rta: " + error.Message); }
    }
    private static bool Matches(string value, string filter) => filter.Length == 0 || (value ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    private static string Csv(string value) => "\"" + (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Replace("\"", "\"\"") + "\"";
}
