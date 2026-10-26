using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

public sealed partial class XUiC_RebirthCompanions
{
    private const float SkillBarMax = 100f;
    private const int TraitVisible = 9, TraitTrackHeight = 450;
    private readonly XUiController[] traitRows = new XUiController[TraitVisible];
    private readonly XUiV_Label[] traitNames = new XUiV_Label[TraitVisible];
    private readonly XUiV_Sprite[] traitAccents = new XUiV_Sprite[TraitVisible];
    private Color[] traitColors = Array.Empty<Color>();
    private const int DetailVisible = 10, DetailTrackHeight = 262, DetailWrapChars = 56;
    private static readonly string[] AttributeIds = { "strength", "dexterity", "constitution", "intelligence", "charisma" };
    private readonly XUiController[] companionSkillRows = new XUiController[12];
    private readonly XUiV_Label[] companionSkillTexts = new XUiV_Label[12];
    private readonly XUiV_Label[] companionSkillPracticalTexts = new XUiV_Label[12];
    private readonly XUiV_Label[] companionSkillTheoryTexts = new XUiV_Label[12];
    private readonly XUiV_Sprite[] companionSkillPracticalFills = new XUiV_Sprite[12];
    private readonly XUiV_Sprite[] companionSkillTheoryFills = new XUiV_Sprite[12];
    private readonly XUiController[] companionAttrCells = new XUiController[5];
    private readonly XUiV_Label[] companionAttrNames = new XUiV_Label[5];
    private readonly XUiV_Label[] companionAttrValues = new XUiV_Label[5];
    private readonly XUiV_Sprite[] companionAttrFills = new XUiV_Sprite[5];
    private XUiV_Label companionSkillSelectedName, companionSkillSelectedDetails, companionSkillSelPractical, companionSkillSelTheory;
    private XUiV_Sprite companionSkillSelPracticalFill, companionSkillSelTheoryFill;
    private RebirthHumanProgressionProjection companionSkillProfile;
    private string[] companionSkillIds = Array.Empty<string>();
    private string companionSkillSelectedId = string.Empty;
    private int companionSkillOffset;
    private string[] traitLines = Array.Empty<string>(), detailLines = Array.Empty<string>();
    private int traitOffset, detailOffset;
    private XUiController traitsTrack, traitsThumbControl, detailTrack, detailThumbControl;
    private XUiV_Button traitsThumb, detailThumb;

    private XUiV_Sprite Sprite(string id) => GetChildById(id)?.ViewComponent as XUiV_Sprite;

    private void InitHumanSkillRows()
    {
        for (int i = 0; i < TraitVisible; i++)
        {
            traitRows[i] = GetChildById("traitRow" + i); traitNames[i] = Label("traitName" + i); traitAccents[i] = Sprite("traitAccent" + i);
        }
        companionSkillSelectedName = Label("companionSkillSelectedName");
        companionSkillSelectedDetails = Label("companionSkillSelectedDetails");
        companionSkillSelPractical = Label("companionSkillSelPracticalValue");
        companionSkillSelTheory = Label("companionSkillSelTheoryValue");
        companionSkillSelPracticalFill = Sprite("companionSkillSelPracticalFill");
        companionSkillSelTheoryFill = Sprite("companionSkillSelTheoryFill");
        for (int i = 0; i < companionSkillRows.Length; i++)
        {
            int row = i;
            companionSkillRows[i] = GetChildById("companionSkillRow" + i);
            companionSkillTexts[i] = Label("companionSkillText" + i);
            companionSkillPracticalTexts[i] = Label("companionSkillPractical" + i);
            companionSkillTheoryTexts[i] = Label("companionSkillTheory" + i);
            companionSkillPracticalFills[i] = Sprite("companionSkillPracticalFill" + i);
            companionSkillTheoryFills[i] = Sprite("companionSkillTheoryFill" + i);
            if (companionSkillRows[i] != null) companionSkillRows[i].OnPress += delegate { SelectCompanionSkill(row); };
        }
        for (int i = 0; i < AttributeIds.Length; i++)
        {
            companionAttrCells[i] = GetChildById("companionAttrCell" + i);
            companionAttrNames[i] = Label("companionAttrName" + i);
            companionAttrValues[i] = Label("companionAttrValue" + i);
            companionAttrFills[i] = Sprite("companionAttrFill" + i);
        }
        var prev = GetChildById("companionSkillPrev");
        var next = GetChildById("companionSkillNext");
        if (prev != null) prev.OnPress += delegate { companionSkillOffset = Math.Max(0, companionSkillOffset - 12); RenderCompanionSkillRows(); };
        if (next != null) next.OnPress += delegate { companionSkillOffset = Math.Min(Math.Max(0, companionSkillIds.Length - 12), companionSkillOffset + 12); RenderCompanionSkillRows(); };

        // Conditional text scroll: same track/thumb + paging helpers as the roster, command and inventory panes.
        traitsTrack = GetChildById("traitsScrollTrackInput"); traitsThumbControl = GetChildById("traitsScrollThumb"); traitsThumb = traitsThumbControl?.ViewComponent as XUiV_Button;
        detailTrack = GetChildById("skillDetailScrollTrackInput"); detailThumbControl = GetChildById("skillDetailScrollThumb"); detailThumb = detailThumbControl?.ViewComponent as XUiV_Button;
        Action<float> traitsScroll = delegate(float delta) { traitOffset = ScrollOffset(traitOffset, delta, traitLines.Length, TraitVisible); RenderTraits(); };
        Action<float> detailScroll = delegate(float delta) { detailOffset = ScrollOffset(detailOffset, delta, detailLines.Length, DetailVisible); RenderDetails(); };
        WireScroll(GetChildById("traitsPaneScrollCapture"), traitsScroll); WireScroll(traitsTrack, traitsScroll); WireScroll(traitsThumbControl, traitsScroll); WireScroll(GetChildById("backgroundTraits"), traitsScroll);
        WireScroll(GetChildById("skillDetailPaneScrollCapture"), detailScroll); WireScroll(detailTrack, detailScroll); WireScroll(detailThumbControl, detailScroll); WireScroll(GetChildById("companionSkillSelectedDetails"), detailScroll);
        WireThumbDrag(traitsThumbControl, delegate(float dy) { DragScroll(ref traitOffset, traitLines.Length, TraitVisible, TraitTrackHeight, dy, RenderTraits); });
        WireThumbDrag(detailThumbControl, delegate(float dy) { DragScroll(ref detailOffset, detailLines.Length, DetailVisible, DetailTrackHeight, dy, RenderDetails); });
    }

    private void SelectCompanionSkill(int row)
    {
        int index = companionSkillOffset + row;
        if (index < 0 || index >= companionSkillIds.Length) return;
        companionSkillSelectedId = companionSkillIds[index];
        RenderCompanionSkillRows();
    }

    // Bar scale comes from the same definitions the player Skills window uses (skill Max, theory SkillKnowledgeMax); 100 only as fallback.
    private static void SkillMaxes(string id, out float practicalMax, out float theoryMax)
    {
        practicalMax = theoryMax = SkillBarMax;
        if (RebirthSurvivorDefinitionRegistry.TryGetSkill(id, out var definition) && definition != null && definition.Max > 0f) practicalMax = definition.Max;
        var progression = RebirthSurvivorDefinitionRegistry.Bundle?.Progression;
        if (progression != null && progression.SkillKnowledgeMax > 0f) theoryMax = progression.SkillKnowledgeMax;
    }

    private static void SetFill(XUiV_Sprite sprite, float value, float max)
    {
        if (sprite == null) return;
        float fill = Mathf.Clamp01(value / Mathf.Max(1f, max));
        if (!Mathf.Approximately(sprite.Fill, fill)) sprite.Fill = fill;
    }

    private void RenderCompanionSkillRows()
    {
        RebirthHumanProgressionProjection profile = companionSkillProfile;
        for (int i = 0; i < companionSkillRows.Length; i++)
        {
            int index = companionSkillOffset + i;
            bool present = index >= 0 && index < companionSkillIds.Length && profile != null;
            SetVisible(companionSkillRows[i], present);
            float practical = 0f, theory = 0f, practicalMax = SkillBarMax, theoryMax = SkillBarMax;
            if (present)
            {
                profile.Skills.TryGetValue(companionSkillIds[index], out practical); profile.Theory.TryGetValue(companionSkillIds[index], out theory);
                SkillMaxes(companionSkillIds[index], out practicalMax, out theoryMax);
            }
            Set(companionSkillTexts[i], present ? RebirthSkillDisplayNames.Get(companionSkillIds[index]) : string.Empty);
            Set(companionSkillPracticalTexts[i], present ? practical.ToString("0.#", CultureInfo.CurrentCulture) : string.Empty);
            Set(companionSkillTheoryTexts[i], present ? theory.ToString("0.#", CultureInfo.CurrentCulture) : string.Empty);
            SetFill(companionSkillPracticalFills[i], practical, practicalMax); SetFill(companionSkillTheoryFills[i], theory, theoryMax);
            if (companionSkillRows[i]?.ViewComponent is XUiV_Button button)
                button.Selected = present && companionSkillIds[index] == companionSkillSelectedId;
        }
        bool has = profile != null && !string.IsNullOrEmpty(companionSkillSelectedId) && profile.Skills.ContainsKey(companionSkillSelectedId);
        float selPractical = 0f, selTheory = 0f, selPracticalMax = SkillBarMax, selTheoryMax = SkillBarMax;
        if (has)
        {
            profile.Skills.TryGetValue(companionSkillSelectedId, out selPractical); profile.Theory.TryGetValue(companionSkillSelectedId, out selTheory);
            SkillMaxes(companionSkillSelectedId, out selPracticalMax, out selTheoryMax);
        }
        Set(companionSkillSelectedName, has ? RebirthSkillDisplayNames.Get(companionSkillSelectedId) : string.Empty);
        Set(companionSkillSelPractical, has ? selPractical.ToString("0.#", CultureInfo.CurrentCulture) + " / " + selPracticalMax.ToString("0", CultureInfo.CurrentCulture) : string.Empty);
        Set(companionSkillSelTheory, has ? selTheory.ToString("0.#", CultureInfo.CurrentCulture) + " / " + selTheoryMax.ToString("0", CultureInfo.CurrentCulture) : string.Empty);
        SetFill(companionSkillSelPracticalFill, selPractical, selPracticalMax); SetFill(companionSkillSelTheoryFill, selTheory, selTheoryMax);
        // Rebuilt only when the selected skill, its values or the NPC change (the numeric effect reads the saved record).
        string detailKey = has ? companionSkillSelectedId + "|" + selPractical + "|" + selTheory + "|" + companionSkillHasStable + companionSkillStable : string.Empty;
        if (detailKey != companionDetailKey)
        {
            companionDetailKey = detailKey;
            string[] lines = has ? WrapLines(SkillDetailText(companionSkillSelectedId, selPractical, selTheory), DetailWrapChars) : Array.Empty<string>();
            if (!lines.SequenceEqual(detailLines)) { detailLines = lines; detailOffset = 0; }
        }
        RenderDetails();
    }

    // Practical/Theory numbers are shown by the bars, so this is the non-numeric remainder of the shared detail text.
    private static string SkillDetailText(string id, float practical, float theory)
    {
        if (!RebirthSurvivorDefinitionRegistry.TryGetSkill(id, out var definition) || definition == null) return RebirthHumanProfileUiText.Unavailable;
        string attribute = RebirthAttributeProgressionService.GetPrimaryAttributeForSkill(id);
        string text = RebirthSurvivorUiText.L("xuiRebirthProgressionPrimaryAttribute", "Primary Attribute") + ": " +
               RebirthSurvivorUiText.ResolveDefinitionName(attribute) + "\n\n" +
               RebirthSurvivorUiText.L("xuiRbNpcUiPracticalTraining", "Practical training") + ": " +
               RebirthCharacterUiSnapshotBuilder.SkillTrainingSource(definition) + "\n\n" +
               RebirthSurvivorUiText.L("xuiRbNpcUiTheorySources", "Theory sources") + ": " +
               RebirthCharacterUiSnapshotBuilder.TheorySource(id);
        // "Current effects": the same status and Theory-band reveal the player Skills/Explorer panels show
        // (RebirthCharacterUiSnapshotBuilder.BuildExplorerSkillDetails), fed with this NPC's shared values.
        // Effects are listed only where a real enabled NPC consumer exists (NpcSkillEffectStatus); information the
        // player panels reveal by Theory band is shown separately and is not presented as an effect.
        text += "\n\n" + RebirthSurvivorUiText.L("xuiRbNpcUiCurrentEffects", "Current effects") + "\n" + NpcSkillEffectStatus(id, practical) + NpcSkillNumericEffect(id);
        string reveal = RebirthTheoryRevealService.BuildReveal(id, theory);
        if (!string.IsNullOrEmpty(reveal))
            text += "\n\n" + RebirthSurvivorUiText.L("xuiRbNpcUiTheoryReveals", "What Theory reveals") + "\n" +
                    RebirthCharacterUiSnapshotBuilder.PracticalStatus(practical) + " (" + practical.ToString("0.#", CultureInfo.CurrentCulture) + ") - " +
                    RebirthTheoryProgressionService.GetTheoryStatus(theory) + " " + RebirthSurvivorUiText.L("xuiRbNpcUiTheory", "Theory") + "\n" + reveal;
        return text;
    }

    private static readonly RebirthNpcProfession[] SharedWorkProfessions =
        { RebirthNpcProfession.Farming, RebirthNpcProfession.Mining, RebirthNpcProfession.Salvaging, RebirthNpcProfession.Cooking, RebirthNpcProfession.BaseRepairs, RebirthNpcProfession.Medicine };

    // Source-proven per-skill NPC consumers (P1 matrix): weapon passives (RebirthHumanWeaponPassives, Chunk7 skills),
    // the six one-to-one professions (RebirthNpcProfessionProgressionService.TryGetSharedSkill, level 1..20 as in TryGetSharedLevel)
    // and instructor Teaching (RebirthHumanTeaching). Everything else is reported as having no NPC action yet.
    private static string NpcSkillEffectStatus(string id, float practical)
    {
        if (RebirthWeaponFamilySkillService.IsChunk7Skill(id))
            return RebirthSurvivorUiText.L("xuiRbNpcUiSkillActiveCombat", "Active: weapon handling bonuses apply while this survivor fights with a matching weapon.");
        for (int i = 0; i < SharedWorkProfessions.Length; i++)
        {
            if (!RebirthNpcProfessionProgressionService.TryGetSharedSkill(SharedWorkProfessions[i], out string shared) || !string.Equals(shared, id, StringComparison.OrdinalIgnoreCase)) continue;
            int level = 1;
            if (RebirthSurvivorDefinitionRegistry.TryGetSkill(id, out var definition) && definition != null && definition.Max > 0f)
                level = RebirthG02SkillScale.ToLevel(practical, definition.Max);
            return string.Format(RebirthSurvivorUiText.L("xuiRbNpcUiSkillActiveWork", "Active: work speed, yield and quality use this skill (competence {0} / 20)."), level);
        }
        if (string.Equals(id, RebirthSurvivorIds.SkillTeaching, StringComparison.OrdinalIgnoreCase))
            return RebirthSurvivorUiText.L("xuiRbNpcUiSkillActiveTeaching", "Active: sets how much this survivor can teach in lessons.");
        return RebirthSurvivorUiText.L("xuiRbNpcUiSkillNoNpcAction", "No survivor action uses this skill yet.");
    }

    // Selected NPC for numeric effects; set by RenderHumanProfile. Values exist only where the saved record is readable
    // here (host / single player); otherwise no number is shown rather than an invented one.
    private static RebirthNpcStableId companionSkillStable;
    private static bool companionSkillHasStable;
    private string companionDetailKey = string.Empty;

    // Real numeric consumer: RebirthHumanResourceSkillEffects applies SignedEndpoint(effective, negative, positive) of the NPC's
    // own saved skill as the native harvest-count passive; the same calculation is evaluated here for display.
    private static string NpcSkillNumericEffect(string id)
    {
        if (!companionSkillHasStable) return string.Empty;
        bool resource = string.Equals(id, RebirthSurvivorIds.SkillMining, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(id, RebirthSurvivorIds.SkillLogging, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(id, RebirthSurvivorIds.SkillSalvage, StringComparison.OrdinalIgnoreCase);
        bool farming = string.Equals(id, RebirthSurvivorIds.SkillFarming, StringComparison.OrdinalIgnoreCase);
        if (!resource && !farming) return string.Empty;
        if (!RebirthHumanProgression.TryPeek(companionSkillStable, out RebirthWorldCharacterRecord record) ||
            !RebirthSkillOutcomeValueService.TryGetPracticalAndEffective(record, id, out float practical, out float effective, out float attribute))
            return string.Empty;
        float value = resource
            ? RebirthResourceFieldSkillService.SignedEndpoint(effective, RebirthProgressionRuntimeConfig.ResourceHarvestNegative, RebirthProgressionRuntimeConfig.ResourceHarvestPositive)
            : RebirthResourceFieldSkillService.SignedEndpoint(effective, RebirthProgressionRuntimeConfig.FarmingHarvestNegative, RebirthProgressionRuntimeConfig.FarmingHarvestPositive);
        return "\n" + string.Format(RebirthSurvivorUiText.L("xuiRbNpcUiSkillHarvestEffect", "Harvest yield: {0}"),
            (value >= 0f ? "+" : string.Empty) + (value * 100f).ToString("0.#", CultureInfo.CurrentCulture) + "%");
    }

    // ---- Overview portrait (validated RebirthCharacterModelBinder, same bounded approach as the Equipment tab) ----
    // Never built in the open frame: >=30 frames after the tab is shown on a settled frame, then BindStaged (frame A rig, frame B
    // render/readback), once per NPC+gear epoch, finished image kept by Suspend for a cheap reopen, placeholder icon while pending.
    private RebirthCharacterModelBinder portraitBinder;
    private bool portraitShown;
    private int portraitDueFrame, portraitAttempts, portraitEpoch;
    private string portraitToken, portraitFailedToken;
    private long portraitStageMs;
    private Tab portraitLastTab;

    private void UpdatePortrait()
    {
        if (currentTab != Tab.Overview || !XUiC_RebirthHumanEquipment.ModelPreviewEnabled || snapshot == null || snapshot.RequestFailed || EquipmentBusy) return;
        RebirthCompanionListEntry selected = Selected();
        EntityAlive npc = null;
        if (selected != null && selected.Kind == RebirthCompanionTargetKind.Npc && !selected.IsDog && !selected.IsBoundUndead)
            npc = GameManager.Instance?.World?.GetEntity(selected.EntityId) as EntityAlive;
        if (npc == null || npc.IsDead())
        {
            if (portraitToken != null || portraitShown) TearDownPortrait(true);
            return;
        }
        if (portraitBinder == null)
        {
            XUiController texture = GetChildById("overviewPortrait");
            if (texture == null) return;
            portraitBinder = new RebirthCharacterModelBinder(texture, true, "[REBIRTH Companion Overview]");
        }
        string token = npc.entityId + "|" + portraitEpoch;
        if (portraitToken != null && token != portraitToken) TearDownPortrait(true);
        if (token == portraitToken && !portraitShown)
        {
            // Reopen of the same NPC and gear: the binder kept the finished image (Suspend); Bind(false) only reattaches it.
            if (portraitBinder.Bind(npc, false)) ShowPortrait(true); else portraitToken = null;
            return;
        }
        if (token == portraitToken || (token == portraitFailedToken && portraitAttempts >= 3)) return;
        if (!portraitBinder.IsStaging)
        {
            if (portraitDueFrame == 0) { portraitDueFrame = Time.frameCount + 30; return; }
            if (Time.frameCount < portraitDueFrame) return;
            if (Time.unscaledDeltaTime > 0.05f && Time.frameCount < portraitDueFrame + 60) return;
        }
        var timer = System.Diagnostics.Stopwatch.StartNew();
        bool ok = portraitBinder.BindStaged(npc);
        timer.Stop();
        portraitStageMs += timer.ElapsedMilliseconds;
        if (!ok && portraitBinder.IsStaging) return;
        portraitDueFrame = 0;
        long totalMs = portraitStageMs; portraitStageMs = 0;
        if (ok)
        {
            Log.Out("[REBIRTH Companion Overview] portrait ms=" + totalMs + " (staged over 2 frames) ok=True entity=" + npc.entityId);
            portraitToken = token; portraitFailedToken = null; portraitAttempts = 0;
            ShowPortrait(true);
        }
        else
        {
            if (portraitFailedToken != token) { portraitFailedToken = token; portraitAttempts = 0; }
            if (portraitAttempts++ == 0) Log.Out("[REBIRTH Companion Overview] portrait ms=" + totalMs + " ok=False entity=" + npc.entityId);
        }
    }

    private void ShowPortrait(bool shown)
    {
        portraitShown = shown;
        SetVisible(GetChildById("overviewPortrait"), shown);
        if (selectedIcon != null) selectedIcon.IsVisible = !shown && Selected() != null;
    }

    // dispose=false (tab hide): Suspend keeps the finished image. dispose=true (NPC change / close / dog selected): Clear destroys it.
    private void TearDownPortrait(bool dispose)
    {
        if (portraitBinder != null) { if (dispose) { portraitBinder.Clear(); portraitToken = null; } else portraitBinder.Suspend(); }
        portraitDueFrame = 0; portraitStageMs = 0; portraitFailedToken = null; portraitAttempts = 0;
        if (portraitShown) ShowPortrait(false);
    }

    // Signature bonuses with a real NPC consumer (PROVISIONAL_DECISIONS_G01 P-1: A = already applied, N = applied by G01).
    // Every other signature bonus stays player-only (H). professional_logging applies only its wood multiplier and
    // nothing_is_junk only its backpack bonus; their other parts are held.
    private static readonly HashSet<string> NpcAppliedSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "background_bonus.fire_resistant", "background_bonus.combat_momentum", "background_bonus.scholar_and_mentor",
        "background_bonus.professional_logging", "background_bonus.nothing_is_junk"
    };
    private string companionSignatureKey = string.Empty, companionSignatureText = string.Empty;

    // Active/held text from the live NPC profile (RebirthG01NpcBackgroundEffects keeps it cached; readable on host / single player).
    // Anything unreadable (remote client, profile not ready) falls back to the previous projection-only text.
    private string SignatureEffectText(RebirthCompanionListEntry selected, RebirthHumanProgressionProjection profile)
    {
        string fallback = RebirthHumanProfileUiText.SignatureDescription(profile);
        var npc = selected == null ? null : GameManager.Instance?.World?.GetEntity(selected.EntityId) as EntityRebirthHumanoidNPC;
        if (npc == null || !RebirthG01NpcBackgroundEffects.TryGetSignatureBonus(npc, out RebirthBackgroundBonusDefinition bonus) || bonus == null) return fallback;
        int bag = RebirthG01NpcBackgroundEffects.BagSlotBonus(npc);
        string key = profile.BackgroundId + "|" + bonus.Id + "|" + bag;
        if (key == companionSignatureKey) return companionSignatureText;
        string description = RebirthSurvivorUiText.SignatureBonusDescription(profile.BackgroundId);
        string status;
        if (!NpcAppliedSignatures.Contains(bonus.Id))
            status = RebirthSurvivorUiText.L("xuiRbNpcUiSignatureNotUsed", "Not used by survivors yet: this effect only exists in a player system.");
        else if (string.Equals(bonus.Id, "background_bonus.professional_logging", StringComparison.OrdinalIgnoreCase))
            status = RebirthSurvivorUiText.L("xuiRbNpcUiSignatureActiveLogging", "Active: tree wood yield. The stamina refund is not used by survivors yet.");
        else if (string.Equals(bonus.Id, "background_bonus.nothing_is_junk", StringComparison.OrdinalIgnoreCase))
            status = string.Format(RebirthSurvivorUiText.L("xuiRbNpcUiSignatureActiveBag", "Active: +{0} backpack slots. The salvage part is not used by survivors yet."), bag);
        else
            status = RebirthSurvivorUiText.L("xuiRbNpcUiSignatureSupported", "Applies to this survivor's available actions.");
        companionSignatureKey = key;
        return companionSignatureText = description + "\n\n" + status;
    }

    // Overview summary: only values with a real source (assignments rows already loaded by RenderAssignments, snapshot storage).
    private void RenderOverviewSummary(RebirthCompanionListEntry selected)
    {
        var label = Label("overviewSummary");
        if (label == null) return;
        if (selected == null) { Set(label, string.Empty); return; }
        var text = new StringBuilder();
        if (selected.Kind == RebirthCompanionTargetKind.Npc && !selected.IsDog && assignmentRows != null && assignmentRows.Count > 0)
        {
            var active = assignmentRows.FirstOrDefault(r => !r.Job && r.Status == "Executing") ?? assignmentRows.FirstOrDefault(r => !r.Job && !IsTerminal(r));
            if (active != null) text.Append(RebirthSurvivorUiText.L("xuiRbNpcUiCurrentJob", "Current job")).Append(": ").Append(active.Name).Append(" - ").Append(active.State).Append('\n');
        }
        if (selected.StorageTotal > 0)
            text.Append(RebirthSurvivorUiText.L("xuiRbNpcUiCarrying", "Carrying")).Append(": ").Append(selected.StorageUsed).Append(" / ").Append(selected.StorageTotal)
                .Append(' ').Append(RebirthSurvivorUiText.L("xuiRbNpcUiSlots", "slots")).Append('\n');
        Set(label, text.ToString().TrimEnd('\n'));
    }

    // Greedy word wrap by estimated character width so the scroll unit is a rendered line.
    private static string[] WrapLines(string text, int width)
    {
        var lines = new List<string>();
        foreach (string paragraph in (text ?? string.Empty).Split('\n'))
        {
            if (paragraph.Length == 0) { lines.Add(string.Empty); continue; }
            var line = new StringBuilder();
            foreach (string word in paragraph.Split(' '))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > width) { lines.Add(line.ToString()); line.Clear(); }
                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }
            lines.Add(line.ToString());
        }
        return lines.ToArray();
    }

    // Shows lines [offset, offset+visible). The scrollbar exists only when total > visible:
    // 0 / 1 / exact-fit rows -> no bar; one over -> bar, max offset 1; many -> bar, max offset total-visible.
    private static string Window(string[] lines, int offset, int visible)
    {
        var text = new StringBuilder();
        for (int i = offset; i < lines.Length && i < offset + visible; i++)
        {
            if (i > offset) text.Append('\n');
            text.Append(lines[i]);
        }
        return text.ToString();
    }

    // Trait cards with a polarity accent (green positive, red negative, lavender neutral), names from the same
    // definitions the player UI uses (RebirthHumanProfileUiText.Traits uses the identical lookup and colors).
    private void SetTraits(RebirthHumanProgressionProjection profile)
    {
        var names = new List<string>(); var colors = new List<Color>();
        foreach (string id in profile?.TraitIds ?? Array.Empty<string>())
            if (RebirthSurvivorDefinitionRegistry.TryGetTrait(id, out var definition) && definition != null)
            {
                names.Add(RebirthSurvivorUiText.TraitDisplayName(definition));
                colors.Add(definition.Polarity == RebirthTraitPolarity.Positive ? new Color(0.56f, 0.82f, 0.56f, 1f) :
                           definition.Polarity == RebirthTraitPolarity.Negative ? new Color(0.92f, 0.42f, 0.4f, 1f) : new Color(0.71f, 0.55f, 1f, 1f));
            }
        string[] lines = names.ToArray();
        if (!lines.SequenceEqual(traitLines)) { traitLines = lines; traitOffset = 0; }
        traitColors = colors.ToArray();
        RenderTraits();
    }

    private void RenderTraits()
    {
        traitOffset = ClampOffset(traitOffset, traitLines.Length, TraitVisible);
        for (int i = 0; i < TraitVisible; i++)
        {
            int index = traitOffset + i;
            bool present = index < traitLines.Length;
            SetVisible(traitRows[i], present);
            if (!present) continue;
            Set(traitNames[i], traitLines[index]);
            if (traitNames[i] != null) traitNames[i].Color = traitColors[index];
            if (traitAccents[i] != null) traitAccents[i].Color = traitColors[index];
        }
        UpdateScrollBar(traitsTrack, traitsThumb, traitOffset, traitLines.Length, TraitVisible, TraitTrackHeight);
    }

    private void RenderDetails()
    {
        detailOffset = ClampOffset(detailOffset, detailLines.Length, DetailVisible);
        Set(companionSkillSelectedDetails, Window(detailLines, detailOffset, DetailVisible));
        UpdateScrollBar(detailTrack, detailThumb, detailOffset, detailLines.Length, DetailVisible, DetailTrackHeight);
    }

    private void RenderAttributes(RebirthHumanProgressionProjection profile)
    {
        bool ok = profile?.Attributes != null && profile.AttributePotentials != null && profile.Attributes.Count > 0;
        for (int i = 0; ok && i < AttributeIds.Length; i++)
            ok = profile.Attributes.ContainsKey(AttributeIds[i]) && profile.AttributePotentials.ContainsKey(AttributeIds[i]);
        for (int i = 0; i < AttributeIds.Length; i++)
        {
            SetVisible(companionAttrCells[i], ok);
            if (!ok) continue;
            float current = profile.Attributes[AttributeIds[i]], potential = profile.AttributePotentials[AttributeIds[i]];
            Set(companionAttrNames[i], RebirthSurvivorUiText.ResolveDefinitionName(AttributeIds[i]));
            Set(companionAttrValues[i], current.ToString("0.#", CultureInfo.CurrentCulture) + " / " + potential.ToString("0.#", CultureInfo.CurrentCulture));
            if (companionAttrFills[i] != null) companionAttrFills[i].Fill = potential > 0f ? Mathf.Clamp01(current / potential) : 0f;
        }
        Set(skillsAttributes, ok ? string.Empty : RebirthHumanProfileUiText.Unavailable);
    }

    // Overview "background + skills at a glance": real projection values only (top three by practical).
    private void RenderOverviewGlance(RebirthHumanProgressionProjection profile)
    {
        if (overviewGlanceBackground == null) overviewGlanceBackground = Label("overviewGlanceBackground");
        if (overviewGlanceEmpty == null) overviewGlanceEmpty = Label("overviewGlanceEmpty");
        string[] ids = profile?.Skills == null ? Array.Empty<string>() : RebirthHumanProfileUiText.SkillIds(profile);
        Array.Sort(ids, (a, b) => profile.Skills[b].CompareTo(profile.Skills[a]));
        Set(overviewGlanceBackground, profile == null ? string.Empty : RebirthHumanProfileUiText.BackgroundName(profile));
        Set(overviewGlanceEmpty, profile == null ? RebirthHumanProfileUiText.NoBackground : string.Empty);
        SetVisible(overviewGlanceEmpty?.Controller, profile == null);
        for (int i = 0; i < 3; i++)
        {
            if (overviewGlanceRows[i] == null)
            {
                overviewGlanceRows[i] = GetChildById("overviewGlanceRow" + i); overviewGlanceNames[i] = Label("overviewGlanceName" + i);
                overviewGlancePractical[i] = Label("overviewGlancePractical" + i); overviewGlanceTheory[i] = Label("overviewGlanceTheory" + i);
                overviewGlancePracticalFill[i] = Sprite("overviewGlancePracticalFill" + i); overviewGlanceTheoryFill[i] = Sprite("overviewGlanceTheoryFill" + i);
            }
            bool present = i < ids.Length;
            SetVisible(overviewGlanceRows[i], present);
            if (!present) continue;
            profile.Skills.TryGetValue(ids[i], out float practical);
            float theory = 0f;
            if (profile.Theory != null) profile.Theory.TryGetValue(ids[i], out theory);
            SkillMaxes(ids[i], out float practicalMax, out float theoryMax);
            Set(overviewGlanceNames[i], RebirthSkillDisplayNames.Get(ids[i]));
            Set(overviewGlancePractical[i], practical.ToString("0.#", CultureInfo.CurrentCulture));
            Set(overviewGlanceTheory[i], theory.ToString("0.#", CultureInfo.CurrentCulture));
            SetFill(overviewGlancePracticalFill[i], practical, practicalMax); SetFill(overviewGlanceTheoryFill[i], theory, theoryMax);
        }
    }

    private XUiV_Label overviewGlanceBackground, overviewGlanceEmpty;
    private readonly XUiController[] overviewGlanceRows = new XUiController[3];
    private readonly XUiV_Label[] overviewGlanceNames = new XUiV_Label[3], overviewGlancePractical = new XUiV_Label[3], overviewGlanceTheory = new XUiV_Label[3];
    private readonly XUiV_Sprite[] overviewGlancePracticalFill = new XUiV_Sprite[3], overviewGlanceTheoryFill = new XUiV_Sprite[3];

    // Both the hire card and management window consume the same saved, read-only
    // projection. Never infer a background or skill from a client-side entity CVar.
    private void RenderHumanProfile(RebirthCompanionListEntry selected)
    {
        RebirthHumanProgressionProjection profile = null;
        companionSkillHasStable = false;
        RenderOverviewSummary(selected);
        SetVisible(GetChildById("overviewGlanceCard"), selected != null && selected.Kind == RebirthCompanionTargetKind.Npc && !selected.IsDog && !selected.IsBoundUndead);
        SetVisible(GetChildById("overviewBackgroundBlock"), selected != null && selected.Kind == RebirthCompanionTargetKind.Npc && !selected.IsDog && !selected.IsBoundUndead);
        if (selected != null && selected.Kind == RebirthCompanionTargetKind.Npc &&
            RebirthNpcRuntimeRegistry.TryGet(selected.EntityId, out var runtime) && runtime != null)
        { RebirthHumanProgressionClientCache.TryGet(runtime.StableId, out profile); companionSkillStable = runtime.StableId; companionSkillHasStable = true; }

        if (profile == null)
        {
            string unavailable = RebirthHumanProfileUiText.Unavailable;
            Set(backgroundName, unavailable); Set(backgroundDescription, string.Empty);
            Set(backgroundSignatureName, string.Empty); Set(backgroundSignatureDescription, string.Empty);
            SetTraits(null); Set(skillsText, unavailable);
            RenderAttributes(null); RenderOverviewGlance(null);
            companionSkillProfile = null; companionSkillIds = Array.Empty<string>(); companionSkillSelectedId = string.Empty; companionSkillOffset = 0;
            RenderCompanionSkillRows();
            return;
        }

        Set(backgroundName, RebirthHumanProfileUiText.BackgroundName(profile));
        Set(backgroundDescription, RebirthHumanProfileUiText.BackgroundDescription(profile));
        Set(backgroundSignatureName, RebirthSurvivorUiText.SignatureBonusName(profile.BackgroundId));
        Set(backgroundSignatureDescription, SignatureEffectText(selected, profile));
        SetTraits(profile);
        Set(skillsText, RebirthHumanProfileUiText.Skills(profile));
        RenderAttributes(profile);
        RenderOverviewGlance(profile);
        companionSkillProfile = profile;
        companionSkillIds = RebirthHumanProfileUiText.SkillIds(profile);
        if (Array.IndexOf(companionSkillIds, companionSkillSelectedId) < 0)
        { companionSkillSelectedId = companionSkillIds.Length > 0 ? companionSkillIds[0] : string.Empty; companionSkillOffset = 0; }
        RenderCompanionSkillRows();
    }
}
