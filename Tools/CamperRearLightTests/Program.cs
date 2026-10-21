int count=0;
void Check(bool pass,string label){if(!pass)throw new Exception(label);count++;}
Check(RebirthCamperLampIntensity.Calculate(false,false,-1,false)==.65f,"Native camper reverse lens unchanged");
Check(RebirthCamperLampIntensity.Calculate(false,true,1,false)==.65f,"Brake lens unchanged");
Check(RebirthCamperLampIntensity.Calculate(false,false,1,true)==.12f,"Tail lens unchanged");
Check(RebirthCamperLampIntensity.Calculate(false,false,1,false)==0,"Unlit forward stays dark");
Check(RebirthCamperLampIntensity.Marker(true)==.08f,"Marker unchanged");
Check(RebirthCamperLampIntensity.PickupRed(false,false,-1,false)==0,"Reverse does not turn pickup red segments on");
Check(RebirthCamperLampIntensity.PickupReverse(false,-1)==.65f,"Pickup clear reverse lens unchanged");
foreach(float velocity in new[]{0f,1f,-.05f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
    Check(!RebirthCamperLampIntensity.Reversing(false,velocity),"Non-reverse/invalid velocity refuses boost");
Check(RebirthCamperLampIntensity.Reversing(false,-.051f),"Real reverse admits boost");
Check(!RebirthCamperLampIntensity.Reversing(true,-1),"Broken headlight part refuses boost");
Check(.65f*RebirthCamperLampIntensity.ReverseBeamMultiplier==3.25f,"Fivefold beam strength");
Check(RebirthCamperLampIntensity.ReverseBeamRange==6,"Six metre cone");
Check(RebirthCamperLampIntensity.ReverseBeamAngle==60,"Modestly wider sixty-degree cone");
Console.WriteLine("PASS "+count+" production rear-light policy checks; native rendered brightness requires night test.");
