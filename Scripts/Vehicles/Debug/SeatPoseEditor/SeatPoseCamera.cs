using System;
using UnityEngine;

namespace Rebirth.Vehicles.Authoring
{
    internal sealed class SeatPoseCamera : IDisposable
    {
        readonly Camera original, preview;
        readonly bool wasEnabled;
        readonly GameObject owner;
        Transform focus, model;
        Vector3 pan;
        float yaw = 90, pitch = 10, distance = 2;
        bool disposed;
        internal SeatPoseCamera(Camera native, Transform vehicleModel, Transform seatFocus)
        {
            original = native ? native : throw new ArgumentNullException(nameof(native));
            model = vehicleModel ? vehicleModel : throw new ArgumentNullException(nameof(vehicleModel));
            focus = seatFocus ? seatFocus : throw new ArgumentNullException(nameof(seatFocus));
            wasEnabled = native.enabled;
            owner = new GameObject("REBIRTH_SeatAuthoring_Camera");
            preview = owner.AddComponent<Camera>(); preview.CopyFrom(native);
            preview.stereoTargetEye = StereoTargetEyeMask.None;
            preview.nearClipPlane = .02f; preview.enabled = true; native.enabled = false;
            Tick();
        }
        internal void Focus(Transform seatFocus) { focus = seatFocus; pan = Vector3.zero; }
        internal void Preset(string name)
        {
            if (name == "Orbit left") Orbit(-10, 0);
            else if (name == "Orbit right") Orbit(10, 0);
            else if (name == "Orbit up") Orbit(0, -10);
            else if (name == "Orbit down") Orbit(0, 10);
            else if (name == "Zoom in") Zoom(-.15f);
            else if (name == "Zoom out") Zoom(.15f);
            else if (name == "Pan left") Pan(-.1f, 0);
            else if (name == "Pan right") Pan(.1f, 0);
            else if (name == "Pan up") Pan(0, .1f);
            else if (name == "Pan down") Pan(0, -.1f);
            if (name == "Reset camera" || name == "Focus") { pan = Vector3.zero; distance = 2; }
            if (name == "Front") { yaw = 0; pitch = 0; }
            else if (name == "Rear") { yaw = 180; pitch = 0; }
            else if (name == "Side") { yaw = 90; pitch = 0; }
            else if (name == "Top") { yaw = 0; pitch = 89; }
            Tick();
        }
        internal void Orbit(float horizontal, float vertical) { yaw += horizontal; pitch = Mathf.Clamp(pitch + vertical, -85, 89); }
        internal void Zoom(float amount) { distance = Mathf.Clamp(distance * Mathf.Exp(amount), .25f, 8); }
        internal void Pan(float horizontal, float vertical) { pan += preview.transform.right * horizontal + preview.transform.up * vertical; }
        internal void Tick()
        {
            if (disposed || !focus || !model || !preview) return;
            Vector3 at = focus.position + model.up * .65f + pan;
            Quaternion rotation = model.rotation * Quaternion.Euler(pitch, yaw, 0);
            preview.transform.SetPositionAndRotation(at - rotation * Vector3.forward * distance, rotation);
        }
        internal Vector3 Project(Vector3 point) => preview ? preview.WorldToScreenPoint(point) : new Vector3(0,0,-1);
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (original) original.enabled = wasEnabled;
            if (owner) UnityEngine.Object.Destroy(owner);
        }
    }
}
