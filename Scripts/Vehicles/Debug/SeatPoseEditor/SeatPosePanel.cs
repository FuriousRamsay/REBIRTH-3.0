using System;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;

namespace Rebirth.Vehicles.Authoring
{
    // Created only by an explicit authoring session. No global Update or Harmony hook.
    internal sealed class SeatPosePanel : MonoBehaviour
    {
        internal SeatPoseProfile Profile { get; private set; }
        internal Action<SeatPoseProfile> Apply;
        internal Action<int> SelectSeat;
        internal Action<string> CameraPreset;
        internal Action Closed;
        internal Action<bool> SetFrozen;
        internal string DirectoryPath;
        internal string[] SeatNames = new string[0];
        internal string MappingWarnings = "";
        internal bool ShowTargets = true;
        readonly SeatPoseHistory history = new SeatPoseHistory();
        readonly Dictionary<string,string> numericEdits = new Dictionary<string,string>();
        Rect window = new Rect(20, 40, 460, 700);
        Vector2 scroll;
        string filename = "seat-pose", message = "", poseText = "0";
        string baseline;
        int targetIndex, boneIndex, handIndex;
        float positionStep = .01f, rotationStep = 1;
        bool advanced, closing, frozen;

        internal void Bind(SeatPoseProfile profile)
        {
            profile.Validate(); Profile = SeatPoseProfile.Parse(profile.Serialize());
            baseline = Profile.Serialize(); poseText = Profile.basePose.ToString(CultureInfo.InvariantCulture);
            history.Commit(Profile);
        }
        void OnGUI()
        {
            if (Profile == null) return;
            window.width = Mathf.Min(460, Screen.width - 20);
            window.height = Mathf.Min(760, Screen.height - 50);
            window = GUILayout.Window(GetInstanceID(), window, Draw, SeatPoseText.Get("REBIRTH Seat Pose Editor"));
        }
        void Draw(int id)
        {
            bool scrollOpen = false;
            try
            {
                scroll = GUILayout.BeginScrollView(scroll);
                scrollOpen = true;
                Label(Profile.vehicle + " / seat " + Profile.seat + " / " + Profile.rig);
                Label("Vehicle-model local coordinates · metres / degrees");
                if(!string.IsNullOrEmpty(MappingWarnings))Label("Unavailable mappings: "+MappingWarnings);
                ShowTargets=Toggle(ShowTargets,"Show IK target markers");
                for (int i = 0; i < SeatNames.Length; i++)
                    if (Button(SeatNames[i])) SelectSeat?.Invoke(i);
                GUILayout.BeginHorizontal();
                foreach (string view in new[] { "Focus", "Front", "Side", "Rear", "Top", "Reset camera" })
                    if (Button(view)) Attempt(()=>CameraPreset?.Invoke(view));
                GUILayout.EndHorizontal();
                foreach (var row in new[] { new[] { "Orbit left", "Orbit right", "Orbit up", "Orbit down" }, new[] { "Zoom in", "Zoom out", "Pan left", "Pan right" }, new[] { "Pan up", "Pan down" } })
                { GUILayout.BeginHorizontal(); foreach (string action in row) if (Button(action)) Attempt(()=>CameraPreset?.Invoke(action)); GUILayout.EndHorizontal(); }
                bool freeze = Toggle(frozen, "Freeze base animation for inspection");
                if (freeze != frozen) { SetFrozen?.Invoke(freeze); frozen = freeze; }
                Label("Native base pose ID");
                poseText = GUILayout.TextField(poseText);
                if (Button("Apply native base pose") && int.TryParse(poseText, out int pose) && pose >= 0)
                    Change(() => Profile.basePose = pose);
                GUILayout.BeginHorizontal();
                foreach (int nativePose in new[] { 0, 1, 20, 21, 30, 31, 40, 41, 42, 43, 44, 45, 50, 51 })
                    if (Button(nativePose.ToString())) { int value=nativePose; Change(() => Profile.basePose=value); poseText=value.ToString(CultureInfo.InvariantCulture); }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (Button("Fine")) { positionStep = .001f; rotationStep = .1f; }
                if (Button("Normal")) { positionStep = .01f; rotationStep = 1; }
                if (Button("Coarse")) { positionStep = .05f; rotationStep = 5; }
                GUILayout.EndHorizontal();
                Vector("Seat position", Profile.anchorPosition, positionStep, v => Profile.anchorPosition = v);
                Vector("Seat rotation", Profile.anchorRotation, rotationStep, v => Profile.anchorRotation = v);
                if (Profile.targets.Length > 0)
                {
                    GUILayout.BeginHorizontal();
                    if (Button("Previous target")) targetIndex = (targetIndex + Profile.targets.Length - 1) % Profile.targets.Length;
                    if (Button("Next target")) targetIndex = (targetIndex + 1) % Profile.targets.Length;
                    GUILayout.EndHorizontal();
                    targetIndex = Mathf.Clamp(targetIndex, 0, Profile.targets.Length - 1);
                    var target = Profile.targets[targetIndex];
                    Vector(target.name + SeatPoseText.Get(" position"), target.position, positionStep, v => target.position = v);
                    Vector(target.name + SeatPoseText.Get(" rotation"), target.rotation, rotationStep, v => target.rotation = v);
                    Slider("Weight", target.weight, v => target.weight = v);
                    if(Button("Mirror selected target to opposite limb"))Change(()=>Profile.MirrorTarget(target.name));
                    if (Button("Reset selected target"))
                    {
                        var initial=SeatPoseProfile.Parse(baseline);
                        var original=Array.Find(initial.targets,t=>t.name==target.name);
                        if(original!=null)Change(()=>{target.position=original.position;target.rotation=original.rotation;target.weight=original.weight;});
                    }
                }
                handIndex = GUILayout.Toolbar(handIndex, new[] { SeatPoseText.Get("Left hand"), SeatPoseText.Get("Right hand") });
                var hand = handIndex == 0 ? Profile.leftHand : Profile.rightHand;
                Slider("Open ← native → closed", hand.close, v => hand.close = v, -1);
                Slider("Finger spread", hand.spread, v => hand.spread = v, -1);
                if(Button("Copy this hand's controls to other hand"))Change(()=>
                {var destination=handIndex==0?Profile.rightHand:Profile.leftHand;destination.close=hand.close;destination.spread=hand.spread;destination.curl=(float[])hand.curl.Clone();});
                string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
                for (int i = 0; i < 5; i++) { int finger = i; Slider(SeatPoseText.Get(fingers[i]) + SeatPoseText.Get(" curl"), hand.curl[i], v => hand.curl[finger] = v, -1); }
                advanced = Toggle(advanced, "Advanced individual bone offsets");
                if (advanced && Profile.bones.Length > 0)
                {
                    GUILayout.BeginHorizontal();
                    if (Button("Previous bone")) boneIndex = (boneIndex + Profile.bones.Length - 1) % Profile.bones.Length;
                    if (Button("Next bone")) boneIndex = (boneIndex + 1) % Profile.bones.Length;
                    GUILayout.EndHorizontal();
                    boneIndex = Mathf.Clamp(boneIndex, 0, Profile.bones.Length - 1);
                    var bone = Profile.bones[boneIndex];
                    Vector(bone.path, bone.rotation, rotationStep, v => bone.rotation = v);
                    if (Button("Reset this bone")) Change(() => bone.rotation = Vector3.zero);
                }
                GUILayout.BeginHorizontal();
                if (Button("Undo")) Restore(history.Undo());
                if (Button("Redo")) Restore(history.Redo());
                if (Button("Reset session")) Change(() => Profile = SeatPoseProfile.Parse(baseline));
                GUILayout.EndHorizontal();
                filename = GUILayout.TextField(filename);
                GUILayout.BeginHorizontal();
                if (Button("Save profile")) Attempt(()=>{ SeatPoseProfileStore.Save(DirectoryPath, filename, Profile); message = "Saved " + filename; });
                if (Button("Export copy")) Attempt(()=>{ SeatPoseProfileStore.Save(System.IO.Path.Combine(DirectoryPath,"Exports"),filename,Profile); message="Exported complete profile to Exports"; });
                if (Button("Load profile"))
                Attempt(()=>{
                    // Same filename validation and containment rules as Save, without rewriting it.
                    if (filename.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || string.IsNullOrWhiteSpace(filename)) throw new ArgumentException("Invalid profile filename.");
                    var loaded = SeatPoseProfileStore.Load(System.IO.Path.Combine(DirectoryPath, filename + ".json"));
                    if (loaded.vehicle != Profile.vehicle || loaded.rig != Profile.rig || loaded.seat != Profile.seat) throw new InvalidOperationException("Profile belongs to a different vehicle, rig or seat.");
                    Change(() => Profile = loaded); poseText = Profile.basePose.ToString(CultureInfo.InvariantCulture);
                });
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (Button("Close / keep saved profile")) Close();
                if (Button("Cancel edits")) { Restore(SeatPoseProfile.Parse(baseline)); Close(); }
                GUILayout.EndHorizontal();
                Label(message);
            }
            catch (ExitGUIException) { throw; }
            catch (Exception error) { message = error.Message; }
            finally { if (scrollOpen) GUILayout.EndScrollView(); }
            GUI.DragWindow(new Rect(0, 0, window.width, 22));
        }
        void Vector(string label, Vector3 value, float step, Action<Vector3> set)
        {
            Label(label);
            for (int axis = 0; axis < 3; axis++)
            {
                int selected = axis; GUILayout.BeginHorizontal();
                try
                {
                Label("XYZ"[axis].ToString(), GUILayout.Width(20));
                if (Button("−", GUILayout.Width(30))) { var v = value; v[selected] -= step; Change(() => set(v)); }
                string key=label+":"+axis;
                if(!numericEdits.TryGetValue(key,out string text))text=value[axis].ToString("0.####",CultureInfo.InvariantCulture);
                numericEdits[key]=GUILayout.TextField(text);
                if(Button("Set",GUILayout.Width(35)))
                {
                    if(!float.TryParse(numericEdits[key],NumberStyles.Float,CultureInfo.InvariantCulture,out float parsed)||float.IsInfinity(parsed)||float.IsNaN(parsed))throw new ArgumentException("Enter a finite number.");
                    var v=value;v[selected]=parsed;Change(()=>set(v));
                }
                if (Button("+", GUILayout.Width(30))) { var v = value; v[selected] += step; Change(() => set(v)); }
                }
                finally { GUILayout.EndHorizontal(); }
            }
        }
        void Slider(string label, float value, Action<float> set, float minimum = 0)
        {
            Label(label + " " + value.ToString("0.00", CultureInfo.InvariantCulture));
            float next = GUILayout.HorizontalSlider(value, minimum, 1);
            if (Mathf.Abs(next - value) > .0001f) Change(() => set(next));
        }
        void Change(Action change)
        {
            string before = Profile.Serialize();
            try { change(); Profile.Validate(); Apply?.Invoke(Profile); history.Commit(Profile); numericEdits.Clear(); message = "Unsaved edits"; }
            catch(Exception error) { Profile = SeatPoseProfile.Parse(before); Attempt(()=>Apply?.Invoke(Profile)); message=error.Message; }
        }
        void Attempt(Action action) { try { action(); } catch(ExitGUIException){throw;} catch(Exception error){message=error.Message;} }
        static bool Button(string text,params GUILayoutOption[] options)=>GUILayout.Button(SeatPoseText.Get(text),options);
        static void Label(string text,params GUILayoutOption[] options)=>GUILayout.Label(SeatPoseText.Get(text),options);
        static bool Toggle(bool value,string text)=>GUILayout.Toggle(value,SeatPoseText.Get(text));
        void Restore(SeatPoseProfile profile) { if (profile == null) return; Profile = profile; numericEdits.Clear(); poseText = Profile.basePose.ToString(CultureInfo.InvariantCulture); Apply?.Invoke(Profile); }
        void Close() { if (closing) return; closing = true; try { Closed?.Invoke(); } finally { Destroy(this); } }
        void OnDestroy() { if (!closing) { closing = true; Closed?.Invoke(); } }
    }
}

