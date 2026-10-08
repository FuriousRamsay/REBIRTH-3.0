using UnityEngine;

/// <summary>Shared HUD and Conditions thresholds; descriptions are localized as complete sentences.</summary>
public static class RebirthStressPresentation
{
    public const string Icon="rb_condition_stress";
    public static string Tier(float value)=>value>=80?"High":value>=60?"Shaken":value>=40?"Tense":"Calm";
    public static string Name(float value)=>Localization.Get("rbStress"+Tier(value));
    public static Color Color(float value)=>value>=80?new Color32(235,92,92,255):value>=60?new Color32(241,155,71,255):value>=40?new Color32(232,205,95,255):new Color32(112,196,126,255);
    public static string PlaceKey(int place)=>place==3?"rbStressDanger":place==2?"rbStressTrader":place==1?"rbStressCamp":"rbStressExploring";
    public static string PlaceIcon(int place)=>place==3?"ui_game_symbol_zombie":place==2?"ui_game_symbol_map_trader":place==1?"ui_game_symbol_map_bed":"ui_game_symbol_map";
    public static void AddConditions(RebirthCharacterUiSnapshot snapshot,EntityPlayer player)
    {
        if(player==null)return;
        float value=player.Buffs.GetCustomVar("rebirthStress");
        int place=(int)player.Buffs.GetCustomVar("rebirthStressLocation");
        snapshot.Conditions.Add(new RebirthCharacterUiCondition {
            Id="rebirth:stress",Name=Localization.Get("rbStressTitle"),Category=Localization.Get("rbStressTitle"),
            Detail=value.ToString("0")+" / 100 · "+Name(value),Status=Name(value)+" · "+value.ToString("0.0")+" / 100",
            Description=Localization.Get("rbStress"+Tier(value)+"Description"),Effects=Localization.Get("rbStress"+Tier(value)+"Effects"),
            Guidance=Localization.Get("rbStressRecovery"),Icon=Icon,IconColor=Color(value),Negative=value>=60
        });
        snapshot.Conditions.Add(new RebirthCharacterUiCondition {
            Id="rebirth:surroundings",Name=Localization.Get(PlaceKey(place)),Category=Localization.Get("rbStressSurroundings"),
            Detail=Localization.Get("rbStressSurroundings"),Status=Localization.Get(PlaceKey(place)),
            Description=Localization.Get(PlaceKey(place)+"Description"),Guidance=Localization.Get("rbStressLocationGuidance"),
            Atlas="UIAtlas",Icon=PlaceIcon(place),Negative=place==3,Positive=place==1||place==2
        });
    }
}
