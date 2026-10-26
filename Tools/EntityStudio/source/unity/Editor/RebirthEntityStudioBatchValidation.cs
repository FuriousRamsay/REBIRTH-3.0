using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityStudioBatchValidation
    {
        [Serializable]
        private sealed class Report
        {
            public bool success;
            public string unityVersion;
            public string editorAssembly;
            public bool testAssemblyCompiled;
            public bool simplifierPackageLoaded;
            public int generatedBones;
            public int validatedWeightVertices;
            public int colliderCount;
            public int fallbackColliderCount;
            public int oversizedColliderCount;
            public string fixtureSkeletonPlacement;
            public float minimumColliderCoverage;
            public float averageColliderCoverage;
            public string[] colliderFits;
            public string[] colliderSampleDiagnostics;
            public int lodCount;
            public int[] lodTriangles;
            public string[] checks;
            public string error;
        }

        public static void Run()
        {
            string reportPath=GetArgument("-entityStudioReport");
            var report=new Report
            {
                unityVersion=Application.unityVersion,
                editorAssembly="RebirthEntityStudio.Editor",
                testAssemblyCompiled=false,
                simplifierPackageLoaded=Type.GetType("UnityMeshSimplifier.LODGenerator, Whinarn.UnityMeshSimplifier.Runtime")!=null,
                checks=new string[0]
            };
            RebirthEntityRigProfile profile=null;
            GameObject source=null;
            Mesh denseSourceMesh=null;
            RebirthEntityRigWorkspace workspace=null;
            string generatedLodFolder=null;
            string[] checks=new string[7];
            try
            {
                var assemblies=CompilationPipeline.GetAssemblies(AssembliesType.Editor);
                bool editorCompiled=false;
                foreach(var assembly in assemblies)
                {
                    if(assembly.name==report.editorAssembly)editorCompiled=true;
                    if(assembly.name=="RebirthEntityStudio.EditorTests")report.testAssemblyCompiled=true;
                }
                if(!editorCompiled)throw new InvalidOperationException("The RebirthEntityStudio.Editor assembly did not appear in Unity's compiled Editor assembly list.");
                if(!report.testAssemblyCompiled)throw new InvalidOperationException("The RebirthEntityStudio.EditorTests assembly did not compile. Check Test Framework package and asmdef references.");
                if(!report.simplifierPackageLoaded)throw new InvalidOperationException("Unity Mesh Simplifier v3.1.1 did not load into the Editor domain.");
                checks[0]="Editor and EditMode test assemblies compiled";
                checks[1]="Pinned Unity Mesh Simplifier package loaded";

                profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
                profile.InitializeBipedDefaults();
                profile.entityClass="BatchValidationEntity";
                profile.sourceUpAxis=EntityModelUpAxis.Y;
                profile.generateTierMaterials=false;
                profile.createHitColliders=true;
                profile.createRagdoll=true;
                source=CreateSyntheticUnriggedHumanoid(out denseSourceMesh);
                FitSyntheticProfileToSource(profile,source.transform);
                report.fixtureSkeletonPlacement="MODEL_FITTED_TO_SYNTHETIC_HUMANOID_GEOMETRY";
                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);
                generatedLodFolder="Assets/EntityStudioGenerated/LODs/BatchValidationEntity_"+workspace.generatedAssetId;
                report.generatedBones=workspace.generatedSkeleton.GetComponentsInChildren<Transform>(true).Length;
                if(workspace.gameObject.tag!="E_Enemy"||workspace.generatedSkeleton.tag!="E_BP_BipedRoot")
                    throw new InvalidOperationException("The entity root or biped root tag was not assigned.");
                checks[2]="Game tags were added and applied";

                var renderers=profile.sourceSkinnedMeshes;
                if(renderers==null||renderers.Count!=11)throw new InvalidOperationException("The humanoid multi-part intake did not create one skinned renderer per source mesh.");
                foreach(var renderer in renderers)
                {
                    if(renderer==null||renderer.sharedMesh==null)throw new InvalidOperationException("Skinned renderer or generated mesh is missing.");
                    if(renderer.sharedMesh.boneWeights.Length!=renderer.sharedMesh.vertexCount)
                        throw new InvalidOperationException("Generated vertex weights are incomplete on "+renderer.name+".");
                    foreach(var weight in renderer.sharedMesh.boneWeights)
                    {
                        float sum=weight.weight0+weight.weight1+weight.weight2+weight.weight3;
                        if(float.IsNaN(sum)||float.IsInfinity(sum)||Mathf.Abs(sum-1f)>0.001f)
                            throw new InvalidOperationException("Skin weights do not sum to one on "+renderer.name+".");
                        if(!ValidInfluence(weight.boneIndex0,weight.weight0,renderer.bones.Length)||!ValidInfluence(weight.boneIndex1,weight.weight1,renderer.bones.Length)||
                           !ValidInfluence(weight.boneIndex2,weight.weight2,renderer.bones.Length)||!ValidInfluence(weight.boneIndex3,weight.weight3,renderer.bones.Length))
                            throw new InvalidOperationException("Skin weights contain an invalid bone index on "+renderer.name+".");
                        report.validatedWeightVertices++;
                    }
                    if(renderer.sharedMesh.bindposes.Length!=renderer.bones.Length)
                        throw new InvalidOperationException("Generated bind-pose count does not match the renderer bones on "+renderer.name+".");
                    for(int i=0;i<renderer.bones.Length;i++)
                    {
                        Matrix4x4 expected=renderer.bones[i].worldToLocalMatrix*renderer.transform.localToWorldMatrix;
                        Matrix4x4 actual=renderer.sharedMesh.bindposes[i];
                        for(int row=0;row<4;row++)for(int column=0;column<4;column++)
                            if(Mathf.Abs(expected[row,column]-actual[row,column])>0.0001f)
                                throw new InvalidOperationException("Bind pose mismatch for bone "+renderer.bones[i].name+" on "+renderer.name+".");
                    }
                }
                checks[3]="Offset/rotated multi-mesh source has normalized weights for "+report.validatedWeightVertices+" vertices, valid bone indices, and renderer-relative bind poses";

                var capsules=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true);
                var bodies=workspace.generatedSkeleton.GetComponentsInChildren<Rigidbody>(true);
                var joints=workspace.generatedSkeleton.GetComponentsInChildren<CharacterJoint>(true);
                if(capsules.Length<8||workspace.colliderFitReports==null||workspace.colliderFitReports.Count!=capsules.Length)
                    throw new InvalidOperationException("Mesh-fitted collider reports are incomplete.");
                if(bodies.Length!=capsules.Length||joints.Length!=bodies.Length-1)
                    throw new InvalidOperationException("Ragdoll body parent chain is incomplete.");
                foreach(var joint in joints)if(joint.connectedBody==null)
                    throw new InvalidOperationException("Ragdoll joint has no connected parent body: "+joint.name);
                foreach(var capsule in capsules)if(capsule.radius<=0f||capsule.height<2f*capsule.radius)
                    throw new InvalidOperationException("Mesh-fitted capsule dimensions are invalid: "+capsule.name);
                report.colliderCount=workspace.colliderFitReports.Count;
                report.minimumColliderCoverage=1f;
                float coverageTotal=0f;
                var fitSummaries=new System.Collections.Generic.List<string>();
                foreach(var fit in workspace.colliderFitReports)
                {
                    coverageTotal+=fit.estimatedSurfaceCoverage;
                    report.minimumColliderCoverage=Mathf.Min(report.minimumColliderCoverage,fit.estimatedSurfaceCoverage);
                    if(fit.usedProportionalFallback)report.fallbackColliderCount++;
                    if(fit.baseGameReferenceRadius>0f&&fit.radius>fit.baseGameReferenceRadius*1.5f)report.oversizedColliderCount++;
                    fitSummaries.Add(fit.colliderName+": samples="+fit.sampledVertices+", coverage="+fit.estimatedSurfaceCoverage.ToString("P1")+
                        ", radius="+fit.radius.ToString("F3")+", 3.3-reference="+fit.baseGameReferenceRadius.ToString("F3")+
                        ", height="+fit.height.ToString("F3")+", fallback="+fit.usedProportionalFallback);
                }
                report.averageColliderCoverage=workspace.colliderFitReports.Count>0?coverageTotal/workspace.colliderFitReports.Count:0f;
                report.colliderFits=fitSummaries.ToArray();
                foreach(var fit in workspace.colliderFitReports)
                    if((fit.colliderName=="LeftLowerArm"||fit.colliderName=="RightLowerArm")&&
                       (fit.usedProportionalFallback||fit.radius>.085f))
                        throw new InvalidOperationException("Synthetic forearm fit exceeded its known mesh radius (+padding): "+fit.colliderName+" radius="+fit.radius.ToString("F3"));
                    else if((fit.colliderName=="LeftUpperArm"||fit.colliderName=="RightUpperArm")&&
                       (fit.usedProportionalFallback||fit.radius>.10f))
                        throw new InvalidOperationException("Synthetic upper-arm fit exceeded its known mesh radius (+padding): "+fit.colliderName+" radius="+fit.radius.ToString("F3"));
                    else if(fit.colliderName=="Hips"&&(fit.usedProportionalFallback||fit.radius>.30f))
                        throw new InvalidOperationException("Synthetic torso fit exceeded its known body envelope: radius="+fit.radius.ToString("F3"));
                report.colliderSampleDiagnostics=new[]
                {
                    DescribeColliderSamples(workspace,"LeftArm","LeftForeArm"),
                    DescribeColliderSamples(workspace,"LeftForeArm","LeftHand"),
                    DescribeColliderSamples(workspace,"RightArm","RightForeArm"),
                    DescribeColliderSamples(workspace,"RightForeArm","RightHand")
                };
                checks[4]="Mesh-sampled capsule fit reports ("+report.colliderCount+" colliders, minimum coverage "+report.minimumColliderCoverage.ToString("P1")+") and explicit ragdoll body/joint chain";

                var lods=workspace.lodGroup!=null?workspace.lodGroup.GetLODs():new LOD[0];
                if(lods.Length<2)throw new InvalidOperationException("Simplified LODGroup was not generated.");
                report.lodCount=lods.Length;
                report.lodTriangles=new int[lods.Length];
                for(int i=0;i<lods.Length;i++)
                {
                    int triangleCount=0;
                    foreach(var lodRenderer in lods[i].renderers)
                    {
                        var skinned=lodRenderer as SkinnedMeshRenderer;
                        if(skinned==null||skinned.sharedMesh==null)continue;
                        for(int sub=0;sub<skinned.sharedMesh.subMeshCount;sub++)
                            triangleCount+=skinned.sharedMesh.GetTriangles(sub).Length/3;
                    }
                    report.lodTriangles[i]=triangleCount;
                    if(triangleCount<=0)throw new InvalidOperationException("LOD"+i+" has no skinned triangles.");
                    if(i>0&&triangleCount>=report.lodTriangles[i-1])
                        throw new InvalidOperationException("LOD"+i+" did not reduce triangle count from "+report.lodTriangles[i-1]+" to below its previous level.");
                }
                checks[5]="Generated LODs reduce actual aggregate triangle counts: "+string.Join(" -> ",Array.ConvertAll(report.lodTriangles,x=>x.ToString()));
                foreach(var finding in RebirthEntityRigValidator.Inspect(workspace))
                    if(finding.severity=="ERROR")throw new InvalidOperationException("Workspace validator reported "+finding);
                checks[6]="Workspace validator has no structural errors or stale-input findings";
                report.checks=checks;
                report.success=true;
            }
            catch(Exception exception)
            {
                report.success=false;
                report.error=exception.ToString();
                Debug.LogException(exception);
            }
            finally
            {
                if(workspace!=null)UnityEngine.Object.DestroyImmediate(workspace.gameObject);
                if(source!=null)UnityEngine.Object.DestroyImmediate(source);
                if(denseSourceMesh!=null)UnityEngine.Object.DestroyImmediate(denseSourceMesh);
                if(profile!=null)UnityEngine.Object.DestroyImmediate(profile);
                if(!string.IsNullOrEmpty(generatedLodFolder))AssetDatabase.DeleteAsset(generatedLodFolder);
                if(!string.IsNullOrEmpty(reportPath))
                {
                    string parent=Path.GetDirectoryName(reportPath);
                    if(!string.IsNullOrEmpty(parent))Directory.CreateDirectory(parent);
                    File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));
                }
            }
            if(report.success)Debug.Log("REBIRTH Entity Studio Unity batch validation passed.");
            else Debug.LogError("REBIRTH Entity Studio Unity batch validation failed: "+report.error);
            EditorApplication.Exit(report.success?0:1);
        }

        private static string GetArgument(string key)
        {
            string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length;i++)
            {
                if(args[i].StartsWith(key+"=",StringComparison.Ordinal))return args[i].Substring(key.Length+1).Trim('"');
                if(args[i]==key&&i+1<args.Length)return args[i+1].Trim('"');
            }
            return null;
        }

        private static GameObject CreateSyntheticUnriggedHumanoid(out Mesh denseTorso)
        {
            var root=new GameObject("SyntheticUnriggedHumanoid");
            GameObject Part(PrimitiveType type,string name,Vector3 position,Vector3 euler,Vector3 scale)
            {
                var part=GameObject.CreatePrimitive(type);
                part.name=name;
                part.transform.SetParent(root.transform,false);
                part.transform.localPosition=position;
                part.transform.localRotation=Quaternion.Euler(euler);
                part.transform.localScale=scale;
                return part;
            }

            var torso=Part(PrimitiveType.Capsule,"Torso",new Vector3(0f,1.29f,0f),Vector3.zero,new Vector3(.48f,.40f,.31f));
            denseTorso=Subdivide(torso.GetComponent<MeshFilter>().sharedMesh,2);
            torso.GetComponent<MeshFilter>().sharedMesh=denseTorso;
            Part(PrimitiveType.Sphere,"Head",new Vector3(0f,1.88f,0f),Vector3.zero,new Vector3(.22f,.25f,.22f));
            // These long axes follow each side's shoulder-to-hand direction. The old signs
            // were mirrored: their surfaces lay almost perpendicular to the matching bone
            // segments, which made a correct mesh fit report an artificially large radius.
            Part(PrimitiveType.Capsule,"LeftUpperArm",new Vector3(-.39f,1.36f,0f),new Vector3(0f,0f,-52f),new Vector3(.12f,.18f,.12f));
            Part(PrimitiveType.Capsule,"LeftLowerArm",new Vector3(-.74f,1.145f,0f),new Vector3(0f,0f,-63.4f),new Vector3(.10f,.235f,.10f));
            Part(PrimitiveType.Capsule,"RightUpperArm",new Vector3(.39f,1.36f,0f),new Vector3(0f,0f,52f),new Vector3(.12f,.18f,.12f));
            Part(PrimitiveType.Capsule,"RightLowerArm",new Vector3(.74f,1.145f,0f),new Vector3(0f,0f,63.4f),new Vector3(.10f,.235f,.10f));
            Part(PrimitiveType.Capsule,"LeftUpperLeg",new Vector3(-.19f,.70f,0f),Vector3.zero,new Vector3(.18f,.28f,.18f));
            Part(PrimitiveType.Capsule,"LeftLowerLeg",new Vector3(-.19f,.23f,0f),Vector3.zero,new Vector3(.14f,.28f,.14f));
            Part(PrimitiveType.Capsule,"RightUpperLeg",new Vector3(.19f,.70f,0f),Vector3.zero,new Vector3(.18f,.28f,.18f));
            Part(PrimitiveType.Capsule,"RightLowerLeg",new Vector3(.19f,.23f,0f),Vector3.zero,new Vector3(.14f,.28f,.14f));
            Part(PrimitiveType.Cube,"SyntheticAccessoryMesh",new Vector3(.03f,1.06f,.12f),new Vector3(0f,19f,0f),new Vector3(.24f,.20f,.12f));
            return root;
        }

        private static string DescribeColliderSamples(RebirthEntityRigWorkspace workspace,string startName,string endName)
        {
            Transform start=FindTransform(workspace.generatedSkeleton,startName);
            Transform end=FindTransform(workspace.generatedSkeleton,endName);
            Vector3 axis=(end.position-start.position).normalized;
            var descriptions=new System.Collections.Generic.List<string>();
            foreach(var renderer in workspace.profile.sourceSkinnedMeshes)
            {
                int endIndex=Array.IndexOf(renderer.bones,end);
                var mesh=renderer.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
                int count=0;float maxRadial=0f,minAxial=float.PositiveInfinity,maxAxial=float.NegativeInfinity;
                for(int i=0;i<vertices.Length&&i<weights.Length;i++)
                {
                    BoneWeight weight=weights[i];
                    // For a direct parent-child limb segment, the child bone's weights
                    // describe that segment; the start bone belongs to the preceding one.
                    float influence=WeightForBone(weight,endIndex);
                    if(influence<.7f)continue;
                    Vector3 offset=renderer.transform.TransformPoint(vertices[i])-start.position;
                    float along=Vector3.Dot(offset,axis);
                    float radial=(offset-axis*along).magnitude;
                    count++;maxRadial=Mathf.Max(maxRadial,radial);minAxial=Mathf.Min(minAxial,along);maxAxial=Mathf.Max(maxAxial,along);
                }
                if(count>0)descriptions.Add(renderer.name+"="+count+" samples,rmax="+maxRadial.ToString("F3")+",axial="+minAxial.ToString("F3")+".."+maxAxial.ToString("F3"));
            }
            return startName+"->"+endName+" "+start.position.ToString("F3")+" -> "+end.position.ToString("F3")+"; "+string.Join("; ",descriptions);
        }

        private static Transform FindTransform(Transform root,string name)
        {
            foreach(Transform child in root.GetComponentsInChildren<Transform>(true))
                if(child.name==name)return child;
            throw new InvalidOperationException("Missing generated bone "+name+".");
        }

        private static float WeightForBone(BoneWeight weight,int index)
        {
            if(index<0)return 0f;
            return (weight.boneIndex0==index?weight.weight0:0f)+(weight.boneIndex1==index?weight.weight1:0f)+
                (weight.boneIndex2==index?weight.weight2:0f)+(weight.boneIndex3==index?weight.weight3:0f);
        }

        private static void FitSyntheticProfileToSource(RebirthEntityRigProfile profile,Transform sourceRoot)
        {
            Bounds bounds=RebirthEntityRigBuilder.SourceBoundsInRootSpace(sourceRoot);
            void Place(string bone,float x,float y,float z=0f)
            {
                var spec=profile.bones.Find(candidate=>candidate!=null&&candidate.name==bone);
                if(spec==null)throw new InvalidOperationException("Synthetic profile is missing bone "+bone+".");
                spec.normalizedPosition=RebirthEntityRigBuilder.NormalizeBonePositionInBounds(bounds,new Vector3(x,y,z));
            }

            // This fixture is intentionally hand-fitted to the known capsule layout. It
            // validates mesh sampling/fit after anatomy has been reviewed, while the Scene
            // handles remain the authoring route for arbitrary supplied models.
            Place("Hips",0f,.94f);Place("Spine",0f,1.08f);Place("Spine1",0f,1.22f);
            Place("Spine2",0f,1.36f);Place("Spine3",0f,1.53f);Place("Neck",0f,1.74f);Place("Head",0f,1.88f);
            // Arm/ForeArm/Hand are joint landmarks (shoulder, elbow, wrist), not
            // segment centers. Put them at the ends of the synthetic limb meshes so
            // each collider axis follows the surface it is expected to contain.
            Place("LeftShoulder",-.16f,1.51f);Place("LeftArm",-.25f,1.47f);
            Place("LeftForeArm",-.53f,1.25f);Place("LeftHand",-.95f,1.04f);
            Place("RightShoulder",.16f,1.51f);Place("RightArm",.25f,1.47f);
            Place("RightForeArm",.53f,1.25f);Place("RightHand",.95f,1.04f);
            Place("LeftUpLeg",-.19f,.94f);Place("LeftLeg",-.19f,.48f);Place("LeftFoot",-.19f,-.02f);
            Place("RightUpLeg",.19f,.94f);Place("RightLeg",.19f,.48f);Place("RightFoot",.19f,-.02f);
        }

        private static bool ValidInfluence(int index,float weight,int boneCount)
        {return weight>=0f&&(weight==0f||(index>=0&&index<boneCount));}

        private static Mesh Subdivide(Mesh source,int levels)
        {
            var vertices=new System.Collections.Generic.List<Vector3>(source.vertices);
            var uv=new System.Collections.Generic.List<Vector2>(source.uv);
            var submeshes=new System.Collections.Generic.List<int[]>();
            for(int s=0;s<source.subMeshCount;s++)submeshes.Add(source.GetTriangles(s));
            for(int level=0;level<levels;level++)
            {
                var midpointCache=new System.Collections.Generic.Dictionary<long,int>();
                int Midpoint(int a,int b)
                {
                    int low=Mathf.Min(a,b),high=Mathf.Max(a,b);long key=((long)low<<32)|(uint)high;
                    if(midpointCache.TryGetValue(key,out int existing))return existing;
                    int index=vertices.Count;vertices.Add((vertices[a]+vertices[b])*.5f);
                    uv.Add((uv[a]+uv[b])*.5f);midpointCache[key]=index;return index;
                }
                for(int s=0;s<submeshes.Count;s++)
                {
                    int[] triangles=submeshes[s];var refined=new System.Collections.Generic.List<int>(triangles.Length*4);
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                        int ab=Midpoint(a,b),bc=Midpoint(b,c),ca=Midpoint(c,a);
                        refined.AddRange(new[]{a,ab,ca, ab,b,bc, ca,bc,c, ab,bc,ca});
                    }
                    submeshes[s]=refined.ToArray();
                }
            }
            var result=new Mesh{name="SyntheticDenseCapsule"};
            result.indexFormat=vertices.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;
            result.SetVertices(vertices);result.SetUVs(0,uv);result.subMeshCount=submeshes.Count;
            for(int s=0;s<submeshes.Count;s++)result.SetTriangles(submeshes[s],s,false);
            result.RecalculateNormals();result.RecalculateBounds();return result;
        }
    }
}
