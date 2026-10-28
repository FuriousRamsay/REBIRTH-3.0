using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

// Native 3D item renders: a vinyl roll bearing the actual wrap's hood artwork.
// One common physical silhouette makes 200 inventory icons consistent; artwork
// distinguishes each item. Source artwork is sampled, never redrawn or recolored.
public static class RebirthWrapCollectionIcons
{
    static string Hash(string path){using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
    static string Arg(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);if(i<0||i+1>=a.Length)throw new Exception(key);return a[i+1];}
    static Mesh Tube()
    {
        const int n=64;var v=new Vector3[(n+1)*4];var uv=new Vector2[v.Length];var triangles=new int[n*24];int t=0;
        for(int i=0;i<=n;i++)
        {
            float angle=2*Mathf.PI*i/n;
            for(int ring=0;ring<4;ring++)
            {
                float radius=ring<2?.17f:.075f;float x=(ring%2==0?-.73f:.73f);
                v[4*i+ring]=new Vector3(x,.67f+Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius);
                uv[4*i+ring]=new Vector2(ring%2,(float)i/n);
            }
        }
        for(int i=0;i<n;i++)foreach(var pair in new[]{new[]{0,1},new[]{3,2},new[]{2,0},new[]{1,3}})
        {
            int a=4*i+pair[0],b=4*i+pair[1],c=a+4,d=b+4;
            foreach(int q in new[]{a,c,b,b,c,d})triangles[t++]=q;
        }
        var mesh=new Mesh{name="Collection vinyl roll"};mesh.vertices=v;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    public static void Run()
    {
        string output=Arg("-wrapIconOutput");Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var bundle=AssetBundle.LoadFromFile(Arg("-wrapIconBundle"));if(!bundle)throw new Exception("Icon bundle unavailable.");
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.72f,.72f,.72f);
        var light=new GameObject("Icon softbox").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(35,-30,0);
        var camera=new GameObject("Icon camera").AddComponent<Camera>();camera.transform.position=new Vector3(-1.5f,1.1f,-4);camera.transform.LookAt(new Vector3(0,.03f,0));camera.orthographic=true;camera.orthographicSize=1.18f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
        var target=new RenderTexture(256,256,24){antiAliasing=4};camera.targetTexture=target;
        var provenance=new List<string>{"id\tartSHA256\ticonSHA256"};
        foreach(string path in bundle.GetAllAssetNames().Where(p=>p.EndsWith(".png")))
        {
            string id=Path.GetFileNameWithoutExtension(path);
            // Asset bundle names are lowercase; restore the catalogue's stable
            // case via the generated item ID list passed by the staging script.
            string item=Arg("-wrapIconIds").Split(',').FirstOrDefault(s=>string.Equals(s,id,StringComparison.OrdinalIgnoreCase));
            if(item==null)throw new Exception("Unknown icon ID "+id);
            var atlas=bundle.LoadAsset<Texture2D>(path);var root=new GameObject("Vinyl item");
            var face=GameObject.CreatePrimitive(PrimitiveType.Quad);face.transform.SetParent(root.transform);face.transform.localPosition=new Vector3(0,-.04f,-.17f);face.transform.localScale=new Vector3(1.46f,1.46f,1);
            var material=new Material(Shader.Find("Standard"));material.mainTexture=atlas;material.mainTextureScale=new Vector2(.5f,.5f);material.mainTextureOffset=Vector2.zero;material.SetFloat("_Glossiness",.5f);material.SetFloat("_Metallic",.15f);face.GetComponent<Renderer>().sharedMaterial=material;
            var roll=new GameObject("Rolled edge");roll.transform.SetParent(root.transform);var mesh=Tube();roll.AddComponent<MeshFilter>().sharedMesh=mesh;
            var rolled=new Material(material);rolled.mainTextureScale=new Vector2(1,.5f);rolled.mainTextureOffset=new Vector2(0,.5f);roll.AddComponent<MeshRenderer>().sharedMaterial=rolled;
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(256,256,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,256,256),0,0);texture.Apply();
            string destination=Path.Combine(output,"modVehicleWrap"+item+".png");
            File.WriteAllBytes(destination,texture.EncodeToPNG());
            string art=Directory.GetFiles("Assets/REBIRTHCamperWrapCollection/Artwork","*.png").Single(p=>string.Equals(Path.GetFileNameWithoutExtension(p),item,StringComparison.OrdinalIgnoreCase));
            provenance.Add(item+"\t"+Hash(art)+"\t"+Hash(destination));
            RenderTexture.active=previous;Object.DestroyImmediate(texture);Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);Object.DestroyImmediate(material);Object.DestroyImmediate(rolled);
        }
        camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);bundle.Unload(true);
        File.WriteAllLines(Path.Combine(Arg("-wrapOutput"),"icon-provenance.tsv"),provenance);
        Debug.Log("WRAP_COLLECTION_ICONS_RENDERED");
    }
}
