using HarmonyLib;
using UnityEngine.Scripting;

// Keep the native controller type: native ResetList, navigation, selection and save
// handlers continue to work. Only the choices and popup geometry differ.
[Preserve]
public sealed class XUiC_RebirthWaypointPicker : XUiC_MapSubPopupList
{
    internal const int Rows=9, Columns=7, CellWidth=42, CellHeight=43;
    internal static readonly string[] Icons =
    {
        "ui_game_symbol_water",
        "ui_game_symbol_tree",
        "ui_game_symbol_add",
        "ui_game_symbol_workbench",
        "ui_game_symbol_allies",
        "ui_game_symbol_animal_tracker",
        "ui_game_symbol_archery",
        "ui_game_symbol_armor_iron",
        "ui_game_symbol_map_cave",
        "ui_game_symbol_assemble",
        "ui_game_symbol_backpack",
        "ui_game_symbol_book",
        "ui_game_symbol_brick",
        "ui_game_symbol_bundle",
        "ui_game_symbol_cement",
        "ui_game_symbol_character",
        "ui_game_symbol_check",
        "ui_game_symbol_drive",
        "ui_game_symbol_chemistry",
        "ui_game_symbol_coin",
        "ui_game_symbol_crops",
        "ui_game_symbol_diamond",
        "ui_game_symbol_door",
        "ui_game_symbol_electric_power",
        "ui_game_symbol_electric_turret",
        "ui_game_symbol_fetch_loot",
        "ui_game_symbol_safe",
        "ui_game_symbol_flood_fill",
        "ui_game_symbol_foliage",
        "ui_game_symbol_fork",
        "ui_game_symbol_gas",
        "ui_game_symbol_hammer",
        "ui_game_symbol_harvest",
        "ui_game_symbol_lightbulb",
        "ui_game_symbol_lock",
        "ui_game_symbol_loot_sack",
        "ui_game_symbol_map",
        "ui_game_symbol_map_bed",
        "ui_game_symbol_map_cabin",
        "ui_game_symbol_map_campsite",
        "ui_game_symbol_map_city",
        "ui_game_symbol_map_civil",
        "ui_game_symbol_map_fortress",
        "ui_game_symbol_map_house",
        "ui_game_symbol_pills",
        "ui_game_symbol_medical",
        "ui_game_symbol_mining",
        "ui_game_symbol_map_trader",
        "ui_game_symbol_player",
        "ui_game_symbol_quest",
        "ui_game_symbol_radiation",
        "ui_game_symbol_rifle",
        "ui_game_symbol_fire",
        "ui_game_symbol_airdrop",
        "ui_game_symbol_science",
        "ui_game_symbol_scrap",
        "ui_game_symbol_shopping_cart",
        "ui_game_symbol_star",
        "ui_game_symbol_table_saw",
        "ui_game_symbol_tool",
        "ui_game_symbol_treasure",
        "ui_game_symbol_wood",
        "ui_game_symbol_x",
    };
    public override void Init()
    {
        base.Init();
        for(int i=0;i<children.Count&&i<Icons.Length;i++)
        {
            var entry=children[i].GetChildByType<XUiC_MapSubPopupEntry>();
            if(entry==null)continue;
            entry.SetIndex(i);
            entry.SetSpriteName(Icons[i]);
        }
    }
    internal static Vector2i Fit(Vector2i position,Vector2i size,Vector2i screen)
    {
        const int margin=6;
        int left=-screen.x/2+margin, right=screen.x/2-size.x-margin;
        int bottom=-screen.y/2+size.y+margin, top=screen.y/2-margin;
        return new Vector2i(System.Math.Max(left,System.Math.Min(right,position.x)),
            System.Math.Max(bottom,System.Math.Min(top,position.y)));
    }
}

[HarmonyPatch(typeof(XUiC_MapPopupList),"onPressEntry2")]
internal static class RebirthWaypointPickerPosition
{
    private static void Postfix(XUiC_MapPopupList __instance,XUiController _sender)
    {
        if(!(_sender is XUiC_MapPopupEntry))return;
        var window=__instance.xui.GetWindow("mapAreaChooseWaypoint");
        if(window?.Controller.GetChildByType<XUiC_RebirthWaypointPicker>()==null)return;
        window.Position=XUiC_RebirthWaypointPicker.Fit(window.Position,window.Size,__instance.xui.GetXUiScreenSize());
    }
}

[HarmonyPatch(typeof(XUiC_MapSubPopupEntry),"onPressed")]
internal static class RebirthWaypointNamePosition
{
    private static void Postfix(XUiC_MapSubPopupEntry __instance)
    {
        var picker=__instance.xui.GetWindow("mapAreaChooseWaypoint");
        if(picker?.Controller.GetChildByType<XUiC_RebirthWaypointPicker>()==null)return;
        var window=__instance.xui.GetWindow("mapAreaEnterWaypointName");
        if(window==null)return;
        int column=__instance.index/XUiC_RebirthWaypointPicker.Rows;
        int row=__instance.index%XUiC_RebirthWaypointPicker.Rows;
        var position=picker.Position+new Vector2i(3+(column+1)*XUiC_RebirthWaypointPicker.CellWidth,
            -3-row*XUiC_RebirthWaypointPicker.CellHeight);
        window.Position=XUiC_RebirthWaypointPicker.Fit(position,window.Size,__instance.xui.GetXUiScreenSize());
    }
}
