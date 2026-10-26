using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;
using Rebirth.Vehicles.Authoring;

// Compile with the exact runtime SeatPoseVehicleTargets.cs. Exercises the reader
// against the shipped bundles without loading the game or changing profiles.
public static class RebirthVehicleSeatTargetChecks
{
    public static void Run()
    {
        const string output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/SeatTargets_20261017";
        var proof=new List<string>();
        foreach(string name in new[]{"rebirthCamper80s","rebirthBuggy","rebirthBike"})
        {
            string filename=name=="rebirthCamper80s"?"rebirth_camper80s.unity3d":name=="rebirthBuggy"?"rebirth_buggy.unity3d":"rebirth_bike.unity3d";
            var bundle=AssetBundle.LoadFromFile(output+"/Bundles/"+filename);
            if(!bundle)throw new Exception("Bundle load failed");
            GameObject root=null;
            try
            {
                root=Object.Instantiate(bundle.LoadAsset<GameObject>(name));var model=root.transform.Find("GameObject");
                int seats=name=="rebirthCamper80s"?6:name=="rebirthBuggy"?2:1;
                for(int seat=0;seat<seats;seat++)
                {
                    var neutral=SeatPoseVehicleTargets.Read(model,seat);
                    root.transform.SetPositionAndRotation(new Vector3(123,7,-52),Quaternion.Euler(9,73,-4));
                    var shifted=SeatPoseVehicleTargets.Read(model,seat);
                    foreach(var pair in neutral)if(Vector3.Distance(pair.Value,shifted[pair.Key])>.0001f)throw new Exception("World-frame leakage "+name+"/"+seat);
                    root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                }
                var before=SeatPoseVehicleTargets.Read(model,0);
                var steering=model.Find(name=="rebirthCamper80s"?"M/SteeringWheel":name=="rebirthBuggy"?"M/Steering":"M/Forks");
                steering.localRotation*=Quaternion.Euler(0,25,0);
                var after=SeatPoseVehicleTargets.Read(model,0);
                if(Vector3.Distance(before["IKHandL"],after["IKHandL"])<.001f)throw new Exception("Hand marker did not follow steering");
                foreach(string foot in new[]{"IKFootL","IKFootR"})if(Vector3.Distance(before[foot],after[foot])>.0001f)throw new Exception("Foot marker followed steering");
                // Missing references must fail clearly instead of returning zero.
                bool rejected=false;try{SeatPoseVehicleTargets.Read(model,99);}catch(InvalidOperationException){rejected=true;}
                if(!rejected)throw new Exception("Missing marker was accepted");
                proof.Add("PASS "+name+" all "+seats+" seats read; model-space invariant; hands follow steering; feet fixed; missing target rejected. Offline reader verification, not an in-game UI test.");
            }
            finally{if(root)Object.DestroyImmediate(root);bundle.Unload(true);}
        }
        File.WriteAllLines(output+"/reader-verified.txt",proof);Debug.Log(string.Join("\n",proof));
    }
}
