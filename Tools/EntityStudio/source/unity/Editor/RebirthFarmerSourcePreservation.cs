using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthFarmerSourcePreservation
    {
        public static void Run()
        {
            try
            {
                var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
                var workspace=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(value=>value!=null);
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(workspace.profile.sourceModelPath);
                string[] original=source.GetComponentsInChildren<MeshFilter>(true).Select(value=>Hash(value.sharedMesh)).OrderBy(value=>value).ToArray();
                string[] rigged=workspace.sourceSkinnedMeshes.Select(value=>Hash(value.sharedMesh)).OrderBy(value=>value).ToArray();
                if(!original.SequenceEqual(rigged))throw new InvalidDataException("Rigged LOD0 geometry differs from the imported original.");
                int colliders=workspace.generatedSkeleton.GetComponentsInChildren<Collider>(true).Length;
                int bodies=workspace.generatedSkeleton.GetComponentsInChildren<Rigidbody>(true).Length;
                int joints=workspace.generatedSkeleton.GetComponentsInChildren<CharacterJoint>(true).Length;
                if(colliders!=11||bodies!=11||joints!=10)throw new InvalidDataException("Native physics count mismatch.");
                string report="Original mesh preserved: vertices, normals, tangents, colors, all UV channels and submesh triangle indices match exactly.\nMesh hashes: "+string.Join(",",original)+"\nColliders="+colliders+" bodies="+bodies+" joints="+joints;
                string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/RebirthFarmerReview/SourcePreservation.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,report);Debug.Log(report);
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }

        public static string Hash(Mesh mesh)
        {
            using(var stream=new MemoryStream())
            using(var writer=new BinaryWriter(stream))
            {
                writer.Write(mesh.vertexCount);
                foreach(var v in mesh.vertices){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
                foreach(var v in mesh.normals){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
                foreach(var v in mesh.tangents){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);writer.Write(v.w);}
                foreach(var c in mesh.colors32){writer.Write(c.r);writer.Write(c.g);writer.Write(c.b);writer.Write(c.a);}
                for(int channel=0;channel<8;channel++)
                {
                    var uv=new List<Vector4>();mesh.GetUVs(channel,uv);writer.Write(uv.Count);
                    foreach(var v in uv){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);writer.Write(v.w);}
                }
                writer.Write(mesh.subMeshCount);
                for(int sub=0;sub<mesh.subMeshCount;sub++){var indices=mesh.GetTriangles(sub);writer.Write(indices.Length);foreach(int index in indices)writer.Write(index);}
                writer.Flush();using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
    }
}
