using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityHumanoidBuilder
    {
        public static Avatar Build(RebirthEntityRigWorkspace workspace,string assetPath,bool keepTPose=false)
        {
            if(workspace==null||workspace.profile==null)throw new ArgumentException("Workspace required.");
            var rotations=new Dictionary<Transform,Quaternion>();
            foreach(var spec in workspace.profile.bones)
            {
                Transform bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,spec.name);
                if(bone!=null)rotations[bone]=bone.localRotation;
            }
            try
            {
                Aim("LeftArm","LeftForeArm",Vector3.left);Aim("LeftForeArm","LeftHand",Vector3.left);
                Aim("RightArm","RightForeArm",Vector3.right);Aim("RightForeArm","RightHand",Vector3.right);
                Aim("LeftUpLeg","LeftLeg",Vector3.down);Aim("LeftLeg","LeftFoot",Vector3.down);
                Aim("RightUpLeg","RightLeg",Vector3.down);Aim("RightLeg","RightFoot",Vector3.down);
                var human=new List<HumanBone>();
                Map(HumanBodyBones.Hips,"Hips");Map(HumanBodyBones.Spine,"Spine");Map(HumanBodyBones.Chest,"Spine2");
                Map(HumanBodyBones.UpperChest,"Spine3");Map(HumanBodyBones.Neck,"Neck");Map(HumanBodyBones.Head,"Head");
                Map(HumanBodyBones.LeftShoulder,"LeftShoulder");Map(HumanBodyBones.LeftUpperArm,"LeftArm");
                Map(HumanBodyBones.LeftLowerArm,"LeftForeArm");Map(HumanBodyBones.LeftHand,"LeftHand");
                Map(HumanBodyBones.RightShoulder,"RightShoulder");Map(HumanBodyBones.RightUpperArm,"RightArm");
                Map(HumanBodyBones.RightLowerArm,"RightForeArm");Map(HumanBodyBones.RightHand,"RightHand");
                Map(HumanBodyBones.LeftUpperLeg,"LeftUpLeg");Map(HumanBodyBones.LeftLowerLeg,"LeftLeg");Map(HumanBodyBones.LeftFoot,"LeftFoot");
                Map(HumanBodyBones.RightUpperLeg,"RightUpLeg");Map(HumanBodyBones.RightLowerLeg,"RightLeg");Map(HumanBodyBones.RightFoot,"RightFoot");
                Map(HumanBodyBones.LeftToes,"LeftToeBase");Map(HumanBodyBones.RightToes,"RightToeBase");Map(HumanBodyBones.Jaw,"Jaw");
                foreach(string side in new[]{"Left","Right"})foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Little"})
                    for(int segment=1;segment<=3;segment++)
                    {
                        string joint=segment==1?"Proximal":segment==2?"Intermediate":"Distal";
                        var type=(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+finger+joint);
                        Map(type,side+"Hand"+(finger=="Little"?"Pinky":finger)+segment);
                    }
                var skeleton=new List<SkeletonBone>();
                AddSkeleton(workspace.transform);
                foreach(var spec in workspace.profile.bones)
                {
                    Transform bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,spec.name);
                    if(bone!=null)AddSkeleton(bone);
                }
                var description=new HumanDescription{human=human.ToArray(),skeleton=skeleton.ToArray(),
                    armStretch=.05f,legStretch=.05f,upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,
                    feetSpacing=0f,hasTranslationDoF=false};
                Avatar avatar=AvatarBuilder.BuildHumanAvatar(workspace.gameObject,description);
                if(avatar==null||!avatar.isValid||!avatar.isHuman)throw new InvalidOperationException("Unity rejected the generated humanoid avatar.");
                avatar.name=workspace.profile.entityClass+"Avatar";
                if(!string.IsNullOrEmpty(assetPath))AssetDatabase.CreateAsset(avatar,assetPath);
                Animator animator=workspace.GetComponent<Animator>();
                if(animator==null)animator=workspace.gameObject.AddComponent<Animator>();
                animator.avatar=avatar;animator.applyRootMotion=true;animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
                EditorUtility.SetDirty(workspace);if(!string.IsNullOrEmpty(assetPath))AssetDatabase.SaveAssets();
                Debug.Log("REBIRTH HUMANOID AVATAR: valid="+avatar.isValid+" human="+avatar.isHuman+" path="+assetPath);
                if(keepTPose)
                {
                    foreach(var renderer in workspace.generatedSkeleton.GetComponentsInChildren<SkinnedMeshRenderer>(true))RebindCurrentPose(renderer);
                    rotations.Clear();
                }
                return avatar;

                void Map(HumanBodyBones type,string name)
                {
                    Transform bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,name);
                    if(bone!=null)human.Add(new HumanBone{humanName=HumanTrait.BoneName[(int)type],boneName=bone.name,limit=new HumanLimit{useDefaultValues=true}});
                }
                void AddSkeleton(Transform bone)
                {skeleton.Add(new SkeletonBone{name=bone.name,position=bone.localPosition,rotation=bone.localRotation,scale=bone.localScale});}
            }
            finally{foreach(var pair in rotations)if(pair.Key!=null)pair.Key.localRotation=pair.Value;}

            void Aim(string startName,string endName,Vector3 direction)
            {
                Transform start=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,startName);
                Transform end=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,endName);
                if(start!=null&&end!=null&&(end.position-start.position).sqrMagnitude>.000001f)
                    start.rotation=Quaternion.FromToRotation(end.position-start.position,direction)*start.rotation;
            }
        }

        public static void RebindCurrentPose(SkinnedMeshRenderer renderer)
        {
            Mesh mesh=renderer.sharedMesh;Vector3[] vertices=mesh.vertices,normals=mesh.normals;BoneWeight[] weights=mesh.boneWeights;
            Matrix4x4[] old=mesh.bindposes,matrices=new Matrix4x4[renderer.bones.Length],newBind=new Matrix4x4[renderer.bones.Length];
            for(int i=0;i<matrices.Length;i++)
            {matrices[i]=renderer.transform.worldToLocalMatrix*renderer.bones[i].localToWorldMatrix*old[i];newBind[i]=renderer.bones[i].worldToLocalMatrix*renderer.transform.localToWorldMatrix;}
            for(int i=0;i<vertices.Length;i++)
            {
                BoneWeight w=weights[i];Vector3 p=vertices[i],n=normals.Length==vertices.Length?normals[i]:Vector3.up;
                vertices[i]=matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
                if(normals.Length==vertices.Length)normals[i]=(matrices[w.boneIndex0].inverse.transpose.MultiplyVector(n)*w.weight0+matrices[w.boneIndex1].inverse.transpose.MultiplyVector(n)*w.weight1+matrices[w.boneIndex2].inverse.transpose.MultiplyVector(n)*w.weight2+matrices[w.boneIndex3].inverse.transpose.MultiplyVector(n)*w.weight3).normalized;
            }
            mesh.vertices=vertices;if(normals.Length==vertices.Length)mesh.normals=normals;mesh.bindposes=newBind;mesh.RecalculateBounds();mesh.RecalculateTangents();renderer.localBounds=mesh.bounds;EditorUtility.SetDirty(mesh);
        }
    }
}
