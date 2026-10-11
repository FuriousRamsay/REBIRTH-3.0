using System;
using UnityEngine;

#nullable disable

/// <summary>
/// REBIRTH presentation controller for dialog response entries.
/// In ordinary dialogue it renders the vanilla-style response row. When the parent
/// response list detects trader quest offers it exposes the fixed-size REBIRTH job-card presentation.
/// Card presses show the preview; acceptance requires its explicit Accept button.
/// </summary>
public sealed class XUiC_RebirthTraderJobCard : XUiC_DialogResponseEntry
{
    private sealed class CardData
    {
        public string PreviewUri = string.Empty;
        public string PreviewPlaceholderSprite = "rb_preview_missing";
        public string BiomeName = string.Empty;
        public string BiomeSprite = "rb_biome_unknown";
        public string BiomeColor = "220,220,210,255";
        public string JobType = string.Empty;
        public string JobTypeSprite = "rb_job_special";
        public string PrefabName = string.Empty;
        public int CompletionCount;
        public string CompletionDisplay = string.Empty;
        public string Distance = string.Empty;
        public string DirectionText = string.Empty;
        public string DistanceDisplay = string.Empty;
        public int Tier = 1;
        public float DirectionAngle;
        public bool InfestedVisible;
        public string ReadableDetails;
        public string DetailsTierLabel;
    }

    private bool cardMode;
    private Quest cachedQuest;
    private CardData cachedData;
    private int cachedSemanticHash;
    private QuestJournal cachedJournal;
    private bool cachedHideIdentity;
    private float nextSemanticCheck;
    private XUiController directionArrowController;
    private XUiController previewController;
    private Texture lastPreviewTexture;
    private string lastPreviewUri = null;
    private const float PreviewTargetAspect = 282f / 159f;

    public bool CardMode { get { return cardMode; } }

    public override void OnHovered(bool over)
    {
        base.OnHovered(over);
        if (over && cardMode && CurrentResponse is DialogResponseQuest)
            XUiC_RebirthTraderJobPopup.ShowForCard(this, false);
    }

    internal object CapturePreviewAcceptance() => RebirthTraderJobPreviewAcceptance.Capture(this);
    internal bool IsPreviewAcceptanceCurrent(object token) => RebirthTraderJobPreviewAcceptance.IsCurrent(this, token);
    internal bool TryAcceptPreview(object token) => RebirthTraderJobPreviewAcceptance.TryAccept(this, token);
    internal Texture PreviewTexture => (previewController?.ViewComponent as XUiV_Texture)?.Texture;
    internal Rect PreviewUVRect => (previewController?.ViewComponent as XUiV_Texture)?.UVRect ?? new Rect(0, 0, 1, 1);
    internal string PreviewPlaceholderSprite => GetCardData()?.PreviewPlaceholderSprite ?? "rb_preview_missing";
    internal string PreviewUri => GetCardData()?.PreviewUri ?? string.Empty;

    private void ShowPreviewOnPress(XUiController sender, int mouseButton)
    {
        if (ReferenceEquals(sender, this) && cardMode && CurrentResponse is DialogResponseQuest)
            XUiC_RebirthTraderJobPopup.ShowForCard(this, true);
    }

    public override void OnCursorSelected(bool isActualElement)
    {
        base.OnCursorSelected(isActualElement);
        if (isActualElement && cardMode && CurrentResponse is DialogResponseQuest)
            XUiC_RebirthTraderJobPopup.ShowForCard(this, false);
    }

    internal string ReadableDetails()
    {
        CardData data = GetCardData();
        if (data == null) return string.Empty;
        string tierLabel = Localization.Get("xuiTier");
        if (data.ReadableDetails == null || data.DetailsTierLabel != tierLabel)
        {
            data.DetailsTierLabel = tierLabel;
            data.ReadableDetails = "[B58CFF]" + data.PrefabName + "[-]\n" + data.JobType + (string.IsNullOrEmpty(cachedQuest?.QuestClass?.QuestType) ? string.Empty : " (" + cachedQuest.QuestClass.QuestType + ")") + "\n"
                + data.BiomeName + " | " + tierLabel + " " + data.Tier + "\n"
                + data.DistanceDisplay + "\n[8FD18F]" + data.CompletionDisplay + "[-]";
        }
        return data.ReadableDetails;
    }

    public void SetCardMode(bool value)
    {
        if (cardMode == value)
            return;

        cardMode = value;
        RefreshBindings();
    }

    public override void Init()
    {
        base.Init();
        OnPress -= ShowPreviewOnPress;
        OnPress += ShowPreviewOnPress;
        directionArrowController = GetChildById("directionArrow");
        previewController = GetChildById("jobPreview");
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);

        if (!cardMode || windowGroup == null || !windowGroup.isShowing)
            return;

        CardData previous = cachedData;
        CardData data = GetCardData();
        if (!ReferenceEquals(previous, data)) RefreshBindings();
        if (data != null && directionArrowController != null && directionArrowController.ViewComponent != null)
            if (directionArrowController.ViewComponent.Rotation != data.DirectionAngle)
                directionArrowController.ViewComponent.Rotation = data.DirectionAngle;

        ApplyPreviewCrop();
    }

    public override void OnOpen()
    {
        cachedQuest = null; cachedData = null; cachedJournal = null; nextSemanticCheck = 0;
        base.OnOpen();
    }

    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        bool hasResponse = CurrentResponse != null;
        bool isQuestCard = cardMode && CurrentResponse is DialogResponseQuest response && response.Quest != null;
        CardData data = isQuestCard ? GetCardData() : null;

        switch (bindingName)
        {
            case "jobcardvisible":
                value = isQuestCard.ToString();
                return true;
            case "cancelcardvisible":
                value = (cardMode && hasResponse && !isQuestCard).ToString();
                return true;
            case "standardrowvisible":
                value = (!cardMode && hasResponse).ToString();
                return true;
            case "jobpreviewvisible":
                value = (data != null && !string.IsNullOrEmpty(data.PreviewUri)).ToString();
                return true;
            case "jobplaceholdervisible":
                value = (data == null || string.IsNullOrEmpty(data.PreviewUri)).ToString();
                return true;
            case "jobplaceholdersprite":
                value = data != null ? data.PreviewPlaceholderSprite : "rb_preview_missing";
                return true;
            case "jobpreviewuri":
                string previewUri = data != null ? data.PreviewUri : string.Empty;
                PreparePreviewForUri(previewUri);
                value = previewUri;
                return true;
            case "jobbiome":
                value = data != null ? data.BiomeName : string.Empty;
                return true;
            case "jobbiomesprite":
                value = data != null ? data.BiomeSprite : "rb_biome_unknown";
                return true;
            case "jobbiomecolor":
                value = data != null ? data.BiomeColor : "220,220,210,255";
                return true;
            case "jobtier":
                value = data != null ? data.Tier.ToString() : string.Empty;
                return true;
            case "jobdistance":
                value = data != null ? data.Distance : string.Empty;
                return true;
            case "jobdistancedisplay":
                value = data != null ? data.DistanceDisplay : string.Empty;
                return true;
            case "jobdirectiontext":
                value = data != null ? data.DirectionText : string.Empty;
                return true;
            case "jobtypesprite":
                value = data != null ? data.JobTypeSprite : "rb_job_special";
                return true;
            case "jobtype":
                value = data != null ? data.JobType : string.Empty;
                return true;
            case "jobprefabname":
                value = data != null ? data.PrefabName : string.Empty;
                return true;
            case "jobcompletionhistory":
                value = data != null ? data.CompletionDisplay : string.Empty;
                return true;
            case "jobbackgroundsprite":
                value = data != null && data.InfestedVisible ? "rb_job_frame_infested" : "rb_job_frame_normal";
                return true;
            case "jobframesprite":
                value = data != null && data.InfestedVisible ? "rb_job_frame_infested_outline" : "rb_job_frame_normal_outline";
                return true;
            case "jobstripcolor":
                value = data != null && data.InfestedVisible ? "42,14,11,238" : "10,18,12,238";
                return true;
            case "jobinfestedvisible":
                value = (data != null && data.InfestedVisible).ToString();
                return true;
            case "joblinecolor":
                value = data != null && data.InfestedVisible ? "132,43,35,225" : "70,108,72,225";
                return true;
            case "jobhighlightvisible":
                value = (cardMode && hasResponse && (Selected || IsHovered)).ToString();
                return true;
            case "jobhighlightsprite":
                value = Selected ? "rb_job_card_selected" : "rb_job_card_hover";
                return true;
            case "jobactionbackgroundsprite":
                value = GetActionBackgroundSprite(CurrentResponse);
                return true;
            case "jobactionsprite":
                value = GetActionSprite(CurrentResponse);
                return true;
            case "jobactioncolor":
                value = GetActionColor(CurrentResponse);
                return true;
            case "jobtooltip":
                value = data != null ? BuildTooltip(data) : string.Empty;
                return true;
            default:
                return base.GetBindingValueInternal(ref value, bindingName);
        }
    }

    /// <summary>
    /// The 3.1 XUiV_Texture AutoUnload setter clears TextureUris while replacing an
    /// already-loaded external texture. That is safe for ordinary one-shot textures,
    /// but it breaks these reusable trader cards when Previous/Next Tier swaps the URI:
    /// the new prefab image loads, the old texture is auto-unloaded, and the URI is
    /// cleared out from underneath the binding.
    ///
    /// REBIRTH owns this one texture transition explicitly instead. Before a different
    /// preview URI is bound, dispose the old texture/path, reset crop state, then let
    /// the normal binding assign/load the new URI. templates.xml therefore keeps
    /// autounload disabled for jobPreview; cleanup happens here on every URI change.
    /// </summary>
    private void PreparePreviewForUri(string previewUri)
    {
        previewUri = previewUri ?? string.Empty;
        if (string.Equals(lastPreviewUri, previewUri, StringComparison.Ordinal))
            return;

        lastPreviewUri = previewUri;
        lastPreviewTexture = null;

        XUiV_Texture preview = previewController != null
            ? previewController.ViewComponent as XUiV_Texture
            : null;
        if (preview == null)
            return;

        // GetPreviewUri only supplies external JPEGs owned by this card. Native
        // UnloadTexture clears TextureUris, whose setter resets isExternalTexture
        // before native code tests it, incorrectly choosing Resources.UnloadAsset.
        // Detach first; shared popup views never own/dispose this texture.
        Texture previous = preview.Texture;
        preview.AutoUnload = false;
        preview.Texture = null;
        preview.TextureUris = null;
        if (previous != null) UnityEngine.Object.Destroy(previous);

        preview.UVRect = new Rect(0f, 0f, 1f, 1f);
    }

    /// <summary>
    /// XUi's normal keep-source-aspect mode letterboxes wide/tall prefab thumbnails.
    /// For job cards we instead center-crop the source UVs to the fixed 286x161 preview
    /// rectangle, preserving source aspect ratio while filling the entire card width.
    /// </summary>
    private void ApplyPreviewCrop()
    {
        XUiV_Texture preview = previewController != null ? previewController.ViewComponent as XUiV_Texture : null;
        if (preview == null)
            return;

        Texture texture = preview.Texture;
        if (texture == null)
        {
            lastPreviewTexture = null;
            return;
        }

        if (ReferenceEquals(lastPreviewTexture, texture))
            return;

        lastPreviewTexture = texture;
        preview.KeepSourceAspectRatio = false;
        preview.SourceAspectRatioRespectPivot = false;

        float sourceAspect = texture.height > 0 ? texture.width / (float)texture.height : PreviewTargetAspect;
        Rect uv = new Rect(0f, 0f, 1f, 1f);
        if (sourceAspect > PreviewTargetAspect)
        {
            float width = PreviewTargetAspect / sourceAspect;
            uv.x = (1f - width) * 0.5f;
            uv.width = width;
        }
        else if (sourceAspect < PreviewTargetAspect && sourceAspect > 0f)
        {
            float height = sourceAspect / PreviewTargetAspect;
            uv.y = (1f - height) * 0.5f;
            uv.height = height;
        }

        preview.UVRect = uv;
    }

    private CardData GetCardData()
    {
        DialogResponseQuest response = CurrentResponse as DialogResponseQuest;
        Quest quest = response != null ? response.Quest : null;
        if (quest == null)
            return null;

        QuestJournal journal = xui?.playerUI?.entityPlayer?.QuestJournal;
        bool hideIdentity = RebirthInfestedJobsRuntimePolicy.HideIdentity;
        bool sameContext = ReferenceEquals(cachedQuest, quest) && ReferenceEquals(cachedJournal, journal)
            && cachedHideIdentity == hideIdentity;
        if (sameContext && cachedData != null && Time.realtimeSinceStartup < nextSemanticCheck)
            return cachedData;
        nextSemanticCheck = Time.realtimeSinceStartup + .25f;
        int completionCount = RebirthTraderJobCompletionStats.GetCompletionCount(journal, quest);
        int semanticHash = ComputeQuestSemanticHash(quest, completionCount);
        if (sameContext && cachedData != null && cachedSemanticHash == semanticHash) return cachedData;

        cachedQuest = quest;
        cachedJournal = journal;
        cachedHideIdentity = hideIdentity;
        cachedSemanticHash = semanticHash;
        cachedData = BuildCardData(quest, completionCount);
        return cachedData;
    }

    private int ComputeQuestSemanticHash(Quest quest, int completionCount)
    {
        if(quest==null)return 0;
        int hash=17;
        hash=unchecked(hash*31+(quest.QuestClass!=null?(quest.QuestClass.ID??string.Empty).GetHashCode():0));
        hash=unchecked(hash*31+(quest.QuestClass!=null?(int)quest.QuestClass.DifficultyTier:0));
        hash=unchecked(hash*31+quest.GetLocation().GetHashCode());
        hash=unchecked(hash*31+(quest.GetPOIName()??string.Empty).GetHashCode());
        hash=unchecked(hash*31+completionCount);
        hash=unchecked(hash*31+(RebirthInfestedJobsRuntimePolicy.HideIdentity?1:0));
        return hash;
    }

    private CardData BuildCardData(Quest quest, int completionCount)
    {
        CardData data = new CardData();
        PrefabInstance prefab = ResolvePrefab(quest);
        Vector3 target = GetTargetPosition(quest, prefab);
        Vector3 origin = GetTraderPosition(quest);

        PopulateBiome(data, target);
        PopulateJobType(data, quest);

        bool isBuriedSupplies = string.Equals(data.JobTypeSprite, "rb_job_buried", StringComparison.Ordinal);
        if (isBuriedSupplies)
        {
            data.PreviewUri = string.Empty;
            data.PreviewPlaceholderSprite = "rb_preview_buried";
            data.PrefabName = Localization.Get("xuiRebirthTraderJobBuriedSupplies");
        }
        else
        {
            data.PreviewUri = GetPreviewUri(prefab);
            data.PreviewPlaceholderSprite = "rb_preview_missing";
            data.PrefabName = GetPrefabDisplayName(quest, prefab);
        }

        data.Tier = Math.Max(1, (int)quest.QuestClass.DifficultyTier);

        data.CompletionCount = completionCount;

        string completionFormat =
            Localization.Get("xuiRebirthTraderJobCompletedCount");
        if (string.IsNullOrEmpty(completionFormat) ||
            completionFormat == "xuiRebirthTraderJobCompletedCount")
            completionFormat = "Completed: {0}";

        data.CompletionDisplay =
            string.Format(completionFormat, data.CompletionCount);

        Vector2 delta = new Vector2(target.x - origin.x, target.z - origin.z);
        data.Distance = ValueDisplayFormatters.Distance(delta.magnitude);
        data.DirectionText = GetCompassDirection(delta);
        data.DistanceDisplay = data.Distance + (string.IsNullOrEmpty(data.DirectionText)
            ? string.Empty
            : " [C0E6BC](" + data.DirectionText + ")[-]");
        data.DirectionAngle = delta.sqrMagnitude > 0.001f
            ? -Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg
            : 0f;

        bool reallyInfested = RebirthInfestedJobsRuntimePolicy.IsInfestedId(quest.QuestClass.ID);
        data.InfestedVisible = reallyInfested && !RebirthInfestedJobsRuntimePolicy.HideIdentity;
        return data;
    }


    private static string GetCompassDirection(Vector2 delta)
    {
        if (delta.sqrMagnitude <= 0.001f)
            return string.Empty;

        float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
        if (angle < 0f)
            angle += 360f;

        string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        int index = Mathf.FloorToInt((angle + 22.5f) / 45f) & 7;
        return directions[index];
    }

    private static PrefabInstance ResolvePrefab(Quest quest)
    {
        if (quest == null)
            return null;
        if (quest.QuestPrefab != null)
            return quest.QuestPrefab;

        GameManager gm = GameManager.Instance;
        DynamicPrefabDecorator decorator = gm != null ? gm.GetDynamicPrefabDecorator() : null;
        if (decorator == null)
            return null;

        Vector3 location = quest.GetLocation();
        if (location == Vector3.zero)
            return null;

        PrefabInstance prefab = decorator.GetPrefabFromWorldPos((int)location.x, (int)location.z);
        if (prefab == null)
            prefab = decorator.GetPrefabFromWorldPosInside((int)location.x, (int)location.z);
        return prefab;
    }

    private static Vector3 GetTargetPosition(Quest quest, PrefabInstance prefab)
    {
        if (prefab != null)
        {
            Vector2 center = prefab.GetCenterXZ();
            return new Vector3(center.x, quest.Position.y, center.y);
        }

        Vector3 location = quest.GetLocation();
        if (location != Vector3.zero)
        {
            Vector3 size = quest.GetLocationSize();
            if (size != Vector3.zero)
                return new Vector3(location.x + size.x * 0.5f, location.y, location.z + size.z * 0.5f);
            return location;
        }

        return quest.Position;
    }

    private Vector3 GetTraderPosition(Quest quest)
    {
        if (xui != null && xui.Dialog != null && xui.Dialog.Respondent != null)
            return xui.Dialog.Respondent.position;

        Vector3 traderPosition;
        if (quest != null && quest.GetPositionData(out traderPosition, Quest.PositionDataTypes.TraderPosition))
            return traderPosition;
        if (quest != null && quest.GetPositionData(out traderPosition, Quest.PositionDataTypes.QuestGiver))
            return traderPosition;
        return Vector3.zero;
    }

    private static string GetPreviewUri(PrefabInstance prefab)
    {
        try
        {
            if (prefab == null || prefab.location == PathAbstractions.AbstractedLocation.None)
                return string.Empty;

            string path = prefab.location.FullPathNoExtension + ".jpg";
            if (!SdFile.Exists(path))
                return string.Empty;

            return "@" + new Uri(path).AbsoluteUri;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetPrefabDisplayName(Quest quest, PrefabInstance prefab)
    {
        string name = quest != null ? quest.GetPOIName() : string.Empty;
        if (!string.IsNullOrEmpty(name))
            return name;

        if (prefab != null)
        {
            if (prefab.prefab != null && !string.IsNullOrEmpty(prefab.prefab.LocalizedName))
                return prefab.prefab.LocalizedName;
            if (prefab.location != PathAbstractions.AbstractedLocation.None && !string.IsNullOrEmpty(prefab.location.Name))
                return Localization.Get(prefab.location.Name);
        }

        return Localization.Get("xuiRebirthTraderJobUnknownLocation");
    }

    private static void PopulateBiome(CardData data, Vector3 target)
    {
        BiomeDefinition biome = null;
        try
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world != null)
                biome = world.GetBiomeInWorld((int)target.x, (int)target.z);
        }
        catch
        {
        }

        if (biome == null)
        {
            data.BiomeName = Localization.Get("xuiRebirthTraderJobUnknownBiome");
            data.BiomeSprite = "rb_biome_unknown";
            data.BiomeColor = "210,210,200,255";
            return;
        }

        data.BiomeName = biome.LocalizedName;
        switch (biome.m_BiomeType)
        {
            case BiomeDefinition.BiomeType.Desert:
                data.BiomeSprite = "rb_biome_desert";
                data.BiomeColor = "226,173,86,255";
                break;
            case BiomeDefinition.BiomeType.Snow:
                data.BiomeSprite = "rb_biome_snow";
                data.BiomeColor = "211,234,255,255";
                break;
            case BiomeDefinition.BiomeType.Wasteland:
            case BiomeDefinition.BiomeType.city_wasteland:
            case BiomeDefinition.BiomeType.wasteland_hub:
            case BiomeDefinition.BiomeType.Radiated:
                data.BiomeSprite = "rb_biome_wasteland";
                data.BiomeColor = "205,193,137,255";
                break;
            case BiomeDefinition.BiomeType.burnt_forest:
                data.BiomeSprite = "rb_biome_burnt";
                data.BiomeColor = "239,127,61,255";
                break;
            case BiomeDefinition.BiomeType.Forest:
            case BiomeDefinition.BiomeType.PineForest:
            case BiomeDefinition.BiomeType.Plains:
            case BiomeDefinition.BiomeType.city:
                data.BiomeSprite = "rb_biome_forest";
                data.BiomeColor = "112,205,127,255";
                break;
            default:
                data.BiomeSprite = "rb_biome_unknown";
                data.BiomeColor = "210,210,200,255";
                break;
        }
    }

    private static void PopulateJobType(CardData data, Quest quest)
    {
        string id = quest != null && quest.QuestClass != null ? quest.QuestClass.ID ?? string.Empty : string.Empty;
        string lower = id.ToLowerInvariant();

        if (lower.Contains("restore_power"))
        {
            data.JobTypeSprite = "rb_job_power";
            data.JobType = Localization.Get("xuiRebirthTraderJobRestorePower");
        }
        else if (lower.Contains("buried"))
        {
            data.JobTypeSprite = "rb_job_buried";
            data.JobType = Localization.Get("xuiRebirthTraderJobBuriedSupplies");
        }
        else if (lower.Contains("fetch_clear"))
        {
            data.JobTypeSprite = "rb_job_fetch_clear";
            data.JobType = Localization.Get("xuiRebirthTraderJobFetchClear");
        }
        else if (lower.Contains("fetch"))
        {
            data.JobTypeSprite = "rb_job_fetch";
            data.JobType = Localization.Get("xuiRebirthTraderJobFetch");
        }
        else if (lower.Contains("clear"))
        {
            data.JobTypeSprite = "rb_job_clear";
            data.JobType = Localization.Get("xuiRebirthTraderJobClear");
        }
        else
        {
            data.JobTypeSprite = "rb_job_special";
            data.JobType = Localization.Get("xuiRebirthTraderJobSpecial");
        }
    }

    private static string GetActionBackgroundSprite(DialogResponse response)
    {
        string id = response != null ? response.ID ?? string.Empty : string.Empty;
        if (id.Equals("nevermind", StringComparison.OrdinalIgnoreCase))
            return "rb_job_command_cancel";
        return "rb_job_command_normal";
    }

    private static string GetActionSprite(DialogResponse response)
    {
        string id = response != null ? response.ID ?? string.Empty : string.Empty;
        if (id.StartsWith("jobsprev", StringComparison.OrdinalIgnoreCase))
            return "rb_nav_left";
        if (id.StartsWith("jobsnext", StringComparison.OrdinalIgnoreCase))
            return "rb_nav_right";
        if (id.Equals("nevermind", StringComparison.OrdinalIgnoreCase))
            return "rb_close";
        return "rb_job_special";
    }

    private static string GetActionColor(DialogResponse response)
    {
        string id = response != null ? response.ID ?? string.Empty : string.Empty;
        if (id.Equals("nevermind", StringComparison.OrdinalIgnoreCase))
            return "225,225,215,255";
        return "180,220,185,255";
    }

    private static string BuildTooltip(CardData data)
    {
        if (data == null)
            return string.Empty;

        return data.JobType + " - " + data.PrefabName + "\n" +
               data.BiomeName + " | " + Localization.Get("xuiTier") + " " + data.Tier + " | " +
               data.Distance + (string.IsNullOrEmpty(data.DirectionText) ? string.Empty : " (" + data.DirectionText + ")") + "\n" +
               data.CompletionDisplay;
    }
}
