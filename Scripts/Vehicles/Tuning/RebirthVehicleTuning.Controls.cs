#if REBIRTH_DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

internal sealed partial class RebirthVehicleTuningSession
{
    internal const int SpeedCategory=0,SteeringCategory=1,BrakesCategory=2,SuspensionCategory=3,StabilityCategory=4,StorageCategory=5,AdvancedCategory=6;
    internal readonly List<Control> Controls=new List<Control>();
    private float accelerationFactor=1f;
    internal float AccelerationPercent=>accelerationFactor*100f;
    internal string DisplayName=>Localization.Get(nativeName);
    internal float ForwardLimitKmh=>Entity.vehicle.VelocityMaxForward*3.6f;
    internal sealed class Control
    {
        internal RebirthVehicleTuningSession Owner;
        internal Setting Setting;
        internal int Source=-1,Component,Category,OrderHint;
        internal string LabelKey,HelpKey,Technical,CapturedText,Unit="",Icon="rb_tuner_advanced";
        internal float Minimum,Maximum,Scale=1f,OriginalNumber;
        internal bool Numeric=true,Boolean,Acceleration,SourceProposal;
        internal Func<float> ReadNumberOverride;
        internal Action<float> WriteNumberOverride;
        internal Func<string> ReadTextOverride;
        internal bool Editable=>Acceleration||WriteNumberOverride!=null||Setting!=null&&!Setting.ReadOnly;
        internal bool Proposal=>Setting?.ResolvedXml==true||SourceProposal;
        internal string Label=>LabelKey==null?Technical:Localization.Get(LabelKey);
        internal string Description=>HelpKey==null?Technical:Localization.Get(HelpKey);
        internal string Help=>Proposal?Localization.Get("xuiRbTuneProposalShort"):!Editable?Localization.Get("xuiRbTuneReadOnlyShort"):Description;
        internal string DetailHelp=>Proposal?Localization.Get("xuiRbTuneProposal")+" "+Description:!Editable?Localization.Get("xuiRbTuneReadOnly")+" "+Description:Description;
        internal string Raw=>ReadTextOverride!=null?ReadTextOverride():Setting==null?"":Setting.Tuned||Setting.Read==null?Setting.Value:Setting.Read();
        internal float Number
        {
            get
            {
                if(Acceleration)return Owner.AccelerationPercent;
                if(ReadNumberOverride!=null)return ReadNumberOverride();
                if(Boolean)return bool.Parse(Raw)?1f:0f;
                return float.Parse(Raw.Split(',')[Component].Trim(),CultureInfo.InvariantCulture)*Scale;
            }
        }
        internal string Text=>Numeric?(Boolean?Localization.Get(Number>=.5f?"xuiRbTuneOn":"xuiRbTuneOff"):Number.ToString("G7",CultureInfo.InvariantCulture)):Raw;
        internal string OriginalText=>Numeric?(Boolean?Localization.Get(OriginalNumber>=.5f?"xuiRbTuneOn":"xuiRbTuneOff"):OriginalNumber.ToString("G7",CultureInfo.InvariantCulture)):CapturedText??Setting?.Original??"";
        internal float Normalized=>Maximum<=Minimum?0:Mathf.Clamp01((Number-Minimum)/(Maximum-Minimum));
        internal float OriginalNormalized=>Maximum<=Minimum?0:Mathf.Clamp01((OriginalNumber-Minimum)/(Maximum-Minimum));
        internal void ApplyText(string text)
        {
            if(!Numeric)throw new InvalidOperationException(Localization.Get("xuiRbTuneReadOnly"));
            float value;
            if(Boolean)
            {
                bool boolean;
                if(bool.TryParse(text,out boolean))value=boolean?1:0;
                else if(string.Equals(text,Localization.Get("xuiRbTuneOn"),StringComparison.OrdinalIgnoreCase))value=1;
                else if(string.Equals(text,Localization.Get("xuiRbTuneOff"),StringComparison.OrdinalIgnoreCase))value=0;
                else value=float.Parse(text,NumberStyles.Float,CultureInfo.InvariantCulture);
            }
            else value=float.Parse(text,NumberStyles.Float,CultureInfo.InvariantCulture);
            Apply(value);
        }
        internal void Apply(float value)
        {
            if(!Owner.Valid)throw new InvalidOperationException(Localization.Get("xuiRbVehicleTune6"));
            if(!Editable)throw new InvalidOperationException(Localization.Get("xuiRbTuneReadOnly"));
            if(float.IsNaN(value)||float.IsInfinity(value)||value<Minimum||value>Maximum)
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,Localization.Get("xuiRbVehicleTuneBounds"),Minimum,Maximum));
            if(Acceleration){Owner.SetAcceleration(value/100f);return;}
            if(WriteNumberOverride!=null){WriteNumberOverride(value);return;}
            if(Boolean){Owner.Apply(Source,value>=.5f?"true":"false");return;}
            var values=Raw.Split(',');values[Component]=(value/Scale).ToString("R",CultureInfo.InvariantCulture);
            Owner.Apply(Source,string.Join(", ",values));
        }
    }
    private void SetAcceleration(float factor)
    {
        if(factor<=0||float.IsNaN(factor)||float.IsInfinity(factor))throw new ArgumentException(Localization.Get("xuiRbTunePositive"));
        int index=Settings.FindIndex(s=>s.Xml=="motorTorque_turbo"&&!s.ResolvedXml);
        if(index<0)throw new InvalidOperationException(Localization.Get("xuiRbTuneUnavailable"));
        var row=Settings[index];var current=Numbers(row.Tuned?row.Value:row.Read(),0,100000);
        // Scale current components, preserving independently tuned advanced torque ratios.
        float ratio=factor/accelerationFactor;
        for(int i=0;i<current.Length;i++)current[i]*=ratio;
        string value=string.Join(", ",current.Select(F));
        Apply(index,value);accelerationFactor=factor;
    }
    private void BuildControls()
    {
        for(int index=0;index<Settings.Count;index++)
        {
            var setting=Settings[index];bool boolean=bool.TryParse(setting.Original,out _);float[] numbers=null;
            if(!boolean)try{numbers=Numbers(setting.Original,-1000000,1000000);}catch{}
            int count=boolean||numbers==null?1:numbers.Length;
            for(int component=0;component<count;component++)
            {
                var row=new Control{Owner=this,Setting=setting,Source=index,Component=component,Category=AdvancedCategory,
                    Technical=setting.Xml+(count>1?" ["+(component+1)+"]":""),Minimum=setting.Min,Maximum=setting.Max,
                    Numeric=boolean||numbers!=null,Boolean=boolean,OriginalNumber=boolean?(bool.Parse(setting.Original)?1:0):numbers==null?0:numbers[component]};
                if(boolean){row.Minimum=0;row.Maximum=1;}
                Configure(row);
                Controls.Add(row);
            }
        }
        if(Settings.Any(s=>s.Xml=="motorTorque_turbo"&&!s.ResolvedXml))Controls.Add(new Control{Owner=this,Acceleration=true,Category=SpeedCategory,LabelKey="xuiRbTuneAcceleration",HelpKey="xuiRbTuneAccelerationHelp",Unit="%",Minimum=1,Maximum=500,OriginalNumber=100,Icon="rb_tuner_acceleration"});
        BuildStorageControls();
        Controls.Sort((a,b)=>Order(a).CompareTo(Order(b)));
    }
    private static int Order(Control c)
    {
        if(c.Category!=SpeedCategory)return c.Category*100000+(c.Source<0?c.OrderHint:c.Source*10+c.Component);
        if(c.Acceleration)return 3;
        return c.Setting?.Xml=="velocityMax_turbo"?(c.Component==0?0:c.Component==2?1:c.Component==1?2:4):100+c.Source;
    }
    private static void Named(Control row,int category,string key,string unit="",string icon=null)
    {row.Category=category;row.LabelKey="xuiRbTune"+key;row.HelpKey=row.LabelKey+"Help";row.Unit=unit;row.Icon=icon??new[]{"rb_tuner_speed","rb_tuner_steering","rb_tuner_brakes","rb_tuner_suspension","rb_tuner_stability","rb_tuner_storage","rb_tuner_advanced"}[category];}
    private void Configure(Control c)
    {
        if(c.Setting.ResolvedXml){c.HelpKey=c.Numeric?"xuiRbTuneSourceHelp":"xuiRbTuneResolvedHelp";return;}
        string key=c.Setting.Xml;
        if(key=="velocityMax_turbo")
        {
            Named(c,c.Component==3?AdvancedCategory:SpeedCategory,new[]{"ForwardSpeed","ReverseSpeed","BoostedSpeed","BoostedReverseSpeed"}[c.Component],"km/h",c.Component==0?"rb_tuner_speed":c.Component==2?"rb_tuner_boost":"rb_tuner_reverse");
            c.Scale=3.6f;c.OriginalNumber*=3.6f;c.Minimum=1;c.Maximum=360;return;
        }
        if(key=="motorTorque_turbo")
        {
            Named(c,AdvancedCategory,new[]{"ForwardTorque","ReverseTorque","BoostedTorque","BoostedReverseTorque"}[c.Component]);
            c.Minimum=c.OriginalNumber==0?0:.001f;c.Maximum=100000;return;
        }
        switch(key)
        {
            case "steerRate":Named(c,SteeringCategory,"SteeringResponse");c.Maximum=360;break;
            case "steerCenteringRate":Named(c,SteeringCategory,"SteeringReturn");c.Maximum=360;break;
            case "steerAngleMax":Named(c,SteeringCategory,"SteeringAngle","°");c.Minimum=1;c.Maximum=60;break;
            case "brakeTorque":Named(c,BrakesCategory,"BrakeStrength", "", "rb_tuner_brakes");c.Maximum=20000;break;
            case "Rigidbody.mass":Named(c,StabilityCategory,"BodyMass","kg");c.Maximum=10000;break;
            case "Rigidbody.drag":Named(c,StabilityCategory,"BodyDrag");c.Maximum=10;break;
            case "Rigidbody.angularDrag":Named(c,StabilityCategory,"RotationDrag");c.Maximum=10;break;
            case "Rigidbody.centerOfMass":Named(c,StabilityCategory,"MassCenter"+new[]{"X","Y","Z"}[c.Component],"m");c.Minimum=-2;c.Maximum=2;break;
            case "Rigidbody.useGravity":Named(c,StabilityCategory,"Gravity");break;
            case "upAngleMax":Named(c,StabilityCategory,"UprightAngle","°");c.Maximum=90;break;
            case "upForce":Named(c,StabilityCategory,"UprightForce");c.Maximum=20;break;
            case "tiltAngleMax":Named(c,StabilityCategory,"LeanAngle","°");c.Maximum=90;break;
            case "tiltThreshold":Named(c,StabilityCategory,"LeanThreshold");c.Maximum=90;break;
            case "tiltDampening":Named(c,StabilityCategory,"LeanDamping","%");c.Scale=100;c.OriginalNumber*=100;c.Maximum=95;break;
            case "tiltDampenThreshold":Named(c,StabilityCategory,"LeanDampingThreshold");c.Maximum=90;break;
            case "tiltUpForce":Named(c,StabilityCategory,"LeanRecovery");c.Maximum=20;break;
            case "unstickForce":Named(c,StabilityCategory,"UnstickForce");c.Maximum=5;break;
            case "cameraDistance":Named(c,AdvancedCategory,c.Component==0?"CameraDistanceStopped":"CameraDistanceMoving","m");c.Minimum=.5f;c.Maximum=20;break;
            case "cameraTurnRate":Named(c,AdvancedCategory,c.Component==0?"CameraHorizontal":"CameraVertical");c.Minimum=.01f;c.Maximum=5;break;
            case "hopForce":Named(c,AdvancedCategory,c.Component==0?"HopStrength":"HopPosition",c.Component==0?"m/s":"m");c.Minimum=c.Component==0?0:-5;c.Maximum=c.Component==0?10:5;break;
            case "wheelPtlScale":Named(c,AdvancedCategory,"WheelParticles");c.Maximum=5;break;
            case "engine/fuelKmPerL":Named(c,AdvancedCategory,"FuelEfficiency","km/L");break;
            case "engine/foodDrain":Named(c,AdvancedCategory,c.Component==0?"FoodDrain":"BoostedFoodDrain");c.Maximum=1;break;
            case "airDrag_velScale_angVelScale":Named(c,StabilityCategory,c.Component==0?"AirSpeedRetention":"AirRotationRetention");c.Maximum=1;break;
            case "waterDrag_y_velScale_velMaxScale":Named(c,AdvancedCategory,new[]{"WaterHeight","WaterSpeedRetention","WaterSpeedLimit"}[c.Component]);if(c.Component==1){c.Minimum=0;c.Maximum=1;}else if(c.Component==2){c.Minimum=0;c.Maximum=1;}break;
            case "waterLift_y_depth_force":Named(c,AdvancedCategory,new[]{"WaterLiftHeight","WaterLiftDepth","WaterLiftForce"}[c.Component]);break;
        }
        if(key.StartsWith("runtime.wheel",StringComparison.Ordinal))
        {
            int dot=key.IndexOf('.',"runtime.wheel".Length);string suffix=dot<0?key:key.Substring(dot+1);int wheel;
            string number=dot<0?"0":key.Substring("runtime.wheel".Length,dot-"runtime.wheel".Length);int.TryParse(number,out wheel);
            string label;
            switch(suffix)
            {
                case "radius":label="WheelRadius";c.Minimum=.05f;c.Maximum=2;break;
                case "mass":label="WheelMass";c.Minimum=1;c.Maximum=500;break;
                case "suspensionDistance":label="SuspensionTravel";c.Maximum=1.5f;break;
                case "forceAppPointDistance":label="WheelForceHeight";c.Maximum=2;break;
                case "center":label="WheelCenter"+new[]{"X","Y","Z"}[c.Component];c.Minimum=-2;c.Maximum=2;break;
                case "suspensionSpring":label=new[]{"SpringStrength","SpringDamping","SpringRest"}[c.Component];if(c.Component==0)c.Maximum=200000;if(c.Component==1)c.Maximum=20000;if(c.Component==2){c.Scale=100;c.OriginalNumber*=100;c.Maximum=100;c.Unit="%";}break;
                default:label="WheelSetting";break;
            }
            string unit=suffix=="mass"?"kg":suffix=="suspensionSpring"?(c.Component==2?"%":""):"m";
            Named(c,SuspensionCategory,label,unit);c.Technical=string.Format(CultureInfo.InvariantCulture,Localization.Get("xuiRbTuneWheelFormat"),wheel+1,Localization.Get(c.LabelKey));
            c.LabelKey=null;
        }
    }
}
#endif
