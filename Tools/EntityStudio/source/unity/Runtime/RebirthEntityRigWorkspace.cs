using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio
{
    // Authoring data used to bake newly exposed repair surfaces.
    // Original faces/UVs are kept separate from generated caps; none ships.
    [System.Serializable] public sealed class EntitySurfaceRepairInput
    {
        public Mesh repairedMesh;
        public Vector3[] sourcePositionsWorld,repairPositionsWorld;
        public Vector2[] sourceUv;
        public int[] sourceTriangles,sourceTriangleParts,repairVertexIds,repairVertexParts;
        public Vector3Int[] repairWeightAnchors;
        public Vector3[] repairWeightBarycentrics;
    }

    public sealed class RebirthEntityRigWorkspace : MonoBehaviour
    {
        public RebirthEntityRigProfile profile;
        public List<EntitySurfaceRepairInput> surfaceRepairInputs=new List<EntitySurfaceRepairInput>();
        public Transform generatedSkeleton;
        public Transform sourceVisual;
        public Transform colliderRoot;
        public Transform goreAnchorRoot;
        public LODGroup lodGroup;
        public List<SkinnedMeshRenderer> sourceSkinnedMeshes = new List<SkinnedMeshRenderer>();
        public List<EntityRendererTierMaterials> rendererTierMaterials = new List<EntityRendererTierMaterials>();
        public List<EntityLodRendererMapping> lodRendererMappings=new List<EntityLodRendererMapping>();
        public List<EntityColliderFitReport> colliderFitReports = new List<EntityColliderFitReport>();
        public string generatedAssetId;
        public string sourceModelAssetPath;
        public bool sourceModelIsSceneInstance;
        public GameObject sourceModelObject;
        public string sourceModelKey;
        public string profileBuildFingerprint;
        public string poseColliderProofSignature;
        public ZombieTier activeTier = ZombieTier.Normal;
        public string validationReport = "Not validated";
    }
}
