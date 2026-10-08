using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable


/// <summary>
/// REBIRTH party HUD row. It deliberately inherits the native PartyEntry so
/// name/health/party color/leader state remain authoritative, but corrects the
/// arrow orientation for rb_direction_arrow and supplies an SDCS head portrait.
/// </summary>
[Preserve]
public class XUiC_RebirthHudPartyEntry : XUiC_PartyEntry
{
    private XUiV_Texture portraitTexture;
    private XUiV_Sprite portraitFallback;
    private XUiV_Sprite rebirthArrowContent;
    private RenderTextureSystem portraitRenderSystem;
    private Texture2D portraitSnapshot;
    private GameObject portraitRig;
    private SDCSUtils.TransformCatalog portraitBoneCatalog;
    private EModelSDCS portraitModel;
    private float portraitRetryAt;
    private int portraitFailureCount;
    private string portraitLastFailure = string.Empty;
    private float rebirthArrowRotation;
    private bool hasRebirthArrowRotation;
    private int portraitAppearanceSignature = int.MinValue;
    private float portraitAppearanceCheckAt;

    public override void Init()
    {
        base.Init();

        XUiController portraitController = GetChildById("playerPortrait");
        if (portraitController != null)
            portraitTexture = portraitController.ViewComponent as XUiV_Texture;

        XUiController fallbackController = GetChildById("playerPortraitFallback");
        if (fallbackController != null)
            portraitFallback = fallbackController.ViewComponent as XUiV_Sprite;

        XUiController arrowController = GetChildById("arrowContent");
        if (arrowController != null)
            rebirthArrowContent = arrowController.ViewComponent as XUiV_Sprite;

        SetPortraitVisibility(false);
    }

    internal void SetHudPlayer(EntityPlayer player)
    {
        if ((UnityEngine.Object)Player == (UnityEngine.Object)player)
        {
            if ((UnityEngine.Object)player == (UnityEngine.Object)null)
            {
                SetPortraitVisibility(false);
                if (ViewComponent != null)
                    ViewComponent.IsVisible = false;
            }
            return;
        }

        CleanupPortrait();
        SetPlayer(player);
        if (ViewComponent != null)
            ViewComponent.IsVisible = (UnityEngine.Object)player != (UnityEngine.Object)null;
        if ((UnityEngine.Object)player != (UnityEngine.Object)null)
            RefreshBindings();
        portraitRetryAt = 0f;
        portraitFailureCount = 0;
        portraitLastFailure = string.Empty;
        portraitAppearanceCheckAt = 0f;
        portraitAppearanceSignature = int.MinValue;
        hasRebirthArrowRotation = false;
        IsDirty = true;

        if ((UnityEngine.Object)player != (UnityEngine.Object)null)
            TryBuildPortrait();
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);

        if ((UnityEngine.Object)Player == (UnityEngine.Object)null || !XUi.IsGameRunning())
            return;

        EModelSDCS currentModel = Player.emodel as EModelSDCS;
        bool portraitNeedsRefresh = (UnityEngine.Object)currentModel != (UnityEngine.Object)portraitModel ||
                                    (UnityEngine.Object)portraitModel == (UnityEngine.Object)null;

        // A party member can change armor/cosmetics while remaining the same
        // EntityPlayer/EModelSDCS instance.  Check the visible-equipment signature
        // at a low cadence and rebuild only when appearance data actually changes.
        if (Time.time >= portraitAppearanceCheckAt)
        {
            portraitAppearanceCheckAt = Time.time + 0.75f;
            int currentAppearanceSignature = ComputePortraitAppearanceSignature(Player);
            if (currentAppearanceSignature != portraitAppearanceSignature)
                portraitNeedsRefresh = true;
        }

        if (portraitNeedsRefresh && Time.time >= portraitRetryAt)
            TryBuildPortrait();

        EntityPlayerLocal localPlayer = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if ((UnityEngine.Object)localPlayer == (UnityEngine.Object)null || rebirthArrowContent == null || rebirthArrowContent.UiTransform == null)
            return;

        // Native PartyEntry rotates ui_game_symbol_map_player_arrow with an extra
        // -180 degree artwork compensation. REBIRTH uses rb_direction_arrow, the
        // same up-authored arrow used by the Companions window, so replace the
        // final native rotation with the shared REBIRTH relative-bearing result.
        float targetRotation = RebirthCompanionService.DirectionAngle(localPlayer, Player.position);
        if (!hasRebirthArrowRotation)
        {
            rebirthArrowRotation = targetRotation;
            hasRebirthArrowRotation = true;
        }
        else
        {
            float delta = Mathf.DeltaAngle(rebirthArrowRotation, targetRotation);
            rebirthArrowRotation += delta * Mathf.Clamp01(_dt * 3f);
        }

        rebirthArrowContent.UiTransform.localEulerAngles = new Vector3(0f, 0f, rebirthArrowRotation);
    }

    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        if (bindingName == "distancecolor")
        {
            value = (UnityEngine.Object)Player == (UnityEngine.Object)null
                ? "232,232,232,255"
                : (distance > 100f ? "160,160,160,250" : "232,232,232,255");
            return true;
        }

        return base.GetBindingValueInternal(ref value, bindingName);
    }

    public override void OnClose()
    {
        CleanupPortrait();
        base.OnClose();
    }

    public override void Cleanup()
    {
        CleanupPortrait();
        base.Cleanup();
    }

    private bool TryBuildPortrait()
    {
        portraitRetryAt = Time.time + 1f;

        if (portraitTexture == null || (UnityEngine.Object)Player == (UnityEngine.Object)null)
        {
            SetPortraitVisibility(false);
            return false;
        }

        EModelSDCS model = Player.emodel as EModelSDCS;
        Archetype archetype = (UnityEngine.Object)model != (UnityEngine.Object)null ? model.Archetype : null;
        if ((UnityEngine.Object)model == (UnityEngine.Object)null || archetype == null)
        {
            SetPortraitVisibility(false);
            return false;
        }

        CleanupPortrait();

        try
        {
            // Use the entity-aware 3.1 SDCS UI path so the portrait reflects the
            // party member's live SDCS body, hair and equipped cosmetics.
            SDCSUtils.CreateVizUI(archetype, ref portraitRig, ref portraitBoneCatalog, Player, false);
            if ((UnityEngine.Object)portraitRig == (UnityEngine.Object)null)
            {
                SetPortraitVisibility(false);
                return false;
            }

            Animator animator = portraitRig.GetComponentInChildren<Animator>();
            if ((UnityEngine.Object)animator != (UnityEngine.Object)null)
                animator.Update(0f);

            portraitRenderSystem = new RenderTextureSystem();
            GameObject target = new GameObject("RebirthPartyPortraitTarget_" + Player.entityId);
            portraitRenderSystem.Create(
                "rebirthPartyPortrait_" + Player.entityId,
                target,
                Vector3.zero,
                Vector3.zero,
                new Vector2i(64, 64),
                true);

            portraitRig.transform.SetParent(portraitRenderSystem.TargetGO.transform, false);
            portraitRig.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            portraitRig.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            // Tighter than the stock 12-degree Head zoom because the HUD portrait
            // is only 36x36.  Mirror the stock three-quarter camera to the opposite
            // side so the player faces the same general direction as the dog art.
            portraitRenderSystem.TargetGO.transform.localPosition = new Vector3(-0.015f, -0.78f, 2.14f);
            Utils.SetLayerRecursively(portraitRenderSystem.TargetGO, 11);

            Camera camera = portraitRenderSystem.CameraGO.GetComponent<Camera>();
            if ((UnityEngine.Object)camera == (UnityEngine.Object)null)
            {
                CleanupPortrait();
                return false;
            }

            // Match XUiC_SDCSPreviewWindow.setToHeadZoom exactly: the head
            // framing uses the render-camera origin.  The previous HUD portrait
            // incorrectly reused the full-body Y=-0.75 camera offset, which
            // framed the character's lower torso instead of the head.
            camera.transform.localPosition = Vector3.zero;
            camera.transform.localRotation = Quaternion.Euler(1.5f, 0f, 0f);
            camera.orthographic = false;
            camera.fieldOfView = 9f;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);

            Light light = portraitRenderSystem.LightGO != null
                ? portraitRenderSystem.LightGO.GetComponent<Light>()
                : null;
            if ((UnityEngine.Object)light != (UnityEngine.Object)null)
            {
                portraitRenderSystem.LightGO.transform.localPosition = new Vector3(0.35f, 0.45f, 0.5f);
                light.type = LightType.Point;
                light.range = 20f;
                light.intensity = 1.5f;
                light.color = new Color(1f, 0.92f, 0.84f, 1f);
            }

            // Use the stock 30-degree three-quarter amount, mirrored so the face
            // points toward the same side as the REBIRTH dog portraits instead of
            // appearing as a left-facing profile.
            camera.transform.RotateAround(portraitRig.transform.position, Vector3.up, 30f);
            if (portraitRenderSystem.LightGO != null)
                portraitRenderSystem.LightGO.transform.RotateAround(portraitRig.transform.position, Vector3.up, 30f);

            CharacterGazeController gazeController = portraitRig.GetComponentInChildren<CharacterGazeController>();
            if ((UnityEngine.Object)gazeController != (UnityEngine.Object)null)
                gazeController.SnapNextUpdate();

            // Apply the snapped gaze before taking the one-frame portrait snapshot.
            if ((UnityEngine.Object)animator != (UnityEngine.Object)null)
                animator.Update(0f);

            // Snapshot the SDCS head once, then release the temporary camera/rig.
            // Keeping a live RenderTexture camera per party member would add a
            // permanent render cost to the gameplay HUD.
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = portraitRenderSystem.RenderTex;
                portraitSnapshot = new Texture2D(
                    portraitRenderSystem.RenderTex.width,
                    portraitRenderSystem.RenderTex.height,
                    TextureFormat.RGBA32,
                    false);
                portraitSnapshot.name = "RebirthPartyPortrait_" + Player.entityId;
                portraitSnapshot.ReadPixels(
                    new Rect(0f, 0f, portraitRenderSystem.RenderTex.width, portraitRenderSystem.RenderTex.height),
                    0,
                    0);
                portraitSnapshot.Apply();
                portraitSnapshot.filterMode = FilterMode.Bilinear;
            }
            finally
            {
                RenderTexture.active = previousActive;
            }

            portraitTexture.Texture = portraitSnapshot;
            portraitModel = model;
            portraitAppearanceSignature = ComputePortraitAppearanceSignature(Player);
            portraitAppearanceCheckAt = Time.time + 0.75f;
            SetPortraitVisibility(true);
            ReleasePortraitRenderObjects();
            portraitFailureCount = 0;
            portraitLastFailure = string.Empty;
            portraitRetryAt = 0f;
            return true;
        }
        catch (Exception ex)
        {
            portraitFailureCount = Mathf.Min(portraitFailureCount + 1, 8);
            portraitRetryAt = Time.time + Mathf.Min(30f, Mathf.Pow(2f, portraitFailureCount - 1));
            string failure = ex.GetType().Name + ": " + ex.Message;
            if (!string.Equals(failure, portraitLastFailure, StringComparison.Ordinal))
            {
                portraitLastFailure = failure;
                Log.Warning("[REBIRTH Companion HUD] Could not build SDCS party portrait for entity " + Player.entityId + "; retryIn=" + (portraitRetryAt - Time.time).ToString("0.#") + "s: " + failure);
            }
            CleanupPortrait();
            SetPortraitVisibility(false);
            return false;
        }
    }

    private static int ComputePortraitAppearanceSignature(EntityPlayer player)
    {
        if ((UnityEngine.Object)player == (UnityEngine.Object)null)
            return 0;

        unchecked
        {
            int hash = 17;
            EModelSDCS model = player.emodel as EModelSDCS;
            Archetype archetype = (UnityEngine.Object)model != (UnityEngine.Object)null ? model.Archetype : null;
            if (archetype != null)
            {
                hash = hash * 31 + archetype.GetHashCode();
                hash = hash * 31 + StableStringHash(archetype.Name);
                hash = hash * 31 + StableStringHash(archetype.Race);
                hash = hash * 31 + archetype.Variant;
                hash = hash * 31 + StableStringHash(archetype.Hair);
                hash = hash * 31 + StableStringHash(archetype.HairColor);
                hash = hash * 31 + StableStringHash(archetype.MustacheName);
                hash = hash * 31 + StableStringHash(archetype.ChopsName);
                hash = hash * 31 + StableStringHash(archetype.BeardName);
                hash = hash * 31 + StableStringHash(archetype.EyeColorName);
                hash = hash * 31 + (archetype.IsMale ? 1 : 0);
            }
            else
            {
                hash *= 31;
            }

            Equipment equipment = player.equipment;
            if (equipment == null)
                return hash;

            int slotCount = equipment.GetSlotCount();
            hash = hash * 31 + slotCount;
            for (int i = 0; i < slotCount; i++)
            {
                ItemValue item = equipment.GetSlotItem(i);
                hash = hash * 31 + (item != null ? item.type : 0);

                ItemClass cosmetic = equipment.GetCosmeticSlot(i, false);
                if (cosmetic != null)
                {
                    string cosmeticName = cosmetic.GetItemName();
                    hash = hash * 31 + StableStringHash(cosmeticName);
                }
                else
                {
                    hash *= 31;
                }
            }

            return hash;
        }
    }

    private static int StableStringHash(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        unchecked
        {
            int hash = 23;
            for (int i = 0; i < value.Length; i++)
                hash = hash * 31 + value[i];
            return hash;
        }
    }

    private void CleanupPortrait()
    {
        if (portraitTexture != null)
            portraitTexture.Texture = null;

        if ((UnityEngine.Object)portraitSnapshot != (UnityEngine.Object)null)
            UnityEngine.Object.Destroy(portraitSnapshot);
        portraitSnapshot = null;

        ReleasePortraitRenderObjects();
        portraitModel = null;
        SetPortraitVisibility(false);
    }

    private void ReleasePortraitRenderObjects()
    {
        if ((UnityEngine.Object)portraitRig != (UnityEngine.Object)null)
        {
            try
            {
                SDCSUtils.UnloadViz(portraitRig);
            }
            catch
            {
                UnityEngine.Object.Destroy(portraitRig);
            }
        }

        portraitRig = null;
        portraitBoneCatalog = null;

        if (portraitRenderSystem != null)
        {
            portraitRenderSystem.Cleanup();
            portraitRenderSystem = null;
        }
    }

    private void SetPortraitVisibility(bool hasPortrait)
    {
        bool hasPlayer = (UnityEngine.Object)Player != (UnityEngine.Object)null;
        if (portraitTexture != null)
            portraitTexture.IsVisible = hasPlayer && hasPortrait;
        if (portraitFallback != null)
            portraitFallback.IsVisible = hasPlayer && !hasPortrait;
    }
}

/// <summary>
/// REBIRTH HUD entry for a following companion.
///
/// This intentionally does NOT inherit XUiC_CompanionEntry. The native 3.1
/// controller assumes a native companion-entry template/stat layout and its
/// RefreshFill path can dereference state that is not valid for every REBIRTH
/// companion type (notably the drone path used by the custom HUD).
///
/// The REBIRTH entry owns only the fields it actually renders: icon, name,
/// distance, current/max health, health fill, and direction arrow.
/// </summary>
[Preserve]
public class XUiC_RebirthHudCompanionEntry : XUiController
{
    private EntityAlive companion;
    private XUiV_Sprite barHealth;
    private XUiV_Sprite arrowContent;
    private float distance;
    private float updateTime;
    private float arrowRotation;
    private float lastArrowRotation;

    public EntityAlive Companion => companion;

    public override void Init()
    {
        base.Init();

        XUiController healthController = GetChildById("BarHealth");
        if (healthController != null)
            barHealth = healthController.ViewComponent as XUiV_Sprite;

        XUiController arrowController = GetChildById("arrowContent");
        if (arrowController != null)
            arrowContent = arrowController.ViewComponent as XUiV_Sprite;

        IsDirty = true;
    }

    internal void SetCompanion(EntityAlive entity)
    {
        if ((UnityEngine.Object)companion == (UnityEngine.Object)entity)
        {
            // A shared HUD slot overlays party and companion row controllers.
            // Even when the logical companion value is already null, explicitly
            // enforce the hidden state so a recycled slot can never leave stale
            // companion visuals underneath a party row.
            if ((UnityEngine.Object)entity == (UnityEngine.Object)null)
            {
                if (ViewComponent != null)
                    ViewComponent.IsVisible = false;
                if (barHealth != null)
                    barHealth.Fill = 0f;
            }
            return;
        }

        companion = entity;
        if (ViewComponent != null)
            ViewComponent.IsVisible = (UnityEngine.Object)entity != (UnityEngine.Object)null;
        distance = 0f;
        updateTime = 0f;
        arrowRotation = 0f;
        lastArrowRotation = 0f;
        IsDirty = true;
        RefreshBindings();

        if (barHealth != null && (UnityEngine.Object)companion == (UnityEngine.Object)null)
            barHealth.Fill = 0f;
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);

        if ((UnityEngine.Object)companion == (UnityEngine.Object)null || !XUi.IsGameRunning())
            return;

        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if ((UnityEngine.Object)player == (UnityEngine.Object)null)
            return;

        int maxHealth = companion.GetMaxHealth();
        if (barHealth != null)
        {
            float fill = maxHealth > 0 ? Mathf.Clamp01((float)companion.Health / (float)maxHealth) : 0f;
            barHealth.Fill = fill;
        }

        distance = (companion.GetPosition() - player.GetPosition()).magnitude;

        // Distance is presentation data, not an every-frame invalidation source. The arrow and
        // health bar remain smooth, while text/name/icon bindings refresh at the bounded cadence.
        if (Time.time >= updateTime || IsDirty)
        {
            updateTime = Time.time + 0.5f;
            RefreshBindings();
            IsDirty = false;
        }

        if (arrowContent == null || arrowContent.UiTransform == null)
            return;

        // Use the exact same relative-bearing calculation as the Companions window.
        // The HUD template uses the same rb_direction_arrow artwork, so no stock
        // PartyEntry 180-degree artwork compensation belongs on companion rows.
        arrowRotation = RebirthCompanionService.DirectionAngle(player, companion.position);
        float delta = Mathf.DeltaAngle(lastArrowRotation, arrowRotation);
        lastArrowRotation += delta * Mathf.Clamp01(_dt * 3f);
        arrowContent.UiTransform.localEulerAngles = new Vector3(0f, 0f, lastArrowRotation);
    }

    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        switch (bindingName)
        {
            case "arrowcolor":
            {
                EntityPlayerLocal local = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
                Color32 c = (Color32)RebirthCompanionColorService.GetColor(companion, local);
                const float arrowBrightness = 0.90f;
                byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * arrowBrightness), 0, 255);
                byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * arrowBrightness), 0, 255);
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * arrowBrightness), 0, 255);
                value = r + "," + g + "," + b + "," + c.a;
                return true;
            }

            case "distance":
                if ((UnityEngine.Object)companion == (UnityEngine.Object)null)
                {
                    value = string.Empty;
                    return true;
                }
                value = ValueDisplayFormatters.Distance(distance);
                return true;

            case "distancecolor":
                value = (UnityEngine.Object)companion == (UnityEngine.Object)null
                    ? "232,232,232,255"
                    : (distance > 100f ? "160,160,160,250" : "232,232,232,255");
                return true;

            case "healthcurrentwithmax":
                if ((UnityEngine.Object)companion == (UnityEngine.Object)null)
                {
                    value = string.Empty;
                    return true;
                }
                value = companion.Health + "/" + companion.GetMaxHealth();
                return true;

            case "healthfill":
                if ((UnityEngine.Object)companion == (UnityEngine.Object)null)
                {
                    value = "0";
                    return true;
                }
                int maxHealth = companion.GetMaxHealth();
                value = (maxHealth > 0 ? Mathf.Clamp01((float)companion.Health / (float)maxHealth) : 0f).ToCultureInvariantString();
                return true;

            case "name":
                if ((UnityEngine.Object)companion == (UnityEngine.Object)null)
                {
                    value = string.Empty;
                    return true;
                }
                EntityDrone namedDrone = companion as EntityDrone;
                value = namedDrone != null ? RebirthDroneRenameService.GetDisplayName(namedDrone) : companion.EntityName;
                return true;

            case "partyvisible":
                value = ((UnityEngine.Object)companion != (UnityEngine.Object)null &&
                         xui != null && xui.playerUI != null && xui.playerUI.entityPlayer != null &&
                         !xui.playerUI.entityPlayer.IsDead()).ToString();
                return true;

            case "showarrow":
                value = ((UnityEngine.Object)companion != (UnityEngine.Object)null && companion.IsAlive()).ToString();
                return true;

            case "companionitemiconvisible":
            {
                string atlas;
                string icon;
                TryResolveCompanionIcon(out atlas, out icon);
                value = string.Equals(atlas, "ItemIconAtlas", StringComparison.Ordinal) ? "true" : "false";
                return true;
            }

            case "companionuiiconvisible":
            {
                string atlas;
                string icon;
                TryResolveCompanionIcon(out atlas, out icon);
                value = string.Equals(atlas, "ItemIconAtlas", StringComparison.Ordinal) ? "false" : "true";
                return true;
            }

            case "companionitemicon":
            {
                string atlas;
                string icon;
                TryResolveCompanionIcon(out atlas, out icon);
                value = string.Equals(atlas, "ItemIconAtlas", StringComparison.Ordinal) ? icon : string.Empty;
                return true;
            }

            case "companionuiicon":
            {
                string atlas;
                string icon;
                TryResolveCompanionIcon(out atlas, out icon);
                value = string.Equals(atlas, "ItemIconAtlas", StringComparison.Ordinal) ? string.Empty : icon;
                return true;
            }

            default:
                return base.GetBindingValueInternal(ref value, bindingName);
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
        IsDirty = true;
    }

    public override void OnClose()
    {
        SetCompanion(null);
        base.OnClose();
    }

    private void TryResolveCompanionIcon(out string atlas, out string icon)
    {
        atlas = "UIAtlas";
        icon = "ui_game_symbol_allies";

        if ((UnityEngine.Object)companion == (UnityEngine.Object)null)
            return;

        EntityDrone drone = companion as EntityDrone;
        if (drone != null)
        {
            ItemClass droneItem = ItemClass.GetItemClass("gunBotT3JunkDrone", false);
            atlas = "ItemIconAtlas";
            icon = droneItem != null ? droneItem.GetIconName() : "gunBotT3JunkDrone";
            return;
        }

        EntityRebirthDogCompanion dog = companion as EntityRebirthDogCompanion;
        if (dog != null)
        {
            RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
            RebirthDogBreedDefinition breed;
            if (state != null && !state.StableId.IsEmpty &&
                RebirthDogStateService.TryGetBreed(state.StableId, out breed) &&
                breed != null && !string.IsNullOrEmpty(breed.IconName))
            {
                atlas = "ItemIconAtlas";
                icon = breed.IconName;
                return;
            }

            atlas = "UIAtlas";
            icon = "ui_game_symbol_tracking_wolf";
            return;
        }

        EntityRebirthNPC npc = companion as EntityRebirthNPC;
        if (npc != null)
        {
            atlas = "UIAtlas";
            icon = "ui_game_symbol_players";
        }
    }
}

internal static class RebirthSimpleHudHealthBar
{
    internal const int FullHeight = 42;
    internal const int BottomY = -44;

    internal static void Update(XUiController controller, XUiV_Sprite sprite, int x, float fill, ref int lastHeight)
    {
        if (controller == null || controller.ViewComponent == null || sprite == null)
            return;

        int height = Mathf.Clamp(Mathf.RoundToInt(FullHeight * Mathf.Clamp01(fill)), 0, FullHeight);
        if (height == lastHeight)
            return;
        lastHeight = height;
        sprite.IsVisible = height > 0;
        if (height <= 0)
            return;

        controller.ViewComponent.Size = new Vector2i(5, height);
        if (sprite.Sprite != null)
            sprite.Sprite.SetDimensions(5, height);
        int y = BottomY + height;
        controller.ViewComponent.Position = new Vector2i(x, y);
        if (controller.ViewComponent.UiTransform != null)
        {
            Vector3 local = controller.ViewComponent.UiTransform.localPosition;
            controller.ViewComponent.UiTransform.localPosition = new Vector3((float)x, (float)y, local.z);
        }
    }
}

/// <summary>
/// Simple player card: SDCS portrait plus vertical health, distance and direction.
/// Names and native party-status icons are intentionally omitted from this style.
/// </summary>
[Preserve]
public class XUiC_RebirthHudPartySimpleEntry : XUiC_RebirthHudPartyEntry
{
    private XUiController simpleHealthController;
    private XUiV_Sprite simpleHealthFill;
    private int simpleHealthX = 2;
    private int lastSimpleHealthHeight = -1;

    public override void Init()
    {
        base.Init();
        simpleHealthController = GetChildById("simpleHealthFill");
        if (simpleHealthController != null)
        {
            simpleHealthFill = simpleHealthController.ViewComponent as XUiV_Sprite;
            if (simpleHealthController.ViewComponent != null)
                simpleHealthX = simpleHealthController.ViewComponent.Position.x;
        }
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
        float fill = (UnityEngine.Object)Player != (UnityEngine.Object)null
            ? Player.Stats.Health.ValuePercentUI
            : 0f;
        RebirthSimpleHudHealthBar.Update(simpleHealthController, simpleHealthFill, simpleHealthX, fill, ref lastSimpleHealthHeight);
    }
}

/// <summary>
/// Simple companion card counterpart. It deliberately keeps no name or numeric
/// health text so the card can remain narrow enough to accumulate horizontally.
/// </summary>
[Preserve]
public class XUiC_RebirthHudCompanionSimpleEntry : XUiC_RebirthHudCompanionEntry
{
    private XUiController simpleHealthController;
    private XUiV_Sprite simpleHealthFill;
    private int simpleHealthX = 2;
    private int lastSimpleHealthHeight = -1;

    public override void Init()
    {
        base.Init();
        simpleHealthController = GetChildById("simpleHealthFill");
        if (simpleHealthController != null)
        {
            simpleHealthFill = simpleHealthController.ViewComponent as XUiV_Sprite;
            if (simpleHealthController.ViewComponent != null)
                simpleHealthX = simpleHealthController.ViewComponent.Position.x;
        }
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
        int maxHealth = (UnityEngine.Object)Companion != (UnityEngine.Object)null ? Companion.GetMaxHealth() : 0;
        float fill = maxHealth > 0 ? Mathf.Clamp01((float)Companion.Health / (float)maxHealth) : 0f;
        RebirthSimpleHudHealthBar.Update(simpleHealthController, simpleHealthFill, simpleHealthX, fill, ref lastSimpleHealthHeight);
    }
}

/// <summary>
/// One compact HUD slot that can display either a native PartyEntry or a
/// REBIRTH companion entry. Keeping both row types inside the same slot lets a
/// single 4x2 grid use all eight positions for any party/companion mixture.
/// </summary>
[Preserve]
public class XUiC_RebirthPartyCompanionHudSlot : XUiController
{
    private XUiC_RebirthHudPartyEntry partyEntry;
    private XUiC_RebirthHudCompanionEntry companionEntry;

    public EntityPlayer PartyPlayer => partyEntry != null ? partyEntry.Player : null;
    public EntityAlive Companion => companionEntry != null ? companionEntry.Companion : null;

    public override void Init()
    {
        base.Init();
        partyEntry = GetChildByType<XUiC_RebirthHudPartyEntry>();
        companionEntry = GetChildByType<XUiC_RebirthHudCompanionEntry>();

        // Force the two overlaid row types into a known hidden state before the
        // combined list assigns this slot for the first time.
        if (partyEntry != null)
        {
            partyEntry.SetHudPlayer(null);
        }
        if (companionEntry != null)
        {
            companionEntry.SetCompanion(null);
            companionEntry.RefreshBindings();
        }
    }

    internal void SetPartyPlayer(EntityPlayer player)
    {
        if (companionEntry != null)
            companionEntry.SetCompanion(null);

        if (partyEntry != null && (UnityEngine.Object)partyEntry.Player == (UnityEngine.Object)player)
        {
            // Reassert visibility even when the logical assignment is unchanged.
            // This prevents an overlaid companion view from surviving a slot
            // transition or disconnect/repack edge case.
            if (partyEntry.ViewComponent != null)
                partyEntry.ViewComponent.IsVisible = (UnityEngine.Object)player != (UnityEngine.Object)null;
            return;
        }

        if (partyEntry != null)
            partyEntry.SetHudPlayer(player);
    }

    internal void SetCompanion(EntityAlive companion)
    {
        if (companionEntry != null && (UnityEngine.Object)companionEntry.Companion == (UnityEngine.Object)companion &&
            (partyEntry == null || (UnityEngine.Object)partyEntry.Player == (UnityEngine.Object)null))
            return;

        ClearPartyPlayer();
        if (companionEntry != null)
            companionEntry.SetCompanion(companion);
    }

    internal void Clear()
    {
        ClearPartyPlayer();
        if (companionEntry != null)
        {
            companionEntry.SetCompanion(null);
            if (companionEntry.ViewComponent != null)
                companionEntry.ViewComponent.IsVisible = false;
        }
    }

    private void ClearPartyPlayer()
    {
        if (partyEntry == null)
            return;

        if ((UnityEngine.Object)partyEntry.Player != (UnityEngine.Object)null)
            partyEntry.SetHudPlayer(null);

        if (partyEntry.ViewComponent != null)
            partyEntry.ViewComponent.IsVisible = false;
    }
}

/// <summary>
/// Combined compact Party + Following Companion HUD population controller.
///
/// The eight slots are ordered exactly as the vertical 4x2 XML grid lays them
/// out: four down the left column, then four down the right. Remote party
/// members are placed first and following companions consume every remaining
/// slot. This supports 4+4, 2+6, 0+8 and every other combination without
/// reserving unused rows for either list.
/// </summary>
[Preserve]
public class XUiC_RebirthPartyCompanionHudList : XUiController
{
    private const int MaximumEntries = 8;
    private readonly List<XUiC_RebirthPartyCompanionHudSlot> slots = new List<XUiC_RebirthPartyCompanionHudSlot>();
    private float refreshTimer;
    private int lastPartySignature = int.MinValue;

    public override void Init()
    {
        base.Init();
        XUiC_RebirthPartyCompanionHudSlot[] found = GetChildrenByType<XUiC_RebirthPartyCompanionHudSlot>();
        if (found != null)
            slots.AddRange(found);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        refreshTimer = 0f;
        lastPartySignature = int.MinValue;
        bool active = RebirthCompanionCardStyleRuntime.CurrentStyle == RebirthCompanionCardStyle.Default;
        if (ViewComponent != null)
            ViewComponent.IsVisible = active;
        if (active)
            RefreshList();
        else
            for (int i = 0; i < slots.Count; i++) slots[i].Clear();
    }

    public override void OnClose()
    {
        for (int i = 0; i < slots.Count; i++)
            slots[i].Clear();
        base.OnClose();
    }

    public override void Update(float _dt)
    {
        bool active = RebirthCompanionCardStyleRuntime.CurrentStyle == RebirthCompanionCardStyle.Default;
        if (!active)
        {
            if (ViewComponent != null && ViewComponent.IsVisible)
            {
                ViewComponent.IsVisible = false;
                for (int i = 0; i < slots.Count; i++)
                    slots[i].Clear();
            }
            return;
        }

        if (ViewComponent != null && !ViewComponent.IsVisible)
        {
            ViewComponent.IsVisible = true;
            refreshTimer = 0f;
            lastPartySignature = int.MinValue;
        }

        // Detect party membership changes before child PartyEntry controllers
        // update. This prevents a removed member from briefly retaining a stale
        // Party reference while its bindings/arrow color refresh.
        int partySignature = ComputePartySignature();
        refreshTimer -= _dt;
        if (refreshTimer <= 0f || partySignature != lastPartySignature)
        {
            refreshTimer = 0.5f;
            lastPartySignature = partySignature;
            RefreshList();
        }

        base.Update(_dt);
    }

    private void RefreshList()
    {
        if (xui == null || xui.playerUI == null)
            return;

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if ((UnityEngine.Object)player == (UnityEngine.Object)null)
            return;

        int slotIndex = 0;

        // Match native PartyEntryList ordering and exclude the local player.
        Party party = player.Party;
        if (party != null && party.MemberList != null)
        {
            for (int i = 0; i < party.MemberList.Count && slotIndex < MaximumEntries && slotIndex < slots.Count; i++)
            {
                EntityPlayer member = party.MemberList[i];
                if (!IsActiveRemotePartyMember(player, member))
                    continue;

                slots[slotIndex++].SetPartyPlayer(member);
            }
        }

        if (slotIndex < MaximumEntries && slotIndex < slots.Count)
        {
            List<EntityAlive> following = RebirthFollowingCompanionHudService.GetFollowing(player);
            for (int i = 0; i < following.Count && slotIndex < MaximumEntries && slotIndex < slots.Count; i++)
                slots[slotIndex++].SetCompanion(following[i]);
        }

        for (; slotIndex < slots.Count; slotIndex++)
            slots[slotIndex].Clear();
    }

    private bool IsActiveRemotePartyMember(EntityPlayerLocal localPlayer, EntityPlayer member)
    {
        if ((UnityEngine.Object)member == (UnityEngine.Object)null ||
            (UnityEngine.Object)member == (UnityEngine.Object)localPlayer ||
            member.IsDespawned || member.markedForUnload)
            return false;

        World world = xui != null && xui.playerUI != null && xui.playerUI.entityPlayer != null
            ? xui.playerUI.entityPlayer.world
            : null;
        if (world == null)
            return false;

        // Party.MemberList and World can both retain the old EntityPlayer for a
        // short period after disconnect.  Use the authoritative connection state
        // first: on the host/server the remote player must still have a ClientInfo.
        ConnectionManager connectionManager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if ((UnityEngine.Object)connectionManager != (UnityEngine.Object)null && connectionManager.IsServer &&
            connectionManager.Clients.ForEntityId(member.entityId) == null)
            return false;

        // Persistent player state is synchronized to EntityId=-1 on disconnect.
        // This also gives joined clients a disconnect signal without waiting for
        // the stale world entity to finish unloading.  If the entity-id map has
        // already been removed, the missing record is likewise treated as offline.
        GameManager gameManager = GameManager.Instance;
        PersistentPlayerList persistentPlayers = gameManager != null ? gameManager.GetPersistentPlayerList() : null;
        if (persistentPlayers != null)
        {
            PersistentPlayerData playerData = persistentPlayers.GetPlayerDataFromEntityID(member.entityId);
            if (playerData == null || playerData.EntityId != member.entityId)
                return false;
        }

        // Final object-identity guard: do not render a Party.MemberList object that
        // is no longer the current world entity for that id.
        Entity liveEntity = world.GetEntity(member.entityId);
        return (UnityEngine.Object)liveEntity == (UnityEngine.Object)member;
    }

    private int ComputePartySignature()
    {
        if (xui == null || xui.playerUI == null)
            return 0;

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if ((UnityEngine.Object)player == (UnityEngine.Object)null || player.Party == null || player.Party.MemberList == null)
            return 0;

        unchecked
        {
            int hash = 17;
            int visibleCount = 0;
            for (int i = 0; i < player.Party.MemberList.Count && visibleCount < MaximumEntries; i++)
            {
                EntityPlayer member = player.Party.MemberList[i];
                if (!IsActiveRemotePartyMember(player, member))
                    continue;

                hash = hash * 31 + member.entityId;
                visibleCount++;
            }

            return hash * 31 + visibleCount;
        }
    }
}

/// <summary>
/// One Simple Card slot. Player cards have priority in the list and companions
/// follow them; both render without names.
/// </summary>
[Preserve]
public class XUiC_RebirthSimplePartyCompanionHudSlot : XUiController
{
    private XUiC_RebirthHudPartySimpleEntry partyEntry;
    private XUiC_RebirthHudCompanionSimpleEntry companionEntry;

    public override void Init()
    {
        base.Init();
        partyEntry = GetChildByType<XUiC_RebirthHudPartySimpleEntry>();
        companionEntry = GetChildByType<XUiC_RebirthHudCompanionSimpleEntry>();
        Clear();
    }

    internal void SetPartyPlayer(EntityPlayer player)
    {
        if (companionEntry != null)
            companionEntry.SetCompanion(null);
        if (partyEntry != null)
            partyEntry.SetHudPlayer(player);
    }

    internal void SetCompanion(EntityAlive companion)
    {
        if (partyEntry != null)
            partyEntry.SetHudPlayer(null);
        if (companionEntry != null)
            companionEntry.SetCompanion(companion);
    }

    internal void Clear()
    {
        if (partyEntry != null)
        {
            partyEntry.SetHudPlayer(null);
            if (partyEntry.ViewComponent != null)
                partyEntry.ViewComponent.IsVisible = false;
        }
        if (companionEntry != null)
        {
            companionEntry.SetCompanion(null);
            if (companionEntry.ViewComponent != null)
                companionEntry.ViewComponent.IsVisible = false;
        }
    }
}

/// <summary>
/// Simple Cards population: fills left-to-right across eight columns, then
/// continues on a second row. Party players always occupy the first positions,
/// followed by every currently following companion, up to sixteen visible cards.
/// </summary>
[Preserve]
public class XUiC_RebirthSimplePartyCompanionHudList : XUiController
{
    private const int MaximumEntries = 16;
    private readonly List<XUiC_RebirthSimplePartyCompanionHudSlot> slots = new List<XUiC_RebirthSimplePartyCompanionHudSlot>();
    private float refreshTimer;
    private int lastPartySignature = int.MinValue;

    public override void Init()
    {
        base.Init();
        XUiC_RebirthSimplePartyCompanionHudSlot[] found = GetChildrenByType<XUiC_RebirthSimplePartyCompanionHudSlot>();
        if (found != null)
            slots.AddRange(found);
        if (ViewComponent != null)
            ViewComponent.IsVisible = false;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        refreshTimer = 0f;
        lastPartySignature = int.MinValue;
        bool active = RebirthCompanionCardStyleRuntime.CurrentStyle == RebirthCompanionCardStyle.Simple;
        if (ViewComponent != null)
            ViewComponent.IsVisible = active;
        if (active)
            RefreshList();
    }

    public override void OnClose()
    {
        for (int i = 0; i < slots.Count; i++)
            slots[i].Clear();
        base.OnClose();
    }

    public override void Update(float _dt)
    {
        bool active = RebirthCompanionCardStyleRuntime.CurrentStyle == RebirthCompanionCardStyle.Simple;
        if (!active)
        {
            if (ViewComponent != null && ViewComponent.IsVisible)
            {
                ViewComponent.IsVisible = false;
                for (int i = 0; i < slots.Count; i++)
                    slots[i].Clear();
            }
            return;
        }

        if (ViewComponent != null && !ViewComponent.IsVisible)
        {
            ViewComponent.IsVisible = true;
            refreshTimer = 0f;
            lastPartySignature = int.MinValue;
        }

        int partySignature = ComputePartySignature();
        refreshTimer -= _dt;
        if (refreshTimer <= 0f || partySignature != lastPartySignature)
        {
            refreshTimer = 0.5f;
            lastPartySignature = partySignature;
            RefreshList();
        }

        base.Update(_dt);
    }

    private void RefreshList()
    {
        if (xui == null || xui.playerUI == null)
            return;

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if ((UnityEngine.Object)player == (UnityEngine.Object)null)
            return;

        int slotIndex = 0;
        Party party = player.Party;
        if (party != null && party.MemberList != null)
        {
            for (int i = 0; i < party.MemberList.Count && slotIndex < MaximumEntries && slotIndex < slots.Count; i++)
            {
                EntityPlayer member = party.MemberList[i];
                if (!IsActiveRemotePartyMember(player, member))
                    continue;
                slots[slotIndex++].SetPartyPlayer(member);
            }
        }

        if (slotIndex < MaximumEntries && slotIndex < slots.Count)
        {
            List<EntityAlive> following = RebirthFollowingCompanionHudService.GetFollowing(player);
            for (int i = 0; i < following.Count && slotIndex < MaximumEntries && slotIndex < slots.Count; i++)
                slots[slotIndex++].SetCompanion(following[i]);
        }

        for (; slotIndex < slots.Count; slotIndex++)
            slots[slotIndex].Clear();
    }

    private bool IsActiveRemotePartyMember(EntityPlayerLocal localPlayer, EntityPlayer member)
    {
        if ((UnityEngine.Object)member == (UnityEngine.Object)null ||
            (UnityEngine.Object)member == (UnityEngine.Object)localPlayer ||
            member.IsDespawned || member.markedForUnload)
            return false;

        World world = xui != null && xui.playerUI != null && xui.playerUI.entityPlayer != null
            ? xui.playerUI.entityPlayer.world
            : null;
        if (world == null)
            return false;

        ConnectionManager connectionManager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if ((UnityEngine.Object)connectionManager != (UnityEngine.Object)null && connectionManager.IsServer &&
            connectionManager.Clients.ForEntityId(member.entityId) == null)
            return false;

        GameManager gameManager = GameManager.Instance;
        PersistentPlayerList persistentPlayers = gameManager != null ? gameManager.GetPersistentPlayerList() : null;
        if (persistentPlayers != null)
        {
            PersistentPlayerData playerData = persistentPlayers.GetPlayerDataFromEntityID(member.entityId);
            if (playerData == null || playerData.EntityId != member.entityId)
                return false;
        }

        Entity liveEntity = world.GetEntity(member.entityId);
        return (UnityEngine.Object)liveEntity == (UnityEngine.Object)member;
    }

    private int ComputePartySignature()
    {
        if (xui == null || xui.playerUI == null)
            return 0;

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if ((UnityEngine.Object)player == (UnityEngine.Object)null || player.Party == null || player.Party.MemberList == null)
            return 0;

        unchecked
        {
            int hash = 17;
            int visibleCount = 0;
            for (int i = 0; i < player.Party.MemberList.Count && visibleCount < MaximumEntries; i++)
            {
                EntityPlayer member = player.Party.MemberList[i];
                if (!IsActiveRemotePartyMember(player, member))
                    continue;
                hash = hash * 31 + member.entityId;
                visibleCount++;
            }
            return hash * 31 + visibleCount;
        }
    }
}

/// <summary>
/// Standalone REBIRTH following-companion list. It does not derive from the
/// native CompanionEntryList, so the native controller never runs its stock
/// population/position logic against the custom entries.
/// </summary>
[Preserve]
public class XUiC_RebirthFollowingCompanionEntryList : XUiController
{
    private readonly List<XUiC_RebirthHudCompanionEntry> entryList = new List<XUiC_RebirthHudCompanionEntry>();
    private float refreshTimer;

    public override void Init()
    {
        base.Init();
        XUiC_RebirthHudCompanionEntry[] entries = GetChildrenByType<XUiC_RebirthHudCompanionEntry>();
        if (entries != null)
            entryList.AddRange(entries);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        refreshTimer = 0f;
        RefreshList();
    }

    public override void OnClose()
    {
        for (int i = 0; i < entryList.Count; i++)
            entryList[i].SetCompanion(null);
        base.OnClose();
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
        refreshTimer -= _dt;
        if (refreshTimer > 0f)
            return;

        refreshTimer = 0.5f;
        RefreshList();
    }

    private void RefreshList()
    {
        if (xui == null || xui.playerUI == null)
            return;

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if ((UnityEngine.Object)player == (UnityEngine.Object)null)
            return;

        List<EntityAlive> following = RebirthFollowingCompanionHudService.GetFollowing(player);
        int index = 0;
        for (; index < following.Count && index < entryList.Count; index++)
            entryList[index].SetCompanion(following[index]);
        for (; index < entryList.Count; index++)
            entryList[index].SetCompanion(null);

        int partyCount = RebirthFollowingCompanionHudService.GetPartyMemberCount(player);
        // Party is rendered by the native PartyEntryList immediately above this grid.
        // Move the companion grid by the number of visible remote party members so the
        // two sections read as one compact stack with no artificial section gap.
        int companionListY = RebirthFollowingCompanionHudService.ListTop -
            partyCount * RebirthFollowingCompanionHudService.RowPitch;
        RebirthFollowingCompanionHudService.SetControllerY(this, companionListY);
    }
}

internal static class RebirthFollowingCompanionHudService
{
    internal const int MaximumRows = 7;
    internal const int RowPitch = 58;
    internal const int ListTop = -4;

    internal static List<EntityAlive> GetFollowing(EntityPlayerLocal player)
    {
        List<EntityAlive> result = new List<EntityAlive>();
        HashSet<int> seen = new HashSet<int>();
        if ((UnityEngine.Object)player == (UnityEngine.Object)null || player.world == null)
            return result;

        // Preserve native companion ordering first.
        if (player.Companions != null)
        {
            for (int i = 0; i < player.Companions.Count; i++)
            {
                EntityAlive candidate = player.Companions[i];
                if (IsFollowingOwnedCompanion(player, candidate) && seen.Add(candidate.entityId))
                    result.Add(candidate);
            }
        }

        // Drones use the exact 3.1 class-filtered ownership API.
        List<OwnedEntityData> ownedDrones = player.GetOwnedEntities(EntityClass.junkDroneClass);
        if (ownedDrones != null)
        {
            for (int i = 0; i < ownedDrones.Count; i++)
            {
                OwnedEntityData owned = ownedDrones[i];
                EntityDrone drone = owned != null ? player.world.GetEntity(owned.Id) as EntityDrone : null;
                if (IsFollowingOwnedCompanion(player, drone) && seen.Add(drone.entityId))
                    result.Add(drone);
            }
        }

        // REBIRTH NPC/dog ownership is represented by the runtime registry.
        string ownerId;
        if (RebirthDogLifecycleService.TryResolveOwnerId(player, out ownerId))
        {
            RebirthNpcRuntimeState[] states = RebirthNpcRuntimeRegistry.GetSnapshot();
            for (int i = 0; i < states.Length; i++)
            {
                RebirthNpcRuntimeState state = states[i];
                if (state == null || state.Presence != RebirthNpcPresenceState.Active ||
                    state.Order != RebirthNpcOrderState.Follow ||
                    state.OwnershipKind != RebirthNpcOwnershipKind.Player ||
                    !string.Equals(state.OwnerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase))
                    continue;

                int entityId;
                if (!RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId, out entityId))
                    continue;

                EntityRebirthNPC candidate = player.world.GetEntity(entityId) as EntityRebirthNPC;
                if ((UnityEngine.Object)candidate == (UnityEngine.Object)null || candidate.IsDead() || !seen.Add(candidate.entityId))
                    continue;

                result.Add(candidate);
            }
        }

        return result;
    }

    private static bool IsFollowingOwnedCompanion(EntityPlayerLocal player, EntityAlive candidate)
    {
        if ((UnityEngine.Object)player == (UnityEngine.Object)null ||
            (UnityEngine.Object)candidate == (UnityEngine.Object)null || candidate.IsDead())
            return false;

        EntityDrone drone = candidate as EntityDrone;
        if (drone != null)
            return drone.belongsPlayerId == player.entityId && drone.OrderState == EntityDrone.Orders.Follow;

        EntityRebirthDogCompanion dog = candidate as EntityRebirthDogCompanion;
        if (dog != null)
        {
            RebirthNpcRuntimeState dogState = dog.RebirthRuntimeState;
            return dogState != null && dogState.Order == RebirthNpcOrderState.Follow &&
                   RebirthDogLifecycleService.IsOwnedBy(dog, player);
        }

        EntityRebirthNPC npc = candidate as EntityRebirthNPC;
        if (npc != null)
        {
            RebirthNpcRuntimeState state = npc.RebirthRuntimeState;
            string ownerId;
            return state != null && state.Order == RebirthNpcOrderState.Follow &&
                   state.OwnershipKind == RebirthNpcOwnershipKind.Player &&
                   RebirthDogLifecycleService.TryResolveOwnerId(player, out ownerId) &&
                   string.Equals(state.OwnerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase);
        }

        // Preserve compatibility with ordinary/base companion types.
        return player.Companions != null && player.Companions.IndexOf(candidate) >= 0;
    }

    internal static int GetPartyMemberCount(EntityPlayer player)
    {
        if ((UnityEngine.Object)player == (UnityEngine.Object)null || player.Party == null || player.Party.MemberList == null)
            return 0;

        int count = 0;
        for (int i = 0; i < player.Party.MemberList.Count && count < MaximumRows; i++)
        {
            EntityPlayer member = player.Party.MemberList[i];
            if ((UnityEngine.Object)member != (UnityEngine.Object)null &&
                (UnityEngine.Object)member != (UnityEngine.Object)player)
                count++;
        }
        return count;
    }

    internal static void SetControllerY(XUiController controller, int y)
    {
        if (controller == null || controller.ViewComponent == null)
            return;

        Vector2i pos = controller.ViewComponent.Position;
        controller.ViewComponent.Position = new Vector2i(pos.x, y);
        if (controller.ViewComponent.UiTransform != null)
            controller.ViewComponent.UiTransform.localPosition = new Vector3((float)pos.x, (float)y, controller.ViewComponent.UiTransform.localPosition.z);
    }
}
