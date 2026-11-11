using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Modifier Station workspace (armor bonus focus spec sections 6 and 7). The left column of the Modifier Station shows:
/// <list type="bullet">
/// <item>Rows: the kit's rows for the armor and kit in the slots (kept bonuses, new-bonus rows with a focus link,
/// Keep / Reroll / Replace, Keep / Remove, the random 25% row) and the reason a kit cannot be used.</item>
/// <item>Explorer: armor type, category, effect and exact bonus, with locks by Tailoring (10 category, 25 effect/exact),
/// "Already on this item" for families the item keeps, a read-only category preview at Tailoring 10-24, search, and
/// favourites saved per character in the save's local folder (a UI preference; the server never reads it).</item>
/// <item>Summary: the Tailoring strip, the rarity odds for the kit level and item quality, and the result count.</item>
/// <item>Result: bonuses before and after the last applied kit, new ones highlighted.</item>
/// </list>
/// The choices become a <see cref="RebirthArmorBonusKitRequest"/> when the action button is pressed; the server checks
/// them again. Every view is a fixed pool from controls.xml (rebirth_modifier_workspace) and is only touched when a
/// value changes; the pane redraws only after a change.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthBonusWorkspace : XUiController
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

    private const int RowPool = 16, Pad = 10, RowGap = 4, LabelHeight = 24, RowHeight = 36, SegRowHeight = 62, ListRowHeight = 34, ListGap = 3;
    private static readonly string[] RarityHex = { "c8c8c8", "88aaff", "ffaa55", "cc88ff" };
    private static readonly string[] RarityKeys = { "xuiRebirthRarityCommon", "xuiRebirthRarityUncommon", "xuiRebirthRarityRare", "xuiRebirthRarityLegendary" };
    private static readonly Color[] RarityColors =
    {
        new Color32(200, 200, 200, 255), new Color32(136, 170, 255, 255), new Color32(255, 170, 85, 255), new Color32(204, 136, 255, 255)
    };
    private static readonly Color TextColor = new Color32(235, 235, 240, 255), MutedColor = new Color32(185, 182, 198, 255),
        LabelColor = new Color32(170, 149, 199, 255), LinkColor = new Color32(181, 140, 255, 255), OkColor = new Color32(232, 222, 139, 255),
        RowBg = new Color32(27, 27, 31, 255), RowFrame = new Color32(52, 52, 60, 255), OpenBg = new Color32(17, 17, 21, 0),
        RemoveBg = new Color32(60, 30, 32, 255), RemoveFrame = new Color32(210, 100, 100, 255), RemoveText = new Color32(230, 140, 140, 255),
        SelectBg = new Color32(48, 42, 24, 255), SelectFrame = new Color32(204, 167, 56, 255), NewRowBg = new Color32(38, 32, 54, 255), TakenColor = new Color32(196, 180, 112, 255),
        SegBg = new Color32(20, 20, 24, 255), SegOnBg = new Color32(35, 35, 43, 255), SegFrame = new Color32(64, 64, 72, 255), SegOnFrame = new Color32(181, 140, 255, 255),
        SegText = new Color32(185, 182, 198, 255), SegOnText = new Color32(240, 240, 244, 255),
        StarOn = new Color32(232, 222, 139, 255), StarOff = new Color32(90, 90, 100, 255), ChipText = new Color32(12, 12, 15, 255),
        MarkOff = new Color32(64, 64, 72, 255);

    private enum Mode { Rows, Explorer, Result }
    private enum Kind { Label, Kept, New, Bonus, Sub, Extra, Folder, Option, Exact }

    private sealed class Spec
    {
        internal Kind Kind;
        internal int Rarity = -1;
        internal string Text = string.Empty, Right = string.Empty;
        internal Color RightColor = TextColor; // right-hand text is white unless a row says otherwise (owner 2026-11-08)
        internal bool Removed, Dim, Selected, Highlight, Locked, Taken;
        internal int Star = -1; // -1 none, 0 off, 1 on
        internal string[] Segments;
        internal int Segment = -1;
        internal Action Press, PressStar;
        internal Action<int> PressSegment;
        internal string Button; // one inline button at the right (focus rows: "Choose")
    }

    private sealed class SegView { internal XUiV_Sprite Bg, Frame; internal XUiV_Label Text; internal XUiController Button; }

    private sealed class RowView
    {
        internal XUiController Root, Press, StarButton;
        internal XUiV_Sprite Bg, Frame, Chip, Star;
        internal XUiV_Label ChipText, Text, Right;
        internal readonly SegView[] Segs = new SegView[3];
        internal Spec Spec;
    }

    private readonly RowView[] rows = new RowView[RowPool];
    private XUiV_Label title, back, note, message, searchHint, crumb, more, tailorText, mark10Text, mark25Text, tailorNote, oddsText, oddsPct, resultLine;
    private XUiV_Sprite messageBg, messageFrame, sumRule, trackBar, trackFill, mark10, mark25;
    private readonly XUiV_Sprite[] odds = new XUiV_Sprite[4];
    private readonly XUiV_Sprite[] tabBg = new XUiV_Sprite[2], tabFrame = new XUiV_Sprite[2];
    private readonly XUiV_Label[] tabText = new XUiV_Label[2];
    private readonly XUiController[] tabButton = new XUiController[2];
    private XUiController background, frame, rule, backButton, crumbButton;
    private XUiC_TextInput search;
    private int width = 400, height = 682;
    private bool placed;
    private float nextPoll;

    // Slots and the player's choices.
    private string kitName = string.Empty, itemName = string.Empty, signature;
    private int itemType, quality;
    private EquipmentSlots slot;
    private RebirthArmorRolledBonus[] bonuses = new RebirthArmorRolledBonus[0];
    private float tailoring;
    private readonly List<RebirthArmorBonusFocus> adds = new List<RebirthArmorBonusFocus>();
    private readonly List<RebirthArmorBonusRowAction> actions = new List<RebirthArmorBonusRowAction>();
    private readonly List<RebirthArmorBonusFocus> rowFocus = new List<RebirthArmorBonusFocus>();
    private bool dirty = true;
    private bool toolPresent;
    private string lastPanelMessage = string.Empty;

    // Modes.
    private Mode mode = Mode.Rows;
    private bool targetReplace;
    private int targetIndex, level, tab, scroll;
    private string categoryId, familyId, searchText = string.Empty;
    private int seenResult = -1;
    private string resultSnapshot;
    private RebirthArmorRolledBonus[] resultBefore = new RebirthArmorRolledBonus[0], resultAfter = new RebirthArmorRolledBonus[0];

    // ---------- State for the centre visual (XUiC_RebirthBonusCentre) ----------

    internal int Version { get; private set; }
    internal bool IsShown => placed && ViewComponent != null && ViewComponent.IsVisible;
    internal bool IsReady => Ready;
    internal bool InResult => mode == Mode.Result;
    internal bool InExplorer => mode == Mode.Explorer;
    internal string KitName => kitName;
    internal string ItemName => itemName;
    internal int ItemType => itemType;
    internal int ItemQuality => quality;
    internal float Tailoring => tailoring;
    internal IReadOnlyList<RebirthArmorRolledBonus> Bonuses => bonuses;
    internal IReadOnlyList<RebirthArmorBonusRowAction> Actions => actions;
    internal IReadOnlyList<RebirthArmorBonusFocus> Adds => adds;
    internal IReadOnlyList<RebirthArmorBonusFocus> RowFocuses => rowFocus;
    internal IReadOnlyList<RebirthArmorRolledBonus> ResultBefore => resultBefore;
    internal IReadOnlyList<RebirthArmorRolledBonus> ResultAfter => resultAfter;
    internal string ResultCountText => ResultCount();
    internal string VerbText => Verb();
    internal string FocusName(RebirthArmorBonusFocus focus) => FocusText(focus);
    internal int SlotBonusCount => RebirthArmorBonusTree.Categories(slot).Sum(c => Picks(c, NoneTaken));

    // Counts show what the player can pick (owner 2026-11-08): one entry per subject in combination families, one per
    // exact bonus otherwise, minus those already on the item or used by another row. Not rarity versions.
    private static string CountText(int count) => count == 1 ? Localization.Get("xuiRebirthBonusWsCountOne") : string.Format(Localization.Get("xuiRebirthBonusWsCount"), count);

    private static readonly HashSet<string> NoneTaken = new HashSet<string>(StringComparer.Ordinal);
    private static int Picks(RebirthArmorBonusTree.Effect effect, HashSet<string> taken) => effect == null ? 0
        : effect.Subjects.Count > 0 ? effect.Subjects.Count(sj => !sj.Members.All(m => taken.Contains(m.Key))) : effect.Exacts.Count(x => !taken.Contains(x.Key));
    private static int Picks(RebirthArmorBonusTree.Category category, HashSet<string> taken) => category == null ? 0 : category.Effects.Sum(e => Picks(e, taken));
    internal string BlockReason => Ready && !RebirthArmorBonusKits.CheckRequirement(kitName, bonuses.Length, out string key) ? Localization.Get(key) : null;

    /// <summary>The odds, Tailoring track and result count moved to the centre visual (owner 2026-11-08, concepts A+B).</summary>
    private const bool SummaryInCentre = true;

    // ---------- Setup ----------

    public override void Init()
    {
        base.Init();
        title = View<XUiV_Label>("wsTitle");
        back = View<XUiV_Label>("wsBack");
        backButton = GetChildById("wsBackBtn");
        note = View<XUiV_Label>("wsNote");
        message = View<XUiV_Label>("wsMsg");
        messageBg = View<XUiV_Sprite>("wsMsgBg");
        messageFrame = View<XUiV_Sprite>("wsMsgFrame");
        searchHint = View<XUiV_Label>("wsSearchHint");
        crumb = View<XUiV_Label>("wsCrumb");
        more = View<XUiV_Label>("wsMore");
        sumRule = View<XUiV_Sprite>("wsSumRule");
        tailorText = View<XUiV_Label>("wsTailorText");
        trackBar = View<XUiV_Sprite>("wsTrackBar");
        trackFill = View<XUiV_Sprite>("wsTrackFill");
        mark10 = View<XUiV_Sprite>("wsMark10");
        mark25 = View<XUiV_Sprite>("wsMark25");
        mark10Text = View<XUiV_Label>("wsMark10Text");
        mark25Text = View<XUiV_Label>("wsMark25Text");
        tailorNote = View<XUiV_Label>("wsTailorNote");
        oddsText = View<XUiV_Label>("wsOddsText");
        oddsPct = View<XUiV_Label>("wsOddsPct");
        resultLine = View<XUiV_Label>("wsResult");
        for (int k = 0; k < 4; k++) odds[k] = View<XUiV_Sprite>("wsOdds" + k);
        background = GetChildById("wsBg");
        frame = GetChildById("wsFrame");
        rule = GetChildById("wsRule");
        search = GetChildById("wsSearch") as XUiC_TextInput;
        if (search != null)
            search.OnChangeHandler += (sender, text, fromCode) =>
            {
                if (fromCode || text == searchText) return;
                searchText = text ?? string.Empty;
                scroll = 0;
                Redraw();
            };
        if (backButton != null) backButton.OnPress += (sender, mouse) => Back();
        crumbButton = GetChildById("wsCrumbBtn");
        if (crumbButton != null) crumbButton.OnPress += (sender, mouse) => { if (mode == Mode.Explorer && level > 0) { GoUp(); scroll = 0; Redraw(); } };
        // Buttons hidden in the XML must still take clicks once shown (owner 2026-11-08: Back did nothing).
        Pressable(backButton);
        Pressable(crumbButton);
        for (int k = 0; k < 2; k++)
        {
            int index = k;
            tabBg[k] = View<XUiV_Sprite>("wsTabBg" + k);
            tabFrame[k] = View<XUiV_Sprite>("wsTabFrame" + k);
            tabText[k] = View<XUiV_Label>("wsTabText" + k);
            tabButton[k] = GetChildById("wsTabBtn" + k);
            Pressable(tabButton[k]);
            if (tabButton[k] != null) tabButton[k].OnPress += (sender, mouse) => { tab = index; scroll = 0; Redraw(); };
        }
        WireScroll(this);
        for (int i = 0; i < RowPool; i++)
        {
            var row = new RowView
            {
                Root = GetChildById("wsRow" + i),
                Bg = View<XUiV_Sprite>("wsRowBg" + i),
                Frame = View<XUiV_Sprite>("wsRowFrame" + i),
                Chip = View<XUiV_Sprite>("wsChipBg" + i),
                ChipText = View<XUiV_Label>("wsChipText" + i),
                Text = View<XUiV_Label>("wsRowText" + i),
                Right = View<XUiV_Label>("wsRowRight" + i),
                Star = View<XUiV_Sprite>("wsStar" + i),
                Press = GetChildById("wsRowPress" + i),
                StarButton = GetChildById("wsStarBtn" + i)
            };
            int index = i;
            WireScroll(row.Root);
            WireScroll(row.Press);
            WireScroll(row.StarButton);
            Pressable(row.Press);
            Pressable(row.StarButton);
            if (row.Press != null) row.Press.OnPress += (sender, mouse) => { var action = rows[index].Spec?.Press; if (action != null) { action(); Changed(); } };
            if (row.StarButton != null) row.StarButton.OnPress += (sender, mouse) => { var action = rows[index].Spec?.PressStar; if (action != null) { action(); Redraw(); } };
            for (int k = 0; k < 3; k++)
            {
                int segment = k;
                row.Segs[k] = new SegView
                {
                    Bg = View<XUiV_Sprite>("wsSegBg" + i + "_" + k),
                    Frame = View<XUiV_Sprite>("wsSegFrame" + i + "_" + k),
                    Text = View<XUiV_Label>("wsSegText" + i + "_" + k),
                    Button = GetChildById("wsSegBtn" + i + "_" + k)
                };
                WireScroll(row.Segs[k].Button);
                Pressable(row.Segs[k].Button);
                if (row.Segs[k].Button != null) row.Segs[k].Button.OnPress += (sender, mouse) => { var action = rows[index].Spec?.PressSegment; if (action != null) { action(segment); Changed(); } };
            }
            rows[i] = row;
        }
    }

    private T View<T>(string id) where T : XUiView => GetChildById(id)?.ViewComponent as T;

    private static void Pressable(XUiController controller)
    {
        if (controller?.ViewComponent != null) controller.ViewComponent.EventOnPress = true;
    }

    private void WireScroll(XUiController controller)
    {
        if (controller == null) return;
        if (controller.ViewComponent != null) controller.ViewComponent.EventOnScroll = true;
        controller.OnScroll += (sender, delta) => { scroll = Math.Max(0, scroll + (delta > 0 ? -2 : 2)); Redraw(); };
    }

    /// <summary>The layout service gives the workspace its column (Modifier Station only).</summary>
    internal void Place(int columnWidth, int columnHeight)
    {
        placed = columnWidth > 0;
        if (columnWidth == width && columnHeight == height) return;
        width = columnWidth;
        height = columnHeight;
        dirty = true;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        signature = null;
        mode = Mode.Rows;
        seenResult = Panel?.ResultVersion ?? -1;
        ClearSearch();
        dirty = true;
        nextPoll = 0f;
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        bool show = placed && RebirthScrapWindow.IsModifier && windowGroup?.isShowing == true;
        if (ViewComponent != null && ViewComponent.IsVisible != show) ViewComponent.IsVisible = show;
        if (!show || Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 0.2f;
        Poll();
        if (dirty) Redraw();
    }

    private void Redraw()
    {
        dirty = false;
        Render();
    }

    /// <summary>A new choice clears the last refusal and redraws at once.</summary>
    private void Changed()
    {
        Panel?.ClearMessage();
        lastPanelMessage = string.Empty;
        Redraw();
    }

    // ---------- State ----------

    private XUiC_RebirthModifierPanel Panel => Find<XUiC_RebirthModifierPanel>();

    private void Poll()
    {
        var grid = Find<XUiC_RebirthKitInputGrid>();
        // Read the slot controllers themselves: GetSlots() returns the grid's cached array, which can lag behind what
        // the slots show after a drag or shift-click (owner 2026-11-08: armor and kit in place, workspace still empty).
        var controllers = grid?.GetItemStackControllers();
        ItemStack armorStack = controllers != null && controllers.Length >= 2 ? controllers[0]?.ItemStack : null;
        ItemStack kitStack = controllers != null && controllers.Length >= 2 ? controllers[1]?.ItemStack : null;
        ItemValue item = armorStack != null && !armorStack.IsEmpty() ? armorStack.itemValue : null;
        ItemValue kit = kitStack != null && !kitStack.IsEmpty() && RebirthArmorBonusKits.IsKit(kitStack.itemValue)
            ? kitStack.itemValue : null;
        string newKit = kit?.ItemClass?.GetItemName() ?? string.Empty;
        int newType = item?.type ?? 0;
        int newQuality = item?.Quality ?? 0;
        string newSignature = RebirthArmorBonusKitRequest.BonusText(item);
        if (newKit != kitName || newType != itemType || newQuality != quality || newSignature != signature)
        {
            kitName = newKit;
            itemType = newType;
            signature = newSignature;
            quality = newQuality;
            itemName = item?.ItemClass?.GetLocalizedItemName() ?? string.Empty;
            bonuses = item == null ? new RebirthArmorRolledBonus[0] : RebirthArmorBonusStorage.Read(item);
            var oldSlot = slot;
            slot = EquipmentSlots.None;
            if (item != null) RebirthArmorBonusKits.TryGetSlot(item, out slot);
            if (slot != oldSlot) names.Clear();
            ResetChoices();
            Panel?.ClearMessage();
            if (mode == Mode.Explorer) { mode = Mode.Rows; ClearSearch(); }
            if (mode == Mode.Result && Ready && SlotKey() != resultSnapshot) mode = Mode.Rows;
            dirty = true;
            RebirthDebug.Log("armorbonuses", "[REBIRTH BonusWorkspace] slots: grid=" + (grid != null) + " count=" + (controllers?.Length ?? -1)
                + " item=" + (item == null ? "none" : item.ItemClass?.GetItemName() + " (" + slot + ")")
                + " kitSlot=" + (kitStack == null || kitStack.IsEmpty() ? "none" : kitStack.itemValue.ItemClass?.GetItemName())
                + " bonuses=" + bonuses.Length + " ready=" + Ready);
        }

        var panel = Panel;
        if (panel != null && panel.ResultVersion != seenResult)
        {
            seenResult = panel.ResultVersion;
            resultBefore = ParseBonuses(panel.LastBefore);
            resultAfter = ParseBonuses(panel.LastAfter);
            resultSnapshot = SlotKey();
            mode = Mode.Result;
            dirty = true;
        }

        float newTailoring = RebirthModifierStationService.TailoringOf(xui?.playerUI?.entityPlayer);
        if (Math.Abs(newTailoring - tailoring) >= 0.05f) { tailoring = newTailoring; dirty = true; }
        string panelMessage = panel?.Message ?? string.Empty;
        if (panelMessage != lastPanelMessage) { lastPanelMessage = panelMessage; dirty = true; }
        // The left panel clears whenever the tool, the armor or the kit is missing (owner 2026-11-08), result card included.
        bool tool = HasTool();
        if (tool != toolPresent) { toolPresent = tool; dirty = true; }
        if (mode != Mode.Rows && (!Ready || !toolPresent)) { mode = Mode.Rows; ClearSearch(); dirty = true; }
        UpdateActionButton(tool);
    }

    /// <summary>
    /// The action button shows only when the kit can be applied as chosen (owner 2026-11-08): Hammer and Pliers, armor, a
    /// fitting kit, a valid choice (something to reroll, replace or remove), enough Tailoring for every focus, and room in
    /// Output. The server still checks everything again.
    /// </summary>
    private void UpdateActionButton(bool toolPresent)
    {
        var button = windowGroup?.Controller?.GetChildById("btnEnchant")?.ViewComponent;
        if (button == null) return;
        bool can = CanApplyNow(toolPresent);
        if (button.IsVisible != can) button.IsVisible = can;
    }

    internal bool CanApply => CanApplyNow();

    private bool CanApplyNow(bool? observedTool = null) =>
        Ready && Blocker() == null && (observedTool ?? HasTool()) && OutputHasRoom() && FocusesAllowed();

    private bool FocusesAllowed()
    {
        foreach (var focus in adds) if (tailoring < focus.RequiredTailoring) return false;
        for (int i = 0; i < rowFocus.Count && i < actions.Count; i++)
            if (actions[i] == RebirthArmorBonusRowAction.Replace && tailoring < rowFocus[i].RequiredTailoring) return false;
        return true;
    }

    private const string RequiredTool = "FuriousRamsayHammerPliers";

    /// <summary>The Hammer and Pliers is in the station (stored data) or in the tool slot as drawn.</summary>
    internal bool HasTool()
    {
        int type = ItemClass.GetItem(RequiredTool).type;
        var station = (windowGroup?.Controller as XUiC_WorkstationWindowGroup)?.WorkstationData?.TileEntity;
        if (station?.Tools != null && station.Tools.Any(t => t != null && !t.IsEmpty() && t.itemValue.type == type)) return true;
        var grid = Find<XUiC_WorkstationToolGrid>();
        return grid != null && grid.GetItemStackControllers().Any(c => c?.ItemStack != null && !c.ItemStack.IsEmpty() && c.ItemStack.itemValue.type == type);
    }

    /// <summary>At least one empty Output slot, as drawn.</summary>
    internal bool OutputHasRoom()
    {
        var grid = Find<XUiC_WorkstationOutputGrid>();
        if (grid == null) return true;
        return grid.GetItemStackControllers().Any(c => c?.ItemStack == null || c.ItemStack.IsEmpty());
    }

    private string SlotKey() => kitName + "|" + itemType + "|" + signature + "#" + quality;

    private static RebirthArmorRolledBonus[] ParseBonuses(string raw)
    {
        var list = new List<RebirthArmorRolledBonus>();
        foreach (string part in (raw ?? string.Empty).Split(';'))
        {
            int colon = part.IndexOf(':');
            if (colon <= 0) continue;
            if (RebirthArmorBonusCatalog.TryGet(part.Substring(0, colon), out var definition) &&
                float.TryParse(part.Substring(colon + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                list.Add(new RebirthArmorRolledBonus { Definition = definition, Value = value });
        }
        return list.ToArray();
    }

    private void ResetChoices()
    {
        adds.Clear();
        actions.Clear();
        rowFocus.Clear();
        for (int i = 0; i < RebirthArmorBonusKits.AddRowsOf(kitName); i++) adds.Add(RebirthArmorBonusFocus.Any);
        for (int i = 0; i < bonuses.Length; i++) { actions.Add(RebirthArmorBonusRowAction.Keep); rowFocus.Add(RebirthArmorBonusFocus.Any); }
    }

    private bool Ready => itemType != 0 && kitName.Length > 0 && slot != EquipmentSlots.None;

    /// <summary>The request for the action button (null when the slots are not ready; the server then explains).</summary>
    internal RebirthArmorBonusKitRequest BuildRequest()
    {
        if (!Ready) return null;
        var request = new RebirthArmorBonusKitRequest { Kit = kitName, ItemBonuses = string.Join(",", bonuses.Select(b => b.Definition.Id)) };
        foreach (var focus in adds) request.Add(focus);
        bool rowsMatter = kitName == "kitRetuning" || (kitName == "kitBreakdown" && bonuses.Length > 1);
        if (rowsMatter) for (int i = 0; i < actions.Count; i++) request.Row(actions[i], rowFocus[i]);
        return request;
    }

    /// <summary>Why the kit cannot be used as chosen (spec 7.4), or null.</summary>
    private string Blocker()
    {
        if (!RebirthArmorBonusKits.CheckRequirement(kitName, bonuses.Length, out string failKey)) return Localization.Get(failKey);
        if (kitName == "kitRetuning" && actions.All(a => a == RebirthArmorBonusRowAction.Keep)) return Localization.Get("xuiEnchantRetuneNothing");
        if (kitName == "kitBreakdown" && bonuses.Length > 1 && actions.All(a => a == RebirthArmorBonusRowAction.Keep)) return Localization.Get("xuiEnchantBreakdownNothing");
        return null;
    }

    // ---------- Names ----------

    private readonly Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.Ordinal);

    private static string CategoryName(RebirthArmorBonusTree.Category category) => category == null ? string.Empty : Localization.Get(category.NameKey);

    /// <summary>"+{0} reload speed with shotguns" → "Reload speed with shotguns".</summary>
    private string ExactName(RebirthArmorBonusTree.Exact exact)
    {
        if (exact == null) return string.Empty;
        if (names.TryGetValue("x:" + exact.Key, out string cached)) return cached;
        string format = exact.Rows.Count > 0 ? Localization.Get("aeBonusFmt_" + exact.Rows[0].Id) : exact.Key;
        string text = System.Text.RegularExpressions.Regex.Replace(format.Replace("{0}%", string.Empty).Replace("{0}", string.Empty), @"\s+%?\s*", " ").Trim().TrimStart('+', '-', '%', ' ', '−').Trim();
        if (text.Length > 0) text = char.ToUpperInvariant(text[0]) + text.Substring(1);
        text = ShortSkillXp(text);
        names["x:" + exact.Key] = text;
        return text;
    }

    /// <summary>
    /// Skill experience bonuses, short (owner 2026-11-08): "More animal processing and tracking skill experience" becomes
    /// "Animal Processing and Tracking XP". Other bonus names are unchanged.
    /// </summary>
    private static string ShortSkillXp(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text ?? string.Empty, @"^(?:More\s+)?(.+?)\s+skill experience$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success) return text;
        var words = match.Groups[1].Value.Split(new[] { (char)32 }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            if (i > 0 && (word == "and" || word == "or" || word == "of" || word == "with")) continue;
            words[i] = char.ToUpperInvariant(word[0]) + word.Substring(1);
        }
        return string.Join(" ", words) + " XP";
    }

    /// <summary>A subject's name: its smallest bonus without "More" ("Dukes from containers", "Cooking XP").</summary>
    private string SubjectName(RebirthArmorBonusTree.Subject subject)
    {
        string text = ExactName(subject.Base);
        if (text.StartsWith("More ", StringComparison.OrdinalIgnoreCase) && text.Length > 5) text = char.ToUpperInvariant(text[5]) + text.Substring(6);
        return text;
    }

    /// <summary>Family display name: localization "aeFamily_ID" when present, else the words its bonuses share, else the id.</summary>
    private string EffectName(RebirthArmorBonusTree.Effect effect)
    {
        if (effect == null) return string.Empty;
        if (names.TryGetValue("e:" + effect.FamilyId, out string cached)) return cached;
        string key = "aeFamily_" + effect.FamilyId, text = Localization.Get(key);
        if (string.IsNullOrEmpty(text) || text == key)
        {
            var words = effect.Exacts.Select(x => ExactName(x).Split(' ')).ToList();
            int common = 0;
            if (words.Count > 1)
                while (words.All(w => w.Length > common) && words.All(w => string.Equals(w[common], words[0][common], StringComparison.OrdinalIgnoreCase))) common++;
            if (common >= 2 && words.Count > 1) text = string.Join(" ", words[0].Take(common)).TrimEnd(',', ' ');
            else if (words.Count == 1) text = ExactName(effect.Exacts[0]);
            else
            {
                string id = effect.FamilyId;
                foreach (var prefix in new[] { "Head", "Body", "Feet", "Hands" }) if (id.StartsWith(prefix, StringComparison.Ordinal) && id.Length > prefix.Length) { id = id.Substring(prefix.Length); break; }
                var spaced = new StringBuilder();
                for (int i = 0; i < id.Length; i++) { if (i > 0 && char.IsUpper(id[i])) spaced.Append(' '); spaced.Append(i == 0 ? id[i] : char.ToLowerInvariant(id[i])); }
                text = spaced.ToString();
            }
            if (text.EndsWith(" with", StringComparison.OrdinalIgnoreCase)) text = text.Substring(0, text.Length - 5);
            if (text.Length > 0) text = char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
        names["e:" + effect.FamilyId] = text;
        return text;
    }

    private RebirthArmorBonusTree.Effect EffectOf(string category, string family) =>
        RebirthArmorBonusTree.TryGetCategory(category, out var c) ? c.Effects.FirstOrDefault(e => e.FamilyId == family) : null;

    private static RebirthArmorBonusTree.Category CategoryOfExact(RebirthArmorBonusTree.Exact exact) =>
        exact == null || exact.Rows.Count == 0 ? null : RebirthArmorBonusTree.CategoryOf(exact.Rows[0]);

    private string FocusText(RebirthArmorBonusFocus focus)
    {
        switch (focus.Depth)
        {
            case RebirthArmorBonusFocusDepth.Category:
                return RebirthArmorBonusTree.TryGetCategory(focus.CategoryId, out var category) ? CategoryName(category) : focus.CategoryId;
            // Only the chosen name, no path (owner 2026-11-08: the path made it far too long).
            case RebirthArmorBonusFocusDepth.Effect:
                return EffectName(EffectOf(focus.CategoryId, focus.FamilyId));
            case RebirthArmorBonusFocusDepth.Exact:
                return RebirthArmorBonusTree.TryGetExact(focus.ExactKey, out var exact) ? ExactName(exact) : focus.ExactKey;
            case RebirthArmorBonusFocusDepth.Subject:
                return RebirthArmorBonusTree.TryGetSubject(focus.ExactKey, out var subject) ? SubjectName(subject) : focus.ExactKey;
            default: return Localization.Get("xuiRebirthBonusWsAny");
        }
    }

    private static string RarityLetters(IEnumerable<RebirthArmorBonusRarity> rarities)
    {
        var text = new StringBuilder();
        foreach (var r in rarities)
        {
            if (text.Length > 0) text.Append(' ');
            text.Append('[').Append(RarityHex[(int)r]).Append(']').Append(Localization.Get(RarityKeys[(int)r]).Substring(0, 1)).Append("[-]");
        }
        return text.ToString();
    }

    private string SlotName() => Localization.Get("xuiRebirthSlot" + slot);

    // ---------- Rows mode ----------

    private List<Spec> RowSpecs(out string titleText, out string noteText, out string blocker)
    {
        var list = new List<Spec>();
        titleText = Localization.Get("xuiEnchantInput");
        noteText = string.Empty;
        blocker = null;
        if (Ready && !toolPresent) { noteText = Localization.Get("xuiRebirthBonusWsNeedTool"); return list; }
        if (!Ready)
        {
            // Say what is missing (spec 7.4, "No item or no kit").
            noteText = Localization.Get(itemType != 0 && slot != EquipmentSlots.None ? "xuiRebirthBonusWsNeedKit" : kitName.Length > 0 ? "xuiRebirthBonusWsNeedArmor" : "xuiRebirthBonusWsEmpty");
            return list;
        }
        titleText = ItemClass.GetItem(kitName)?.ItemClass?.GetLocalizedItemName() ?? kitName;
        if (!RebirthArmorBonusKits.CheckRequirement(kitName, bonuses.Length, out string failKey))
        {
            // Show what is on the armor, so the reason is visible (owner 2026-11-08: a Common bonus is easy to miss on the slot).
            blocker = Localization.Get(failKey);
            list.Add(new Spec { Kind = Kind.Label, Text = Localization.Get("xuiRebirthBonusWsCurrent") });
            if (bonuses.Length == 0) list.Add(new Spec { Kind = Kind.Label, Text = Localization.Get("xuiRebirthBonusWsNone"), Dim = true });
            foreach (var bonus in bonuses) list.Add(KeptRow(bonus, string.Empty));
            return list;
        }
        switch (kitName)
        {
            case "kitRetuning": RetuningRows(list); break;
            case "kitBreakdown": BreakdownRows(list); break;
            default: AddingRows(list); break;
        }
        noteText = Localization.Get(KitNoteKey());
        blocker = Blocker();
        return list;
    }

    /// <summary>One line at the top of the rows saying what the kit does and what to press (owner 2026-11-08).</summary>
    private string KitNoteKey()
    {
        switch (kitName)
        {
            // Below Tailoring 10 the bonus can only be random, so the hint does not mention Choose (owner 2026-11-08).
            case "kitSurvivorUpgrade": return tailoring < 10f ? "xuiRebirthBonusWsKitSurvivorRandom" : "xuiRebirthBonusWsKitSurvivor";
            case "kitAdjustment": return tailoring < 10f ? "xuiRebirthBonusWsKitAdjustmentRandom" : "xuiRebirthBonusWsKitAdjustment";
            case "kitComprehensiveUpgrade": return tailoring < 10f ? "xuiRebirthBonusWsKitComprehensiveRandom" : "xuiRebirthBonusWsKitComprehensive";
            case "kitEliteReinforcement": return tailoring < 10f ? "xuiRebirthBonusWsKitEliteRandom" : "xuiRebirthBonusWsKitElite";
            case "kitRetuning": return "xuiRebirthBonusWsKitRetuning";
            default: return bonuses.Length == 1 ? "xuiRebirthBonusWsKitBreakdownOne" : "xuiRebirthBonusWsKitBreakdown";
        }
    }

    private string TargetName() => targetReplace ? Localization.Get("xuiRebirthBonusWsTheReplacement")
        : string.Format(Localization.Get("xuiRebirthBonusWsBonusN"), bonuses.Length + targetIndex + 1);

    /// <summary>Step-by-step help for the centre panel, for what is on screen (owner 2026-11-08).</summary>
    private string Guide()
    {
        if (mode == Mode.Result) return Localization.Get("xuiRebirthGuideResult");
        if (!Ready) return Localization.Get("xuiRebirthGuideEmpty");
        if (!RebirthArmorBonusKits.CheckRequirement(kitName, bonuses.Length, out string failKey))
            return string.Format(Localization.Get("xuiRebirthGuideBlocked"), Localization.Get(failKey));
        if (mode == Mode.Explorer) return string.Format(Localization.Get("xuiRebirthGuideExplorer"), TargetName().ToUpperInvariant());
        string value = tailoring.ToString("0.#", CultureInfo.InvariantCulture);
        string unlock = Localization.Get(tailoring >= 25f ? "xuiRebirthGuideUnlock25" : tailoring >= 10f ? "xuiRebirthGuideUnlock10" : "xuiRebirthGuideUnlock0");
        string kitTitle = (ItemClass.GetItem(kitName)?.ItemClass?.GetLocalizedItemName() ?? kitName).ToUpperInvariant() + ": " + Localization.Get(KitNoteKey()).Split(new[] { ". " }, StringSplitOptions.None)[0];
        switch (kitName)
        {
            case "kitRetuning": return string.Format(Localization.Get("xuiRebirthGuideRetune"), value, unlock);
            case "kitBreakdown": return Localization.Get("xuiRebirthGuideBreakdown");
            default: return string.Format(Localization.Get("xuiRebirthGuideAdd"), kitTitle, Verb(), value, unlock);
        }
    }

    private Spec KeptRow(RebirthArmorRolledBonus bonus, string right) =>
        new Spec { Kind = Kind.Kept, Rarity = (int)bonus.Definition.Rarity, Text = RebirthArmorBonusText.Plain(bonus), Right = right };

    private void AddingRows(List<Spec> list)
    {
        list.Add(new Spec { Kind = Kind.Label, Text = Localization.Get("xuiRebirthBonusWsCurrent") });
        if (bonuses.Length == 0) list.Add(new Spec { Kind = Kind.Label, Text = Localization.Get("xuiRebirthBonusWsNone"), Dim = true });
        foreach (var bonus in bonuses) list.Add(KeptRow(bonus, Localization.Get("xuiRebirthBonusWsKept")));
        list.Add(new Spec { Kind = Kind.Label, Text = Localization.Get(adds.Count > 1 ? "xuiRebirthBonusWsNewMany" : "xuiRebirthBonusWsNewOne") });
        for (int i = 0; i < adds.Count; i++)
        {
            int row = i;
            list.Add(FocusRow(Kind.New, string.Format(Localization.Get("xuiRebirthBonusWsBonusN"), bonuses.Length + i + 1), adds[i], () => OpenExplorer(false, row)));
        }
        if (RebirthArmorBonusKits.LevelOf(kitName) == 3)
            list.Add(new Spec { Kind = Kind.Extra, Text = Localization.Get("xuiRebirthBonusWsExtra"), Right = Localization.Get("xuiRebirthBonusWsExtraRandom") });
    }

    private Spec FocusRow(Kind kind, string text, RebirthArmorBonusFocus focus, Action open)
    {
        bool locked = tailoring < 10f;
        return new Spec
        {
            Kind = kind,
            Text = text,
            // What the bonus will be, and a button to choose it (owner 2026-11-08: a plain link was not seen as clickable).
            Right = focus.Depth == RebirthArmorBonusFocusDepth.Any ? Localization.Get(locked ? "xuiRebirthBonusWsRandomLocked" : "xuiRebirthBonusWsRandom") : FocusText(focus),
            RightColor = focus.Depth == RebirthArmorBonusFocusDepth.Any ? TextColor : LinkColor,
            // Below Tailoring 10 nothing can be chosen: no button, no click, "Random bonus" at the right (owner 2026-11-08).
            Button = locked ? null : Localization.Get("xuiRebirthBonusWsChooseButton"),
            PressSegment = locked ? (Action<int>)null : segment => open(),
            Press = locked ? null : open
        };
    }

    private void RetuningRows(List<Spec> list)
    {
        list.Add(new Spec { Kind = Kind.Label, Text = string.Format(Localization.Get("xuiRebirthBonusWsOnItem"), itemName) });
        var segmentNames = new[] { Localization.Get("xuiRebirthBonusWsKeep"), Localization.Get("xuiRebirthBonusWsReroll"), Localization.Get("xuiRebirthBonusWsReplace") };
        for (int i = 0; i < bonuses.Length; i++)
        {
            int row = i;
            list.Add(new Spec
            {
                Kind = Kind.Bonus,
                Rarity = (int)bonuses[i].Definition.Rarity,
                Text = RebirthArmorBonusText.Plain(bonuses[i]),
                Segments = segmentNames,
                Segment = (int)actions[i],
                PressSegment = segment =>
                {
                    actions[row] = (RebirthArmorBonusRowAction)segment;
                    if (segment != (int)RebirthArmorBonusRowAction.Replace) rowFocus[row] = RebirthArmorBonusFocus.Any;
                    DropClashingFocuses();
                }
            });
            if (actions[i] == RebirthArmorBonusRowAction.Replace)
                list.Add(FocusRow(Kind.Sub, Localization.Get("xuiRebirthBonusWsNewSub"), rowFocus[i], () => OpenExplorer(true, row)));
        }
    }

    private void BreakdownRows(List<Spec> list)
    {
        bool single = bonuses.Length == 1;
        bool allRemoved = actions.All(a => a == RebirthArmorBonusRowAction.Remove);
        list.Add(new Spec
        {
            Kind = Kind.Label,
            Text = string.Format(Localization.Get("xuiRebirthBonusWsOnItem"), itemName),
            Right = single ? string.Empty : "[u]" + Localization.Get(allRemoved ? "xuiRebirthBonusWsKeepAll" : "xuiRebirthBonusWsRemoveAll") + "[/u]",
            RightColor = LinkColor,
            Press = single ? (Action)null : () =>
            {
                for (int i = 0; i < actions.Count; i++) actions[i] = allRemoved ? RebirthArmorBonusRowAction.Keep : RebirthArmorBonusRowAction.Remove;
            }
        });
        for (int i = 0; i < bonuses.Length; i++)
        {
            int row = i;
            bool removed = single || actions[i] == RebirthArmorBonusRowAction.Remove;
            list.Add(new Spec
            {
                Kind = Kind.Bonus,
                Rarity = (int)bonuses[i].Definition.Rarity,
                Text = RebirthArmorBonusText.Plain(bonuses[i]),
                Removed = removed,
                Right = Localization.Get(single ? "xuiRebirthBonusWsWillRemove" : removed ? "xuiRebirthBonusWsRemove" : "xuiRebirthBonusWsKeep"),
                RightColor = removed ? RemoveText : TextColor,
                Press = single ? (Action)null : () =>
                {
                    actions[row] = actions[row] == RebirthArmorBonusRowAction.Remove ? RebirthArmorBonusRowAction.Keep : RebirthArmorBonusRowAction.Remove;
                }
            });
        }
    }

    // Rule (owner 2026-11-08): only the same bonus cannot appear twice on an item. Different bonuses of one family
    // (Construction XP and Batons XP) may sit together. Everything below works on exact bonus keys.

    /// <summary>When a choice repeats another row's exact bonus, that other row returns to Random.</summary>
    private void DropClashingFocuses()
    {
        var taken = new HashSet<string>(StayingKeys(), StringComparer.Ordinal);
        for (int i = 0; i < adds.Count; i++)
            if (adds[i].Depth == RebirthArmorBonusFocusDepth.Exact && !taken.Add(adds[i].ExactKey)) adds[i] = RebirthArmorBonusFocus.Any;
        for (int i = 0; i < rowFocus.Count; i++)
            if (actions[i] == RebirthArmorBonusRowAction.Replace && rowFocus[i].Depth == RebirthArmorBonusFocusDepth.Exact && !taken.Add(rowFocus[i].ExactKey))
                rowFocus[i] = RebirthArmorBonusFocus.Any;
    }

    /// <summary>Exact bonuses that stay on the item (not counting the focus rows).</summary>
    private IEnumerable<string> StayingKeys()
    {
        for (int i = 0; i < bonuses.Length; i++)
            if (kitName != "kitRetuning" || actions[i] != RebirthArmorBonusRowAction.Replace) yield return RebirthArmorBonusTree.ExactKey(bonuses[i].Definition.Id);
    }

    /// <summary>Why each blocked exact bonus is blocked: on the item, or picked for another row.</summary>
    private readonly Dictionary<string, string> takenBy = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Exact bonuses the explorer's target row may not pick.</summary>
    private HashSet<string> TakenForTarget()
    {
        takenBy.Clear();
        foreach (string key in StayingKeys()) takenBy[key] = Localization.Get("xuiRebirthBonusWsTaken");
        for (int i = 0; i < adds.Count; i++)
            if ((targetReplace || i != targetIndex) && adds[i].Depth == RebirthArmorBonusFocusDepth.Exact && !takenBy.ContainsKey(adds[i].ExactKey))
                takenBy[adds[i].ExactKey] = string.Format(Localization.Get("xuiRebirthBonusWsUsedBy"), string.Format(Localization.Get("xuiRebirthBonusWsBonusN"), bonuses.Length + i + 1));
        for (int i = 0; i < rowFocus.Count; i++)
            if (actions.Count > i && actions[i] == RebirthArmorBonusRowAction.Replace && (!targetReplace || i != targetIndex)
                && rowFocus[i].Depth == RebirthArmorBonusFocusDepth.Exact && !takenBy.ContainsKey(rowFocus[i].ExactKey))
                takenBy[rowFocus[i].ExactKey] = Localization.Get("xuiRebirthBonusWsUsedByReplacement");
        return new HashSet<string>(takenBy.Keys, StringComparer.Ordinal);
    }

    private static bool EffectBlocked(RebirthArmorBonusTree.Effect effect, HashSet<string> taken) => effect != null && effect.Exacts.All(x => taken.Contains(x.Key));
    private static bool CategoryBlocked(RebirthArmorBonusTree.Category category, HashSet<string> taken) => category != null && category.Effects.All(e => EffectBlocked(e, taken));
    private static IEnumerable<string> KeysOf(RebirthArmorBonusTree.Effect effect) => effect == null ? new string[0] : effect.Exacts.Select(x => x.Key);
    private static IEnumerable<string> KeysOf(RebirthArmorBonusTree.Category category) => category == null ? new string[0] : category.Effects.SelectMany(e => KeysOf(e));

    private IEnumerable<string> KeysOf(RebirthArmorBonusFocus focus)
    {
        switch (focus.Depth)
        {
            case RebirthArmorBonusFocusDepth.Category: return RebirthArmorBonusTree.TryGetCategory(focus.CategoryId, out var c) ? KeysOf(c) : new string[0];
            case RebirthArmorBonusFocusDepth.Effect: return KeysOf(EffectOf(focus.CategoryId, focus.FamilyId));
            case RebirthArmorBonusFocusDepth.Exact: return new[] { focus.ExactKey };
            case RebirthArmorBonusFocusDepth.Subject: return RebirthArmorBonusTree.TryGetSubject(focus.ExactKey, out var subject) ? subject.Members.Select(m => m.Key).ToArray() : new string[0];
            default: return new string[0];
        }
    }

    /// <summary>The reason shown on a blocked row: "Already on this item" first, else which row picked it.</summary>
    private string TakenText(IEnumerable<string> keys)
    {
        string reason = null;
        foreach (string key in keys)
        {
            if (key == null || !takenBy.TryGetValue(key, out string text)) continue;
            if (text == Localization.Get("xuiRebirthBonusWsTaken")) return text;
            if (reason == null) reason = text;
        }
        return reason ?? Localization.Get("xuiRebirthBonusWsTaken");
    }

    private string Verb()
    {
        if (!Ready) return Localization.Get("xuiEnchantAction");
        switch (kitName)
        {
            case "kitComprehensiveUpgrade": return Localization.Get("xuiRebirthBonusWsVerbAddMany");
            case "kitRetuning": return Localization.Get("xuiRebirthBonusWsVerbRetune");
            case "kitBreakdown": return Localization.Get(bonuses.Length > 1 ? "xuiRebirthBonusWsVerbRemoveMany" : "xuiRebirthBonusWsVerbRemove");
            default: return Localization.Get("xuiRebirthBonusWsVerbAdd");
        }
    }

    private string ResultCount()
    {
        string Count(int n) => n == 1 ? Localization.Get("xuiRebirthBonusWsResultOne") : string.Format(Localization.Get("xuiRebirthBonusWsResultN"), n);
        switch (kitName)
        {
            case "kitSurvivorUpgrade": return Count(1);
            case "kitAdjustment": return Count(2);
            case "kitRetuning": return Count(bonuses.Length);
            case "kitComprehensiveUpgrade":
            case "kitEliteReinforcement": return string.Format(Localization.Get("xuiRebirthBonusWsResultExtra"), 3, 4);
            default:
            {
                int kept = bonuses.Length == 1 ? 0 : actions.Count(a => a == RebirthArmorBonusRowAction.Keep);
                return kept == 0 ? Localization.Get("xuiRebirthBonusWsResultNone") : string.Format(Localization.Get("xuiRebirthBonusWsResultKept"), kept);
            }
        }
    }

    // ---------- Explorer mode ----------

    private RebirthArmorBonusFocus TargetFocus => targetReplace ? rowFocus[targetIndex] : adds[targetIndex];

    private void OpenExplorer(bool replace, int index)
    {
        targetReplace = replace;
        targetIndex = index;
        // Always open at the top with the current choice highlighted, so every row starts from the same list
        // (owner 2026-11-08: opening deep in the tree for a row that already had a choice was confusing).
        level = 0;
        categoryId = null;
        familyId = null;
        tab = 0;
        scroll = 0;
        ClearSearch();
        mode = Mode.Explorer;
    }

    /// <summary>A category with a single kind of bonus opens straight on its bonuses (owner 2026-11-08: no extra step).</summary>
    private void OpenCategory(RebirthArmorBonusTree.Category category)
    {
        categoryId = category.Id;
        familyId = null;
        level = 1;
        if (tailoring >= 25f && category.Effects.Count == 1) { level = 2; familyId = category.Effects[0].FamilyId; }
        scroll = 0;
    }

    /// <summary>One level up; a skipped single-effect level is skipped on the way back too.</summary>
    private void GoUp()
    {
        if (level == 2 && RebirthArmorBonusTree.TryGetCategory(categoryId, out var category) && category.Effects.Count == 1) level = 0;
        else level--;
        if (level <= 1) familyId = null;
        if (level == 0) categoryId = null;
    }

    private void Choose(RebirthArmorBonusFocus focus)
    {
        if (targetReplace) rowFocus[targetIndex] = focus; else adds[targetIndex] = focus;
        mode = Mode.Rows;
        ClearSearch();
        Changed();
    }

    private void ClearSearch()
    {
        searchText = string.Empty;
        if (search != null && !string.IsNullOrEmpty(search.Text)) search.Text = string.Empty;
    }

    private void Back()
    {
        if (mode == Mode.Explorer)
        {
            if (searchText.Length > 0) ClearSearch();
            else if (tab == 1) tab = 0;
            else if (level > 0) GoUp();
            else mode = Mode.Rows;
        }
        else if (mode == Mode.Result) mode = Mode.Rows;
        scroll = 0;
        Redraw();
    }

    private bool Allowed(RebirthArmorBonusFocus focus) => tailoring >= focus.RequiredTailoring;

    private string LockText(int need) => Localization.Get(need >= 25 ? "xuiRebirthBonusWsNeeds25" : "xuiRebirthBonusWsNeeds10");

    private Spec StarFor(Spec spec, RebirthArmorBonusFocus focus)
    {
        string key = focus.ToString();
        spec.Star = Favourites.Contains(key) ? 1 : 0;
        spec.PressStar = () => { if (!Favourites.Remove(key)) Favourites.Add(key); SaveFavourites(); };
        return spec;
    }

    /// <summary>A selectable row for a focus, with its lock, family block, selection and star.</summary>
    private Spec FocusOption(Kind kind, string text, RebirthArmorBonusFocus focus, string right, bool blocked, bool star)
    {
        bool allowed = Allowed(focus);
        var spec = new Spec
        {
            Kind = kind,
            Text = text,
            Taken = blocked,
            Right = blocked ? TakenText(KeysOf(focus)) : !allowed ? LockText(focus.RequiredTailoring) : right,
            Dim = blocked || !allowed,
            Locked = !allowed,
            Selected = TargetFocus.ToString() == focus.ToString(),
            Press = blocked || !allowed ? (Action)null : () => Choose(focus)
        };
        return star ? StarFor(spec, focus) : spec;
    }

    private List<Spec> ExplorerSpecs(out string crumbText, out string noteText)
    {
        var list = new List<Spec>();
        noteText = string.Empty;
        var taken = TakenForTarget();
        if (searchText.Trim().Length > 0) { crumbText = string.Empty; SearchRows(list, taken); return list; }
        if (tab == 1) { crumbText = string.Empty; FavouriteRows(list, taken); return list; }

        var categories = RebirthArmorBonusTree.Categories(slot);
        RebirthArmorBonusTree.TryGetCategory(categoryId, out var category);
        var effect = category?.Effects.FirstOrDefault(e => e.FamilyId == familyId);
        if (level >= 1 && category == null) level = 0;
        if (level >= 2 && effect == null) level = 1;
        bool singleEffect = category != null && category.Effects.Count == 1;
        crumbText = SlotName() + (level >= 1 ? " > " + CategoryName(category) : string.Empty) + (level >= 2 && !singleEffect ? " > " + EffectName(effect) : string.Empty);

        if (level == 0)
        {
            // No "Any" row where specific options are listed (owner 2026-11-08); a row that has a pick can go back to Random.
            if (TargetFocus.Depth != RebirthArmorBonusFocusDepth.Any)
                list.Add(new Spec { Kind = Kind.Option, Text = Localization.Get("xuiRebirthBonusWsSetRandom"), Press = () => Choose(RebirthArmorBonusFocus.Any) });
            foreach (var c in categories)
            {
                var cat = c;
                bool blocked = CategoryBlocked(cat, taken);
                bool open = tailoring >= 10f && !blocked;
                var spec = new Spec
                {
                    Kind = Kind.Folder,
                    Text = CategoryName(cat),
                    Taken = blocked,
            Right = blocked ? TakenText(KeysOf(cat)) : open ? CountText(Picks(cat, taken)) + " >" : LockText(10),
                    Dim = !open,
                    Locked = tailoring < 10f,
                    Selected = TargetFocus.Depth != RebirthArmorBonusFocusDepth.Any && FocusCategory(TargetFocus) == cat.Id,
                    Press = open ? () => OpenCategory(cat) : (Action)null
                };
                // A category is a pick of its own only below Tailoring 25; from 25 its specific bonuses are listed instead.
                list.Add(tailoring < 25f ? StarFor(spec, RebirthArmorBonusFocus.ForCategory(cat.Id)) : spec);
            }
            return list;
        }

        if (level == 1)
        {
            // Below Tailoring 25 only the category itself can be picked; nothing locked is listed (owner 2026-11-08).
            if (tailoring < 25f)
            {
                list.Add(FocusOption(Kind.Option, string.Format(Localization.Get("xuiRebirthBonusWsAnyIn"), CategoryName(category)),
                    RebirthArmorBonusFocus.ForCategory(category.Id), CountText(Picks(category, taken)), CategoryBlocked(category, taken), true));
                return list;
            }
            foreach (var e in category.Effects)
            {
                var eff = e;
                bool blocked = EffectBlocked(eff, taken);
                var spec = new Spec
                {
                    Kind = Kind.Folder,
                    Text = EffectName(eff),
                    Taken = blocked,
            Right = blocked ? TakenText(KeysOf(eff)) : CountText(Picks(eff, taken)) + " >",
                    Dim = blocked,
                    Selected = FocusFamily(TargetFocus) == eff.FamilyId && FocusCategory(TargetFocus) == category.Id,
                    Press = blocked ? (Action)null : () => { level = 2; familyId = eff.FamilyId; scroll = 0; }
                };
                list.Add(spec); // opens its specific bonuses; not a pick of its own
            }
            return list;
        }

        // Combination families list what a bonus affects ("Dukes from containers"); the rarity roll picks the version,
        // which always includes it. Rarity is never shown here: it is rolled when the button is pressed (owner 2026-11-08).
        if (effect.Subjects.Count > 0)
            foreach (var subject in effect.Subjects)
                list.Add(FocusOption(Kind.Exact, SubjectName(subject), RebirthArmorBonusFocus.ForSubject(subject.Key), string.Empty, subject.Members.All(m => taken.Contains(m.Key)), true));
        else
            foreach (var x in effect.Exacts)
                list.Add(FocusOption(Kind.Exact, ExactName(x), RebirthArmorBonusFocus.ForExact(x.Key), string.Empty, taken.Contains(x.Key), true));
        return list;
    }

    private static string FocusCategory(RebirthArmorBonusFocus focus)
    {
        if (focus.Depth == RebirthArmorBonusFocusDepth.Exact) return RebirthArmorBonusTree.TryGetExact(focus.ExactKey, out var x) ? CategoryOfExact(x)?.Id : null;
        if (focus.Depth == RebirthArmorBonusFocusDepth.Subject) return RebirthArmorBonusTree.TryGetSubject(focus.ExactKey, out var subject) ? subject.Category?.Id : null;
        return focus.CategoryId;
    }

    private static string FocusFamily(RebirthArmorBonusFocus focus) => RebirthArmorBonusKits.FamilyOf(focus);

    /// <summary>Search over category, effect and bonus names; results show their full path (mockup 7).</summary>
    private void SearchRows(List<Spec> list, HashSet<string> taken)
    {
        var words = searchText.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var found = new List<Spec>();
        foreach (var category in RebirthArmorBonusTree.Categories(slot))
        {
            string catName = CategoryName(category);
            if (tailoring < 25f && words.All(w => catName.ToLowerInvariant().Contains(w))) // from 25 only specific bonuses are picks
                found.Add(FocusOption(Kind.Folder, string.Format(Localization.Get("xuiRebirthBonusWsCategoryFav"), catName), RebirthArmorBonusFocus.ForCategory(category.Id),
                    CountText(Picks(category, taken)), CategoryBlocked(category, taken), true));
            foreach (var effect in category.Effects)
            {
                if (effect.Subjects.Count > 0)
                {
                    foreach (var subject in effect.Subjects)
                    {
                        string path = catName + " > " + EffectName(effect) + " > " + SubjectName(subject);
                        if (!words.All(w => path.ToLowerInvariant().Contains(w))) continue;
                        found.Add(FocusOption(Kind.Exact, path, RebirthArmorBonusFocus.ForSubject(subject.Key), string.Empty, subject.Members.All(m => taken.Contains(m.Key)), true));
                    }
                    continue;
                }
                foreach (var exact in effect.Exacts)
                {
                    string path = catName + " > " + EffectName(effect) + " > " + ExactName(exact);
                    if (!words.All(w => path.ToLowerInvariant().Contains(w))) continue;
                    found.Add(FocusOption(Kind.Exact, path, RebirthArmorBonusFocus.ForExact(exact.Key), string.Empty, taken.Contains(exact.Key), true));
                }
            }
        }
        found.RemoveAll(sp => sp.Locked);
        list.Add(new Spec { Kind = Kind.Label, Text = found.Count == 0 ? Localization.Get("xuiRebirthBonusWsNoResults") : string.Format(Localization.Get("xuiRebirthBonusWsResults"), found.Count), Dim = found.Count == 0 });
        list.AddRange(found);
    }

    private void FavouriteRows(List<Spec> list, HashSet<string> taken)
    {
        int before = list.Count;
        foreach (string key in Favourites.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (!RebirthArmorBonusFocus.TryParse(key, out var focus) || RebirthArmorBonusTree.Pool(slot, focus).Count == 0) continue;
            string family = FocusFamily(focus);
            bool blocked;
            string right;
            if (focus.Depth == RebirthArmorBonusFocusDepth.Category)
            {
                RebirthArmorBonusTree.TryGetCategory(focus.CategoryId, out var c);
                blocked = CategoryBlocked(c, taken);
                right = CountText(Picks(c, taken));
                list.Add(FocusOption(Kind.Folder, string.Format(Localization.Get("xuiRebirthBonusWsCategoryFav"), CategoryName(c)), focus, right, blocked, true));
                continue;
            }
            blocked = KeysOf(focus).Any() && KeysOf(focus).All(k => taken.Contains(k));
            right = focus.Depth >= RebirthArmorBonusFocusDepth.Exact
                ? string.Empty
                : CountText(Picks(EffectOf(focus.CategoryId, focus.FamilyId), taken));
            string path = focus.Depth == RebirthArmorBonusFocusDepth.Exact && RebirthArmorBonusTree.TryGetExact(focus.ExactKey, out var exact)
                ? CategoryName(CategoryOfExact(exact)) + " > " + FocusText(focus)
                : focus.Depth == RebirthArmorBonusFocusDepth.Subject && RebirthArmorBonusTree.TryGetSubject(focus.ExactKey, out var favSubject)
                ? CategoryName(favSubject.Category) + " > " + FocusText(focus)
                : FocusText(focus);
            list.Add(FocusOption(Kind.Exact, path, focus, right, blocked, true));
        }
        if (list.Count == before) list.Add(new Spec { Kind = Kind.Label, Text = Localization.Get("xuiRebirthBonusWsNoFav"), Dim = true });
    }

    // ---------- Favourites (per character, this save's local folder) ----------

    private static HashSet<string> favourites;
    private static string favouritesPath;

    private static HashSet<string> Favourites
    {
        get
        {
            string path = FavouritesPath();
            if (favourites != null && path == favouritesPath) return favourites;
            favouritesPath = path;
            favourites = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                if (path != null && File.Exists(path))
                    foreach (string line in File.ReadAllLines(path))
                        if (line.Length > 0 && line.Length <= 160) favourites.Add(line.Trim());
            }
            catch (Exception e) { Log.Warning("[REBIRTH BonusWorkspace] Could not read favourites: " + e.Message); }
            return favourites;
        }
    }

    private static string FavouritesPath()
    {
        try
        {
            string dir = GameIO.GetSaveGameLocalDir();
            return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, "rebirth", "bonus_favourites.txt");
        }
        catch (Exception) { return null; }
    }

    private static void SaveFavourites()
    {
        try
        {
            string path = FavouritesPath();
            if (path == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, Favourites.OrderBy(k => k, StringComparer.Ordinal));
        }
        catch (Exception e) { Log.Warning("[REBIRTH BonusWorkspace] Could not save favourites: " + e.Message); }
    }

    // ---------- Result mode ----------

    private List<Spec> ResultSpecs()
    {
        var list = new List<Spec> { new Spec { Kind = Kind.Label, Text = Localization.Get("xuiRebirthBonusWsBefore") } };
        if (resultBefore.Length == 0) list.Add(new Spec { Kind = Kind.Label, Text = Localization.Get("xuiRebirthBonusWsNone"), Dim = true });
        foreach (var bonus in resultBefore) list.Add(KeptRow(bonus, string.Empty));
        list.Add(new Spec { Kind = Kind.Label, Text = Localization.Get("xuiRebirthBonusWsAfter") });
        if (resultAfter.Length == 0) list.Add(new Spec { Kind = Kind.Label, Text = Localization.Get("xuiRebirthBonusWsResultNone"), Dim = true });
        var old = new HashSet<string>(resultBefore.Select(b => b.Definition.Id + ":" + b.Value.ToString("0.####", CultureInfo.InvariantCulture)), StringComparer.Ordinal);
        foreach (var bonus in resultAfter)
        {
            bool isNew = !old.Contains(bonus.Definition.Id + ":" + bonus.Value.ToString("0.####", CultureInfo.InvariantCulture));
            var spec = KeptRow(bonus, isNew ? Localization.Get("xuiRebirthBonusWsNew") : string.Empty);
            spec.Highlight = isNew;
            spec.RightColor = OkColor;
            list.Add(spec);
        }
        return list;
    }

    // ---------- Drawing ----------

    private void Render()
    {
        Version++;
        int inner = width - Pad * 2;
        Rect(background, 0, 0, width, height);
        Rect(frame, 0, 0, width, height);
        Rect(rule, 0, -38, width, 2);

        string titleText, noteText = string.Empty, crumbText = string.Empty, blocker = null;
        List<Spec> specs;
        if (mode == Mode.Explorer && (!Ready || (targetReplace ? targetIndex >= rowFocus.Count : targetIndex >= adds.Count))) mode = Mode.Rows;
        if (mode == Mode.Explorer)
        {
            titleText = Localization.Get("xuiRebirthBonusWsFocusTitle");
            specs = ExplorerSpecs(out crumbText, out noteText);
            specs.RemoveAll(sp => sp.Locked); // only what the player can pick is listed (owner 2026-11-08)
            if (noteText.Length == 0) noteText = string.Format(Localization.Get("xuiRebirthBonusWsExplorerNote"), TargetName());
        }
        else if (mode == Mode.Result) { titleText = Localization.Get("xuiRebirthBonusWsResultTitle"); specs = ResultSpecs(); }
        else specs = RowSpecs(out titleText, out noteText, out blocker);

        Rect(title, 42, -8, width - 130, 28);
        Text(title, titleText, LinkColor);
        bool showBack = mode != Mode.Rows;
        Visible(back, showBack);
        Visible(backButton?.ViewComponent, showBack);
        if (showBack)
        {
            Rect(back, width - 90, -10, 80, 24);
            Rect(backButton?.ViewComponent, width - 90, -6, 82, 30);
            Text(back, "[u]" + Localization.Get("xuiRebirthBonusWsBackLink") + "[/u]", LinkColor);
        }

        int y = -50;
        bool explorer = mode == Mode.Explorer;
        Visible(search?.ViewComponent, explorer);
        Visible(searchHint, explorer && searchText.Length == 0);
        for (int k = 0; k < 2; k++)
        {
            Visible(tabBg[k], explorer); Visible(tabFrame[k], explorer); Visible(tabText[k], explorer); Visible(tabButton[k]?.ViewComponent, explorer);
        }
        Visible(crumb, explorer && crumbText.Length > 0);
        bool crumbUp = explorer && crumbText.Length > 0 && level > 0 && searchText.Length == 0 && tab == 0;
        Visible(crumbButton?.ViewComponent, crumbUp);
        if (explorer)
        {
            Rect(search?.ViewComponent, Pad, y + 2, inner, 34);
            Rect(searchHint, Pad + 10, y - 7, inner - 60, 22);
            y -= 42;
            for (int k = 0; k < 2; k++)
            {
                int x = Pad + k * 136, w = 132;
                bool on = tab == k && searchText.Length == 0;
                Rect(tabBg[k], x, y, w, 28); Rect(tabFrame[k], x, y, w, 28); Rect(tabText[k], x, y - 4, w, 22); Rect(tabButton[k]?.ViewComponent, x, y, w, 28);
                Tint(tabBg[k], on ? SelectBg : SegBg);
                Tint(tabFrame[k], on ? SelectFrame : SegFrame);
                Text(tabText[k], Localization.Get(k == 0 ? "xuiRebirthBonusWsBrowse" : "xuiRebirthBonusWsFavourites"), on ? SegOnText : SegText);
            }
            y -= 34;
            if (crumbText.Length > 0)
            {
                // The path goes up one level when clicked; the "<" shows it can (owner 2026-11-08).
                Rect(crumb, Pad + 2, y, inner - 4, 22);
                Text(crumb, crumbUp ? "[u]< " + crumbText + "[/u]" : crumbText, crumbUp ? LinkColor : MutedColor);
                if (crumbUp) Rect(crumbButton?.ViewComponent, Pad, y + 2, inner, 24);
                y -= 26;
            }
        }

        bool showNote = noteText.Length > 0;
        Visible(note, showNote);
        if (showNote)
        {
            int perLine = Math.Max(30, (inner - 4) / 8); // about 8 px per character at these sizes
            int noteLines = Math.Max(1, Math.Min(4, (noteText.Length + perLine - 1) / perLine));
            int noteHeight = noteLines * (explorer ? 20 : 22) + 2;
            Rect(note, Pad + 2, y, inner - 4, noteHeight);
            Text(note, noteText, MutedColor);
            if (note.FontSize != (explorer ? 16 : 18)) note.FontSize = explorer ? 16 : 18;
            y -= noteHeight + 4;
        }

        // Message: before pressing, why the kit cannot apply; after pressing, the refusal. Space is kept for it above the summary.
        string shownMessage = mode == Mode.Rows ? blocker ?? (lastPanelMessage.Length > 0 ? lastPanelMessage : null) : null;
        bool showMessage = !string.IsNullOrEmpty(shownMessage);
        int summaryTop = explorer ? -height : RenderSummary(inner);
        int listBottom = explorer ? -(height - 30) : summaryTop + 8 + (showMessage ? 66 : 0);

        // Rows (scrolling when they do not fit).
        int maxScroll = Math.Max(0, specs.Count - 1);
        if (scroll > maxScroll) scroll = maxScroll;
        int first = specs.Count > 0 && FitsAll(specs, y, listBottom) ? 0 : scroll;
        if (first == 0) scroll = 0;
        int shown = 0, pool = 0;
        for (int s = first; s < specs.Count && pool < RowPool; s++)
        {
            var spec = specs[s];
            int h = HeightOf(spec);
            if (y - h < listBottom && shown > 0) break;
            var row = rows[pool++];
            row.Spec = spec;
            Visible(row.Root?.ViewComponent, true);
            int indent = spec.Kind == Kind.Sub ? 18 : 0;
            if (spec.Kind == Kind.Label && s > first && !explorer) y -= 6;
            Rect(row.Root?.ViewComponent, Pad + indent, y, inner - indent, h);
            DrawRow(row, spec, inner - indent, h);
            y -= h + (explorer ? ListGap : RowGap);
            shown++;
        }
        for (int i = pool; i < RowPool; i++) { rows[i].Spec = null; Visible(rows[i].Root?.ViewComponent, false); }
        bool showMore = first + shown < specs.Count || first > 0;
        Visible(more, showMore);
        if (showMore)
        {
            Rect(more, Pad, explorer ? -(height - 26) : listBottom + 2, inner, 20);
            Text(more, string.Format(Localization.Get("xuiRebirthBonusWsMore"), first + 1, first + shown, specs.Count), MutedColor);
            y -= 22;
        }

        Visible(message, showMessage); Visible(messageBg, showMessage); Visible(messageFrame, showMessage);
        if (showMessage)
        {
            int top = Math.Min(y - 6, -50);
            Rect(messageBg, Pad, top, inner, 58);
            Rect(messageFrame, Pad, top, inner, 58);
            Rect(message, Pad + 8, top - 5, inner - 16, 48);
            Text(message, shownMessage, TextColor);
        }

        var button = windowGroup?.Controller?.GetChildById("btnEnchant") as XUiC_SimpleButton;
        string verb = Verb();
        if (button != null && button.Text != verb) button.Text = verb;

        string guide = Guide();
        if (XUiC_RebirthCraftingRecipeDetails.ModifierGuide != guide)
        {
            XUiC_RebirthCraftingRecipeDetails.ModifierGuide = guide;
            Find<XUiC_RebirthCraftingRecipeDetails>()?.RefreshModifierGuide();
        }
    }

    private bool FitsAll(List<Spec> specs, int top, int bottom)
    {
        int y = top;
        if (specs.Count > RowPool) return false;
        foreach (var spec in specs) y -= HeightOf(spec) + RowGap + (spec.Kind == Kind.Label ? 6 : 0);
        return y >= bottom;
    }

    private int HeightOf(Spec spec)
    {
        if (spec.Kind == Kind.Label) return LabelHeight;
        if (spec.Segments != null) return SegRowHeight;
        return mode == Mode.Explorer ? ListRowHeight : RowHeight;
    }

    /// <summary>Tailoring strip, rarity odds and result count at the bottom of the column (spec 7.3). Returns its top.</summary>
    private int RenderSummary(int inner)
    {
        if (SummaryInCentre)
        {
            foreach (var v in new XUiView[] { resultLine, oddsText, oddsPct, tailorText, trackBar, trackFill, mark10, mark25, mark10Text, mark25Text, tailorNote, sumRule }) Visible(v, false);
            for (int k = 0; k < 4; k++) Visible(odds[k], false);
            return -height;
        }
        bool rowsMode = mode == Mode.Rows, resultMode = mode == Mode.Result;
        bool requirementOk = Ready && RebirthArmorBonusKits.CheckRequirement(kitName, bonuses.Length, out _);
        int kitLevel = RebirthArmorBonusKits.LevelOf(kitName);
        bool showOdds = rowsMode && requirementOk && kitLevel > 0;
        bool showResult = (rowsMode && requirementOk) || resultMode;
        bool unlocked = tailoring >= 25f;

        int bottom = -(height - 10);
        int y = bottom;
        // Result line
        Visible(resultLine, showResult);
        if (showResult)
        {
            y += 24;
            Rect(resultLine, Pad + 2, y, inner - 4, 22);
            Text(resultLine, resultMode ? Localization.Get("xuiRebirthBonusWsDone") : string.Format(Localization.Get("xuiRebirthBonusWsResultLine"), ResultCount()), resultMode ? OkColor : TextColor);
        }
        // Odds bar
        Visible(oddsText, showOdds); Visible(oddsPct, showOdds);
        for (int k = 0; k < 4; k++) Visible(odds[k], showOdds);
        if (showOdds)
        {
            var weights = RebirthArmorBonusCatalog.GetRarityWeights(kitLevel, Math.Max(1, quality));
            float total = Math.Max(1, weights.Sum());
            y += 22;
            Rect(oddsPct, Pad + 2, y, inner - 4, 20);
            var pct = new StringBuilder();
            for (int k = 0; k < 4; k++)
            {
                if (pct.Length > 0) pct.Append("   ");
                float p = weights[k] * 100f / total;
                pct.Append('[').Append(RarityHex[k]).Append(']').Append(Localization.Get(RarityKeys[k])).Append(' ').Append(p.ToString(p < 1f && p > 0f ? "0.0" : "0", CultureInfo.InvariantCulture)).Append("%[-]");
            }
            Text(oddsPct, pct.ToString(), MutedColor);
            y += 12;
            int x = Pad;
            for (int k = 0; k < 4; k++)
            {
                int w = k == 3 ? Pad + inner - x : (int)Math.Round(inner * weights[k] / total);
                Visible(odds[k], w > 0);
                Rect(odds[k], x, y, Math.Max(1, w), 8);
                x += Math.Max(0, w);
            }
            y += 24;
            Rect(oddsText, Pad + 2, y, inner - 4, 20);
            Text(oddsText, string.Format(Localization.Get("xuiRebirthBonusWsOdds"), kitLevel, Math.Max(1, quality)).ToUpperInvariant(), LabelColor);
        }
        // Tailoring strip
        bool showTailor = rowsMode || resultMode;
        bool showTrack = showTailor && !unlocked;
        Visible(tailorText, showTailor);
        foreach (var v in new XUiView[] { trackBar, trackFill, mark10, mark25, mark10Text, mark25Text, tailorNote }) Visible(v, showTrack);
        if (showTrack)
        {
            y += 22;
            Rect(tailorNote, Pad + 2, y, inner - 4, 20);
            Text(tailorNote, Localization.Get("xuiRebirthBonusWsGainNote"), MutedColor);
            y += 20;
            int trackW = inner - 8, x0 = Pad + 4;
            int x10 = x0 + trackW * 10 / 50, x25 = x0 + trackW * 25 / 50;
            bool ok10 = tailoring >= 10f;
            Rect(mark10Text, x10 - 60, y, 120, 18);
            Rect(mark25Text, x25 - 60, y, 120, 18);
            Text(mark10Text, Localization.Get("xuiRebirthBonusWsMark10"), ok10 ? OkColor : MutedColor);
            Text(mark25Text, Localization.Get("xuiRebirthBonusWsMark25"), MutedColor);
            y += 20;
            Rect(trackBar, x0, y - 5, trackW, 6);
            Rect(trackFill, x0, y - 5, Math.Max(1, (int)(trackW * Math.Min(1f, tailoring / 50f))), 6);
            Visible(trackFill, tailoring > 0f);
            Rect(mark10, x10 - 1, y, 2, 16);
            Rect(mark25, x25 - 1, y, 2, 16);
            Tint(mark10, ok10 ? OkColor : MarkOff);
            Tint(mark25, MarkOff);
            y += 4;
        }
        if (showTailor)
        {
            y += 22;
            Rect(tailorText, Pad + 2, y, inner - 4, 20);
            string value = tailoring.ToString("0.#", CultureInfo.InvariantCulture);
            string state = unlocked ? Localization.Get("xuiRebirthBonusWsAllUnlocked")
                : tailoring >= 10f ? string.Format(Localization.Get("xuiRebirthBonusWsMoreToExact"), Math.Ceiling(25f - tailoring).ToString(CultureInfo.InvariantCulture))
                : string.Format(Localization.Get("xuiRebirthBonusWsMoreToCat"), Math.Ceiling(10f - tailoring).ToString(CultureInfo.InvariantCulture));
            Text(tailorText, (string.Format(Localization.Get("xuiRebirthBonusWsTailoring"), value) + "  -  " + state).ToUpperInvariant(), LabelColor);
        }
        bool any = showTailor || showOdds || showResult;
        Visible(sumRule, any);
        if (any) { y += 8; Rect(sumRule, Pad, y, inner, 2); }
        return y;
    }

    private void DrawRow(RowView row, Spec spec, int w, int h)
    {
        bool label = spec.Kind == Kind.Label;
        bool chip = spec.Rarity >= 0;
        bool segments = spec.Segments != null;
        bool button = !segments && !string.IsNullOrEmpty(spec.Button);
        int lineH = segments ? 36 : h; // two-line rows centre their text in the first line
        Visible(row.Bg, !label);
        Visible(row.Frame, !label);
        if (!label)
        {
            Rect(row.Bg, 0, 0, w, h);
            Rect(row.Frame, 0, 0, w, h);
            // New-bonus rows stand out in the station purple (owner 2026-11-08: "barely visible"); the 25% row stays quiet.
            bool fresh = spec.Kind == Kind.New || spec.Kind == Kind.Sub;
            Tint(row.Bg, spec.Removed ? RemoveBg : spec.Selected || spec.Highlight ? SelectBg : fresh ? NewRowBg : spec.Kind == Kind.Extra ? OpenBg : RowBg);
            Tint(row.Frame, spec.Removed ? RemoveFrame : spec.Selected || spec.Highlight ? SelectFrame : fresh ? SegOnFrame : RowFrame);
        }
        Visible(row.Chip, chip);
        Visible(row.ChipText, chip);
        int textX = label ? 0 : 8;
        if (chip)
        {
            int chipTop = -(lineH - 20) / 2;
            Rect(row.Chip, 6, chipTop, 82, 20);
            Mid(row.ChipText, UIWidget.Pivot.Center, 6 + 41, chipTop, 20, 80);
            Tint(row.Chip, RarityColors[spec.Rarity]);
            Text(row.ChipText, Localization.Get(RarityKeys[spec.Rarity]), ChipText);
            textX = 96;
        }

        bool star = spec.Star >= 0;
        int starSpace = star ? 28 : 0;
        const int buttonW = 92;
        int buttonSpace = button ? buttonW + 8 : 0;
        string text = spec.Kind == Kind.Sub ? "> " + spec.Text : spec.Text;
        int rightWidth = string.IsNullOrEmpty(spec.Right) ? 0
            : spec.Kind == Kind.New || spec.Kind == Kind.Sub ? Math.Max(100, w - textX - 100 - starSpace - buttonSpace)
            : spec.Kind == Kind.Label ? Math.Min(160, w / 2)
            : Math.Min(w / 2, Math.Max(60, VisibleLength(spec.Right) * 8 + 12)); // only what its text needs, so the name keeps its size
        int textWidth = segments ? w - textX - 8 : Math.Max(40, w - textX - rightWidth - starSpace - buttonSpace - 14);
        Mid(row.Text, UIWidget.Pivot.Left, textX, 0, lineH, textWidth);
        var textColor = label ? (spec.Dim ? MutedColor : LabelColor) : spec.Dim ? MutedColor : TextColor;
        Text(row.Text, label && !spec.Dim ? text.ToUpperInvariant() : text, textColor);
        if (row.Text != null) { int size = label ? 16 : 18; if (row.Text.FontSize != size) row.Text.FontSize = size; }

        Visible(row.Right, rightWidth > 0);
        if (rightWidth > 0)
        {
            Mid(row.Right, UIWidget.Pivot.Right, w - 8 - starSpace - buttonSpace, 0, lineH, rightWidth);
            Text(row.Right, spec.Right, spec.Taken ? TakenColor : spec.Dim && !label ? MutedColor : spec.RightColor); // muted yellow for "Already on this item" (owner 2026-11-08)
        }

        Visible(row.Star, star);
        Visible(row.StarButton?.ViewComponent, star);
        if (star)
        {
            Rect(row.Star, w - 28, -(lineH - 20) / 2, 20, 20);
            Rect(row.StarButton?.ViewComponent, w - 32, 0, 30, h);
            Tint(row.Star, spec.Star == 1 ? StarOn : StarOff);
            string tip = Localization.Get(spec.Star == 1 ? "xuiRebirthBonusWsStarRemove" : "xuiRebirthBonusWsStarAdd");
            var view = row.StarButton?.ViewComponent;
            if (view != null && view.ToolTip != tip) view.ToolTip = tip;
        }

        for (int k = 0; k < 3; k++)
        {
            var seg = row.Segs[k];
            bool on = segments || (button && k == 0);
            Visible(seg.Bg, on); Visible(seg.Frame, on); Visible(seg.Text, on); Visible(seg.Button?.ViewComponent, on);
            if (!on) continue;
            const int segW = 82, segH = 24;
            int x, top, width;
            bool selected;
            string caption;
            if (segments)
            {
                x = w - 8 - (3 - k) * (segW + 4) + 4;
                top = -32;
                width = segW;
                selected = spec.Segment == k;
                caption = spec.Segments[k];
            }
            else
            {
                x = w - 8 - starSpace - buttonW;
                top = -(h - segH) / 2;
                width = buttonW;
                selected = true; // a button, drawn like the selected segment so it reads as clickable
                caption = spec.Button;
            }
            Rect(seg.Bg, x, top, width, segH);
            Rect(seg.Frame, x, top, width, segH);
            Mid(seg.Text, UIWidget.Pivot.Center, x + width / 2, top, segH, width - 4);
            Rect(seg.Button?.ViewComponent, x, top, width, segH);
            Tint(seg.Bg, selected ? SegOnBg : SegBg);
            Tint(seg.Frame, selected ? SegOnFrame : SegFrame);
            Text(seg.Text, caption, selected ? SegOnText : SegText);
        }

        bool pressable = spec.Press != null;
        Visible(row.Press?.ViewComponent, pressable);
        if (pressable) Rect(row.Press?.ViewComponent, 0, 0, w - starSpace - buttonSpace - (star || button ? 6 : 0), h);
    }

    /// <summary>Characters a label will show (bbcode colour tags removed).</summary>
    private static int VisibleLength(string text) => string.IsNullOrEmpty(text) ? 0 : System.Text.RegularExpressions.Regex.Replace(text, @"\[[^\]]*\]", string.Empty).Length;

    /// <summary>
    /// Places a label in a band (top, height) with its text vertically centred and justified left, right or centre.
    /// The label keeps a top-left pivot (owner 2026-11-08: changing the pivot pushed text past the row edge); for Right,
    /// x is the right edge, for Center the middle. The text top sits so the capitals are centred: half the band minus
    /// about 0.45 of the font size.
    /// </summary>
    private static void Mid(XUiV_Label label, UIWidget.Pivot pivot, int x, int top, int bandHeight, int width)
    {
        if (label == null) return;
        if (label.Pivot != UIWidget.Pivot.TopLeft) label.Pivot = UIWidget.Pivot.TopLeft;
        var alignment = pivot == UIWidget.Pivot.Right ? NGUIText.Alignment.Right : pivot == UIWidget.Pivot.Center ? NGUIText.Alignment.Center : NGUIText.Alignment.Left;
        if (label.label != null && label.label.alignment != alignment) label.label.alignment = alignment;
        int left = pivot == UIWidget.Pivot.Right ? x - width : pivot == UIWidget.Pivot.Center ? x - width / 2 : x;
        int font = label.FontSize > 0 ? label.FontSize : 18;
        int textTop = top - (int)Math.Round(bandHeight / 2f - font * 0.45f);
        var size = new Vector2i(Math.Max(1, width), font + 6);
        if (label.Size != size) label.Size = size;
        var position = new Vector2i(left, textTop);
        if (label.Position != position) label.Position = position;
    }

    private static void Rect(XUiController controller, int x, int y, int w, int h) => Rect(controller?.ViewComponent, x, y, w, h);

    private static void Rect(XUiView view, int x, int y, int w, int h)
    {
        if (view == null) return;
        var position = new Vector2i(x, y);
        var size = new Vector2i(Math.Max(1, w), Math.Max(1, h));
        if (view.Position != position) view.Position = position;
        if (view.Size != size) view.Size = size;
    }

    private static void Visible(XUiView view, bool visible)
    {
        if (view != null && view.IsVisible != visible) view.IsVisible = visible;
    }

    private static void Text(XUiV_Label label, string text, Color color)
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
