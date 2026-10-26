using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using RebirthVehicleInspector.Config;
using RebirthVehicleInspector.Model;
using Pose = RebirthVehicleInspector.Model.Pose;
using RebirthVehicleInspector.Discovery;
using RebirthVehicleInspector.Capture;
using RebirthVehicleInspector.Util;

namespace RebirthVehicleInspector
{
    internal sealed class VehicleInspectorService
    {
        private readonly WorldProbe world = new WorldProbe();
        private readonly string exportFolder;
        private readonly VehicleXmlReader xml;
        private object selected;
        private int selectedId = -1;
        private string selectedBy="none";
        private string boundVehicleXml;
        private CaptureFile recording;
        private readonly List<FrameRecord> frames = new List<FrameRecord>();
        private float recordStarted, nextFrame, recordDuration, period;
        private string recordLabel="motion";
        private float candidateRadius=OccupantCapture.MinimumCandidateRadius;
        public bool IsRecording => recording!=null;

        internal VehicleInspectorService(string modPath)
        {
            exportFolder = Path.Combine(modPath,"Exports");
            xml = new VehicleXmlReader(Path.GetFullPath(Path.Combine(Application.dataPath,"..")));
        }
        public void ResetForWorldStart()
        {
            if(IsRecording)
            {
                try { Log.Out("[VehicleInspector] Previous-world recording: " + StopRecord()); }
                catch(Exception ex) { Log.Out("[VehicleInspector] Could not finalize previous recording: " + ex); }
            }
            selected=null; selectedId=-1; selectedBy="none"; boundVehicleXml=null;
            candidateRadius=OccupantCapture.MinimumCandidateRadius;
        }
        public string Help => "vinspect help | list | select nearest [radius] | select <entityId> | bind <vehicles.xml name> | " +
                              "snap [label] | record <seconds 1-120> [hz 1-10] [scenarioLabel] | stop | status | diagnose | seatdiag";

        public string ListVehicles()
        {
            var rows = new List<string>();
            var vehicles=world.FindVehicles();
            foreach(object e in vehicles)
            {
                EntitySummary s=world.Describe(e);
                if(s==null) continue;
                rows.Add(s.entityId+" "+s.type+" distance="+s.distanceMetres.ToString("F1",CultureInfo.InvariantCulture)+
                    "m root="+s.referenceStatus);
            }
            return rows.Count==0 ? "No loaded vehicle entities found. Must be in a local world with vehicles spawned; check GameManager world entity shape." :
                 "Loaded vehicles (max 128):\n"+string.Join("\n",rows.ToArray());
        }
        public string SelectNearest(float radius)
        {
            if(IsRecording) return "Stop the recording before changing selection.";
            object e=world.NearestVehicle(radius);
            return e==null ? "No vehicle within "+radius.ToString("F1",CultureInfo.InvariantCulture)+"m, or player position unavailable." : Select(e,"nearest");
        }
        public string SelectId(int id)
        {
            if(IsRecording) return "Stop the recording before changing selection.";
            object e=world.FindVehicleById(id);
            return e==null ? "No loaded EntityVehicle with ID "+id+"." : Select(e,"entityId");
        }
        private string Select(object e,string method)
        {
            selected=e; selectedId=Reflect.Id(e); selectedBy=method;
            candidateRadius=EstimateOccupantRadius(Reflect.FindTransform(e));
            boundVehicleXml=null; // never silently apply the last vehicle's XML to this vehicle
            var d=world.Describe(e);
            return "Selected "+d.type+" #"+selectedId+" ("+d.referenceStatus+"). " +
                   "Use 'vinspect bind vehicleTruck4x4' (or correct XML name) then 'vinspect snap'.";
        }
        public string BindXml(string name)
        {
            if(IsRecording) return "Stop recording before changing XML binding.";
            if(selected==null) return "Select a vehicle first.";
            if(name.Length>96 || name.IndexOfAny(new [] {'<','>','"','\'','/','\\'})>=0) return "Invalid XML vehicle name.";
            var c=xml.Read(name);
            if(c.status!="ok") return "Could not bind '"+name+"': "+c.status+" ("+xml.SourcePath+")";
            boundVehicleXml=name;
            return "Bound to vehicles.xml <vehicle name=\""+name+"\">; "+c.seats.Length+
                " seat(s), "+c.parts.Length+" part group(s). Binding is explicit and not independently identity-verified.";
        }
        public string Status()
        {
            return "Vehicle="+(selected==null ? "none" : selectedId.ToString())+
                "; root="+(Reflect.FindTransform(selected)!=null ? "resolved" : "unresolved")+
                "; xml="+(boundVehicleXml??"unbound")+
                "; recording="+IsRecording+
                (IsRecording ? " frames="+frames.Count+" / "+recordDuration+"s" : "")+
                "; nearbyCandidateRadius="+candidateRadius.ToString("F1",CultureInfo.InvariantCulture)+"m"+
                "; destination="+exportFolder;
        }
        public string Diagnose()
        {
            if(selected==null) return "Select a vehicle first. 'vinspect list' shows loaded IDs.";
            var hints=world.GeometryHints(selected);
            return "Geometry root: "+(Reflect.FindTransform(selected)!=null ? "RESOLVED":"UNRESOLVED")+
                "\nRelevant class members (names/types only):\n"+string.Join("\n",hints);
        }
        // Emits only seat/attachment-related member names, types, and bounded
        // primitive values; never private strings or arbitrary object graphs.
        public string SeatDiagnostics()
        {
            if(selected==null) return "Select a vehicle first ('vinspect select nearest 15').";
            Transform root=Reflect.FindTransform(selected);
            if(root==null) return "Vehicle transform not available.";
            VehicleConfig config=xml.Read(boundVehicleXml);
            var lines=new List<string>();
            lines.Add("Vehicle #"+selectedId+", XML="+(boundVehicleXml??"UNBOUND")+", seats="+config.seats.Length);
            var occupants=OccupantCapture.Nearby(selected,root,world,candidateRadius,config);
            foreach(var occupant in occupants)
            {
                lines.Add("Candidate #"+occupant.entityId+" attached="+
                    occupant.association+" seat="+occupant.reportedSeatIndex+
                    " seatStatus="+occupant.reportedSeatStatus+" evidence="+occupant.seatEvidence.Length);
                foreach(var ev in occupant.seatEvidence)
                    lines.Add("  " + ev.source+"."+ev.member+"="+ev.rawValue+" ("+ev.assessment+")");
                object player=world.FindPlayerById(occupant.entityId);
                foreach(string hint in SeatMemberProbe.Hints(player,32))
                    lines.Add("  player."+hint);
            }
            foreach(string hint in SeatMemberProbe.Hints(selected,32))
                lines.Add("  vehicleEntity."+hint);
            object holder=Reflect.Get(selected,"vehicle","Vehicle");
            foreach(string hint in SeatMemberProbe.Hints(holder,32))
                lines.Add("  vehicleComponent."+hint);
            return string.Join("\n",lines.ToArray());
        }
        // The F1 console is not a reliable place to copy long diagnostics.
        // Explicit seatdiag and seated snapshots persist identical evidence in
        // an independent file, and mirror it to the standard 7DTD game log.
        public string SeatDiagnosticsToLog(string trigger="command")
        {
            try
            {
                string context="";
                // If no vehicle is selected, try the closest loaded vehicle.
                // This does NOT silently bind its identity to 4x4 XML or invent
                // a seat index. A missing XML binding is conspicuous in the log.
                if(selected==null || world.FindVehicleById(selectedId)==null)
                {
                    object nearest=world.NearestVehicle(15f);
                    if(nearest!=null)
                        context="Automatic proximity selection: "+Select(nearest,"seatdiag-nearest")+"\n";
                    else
                    {
                        selected=null; selectedId=-1; selectedBy="none"; boundVehicleXml=null;
                        context="WARNING: no loaded vehicle was selected or within 15 metres.\n";
                    }
                }
                string report=context+SeatDiagnostics();
                string file=SeatDiagnosticLog.Write(exportFolder,trigger,selectedId,boundVehicleXml,report);
                return "Seat diagnostics saved to: "+file+
                    ". Also written to the 7DTD log. Upload the ZIP created by Collect_RuntimeInspector_Captures.ps1; no console copy needed.";
            }
            catch(Exception ex)
            {
                Log.Out("[VehicleInspector][seatdiag] ERROR saving diagnostic text: "+ex);
                return "ERROR: seat diagnostics could not be saved ("+ex.GetType().Name+"): "+ex.Message+
                    ". Check the 7DTD game log; do not assume the report exists.";
            }
        }
        private CaptureFile MakeCapture(string type)
        {
            if(selected==null) throw new InvalidOperationException("Select a loaded vehicle first.");
            // A destroyed entity may have remained referenced after changing worlds. Refresh by ID.
            object live=world.FindVehicleById(selectedId);
            if(live==null) throw new InvalidOperationException("Selected vehicle is no longer loaded; select again.");
            selected=live;
            Transform t=Reflect.FindTransform(selected);
            if(t==null) throw new InvalidOperationException("Vehicle transform not resolved. Run 'vinspect diagnose' and adapt FindTransform to this game version.");
            bool truncated;
            var file=new CaptureFile {
                capturedUtc=DateTime.UtcNow.ToString("o"), gameVersion=Application.version,
                captureType=type, selectionMethod=selectedBy,
                vehicle=world.Describe(selected),
                hierarchy=HierarchyCapture.Collect(t,out truncated), truncated=truncated,
                xmlConfig=xml.Read(boundVehicleXml),
                notes="Snapshots come from live 7DTD Unity objects. Nearby players are NOT verified seat occupants. " +
                      "Vehicle-relative positions are rooted at the discovered runtime transform. No prefab was modified."
            };
            file.transformChecks=TransformLinkChecks.Check(t,file.xmlConfig);
            var warnings=new List<string>();
            int requiredMissing=0,conditionalMissing=0;
            foreach(var check in file.transformChecks)
            {
                if(check.found) continue;
                if(check.classification=="conditional_upgrade") conditionalMissing++;
                else requiredMissing++;
            }
            file.unresolvedRequiredTransformCount=requiredMissing;
            file.unresolvedConditionalUpgradeTransformCount=conditionalMissing;
            if(requiredMissing>0) warnings.Add("xml_required_transform_paths_unresolved:"+requiredMissing+"/"+file.transformChecks.Length);
            if(conditionalMissing>0) warnings.Add("xml_optional_upgrade_paths_unresolved_unverified:"+conditionalMissing+"/"+file.transformChecks.Length);
            if(truncated) warnings.Add("hierarchy_truncated_at_"+HierarchyCapture.MaxNodes);
            if(boundVehicleXml==null) warnings.Add("xml_unbound: use 'vinspect bind <vehicleName>' to correlate XML with the selected entity");
            if(file.xmlConfig.status!="ok") warnings.Add("xml_status:"+file.xmlConfig.status);
            file.warnings=warnings.ToArray();
            file.diagnostics=world.GeometryHints(selected);
            return file;
        }
        public string Snapshot(string label)
        {
            if(IsRecording) return "Recording in progress; stop it before taking an independent snapshot.";
            CaptureFile cap=MakeCapture("snapshot");
            cap.scenarioLabel=SafeLabel(label);
            cap.frames=new [] { CaptureFrame(Time.realtimeSinceStartup,Time.realtimeSinceStartup,cap.xmlConfig) };
            string path=Save(cap,label);
            // Seated-driver evidence should be automatically logged even when
            // the player cannot copy F1 console output. This never replaces the
            // actual JSON capture or marks an unknown seat as verified.
            string seatDiagnosticResult = cap.scenarioLabel=="seated"
                ? SeatDiagnosticsToLog("automatic_seated_snapshot") : null;
            return "Snapshot saved: "+path+". Hierarchy="+cap.hierarchy.Length+
                   " nodes, frames="+cap.frames.Length+", nearby candidates="+cap.occupantCandidateSamples+
                   ", bones="+cap.occupantBoneSamples+", confirmed seat samples="+cap.knownSeatSamples+
                   "; required XML paths missing="+cap.unresolvedRequiredTransformCount+
                   "; conditional upgrade paths unresolved="+cap.unresolvedConditionalUpgradeTransformCount+
                   (seatDiagnosticResult==null ? "" : "; "+seatDiagnosticResult);
        }
        public string StartRecord(float duration,float rate,string label="motion")
        {
            if(IsRecording) return "A recording is already running; type 'vinspect stop'.";
            recordLabel=SafeLabel(label);
            recording=MakeCapture("motion");
            recording.scenarioLabel=recordLabel;
            frames.Clear();
            recordStarted=Time.realtimeSinceStartup;
            nextFrame=recordStarted;
            recordDuration=Mathf.Clamp(duration,1f,120f);
            period=1f / Mathf.Clamp(rate,1f,10f);
            Sample(recordStarted);
            nextFrame=recordStarted+period;
            return "Recording vehicle #"+selectedId+" for "+recordDuration.ToString("F1",CultureInfo.InvariantCulture)+
                   "s at "+(1f/period).ToString("F1",CultureInfo.InvariantCulture)+"Hz; use 'vinspect stop' to finish early.";
        }
        public string StopRecord()
        {
            if(!IsRecording) return "No recording in progress.";
            CaptureFile cap=recording;
            cap.frames=frames.ToArray();
            string path=Save(cap,recordLabel);
            recording=null;
            frames.Clear();
            return "Motion saved: "+path+"; "+cap.frames.Length+" frames, "+cap.hierarchy.Length+
                   " hierarchy nodes, bones="+cap.occupantBoneSamples+", confirmed seat samples="+cap.knownSeatSamples+".";
        }
        public void LateTick()
        {
            if(!IsRecording) return;
            float now=Time.realtimeSinceStartup;
            if(now>=nextFrame && frames.Count<1201)
            {
                try { Sample(now); }
                catch(Exception ex)
                {
                    Log.Out("[VehicleInspector] Recording interrupted: "+ex);
                    recording.warnings=Append(recording.warnings,"recording_interrupted:"+ex.GetType().Name);
                    try { StopRecord(); } catch(Exception saveEx) { Log.Out("[VehicleInspector] Failed export: "+saveEx); }
                    return;
                }
                nextFrame=now+period; // no catch-up burst after framerate stalls
            }
            if(now-recordStarted>=recordDuration || frames.Count>=1201)
            {
                if(frames.Count>=1201) recording.warnings=Append(recording.warnings,"frame_cap_reached");
                try { Log.Out("[VehicleInspector] "+StopRecord()); }
                catch(Exception ex) { Log.Out("[VehicleInspector] Failed to save recording: "+ex); }
            }
        }
        private static string[] Append(string[] arr,string s)
        {
            var copy=new string[arr.Length+1];Array.Copy(arr,copy,arr.Length);copy[arr.Length]=s;return copy;
        }
        private void Sample(float now)
        {
            if(selected==null) throw new InvalidOperationException("Vehicle selection lost.");
            object live=world.FindVehicleById(selectedId);
            if(live==null) throw new InvalidOperationException("Selected entity unloaded.");
            selected=live;
            frames.Add(CaptureFrame(now,recordStarted,recording.xmlConfig));
        }
        private FrameRecord CaptureFrame(float time,float origin,VehicleConfig config)
        {
            Transform t=Reflect.FindTransform(selected);
            if(t==null) throw new InvalidOperationException("Vehicle transform unavailable.");
            return new FrameRecord {
                secondsFromStart=time-origin, realtimeSinceStartup=time, unityFrame=Time.frameCount,
                vehiclePose=Pose.From(t,t), trackedNodes=HierarchyCapture.Tracked(t),
                occupantCandidates=OccupantCapture.Nearby(selected,t,world,candidateRadius,config)
            };
        }
        private static float EstimateOccupantRadius(Transform root)
        {
            if(root==null) return OccupantCapture.MinimumCandidateRadius;
            float longest=OccupantCapture.MinimumCandidateRadius;
            try
            {
                foreach(Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if(renderer==null) continue;
                    longest=Mathf.Max(longest,Vector3.Distance(root.position,renderer.bounds.center)+renderer.bounds.extents.magnitude+2f);
                }
            }
            catch(Exception) {return OccupantCapture.MinimumCandidateRadius;}
            return Mathf.Clamp(longest,OccupantCapture.MinimumCandidateRadius,40f);
        }
        private static string SafeLabel(string label)
        {
            if(string.IsNullOrWhiteSpace(label)) return "capture";
            if(label.Length>40) throw new ArgumentException("Scenario label must be 1–40 characters.");
            foreach(char ch in label)
                if(!((ch>='A'&&ch<='Z') || (ch>='a'&&ch<='z') ||
                     (ch>='0'&&ch<='9') || ch=='_' || ch=='-'))
                    throw new ArgumentException("Scenario label may contain only letters, numbers, _ and -.");
            return label;
        }
        private string Save(CaptureFile cap,string label)
        {
            Directory.CreateDirectory(exportFolder);
            string clean=SafeLabel(label);
            // Timestamp + random suffix avoids overwriting captures when multiple
            // tests finish within a millisecond or a previous export is present.
            string basename=DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff",CultureInfo.InvariantCulture)+
                           "_"+selectedId+"_"+clean+"_"+Guid.NewGuid().ToString("N").Substring(0,8);
            string path=Path.Combine(exportFolder,basename+".json");
            // Export DTOs through our own public-field serializer. The game's Unity
            // JsonUtility build omitted ALL nested model records from live captures.
            if(cap.vehicle==null || cap.hierarchy==null || cap.hierarchy.Length<1 ||
               cap.frames==null || cap.frames.Length<1 || cap.xmlConfig==null ||
               cap.transformChecks==null)
                throw new InvalidDataException("Capture is missing vehicle, hierarchy, frame, XML, or transform-check data; refusing metadata-only export.");
            cap.serializer="RebirthCaptureFields/1";
            cap.hierarchyNodeCount=cap.hierarchy.Length;
            cap.frameCount=cap.frames.Length;
            cap.transformCheckCount=cap.transformChecks.Length;
            int candidates=0, bones=0, attached=0, knownSeat=0;
            foreach(var f in cap.frames)
            {
                if(f==null || f.vehiclePose==null || f.trackedNodes==null || f.occupantCandidates==null)
                    throw new InvalidDataException("Capture contains an invalid frame.");
                foreach(var o in f.occupantCandidates)
                {
                    if(o==null) continue;
                    candidates++;
                    if(o.bones!=null) bones+=o.bones.Length;
                    if(o.association!=null && o.association.StartsWith("runtimeVehicleReference_",StringComparison.Ordinal))
                    {
                        attached++;
                        if(o.reportedSeatIndex>=0 && o.reportedSeatStatus=="verified_player_slot") knownSeat++;
                    }
                }
            }
            cap.occupantCandidateSamples=candidates;
            cap.occupantBoneSamples=bones;
            cap.vehicleAttachmentSamples=attached;
            cap.knownSeatSamples=knownSeat;
            if(cap.scenarioLabel=="seated" || cap.scenarioLabel=="steering" || cap.scenarioLabel=="driving")
            {
                if(knownSeat==0) cap.warnings=Append(cap.warnings,"no_verified_seat_index_observed");
                if(bones==0) cap.warnings=Append(cap.warnings,"no_occupant_bones_observed");
            }
            if(cap.captureType=="motion" && cap.frameCount<2)
                cap.warnings=Append(cap.warnings,"insufficient_motion_frames");
            string temp=path+".tmp";
            try
            {
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                using(var writer=new StreamWriter(stream,new System.Text.UTF8Encoding(false)))
                {
                    CaptureJsonWriter.Write(writer,cap);
                    writer.Flush();
                    stream.Flush(true);
                }
                File.Move(temp,path);
            }
            catch
            {
                try { if(File.Exists(temp)) File.Delete(temp); } catch { }
                throw;
            }
            return path;
        }
    }
}
