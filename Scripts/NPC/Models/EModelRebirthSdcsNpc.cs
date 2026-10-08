using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

#nullable disable

/// <summary>
/// REBIRTH-owned SDCS visual model for human NPCs.
///
/// The entity class selects this model type in XML. The authoritative model
/// pipeline and appearance seed are supplied by EntityRebirthHumanoidNPC after
/// its persistent runtime state has been resolved. This deliberately avoids
/// transient CVar seeds and random client-side appearance generation.
/// </summary>
public sealed class EModelRebirthSdcsNpc : EModelSDCS
{
    private EntityAlive npcEntity;
    private EntityClass npcEntityClass;
    private RebirthHumanNpcAppearanceDescriptor appearance;
    private bool appearanceApplied;
    private int appearanceRetryCount;
    private const int MaximumAppearanceRetries = 3;

    public RebirthHumanNpcAppearanceDescriptor RebirthAppearance => appearance;
    public bool RebirthAppearanceApplied => appearanceApplied;

    public override void Init(
        World world,
        Entity sourceEntity,
        EModelInstanceAssets modelAssets)
    {
        // Do not call EModelSDCS.Init here. Its player-oriented initialization
        // may create a visual before the persistent NPC appearance descriptor is
        // available. The REBIRTH adapter calls ApplyAppearance immediately after
        // EntityNPC.Init has registered the NPC runtime state.
        entity = sourceEntity;
        assets = modelAssets;
        npcEntity = sourceEntity as EntityAlive;
        npcEntityClass = EntityClass.list[sourceEntity.entityClass];
        entityClass = npcEntityClass;
        modelTransformParent = EModelBase.FindModel(base.transform);
        bHasRagdoll = true;
        visible = false;
    }

    private static readonly MethodInfo EModelBaseSwitchModelAndView = AccessTools.Method(typeof(EModelBase), "SwitchModelAndView", new[] { typeof(bool), typeof(bool) });

    /// <summary>
    /// Game 3.2 EModelSDCS.SwitchModelAndView assumes a player: it writes playerEntity.IsMale and regenerates the meshes from
    /// the player profile. This NPC model has no playerEntity (Init is deliberately not called), so the inherited version throws a
    /// NullReferenceException inside EntityAlive.Init and the NPC can not spawn. Here: nothing to switch until the appearance
    /// has generated a model (ApplyAppearance calls this again afterwards), then run the EModelBase logic only.
    /// </summary>
    /// <summary>EntityFactory calls this for every entity. The inherited EModelPlayer version reads the first child of the model parent, which does not exist yet for an NPC; the appearance comes from the SDCS descriptor, not a skin texture.</summary>
    public override void SetSkinTexture(string _textureName)
    {
    }

    public override void SwitchModelAndView(bool _bFPV, bool _isMale)
    {
        IsFPV = false;
        if (baseRig == null || modelName == null) return;
        AccessTools.MethodDelegate<Action<bool, bool>>(EModelBaseSwitchModelAndView, this, false)(false, _isMale);
        meshTransform = modelTransform != null ? modelTransform.FindInChildren("Spine1") : null;
    }

    public void ApplyAppearance(RebirthHumanNpcAppearanceDescriptor descriptor)
    {
        if (!descriptor.IsValid || descriptor.Pipeline != RebirthHumanNpcModelPipeline.SDCS)
            throw new ArgumentException("A valid SDCS appearance descriptor is required.", nameof(descriptor));
        if (npcEntity == null || npcEntityClass == null)
            throw new InvalidOperationException("The SDCS model must be initialized before appearance is applied.");

        bool changed = !appearanceApplied || !appearance.Equals(descriptor);
        if (!changed)
            return;

        Archetype selected = descriptor.Resolved != null ? BuildResolvedArchetype(descriptor.Resolved, npcEntityClass)
            : BuildArchetype(descriptor.Seed, npcEntityClass, descriptor.Archetype);
        appearance = descriptor; archetype = selected;

        DestroyCurrentGeneratedModel();
        appearanceApplied = CreateGeneratedModel();
        appearanceRetryCount = appearanceApplied ? 0 : 1;
    }

    public void RefreshAppearanceMeshes()
    {
        if (npcEntity == null) return;
        if (!appearanceApplied)
        {
            if (!appearance.IsValid || appearanceRetryCount >= MaximumAppearanceRetries) return;
            appearanceRetryCount++;
            DestroyCurrentGeneratedModel();
            appearanceApplied = CreateGeneratedModel();
            if (!appearanceApplied) return;
        }

        Transform refreshed = GenerateRebirthMeshes();
        if (refreshed != null) npcEntity.ReassignEquipmentTransforms();
    }

    private bool CreateGeneratedModel()
    {
        if (GameManager.IsDedicatedServer)
        {
            visible = false;
            return true;
        }
        if (modelTransformParent == null)
        {
            Log.Error("[REBIRTH NPC] SDCS model parent was not found for entity=" + entity?.entityId);
            return false;
        }

        Transform generated = GenerateRebirthMeshes();
        if (generated == null)
        {
            Log.Error("[REBIRTH NPC] SDCS mesh generation returned null for entity=" + entity?.entityId);
            return false;
        }

        modelName = "rebirth_npc_sdcs_" + (archetype.IsMale ? "male_" : "female_") + appearance.Seed;
        generated.name = modelName;
        generated.tag = "E_BP_Body";
        generated.SetParent(modelTransformParent, false);
        generated.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        Transform origin = generated.FindInChilds("Origin") ?? generated.FindRecursive("Origin");
        if (origin != null)
            origin.tag = "E_BP_BipedRoot";

        generated.gameObject.GetOrAddComponent<AnimationEventBridge>();
        npcEntity.ReassignEquipmentTransforms();
        createAvatarController(npcEntityClass);
        SwitchModelAndView(false, archetype.IsMale);
        visible = true;
        SetVisible(true, true);
        return true;
    }

    private Transform GenerateRebirthMeshes()
    {
        if (GameManager.IsDedicatedServer)
            return null;

        SDCSUtils.CreateVizTP(archetype, ref baseRig, ref boneCatalog, npcEntity, false);
        if (baseRig == null)
            return null;

        ClothSimInit();
        return baseRig.transform;
    }

    private void DestroyCurrentGeneratedModel()
    {
        if (GameManager.IsDedicatedServer)
            return;

        if (baseRig != null)
        {
            UnityEngine.Object.Destroy(baseRig);
            baseRig = null;
        }
        boneCatalog?.Clear();
        modelTransform = null;
        meshTransform = null;
        headTransform = null;
        bipedRootTransform = null;
        bipedPelvisTransform = null;
    }

    private Archetype BuildResolvedArchetype(RebirthNpcResolvedAppearance resolved, EntityClass definition)
    {
        bool male=npcEntity?.IsMale ?? definition.bIsMale;
        if (resolved.Pipeline!=RebirthHumanNpcModelPipeline.SDCS || resolved.IsMale!=male ||
            !string.Equals(resolved.ModelId,definition.Properties.GetString("Prefab"),StringComparison.Ordinal) ||
            resolved.GeneratorId!="rebirth-sdcs-resolved-v1")
            throw new InvalidOperationException("Resolved NPC appearance does not match the current model binding.");
        {
            string catalogue=RebirthSdcsCatalogService.QualifyTextCatalog();
            bool variant=SDCSDataUtils.GetVariantList(male,resolved.Race).Exists(value => int.TryParse(value,out var number)&&number==resolved.Variant);
            bool hairColour=string.IsNullOrEmpty(resolved.HairColour)||SDCSDataUtils.GetHairColorNames().Exists(value=>value.PrefabName==resolved.HairColour);
            if (catalogue!=resolved.CatalogueId || Archetype.GetArchetype(resolved.Archetype)==null ||
                !SDCSDataUtils.GetRaceList(male).Contains(resolved.Race)||!variant||
                !SDCSDataUtils.GetEyeColorNames().Contains(resolved.Eye)||!hairColour||
                !ResolvedPartExists(male,SDCSDataUtils.HairTypes.Hair,resolved.Hair)||
                !ResolvedPartExists(male,SDCSDataUtils.HairTypes.Mustache,resolved.Mustache)||
                !ResolvedPartExists(male,SDCSDataUtils.HairTypes.Chops,resolved.Chops)||
                !ResolvedPartExists(male,SDCSDataUtils.HairTypes.Beard,resolved.Beard))
                throw new InvalidOperationException("Saved NPC appearance choices are unavailable in the current catalogue; reroll refused.");
        }
        return new Archetype(resolved.Archetype,male,true) { Race=resolved.Race,Variant=resolved.Variant,
            EyeColorName=resolved.Eye,Hair=resolved.Hair,HairColor=resolved.HairColour,
            MustacheName=resolved.Mustache,ChopsName=resolved.Chops,BeardName=resolved.Beard };
    }
    private static bool ResolvedPartExists(bool male,SDCSDataUtils.HairTypes type,string choice)
    { return string.IsNullOrEmpty(choice)||SDCSDataUtils.GetHairNames(male,type).Contains(choice); }
    private Archetype BuildArchetype(int seed, EntityClass entityDefinition, string requestedArchetype)
    {
        if (!GameManager.IsDedicatedServer) RebirthSdcsCatalogService.EnsureVisualCatalog();
        System.Random random = new System.Random(seed == 0 ? 1 : seed);
        bool isMale = npcEntity?.IsMale ?? entityDefinition.bIsMale;

        List<string> races = SDCSDataUtils.GetRaceList(isMale);
        string race = Pick(races, random, "white");

        List<string> variants = SDCSDataUtils.GetVariantList(isMale, race);
        int variant = 1;
        string variantText = Pick(variants, random, "1");
        int.TryParse(variantText, out variant);
        if (variant <= 0) variant = 1;

        string eye = Pick(SDCSDataUtils.GetEyeColorNames(), random, "Blue01");
        string hair = Pick(SDCSDataUtils.GetHairNames(isMale, SDCSDataUtils.HairTypes.Hair), random, string.Empty);

        List<SDCSDataUtils.HairColorData> hairColours = SDCSDataUtils.GetHairColorNames();
        string hairColour = hairColours != null && hairColours.Count > 0
            ? hairColours[random.Next(hairColours.Count)].PrefabName
            : string.Empty;

        string mustache = string.Empty;
        string chops = string.Empty;
        string beard = string.Empty;
        if (isMale)
        {
            mustache = Pick(SDCSDataUtils.GetHairNames(true, SDCSDataUtils.HairTypes.Mustache), random, string.Empty);
            chops = Pick(SDCSDataUtils.GetHairNames(true, SDCSDataUtils.HairTypes.Chops), random, string.Empty);
            beard = Pick(SDCSDataUtils.GetHairNames(true, SDCSDataUtils.HairTypes.Beard), random, string.Empty);
        }

        string baseName = string.IsNullOrWhiteSpace(requestedArchetype)
            ? (isMale ? "BaseMale" : "BaseFemale")
            : requestedArchetype.Trim();

        return new Archetype(baseName, isMale, true)
        {
            Race = race,
            Variant = variant,
            EyeColorName = eye,
            Hair = hair,
            HairColor = hairColour,
            MustacheName = mustache,
            ChopsName = chops,
            BeardName = beard
        };
    }

    private static string Pick(List<string> values, System.Random random, string fallback)
    {
        return values != null && values.Count > 0 ? values[random.Next(values.Count)] : fallback;
    }
}
