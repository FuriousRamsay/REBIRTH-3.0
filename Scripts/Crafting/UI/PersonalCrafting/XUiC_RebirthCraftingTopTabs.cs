using System;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Controller for the Personal Crafting top navigation. Navigation remains delegated to the
/// verified RebirthCraftingNavigationService; this controller only owns the stable native-style
/// selection/hover presentation. The current Crafting tab and any hovered tab use the exact
/// ItemActionEntry highlight treatment: ui_game_select_row tinted white.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCraftingTopTabs : XUiController
{
    private sealed class TabVisual
    {
        public XUiController Button;
        public XUiV_Sprite Background;
        public XUiV_Sprite Icon;
        public XUiV_Label Label;
        public bool Active;
        public RebirthCraftingNavigationService.Destination Destination;
    }

    private readonly TabVisual[] tabs = new TabVisual[9];

    public override void Init()
    {
        base.Init();

        tabs[1] = Wire("Crafting", RebirthCraftingNavigationService.Destination.Crafting, true);
        tabs[2] = Wire("Character", RebirthCraftingNavigationService.Destination.Character, false);
        tabs[3] = Wire("Map", RebirthCraftingNavigationService.Destination.Map, false);

        tabs[5] = Wire("Quests", RebirthCraftingNavigationService.Destination.Quests, false);
        tabs[6] = Wire("Challenges", RebirthCraftingNavigationService.Destination.Challenges, false);
        tabs[7] = Wire("Players", RebirthCraftingNavigationService.Destination.Players, false);
        tabs[8] = Wire("Journal", RebirthCraftingNavigationService.Destination.Journal, false);

        for (int i = 0; i < tabs.Length; i++)
            ApplyVisual(tabs[i], false);
        // Character shares this bar but does not run the Crafting layout service.
        string[] names = { "Crafting", "Character", "Map", "Quests", "Challenges", "Players", "Journal" };
        int width = (ViewComponent.Size.x - 3 * (names.Length - 1)) / names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            string id = "rebirthCraftingTab" + names[i];
            var rect = GetChildById(id);
            if (rect?.ViewComponent == null) continue;
            rect.ViewComponent.Position = new Vector2i(i * (width + 3), 0);
            rect.ViewComponent.Size = new Vector2i(width, 52);
            foreach (string child in new[] { id + "Bg", id + "Frame", "btnRebirthCraftingTab" + names[i] })
                GetChildById(child).ViewComponent.Size = new Vector2i(width, 52);
            GetChildById(id + "Label").ViewComponent.Size = new Vector2i(width - 49, 30);
        }
    }

    public override void OnOpen(){base.OnOpen();RefreshPrimaryTab();}
    public override void Update(float dt){base.Update(dt);RefreshPrimaryTab();}
    private void RefreshPrimaryTab()
    {
        RebirthJournalGuideService.Poll(xui?.playerUI?.entityPlayer);
        var journal=tabs[8];
        if(journal?.Label!=null){int count=RebirthJournalGuideService.UnreadCount;
            journal.Label.Text=Localization.Get("xuiRebirthJournalNav")+(count>0?" ("+count+")":"");
            journal.Label.Color=count>0?new Color32(255,208,96,255):new Color32(245,245,247,255);}
        var tab=tabs[1];if(tab==null)return;
        bool section=xui?.playerUI?.windowManager!=null&&(xui.playerUI.windowManager.IsWindowOpen("rebirthBackpackLibrary")||xui.playerUI.windowManager.IsWindowOpen("rebirthBackpackSellStash"));
        bool context=!section&&RebirthContextNavigationService.IsActiveFor(xui);
        bool cooking=!context && RebirthCookingNavigation.Available(xui);
        string label=section?"STORAGE":context?RebirthContextNavigationService.PrimaryLabel:(cooking?"COOKING":Localization.Get("xuiWPcrafting"));
        if(tab.Label!=null && tab.Label.Text!=label)tab.Label.Text=label;
        string icon=section?"ui_game_symbol_loot_sack":context?RebirthContextNavigationService.PrimaryIcon:(cooking?"ui_game_symbol_fork":"ui_game_symbol_hammer");
        if(tab.Icon!=null && tab.Icon.SpriteName!=icon)tab.Icon.SpriteName=icon;
    }

    private TabVisual Wire(string suffix, RebirthCraftingNavigationService.Destination destination, bool active)
    {
        XUiController button = GetChildById("btnRebirthCraftingTab" + suffix);
        TabVisual visual = new TabVisual
        {
            Button = button,
            Background = GetChildById("rebirthCraftingTab" + suffix + "Bg")?.ViewComponent as XUiV_Sprite,
            Icon = GetChildById("rebirthCraftingTab" + suffix + "Icon")?.ViewComponent as XUiV_Sprite,
            Label = GetChildById("rebirthCraftingTab" + suffix + "Label")?.ViewComponent as XUiV_Label,
            Active = active,
            Destination = destination
        };

        if (button != null)
        {
            button.OnPress += delegate(XUiController sender, int mouseButton)
            {
                if(windowGroup.Controller is XUiC_RebirthCookingStation)
                {
                    if(destination==RebirthCraftingNavigationService.Destination.Crafting)return;
                    xui.playerUI.windowManager.Close(windowGroup);
                }
                RebirthCraftingNavigationService.Navigate(sender ?? this, destination);
            };
            button.OnHover += delegate(XUiController sender, bool isOver)
            {
                ApplyVisual(visual, isOver);
            };
        }
        return visual;
    }

    public void SetActiveDestination(RebirthCraftingNavigationService.Destination destination)
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            TabVisual tab = tabs[i];
            if (tab == null) continue;
            tab.Active = tab.Destination == destination;
            ApplyVisual(tab, false);
        }
    }

    private static void ApplyVisual(TabVisual tab, bool hovered)
    {
        if (tab == null)
            return;

        bool highlighted = tab.Active || hovered;
        if (tab.Background != null)
        {
            string sprite = highlighted ? "ui_game_select_row" : "menu_empty2px";
            Color color = highlighted ? Color.white : new Color32(12, 12, 15, 255);
            if (tab.Background.SpriteName != sprite)
                tab.Background.SpriteName = sprite;
            if (tab.Background.Color != color)
                tab.Background.Color = color;
        }

        // The native ItemActionEntry highlight keeps foreground content white.
        Color foreground = new Color32(245, 245, 247, 255);
        if (tab.Icon != null && tab.Icon.Color != foreground)
            tab.Icon.Color = foreground;
        if (tab.Label != null && tab.Label.Color != foreground)
            tab.Label.Color = foreground;
    }
}
