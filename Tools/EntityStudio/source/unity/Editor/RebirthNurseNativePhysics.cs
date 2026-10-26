using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthNurseNativePhysics
    {
        public static void ApplySaved()
        {
            try
            {
                var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthNurse/RebirthNurse_RigReview.unity");
                var workspace=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(value=>value!=null);
                Configure(workspace);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                RebirthNurseSourcePreservation.Run();
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }

        public static void Configure(RebirthEntityRigWorkspace workspace)
        {
            const string game="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die";
            var body=XDocument.Load(game+"/Data/Config/physicsbodies.xml").Descendants("body").Single(value=>(string)value.Attribute("name")=="zombieStandard");
            var common=AssetBundle.LoadFromFile(game+"/Data/Addressables/Standalone/zombies_assets_entities/zombies/common.bundle");
            var bundle=AssetBundle.LoadFromFile(game+"/Data/Addressables/Standalone/zombies_assets_entities/zombies/nurse.bundle");
            if(bundle==null)throw new IOException("Native physics reference bundle unavailable.");
            try
            {
                var native=bundle.LoadAsset<GameObject>(bundle.GetAllAssetNames().Single(value=>value.EndsWith("/ZNurse.prefab",StringComparison.OrdinalIgnoreCase)));
                var sourceRenderer=native.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(value=>value.sharedMesh!=null?value.sharedMesh.vertexCount:0).First();
                float nativeHeight=sourceRenderer.sharedMesh.bounds.size.y*Mathf.Abs(sourceRenderer.transform.lossyScale.y);
                float scale=workspace.profile.estimatedHeight/Mathf.Max(nativeHeight,.1f);workspace.profile.colliderModes.Clear();
                foreach(var entry in body.Elements("collider"))
                {
                    string path=Property(entry,"path");Transform source=native.transform.Find("Origin/"+path),target=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,Path.GetFileName(path));
                    if(source==null||target==null)throw new InvalidDataException("Native collider role cannot resolve: "+path);
                    if(workspace.profile.sourceModelPath.Contains("RebirthNurseReplacement"))
                    {
                        var original=source.GetComponent<CapsuleCollider>();var capsule=target.GetComponent<CapsuleCollider>();
                        if(original==null||capsule==null)throw new InvalidDataException("Native capsule missing: "+path);
                        string endName=target.name=="Hips"?null:target.name=="Spine2"?"Neck":
                            target.name.EndsWith("ForeArm",StringComparison.Ordinal)?target.name.Replace("ForeArm","HandMiddle4"):
                            target.name.EndsWith("Arm",StringComparison.Ordinal)?target.name.Replace("Arm","ForeArm"):
                            target.name.EndsWith("UpLeg",StringComparison.Ordinal)?target.name.Replace("UpLeg","Leg"):
                            target.name.EndsWith("Leg",StringComparison.Ordinal)?target.name.Replace("Leg","Foot"):null;
                        var nativeEnd=endName==null?null:native.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==endName);
                        var targetEnd=endName==null?null:RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,endName);
                        Quaternion alignment=Quaternion.identity;float lengthScale=scale;
                        if(nativeEnd!=null&&targetEnd!=null)
                        {
                            Vector3 from=nativeEnd.position-source.position,to=targetEnd.position-target.position;
                            alignment=Quaternion.FromToRotation(from,to);lengthScale=to.magnitude/Mathf.Max(from.magnitude,.00001f);
                        }
                        Vector3 nativeAxis=original.direction==0?Vector3.right:original.direction==1?Vector3.up:Vector3.forward;
                        Vector3 axis=target.InverseTransformDirection(alignment*source.TransformDirection(nativeAxis));
                        capsule.direction=Mathf.Abs(axis.x)>Mathf.Abs(axis.y)&&Mathf.Abs(axis.x)>Mathf.Abs(axis.z)?0:Mathf.Abs(axis.y)>Mathf.Abs(axis.z)?1:2;
                        Vector3 nativeScale=source.lossyScale,targetScale=target.lossyScale;
                        float nativeAcross=Mathf.Max(Mathf.Abs(nativeScale[(original.direction+1)%3]),Mathf.Abs(nativeScale[(original.direction+2)%3]));
                        float targetAcross=Mathf.Max(Mathf.Abs(targetScale[(capsule.direction+1)%3]),Mathf.Abs(targetScale[(capsule.direction+2)%3]));
                        capsule.radius=original.radius*nativeAcross*scale/Mathf.Max(targetAcross,.00001f);
                        capsule.height=Mathf.Max(2*capsule.radius,original.height*Mathf.Abs(nativeScale[original.direction])*lengthScale/Mathf.Max(Mathf.Abs(targetScale[capsule.direction]),.00001f));
                        Vector3 center=source.TransformVector(original.center);
                        Vector3 axisWorld=source.TransformDirection(nativeAxis);Vector3 axial=axisWorld*Vector3.Dot(center,axisWorld);
                        capsule.center=target.InverseTransformPoint(target.position+alignment*(axial*lengthScale+(center-axial)*scale));
                        var fit=workspace.colliderFitReports.FirstOrDefault(f=>f.collider==capsule);
                        if(fit!=null){fit.radius=capsule.radius*targetAcross;fit.height=capsule.height*Mathf.Abs(targetScale[capsule.direction]);fit.sampledVertices=0;fit.estimatedSurfaceCoverage=0;fit.usesPoseUnionCoverage=false;}
                        Debug.Log("NATIVE CAPSULE RETARGET: "+target.name+" worldRadius="+(capsule.radius*targetAcross)+" worldHeight="+(capsule.height*Mathf.Abs(targetScale[capsule.direction])));
                    }
                    var nativeBody=source.GetComponent<Rigidbody>();var targetBody=target.GetComponent<Rigidbody>();
                    if(nativeBody==null||targetBody==null)throw new InvalidDataException("Native ragdoll body cannot resolve: "+path);
                    targetBody.mass=nativeBody.mass;targetBody.drag=nativeBody.drag;targetBody.angularDrag=nativeBody.angularDrag;
                    targetBody.useGravity=nativeBody.useGravity;targetBody.constraints=nativeBody.constraints;
                    targetBody.interpolation=nativeBody.interpolation;targetBody.collisionDetectionMode=nativeBody.collisionDetectionMode;
                    targetBody.detectCollisions=nativeBody.detectCollisions;targetBody.isKinematic=true;
                    var nativeJoint=source.GetComponent<CharacterJoint>();var targetJoint=target.GetComponent<CharacterJoint>();
                    if((nativeJoint==null)!=(targetJoint==null))throw new InvalidDataException("Native ragdoll joint role differs: "+path);
                    if(nativeJoint!=null)
                    {
                        // Retain the fitted anatomical anchors and hinge frame;
                        // copy the game's numeric joint behavior for this role.
                        targetJoint.lowTwistLimit=nativeJoint.lowTwistLimit;targetJoint.highTwistLimit=nativeJoint.highTwistLimit;
                        targetJoint.swing1Limit=nativeJoint.swing1Limit;targetJoint.swing2Limit=nativeJoint.swing2Limit;
                        targetJoint.twistLimitSpring=nativeJoint.twistLimitSpring;targetJoint.swingLimitSpring=nativeJoint.swingLimitSpring;
                        targetJoint.massScale=nativeJoint.massScale;targetJoint.connectedMassScale=nativeJoint.connectedMassScale;
                        targetJoint.breakForce=nativeJoint.breakForce;targetJoint.breakTorque=nativeJoint.breakTorque;
                        targetJoint.enableCollision=nativeJoint.enableCollision;targetJoint.enablePreprocessing=nativeJoint.enablePreprocessing;
                        targetJoint.enableProjection=nativeJoint.enableProjection;targetJoint.projectionDistance=nativeJoint.projectionDistance;
                        targetJoint.projectionAngle=nativeJoint.projectionAngle;
                    }
                    workspace.profile.colliderModes.Add(new EntityColliderModeSpec{bone=target.name,
                        collisionScale=Vector(Property(entry,"collisionScale")),ragdollScale=Vector(Property(entry,"ragdollScale")),
                        collisionOffset=Offset(Property(entry,"collisionOffset")),ragdollOffset=Offset(Property(entry,"ragdollOffset")),
                        collisionLayer=int.Parse(Property(entry,"collisionLayer"),CultureInfo.InvariantCulture),ragdollLayer=int.Parse(Property(entry,"ragdollLayer"),CultureInfo.InvariantCulture)});
                    Vector3 Offset(string text){Vector3 rootVector=native.transform.InverseTransformVector(source.TransformVector(Vector(text)));return target.InverseTransformVector(workspace.transform.TransformVector(rootVector*scale));}
                }
                workspace.profile.ragdollMass=workspace.generatedSkeleton.GetComponentsInChildren<Rigidbody>(true).Sum(value=>value.mass);
                foreach(var fit in workspace.colliderFitReports)
                    if(fit.collider!=null&&fit.collider.name.StartsWith("EntityStudio_Compound_",StringComparison.Ordinal))
                    {
                        bool torso=fit.collider.name.StartsWith("EntityStudio_Compound_Hips",StringComparison.Ordinal)||fit.collider.name.StartsWith("EntityStudio_Compound_Spine2",StringComparison.Ordinal);
                        var parent=torso?workspace.profile.colliderModes.Find(mode=>mode.bone==fit.collider.transform.parent.name):null;
                        if(torso&&parent==null)throw new InvalidDataException("Compound torso native mode parent is missing.");
                        workspace.profile.colliderModes.Add(new EntityColliderModeSpec{bone=fit.collider.name,
                            collisionScale=parent!=null?parent.collisionScale:Vector3.one,ragdollScale=parent!=null?parent.ragdollScale:Vector3.one,
                            collisionOffset=parent!=null?parent.collisionOffset:Vector3.zero,ragdollOffset=parent!=null?parent.ragdollOffset:Vector3.zero,collisionLayer=0,ragdollLayer=21});
                    }
                var flesh=native.GetComponentsInChildren<Collider>(true).Select(value=>value.sharedMaterial).FirstOrDefault(value=>value!=null);
                if(flesh!=null)
                {
                    string path="Assets/EntityStudioGenerated/RebirthNurse/RebirthNurseFlesh.asset";
                    var material=AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
                    if(material==null){material=UnityEngine.Object.Instantiate(flesh);material.name="RebirthNurseFlesh";AssetDatabase.CreateAsset(material,path);}
                    else {EditorUtility.CopySerialized(flesh,material);material.name="RebirthNurseFlesh";EditorUtility.SetDirty(material);}
                    workspace.profile.ragdollMaterial=material;
                    foreach(var fit in workspace.colliderFitReports)fit.collider.sharedMaterial=material;
                    Debug.Log("REBIRTH NATIVE FLESH: source="+flesh.name+" static="+material.staticFriction+" dynamic="+material.dynamicFriction+" bounce="+material.bounciness+" frictionCombine="+material.frictionCombine+" bounceCombine="+material.bounceCombine);
                }
                else Debug.LogWarning("Native flesh PhysicMaterial dependency is unresolved; native material parity remains pending.");
                EditorUtility.SetDirty(workspace.profile);AssetDatabase.SaveAssets();workspace.profileBuildFingerprint=RebirthEntityRigBuilder.ComputeProfileFingerprint(workspace.profile,workspace.sourceModelKey);
                Debug.Log("REBIRTH NATIVE PHYSICS: modes="+workspace.profile.colliderModes.Count+" offsetHeightScale="+scale);
            }
            finally{bundle.Unload(true);if(common!=null)common.Unload(true);}
        }
        static string Property(XElement element,string name){return (string)element.Elements("property").Single(value=>(string)value.Attribute("name")==name).Attribute("value");}
        static Vector3 Vector(string value){string[] values=value.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);return new Vector3(float.Parse(values[0],CultureInfo.InvariantCulture),float.Parse(values[1],CultureInfo.InvariantCulture),float.Parse(values[2],CultureInfo.InvariantCulture));}
    }
}
