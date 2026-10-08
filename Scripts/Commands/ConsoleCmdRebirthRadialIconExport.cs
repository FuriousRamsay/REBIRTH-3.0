using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Client-side reference exporter for the radial-menu artwork audit.
///
/// The catalog is intentionally action/context based rather than just icon-name based.
/// This prevents unrelated actions which share a stock sprite from being treated as the
/// same design. The command exports the ORIGINAL runtime atlas crop for every confirmed
/// source-backed action, one unique copy per source sprite, a TSV manifest, and optionally
/// every ui_game_symbol_* sprite from the stock UIAtlas as a safety-net audit.
///
/// Commands:
///   rbradialicons export
///   rbradialicons exportall
///   rbradialicons list
/// </summary>
[Preserve]
public sealed class ConsoleCmdRebirthRadialIconExport : ConsoleCmdAbstract
{
    private sealed class Entry
    {
        public readonly string Group;
        public readonly string ActionKey;
        public readonly string Label;
        public readonly string Atlas;
        public readonly string Sprite;
        public readonly string Scope;
        public readonly string Notes;

        public Entry(string group, string actionKey, string label, string atlas, string sprite, string scope, string notes)
        {
            Group = group;
            ActionKey = actionKey;
            Label = label;
            Atlas = atlas;
            Sprite = sprite;
            Scope = scope;
            Notes = notes;
        }
    }

    private static readonly Entry[] Catalog = new Entry[]
    {
        new Entry("TAB", "crafting", "Crafting", "UIAtlas", "ui_game_symbol_hammer", "normal", ""),
        new Entry("TAB", "character", "Character", "UIAtlas", "ui_game_symbol_character", "normal", ""),
        new Entry("TAB", "map", "Map", "UIAtlas", "ui_game_symbol_map", "normal", "Conditional when map is enabled."),
        new Entry("TAB", "skills", "Skills", "UIAtlas", "ui_game_symbol_skills", "normal", ""),
        new Entry("TAB", "quests", "Quests", "UIAtlas", "ui_game_symbol_quest", "normal", ""),
        new Entry("TAB", "challenges", "Challenges", "UIAtlas", "ui_game_symbol_challenge", "normal", "Conditional when challenges are available."),
        new Entry("TAB", "players", "Players", "UIAtlas", "ui_game_symbol_players", "normal", ""),
        new Entry("TAB", "creative", "Creative", "UIAtlas", "ui_game_symbol_lightbulb", "conditional", "Only when Creative is available."),
        new Entry("TAB", "junk_drone", "Junk Drone", "UIAtlas", "ui_game_symbol_drone", "debug", "Debug-only TAB entry."),
        new Entry("TAB", "chat", "Chat", "UIAtlas", "ui_game_symbol_chat", "normal", ""),
        new Entry("Vehicle", "drive", "Drive", "UIAtlas", "ui_game_symbol_drive", "normal", ""),
        new Entry("Vehicle", "ride", "Ride", "UIAtlas", "ui_game_symbol_drive", "normal", "Shares the same stock sprite as Drive."),
        new Entry("Vehicle", "service", "Open", "UIAtlas", "ui_game_symbol_service", "normal", "Vehicle Open."),
        new Entry("Vehicle", "repair", "Repair", "UIAtlas", "ui_game_symbol_wrench", "normal", ""),
        new Entry("Vehicle", "lock", "Lock", "UIAtlas", "ui_game_symbol_lock", "normal", ""),
        new Entry("Vehicle", "unlock", "Unlock", "UIAtlas", "ui_game_symbol_unlock", "normal", ""),
        new Entry("Vehicle", "storage", "Storage", "UIAtlas", "ui_game_symbol_loot_sack", "normal", ""),
        new Entry("Vehicle", "keypad", "Enter Code", "UIAtlas", "ui_game_symbol_keypad", "normal", ""),
        new Entry("Vehicle", "refuel", "Refuel", "UIAtlas", "ui_game_symbol_gas", "normal", ""),
        new Entry("Vehicle", "take", "Take", "UIAtlas", "ui_game_symbol_hand", "normal", ""),
        new Entry("Vehicle", "horn", "Honk Horn", "UIAtlas", "ui_game_symbol_horn", "normal", ""),
        new Entry("Drone", "talk", "Talk", "UIAtlas", "ui_game_symbol_talk", "normal", ""),
        new Entry("Drone", "service", "Open", "UIAtlas", "ui_game_symbol_service", "normal", "Drone Open; same stock service sprite as Vehicle Open."),
        new Entry("Drone", "repair", "Repair", "UIAtlas", "ui_game_symbol_wrench", "normal", ""),
        new Entry("Drone", "lock", "Lock", "UIAtlas", "ui_game_symbol_lock", "normal", ""),
        new Entry("Drone", "unlock", "Unlock", "UIAtlas", "ui_game_symbol_unlock", "normal", ""),
        new Entry("Drone", "storage", "Storage", "UIAtlas", "ui_game_symbol_loot_sack", "normal", ""),
        new Entry("Drone", "keypad", "Enter Code", "UIAtlas", "ui_game_symbol_keypad", "normal", ""),
        new Entry("Drone", "take", "Take", "UIAtlas", "ui_game_symbol_hand", "normal", ""),
        new Entry("Drone", "drone_command_stay", "Stay", "UIAtlas", "ui_game_symbol_run_and_gun", "normal", "Distinct from Take."),
        new Entry("Drone", "drone_command_follow", "Follow", "UIAtlas", "ui_game_symbol_run", "normal", ""),
        new Entry("Drone", "drone_dont_heal_allies", "Heal Only Me", "UIAtlas", "ui_game_symbol_player", "normal", "Toggle ally-healing policy."),
        new Entry("Drone", "drone_heal_allies", "Heal My Allies", "UIAtlas", "ui_game_symbol_allies", "normal", "Toggle ally-healing policy."),
        new Entry("Drone", "drone_light_on", "Light On", "UIAtlas", "ui_game_symbol_lightbulb", "normal", ""),
        new Entry("Drone", "drone_light_off", "Light Off", "UIAtlas", "ui_game_symbol_electric_switch", "normal", ""),
        new Entry("Drone", "drone_silent_on", "Quiet Mode On", "UIAtlas", "ui_game_symbol_stealth", "normal", "Design requirement: finished Rebirth icon should carry the red slash/line."),
        new Entry("Drone", "drone_silent_off", "Quiet Mode Off", "UIAtlas", "ui_game_symbol_sight", "normal", "Design requirement: finished Rebirth icon should have no red slash/line."),
        new Entry("Drone", "drone_command_heal", "Heal Me", "UIAtlas", "ui_game_symbol_cardio", "normal", "Immediate heal command. There is NO quiet-self action."),
        new Entry("Container", "search", "Search", "UIAtlas", "ui_game_symbol_search", "normal", ""),
        new Entry("Container", "lock", "Lock", "UIAtlas", "ui_game_symbol_lock", "normal", ""),
        new Entry("Container", "unlock", "Unlock", "UIAtlas", "ui_game_symbol_unlock", "normal", ""),
        new Entry("Container", "keypad", "Enter Code", "UIAtlas", "ui_game_symbol_keypad", "normal", "There is no separate container-permissions command."),
        new Entry("Container", "take", "Take", "UIAtlas", "ui_game_symbol_hand", "normal", ""),
        new Entry("Entity", "grab", "Grab", "UIAtlas", "ui_game_symbol_hand", "normal", "EntityAlive grab action."),
        new Entry("SupplyCrate", "search", "Search", "UIAtlas", "ui_game_symbol_search", "normal", ""),
        new Entry("Door", "open", "Open", "UIAtlas", "ui_game_symbol_door", "normal", "Finished Rebirth art: arrow should point outward."),
        new Entry("Door", "close", "Close", "UIAtlas", "ui_game_symbol_door", "normal", "Finished Rebirth art: arrow should point inward."),
        new Entry("Workstation", "open", "Open", "UIAtlas", "ui_game_symbol_campfire", "normal", ""),
        new Entry("Workstation", "extract", "Extract", "UIAtlas", "ui_game_symbol_store_all_up", "normal", ""),
        new Entry("Workstation", "lock", "Lock", "UIAtlas", "ui_game_symbol_lock", "rebirth", "Rebirth workstation security uses the stock lock semantic."),
        new Entry("Workstation", "unlock", "Unlock", "UIAtlas", "ui_game_symbol_unlock", "rebirth", ""),
        new Entry("Workstation", "keypad", "Enter Code", "UIAtlas", "ui_game_symbol_keypad", "rebirth", ""),
        new Entry("PowerSource", "open", "Open", "UIAtlas", "ui_game_symbol_hand", "normal", ""),
        new Entry("PoweredDevice", "activate", "Activate", "UIAtlas", "ui_game_symbol_electric_switch", "normal", ""),
        new Entry("PoweredDevice", "light", "Light / Switch", "UIAtlas", "ui_game_symbol_electric_switch", "normal", ""),
        new Entry("PoweredDevice", "options", "Options", "UIAtlas", "ui_game_symbol_tool", "normal", ""),
        new Entry("Spotlight", "aim", "Aim", "UIAtlas", "ui_game_symbol_map_cursor", "normal", ""),
        new Entry("Spotlight", "take", "Take", "UIAtlas", "ui_game_symbol_hand", "normal", ""),
        new Entry("Sign", "edit", "Edit", "UIAtlas", "ui_game_symbol_pen", "normal", ""),
        new Entry("Sign", "report", "Report", "UIAtlas", "ui_game_symbol_report", "normal", ""),
        new Entry("Vending", "trade", "Trade", "UIAtlas", "ui_game_symbol_vending", "normal", ""),
        new Entry("Vending", "restock", "Restock", "UIAtlas", "ui_game_symbol_coin", "conditional", "Restock availability depends on context."),
        new Entry("Trader", "talk", "Talk", "UIAtlas", "ui_game_symbol_talk", "normal", ""),
        new Entry("Trader", "trade", "Trade", "UIAtlas", "ui_game_symbol_map_trader", "normal", ""),
        new Entry("Trigger", "trigger", "Edit Trigger", "UIAtlas", "ui_game_symbol_wrench", "normal", "Used by trigger-capable blocks."),
        new Entry("Lockable", "pick", "Pick Lock", "UIAtlas", "ui_game_symbol_unlock", "normal", "Lock-pick action uses the stock unlock sprite."),
        new Entry("LandClaim", "show_bounds", "Show Bounds", "UIAtlas", "ui_game_symbol_frames", "normal", "Exact source action. Not claim-view."),
        new Entry("LandClaim", "hide_bounds", "Hide Bounds", "UIAtlas", "ui_game_symbol_frames", "normal", "Exact source action. Not claim-manage."),
        new Entry("LandClaim", "remove", "Remove", "UIAtlas", "ui_game_symbol_x", "normal", ""),
        new Entry("Sleeper", "open", "Open", "UIAtlas", "ui_game_symbol_dummy", "editor", "Only source-backed sleeper radial command. There is NO sleeper-toggle action."),
        new Entry("SpawnEntity", "edit", "Edit / Options", "UIAtlas", "ui_game_symbol_tool", "editor", "Editor/special block interaction."),
        new Entry("BlockShape", "shape", "Shape", "UIAtlas", "ui_game_symbol_all_blocks", "normal", ""),
        new Entry("BlockShape", "copy_shape", "Copy Shape", "UIAtlas", "ui_game_symbol_copy_shape", "normal", ""),
        new Entry("BlockShape", "copy_shape_rotation", "Copy Shape + Rotation", "UIAtlas", "ui_game_symbol_copy_shape_and_rotation", "normal", ""),
        new Entry("BlockShape", "rotate_simple", "Simple Rotation", "UIAtlas", "ui_game_symbol_rotate_simple", "normal", ""),
        new Entry("BlockShape", "rotate_advanced", "Advanced Rotation", "UIAtlas", "ui_game_symbol_rotate_advanced", "normal", ""),
        new Entry("BlockShape", "rotate_on_face", "On-Face Rotation", "UIAtlas", "ui_game_symbol_rotate_on_face", "normal", ""),
        new Entry("BlockShape", "rotate_auto", "Auto Rotation", "UIAtlas", "ui_game_symbol_rotate_auto", "normal", ""),
        new Entry("BlockShape", "copy_rotation", "Copy Rotation", "UIAtlas", "ui_game_symbol_paint_copy_block", "normal", ""),
        new Entry("BlockShape", "materials", "Materials", "UIAtlas", "ui_game_symbol_paint_bucket", "normal", ""),
        new Entry("BlockShape", "placement_voxel", "Voxel Placement", "UIAtlas", "ui_game_symbol_placement_voxel", "normal", ""),
        new Entry("BlockShape", "placement_free", "Free Placement", "UIAtlas", "ui_game_symbol_placement_free", "normal", ""),
        new Entry("QuickActions", "camera", "Camera Change", "UIAtlas", "ui_game_symbol_camera", "normal", ""),
        new Entry("QuickActions", "drop_item", "Drop Item", "UIAtlas", "ui_game_symbol_drop_item", "normal", ""),
        new Entry("QuickActions", "no_activatable_items", "No Activatable Items", "UIAtlas", "ui_game_symbol_x", "normal", ""),
        new Entry("Paint", "materials", "Materials", "UIAtlas", "ui_game_symbol_paint_bucket", "normal", ""),
        new Entry("Paint", "brush", "Paint Brush", "UIAtlas", "ui_game_symbol_paint_brush", "normal", ""),
        new Entry("Paint", "roller", "Paint Roller", "UIAtlas", "ui_game_symbol_paint_roller", "normal", ""),
        new Entry("Paint", "fill", "Paint Fill", "UIAtlas", "ui_game_symbol_flood_fill", "normal", ""),
        new Entry("Paint", "spray", "Spray Gun", "UIAtlas", "ui_game_symbol_paint_spraygun", "normal", ""),
        new Entry("Paint", "all_sides", "Paint All Sides", "UIAtlas", "ui_game_symbol_paint_allsides", "normal", ""),
        new Entry("Paint", "eyedropper", "Texture Picker", "UIAtlas", "ui_game_symbol_paint_eyedropper", "normal", ""),
        new Entry("Paint", "copy_block", "Copy Block", "UIAtlas", "ui_game_symbol_paint_copy_block", "normal", ""),
        new Entry("Paint", "replace_paint", "Replace Paint", "UIAtlas", "ui_game_symbol_book", "normal", ""),
        new Entry("ReplaceBlock", "single", "Replace Block Single", "UIAtlas", "ui_game_symbol_paint_brush", "editor", ""),
        new Entry("ReplaceBlock", "multiple", "Replace Block Multiple", "UIAtlas", "ui_game_symbol_paint_spraygun", "editor", ""),
        new Entry("ReplaceBlock", "keep_paint", "Keep Paint", "UIAtlas", "ui_game_symbol_brick", "editor", ""),
        new Entry("ReplaceBlock", "remove_paint", "Remove Paint", "UIAtlas", "ui_game_symbol_destruction", "editor", ""),
        new Entry("ReplaceBlock", "use_new_paint", "Use New Paint", "UIAtlas", "ui_game_symbol_paint_copy_block", "editor", ""),
        new Entry("ReplaceBlock", "replace_air", "Replace With Air", "UIAtlas", "ui_game_symbol_x", "editor", ""),
        new Entry("Rebirth", "restore_vehicle", "Restore Vehicle", "UIAtlas", "ui_game_symbol_wrench", "rebirth", "Rebirth restoration radial action."),
        new Entry("Rebirth", "pickup_block", "Take", "UIAtlas", "ui_game_symbol_hand", "rebirth", "Rebirth block pickup."),
        new Entry("Rebirth", "pickup_cancel", "Cancel", "UIAtlas", "ui_game_symbol_x", "rebirth", "Rebirth block-pickup cancel."),
        new Entry("Rebirth", "rename_container", "Rename", "UIAtlas", "ui_game_symbol_pen", "rebirth", "Rebirth named-container edit command."),
        new Entry("Rebirth", "quickstack", "Quick Stack", "RebirthUiIcons", "ui_game_symbol_quickstack", "rebirth", "Exact supplied legacy Quick Stack artwork."),
        new Entry("Rebirth", "drone_lock_to_player", "Lock Drone to Player", "RebirthUiIcons", "ui_game_symbol_dronelocktoplayer", "rebirth", "Exact supplied legacy Drone Lock-to-Player artwork."),
        new Entry("Rebirth", "quickstack_categories", "Quick Stack Categories", "RebirthUiIcons", "ui_game_symbol_quickstack_categories", "rebirth", "Accepted checklist icon with black pixels transparent."),
        new Entry("Rebirth", "remote_resource_toggle", "Enable / Disable Remote Resource Use", "UIAtlas", "ui_game_symbol_resource", "rebirth", "Uses the stock base-game resource symbol."),
        new Entry("TestEditor", "spawn_zombie", "Spawn Zombie", "UIAtlas", "ui_game_symbol_zombie", "editor", "Base XML creative/test radial command."),
        new Entry("TestEditor", "spawn_vulture", "Spawn Vulture", "UIAtlas", "ui_game_symbol_hand", "editor", "Base XML creative/test radial command."),
    };

    public override bool IsExecuteOnClient => true;
    public override bool AllowedInMainMenu => false;
    public override int DefaultPermissionLevel => 1000;

    public override string[] getCommands()
    {
        return new[] { "rbradialicons", "rbradialexport" };
    }

    public override string getDescription()
    {
        return "Exports the exact runtime source sprites used by the authoritative Rebirth/3.1 radial-menu audit.";
    }

    public override string getHelp()
    {
        return "rbradialicons export     - export confirmed radial source sprites + manifest + zip\n" +
               "rbradialicons exportall  - same export plus every ui_game_symbol_* sprite in the stock UIAtlas\n" +
               "rbradialicons list       - print the corrected source-backed radial action catalog";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        string sub = _params != null && _params.Count > 0 ? _params[0].Trim().ToLowerInvariant() : "help";
        if (sub == "help" || sub == "?")
        {
            Log.Out(getHelp());
            return;
        }

        if (sub == "list")
        {
            LogCatalog();
            return;
        }

        if (sub != "export" && sub != "exportall")
        {
            Log.Out("[RebirthRadialIcons] Unknown subcommand: " + sub);
            Log.Out(getHelp());
            return;
        }

        try
        {
            Export(sub == "exportall");
        }
        catch (Exception ex)
        {
            Log.Error("[RebirthRadialIcons] Export failed: " + ex);
        }
    }

    private static void LogCatalog()
    {
        Log.Out("[RebirthRadialIcons] Corrected source-backed catalog entries: " + Catalog.Length);
        for (int i = 0; i < Catalog.Length; i++)
        {
            Entry e = Catalog[i];
            Log.Out("[RebirthRadialIcons] " + e.Group + " / " + e.Label +
                    " action=" + e.ActionKey + " atlas=" + e.Atlas + " sprite=" + e.Sprite +
                    " scope=" + e.Scope);
        }
    }

    private static void Export(bool includeAllStockSymbols)
    {
        if (GameManager.Instance == null || GameManager.Instance.World == null)
        {
            Log.Error("[RebirthRadialIcons] No active world. Run the command after loading into a game.");
            return;
        }

        EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
        if (player == null || player.PlayerUI == null || player.PlayerUI.xui == null)
        {
            Log.Error("[RebirthRadialIcons] No local player XUi. Run this from the game client after the HUD has loaded.");
            return;
        }

        XUi xui = player.PlayerUI.xui;
        string root = Path.Combine(GameIO.GetUserGameDataDir(), "RebirthRadialIconExport");
        Directory.CreateDirectory(root);

        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string folder = Path.Combine(root, "RadialIcons-" + stamp);
        string byActionDir = Path.Combine(folder, "ByAction");
        string uniqueDir = Path.Combine(folder, "UniqueSprites");
        string atlasDir = Path.Combine(folder, "SourceAtlases");
        Directory.CreateDirectory(byActionDir);
        Directory.CreateDirectory(uniqueDir);
        Directory.CreateDirectory(atlasDir);

        var manifest = new StringBuilder(32768);
        manifest.AppendLine("group\taction_key\tlabel\tscope\tatlas\tsprite\tstatus\twidth\theight\tsource_texture\tnotes");

        var uniqueWritten = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceAtlasWritten = new HashSet<int>();
        int actionWritten = 0;
        int failed = 0;

        for (int i = 0; i < Catalog.Length; i++)
        {
            Entry e = Catalog[i];
            INGUIAtlas atlas = ResolveAtlas(xui, e.Atlas, e.Sprite);
            UISpriteData data;
            Texture source;
            string error;
            if (!TryResolveSprite(atlas, e.Sprite, out data, out source, out error))
            {
                failed++;
                manifest.Append(Tsv(e.Group)).Append('\t').Append(Tsv(e.ActionKey)).Append('\t').Append(Tsv(e.Label)).Append('\t')
                    .Append(Tsv(e.Scope)).Append('\t').Append(Tsv(e.Atlas)).Append('\t').Append(Tsv(e.Sprite)).Append('\t')
                    .Append("FAILED\t0\t0\t\t").Append(Tsv(AppendNote(e.Notes, error))).AppendLine();
                continue;
            }

            string actionFolder = Path.Combine(byActionDir, SafeName(e.Group));
            Directory.CreateDirectory(actionFolder);
            string actionName = SafeName(e.Label) + "__" + SafeName(e.ActionKey) + "__" + SafeName(e.Sprite) + ".png";
            string actionPath = Path.Combine(actionFolder, actionName);

            Texture2D crop = null;
            try
            {
                crop = ReadSprite(source, data);
                File.WriteAllBytes(actionPath, crop.EncodeToPNG());
                actionWritten++;

                string uniqueKey = e.Atlas + "|" + e.Sprite;
                if (uniqueWritten.Add(uniqueKey))
                {
                    string uniqueName = SafeName(e.Atlas) + "__" + SafeName(e.Sprite) + ".png";
                    File.WriteAllBytes(Path.Combine(uniqueDir, uniqueName), crop.EncodeToPNG());
                }
            }
            finally
            {
                if (crop != null) UnityEngine.Object.Destroy(crop);
            }

            int textureId = source.GetInstanceID();
            if (sourceAtlasWritten.Add(textureId))
            {
                Texture2D whole = null;
                try
                {
                    whole = ReadWholeTexture(source);
                    string atlasName = SafeName(source.name) + "__" + source.width + "x" + source.height + "__" + textureId + ".png";
                    File.WriteAllBytes(Path.Combine(atlasDir, atlasName), whole.EncodeToPNG());
                }
                finally
                {
                    if (whole != null) UnityEngine.Object.Destroy(whole);
                }
            }

            manifest.Append(Tsv(e.Group)).Append('\t').Append(Tsv(e.ActionKey)).Append('\t').Append(Tsv(e.Label)).Append('\t')
                .Append(Tsv(e.Scope)).Append('\t').Append(Tsv(e.Atlas)).Append('\t').Append(Tsv(e.Sprite)).Append('\t')
                .Append("OK\t").Append(data.width).Append('\t').Append(data.height).Append('\t')
                .Append(Tsv(source.name)).Append('\t').Append(Tsv(e.Notes)).AppendLine();
        }

        if (includeAllStockSymbols)
            ExportAllStockSymbols(xui, folder, manifest, uniqueWritten);

        string manifestPath = Path.Combine(folder, "RADIAL_ICON_MANIFEST.tsv");
        File.WriteAllText(manifestPath, manifest.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(folder, "README.txt"), BuildReadme(includeAllStockSymbols, actionWritten, failed), new UTF8Encoding(false));

        string zipPath = Path.Combine(root, "RadialIcons-" + stamp + ".zip");
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(folder, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);

        Log.Out("[RebirthRadialIcons] Export complete. actionPNGs=" + actionWritten + " failed=" + failed +
                " includeAllStockSymbols=" + includeAllStockSymbols);
        Log.Out("[RebirthRadialIcons] ZIP: " + zipPath);
        Log.Out("[RebirthRadialIcons] Folder: " + folder);
    }

    private static INGUIAtlas ResolveAtlas(XUi xui, string atlasName, string spriteName)
    {
        if (xui == null) return null;
        INGUIAtlas atlas = xui.GetAtlasByName(atlasName, spriteName);
        if (atlas != null) return atlas;

        // Existing Rebirth individual PNGs are loaded in the RebirthUiIcons atlas by the
        // game's mod UI-atlas loader. If a Rebirth-specific reference is not yet registered,
        // do not silently substitute a vanilla sprite; the manifest should show FAILED.
        return null;
    }

    private static bool TryResolveSprite(INGUIAtlas atlas, string spriteName, out UISpriteData data, out Texture source, out string error)
    {
        data = null;
        source = null;
        error = null;
        if (atlas == null)
        {
            error = "Atlas was not available at runtime.";
            return false;
        }

        int guard = 0;
        while (atlas.replacement != null && guard++ < 8)
            atlas = atlas.replacement;

        List<UISpriteData> list = atlas.spriteList;
        if (list == null)
        {
            error = "Atlas spriteList is null.";
            return false;
        }

        for (int i = 0; i < list.Count; i++)
        {
            UISpriteData candidate = list[i];
            if (candidate != null && string.Equals(candidate.name, spriteName, StringComparison.OrdinalIgnoreCase))
            {
                data = candidate;
                break;
            }
        }
        if (data == null)
        {
            error = "Sprite not present in resolved atlas.";
            return false;
        }

        Material material = GetSpriteMaterial(atlas);
        if (material == null || material.mainTexture == null)
        {
            error = "Resolved atlas has no readable source material/mainTexture reference.";
            return false;
        }
        source = material.mainTexture;
        return true;
    }

    private static Material GetSpriteMaterial(INGUIAtlas atlas)
    {
        if (atlas == null) return null;
        Type t = atlas.GetType();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        PropertyInfo p = t.GetProperty("spriteMaterial", flags);
        if (p != null && typeof(Material).IsAssignableFrom(p.PropertyType))
            return p.GetValue(atlas, null) as Material;

        FieldInfo f = t.GetField("spriteMaterial", flags);
        if (f != null && typeof(Material).IsAssignableFrom(f.FieldType))
            return f.GetValue(atlas) as Material;

        // Some NGUI versions expose the material as a capitalized property.
        p = t.GetProperty("SpriteMaterial", flags);
        if (p != null && typeof(Material).IsAssignableFrom(p.PropertyType))
            return p.GetValue(atlas, null) as Material;

        return null;
    }

    private static Texture2D ReadSprite(Texture source, UISpriteData data)
    {
        int width = Math.Max(1, data.width);
        int height = Math.Max(1, data.height);
        int sourceY = source.height - data.y - height; // UISpriteData.y is top-origin.
        if (sourceY < 0) sourceY = 0;

        Vector2 scale = new Vector2(width / (float)source.width, height / (float)source.height);
        Vector2 offset = new Vector2(data.x / (float)source.width, sourceY / (float)source.height);
        return ReadViaBlit(source, width, height, scale, offset);
    }

    private static Texture2D ReadWholeTexture(Texture source)
    {
        return ReadViaBlit(source, source.width, source.height, Vector2.one, Vector2.zero);
    }

    private static Texture2D ReadViaBlit(Texture source, int width, int height, Vector2 scale, Vector2 offset)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        Texture2D output = null;
        try
        {
            Graphics.Blit(source, rt, scale, offset);
            RenderTexture.active = rt;
            output = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            output.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            output.Apply(false, false);
            return output;
        }
        catch
        {
            if (output != null) UnityEngine.Object.Destroy(output);
            throw;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private static void ExportAllStockSymbols(XUi xui, string folder, StringBuilder manifest, HashSet<string> uniqueWritten)
    {
        const string probe = "ui_game_symbol_hammer";
        INGUIAtlas atlas = xui.GetAtlasByName("UIAtlas", probe);
        if (atlas == null)
        {
            manifest.AppendLine("SAFETY_NET\texportall\tAll ui_game_symbol_*\taudit\tUIAtlas\t*\tFAILED\t0\t0\t\tCould not resolve stock UIAtlas probe.");
            return;
        }
        int guard = 0;
        while (atlas.replacement != null && guard++ < 8) atlas = atlas.replacement;
        Material material = GetSpriteMaterial(atlas);
        Texture source = material != null ? material.mainTexture : null;
        if (source == null) return;

        string dir = Path.Combine(folder, "AllUiGameSymbols");
        Directory.CreateDirectory(dir);
        List<UISpriteData> list = atlas.spriteList;
        if (list == null) return;

        int count = 0;
        for (int i = 0; i < list.Count; i++)
        {
            UISpriteData data = list[i];
            if (data == null || string.IsNullOrEmpty(data.name) || !data.name.StartsWith("ui_game_symbol_", StringComparison.OrdinalIgnoreCase))
                continue;

            Texture2D crop = null;
            try
            {
                crop = ReadSprite(source, data);
                File.WriteAllBytes(Path.Combine(dir, SafeName(data.name) + ".png"), crop.EncodeToPNG());
                count++;
                string uniqueKey = "UIAtlas|" + data.name;
                if (uniqueWritten.Add(uniqueKey))
                    File.WriteAllBytes(Path.Combine(folder, "UniqueSprites", "UIAtlas__" + SafeName(data.name) + ".png"), crop.EncodeToPNG());
            }
            finally
            {
                if (crop != null) UnityEngine.Object.Destroy(crop);
            }
        }
        manifest.Append("SAFETY_NET\texportall\tAll ui_game_symbol_*\taudit\tUIAtlas\t*\tOK\t0\t0\t")
            .Append(Tsv(source.name)).Append('\t').Append("Exported ").Append(count).Append(" stock UI symbol sprites.").AppendLine();
    }

    private static string BuildReadme(bool all, int written, int failed)
    {
        return "REBIRTH 3.1 RADIAL ICON SOURCE EXPORT\r\n" +
               "=====================================\r\n\r\n" +
               "This package contains ORIGINAL runtime atlas crops. It is a visual-reference export, not generated artwork.\r\n\r\n" +
               "ByAction/       One PNG per confirmed radial action/context.\r\n" +
               "UniqueSprites/  One PNG per unique source atlas+sprite.\r\n" +
               "SourceAtlases/  Full source atlas textures used, for crop verification.\r\n" +
               (all ? "AllUiGameSymbols/ Every ui_game_symbol_* sprite from the stock atlas safety-net.\r\n" : "") +
               "RADIAL_ICON_MANIFEST.tsv maps each action to its exact source sprite.\r\n\r\n" +
               "Corrective exclusions: there is no quiet-self action, container-permissions action, sleeper-toggle action, or claim-view/claim-manage action in the authoritative source.\r\n" +
               "Land claim actions are Show Bounds, Hide Bounds, Remove. Sleeper has Open only.\r\n\r\n" +
               "Action PNGs written: " + written + "\r\n" +
               "Failed source resolutions: " + failed + "\r\n";
    }

    private static string SafeName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "unnamed";
        char[] invalid = Path.GetInvalidFileNameChars();
        var b = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool bad = false;
            for (int j = 0; j < invalid.Length; j++) if (c == invalid[j]) { bad = true; break; }
            b.Append(bad || c == ' ' ? '_' : c);
        }
        return b.ToString();
    }

    private static string Tsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
    }

    private static string AppendNote(string first, string second)
    {
        if (string.IsNullOrEmpty(first)) return second ?? string.Empty;
        if (string.IsNullOrEmpty(second)) return first;
        return first + " " + second;
    }
}
