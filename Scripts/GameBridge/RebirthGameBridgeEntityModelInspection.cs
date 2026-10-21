using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

#nullable disable

/// <summary>Read-only inspection of one live zombie's rendered hierarchy and physical model setup.</summary>
public static class RebirthGameBridgeEntityModelInspection
{
    private const int MaxTransforms = 1024;
    private const int MaxComponents = 4096;

    public static void Handle(BridgeRequest req)
    {
        if (req.Method != "GET") { req.Fail("entity model inspection is read-only", 405); return; }
        int id;
        if (!int.TryParse(req.QueryString("entity"), out id) || id <= 0)
        { req.Fail("explicit positive zombie entity id required", 400); return; }
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityZombie zombie = world != null ? world.GetEntity(id) as EntityZombie : null;
        if (zombie == null || zombie.entityId != id)
        { req.Fail("exact zombie not present in the current world", 404); return; }

        try { req.Complete(Capture(zombie)); }
        catch (Exception ex) { req.Fail("entity model inspection failed: " + ex.GetType().Name + ": " + ex.Message, 500); }
    }

    private static JObject Capture(EntityZombie zombie)
    {
        Transform root = zombie.transform;
        if (root == null) throw new InvalidOperationException("zombie transform unavailable");
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        if (transforms == null || transforms.Length == 0) throw new InvalidOperationException("model hierarchy unavailable");
        if (transforms.Length > MaxTransforms) throw new InvalidOperationException("transform count exceeds safety limit " + MaxTransforms);

        var indexes = new Dictionary<Transform, int>(transforms.Length);
        for (int i = 0; i < transforms.Length; i++) indexes[transforms[i]] = i;
        var nodes = new JArray();
        var components = new JArray();
        int componentCount = 0;
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            int parentIndex;
            int parent = t.parent != null && indexes.TryGetValue(t.parent, out parentIndex) ? parentIndex : -1;
            var node = new JObject
            {
                ["index"] = i,
                ["parent"] = parent,
                ["name"] = t.name,
                ["path"] = Path(t, root),
                ["unityTag"] = SafeTag(t.gameObject),
                ["layer"] = t.gameObject.layer,
                ["activeSelf"] = t.gameObject.activeSelf,
                ["activeInHierarchy"] = t.gameObject.activeInHierarchy,
                ["childCount"] = t.childCount,
                ["localPosition"] = Vec(t.localPosition),
                ["localRotation"] = Quat(t.localRotation),
                ["localScale"] = Vec(t.localScale),
                ["worldPosition"] = Vec(t.position)
            };
            nodes.Add(node);

            Component[] atNode = t.GetComponents<Component>();
            var componentTypes = new JArray();
            for (int c = 0; c < atNode.Length; c++)
            {
                Component component = atNode[c];
                if (component == null) continue;
                if (++componentCount > MaxComponents) throw new InvalidOperationException("component count exceeds safety limit " + MaxComponents);
                string type = component.GetType().FullName;
                componentTypes.Add(type);
                JObject detail = ComponentDetail(component, t, root);
                if (detail != null)
                {
                    detail["node"] = i;
                    detail["path"] = Path(t, root);
                    detail["componentType"] = type;
                    components.Add(detail);
                }
            }
            node["components"] = componentTypes;
        }

        EModelBase model = zombie.emodel;
        Transform head = null, pelvis = null;
        try { if (model != null) head = model.GetHeadTransform(); } catch { }
        try { if (model != null) pelvis = model.GetPelvisTransform(); } catch { }
        var summary = new JObject
        {
            ["inspectionVersion"] = 2,
            ["entityId"] = zombie.entityId,
            ["entityClass"] = zombie.EntityClass != null ? zombie.EntityClass.entityClassName : zombie.GetType().Name,
            ["health"] = zombie.Health,
            ["alive"] = !zombie.IsDead(),
            ["entityUnityTag"] = SafeTag(zombie.gameObject),
            ["entityLayer"] = zombie.gameObject.layer,
            ["rootName"] = root.name,
            ["emodelType"] = model != null ? model.GetType().FullName : null,
            ["headTransformPath"] = head != null ? Path(head, root) : null,
            ["pelvisTransformPath"] = pelvis != null ? Path(pelvis, root) : null,
            ["transformCount"] = transforms.Length,
            ["componentCount"] = componentCount,
            ["transforms"] = nodes,
            ["modelComponents"] = components
        };
        var physics=new JObject();Transform physicsRoot=zombie.PhysicsTransform;
        physics["transformName"]=physicsRoot!=null?physicsRoot.name:null;
        physics["controllerType"]=zombie.m_characterController!=null?zombie.m_characterController.GetType().FullName:null;
        if(zombie.m_characterController!=null)
        {physics["center"]=Vec(zombie.m_characterController.GetCenter());physics["height"]=zombie.m_characterController.GetHeight();physics["radius"]=zombie.m_characterController.GetRadius();}
        var physicsComponents=new JArray();
        if(physicsRoot!=null)foreach(var component in physicsRoot.GetComponents<Component>())
        {if(component==null)continue;var detail=ComponentDetail(component,physicsRoot,physicsRoot);if(detail!=null){detail["componentType"]=component.GetType().FullName;physicsComponents.Add(detail);}}
        physics["components"]=physicsComponents;
        physics["nativeCollider"]=zombie.nativeCollider!=null?ColliderInfo(zombie.nativeCollider):null;
        summary["entityPhysics"]=physics;
        return summary;
    }

    private static JObject ComponentDetail(Component component, Transform node, Transform root)
    {
        var skinned = component as SkinnedMeshRenderer;
        if (skinned != null) return Skinned(skinned, node, root);
        var renderer = component as Renderer;
        if (renderer != null) return RendererInfo(renderer);
        var lod = component as LODGroup;
        if (lod != null) return LodInfo(lod, root);
        var collider = component as Collider;
        if (collider != null) return ColliderInfo(collider);
        var body = component as Rigidbody;
        if (body != null) return BodyInfo(body);
        var joint = component as Joint;
        if (joint != null) return JointInfo(joint, root);
        var animator = component as Animator;
        if (animator != null) return AnimatorInfo(animator);
        var filter = component as MeshFilter;
        if (filter != null) return MeshFilterInfo(filter);
        var audio=component as AudioSource;
        if(audio!=null)return new JObject{["kind"]="AudioSource",["enabled"]=audio.enabled,["clip"]=audio.clip!=null?audio.clip.name:null,["volume"]=audio.volume,["pitch"]=audio.pitch,["spatialBlend"]=audio.spatialBlend,["minDistance"]=audio.minDistance,["maxDistance"]=audio.maxDistance,["rolloffMode"]=audio.rolloffMode.ToString(),["loop"]=audio.loop,["playOnAwake"]=audio.playOnAwake};
        return null;
    }

    private static JObject Skinned(SkinnedMeshRenderer r, Transform node, Transform root)
    {
        var o = RendererInfo(r);
        Mesh mesh = r.sharedMesh;
        o["kind"] = "SkinnedMeshRenderer";
        o["mesh"] = MeshInfo(mesh);
        o["rootBonePath"] = r.rootBone != null ? Path(r.rootBone, root) : null;
        o["updateWhenOffscreen"] = r.updateWhenOffscreen;
        o["quality"] = r.quality.ToString();
        o["localBounds"] = Bounds(r.localBounds);
        var bones = new JArray();
        Transform[] source = r.bones;
        for (int i = 0; source != null && i < source.Length; i++)
            bones.Add(new JObject { ["index"] = i, ["path"] = source[i] != null ? Path(source[i], root) : null });
        o["bones"] = bones;
        return o;
    }

    private static JObject RendererInfo(Renderer r)
    {
        var o = new JObject
        {
            ["kind"] = r.GetType().Name,
            ["enabled"] = r.enabled,
            ["forceRenderingOff"] = r.forceRenderingOff,
            ["shadowCastingMode"] = r.shadowCastingMode.ToString(),
            ["receiveShadows"] = r.receiveShadows,
            ["renderingLayerMask"] = r.renderingLayerMask,
            ["bounds"] = Bounds(r.bounds),
            ["materials"] = new JArray()
        };
        Material[] materials = r.sharedMaterials;
        var slots = (JArray)o["materials"];
        for (int i = 0; materials != null && i < materials.Length; i++)
            slots.Add(MaterialInfo(materials[i], i));
        return o;
    }

    private static JObject MaterialInfo(Material material, int slot)
    {
        if (material == null) return new JObject { ["slot"] = slot, ["missing"] = true };
        var o = new JObject
        {
            ["slot"] = slot,
            ["name"] = material.name,
            ["shader"] = material.shader != null ? material.shader.name : null,
            ["renderQueue"] = material.renderQueue,
            ["enableInstancing"] = material.enableInstancing,
            ["keywords"] = new JArray(material.shaderKeywords),
            ["textureProperties"] = new JArray()
        };
        var props = (JArray)o["textureProperties"];
        string[] textureNames = material.GetTexturePropertyNames();
        for (int i = 0; textureNames != null && i < textureNames.Length; i++)
        {
            string property = textureNames[i];
            Texture texture = null;
            try { texture = material.GetTexture(property); } catch { }
            Vector2 scale = Vector2.one, offset = Vector2.zero;
            try { scale = material.GetTextureScale(property); offset = material.GetTextureOffset(property); } catch { }
            props.Add(new JObject
            {
                ["property"] = property,
                ["texture"] = texture != null ? texture.name : null,
                ["width"] = texture != null ? texture.width : 0,
                ["height"] = texture != null ? texture.height : 0,
                ["filterMode"] = texture != null ? texture.filterMode.ToString() : null,
                ["anisoLevel"] = texture != null ? texture.anisoLevel : 0,
                ["wrapModeU"] = texture != null ? texture.wrapModeU.ToString() : null,
                ["wrapModeV"] = texture != null ? texture.wrapModeV.ToString() : null,
                ["format"] = TextureFormatName(texture),
                ["mipmapCount"] = MipmapCount(texture),
                ["streamingMipmaps"] = StreamingMipmaps(texture),
                ["scale"] = Vec(scale),
                ["offset"] = Vec(offset)
            });
        }
        AddColor(material, o, "_Color", "baseColor");
        AddColor(material, o, "_BaseColor", "baseColorSRP");
        AddColor(material, o, "_EmissionColor", "emissionColor");
        AddFloat(material, o, "_Metallic", "metallic");
        AddFloat(material, o, "_Glossiness", "glossiness");
        AddFloat(material, o, "_Smoothness", "smoothness");
        AddFloat(material, o, "_Cutoff", "alphaCutoff");
        // Game/Character uses properties such as _EmissiveColor rather than the
        // Standard shader's fixed names. Report every declared value for comparison.
        var shaderValues=new JObject();o["shaderProperties"]=shaderValues;
        Shader shader=material.shader;
        if(shader!=null)
            for(int i=0;i<shader.GetPropertyCount();i++)
            {
                string property=shader.GetPropertyName(i);
                var type=shader.GetPropertyType(i);
                try
                {
                    if(type==UnityEngine.Rendering.ShaderPropertyType.Color)shaderValues[property]=ColorJson(material.GetColor(property));
                    else if(type==UnityEngine.Rendering.ShaderPropertyType.Vector)
                    {Vector4 value=material.GetVector(property);shaderValues[property]=new JObject{["x"]=value.x,["y"]=value.y,["z"]=value.z,["w"]=value.w};}
                    else if(type==UnityEngine.Rendering.ShaderPropertyType.Float||type==UnityEngine.Rendering.ShaderPropertyType.Range)shaderValues[property]=material.GetFloat(property);
                    else if(type==UnityEngine.Rendering.ShaderPropertyType.Int)shaderValues[property]=material.GetInteger(property);
                }
                catch(Exception error){shaderValues[property]=new JObject{["readError"]=error.Message};}
            }
        return o;
    }

    private static void AddColor(Material m, JObject o, string property, string key)
    { try { if (m.HasProperty(property)) o[key] = ColorJson(m.GetColor(property)); } catch { } }
    private static void AddFloat(Material m, JObject o, string property, string key)
    { try { if (m.HasProperty(property)) o[key] = m.GetFloat(property); } catch { } }

    private static JObject LodInfo(LODGroup group, Transform root)
    {
        var o = new JObject
        {
            ["kind"] = "LODGroup",
            ["size"] = group.size,
            ["localReferencePoint"] = Vec(group.localReferencePoint),
            ["fadeMode"] = group.fadeMode.ToString(),
            ["animateCrossFading"] = group.animateCrossFading,
            ["lods"] = new JArray()
        };
        var lods = (JArray)o["lods"];
        LOD[] values = group.GetLODs();
        for (int i = 0; values != null && i < values.Length; i++)
        {
            var renderers = new JArray();
            Renderer[] rs = values[i].renderers;
            for (int j = 0; rs != null && j < rs.Length; j++)
                renderers.Add(rs[j] != null ? Path(rs[j].transform, root) : null);
            lods.Add(new JObject
            {
                ["level"] = i,
                ["screenRelativeTransitionHeight"] = values[i].screenRelativeTransitionHeight,
                ["fadeTransitionWidth"] = values[i].fadeTransitionWidth,
                ["renderers"] = renderers
            });
        }
        return o;
    }

    private static JObject ColliderInfo(Collider c)
    {
        PhysicMaterial pm = c.sharedMaterial;
        var o = new JObject
        {
            ["kind"] = c.GetType().Name,
            ["enabled"] = c.enabled,
            ["isTrigger"] = c.isTrigger,
            ["bounds"] = Bounds(c.bounds),
            ["physicMaterial"] = pm != null ? pm.name : null,
            ["staticFriction"] = pm != null ? (JToken)new JValue(pm.staticFriction) : JValue.CreateNull(),
            ["dynamicFriction"] = pm != null ? (JToken)new JValue(pm.dynamicFriction) : JValue.CreateNull(),
            ["bounciness"] = pm != null ? (JToken)new JValue(pm.bounciness) : JValue.CreateNull(),
            ["frictionCombine"] = pm != null ? pm.frictionCombine.ToString() : null,
            ["bounceCombine"] = pm != null ? pm.bounceCombine.ToString() : null
        };
        BoxCollider box = c as BoxCollider;
        if (box != null) { o["center"] = Vec(box.center); o["size"] = Vec(box.size); }
        CapsuleCollider capsule = c as CapsuleCollider;
        if (capsule != null) { o["center"] = Vec(capsule.center); o["radius"] = capsule.radius; o["height"] = capsule.height; o["direction"] = capsule.direction; }
        SphereCollider sphere = c as SphereCollider;
        if (sphere != null) { o["center"] = Vec(sphere.center); o["radius"] = sphere.radius; }
        MeshCollider mesh = c as MeshCollider;
        if (mesh != null) { o["convex"] = mesh.convex; o["mesh"] = MeshInfo(mesh.sharedMesh); }
        return o;
    }

    private static JObject BodyInfo(Rigidbody b)
    {
        return new JObject
        {
            ["kind"] = "Rigidbody", ["mass"] = b.mass, ["drag"] = b.drag, ["angularDrag"] = b.angularDrag,
            ["isKinematic"] = b.isKinematic, ["useGravity"] = b.useGravity,
            ["constraints"] = b.constraints.ToString(), ["interpolation"] = b.interpolation.ToString(),
            ["collisionDetection"] = b.collisionDetectionMode.ToString(), ["detectCollisions"] = b.detectCollisions
        };
    }

    private static JObject JointInfo(Joint j, Transform root)
    {
        var o = new JObject
        {
            ["kind"] = j.GetType().Name,
            ["connectedBodyPath"] = j.connectedBody != null ? Path(j.connectedBody.transform, root) : null,
            ["anchor"] = Vec(j.anchor), ["connectedAnchor"] = Vec(j.connectedAnchor),
            ["autoConfigureConnectedAnchor"] = j.autoConfigureConnectedAnchor,
            ["massScale"] = j.massScale, ["connectedMassScale"] = j.connectedMassScale,
            ["breakForce"] = j.breakForce, ["breakTorque"] = j.breakTorque,
            ["enableCollision"] = j.enableCollision, ["enablePreprocessing"] = j.enablePreprocessing
        };
        CharacterJoint character = j as CharacterJoint;
        if (character != null)
        {
            o["axis"] = Vec(character.axis);
            o["swingAxis"] = Vec(character.swingAxis);
            o["swing1Limit"] = JointLimit(character.swing1Limit);
            o["swing2Limit"] = JointLimit(character.swing2Limit);
            o["lowTwistLimit"] = JointLimit(character.lowTwistLimit);
            o["highTwistLimit"] = JointLimit(character.highTwistLimit);
        }
        return o;
    }

    private static JObject JointLimit(SoftJointLimit limit)
    { return new JObject { ["limit"] = limit.limit }; }

    private static string TextureFormatName(Texture texture)
    {
        Texture2D texture2D = texture as Texture2D;
        return texture2D != null ? texture2D.format.ToString() : null;
    }

    private static int MipmapCount(Texture texture)
    {
        Texture2D texture2D = texture as Texture2D;
        return texture2D != null ? texture2D.mipmapCount : 0;
    }

    private static bool? StreamingMipmaps(Texture texture)
    {
        Texture2D texture2D = texture as Texture2D;
        return texture2D != null ? (bool?)texture2D.streamingMipmaps : null;
    }

    private static JObject AnimatorInfo(Animator a)
    {
        return new JObject
        {
            ["kind"] = "Animator", ["enabled"] = a.enabled, ["applyRootMotion"] = a.applyRootMotion,
            ["avatar"] = a.avatar != null ? a.avatar.name : null,
            ["avatarValid"] = a.avatar != null && a.avatar.isValid,
            ["avatarHuman"] = a.avatar != null && a.avatar.isHuman,
            ["controller"] = a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : null,
            ["hasTransformHierarchy"] = a.hasTransformHierarchy,
            ["layerCount"] = a.layerCount
        };
    }

    private static JObject MeshFilterInfo(MeshFilter f)
    { return new JObject { ["kind"] = "MeshFilter", ["mesh"] = MeshInfo(f.sharedMesh) }; }

    private static JToken MeshInfo(Mesh mesh)
    {
        if (mesh == null) return JValue.CreateNull();
        return new JObject
        {
            ["name"] = mesh.name, ["vertexCount"] = mesh.vertexCount, ["subMeshCount"] = mesh.subMeshCount,
            ["blendShapeCount"] = mesh.blendShapeCount, ["bounds"] = Bounds(mesh.bounds)
        };
    }

    private static JObject Bounds(Bounds b)
    { return new JObject { ["center"] = Vec(b.center), ["size"] = Vec(b.size), ["extents"] = Vec(b.extents) }; }
    private static JObject Vec(Vector3 v)
    { return new JObject { ["x"] = v.x, ["y"] = v.y, ["z"] = v.z }; }
    private static JObject Vec(Vector2 v)
    { return new JObject { ["x"] = v.x, ["y"] = v.y }; }
    private static JObject Quat(Quaternion q)
    { return new JObject { ["x"] = q.x, ["y"] = q.y, ["z"] = q.z, ["w"] = q.w }; }
    private static JObject ColorJson(Color c)
    { return new JObject { ["r"] = c.r, ["g"] = c.g, ["b"] = c.b, ["a"] = c.a }; }
    private static string SafeTag(GameObject gameObject)
    { try { return gameObject != null ? gameObject.tag : null; } catch { return "<undefined-tag>"; } }

    private static string Path(Transform t, Transform root)
    {
        if (t == null) return null;
        var names = new List<string>(16);
        Transform at = t;
        while (at != null)
        {
            names.Add(at.name);
            if (at == root) break;
            at = at.parent;
        }
        names.Reverse();
        return string.Join("/", names.ToArray());
    }
}
