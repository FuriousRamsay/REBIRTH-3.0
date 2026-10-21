using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Entity Studio bundles own their humanoid Avatar. An opt-in class property names
/// a native prefab whose animation controller is reused from the installed game.
/// This keeps native zombie states and root motion available without redistributing
/// the game's animation assets in a custom bundle.
/// </summary>
[Preserve]
public sealed class RebirthEntityStudioModApi : IModApi
{
    private static bool initialized;
    public void InitMod(Mod modInstance)
    {
        if(initialized)return;
        // A separately compiled test adapter and a later cumulative build may
        // coexist. The shared Harmony owner must still install only once.
        if(Harmony.HasAnyPatches("rebirth.entity-studio.runtime")){initialized=true;return;}
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.entity-studio.runtime"),typeof(RebirthEntityStudioAnimatorPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.entity-studio.runtime"),typeof(RebirthEntityStudioCompoundBodyPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.entity-studio.runtime"),typeof(RebirthEntityStudioBridge));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        initialized=true;
    }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {RebirthEntityStudioMaterials.Clear();RebirthEntityStudioAnimatorPatch.Clear();RebirthEntityStudioTags.Clear();}
}

// Native collider wrappers normally own a Rigidbody on their own Transform.
// Authored terminal shapes can instead belong to an existing forearm/calf body,
// while still taking every collision/ragdoll mode change through the native loop.
[HarmonyPatch(typeof(PhysicsBodyColliderBase),MethodType.Constructor,new[]{typeof(Transform),typeof(PhysicsBodyColliderConfiguration)})]
public static class RebirthEntityStudioCompoundBodyPatch
{
    public const string ShapePrefix="EntityStudio_Compound_";
    private static readonly System.Reflection.FieldInfo Body=AccessTools.Field(typeof(PhysicsBodyColliderBase),"rigidBody");
    private static readonly System.Reflection.FieldInfo TransformField=AccessTools.Field(typeof(PhysicsBodyColliderBase),"transform");
    private static readonly System.Reflection.FieldInfo ConfigField=AccessTools.Field(typeof(PhysicsBodyColliderBase),"config");
    private static readonly System.Reflection.MethodInfo EnableBody=AccessTools.Method(typeof(PhysicsBodyColliderBase),"enableRigidBody");

    public static bool Prefix(PhysicsBodyColliderBase __instance,Transform _transform,PhysicsBodyColliderConfiguration _config)
    {
        if(_transform==null||!_transform.name.StartsWith(ShapePrefix,StringComparison.Ordinal))return true;
        Entity entity=_transform.GetComponentInParent<Entity>();
        if(entity==null||entity.EntityClass==null||string.IsNullOrWhiteSpace(entity.EntityClass.Properties.GetString(RebirthEntityStudioAnimatorPatch.SourceProperty)))return true;
        Rigidbody parent=_transform.parent!=null?_transform.parent.GetComponentInParent<Rigidbody>():null;
        if(parent==null||Body==null||TransformField==null||ConfigField==null||EnableBody==null)throw new InvalidOperationException("Entity Studio compound native body contract could not resolve.");
        TransformField.SetValue(__instance,_transform);ConfigField.SetValue(__instance,_config);Body.SetValue(__instance,parent);
        _transform.tag=_config.Tag;_transform.gameObject.layer=_config.CollisionLayer;
        EnableBody.Invoke(__instance,new object[]{false});
        // Do not create a temporary body: native InitRigidBodies must see eleven
        // bodies immediately to apply its native mass/collision callback setup.
        return false;
    }
}

[HarmonyPatch(typeof(EModelBase),"createModel")]
public static class RebirthEntityStudioAnimatorPatch
{
    public const string SourceProperty="RebirthEntityStudioAnimatorSource";
    private static readonly Dictionary<string,RuntimeAnimatorController> Controllers=new Dictionary<string,RuntimeAnimatorController>(StringComparer.Ordinal);
    private static readonly HashSet<string> ReportedFailures=new HashSet<string>(StringComparer.Ordinal);

    public static void Postfix(EModelBase __instance,EntityClass _ec)
    {
        string source=_ec.Properties.GetString(SourceProperty);
        if(string.IsNullOrWhiteSpace(source))return;
        try
        {
            Transform parent=__instance.GetModelTransformParent();
            if(parent==null)throw new InvalidOperationException("The entity model parent is missing.");
            Animator animator=parent.GetComponentInChildren<Animator>(true);
            if(animator==null||animator.avatar==null||!animator.avatar.isValid||!animator.avatar.isHuman)
                throw new InvalidOperationException("The Entity Studio prefab needs a valid humanoid Avatar on its Animator.");
            RebirthEntityStudioTags.Apply(animator.transform,_ec);
            GameObject reference=DataLoader.LoadAsset<GameObject>(source);
            if(reference==null)throw new InvalidOperationException("Native entity reference is unavailable: "+source);
            if(!Controllers.TryGetValue(source,out RuntimeAnimatorController controller)||controller==null)
            {
                Animator native=reference!=null?reference.GetComponentInChildren<Animator>(true):null;
                controller=native!=null?native.runtimeAnimatorController:null;
                if(controller==null)throw new InvalidOperationException("No native animation controller was found in "+source);
                Controllers[source]=controller;
            }
            animator.runtimeAnimatorController=controller;
            animator.applyRootMotion=true;
            // Native prefabs carry this receiver for animation clip events,
            // including PlayStepSound. AvatarHumanController itself installs and
            // initializes AvatarRootMotion during its native model setup.
            var events=animator.GetComponent<AnimationEventBridge>();
            if(events==null)events=animator.gameObject.AddComponent<AnimationEventBridge>();
            events.RootTransform=__instance.transform;
            RebirthEntityStudioMaterials.Bind(parent,_ec);
            InstallNativeBlocker(animator.transform,reference);
            InstallNativeHairMotion(animator.transform,reference,_ec);
        }
        catch(Exception exception)
        {
            if(ReportedFailures.Add(source+"|"+exception.Message))
                Log.Error("[REBIRTH Entity Studio] Native model binding failed: "+exception.Message);
        }
    }
    private static void InstallNativeHairMotion(Transform model,GameObject reference,EntityClass entityClass)
    {
        if(!string.Equals(entityClass.Properties.GetString("RebirthEntityStudioHairMotion"),"true",StringComparison.OrdinalIgnoreCase))return;
        // Reuse the installed game's own solver and the native Nurse's authored settings.
        // Start executes next frame, after all fields have been copied. No custom spring solver.
        foreach(var source in reference.GetComponentsInChildren<Assets.DuckType.Jiggle.Jiggle>(true))
        {
            if(!source.name.StartsWith("Hair_",StringComparison.Ordinal))continue;
            var names=new List<string>();Transform node=source.transform;
            while(node!=null&&node!=reference.transform){names.Add(node.name);node=node.parent;}
            names.Reverse();Transform target=model.Find(string.Join("/",names.ToArray()));
            if(target==null)throw new InvalidOperationException("Native hair bone missing: "+source.name);
            var jiggle=target.GetComponent<Assets.DuckType.Jiggle.Jiggle>();
            if(jiggle!=null)continue;
            jiggle=target.gameObject.AddComponent<Assets.DuckType.Jiggle.Jiggle>();
            jiggle.UpdateWithPhysics=source.UpdateWithPhysics;jiggle.UseCenterOfMass=source.UseCenterOfMass;
            jiggle.CenterOfMass=source.CenterOfMass;jiggle.CenterOfMassInertia=source.CenterOfMassInertia;
            jiggle.AddWind=source.AddWind;jiggle.WindDirection=source.WindDirection;jiggle.WindStrength=source.WindStrength;
            jiggle.AddNoise=source.AddNoise;jiggle.NoiseStrength=source.NoiseStrength;jiggle.NoiseScale=source.NoiseScale;jiggle.NoiseSpeed=source.NoiseSpeed;
            jiggle.RotationInertia=source.RotationInertia;jiggle.Gravity=source.Gravity;
            jiggle.SpringStrength=source.SpringStrength;jiggle.Dampening=source.Dampening;
            jiggle.BlendToOriginalRotation=source.BlendToOriginalRotation;jiggle.Hinge=source.Hinge;jiggle.HingeAngle=source.HingeAngle;
            jiggle.UseAngleLimit=true;jiggle.AngleLimit=Mathf.Min(source.AngleLimit,source.name=="Hair_Back_Left"?2f:1.25f);
            jiggle.UseSoftLimit=source.UseSoftLimit;jiggle.SoftLimitInfluence=source.SoftLimitInfluence;jiggle.SoftLimitStrength=source.SoftLimitStrength;
            jiggle.ShowViewportGizmos=false;jiggle.GizmoScale=source.GizmoScale;
        }
    }
    private static void InstallNativeBlocker(Transform model,GameObject reference)
    {
        foreach(var existing in model.GetComponentsInChildren<CapsuleCollider>(true))if(existing.CompareTag("LargeEntityBlocker"))return;
        CapsuleCollider template=null;
        foreach(var candidate in reference.GetComponentsInChildren<CapsuleCollider>(true))if(candidate.CompareTag("LargeEntityBlocker"))
        {if(template!=null)throw new InvalidOperationException("Native prefab has multiple large entity blockers.");template=candidate;}
        Transform source=template!=null?template.transform:null;
        if(template==null||source.parent!=reference.transform||template.direction!=1||template.height<=0||template.radius<=0)
            throw new InvalidOperationException("Native prefab lacks a root-level upright LargeEntityBlocker capsule.");
        // Entity.PhysicsInit consumes this separate blocker, installs its native
        // Rigidbody and derives controller height from the blocker's upper extent.
        // It is not another body in the eleven-body ragdoll chain.
        var go=new GameObject(source.name);go.transform.SetParent(model,false);
        go.transform.localPosition=source.localPosition;go.transform.localRotation=source.localRotation;go.transform.localScale=source.localScale;
        go.tag="LargeEntityBlocker";go.layer=source.gameObject.layer;
        var capsule=go.AddComponent<CapsuleCollider>();capsule.direction=template.direction;capsule.center=template.center;capsule.height=template.height;capsule.radius=template.radius;
        capsule.isTrigger=template.isTrigger;capsule.enabled=template.enabled;capsule.sharedMaterial=template.sharedMaterial;capsule.contactOffset=template.contactOffset;
    }
    public static void Clear(){Controllers.Clear();ReportedFailures.Clear();}
}

public static class RebirthEntityStudioTags
{
    [Serializable] private sealed class Entry {public string path="",tag="";}
    [Serializable] private sealed class Map {public int version=0;public Entry[] entries=Array.Empty<Entry>();}
    private static readonly Dictionary<string,Map> Maps=new Dictionary<string,Map>(StringComparer.Ordinal);
    public static void Apply(Transform root,EntityClass entityClass)
    {
        string reference=entityClass.Properties.GetString("RebirthEntityStudioTagMap");
        if(string.IsNullOrWhiteSpace(reference))throw new InvalidOperationException("Entity Studio requires an authored string tag map for target-game tag indices.");
        if(!Maps.TryGetValue(reference,out Map map))
        {
            TextAsset source=DataLoader.LoadAsset<TextAsset>(reference);
            if(source==null)throw new InvalidOperationException("Entity Studio tag map could not be loaded: "+reference);
            map=Newtonsoft.Json.JsonConvert.DeserializeObject<Map>(source.text);
            if(map==null||map.version!=1||map.entries==null||map.entries.Length==0)throw new InvalidOperationException("Invalid Entity Studio tag map.");
            Maps.Add(reference,map);
        }
        var targets=new List<Transform>();var paths=new HashSet<string>(StringComparer.Ordinal);
        foreach(Entry entry in map.entries)
        {
            if(entry==null||entry.path==null||string.IsNullOrEmpty(entry.tag)||!paths.Add(entry.path))throw new InvalidOperationException("Invalid or duplicate Entity Studio tag entry.");
            Transform node=string.IsNullOrEmpty(entry.path)?root:root.Find(entry.path);
            if(node==null)throw new InvalidOperationException("Entity Studio tag path missing: "+entry.path);
            node.CompareTag(entry.tag); // Validate target-game tag exists before changing any node.
            targets.Add(node);
        }
        for(int i=0;i<targets.Count;i++)targets[i].tag=map.entries[i].tag;
    }
    public static void Clear(){Maps.Clear();}
}



