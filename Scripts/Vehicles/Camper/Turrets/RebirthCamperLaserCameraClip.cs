using UnityEngine;

// Only attached to the camper CCTV camera. Other cameras render the entire native beam.
internal sealed class RebirthCamperLaserCameraClip:MonoBehaviour
{
    private void OnPreCull()=>RebirthCamperTurretLaser.BeginCamera(GetComponent<Camera>());
    private void OnPostRender()=>RebirthCamperTurretLaser.EndCamera();
    private void OnDisable()=>RebirthCamperTurretLaser.EndCamera();
    private void OnDestroy()=>RebirthCamperTurretLaser.EndCamera();
}
