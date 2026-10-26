using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityCurrentColliderRefit
    {
        public static void CommitSavedFarmer()
        {
            const string path="Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity";
            var scene=EditorSceneManager.OpenScene(path);
            var workspace=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/RebirthFarmerReview");
            string csv=Path.Combine(folder,"RebirthFarmer_PoseUnionFit_DamageRegions_Surfaces_Silhouette.csv");
            var shapes=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true).ToDictionary(shape=>shape.name);
            var rows=File.ReadAllLines(csv).Skip(1).Select(row=>row.Split(',')).ToArray();
            if(rows.Length!=shapes.Count)throw new InvalidDataException("Refit capsule count differs from current scene.");
            if(rows.Select(row=>row[0]).Distinct().Count()!=rows.Length)throw new InvalidDataException("Duplicate refit capsule row.");
            foreach(var row in rows)
            {
                if(row.Length!=7||!shapes.TryGetValue(row[0],out var shape))throw new InvalidDataException("Unknown refit shape.");
                if(Mathf.Abs(shape.radius-Number(row[1]))>1e-6f)throw new InvalidDataException("Refit baseline radius changed: "+shape.name);
            }
            foreach(var row in rows)
            {var shape=shapes[row[0]];shape.radius=Number(row[2]);shape.height=Number(row[3]);shape.center=new Vector3(Number(row[4]),Number(row[5]),Number(row[6]));}
            int samples=RebirthEntityColliderCandidate.Verify(workspace);
            RebirthEntityColliderCandidate.CommitFarmer(workspace);
            var reloaded=EditorSceneManager.OpenScene(path).GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
            int replayed=RebirthEntityColliderCandidate.Verify(reloaded);
            RebirthFarmerReviewCapture.CaptureColliderCandidate(reloaded,Path.Combine(folder,"RebirthFarmer_CurrentSkinRefit_Saved"));
            Debug.Log("CURRENT SKIN COLLIDER REFIT SAVED AND REOPENED: before="+samples+" after="+replayed+" final fit review remains false.");
        }
        static float Number(string text)
        {float value=float.Parse(text,CultureInfo.InvariantCulture);if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("Nonfinite refit value.");return value;}
    }
}
