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
        internal Action<SeatPoseProfile> PrepareLoaded;
        internal Action<int> SelectSeat;
        internal Action<string> CameraPreset;
        internal Action Closed;
        internal Action<bool> SetFrozen;
        internal string DirectoryPath;
        internal string[] SeatNames = new string[0];
        internal string MappingWarnings = "";
        internal SeatPoseTransform[] OriginalTargets;
        internal Action ResetPoseDefaults;
        internal Func<Dictionary<string, Vector3>> ReadVehicleTargets;
        internal Action AlignVehicleTargets;
        internal void ApplyVehicleTargets(Dictionary<string, Vector3> positions, Vector3 anchor)
        {
            foreach (var pair in positions)
                if (pair.Key != "Seat" && Array.Find(Profile.targets, t => t.name == pair.Key) == null)
                    throw new InvalidOperationException("Missing editable limb: " + pair.Key);
            Change(() => {
                Profile.anchorPosition = anchor;
                foreach (var pair in positions)
                    if (pair.Key != "Seat") Array.Find(Profile.targets, t => t.name == pair.Key).position = pair.Value;
            });
            numericEdits.Clear();
            message = "Seat, hand and foot positions aligned. Rotations and weights preserved. Undo restores previous values.";
        }
        internal bool SamplingPose;
        internal void ApplyPoseDefaults(SeatPoseTransform[] targets)
        {
            Change(() => Profile.targets = targets);
            numericEdits.Clear();
            message = "IK targets reset to the selected native animation pose. Undo restores previous targets.";
        }
        internal bool ShowTargets = true;
        readonly SeatPoseHistory history = new SeatPoseHistory();
        readonly Dictionary<string,string> numericEdits = new Dictionary<string,string>();
        Rect window = new Rect(20, 40, 460, 700);
        internal Rect WindowRect => window;
        Vector2 scroll;
        string filename = "seat-pose", message = "", poseText = "0";
        string baseline;
        int targetIndex, boneIndex, handIndex;
        float positionStep = .01f, rotationStep = 1;
        bool advanced, closing, frozen;
        bool bridgeMouseHeld;
        Vector2 bridgeMousePosition, bridgePressPosition;
        int bridgeReleaseFrame = -1, bridgeConsumedFrame = -1;
        bool insideScroll, insideGUI;
        bool? pendingAdvanced;
        readonly Queue<Action> pendingEdits = new Queue<Action>();
        internal int BridgeClicks { get; private set; }

        void Update()
        {
            if (pendingAdvanced.HasValue) { advanced = pendingAdvanced.Value; pendingAdvanced = null; }
            while (pendingEdits.Count > 0) Attempt(pendingEdits.Dequeue());
            // Game Bridge drives native InControl actions and the soft cursor; Unity's
            // IMGUI does not receive those as OS mouse events. Its control wrappers
            // hit-test that native pointer against the actual rendered GUI rectangles.
            bool active = RebirthGameBridgeInput.CursorActive;
            var action = active ? RebirthGameBridgeInput.Find("gui.LeftClick") : null;
            bool held = action != null && RebirthGameBridgeInput.IsHeld(action);
            var point = active ? new Vector2(RebirthGameBridgeInput.CursorPosition.x,
                Screen.height - RebirthGameBridgeInput.CursorPosition.y) : bridgeMousePosition;
            if (held && !bridgeMouseHeld) bridgePressPosition = point;
            if (!held && bridgeMouseHeld) bridgeReleaseFrame = Time.frameCount;
            bridgeMouseHeld = held; bridgeMousePosition = point;
        }

        internal void Bind(SeatPoseProfile profile)
        {
            profile.Validate(); Profile = SeatPoseProfile.Parse(profile.Serialize());
            baseline = Profile.Serialize(); poseText = Profile.basePose.ToString(CultureInfo.InvariantCulture);
            history.Commit(Profile);
        }
        void OnGUI()
        {
            if (Profile == null) return;
            insideGUI = true;
            try
            {
                window.width = Mathf.Min(460, Screen.width - 20);
                window.height = Mathf.Min(760, Screen.height - 50);
                window = GUILayout.Window(GetInstanceID(), window, Draw, SeatPoseText.Get("REBIRTH Seat Pose Editor"));
            }
            finally { insideGUI = false; }
        }
        void Draw(int id)
        {
            bool scrollOpen = false;
            try
            {
                var previousColor = GUI.color;
                GUI.color = new Color(.07f, .08f, .09f, 1);
                GUI.DrawTexture(new Rect(0, 22, window.width, window.height - 22), Texture2D.whiteTexture);
                GUI.color = previousColor;
                GUILayout.BeginHorizontal();
                if (Button("Scroll controls up")) scroll.y = Mathf.Max(0, scroll.y - window.height * .65f);
                if (Button("Scroll controls down")) scroll.y += window.height * .65f;
                GUILayout.EndHorizontal();
                scroll = GUILayout.BeginScrollView(scroll);
                scrollOpen = true;
                insideScroll = true;
                Label(Profile.vehicle + " / seat " + Profile.seat + " / " + Profile.rig);
                Label("Vehicle-model local coordinates · metres / degrees");
                Label("Drag in preview to orbit; mouse wheel zooms. Panel wheel scrolls controls.");
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
                foreach (var poseRow in new[] { new[] { 0, 1, 20, 21, 30, 31, 40 }, new[] { 41, 42, 43, 44, 45, 50, 51 } })
                {
                    GUILayout.BeginHorizontal();
                    foreach (int nativePose in poseRow)
                        if (Button(nativePose.ToString())) { int value=nativePose; Change(() => Profile.basePose=value); poseText=value.ToString(CultureInfo.InvariantCulture); }
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                if (Button("Fine")) { positionStep = .001f; rotationStep = .1f; }
                if (Button("Normal")) { positionStep = .01f; rotationStep = 1; }
                if (Button("Coarse")) { positionStep = .05f; rotationStep = 5; }
                GUILayout.EndHorizontal();
                Vector("Seat position", Profile.anchorPosition, positionStep, v => Profile.anchorPosition = v);
                Vector("Seat rotation", Profile.anchorRotation, rotationStep, v => Profile.anchorRotation = v);
                Label("Torso lean: backward ← 0 → forward (degrees)");
                GUILayout.BeginHorizontal();
                if (Button("Lean back")) Change(() => Profile.torsoLean = Mathf.Max(-80, Profile.torsoLean - rotationStep));
                Label(Profile.torsoLean.ToString("0.0", CultureInfo.InvariantCulture));
                if (Button("Lean forward")) Change(() => Profile.torsoLean = Mathf.Min(80, Profile.torsoLean + rotationStep));
                GUILayout.EndHorizontal();
                if (Button("Reset torso lean")) Change(() => Profile.torsoLean = 0);
                if (Profile.targets.Length > 0)
                {
                    if (!SamplingPose && Button("Align seat, hands and feet to vehicle targets")) Attempt(() => AlignVehicleTargets?.Invoke());
                    if (SamplingPose) Label("Reading native pose defaults...");
                    else if (Button("Reset IK to selected pose defaults")) Attempt(() => ResetPoseDefaults?.Invoke());
                    if (Button("Reset all IK to zero")) Change(() =>
                    {
                        foreach (var target in Profile.targets)
                        {
                            target.position = Vector3.zero;
                            target.rotation = Vector3.zero;
                        }
                    });
                    if (OriginalTargets != null && Button("Reset all IK to originals")) Change(() =>
                    {
                        foreach (var target in Profile.targets)
                        {
                            var original = Array.Find(OriginalTargets, t => t.name == target.name);
                            if (original == null) continue;
                            target.position = original.position;
                            target.rotation = original.rotation;
                            target.weight = original.weight;
                        }
                    });
                    foreach (var row in new[] {
                        new[] { "IKHandL", "Left hand", "IKHandR", "Right hand" },
                        new[] { "IKFootL", "Left foot", "IKFootR", "Right foot" },
                        new[] { "IKElbowL", "Left elbow", "IKElbowR", "Right elbow" },
                        new[] { "IKKneeL", "Left knee", "IKKneeR", "Right knee" } })
                    {
                        GUILayout.BeginHorizontal();
                        for (int i = 0; i < row.Length; i += 2)
                        {
                            int index = Array.FindIndex(Profile.targets, t => t.name == row[i]);
                            if (index >= 0 && Button(row[i + 1])) targetIndex = index;
                        }
                        GUILayout.EndHorizontal();
                    }
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
                GUILayout.BeginHorizontal();
                if (Button("Left hand")) handIndex = 0;
                if (Button("Right hand")) handIndex = 1;
                GUILayout.EndHorizontal();
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
                    PrepareLoaded?.Invoke(loaded);
                    Change(() => Profile = loaded); poseText = Profile.basePose.ToString(CultureInfo.InvariantCulture);
                });
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (Button("Publish for single-player")) Attempt(() => { SeatPosePublishedStore.Publish(SeatPoseGameplay.PublishedDirectory, Profile); SeatPoseGameplay.Reload(); message = "Published for single-player"; });
                if (Button("Remove published pose")) Attempt(() => { SeatPosePublishedStore.Remove(SeatPoseGameplay.PublishedDirectory, Profile); SeatPoseGameplay.Reload(); message = "Removed local published pose"; });
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (Button("Close / keep saved profile")) Close();
                if (Button("Cancel edits")) { Restore(SeatPoseProfile.Parse(baseline)); Close(); }
                GUILayout.EndHorizontal();
                Label(message);
            }
            catch (ExitGUIException) { throw; }
            catch (Exception error) { message = error.Message; }
            finally { insideScroll = false; if (scrollOpen) GUILayout.EndScrollView(); }
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
            if (Event.current.type == EventType.Repaint && (bridgeMouseHeld || bridgeReleaseFrame == Time.frameCount) && BridgePointVisible())
            {
                var rect = GUILayoutUtility.GetLastRect();
                var press = GUIUtility.ScreenToGUIPoint(bridgePressPosition);
                var point = GUIUtility.ScreenToGUIPoint(bridgeMousePosition);
                if (rect.Contains(press)) next = Mathf.Lerp(minimum, 1, Mathf.Clamp01((point.x - rect.xMin) / rect.width));
            }
            if (Mathf.Abs(next - value) > .0001f) Change(() => set(next));
        }
        void Change(Action change)
        {
            if (insideGUI && Event.current.type == EventType.Repaint) { pendingEdits.Enqueue(() => Change(change)); return; }
            string before = Profile.Serialize();
            try { change(); Profile.Validate(); Apply?.Invoke(Profile); history.Commit(Profile); numericEdits.Clear(); poseText = Profile.basePose.ToString(CultureInfo.InvariantCulture); message = "Unsaved edits"; }
            catch(Exception error) { Profile = SeatPoseProfile.Parse(before); Attempt(()=>Apply?.Invoke(Profile)); message=error.Message; }
        }
        void Attempt(Action action) { try { action(); } catch(ExitGUIException){throw;} catch(Exception error){message=error.Message;} }
        bool BridgePointVisible()
        {
            var clip = insideScroll ? new Rect(window.x + 10, window.y + 46, window.width - 20, window.height - 58) : window;
            return clip.Contains(bridgePressPosition) && clip.Contains(bridgeMousePosition);
        }
        bool BridgeClick()
        {
            if (Event.current.type != EventType.Repaint || bridgeReleaseFrame != Time.frameCount || bridgeConsumedFrame == Time.frameCount || !BridgePointVisible()) return false;
            var rect = GUILayoutUtility.GetLastRect();
            if (!rect.Contains(GUIUtility.ScreenToGUIPoint(bridgePressPosition)) || !rect.Contains(GUIUtility.ScreenToGUIPoint(bridgeMousePosition))) return false;
            bridgeConsumedFrame = Time.frameCount; BridgeClicks++; return true;
        }
        bool Button(string text,params GUILayoutOption[] options) { bool clicked = GUILayout.Button(SeatPoseText.Get(text),options); return clicked || BridgeClick(); }
        static void Label(string text,params GUILayoutOption[] options)=>GUILayout.Label(SeatPoseText.Get(text),options);
        bool Toggle(bool value,string text)
        {
            bool next = GUILayout.Toggle(value,SeatPoseText.Get(text));
            if (!BridgeClick()) return next;
            // New layout groups must be introduced before the next Layout event.
            if (text == "Advanced individual bone offsets") { pendingAdvanced = !value; return value; }
            return !value;
        }
        void Restore(SeatPoseProfile profile)
        {
            if (profile == null) return;
            if (insideGUI && Event.current.type == EventType.Repaint) { pendingEdits.Enqueue(() => Restore(profile)); return; }
            Profile = profile; numericEdits.Clear(); poseText = Profile.basePose.ToString(CultureInfo.InvariantCulture); Apply?.Invoke(Profile);
        }
        void Close() { if (closing) return; closing = true; try { Closed?.Invoke(); } finally { Destroy(this); } }
        void OnDestroy() { if (!closing) { closing = true; Closed?.Invoke(); } }
    }
}

