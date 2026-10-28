using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio
{
    public enum ZombieTier { Normal, Feral, Radiated, Charged, Infernal }
    public enum EntityModelUpAxis { Y, Z }

    [Serializable]
    public sealed class EntityMaterialTextureOverride
    {
        public Material sourceMaterial;
        public string mapName;
        public Texture2D albedo,normalMap,roughness,metallic;
    }

    [Serializable]
    public sealed class EntityColliderModeSpec
    {
        public string bone;
        public Vector3 collisionScale=Vector3.one,ragdollScale=Vector3.one,collisionOffset,ragdollOffset;
        public int collisionLayer=0,ragdollLayer=21;
    }

    [Serializable]
    public sealed class EntityBoneSpec
    {
        public string name;
        public string parent;
        public string gameTag;
        public Vector3 normalizedPosition;
        // Reference rest orientation copied from an inspected game hierarchy. Position is
        // re-fit to the supplied mesh bounds; rotation keeps the target bone's local axes.
        public Vector3 referenceLocalEuler;
        // Tagged attachment helpers are part of the hierarchy but must not receive skin weights.
        public bool deformsMesh = true;
        public bool useInfluenceBounds;
        public Vector3 influenceMin=Vector3.zero;
        public Vector3 influenceMax=Vector3.one;
        public bool required = true;
    }

    [Serializable]
    public sealed class EntityRendererTierMaterials
    {
        public SkinnedMeshRenderer renderer;
        public int slotCount;
        public List<Material> sourceMaterials = new List<Material>();
        // Five consecutive material arrays in ZombieTier enum order.
        public List<Material> variants = new List<Material>();
        public List<SkinnedMeshRenderer> lodRenderers=new List<SkinnedMeshRenderer>();
    }

    [Serializable]
    public sealed class EntityLodRendererMapping
    {public SkinnedMeshRenderer source;public SkinnedMeshRenderer renderer;}

    [Serializable]
    public sealed class EntityColliderFitReport
    {
        public string colliderName;
        public int sampledVertices;
        [Range(0f,1f)] public float estimatedSurfaceCoverage;
        public float radius;
        public float baseGameReferenceRadius;
        public float height;
        public bool usedProportionalFallback;
        public Collider collider;
        public Vector3 boxSize;
        public bool usesPoseUnionCoverage;
        public int poseUnionSampleCount;
        public float poseUnionCoverage;
    }

    [CreateAssetMenu(menuName = "REBIRTH/Entity Rig Profile", fileName = "EntityRigProfile")]
    public sealed class RebirthEntityRigProfile : ScriptableObject
    {
        [Header("Source and output")]
        public string sourceModelPath;
        public string sourceTexturePath;
        public string entityClass = "zombieCustom";
        public string prefabFamily = "zombieTemplateMale";
        public string unityVersion = "2022.3.62f2";
        public string gameVersion = "3.0 b259 (target; runtime references currently 3.3.0)";
        [Tooltip("Up axis of the imported model before conversion into the game skeleton's Y-up space.")]
        public EntityModelUpAxis sourceUpAxis = EntityModelUpAxis.Y;
        [Tooltip("Desired world height in metres; zero preserves the imported size.")]
        public float targetHeight;
        public bool useSurfaceGeodesicWeights;
        [Tooltip("Model-specific cleanup for the supplied RebirthFarmer arms-down mesh. Inspect the resulting seams and posed mesh before export.")]
        public bool separateFusedArms;
        [Range(0f,.08f)] public float handCutInset;
        [Range(0f,.08f)] public float wristCutInset;
        public bool wrapMissingTorsoTexture;
        public bool refineBodyWeightsWithPoses;
        public bool useBoxTorso;
        public bool includeNeckCollider;
        public bool includeTerminalPartsInColliders;
        public bool compoundTerminalColliders;
        public bool preferNarrowCapsules;
        public List<EntityColliderModeSpec> colliderModes=new List<EntityColliderModeSpec>();
        public PhysicMaterial ragdollMaterial;
        public bool generateLods = true;
        [Range(0.5f,8f)] public float skinWeightFalloffExponent = 4f;
        [Range(0.01f, 0.95f)] public float lod0TransitionHeight = 0.4f;
        [Range(0.005f, 0.75f)] public float lod1TransitionHeight = 0.147f;
        [Range(0.001f, 0.5f)] public float lod2TransitionHeight = 0.05f;
        [Range(0.001f, 0.25f)] public float lod3TransitionHeight = 0.003f;
        [Range(0.01f, 1f)] public float lod1Ratio = 0.55f;
        [Range(0.01f, 1f)] public float lod2Ratio = 0.25f;
        [Range(0.01f, 1f)] public float lod3Ratio = 0.12f;
        public bool createRagdoll = true;
        public float ragdollMass=70f;
        public bool createHitColliders = true;
        public bool useBoxTorsoAndLegs;
        [Range(0.005f, 0.15f)] public float colliderPadding = 0.015f;
        public bool generateTierMaterials = true;
        public Texture2D bakedAlbedo;
        public Texture2D normalMap;
        public Texture2D rmoeMap;
        public List<Texture2D> nativeRmoeMaps=new List<Texture2D>();
        public int albedoMaxSize=2048;
        public int normalMaxSize=4096;
        public int dataMaxSize=2048;
        public Texture2D sourceRoughness,sourceMetallic;
        public List<EntityMaterialTextureOverride> materialTextureOverrides=new List<EntityMaterialTextureOverride>();
        public float eyeHeightRatio=.934f;
        public float eyeHalfSeparationRatio=.021f;
        public float eyeWidthRatio=.007f;
        public float eyeRadiusHeightRatio=.005f;
        public Shader characterShader;
        public List<Material> generatedTierMaterials = new List<Material>();
        public int generatedMaterialSlotCount;
        // Values cross-checked against the extracted 7DTD v3.0 entityclasses.xml.
        // RGB is MatColor; the fourth value is alpha, with the config noting A+1 as emission power.
        public Color feralTint = new Color(0.72f, 0.88f, 1f, 1f);
        public Color radiatedTint = new Color(0.295f, 1f, 0f, 0f);
        public Color chargedTint = new Color(0f, 0.8f, 3f, 0.7f);
        public Color infernalTint = new Color(2.7f, 0.8f, 0f, 0.55f);
        [Range(0f, 8f)] public float tierEmission = 1f;

        [Header("Source model mapping")]
        public Transform sourceRoot;
        // Scene references are owned and serialized by the workspace, never this asset.
        [NonSerialized] public SkinnedMeshRenderer sourceSkinnedMesh;
        [NonSerialized] public List<SkinnedMeshRenderer> sourceSkinnedMeshes = new List<SkinnedMeshRenderer>();
        [NonSerialized] public List<EntityRendererTierMaterials> rendererTierMaterials = new List<EntityRendererTierMaterials>();
        public List<EntityBoneSpec> bones = new List<EntityBoneSpec>();
        [HideInInspector] public bool boneDeformFlagsMigrated;
        public List<string> requiredGoreAnchors = new List<string>
        { "L_HeadGore", "L_LeftUpperArmGore", "L_RightUpperArmGore", "L_LeftLowerArmGore", "L_RightLowerArmGore",
          "L_LeftUpperLegGore", "L_RightUpperLegGore", "L_LeftLowerLegGore", "L_RightLowerLegGore" };
        [HideInInspector] public bool defaultGoreAnchorsMigrated;
        public List<string> requiredEyeAnchors = new List<string>{"NormalEye","FeralEye"};
        [HideInInspector] public bool defaultEyeAnchorsMigrated;
        public Vector3 overallBoundsCenter;
        public Vector3 overallBoundsSize = new Vector3(0.7f, 1.8f, 0.45f);
        public float estimatedHeight = 1.8f;
        public bool reviewedSkeletonMapping;
        public bool reviewedColliderFit;
        public bool reviewedLods;
        public bool reviewedMaterials;
        public bool reviewedTargetCompatibility;
        [TextArea(2,4)] public string targetCompatibilityEvidence = "";

        public void InitializeBipedDefaults()
        {
            bones.Clear();
            Add("Origin", "", "E_BP_BipedRoot", 0.50f);
            Add("Hips", "Origin", "E_BP_Body", 0.48f);
            Add("Spine", "Hips", "", 0.53f);
            Add("Spine1", "Spine", "", 0.58f);
            Add("Spine2", "Spine1", "E_BP_Body", 0.64f);
            Add("Spine3", "Spine2", "", 0.70f);
            Add("Neck", "Spine3", "", 0.77f);
            Add("Head", "Neck", "E_BP_Head", 0.84f);
            Add("LeftShoulder", "Spine3", "", 0.73f, 0.40f);
            Add("LeftArm", "LeftShoulder", "E_BP_LArm", 0.69f, 0.34f);
            Add("LeftForeArm", "LeftArm", "E_BP_LLowerArm", 0.60f, 0.26f);
            Add("LeftHand", "LeftForeArm", "", 0.52f, 0.22f);
            Add("RightShoulder", "Spine3", "", 0.73f, 0.60f);
            Add("RightArm", "RightShoulder", "E_BP_RArm", 0.69f, 0.66f);
            Add("RightForeArm", "RightArm", "E_BP_RLowerArm", 0.60f, 0.74f);
            Add("RightHand", "RightForeArm", "", 0.52f, 0.78f);
            Add("LeftUpLeg", "Hips", "E_BP_LLeg", 0.38f, 0.46f);
            Add("LeftLeg", "LeftUpLeg", "E_BP_LLowerLeg", 0.20f, 0.46f);
            Add("LeftFoot", "LeftLeg", "", 0.05f, 0.46f);
            Add("RightUpLeg", "Hips", "E_BP_RLeg", 0.38f, 0.54f);
            Add("RightLeg", "RightUpLeg", "E_BP_RLowerLeg", 0.20f, 0.54f);
            Add("RightFoot", "RightLeg", "", 0.05f, 0.54f);
            bones.Find(bone=>bone.name=="Origin").deformsMesh=false;
            boneDeformFlagsMigrated=true;
        }

        public bool EnsureBoneDeformFlags()
        {
            if(boneDeformFlagsMigrated)return false;
            if(bones!=null)foreach(var bone in bones)if(bone!=null)bone.deformsMesh=true;
            boneDeformFlagsMigrated=true;
            return true;
        }

        public void EnsureDefaultGoreAnchors()
        {
            if(defaultGoreAnchorsMigrated)return;
            if(requiredGoreAnchors==null)requiredGoreAnchors=new List<string>();
            string[] defaults={"L_HeadGore","L_LeftUpperArmGore","L_RightUpperArmGore","L_LeftLowerArmGore","L_RightLowerArmGore",
                "L_LeftUpperLegGore","L_RightUpperLegGore","L_LeftLowerLegGore","L_RightLowerLegGore"};
            foreach(string anchor in defaults)
                if(!requiredGoreAnchors.Exists(existing=>string.Equals(existing,anchor,StringComparison.OrdinalIgnoreCase)))requiredGoreAnchors.Add(anchor);
            defaultGoreAnchorsMigrated=true;
        }

        public void EnsureDefaultEyeAnchors()
        {
            if(defaultEyeAnchorsMigrated)return;
            if(requiredEyeAnchors==null)requiredEyeAnchors=new List<string>();
            foreach(string anchor in new[]{"NormalEye","FeralEye"})
                if(!requiredEyeAnchors.Exists(existing=>string.Equals(existing,anchor,StringComparison.OrdinalIgnoreCase)))requiredEyeAnchors.Add(anchor);
            defaultEyeAnchorsMigrated=true;
        }

        private void Add(string name, string parent, string tag, float y, float x = 0.5f)
        { bones.Add(new EntityBoneSpec { name = name, parent = parent, gameTag = tag, normalizedPosition = new Vector3(x, y, 0.5f) }); }
    }

}
