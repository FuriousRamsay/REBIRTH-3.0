using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

#nullable disable

// Runs through the existing authenticated, main-thread Game Bridge dispatcher.
// It does not steer the player camera, send input, or save entity changes.
[HarmonyPatch(typeof(RebirthGameBridgeHandlers),"Dispatch")]
public static class RebirthEntityStudioBridge
{
    public static bool Prefix(BridgeRequest req)
    {
        bool render=req.Path=="/entitystudio/render",physics=req.Path=="/entitystudio/physics",textures=req.Path=="/entitystudio/textures",prepare=req.Path=="/entitystudio/textureprepare",inspect=req.Path=="/entitystudio/entity";
        if(!render&&!physics&&!textures&&!prepare&&!inspect)return true;
        if(req.Method!=((render||prepare)?"POST":"GET")){req.Fail("Entity Studio endpoint method mismatch",405);return false;}
        try
        {
            if(!int.TryParse(req.QueryString("entity"),out int id)||id<=0)throw new ArgumentException("A positive entity id is required.");
            World world=GameManager.Instance!=null?GameManager.Instance.World:null;
            if(inspect)
            {
                Entity target=world!=null?world.GetEntity(id):null;if(target==null)throw new ArgumentException("Exact entity is not present in the current world.");
                req.Complete(InspectEntity(target));return false;
            }
            EntityZombie entity=world!=null?world.GetEntity(id) as EntityZombie:null;
            if(entity==null)throw new ArgumentException("Zombie is not present in the current world.");
            if(prepare)
            {
                ReleaseTextureLease(id);
                if(req.QueryString("release")!="1")
                {
                    var lease=new TextureLease();
                    textureLeases.Add(id,lease);
                    try{foreach(var texture in ModelTextures(entity).Keys)if(texture.streamingMipmaps){lease.previous.Add(texture,texture.requestedMipmapLevel);if(!textureOriginalRequests.ContainsKey(texture))textureOriginalRequests.Add(texture,texture.requestedMipmapLevel);texture.requestedMipmapLevel=0;}}
                    catch{ReleaseTextureLease(id);throw;}
                    GameManager.Instance.StartCoroutine(ExpireTextureLease(id,lease));
                }
            }
            if(render)
            {try{req.Complete(Capture(entity,req.QueryString("pose")!="gpu",req.QueryString("focus")=="face"));}finally{ReleaseTextureLease(id);}}
            else req.Complete(physics?InspectPhysics(entity):InspectTextures(entity));
        }
        catch(Exception error){req.Fail("Entity Studio inspection failed: "+error.GetType().Name+": "+error.Message,500);}
        return false;
    }

    private static JObject InspectEntity(Entity entity)
    {
        Transform root=entity.transform;var renderers=new JArray();var all=root.GetComponentsInChildren<Renderer>(true);
        if(all.Length>128)throw new InvalidOperationException("Entity renderer count exceeds inspection limit.");
        foreach(var renderer in all)
        {
            Mesh mesh=renderer is SkinnedMeshRenderer skin?skin.sharedMesh:renderer.GetComponent<MeshFilter>()!=null?renderer.GetComponent<MeshFilter>().sharedMesh:null;
            var materials=new JArray();foreach(var material in renderer.sharedMaterials)if(material!=null)materials.Add(material.name);
            renderers.Add(new JObject{["path"]=HierarchyPath(renderer.transform,root),["enabled"]=renderer.enabled,["active"]=renderer.gameObject.activeInHierarchy,["mesh"]=mesh!=null?mesh.name:null,["meshBoundsSize"]=mesh!=null?Vec(mesh.bounds.size):null,["worldBoundsSize"]=Vec(renderer.bounds.size),["worldBoundsCenter"]=Vec(renderer.bounds.center),["lossyScale"]=Vec(renderer.transform.lossyScale),["materials"]=materials});
        }
        return new JObject{["ok"]=true,["entityId"]=entity.entityId,["entityClass"]=entity.EntityClass!=null?entity.EntityClass.entityClassName:null,["entityName"]=entity is EntityAlive alive?alive.EntityName:null,["localizedClassName"]=entity.EntityClass!=null?Localization.Get(entity.EntityClass.entityClassName):null,["runtimeType"]=entity.GetType().FullName,["dead"]=entity.IsDead(),["position"]=Vec(entity.position),["rootScale"]=Vec(root.lossyScale),["configuredMesh"]=entity.EntityClass!=null?entity.EntityClass.Properties.GetString("Mesh"):null,["renderers"]=renderers};
    }

    private sealed class TextureLease {public readonly Dictionary<Texture2D,int> previous=new Dictionary<Texture2D,int>();}
    private static readonly Dictionary<int,TextureLease> textureLeases=new Dictionary<int,TextureLease>();
    private static readonly Dictionary<Texture2D,int> textureOriginalRequests=new Dictionary<Texture2D,int>();
    private static void ReleaseTextureLease(int entity)
    {
        if(!textureLeases.TryGetValue(entity,out var lease))return;textureLeases.Remove(entity);
        foreach(var pair in lease.previous)
        {
            bool held=false;foreach(var other in textureLeases.Values)if(other.previous.ContainsKey(pair.Key)){held=true;break;}
            if(held)continue;
            if(textureOriginalRequests.TryGetValue(pair.Key,out int original)&&pair.Key!=null){if(original<0)pair.Key.ClearRequestedMipmapLevel();else pair.Key.requestedMipmapLevel=original;}
            textureOriginalRequests.Remove(pair.Key);
        }
    }
    private static System.Collections.IEnumerator ExpireTextureLease(int entity,TextureLease lease)
    {
        yield return new WaitForSecondsRealtime(30);
        if(textureLeases.TryGetValue(entity,out var current)&&ReferenceEquals(current,lease))ReleaseTextureLease(entity);
    }
    private static Dictionary<Texture2D,JArray> ModelTextures(EntityZombie entity)
    {
        var result=new Dictionary<Texture2D,JArray>();Transform root=entity.emodel!=null?entity.emodel.GetModelTransform():null;
        if(root==null)throw new InvalidOperationException("Entity model unavailable.");
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var slots=renderer.sharedMaterials;for(int slot=0;slot<slots.Length;slot++)
            {
                var material=slots[slot];
                if(material==null)continue;
                foreach(string property in material.GetTexturePropertyNames())if(material.GetTexture(property) is Texture2D texture)
                {
                    if(!result.TryGetValue(texture,out var bindings)){bindings=new JArray();result.Add(texture,bindings);}
                    bindings.Add(new JObject{["renderer"]=renderer.name,["rendererPath"]=HierarchyPath(renderer.transform,root),["slot"]=slot,["material"]=material.name,["shader"]=material.shader!=null?material.shader.name:null,["property"]=property});
                }
            }
        }
        return result;
    }
    private static JObject InspectTextures(EntityZombie entity)
    {
        var textures=new JArray();bool ready=true;
        foreach(var pair in ModelTextures(entity))
        {
            var texture=pair.Key;bool loaded=!texture.streamingMipmaps||texture.IsRequestedMipmapLevelLoaded();ready&=loaded;
            textures.Add(new JObject{["name"]=texture.name,["instanceId"]=texture.GetInstanceID(),["width"]=texture.width,["height"]=texture.height,["format"]=texture.format.ToString(),["mipmapCount"]=texture.mipmapCount,["streaming"]=texture.streamingMipmaps,["desiredMipmapLevel"]=texture.desiredMipmapLevel,["loadedMipmapLevel"]=texture.loadedMipmapLevel,["loadingMipmapLevel"]=texture.loadingMipmapLevel,["requestedMipmapLevel"]=texture.requestedMipmapLevel,["requestedLevelLoaded"]=loaded,["bindings"]=pair.Value});
        }
        return new JObject{["ok"]=true,["entityId"]=entity.entityId,["adapterAssembly"]=typeof(RebirthEntityStudioMaterials).Assembly.GetName().Name,["supportsPerSlotMaterials"]=true,["mipLeaseActive"]=textureLeases.ContainsKey(entity.entityId),["requestedLevelsLoaded"]=ready,["globalMipmapLimit"]=QualitySettings.globalTextureMipmapLimit,["textures"]=textures};
    }

    private static JObject InspectPhysics(EntityZombie entity)
    {
        Transform physics=entity.PhysicsTransform,model=entity.emodel!=null?entity.emodel.GetModelTransform():null;
        var result=new JObject{["ok"]=true,["entityId"]=entity.entityId,["entityClass"]=entity.EntityClass.entityClassName,["physicsTransform"]=physics!=null?physics.name:null};
        var controller=entity.m_characterController;
        result["controller"]=controller!=null?new JObject{["type"]=controller.GetType().FullName,["center"]=Vec(controller.GetCenter()),["height"]=controller.GetHeight(),["radius"]=controller.GetRadius()}:null;
        result["nativeCollider"]=Collider(entity.nativeCollider);
        result["largeEntityBlocker"]=Collider(entity.physicsCapsuleCollider);
        result["physicsBaseHeight"]=entity.physicsBaseHeight;result["physicsHeightScale"]=entity.physicsHeightScale;
        var physicsColliders=new JArray();if(physics!=null)foreach(var collider in physics.GetComponents<Collider>())physicsColliders.Add(Collider(collider));result["physicsColliders"]=physicsColliders;
        var audio=new JArray();
        if(model!=null)foreach(var source in model.GetComponentsInChildren<AudioSource>(true))
            audio.Add(new JObject{["name"]=source.name,["tag"]=source.tag,["enabled"]=source.enabled,["active"]=source.gameObject.activeInHierarchy,["clip"]=source.clip!=null?source.clip.name:null,["isPlaying"]=source.isPlaying,["volume"]=source.volume,["spatialBlend"]=source.spatialBlend,["minDistance"]=source.minDistance,["maxDistance"]=source.maxDistance});
        result["modelAudioSources"]=audio;
        var events=new JArray();
        if(model!=null)foreach(var bridge in model.GetComponentsInChildren<AnimationEventBridge>(true))
            events.Add(new JObject{["name"]=bridge.name,["boundEntityId"]=bridge.entity!=null?bridge.entity.entityId:-1,["lastTimeStepPlayed"]=bridge.lastTimeStepPlayed,["secondsSinceLastStep"]=bridge.lastTimeStepPlayed>0?Time.time-bridge.lastTimeStepPlayed:-1});
        result["animationEvents"]=events;result["unityTime"]=Time.time;
        result["animationEventReceiverCount"]=events.Count;
        Animator animator=model!=null?model.GetComponentInChildren<Animator>(true):null;
        result["animator"]=animator!=null?new JObject{["avatarValid"]=animator.avatar!=null&&animator.avatar.isValid,["avatarHuman"]=animator.avatar!=null&&animator.avatar.isHuman,["applyRootMotion"]=animator.applyRootMotion,["controller"]=animator.runtimeAnimatorController!=null?animator.runtimeAnimatorController.name:null}:null;
        result["rigidbodies"]=model!=null?model.GetComponentsInChildren<Rigidbody>(true).Length:0;
        int ragdollBodies=0,dynamicBodies=0;
        if(model!=null)foreach(var body in model.GetComponentsInChildren<Rigidbody>(true))if(!body.CompareTag("AudioRigidBody")){ragdollBodies++;if(!body.isKinematic)dynamicBodies++;}
        result["ragdollRigidbodies"]=ragdollBodies;result["dynamicRagdollBodies"]=dynamicBodies;
        result["ragdollJoints"]=model!=null?model.GetComponentsInChildren<CharacterJoint>(true).Length:0;
        result["modelCapsules"]=model!=null?model.GetComponentsInChildren<CapsuleCollider>(true).Length:0;
        string nativeSource=entity.EntityClass.Properties.GetString(RebirthEntityStudioAnimatorPatch.SourceProperty);
        if(!string.IsNullOrWhiteSpace(nativeSource))
        {
            GameObject reference=DataLoader.LoadAsset<GameObject>(nativeSource);var referenceShapes=new JArray();var physicsNodes=new JArray();
            if(reference!=null)
            {
                foreach(var shape in reference.GetComponentsInChildren<Collider>(true))
                {JObject detail=Collider(shape);detail["tag"]=shape.tag;detail["path"]=HierarchyPath(shape.transform,reference.transform);referenceShapes.Add(detail);}
                foreach(var node in reference.GetComponentsInChildren<Transform>(true))if(node.name.IndexOf("Physics",StringComparison.OrdinalIgnoreCase)>=0||node.CompareTag("Physics"))
                {var components=new JArray();foreach(var component in node.GetComponents<Component>())if(component!=null)components.Add(component.GetType().FullName);physicsNodes.Add(new JObject{["path"]=HierarchyPath(node,reference.transform),["tag"]=node.tag,["components"]=components});}
            }
            result["nativeReference"]=new JObject{["source"]=nativeSource,["name"]=reference!=null?reference.name:null,["colliders"]=referenceShapes,["physicsNodes"]=physicsNodes};
        }
        return result;
    }
    private static string HierarchyPath(Transform node,Transform root)
    {var names=new List<string>();for(Transform current=node;current!=null;current=current.parent){names.Add(current.name);if(current==root)break;}names.Reverse();return string.Join("/",names);}
    private static JObject Collider(Collider collider)
    {
        if(collider==null)return null;
        var value=new JObject{["name"]=collider.name,["type"]=collider.GetType().FullName,["enabled"]=collider.enabled,["trigger"]=collider.isTrigger,["layer"]=collider.gameObject.layer,["boundsCenter"]=Vec(collider.bounds.center),["boundsSize"]=Vec(collider.bounds.size)};
        if(collider is CapsuleCollider capsule){value["center"]=Vec(capsule.center);value["radius"]=capsule.radius;value["height"]=capsule.height;value["direction"]=capsule.direction;}
        else if(collider is CharacterController character){value["center"]=Vec(character.center);value["radius"]=character.radius;value["height"]=character.height;}
        else if(collider is BoxCollider box){value["center"]=Vec(box.center);value["size"]=Vec(box.size);}
        return value;
    }
    private static JArray Vec(Vector3 vector)=>new JArray(vector.x,vector.y,vector.z);

    private static Mesh Snapshot(SkinnedMeshRenderer renderer,out string method)
    {
        Mesh source=renderer.sharedMesh;
        if(source==null)throw new InvalidOperationException("Skinned renderer has no mesh.");
        if(!source.isReadable||source.blendShapeCount>0)
        {
            var baked=new Mesh();renderer.BakeMesh(baked,false);method="unityBakeMesh(useScale:false)";return baked;
        }
        var result=UnityEngine.Object.Instantiate(source);
        try
        {
            Vector3[] points=source.vertices,normals=source.normals;Vector4[] tangents=source.tangents;BoneWeight[] weights=source.boneWeights;
            Transform[] bones=renderer.bones;Matrix4x4[] binds=source.bindposes;var matrices=new Matrix4x4[bones.Length];var normalMatrices=new Matrix4x4[bones.Length];
            if(weights.Length!=points.Length||binds.Length!=bones.Length)throw new InvalidOperationException("Readable mesh skinning data is incomplete.");
            for(int i=0;i<matrices.Length;i++){if(bones[i]==null)throw new InvalidOperationException("Skin bone is missing.");matrices[i]=renderer.transform.worldToLocalMatrix*bones[i].localToWorldMatrix*binds[i];normalMatrices[i]=matrices[i].inverse.transpose;}
            for(int vertex=0;vertex<points.Length;vertex++)
            {
                BoneWeight w=weights[vertex];Vector3 point=points[vertex];
                points[vertex]=matrices[w.boneIndex0].MultiplyPoint3x4(point)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(point)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(point)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(point)*w.weight3;
                if(normals.Length==points.Length){Vector3 normal=normals[vertex];normals[vertex]=(normalMatrices[w.boneIndex0].MultiplyVector(normal)*w.weight0+normalMatrices[w.boneIndex1].MultiplyVector(normal)*w.weight1+normalMatrices[w.boneIndex2].MultiplyVector(normal)*w.weight2+normalMatrices[w.boneIndex3].MultiplyVector(normal)*w.weight3).normalized;}
                if(tangents.Length==points.Length){Vector4 tangent=tangents[vertex];Vector3 direction=(matrices[w.boneIndex0].MultiplyVector(tangent)*w.weight0+matrices[w.boneIndex1].MultiplyVector(tangent)*w.weight1+matrices[w.boneIndex2].MultiplyVector(tangent)*w.weight2+matrices[w.boneIndex3].MultiplyVector(tangent)*w.weight3).normalized;tangents[vertex]=new Vector4(direction.x,direction.y,direction.z,tangent.w);}
            }
            result.vertices=points;if(normals.Length==points.Length)result.normals=normals;if(tangents.Length==points.Length)result.tangents=tangents;result.RecalculateBounds();method="explicitSkinMatrices";return result;
        }
        catch{UnityEngine.Object.Destroy(result);throw;}
    }

    private static JObject Capture(EntityZombie entity,bool staticPose,bool face=false)
    {
        Transform root=entity.emodel!=null?entity.emodel.GetModelTransform():null;
        if(root==null)throw new InvalidOperationException("Entity model is unavailable.");
        var all=root.GetComponentsInChildren<Renderer>(true);var selected=new HashSet<Renderer>();
        LODGroup group=root.GetComponentInChildren<LODGroup>(true);
        if(group!=null&&group.GetLODs().Length>0)
        {foreach(Renderer renderer in group.GetLODs()[0].renderers)if(renderer!=null&&renderer.gameObject.activeInHierarchy)selected.Add(renderer);}
        else
        {foreach(Renderer renderer in all)if(renderer.enabled&&renderer.gameObject.activeInHierarchy)selected.Add(renderer);}
        if(selected.Count==0)throw new InvalidOperationException("No active model renderers.");
        Bounds bounds=new Bounds();bool hasBounds=false;
        var snapshots=new Dictionary<Renderer,Mesh>();
        var displays=new List<GameObject>();
        var bakeMethods=new JArray();
        try
        {
        foreach(Renderer renderer in selected)
        {
            if(renderer is SkinnedMeshRenderer skin)
            {
                Mesh baked=Snapshot(skin,out string method);bakeMethods.Add(new JObject{["renderer"]=skin.name,["method"]=method,["lossyScale"]=Vec(skin.transform.lossyScale)});
                snapshots.Add(renderer,baked);
                foreach(Vector3 vertex in baked.vertices){Vector3 point=skin.transform.TransformPoint(vertex);if(!hasBounds){bounds=new Bounds(point,Vector3.zero);hasBounds=true;}else bounds.Encapsulate(point);}
            }
            else {if(!hasBounds){bounds=renderer.bounds;hasBounds=true;}else bounds.Encapsulate(renderer.bounds);}
        }
        if(!hasBounds||bounds.size.y<.01f)throw new InvalidOperationException("Model has no usable rendered bounds.");
        var saved=new Dictionary<Renderer,(int layer,bool enabled)>();bool groupEnabled=group!=null&&group.enabled;
        var cameraObject=new GameObject("__EntityStudioOffscreenCamera");cameraObject.hideFlags=HideFlags.HideAndDontSave;
        RenderTexture target=null;Texture2D image=null;RenderTexture previous=RenderTexture.active;
        try
        {
            if(group!=null)group.enabled=false;
            foreach(Renderer renderer in all)
            {
                saved.Add(renderer,(renderer.gameObject.layer,renderer.enabled));
                renderer.enabled=selected.Contains(renderer)&&(!staticPose||!snapshots.ContainsKey(renderer));
                if(renderer.enabled)renderer.gameObject.layer=31;
            }
            // Render precisely the CPU pose used for bounds. A skinned renderer may
            // still use a different GPU skinning update when a second camera renders.
            foreach(var pair in snapshots)
            {
                if(!staticPose)continue;
                var display=new GameObject("__EntityStudioPoseSnapshot");displays.Add(display);
                display.hideFlags=HideFlags.HideAndDontSave;display.layer=31;
                display.transform.SetParent(pair.Key.transform,false);
                display.AddComponent<MeshFilter>().sharedMesh=pair.Value;
                display.AddComponent<MeshRenderer>().sharedMaterials=pair.Key.sharedMaterials;
            }
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.16f,.19f);
            camera.cullingMask=1<<31;camera.nearClipPlane=.01f;camera.farClipPlane=30;camera.aspect=.75f;camera.fieldOfView=28;camera.allowHDR=false;camera.allowMSAA=false;
            float distance=Mathf.Max(bounds.size.y,bounds.size.x/camera.aspect)*.62f/Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            Vector3 direction=entity.transform.forward;if(direction.sqrMagnitude<.5f)direction=Vector3.forward;
            cameraObject.transform.position=bounds.center+direction.normalized*distance+Vector3.up*bounds.size.y*.04f;cameraObject.transform.LookAt(bounds.center);
            if(face)
            {
                Bounds eyeBounds=new Bounds();bool hasEyes=false;Vector3 normal=Vector3.zero;
                foreach(var pair in snapshots)if(pair.Key.CompareTag("E_BP_Eye"))
                {
                    Vector3[] points=pair.Value.vertices,normals=pair.Value.normals;
                    for(int i=0;i<points.Length;i++)
                    {
                        Vector3 point=pair.Key.transform.TransformPoint(points[i]);if(!hasEyes){eyeBounds=new Bounds(point,Vector3.zero);hasEyes=true;}else eyeBounds.Encapsulate(point);
                        if(i<normals.Length)normal+=pair.Key.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(normals[i]);
                    }
                }
                if(!hasEyes||normal.sqrMagnitude<.0001f)throw new InvalidOperationException("Face framing needs an active owned eye surface.");
                normal.Normalize();Vector3 right=Vector3.ProjectOnPlane(entity.transform.right,normal);if(right.sqrMagnitude<.01f)right=Vector3.Cross(Vector3.up,normal);
                Vector3 up=Vector3.Cross(normal,right.normalized).normalized;
                float height=Mathf.Clamp(bounds.size.y*.24f,.22f,.45f),width=Mathf.Clamp(eyeBounds.size.magnitude*2,.22f,.4f);
                distance=Mathf.Max(height,width/camera.aspect)*.62f/Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
                cameraObject.transform.position=eyeBounds.center+normal*distance;cameraObject.transform.LookAt(eyeBounds.center,up);
            }
            target=new RenderTexture(1200,1600,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;camera.Render();
            RenderTexture.active=target;image=new Texture2D(1200,1600,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,1600),0,0);image.Apply();
            string directory=Path.Combine(Path.GetDirectoryName(typeof(RebirthEntityStudioModApi).Assembly.Location),"Tools/EntityStudio/out/RuntimeCaptures");Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")+"_entity"+entity.entityId+".png");File.WriteAllBytes(path,ImageConversion.EncodeToPNG(image));
            return new JObject{["ok"]=true,["entityId"]=entity.entityId,["entityClass"]=entity.EntityClass.entityClassName,["path"]=path,["width"]=1200,["height"]=1600,["lod"]=0,["focus"]=face?"face":"model",["renderers"]=selected.Count,["poseSource"]=staticPose?"cpuSnapshot":"originalGpuSkin",["bakeMethods"]=bakeMethods,["boundsSize"]=Vec(bounds.size),["textureState"]=InspectTextures(entity),["playerCameraChanged"]=false};
        }
        finally
        {
            RenderTexture.active=previous;foreach(var pair in saved){if(pair.Key==null)continue;pair.Key.gameObject.layer=pair.Value.layer;pair.Key.enabled=pair.Value.enabled;}
            if(group!=null)group.enabled=groupEnabled;
            var captureCamera=cameraObject.GetComponent<Camera>();if(captureCamera!=null)captureCamera.targetTexture=null;
            if(target!=null){target.Release();UnityEngine.Object.Destroy(target);}if(image!=null)UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(cameraObject);
        }
        }
        finally
        {
            foreach(GameObject display in displays)if(display!=null){display.SetActive(false);UnityEngine.Object.Destroy(display);}
            foreach(Mesh mesh in snapshots.Values)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
        }
    }
}
