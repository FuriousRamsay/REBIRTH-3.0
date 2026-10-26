using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityCoverageReview
    {
        public static void RunSavedFarmer()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            RebirthEntityRigWorkspace workspace=null;
            foreach(var root in scene.GetRootGameObjects())if(root.TryGetComponent(out RebirthEntityRigWorkspace found)){workspace=found;break;}
            if(workspace==null)throw new InvalidOperationException("Saved Farmer workspace is missing.");
            RebirthEntityRigBuilder.BindWorkspaceReferences(workspace);
            string directory=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/RebirthFarmerReview");
            Directory.CreateDirectory(directory);Write(workspace,Path.Combine(directory,"RebirthFarmer_Coverage.json"));
            Debug.Log("SAVED FARMER COVERAGE: "+directory);
        }
        [Serializable] public sealed class ViewResult
        {public string view;public int meshRays,missedMeshRays,colliderOnlyRays,neckMeshRays,neckMisses;public float meanAbsoluteDepthError,maxAbsoluteDepthError;}
        [Serializable] public sealed class Report {public ViewResult[] views;public int colliderCount;public float gridSpacing;public bool anatomicalNeckRegion;public Vector3 neckRegionMin,neckRegionMax;}
        public static void Write(RebirthEntityRigWorkspace workspace,string output)
        {
            var temporary=new List<GameObject>();var meshColliders=new List<MeshCollider>();
            try
            {
                Bounds bounds=new Bounds();bool has=false;
                foreach(var renderer in workspace.sourceSkinnedMeshes)
                {
                    var go=new GameObject("__CoverageMesh");temporary.Add(go);go.transform.SetParent(renderer.transform,false);
                    var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=renderer.sharedMesh;meshColliders.Add(collider);
                    foreach(Vector3 point in renderer.sharedMesh.vertices){Vector3 world=renderer.transform.TransformPoint(point);if(!has){bounds=new Bounds(world,Vector3.zero);has=true;}else bounds.Encapsulate(world);}
                }
                Physics.SyncTransforms();var body=new List<Collider>();
                // Query the actual hierarchy: fitting candidates can add compound
                // shapes before regenerating persistent fit reports. The temporary
                // triangle colliders above are references, never body hit shapes.
                foreach(var collider in workspace.generatedSkeleton.GetComponentsInChildren<Collider>(true))
                    if(collider is CapsuleCollider||collider is BoxCollider)body.Add(collider);
                Transform neckBone=RebirthEntityRigBuilder.FindBone(workspace.transform,"Neck"),headBone=RebirthEntityRigBuilder.FindBone(workspace.transform,"Head");
                bool anatomicalNeck=neckBone!=null&&headBone!=null;
                Vector3 neckMin=Vector3.zero,neckMax=Vector3.zero;
                if(anatomicalNeck)
                {
                    // Audit the actual neck-to-head span, independent of character height or root placement.
                    float margin=bounds.size.y*.035f;
                    neckMin=Vector3.Min(neckBone.position,headBone.position)-new Vector3(margin,0,margin);
                    neckMax=Vector3.Max(neckBone.position,headBone.position)+new Vector3(margin,0,margin);
                }
                const float step=.0125f;var results=new List<ViewResult>();var misses=new List<string>{"view,x,y,z,meshDistance,colliderDistance,collider"};
                foreach(bool side in new[]{false,true})
                {
                    var result=new ViewResult{view=side?"side":"front"};float error=0;int compared=0;
                    float low=side?bounds.min.z:bounds.min.x,high=side?bounds.max.z:bounds.max.x;
                    for(float y=bounds.min.y-.05f;y<=bounds.max.y+.05f;y+=step)for(float horizontal=low-.05f;horizontal<=high+.05f;horizontal+=step)
                    {
                        Vector3 origin=side?new Vector3(bounds.max.x+.5f,y,horizontal):new Vector3(horizontal,y,bounds.max.z+.5f);
                        Ray ray=new Ray(origin,side?Vector3.left:Vector3.back);float meshDistance=float.PositiveInfinity,bodyDistance=float.PositiveInfinity;
                        string bodyName="";
                        foreach(var collider in meshColliders)if(collider.Raycast(ray,out RaycastHit hit,4))meshDistance=Mathf.Min(meshDistance,hit.distance);
                        foreach(var collider in body)if(collider.Raycast(ray,out RaycastHit hit,4)&&hit.distance<bodyDistance){bodyDistance=hit.distance;bodyName=collider.name;}
                        bool meshHit=!float.IsInfinity(meshDistance),bodyHit=!float.IsInfinity(bodyDistance);
                        if(meshHit)
                        {
                            result.meshRays++;if(!bodyHit)result.missedMeshRays++;
                            if(!bodyHit||Mathf.Abs(meshDistance-bodyDistance)>.05f)misses.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1:R},{2:R},{3:R},{4:R},{5:R},{6}",result.view,origin.x,origin.y,origin.z,meshDistance,bodyDistance,bodyName));
                            bool neck=anatomicalNeck&&y>=neckMin.y&&y<=neckMax.y&&horizontal>=(side?neckMin.z:neckMin.x)&&horizontal<=(side?neckMax.z:neckMax.x);
                            if(neck){result.neckMeshRays++;if(!bodyHit)result.neckMisses++;}
                            if(bodyHit){float difference=Mathf.Abs(meshDistance-bodyDistance);error+=difference;compared++;result.maxAbsoluteDepthError=Mathf.Max(result.maxAbsoluteDepthError,difference);}
                        }
                        else if(bodyHit)result.colliderOnlyRays++;
                    }
                    result.meanAbsoluteDepthError=compared>0?error/compared:0;results.Add(result);
                }
                File.WriteAllText(output,JsonUtility.ToJson(new Report{views=results.ToArray(),colliderCount=body.Count,gridSpacing=step,anatomicalNeckRegion=anatomicalNeck,neckRegionMin=neckMin,neckRegionMax=neckMax},true));
                File.WriteAllLines(Path.ChangeExtension(output,"csv"),misses);
            }
            finally{foreach(var go in temporary)UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
