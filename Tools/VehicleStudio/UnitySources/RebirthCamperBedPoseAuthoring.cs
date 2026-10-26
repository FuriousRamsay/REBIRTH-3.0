using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Native players use the generic BaseRig skeleton, not a Humanoid avatar.
// The clip owns skeletal curves only; vehicle attachment and controllers stay native.
public static class RebirthCamperBedPoseAuthoring
{
    public const string ClipName = "rebirthCamperBedLying";
    public const string HolderPath = "M/BedPoseAsset";
    const string RigAsset = "Assets/AssetBundles/Player/Common/BaseRigs/baseRigPrefab.prefab";
    public static AnimationClip Apply(GameObject root, string folder)
    {
        string game = "C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die";
        var args = Environment.GetCommandLineArgs();
        int argument = Array.IndexOf(args, "-rebirthAvatarGameRoot");
        if (argument >= 0 && argument + 1 < args.Length) game = args[argument + 1];
        string bundlePath = Path.Combine(game, "Data/Addressables/Standalone/player_assets_entities/player/common.bundle");
        var bundle = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(b => b.GetAllAssetNames().Any(n => string.Equals(n, RigAsset, StringComparison.OrdinalIgnoreCase)));
        bool owned = bundle == null;
        if (owned) bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null) throw new Exception("Native generic bed authoring rig bundle unavailable: " + bundlePath);
        GameObject rig = null;
        try
        {
            var source = bundle.LoadAsset<GameObject>(RigAsset);
            if (source == null) throw new Exception("Native BaseRig prefab unavailable.");
            rig = UnityEngine.Object.Instantiate(source); rig.SetActive(false);
            var nativeAnimator = rig.GetComponentInChildren<Animator>(true);
            if (nativeAnimator == null || nativeAnimator.avatar == null || !nativeAnimator.avatar.isValid || nativeAnimator.avatar.isHuman)
                throw new Exception("Expected the installed valid generic BaseRig avatar.");
            return ApplyFromNativeRig(root, folder, nativeAnimator.transform);
        }
        finally
        {
            if (rig != null) UnityEngine.Object.DestroyImmediate(rig);
            if (owned) bundle.Unload(false); // Source asset dependencies may still be in use by another authoring pass.
        }
    }
    public static AnimationClip ApplyFromNativeRig(GameObject root, string folder, Transform rig)
    {
        if (root.transform.Find(HolderPath) != null) throw new Exception("Bed pose holder already exists.");
        var transforms = rig.GetComponentsInChildren<Transform>(true);
        Func<string, Transform> bone = name =>
        {
            var matches = transforms.Where(t => t.name == name).ToArray();
            if (matches.Length != 1) throw new Exception("Native bed bone missing/ambiguous: " + name);
            return matches[0];
        };
        var hips = bone("Hips");
        // Move the actual pelvis to the native bed anchor, then rotate its neutral
        // standing skeleton into a supine frame. The knees and arms are authored
        // independently below; no vehicle sitting animation is sampled or rotated.
        hips.position = rig.position;
        // Turn the native forward axis upward as well as laying the spine along
        // the mattress. A Z-only turn leaves the hip width vertical (sideways).
        hips.rotation = rig.rotation * (Quaternion.Euler(0, 0, 90) * Quaternion.Euler(0, 90, 0)) * Quaternion.Inverse(rig.rotation) * hips.rotation;
        foreach(string name in new[]{"Spine","Spine1","Spine2","Spine3"}) {var joint=bone(name);joint.rotation=Quaternion.AngleAxis(-1.5f,rig.forward)*joint.rotation;}
        foreach (string side in new[] { "Left", "Right" })
        {
            Aim(bone(side + "UpLeg"), bone(side + "Leg"), rig.TransformDirection(new Vector3(.48f, .877f, 0)));
            Aim(bone(side + "Leg"), bone(side + "Foot"), rig.TransformDirection(new Vector3(.48f, -.877f, 0)));
            Aim(bone(side + "Arm"), bone(side + "ForeArm"), rig.TransformDirection(new Vector3(.99f, .08f, side == "Left" ? -.10f : .10f)));
            Aim(bone(side + "ForeArm"), bone(side + "Hand"), rig.TransformDirection(new Vector3(.98f, .18f, 0)));
        }
        var clip = new AnimationClip { name = ClipName, frameRate = 30, legacy = false };
        foreach (var transform in transforms.Where(t => t != rig && t.IsChildOf(hips)))
        {
            string path = AnimationUtility.CalculateTransformPath(transform, rig);
            Vector3 p = transform.localPosition; Quaternion q = transform.localRotation;
            Curve(clip, path, "m_LocalPosition.x", p.x); Curve(clip, path, "m_LocalPosition.y", p.y); Curve(clip, path, "m_LocalPosition.z", p.z);
            Curve(clip, path, "m_LocalRotation.x", q.x); Curve(clip, path, "m_LocalRotation.y", q.y); Curve(clip, path, "m_LocalRotation.z", q.z); Curve(clip, path, "m_LocalRotation.w", q.w);
        }
        clip.EnsureQuaternionContinuity();
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        string clipPath = folder + "/" + ClipName + ".anim";
        AssetDatabase.CreateAsset(clip, clipPath); AssetDatabase.SaveAssets();
        var authored = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (!Valid(authored)) throw new Exception("Native generic lying clip skeletal curves missing/invalid.");
        var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/BedPoseAsset.controller"); controller.AddMotion(authored);
        var holder = new GameObject("BedPoseAsset"); holder.transform.SetParent(root.transform.Find("M"), false);
        var layout = new GameObject("NativeRigLayout"); layout.transform.SetParent(holder.transform, false);
        foreach (var target in transforms.Where(t => t != rig && t.IsChildOf(hips)))
        {
            var parent = layout.transform;
            foreach (var name in AnimationUtility.CalculateTransformPath(target, rig).Split('/') )
            {
                var child = parent.Find(name);
                if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
                parent = child;
            }
        }
        var animator = holder.AddComponent<Animator>(); animator.runtimeAnimatorController = controller; animator.enabled = false;
        return authored;
    }
    public static bool Valid(AnimationClip clip)
    {
        if (clip == null || clip.humanMotion || clip.legacy) return false;
        var curves = AnimationUtility.GetCurveBindings(clip);
        return curves.Length > 100 && curves.All(c => c.type == typeof(Transform) && !string.IsNullOrEmpty(c.path))
            && new[] { "Hips", "LeftUpLeg", "LeftLeg", "RightUpLeg", "RightLeg", "LeftArm", "RightArm" }
                .All(n => curves.Any(c => (c.path == n || c.path.EndsWith("/" + n)) && c.propertyName == "m_LocalRotation.w"));
    }
    static void Aim(Transform joint, Transform child, Vector3 direction)
    {
        Vector3 current = child.position - joint.position;
        if (current.sqrMagnitude < .000001f) throw new Exception("Native skeleton bone length is zero: " + joint.name);
        joint.rotation = Quaternion.FromToRotation(current, direction.normalized) * joint.rotation;
    }
    static void Curve(AnimationClip clip, string path, string property, float value)
    {
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), AnimationCurve.Constant(0, 1, value));
    }
}


