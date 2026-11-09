using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Modifier Station centre visual (owner 2026-11-08: "the pane needs to be visual, not a block of text"; concepts A and B
/// mixed). It covers the centre panel while no backpack item is selected.
/// <list type="bullet">
/// <item>Ready (armor and a fitting kit): the armor before and after as bonus stars with the kit between, the three
/// ways to decide a bonus (Random, Category at Tailoring 10, Exact at 25), the Tailoring track, rarity odds and the result.</item>
/// <item>Not ready or the kit does not fit: a step track (Tools, Armor, Kit, Choose, button) and the six kits as cards with
/// their star change; the kit in the slot is outlined (red when it does not fit, with the reason).</item>
/// </list>
/// It reads everything from <see cref="XUiC_RebirthBonusWorkspace"/> and redraws only when that changes.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthBonusCentre : XUiController
{
    // Controls of this window, looked up once: GetChildByType walks the whole window tree (profiler 2026-11-08).
    private readonly System.Collections.Generic.Dictionary<System.Type, XUiController> found = new System.Collections.Generic.Dictionary<System.Type, XUiController>();
    private T Find<T>() where T : XUiController
    {
        if (!found.TryGetValue(typeof(T), out var controller) || controller == null)
        {
            controller = windowGroup?.Controller?.GetChildByType<T>();
            if (controller != null) found[typeof(T)] = controller;
        }
        return controller as T;
    }

    private enum Pip { Empty, Filled, New, Maybe, Gone }

    private static readonly Color[] Rarity =
    {
        new Color32(200, 200, 200, 255), new Color32(136, 170, 255, 255), new Color32(255, 170, 85, 255), new Color32(204, 136, 255, 255)
    };
    private static readonly string[] RarityHex = { "c8c8c8", "88aaff", "ffaa55", "cc88ff" };
    private static readonly string[] RarityKeys = { "xuiRebirthRarityCommon", "xuiRebirthRarityUncommon", "xuiRebirthRarityRare", "xuiRebirthRarityLegendary" };
    private static readonly Color Text = new Color32(235, 235, 240, 255), Muted = new Color32(185, 182, 198, 255), Label = new Color32(170, 149, 199, 255),
        Purple = new Color32(181, 140, 255, 255), Gold = new Color32(204, 167, 56, 255), Ok = new Color32(232, 222, 139, 255),
        Raised = new Color32(27, 27, 31, 255), Well = new Color32(20, 20, 24, 255), Frame = new Color32(64, 64, 72, 255),
        GoldBg = new Color32(48, 42, 24, 255), Red = new Color32(210, 100, 100, 255), RedBg = new Color32(60, 30, 32, 255),
        PipEmpty = new Color32(58, 58, 66, 255), PipMaybe = new Color32(120, 120, 130, 255), PipGone = new Color32(210, 100, 100, 255), // same red as the legend (owner 2026-11-08)
        Dark = new Color32(17, 17, 21, 255), Track = new Color32(35, 35, 43, 255),
        TrackBright = new Color32(72, 72, 84, 255), MarkBright = new Color32(160, 158, 172, 255);
    private static readonly string[] Kits = { "kitSurvivorUpgrade", "kitAdjustment", "kitEliteReinforcement", "kitComprehensiveUpgrade", "kitRetuning", "kitBreakdown" };
    private const string Tool = "FuriousRamsayHammerPliers";

    private int x, y, width = 600, height = 480;
    private bool placed;
    private int drawnVersion = -1;
    private bool toolPresent;
    private string drawnKey;
    private float nextPoll;

    internal void Place(int left, int top, int w, int h)
    {
        placed = w > 0 && h > 0;
        if (left == x && top == y && w == width && h == height) return;
        x = left; y = top; width = w; height = h;
        drawnVersion = -1;
    }

    private T V<T>(string id) where T : XUiView => GetChildById(id)?.ViewComponent as T;
    private XUiView V(string id) => GetChildById(id)?.ViewComponent;

    public override void Init()
    {
        base.Init();
        // Hovering a kit card shows the kit's item popup (owner 2026-11-08); a single kit, so no count is shown.
        for (int k = 0; k < Kits.Length; k++)
        {
            string kit = Kits[k];
            var hit = GetChildById("mcKitHit" + k);
            if (hit?.ViewComponent != null) hit.ViewComponent.EventOnHover = true;
            RebirthCharacterItemStatsTooltip.BindStackHover(hit, () =>
            {
                var itemClass = ItemClass.GetItemClass(kit);
                return itemClass == null ? ItemStack.Empty : new ItemStack(new ItemValue(itemClass.Id), 1);
            });
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
        drawnVersion = -1;
        drawnKey = null;
    }

    public override void OnClose()
    {
        XUiC_RebirthCraftingRecipeDetails.ModifierVisualActive = false;
        base.OnClose();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (windowGroup?.isShowing != true || Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 0.2f;
        var workspace = Find<XUiC_RebirthBonusWorkspace>();
        var details = Find<XUiC_RebirthCraftingRecipeDetails>();
        // Hidden while the Selected Item panel shows an item (owner 2026-11-08: clicking an Output item drew both).
        var itemContext = Find<XUiC_RebirthCraftingItemContext>();
        bool show = placed && RebirthScrapWindow.IsModifier && workspace != null && workspace.IsShown && details != null && details.EmptyPromptVisible
            && !(itemContext != null && itemContext.IsContextVisible);
        if (XUiC_RebirthCraftingRecipeDetails.ModifierVisualActive != show)
        {
            XUiC_RebirthCraftingRecipeDetails.ModifierVisualActive = show;
            details?.RefreshModifierGuide();
        }
        if (ViewComponent != null && ViewComponent.IsVisible != show) ViewComponent.IsVisible = show;
        if (!show) return;
        bool tool = workspace.HasTool();
        string key = x + "|" + y + "|" + width + "|" + height + "|" + tool + "|" + workspace.OutputHasRoom();
        if (workspace.Version == drawnVersion && key == drawnKey) return;
        drawnVersion = workspace.Version;
        drawnKey = key;
        Draw(workspace, tool);
    }

    private bool HasTool()
    {
        int type = ItemClass.GetItem(Tool).type;
        // The station data first (what Apply checks), then the tool slot as drawn.
        var station = (windowGroup?.Controller as XUiC_WorkstationWindowGroup)?.WorkstationData?.TileEntity;
        if (station?.Tools != null && station.Tools.Any(t => t != null && !t.IsEmpty() && t.itemValue.type == type)) return true;
        var grid = Find<XUiC_WorkstationToolGrid>();
        if (grid == null) return false;
        foreach (var slot in grid.GetItemStackControllers())
            if (slot?.ItemStack != null && !slot.ItemStack.IsEmpty() && slot.ItemStack.itemValue.type == type) return true;
        return false;
    }

    // ---------- Drawing ----------

    private void Draw(XUiC_RebirthBonusWorkspace ws, bool tool)
    {
        Rect(ViewComponent, x, y, width, height);
        string block = ws.BlockReason;
        bool ready = ws.IsReady && block == null;
        SetGroup("A", ready);
        SetGroup("B", !ready);
        toolPresent = tool;
        if (ready) DrawReady(ws);
        else DrawGuide(ws, tool, block);
    }

    private static readonly string[] GroupA =
    {
        "mcCardBeforeBg", "mcCardBeforeFrame", "mcCardBeforeIcon", "mcCardBeforeCaption", "mcCardAfterBg", "mcCardAfterFrame", "mcCardAfterIcon", "mcCardAfterCaption",
        "mcKitIcon", "mcArrowLine", "mcArrowHead", "mcArrowText", "mcTrackTitle", "mcTrackBar", "mcTrackFill", "mcTrackMark10", "mcTrackMark25", "mcTrackText10",
        "mcTrackText25", "mcTrackYou", "mcOddsTitle", "mcOdds0", "mcOdds1", "mcOdds2", "mcOdds3", "mcOddsLegend", "mcResult", "mcOddsKey0", "mcOddsKey1", "mcOddsKey2", "mcOddsKey3", "mcOddsKeyText0", "mcOddsKeyText1", "mcOddsKeyText2", "mcOddsKeyText3"
    };

    private IEnumerable<string> GroupIds(string group)
    {
        if (group == "A")
        {
            foreach (var id in GroupA) yield return id;
            foreach (var c in new[] { "Before", "After" }) for (int i = 0; i < 4; i++) { yield return "mcPip" + c + i; yield return "mcPipText" + c + i; }
            for (int t = 0; t < 3; t++) foreach (var p in new[] { "mcTileBg", "mcTileFrame", "mcTileIcon", "mcTileGlyph", "mcTileTitle", "mcTileSub", "mcTileLock" }) yield return p + t;
        }
        else
        {
            for (int s = 0; s < 5; s++) { yield return "mcStepBg" + s; yield return "mcStepFrame" + s; yield return "mcStepIcon" + s; yield return "mcStepText" + s; if (s < 4) yield return "mcStepJoin" + s; }
            for (int k = 0; k < 6; k++)
            {
                foreach (var p in new[] { "mcKitBg", "mcKitFrame", "mcKitCardIcon", "mcKitName", "mcKitArrow", "mcKitNote" }) yield return p + k;
                for (int p = 0; p < 8; p++) { yield return "mcKitPip" + k + "_" + p; yield return "mcKitPipText" + k + "_" + p; }
            }
            yield return "mcLegend"; yield return "mcReasonBg"; yield return "mcReasonFrame"; yield return "mcReason";
            for (int k = 0; k < 6; k++) yield return "mcKitHit" + k;
            for (int k = 0; k < 3; k++) { yield return "mcLegKey" + k; yield return "mcLegText" + k; }
        }
    }

    /// <summary>Hides a whole group; drawing then shows what it uses.</summary>
    private void SetGroup(string group, bool keep)
    {
        if (keep) return;
        foreach (var id in GroupIds(group)) Show(V(id), false);
    }

    // ----- Ready: before / after (concept A) -----

    private void DrawReady(XUiC_RebirthBonusWorkspace ws)
    {
        string kit = ws.KitName;
        bool result = ws.InResult;
        var before = result ? ws.ResultBefore : ws.Bonuses;
        var beforePips = before.Select(b => (Pip.Filled, (int)b.Definition.Rarity, "")).ToList();
        var afterPips = new List<(Pip kind, int rarity, string text)>();
        string arrow;
        if (result)
        {
            var old = new HashSet<string>(ws.ResultBefore.Select(b => b.Definition.Id + ":" + b.Value.ToString("0.####", CultureInfo.InvariantCulture)));
            foreach (var b in ws.ResultAfter)
                afterPips.Add((Pip.Filled, (int)b.Definition.Rarity, old.Contains(b.Definition.Id + ":" + b.Value.ToString("0.####", CultureInfo.InvariantCulture)) ? "" : "+"));
            arrow = Loc("xuiRebirthMcDone");
        }
        else
        {
            switch (kit)
            {
                case "kitRetuning":
                    for (int i = 0; i < before.Count; i++)
                        afterPips.Add(ws.Actions[i] == RebirthArmorBonusRowAction.Keep ? (Pip.Filled, (int)before[i].Definition.Rarity, "") : (Pip.New, -1, "?"));
                    arrow = Loc("xuiRebirthMcRetune");
                    break;
                case "kitBreakdown":
                {
                    int removed = 0;
                    for (int i = 0; i < before.Count; i++)
                    {
                        bool gone = before.Count == 1 || ws.Actions[i] == RebirthArmorBonusRowAction.Remove;
                        if (gone) removed++;
                        afterPips.Add(gone ? (Pip.Gone, -1, "") : (Pip.Filled, (int)before[i].Definition.Rarity, ""));
                    }
                    arrow = string.Format(Loc("xuiRebirthMcRemove"), removed);
                    break;
                }
                default:
                {
                    afterPips.AddRange(beforePips.Select(p => (p.Item1, p.Item2, p.Item3)));
                    int adds = RebirthArmorBonusKits.AddRowsOf(kit);
                    for (int i = 0; i < adds; i++) afterPips.Add((Pip.New, -1, "?"));
                    bool chance = RebirthArmorBonusKits.LevelOf(kit) == 3;
                    if (chance) afterPips.Add((Pip.Maybe, -1, "?"));
                    arrow = chance ? string.Format(Loc("xuiRebirthMcPlusChance"), adds, adds + 1) : string.Format(Loc("xuiRebirthMcPlus"), adds);
                    break;
                }
            }
        }

        // Cards and arrow
        int cardW = Math.Min(210, (width - 200) / 2), cardH = 150, gap = 16;
        int arrowW = Math.Min(200, width - 2 * cardW - 2 * gap);
        int left = (width - (2 * cardW + 2 * gap + arrowW)) / 2;
        int top = -6;
        var itemClass = ItemClass.GetForId(ws.ItemType);
        DrawCard("Before", left, top, cardW, cardH, itemClass, Loc("xuiRebirthMcBefore"), beforePips.Select(p => (p.Item1, p.Item2, p.Item3)).ToList(), false);
        int ax = left + cardW + gap;
        DrawCard("After", ax + arrowW + gap, top, cardW, cardH, itemClass, Loc(result ? "xuiRebirthMcResult" : "xuiRebirthMcAfter"), afterPips, true);
        var kitClass = ItemClass.GetItemClass(kit);
        var kitIcon = V<XUiV_Sprite>("mcKitIcon");
        Icon(kitIcon, kitClass);
        Rect(kitIcon, ax + arrowW / 2 - 34, top - 6, 68, 68); // larger kit icon (owner 2026-11-08)
        Show(kitIcon, kitClass != null && !result);
        Rect(V("mcArrowLine"), ax + 4, top - 80, arrowW - 22, 3);
        Show(V("mcArrowLine"), true);
        Rect(V("mcArrowHead"), ax + arrowW - 24, top - 72, 20, 20);
        Show(V("mcArrowHead"), true);
        var arrowText = V<XUiV_Label>("mcArrowText");
        Rect(arrowText, ax, top - 94, arrowW, 24);
        Set(arrowText, arrow, result ? Ok : Text);
        Show(arrowText, true);

        // Three ways to decide a bonus (not for Breakdown, which rolls nothing)
        bool rolls = kit != "kitBreakdown" && !result;
        int y2 = top - cardH - 14;
        DrawTiles(ws, rolls, y2);
        int y3 = rolls ? y2 - 72 : y2;
        DrawTrack(ws, rolls, y3);
        int y4 = rolls ? y3 - 66 : y3;
        DrawOdds(ws, rolls, y4);
        // Tailoring and odds sit on a solid panel so they read over the station background (owner 2026-11-08).
        var infoBg = V("mcInfoBg"); var infoFrame = V("mcInfoFrame");
        Rect(infoBg, 4, y3 + 8, width - 8, 146); Rect(infoFrame, 4, y3 + 8, width - 8, 146);
        Show(infoBg, rolls); Show(infoFrame, rolls);
        Show(V("mcResult"), false); // the result count line was removed (owner 2026-11-08); the arrow says what the kit does
        int y5 = rolls ? y4 - 76 : y4;
        DrawNext(NextStep(ws, toolPresent), y5);
    }

    /// <summary>One plain instruction for what to do now (owner 2026-11-08: "there are no instructions").</summary>
    private static string NextStep(XUiC_RebirthBonusWorkspace ws, bool tool)
    {
        string verb = ws.VerbText;
        if (ws.InResult) return Loc("xuiRebirthMcNextAgain");
        if (!tool) return Loc("xuiRebirthMcNextTool");
        if (ws.ItemType == 0) return Loc("xuiRebirthMcNextArmor");
        if (ws.KitName.Length == 0) return Loc("xuiRebirthMcNextKit");
        if (ws.BlockReason != null) return Loc("xuiRebirthMcNextWrongKit");
        if (!ws.OutputHasRoom()) return Loc("xuiRebirthMcNextOutput");
        switch (ws.KitName)
        {
            case "kitRetuning":
                return ws.Actions.All(a => a == RebirthArmorBonusRowAction.Keep) ? Loc("xuiRebirthMcNextRetuneSet") : string.Format(Loc("xuiRebirthMcNextApply"), verb);
            case "kitBreakdown":
                return ws.Bonuses.Count > 1 && ws.Actions.All(a => a == RebirthArmorBonusRowAction.Keep) ? Loc("xuiRebirthMcNextBreakdownMark") : string.Format(Loc("xuiRebirthMcNextApply"), verb);
            default:
                if (ws.Adds.Any(f => f.Depth != RebirthArmorBonusFocusDepth.Any)) return string.Format(Loc("xuiRebirthMcNextApply"), verb);
                return string.Format(Loc(ws.Tailoring < 10f ? "xuiRebirthMcNextRandomOnly" : "xuiRebirthMcNextChooseOrRandom"), verb);
        }
    }

    private void DrawNext(string text, int top)
    {
        var bg = V<XUiV_Sprite>("mcNextBg"); var frame = V<XUiV_Sprite>("mcNextFrame");
        var tag = V<XUiV_Label>("mcNextTag"); var label = V<XUiV_Label>("mcNext");
        Rect(bg, 4, top, width - 8, 40); Rect(frame, 4, top, width - 8, 40);
        Rect(tag, 14, top - 11, 60, 22);
        Rect(label, 70, top - 10, width - 84, 24);
        Set(tag, Loc("xuiRebirthMcNextTag"), Gold);
        Set(label, text, Text);
        foreach (var v in new XUiView[] { bg, frame, tag, label }) Show(v, true);
    }

    private void DrawCard(string id, int cx, int top, int w, int h, ItemClass item, string caption, List<(Pip kind, int rarity, string text)> pips, bool highlight)
    {
        var bg = V<XUiV_Sprite>("mcCard" + id + "Bg");
        var frame = V<XUiV_Sprite>("mcCard" + id + "Frame");
        Rect(bg, cx, top, w, h); Rect(frame, cx, top, w, h);
        Tint(bg, highlight ? GoldBg : Raised);
        Tint(frame, highlight ? Gold : Frame);
        Show(bg, true); Show(frame, true);
        var icon = V<XUiV_Sprite>("mcCard" + id + "Icon");
        Icon(icon, item);
        Rect(icon, cx + (w - 72) / 2, top - 8, 72, 72);
        Show(icon, item != null);
        const int pip = 32, pipGap = 8; // bigger stars (owner 2026-11-08)
        int rowW = 4 * pip + 3 * pipGap, px = cx + (w - rowW) / 2;
        for (int i = 0; i < 4; i++)
        {
            var p = i < pips.Count ? pips[i] : (Pip.Empty, -1, "");
            DrawPip(V<XUiV_Sprite>("mcPip" + id + i), V<XUiV_Label>("mcPipText" + id + i), px + i * (pip + pipGap), top - 86, pip, p.Item1, p.Item2, p.Item3);
        }
        var label = V<XUiV_Label>("mcCard" + id + "Caption");
        Rect(label, cx + 4, top - 122, w - 8, 24);
        Set(label, caption, Muted);
        Show(label, true);
    }

    private static void DrawPip(XUiV_Sprite sprite, XUiV_Label text, int px, int py, int size, Pip kind, int rarity, string glyph)
    {
        Rect(sprite, px, py, size, size);
        Tint(sprite, kind == Pip.Filled ? Rarity[Math.Max(0, rarity)] : kind == Pip.New ? Gold : kind == Pip.Maybe ? PipMaybe : kind == Pip.Gone ? PipGone : PipEmpty);
        Show(sprite, true);
        // The "?" centred on the star: font sized to the star, capitals centred in the box (as the workspace rows).
        if (text != null)
        {
            int font = size >= 28 ? 18 : size >= 20 ? 14 : 11;
            if (text.FontSize != font) text.FontSize = font;
            int textTop = py - (int)Math.Round(size / 2f - font * 0.45f) - 1; // a star carries its weight low: one pixel down
            Rect(text, px, textTop, size, font + 6);
        }
        Set(text, glyph ?? string.Empty, Dark);
        Show(text, !string.IsNullOrEmpty(glyph));
    }

    private void DrawTiles(XUiC_RebirthBonusWorkspace ws, bool rolls, int top)
    {
        var focuses = new List<RebirthArmorBonusFocus>(ws.Adds);
        for (int i = 0; i < ws.RowFocuses.Count && i < ws.Actions.Count; i++) if (ws.Actions[i] == RebirthArmorBonusRowAction.Replace) focuses.Add(ws.RowFocuses[i]);
        var deepest = focuses.Count == 0 ? RebirthArmorBonusFocusDepth.Any : focuses.Max(f => f.Depth);
        var category = focuses.FirstOrDefault(f => f.Depth == RebirthArmorBonusFocusDepth.Category);
        var exact = focuses.FirstOrDefault(f => f.Depth >= RebirthArmorBonusFocusDepth.Effect);
        int tileW = (width - 8 - 2 * 10) / 3;
        for (int t = 0; t < 3; t++)
        {
            int tx = 4 + t * (tileW + 10);
            int need = t == 1 ? 10 : t == 2 ? 25 : 0;
            bool locked = ws.Tailoring < need;
            bool on = t == 0 ? deepest == RebirthArmorBonusFocusDepth.Any : t == 1 ? deepest == RebirthArmorBonusFocusDepth.Category : deepest >= RebirthArmorBonusFocusDepth.Effect;
            var bg = V<XUiV_Sprite>("mcTileBg" + t); var frame = V<XUiV_Sprite>("mcTileFrame" + t);
            var icon = V<XUiV_Sprite>("mcTileIcon" + t); var glyph = V<XUiV_Label>("mcTileGlyph" + t);
            var title = V<XUiV_Label>("mcTileTitle" + t); var sub = V<XUiV_Label>("mcTileSub" + t); var lockIcon = V<XUiV_Sprite>("mcTileLock" + t);
            foreach (var v in new XUiView[] { bg, frame, icon, glyph, title, sub, lockIcon }) Show(v, false);
            if (!rolls) continue;
            Rect(bg, tx, top, tileW, 58); Rect(frame, tx, top, tileW, 58);
            Tint(bg, on ? GoldBg : Raised); Tint(frame, on ? Gold : Frame);
            Show(bg, true); Show(frame, true);
            Color iconColor = on ? Gold : locked ? new Color32(110, 108, 120, 255) : Muted;
            if (t == 0)
            {
                Rect(glyph, tx + 8, top - 9, 38, 40);
                Set(glyph, "?", iconColor);
                Show(glyph, true);
            }
            else
            {
                Rect(icon, tx + 10, top - 12, 34, 34);
                Tint(icon, iconColor);
                Show(icon, true);
            }
            Rect(title, tx + 54, top - 8, tileW - 60, 24);
            Set(title, Loc(t == 0 ? "xuiRebirthMcRandom" : t == 1 ? "xuiRebirthMcCategory" : "xuiRebirthMcExact"), locked ? Muted : Text);
            Show(title, true);
            string subText = t == 0 ? string.Format(Loc("xuiRebirthMcAnyOf"), ws.SlotBonusCount)
                : locked ? string.Format(Loc("xuiRebirthMcNeeds"), need)
                : t == 1 && category.Depth == RebirthArmorBonusFocusDepth.Category ? ws.FocusName(category)
                : t == 2 && exact.Depth >= RebirthArmorBonusFocusDepth.Effect ? ws.FocusName(exact)
                : Loc("xuiRebirthMcPressChoose");
            int subX = tx + 54 + (locked ? 18 : 0);
            Rect(sub, subX, top - 32, tileW - (subX - tx) - 6, 20);
            Set(sub, subText, on ? Ok : Muted);
            Show(sub, true);
            if (locked) { Rect(lockIcon, tx + 54, top - 34, 14, 14); Tint(lockIcon, Muted); Show(lockIcon, true); }
        }
    }

    private void DrawTrack(XUiC_RebirthBonusWorkspace ws, bool rolls, int top)
    {
        var ids = new[] { "mcTrackTitle", "mcTrackBar", "mcTrackFill", "mcTrackMark10", "mcTrackMark25", "mcTrackText10", "mcTrackText25", "mcTrackYou" };
        foreach (var id in ids) Show(V(id), false);
        if (!rolls) return;
        float t = ws.Tailoring;
        string value = t.ToString("0.#", CultureInfo.InvariantCulture);
        // Plain wording (owner 2026-11-08): your Tailoring, then what the next level lets you do.
        string line = string.Format(Loc(t >= 25f ? "xuiRebirthMcTailoringAll" : t >= 10f ? "xuiRebirthMcTailoringTo25" : "xuiRebirthMcTailoringTo10"), value);
        var title = V<XUiV_Label>("mcTrackTitle");
        Rect(title, 14, top, width - 28, 20);
        Set(title, line.ToUpperInvariant(), Text);
        Show(title, true);
        int barX = 16, barW = width - 32, barY = top - 28;
        const float scale = 30f; // 10 and 25 sit at one third and five sixths, as in the concept
        int x10 = barX + (int)(barW * 10 / scale), x25 = barX + (int)(barW * 25 / scale);
        var bar = V<XUiV_Sprite>("mcTrackBar");
        Rect(bar, barX, barY, barW, 10); Tint(bar, TrackBright); Show(bar, true);
        var fill = V("mcTrackFill");
        Rect(fill, barX, barY, Math.Max(1, (int)(barW * Math.Min(1f, t / scale))), 10); Show(fill, t > 0f);
        var m10 = V<XUiV_Sprite>("mcTrackMark10"); var m25 = V<XUiV_Sprite>("mcTrackMark25");
        Rect(m10, x10 - 1, barY + 6, 3, 22); Rect(m25, x25 - 1, barY + 6, 3, 22);
        Tint(m10, t >= 10f ? Ok : MarkBright); Tint(m25, t >= 25f ? Ok : MarkBright);
        Show(m10, true); Show(m25, true);
        var l10 = V<XUiV_Label>("mcTrackText10"); var l25 = V<XUiV_Label>("mcTrackText25");
        Rect(l10, x10 - 60, barY - 12, 120, 20); Rect(l25, x25 - 60, barY - 12, 120, 20);
        Set(l10, Loc("xuiRebirthBonusWsMark10"), t >= 10f ? Ok : Text);
        Set(l25, Loc("xuiRebirthBonusWsMark25"), t >= 25f ? Ok : Text);
        Show(l10, true); Show(l25, true);
    }

    private void DrawOdds(XUiC_RebirthBonusWorkspace ws, bool rolls, int top)
    {
        var ids = new[] { "mcOddsTitle", "mcOdds0", "mcOdds1", "mcOdds2", "mcOdds3", "mcOddsLegend",
            "mcOddsKey0", "mcOddsKey1", "mcOddsKey2", "mcOddsKey3", "mcOddsKeyText0", "mcOddsKeyText1", "mcOddsKeyText2", "mcOddsKeyText3" };
        foreach (var id in ids) Show(V(id), false);
        int level = RebirthArmorBonusKits.LevelOf(ws.KitName);
        if (!rolls || level == 0) return;
        int quality = Math.Max(1, ws.ItemQuality);
        var weights = RebirthArmorBonusCatalog.GetRarityWeights(level, quality);
        float total = Math.Max(1, weights.Sum());
        var title = V<XUiV_Label>("mcOddsTitle");
        Rect(title, 14, top, width - 28, 20);
        // The kit by name, not its level (owner 2026-11-08).
        string kitName = ItemClass.GetItemClass(ws.KitName)?.GetLocalizedItemName() ?? ws.KitName;
        Set(title, string.Format(Loc("xuiRebirthMcOddsTitle"), kitName, quality).ToUpperInvariant(), Text);
        Show(title, true);
        int bx = 16, bw = width - 32;
        for (int k = 0; k < 4; k++)
        {
            int w = k == 3 ? 16 + bw - bx : (int)Math.Round(bw * weights[k] / total);
            var seg = V("mcOdds" + k);
            Rect(seg, bx, top - 24, Math.Max(1, w), 10);
            Show(seg, w > 0);
            bx += Math.Max(0, w);
        }
        // A normal legend: a coloured square, then white "Common (66%)" (owner 2026-11-08).
        int lx = 16, ly = top - 40;
        for (int k = 0; k < 4; k++)
        {
            float p = weights[k] * 100f / total;
            string text = Loc(RarityKeys[k]) + " (" + p.ToString(p > 0f && p < 1f ? "0.0" : "0", CultureInfo.InvariantCulture) + "%)";
            var key = V("mcOddsKey" + k);
            var keyText = V<XUiV_Label>("mcOddsKeyText" + k);
            Rect(key, lx, ly - 3, 14, 14);
            int textWidth = text.Length * 8 + 6;
            // Text capitals centred on the 14 px square (square top ly - 3): same rule as the rows, half the band minus 0.45 of the font.
            Rect(keyText, lx + 20, ly - 3 - (int)Math.Round(7f - (keyText != null && keyText.FontSize > 0 ? keyText.FontSize : 16) * 0.45f) - 1, textWidth, 22);
            Set(keyText, text, Text);
            Show(key, true); Show(keyText, true);
            lx += 20 + textWidth + 18;
        }
    }

    // ----- Not ready: step track and kit sheet (concept B) -----

    private void DrawGuide(XUiC_RebirthBonusWorkspace ws, bool tool, string block)
    {
        bool armor = ws.ItemType != 0, kit = ws.KitName.Length > 0;
        var steps = new[]
        {
            (Loc("xuiRebirthMcStepTools"), "ui_game_symbol_wrench", tool),
            (Loc("xuiRebirthMcStepArmor"), "ui_game_symbol_armor_iron", armor),
            (Loc("xuiRebirthMcStepKit"), "ui_game_symbol_mods", kit && block == null),
            (Loc("xuiRebirthMcStepChoose"), "ui_game_symbol_quickstack_categories", false),
            (ws.VerbText, "ui_game_symbol_arrow_right", false)
        };
        int current = Array.FindIndex(steps, s => !s.Item3);
        int stepW = width / 5, dot = 48, top = -6;
        for (int s = 0; s < 5; s++)
        {
            int sx = s * stepW + (stepW - dot) / 2;
            bool done = steps[s].Item3, now = s == current;
            var bg = V<XUiV_Sprite>("mcStepBg" + s); var frame = V<XUiV_Sprite>("mcStepFrame" + s); var icon = V<XUiV_Sprite>("mcStepIcon" + s); var text = V<XUiV_Label>("mcStepText" + s);
            Rect(bg, sx, top, dot, dot); Rect(frame, sx, top, dot, dot);
            Tint(bg, now ? GoldBg : Well);
            Tint(frame, done ? Ok : now ? Gold : Frame);
            Show(bg, true); Show(frame, true);
            Rect(icon, sx + 10, top - 10, dot - 20, dot - 20);
            if (icon != null && icon.SpriteName != (done ? "ui_game_symbol_check" : steps[s].Item2)) icon.SpriteName = done ? "ui_game_symbol_check" : steps[s].Item2;
            Tint(icon, done ? Ok : now ? Gold : Muted);
            Show(icon, true);
            Rect(text, s * stepW, top - dot - 6, stepW, 22);
            Set(text, steps[s].Item1, now ? Text : Muted);
            Show(text, true);
            if (s < 4)
            {
                var join = V<XUiV_Sprite>("mcStepJoin" + s);
                Rect(join, sx + dot + 4, top - dot / 2, stepW - dot - 8, 2);
                Tint(join, done ? Ok : Frame);
                Show(join, true);
            }
        }

        // Kit sheet: what each kit does to the bonus stars; the kit in the slot is outlined.
        int cols = 3, gap = 8, cardW = (width - 8 - (cols - 1) * gap) / cols, cardH = 74, sheetTop = top - dot - 40;
        for (int k = 0; k < Kits.Length; k++)
        {
            int cx = 4 + (k % cols) * (cardW + gap), cy = sheetTop - (k / cols) * (cardH + gap);
            bool selected = ws.KitName == Kits[k];
            bool bad = selected && block != null;
            var bg = V<XUiV_Sprite>("mcKitBg" + k); var frame = V<XUiV_Sprite>("mcKitFrame" + k);
            Rect(bg, cx, cy, cardW, cardH); Rect(frame, cx, cy, cardW, cardH);
            Tint(bg, bad ? RedBg : selected ? GoldBg : Raised);
            Tint(frame, bad ? Red : selected ? Gold : Frame);
            Show(bg, true); Show(frame, true);
            var kitClass = ItemClass.GetItemClass(Kits[k]);
            var icon = V<XUiV_Sprite>("mcKitCardIcon" + k);
            Icon(icon, kitClass);
            Rect(icon, cx + 8, cy - 8, 30, 30);
            Show(icon, kitClass != null);
            var name = V<XUiV_Label>("mcKitName" + k);
            Rect(name, cx + 44, cy - 12, cardW - 50, 22);
            Set(name, kitClass?.GetLocalizedItemName() ?? Kits[k], selected ? Text : Muted);
            Show(name, true);
            DrawKitPips(k, cx + 10, cy - 46, cardW - 16);
            var hit = V("mcKitHit" + k);
            Rect(hit, cx, cy, cardW, cardH);
            Show(hit, true);
            Show(V("mcKitNote" + k), false);
        }

        // A normal legend under the cards, centred (owner 2026-11-08): a star in each colour, then white text.
        int legendTop = sheetTop - 2 * (cardH + gap) - 2;
        Show(V("mcLegend"), false);
        var legendItems = new[] { (Loc("xuiRebirthMcLegendNew"), Gold), (Loc("xuiRebirthMcLegendChance"), PipMaybe), (Loc("xuiRebirthMcLegendRemoved"), Red) };
        const int keySize = 16, keyGap = 6, itemGap = 28;
        int total = legendItems.Sum(item => keySize + keyGap + item.Item1.Length * 8) + itemGap * (legendItems.Length - 1);
        int lx = Math.Max(4, (width - total) / 2);
        for (int k = 0; k < legendItems.Length; k++)
        {
            var key = V<XUiV_Sprite>("mcLegKey" + k);
            var text = V<XUiV_Label>("mcLegText" + k);
            int textWidth = legendItems[k].Item1.Length * 8 + 4;
            Rect(key, lx, legendTop - 2, keySize, keySize);
            Tint(key, legendItems[k].Item2);
            // Centred on the star like the workspace rows: top - round(star/2 - font*0.45), plus the star's low weight (owner 2026-11-08).
            int font = text?.FontSize ?? 16;
            Rect(text, lx + keySize + keyGap, legendTop - 2 - (int)Math.Round(keySize / 2f - font * 0.45f) - 1, textWidth, font + 6);
            Set(text, legendItems[k].Item1, Text);
            Show(key, true); Show(text, true);
            lx += keySize + keyGap + textWidth + itemGap;
        }

        bool reason = block != null;
        foreach (var id in new[] { "mcReasonBg", "mcReasonFrame", "mcReason" }) Show(V(id), reason);
        if (reason)
        {
            int ry = legendTop - 30;
            Rect(V("mcReasonBg"), 4, ry, width - 8, 52); Rect(V("mcReasonFrame"), 4, ry, width - 8, 52);
            var label = V<XUiV_Label>("mcReason");
            Rect(label, 14, ry - 14, width - 28, 26);
            Set(label, block, Text);
        }
        DrawNext(NextStep(ws, tool), legendTop - (reason ? 92 : 30));
    }

    /// <summary>The star change each kit makes (concept B): before, arrow, after.</summary>
    private void DrawKitPips(int k, int px, int py, int maxWidth)
    {
        Pip[] before, after;
        switch (Kits[k])
        {
            case "kitSurvivorUpgrade": before = new[] { Pip.Empty }; after = new[] { Pip.New }; break;
            case "kitAdjustment": before = new[] { Pip.Filled }; after = new[] { Pip.Filled, Pip.New }; break;
            case "kitEliteReinforcement": before = new[] { Pip.Filled, Pip.Filled }; after = new[] { Pip.Filled, Pip.Filled, Pip.New, Pip.Maybe }; break;
            case "kitComprehensiveUpgrade": before = new[] { Pip.Empty }; after = new[] { Pip.New, Pip.New, Pip.New, Pip.Maybe }; break;
            case "kitRetuning": before = new[] { Pip.Filled, Pip.Filled }; after = new[] { Pip.Filled, Pip.New }; break;
            default: before = new[] { Pip.Filled, Pip.Filled, Pip.Filled }; after = new[] { Pip.Filled, Pip.Gone, Pip.Gone }; break;
        }
        const int size = 18, gap = 5;
        int x0 = px, index = 0;
        foreach (var p in before) { DrawKitPip(k, index++, x0, py, size, p); x0 += size + gap; }
        // The game arrow icon, as tall as the stars and level with them (owner 2026-11-08).
        var arrow = V<XUiV_Sprite>("mcKitArrow" + k);
        Rect(arrow, x0 + 2, py, size, size);
        Tint(arrow, Purple);
        Show(arrow, true);
        x0 += size + 8;
        foreach (var p in after) { if (index < 8) DrawKitPip(k, index++, x0, py, size, p); x0 += size + gap; }
        for (; index < 8; index++) { Show(V("mcKitPip" + k + "_" + index), false); Show(V("mcKitPipText" + k + "_" + index), false); }
    }

    private void DrawKitPip(int k, int index, int px, int py, int size, Pip kind)
    {
        string glyph = kind == Pip.New || kind == Pip.Maybe ? "?" : string.Empty;
        DrawPip(V<XUiV_Sprite>("mcKitPip" + k + "_" + index), V<XUiV_Label>("mcKitPipText" + k + "_" + index), px, py, size, kind, 0, glyph);
    }

    // ---------- Helpers ----------

    private static string Loc(string key) => Localization.Get(key);

    private static void Icon(XUiV_Sprite sprite, ItemClass item)
    {
        if (sprite == null || item == null) return;
        string name = item.GetIconName();
        if (sprite.SpriteName != name) sprite.SpriteName = name;
        Color tint = item.GetIconTint();
        if (sprite.Color != tint) sprite.Color = tint;
    }

    private static void Rect(XUiView view, int x, int y, int w, int h)
    {
        if (view == null) return;
        var position = new Vector2i(x, y);
        var size = new Vector2i(Math.Max(1, w), Math.Max(1, h));
        if (view.Position != position) view.Position = position;
        if (view.Size != size) view.Size = size;
    }

    private static void Show(XUiView view, bool visible)
    {
        if (view != null && view.IsVisible != visible) view.IsVisible = visible;
    }

    private static void Set(XUiV_Label label, string text, Color color)
    {
        if (label == null) return;
        if (label.Text != text) label.Text = text;
        if (label.Color != color) label.Color = color;
    }

    private static void Tint(XUiV_Sprite sprite, Color color)
    {
        if (sprite != null && sprite.Color != color) sprite.Color = color;
    }
}
