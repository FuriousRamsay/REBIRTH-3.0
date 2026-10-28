#if REBIRTH_DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

// Explicit, local debug session: no polling, save writes or global vehicle-definition mutations.
internal sealed partial class RebirthVehicleTuningSession
{
    internal sealed class Setting
    {
        internal string Name, Original, Value, Xml;
        internal bool ReadOnly, ResolvedXml, Tuned;
        internal float Min,Max;
        internal Action<string> Write;
        internal Action Restore;
        internal Func<string> Read;
    }
    internal readonly EntityVehicle Entity;
    internal readonly List<Setting> Settings = new List<Setting>();
    private readonly object native;
    private readonly string nativeName;
    private readonly Rigidbody capturedBody;
    private readonly WheelCollider[] capturedWheels;
    private readonly object[] capturedEngines;
    internal RebirthVehicleTuningSession(EntityVehicle entity)
    {
        Entity=entity; native=entity.vehicle;nativeName=entity.vehicle.GetName();capturedBody=entity.vehicleRB;
        capturedWheels=entity.wheels?.Select(w=>w?.wheelC).ToArray()??new WheelCollider[0];
        capturedEngines=entity.vehicle.vehicleParts?.OfType<VPEngine>().Cast<object>().ToArray()??new object[0];
        Add("motorTorque_turbo", "MotorTorqueForward,MotorTorqueBackward,MotorTorqueTurboForward,MotorTorqueTurboBackward", 0, 100000);
        Add("velocityMax_turbo", "VelocityMaxForward,VelocityMaxBackward,VelocityMaxTurboForward,VelocityMaxTurboBackward", 0, 100);
        foreach(string name in new[]{"brakeTorque","steerRate","steerCenteringRate","steerAngleMax","upAngleMax","upForce","tiltAngleMax","tiltThreshold","tiltDampening","tiltDampenThreshold","tiltUpForce","unstickForce"})
            Add(name,name,0,name=="brakeTorque"?100000:1000);
        if(entity.vehicleRB!=null)
        {
            AddMember("Rigidbody.mass",entity.vehicleRB,"mass",1,100000);
            AddMember("Rigidbody.drag",entity.vehicleRB,"drag",0,100);
            AddMember("Rigidbody.angularDrag",entity.vehicleRB,"angularDrag",0,100);
            AddMember("Rigidbody.centerOfMass",entity.vehicleRB,"centerOfMass",-20,20);
            AddMember("Rigidbody.useGravity",entity.vehicleRB,"useGravity",0,1);
        }
        Add("wheelPtlScale","WheelPtlScale",0,100);
        Add("airDrag_velScale_angVelScale","AirDragVelScale,AirDragAngVelScale",0,100);
        Add("waterDrag_y_velScale_velMaxScale","WaterDragY,WaterDragVelScale,WaterDragVelMaxScale",-1000,1000);
        Add("waterLift_y_depth_force","WaterLiftY,WaterLiftDepth,WaterLiftForce",-1000,1000);
        AddMember("cameraDistance",native,"cameraDistance",.01f,100);
        AddMember("cameraTurnRate",native,"cameraTurnRate",.0001f,100);
        AddMember("hopForce",native,"hopForce",-100,100);
        var parts=entity.vehicle.vehicleParts;
        if(parts!=null)foreach(var part in parts)if(part is VPEngine engine)
        {
            AddMember("engine/fuelKmPerL",engine,"fuelKmPerL",.0001f,10000);
            AddFields("engine/foodDrain",engine,"foodDrain,foodDrainTurbo",0,100);
        }
        if(entity.wheels!=null)for(int i=0;i<entity.wheels.Length;i++)
        {
            var wheel=entity.wheels[i];if(wheel?.wheelC==null)continue;
            foreach(string key in new[]{"radius","mass","suspensionDistance","forceAppPointDistance","center"})
                AddMember("runtime.wheel"+i+"."+key,wheel.wheelC,key,key=="center" ? -20 : (key=="radius"||key=="mass") ? 0.001f : 0,key=="mass"?500:100);
            AddSpring(i,wheel.wheelC);
        }
        AddResolved(entity.vehicle.Properties,"");
        BuildControls();
    }
    private void AddResolved(DynamicProperties properties,string prefix)
    {
        if(properties==null)return;
        foreach(var pair in properties.Values.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            string key=prefix+pair.Key;
            bool editable=Simple(pair.Value);
            Settings.Add(new Setting{Name=key+(editable?Localization.Get("xuiRbVehicleTune3"):Localization.Get("xuiRbVehicleTune4")),Xml=key,Original=pair.Value,Value=pair.Value,ReadOnly=!editable,ResolvedXml=true,Min=-1000000,Max=1000000});
        }
        foreach(var pair in properties.Classes.OrderBy(p=>p.Key,StringComparer.Ordinal))AddResolved(pair.Value,prefix+pair.Key+"/");
    }
    internal bool Valid => GamePrefs.GetBool(EnumGamePrefs.DebugMenuEnabled)&&Entity!=null&&Entity.world!=null&&RuntimeTargetsCurrent()&&!Entity.isEntityRemote&&ReferenceEquals(native,Entity.vehicle)&&!Entity.IsDead()&&ReferenceEquals(GameManager.Instance?.World,Entity.world)&&ReferenceEquals(Entity.world.GetEntity(Entity.entityId),Entity)&&!Entity.world.IsRemote();
    private bool RuntimeTargetsCurrent()
    {
        if(!ReferenceEquals(Entity.vehicleRB,capturedBody)||(!ReferenceEquals(capturedBody,null)&&capturedBody==null))return false;
        if((Entity.wheels?.Length??0)!=capturedWheels.Length)return false;
        for(int i=0;i<capturedWheels.Length;i++)if(!ReferenceEquals(Entity.wheels[i]?.wheelC,capturedWheels[i])||(!ReferenceEquals(capturedWheels[i],null)&&capturedWheels[i]==null))return false;
        int engine=0;
        if(Entity.vehicle?.vehicleParts!=null)foreach(var part in Entity.vehicle.vehicleParts)if(part is VPEngine)
        {if(engine>=capturedEngines.Length||!ReferenceEquals(part,capturedEngines[engine++]))return false;}
        return engine==capturedEngines.Length;
    }
    private static MemberInfo Member(object target,string name) => (MemberInfo)target.GetType().GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)??target.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.Instance);
    private static object Read(object target,MemberInfo m) => m is FieldInfo f?f.GetValue(target):((PropertyInfo)m).GetValue(target,null);
    private static void Write(object target,MemberInfo m,object value){if(m is FieldInfo f)f.SetValue(target,value);else ((PropertyInfo)m).SetValue(target,value,null);}
    private static Type Kind(MemberInfo m)=>m is FieldInfo f?f.FieldType:((PropertyInfo)m).PropertyType;
    private static bool Writable(MemberInfo m)=>m is FieldInfo f?!f.IsInitOnly:m is PropertyInfo p&&p.CanWrite;
    private static string Format(object v) => v is Vector2 p2?F(p2.x)+", "+F(p2.y):v is Vector3 p?F(p.x)+", "+F(p.y)+", "+F(p.z):Convert.ToString(v,CultureInfo.InvariantCulture);
    private static string F(float v)=>v.ToString("R",CultureInfo.InvariantCulture);
    private static float[] Numbers(string text,float min,float max)
    {
        var values=text.Split(',').Select(s=>float.Parse(s.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture)).ToArray();
        if(values.Any(v=>float.IsNaN(v)||float.IsInfinity(v)||v<min||v>max))throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,Localization.Get("xuiRbVehicleTuneBounds"),min,max));
        return values;
    }
    private static bool Simple(string value){if(bool.TryParse(value,out _))return true;try{return Numbers(value,-1000000,1000000).Length<=4;}catch{return false;}}
    private void Add(string xml,string names,float min,float max) => AddFields(xml,native,names,min,max);
    private void AddFields(string xml,object target,string names,float min,float max)
    {
        var members=names.Split(',').Select(n=>Member(target,n)).ToArray();
        if(members.Any(m=>m==null||!Writable(m)||Kind(m)!=typeof(float)))return;
        var row=new Setting{Name=xml+Localization.Get("xuiRbVehicleTune1"),Xml=xml,Min=min,Max=max};
        row.Read=()=>string.Join(", ",members.Select(m=>Format(Read(target,m))));
        row.Original=row.Value=row.Read();
        var originals=members.Select(m=>Read(target,m)).ToArray();row.Restore=()=>{for(int i=0;i<members.Length;i++)Write(target,members[i],originals[i]);};
        row.Write=text=>{var values=Numbers(text,min,max);if(values.Length!=members.Length)throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,Localization.Get("xuiRbVehicleTuneComponents"),members.Length));for(int i=0;i<members.Length;i++)Write(target,members[i],values[i]);};
        Settings.Add(row);
    }
    private void AddMember(string name,object target,string key,float min,float max)
    {
        var member=Member(target,key);if(member==null||!Writable(member))return;var type=Kind(member);
        if(type!=typeof(float)&&type!=typeof(Vector3)&&type!=typeof(Vector2)&&type!=typeof(bool))return;
        var row=new Setting{Name=name+Localization.Get("xuiRbVehicleTune2"),Xml=name,Min=min,Max=max,Read=()=>Format(Read(target,member))};
        row.Original=row.Value=row.Read();var original=Read(target,member);row.Restore=()=>Write(target,member,original);row.Write=text=>{object value;if(type==typeof(bool))value=bool.Parse(text);else{var numbers=Numbers(text,min,max);int n=type==typeof(Vector3)?3:type==typeof(Vector2)?2:1;if(numbers.Length!=n)throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,Localization.Get("xuiRbVehicleTuneCount"),n));value=n==3?(object)new Vector3(numbers[0],numbers[1],numbers[2]):n==2?(object)new Vector2(numbers[0],numbers[1]):numbers[0];}Write(target,member,value);};Settings.Add(row);
    }
    private void AddSpring(int index,WheelCollider wheel)
    {
        var row=new Setting{Name="runtime.wheel"+index+".suspensionSpring [live spring, damper, target]",Xml="runtime.wheel"+index+".suspensionSpring",Min=0,Max=1000000};
        row.Read=()=>{var spring=wheel.suspensionSpring;return F(spring.spring)+", "+F(spring.damper)+", "+F(spring.targetPosition);};
        row.Original=row.Value=row.Read();var original=wheel.suspensionSpring;row.Restore=()=>wheel.suspensionSpring=original;
        row.Write=text=>{var values=Numbers(text,0,1000000);if(values.Length!=3||values[2]>1)throw new ArgumentException(Localization.Get("xuiRbVehicleTune24"));var spring=wheel.suspensionSpring;spring.spring=values[0];spring.damper=values[1];spring.targetPosition=values[2];wheel.suspensionSpring=spring;};Settings.Add(row);
    }
    internal void ReapplyTunedValues()
    {
        // Native vehicle updates can restore values from the vehicle definition while driving.
        // Keep only settings the player changed in this debug session at their chosen values.
        foreach(var row in Settings)
            if(row.Tuned&&row.Write!=null&&row.Read()!=row.Value)row.Write(row.Value);
    }
    internal void Apply(int index,string text)
    {
        if(!Valid)throw new InvalidOperationException(Localization.Get("xuiRbVehicleTune6"));
        var row=Settings[index];if(row.ReadOnly)throw new InvalidOperationException(Localization.Get("xuiRbVehicleTune7"));if(row.Write!=null){row.Write(text);row.Value=row.Read();row.Tuned=true;}
        else
        {
            ValidateProposal(row.Original,text);row.Value=text;
        }
    }
    private static void ValidateProposal(string originalText,string text)
    {
            if(bool.TryParse(originalText,out _)){if(!bool.TryParse(text,out _))throw new ArgumentException(Localization.Get("xuiRbVehicleTune8"));}
            else {var original=Numbers(originalText,-1000000,1000000);var proposed=Numbers(text,-1000000,1000000);if(original.Length!=proposed.Length)throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,Localization.Get("xuiRbVehicleTuneProposalCount"),original.Length));}
    }
    internal void Reset(){if(!Valid)throw new InvalidOperationException(Localization.Get("xuiRbVehicleTune9"));foreach(var row in Settings){row.Restore?.Invoke();row.Value=row.Original;row.Tuned=false;}accelerationFactor=1f;ResetStorageProposal();}
    internal void Export()
    {
        if(!Valid)throw new InvalidOperationException(Localization.Get("xuiRbVehicleTune10"));
        var report=new StringBuilder("[REBIRTH VehicleTuning] BEGIN entity=").Append(Entity.entityId).Append(" vehicleXml=").Append(Entity.vehicle.GetName()).Append(" entityClass=").Append(Entity.entityClass).Append(" world=").Append(Entity.world.worldState.Guid).Append(" position=").Append(Format(Entity.position)).AppendLine();
        foreach(var row in Settings)report.Append(row.ResolvedXml?(row.Value==row.Original?"RESOLVED_XML ":"PROPOSED_XML "):"LIVE_RUNTIME ").Append(row.Xml).Append(" = ").Append(row.Value).Append(" | original=").Append(row.Original).AppendLine();
        report.Append("TUNING_MULTIPLIER acceleration=").Append(F(accelerationFactor)).AppendLine(" | original=1");
        ExportStorage(report);
        if(RebirthVehicleAssemblyIndex.TryGetEntity(Entity,out var assembly)&&assembly?.Performance!=null)
            foreach(var field in assembly.Performance.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance))report.Append("RESTORATION ").Append(field.Name).Append(" = ").Append(Format(field.GetValue(assembly.Performance))).AppendLine();
        if(Entity.wheels!=null)for(int i=0;i<Entity.wheels.Length;i++){var w=Entity.wheels[i];if(w?.wheelC==null)continue;report.Append("WHEEL ").Append(i).Append(" radius=").Append(F(w.wheelC.radius)).Append(" motorScale=").Append(F(w.motorTorqueScale)).Append(" brakeScale=").Append(F(w.brakeTorqueScale)).Append(" damping=").Append(F(w.wheelC.wheelDampingRate)).Append(" sideStiffness=").Append(F(w.wheelC.sidewaysFriction.stiffness)).AppendLine();}
        if(string.Equals(Entity.vehicle.GetName(),"rebirthCamper80s",StringComparison.OrdinalIgnoreCase))report.AppendLine("CAMPER_NORMALIZATION torque/brake=originalScale*radius/.63; damping=.7; sideways stiffness front=1.8 rear=1.7.");
        report.AppendLine("Live values are baselines/runtime settings, not final effective physics. Restoration, native effects, perks and direct assists still apply. SOURCE proposals are NOT applied live or saved. Integrate source explicitly.").Append("[REBIRTH VehicleTuning] END");Log.Out(report.ToString());
    }
}

#endif
