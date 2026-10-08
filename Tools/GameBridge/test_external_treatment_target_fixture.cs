using System;
// PRODUCTION_CLASS
public static class Test
{
    static void Check(bool value,string label){if(!value)throw new Exception(label);}
    public static void Main()
    {
        foreach(string item in new[]{"medicalBandage","medicalFirstAidBandage","medicalFirstAidKit","medicalAloeCream","medicalSplint","medicalPlasterCast","resourceSewingKit"})
            Check(RebirthExternalTreatmentTargetPolicy.IsExternalTreatment(item),item);
        foreach(string item in new[]{null,"","drugVitamins","drugPainkillers","drugAntibiotics","foodHoney"})
            Check(!RebirthExternalTreatmentTargetPolicy.IsExternalTreatment(item),"oral excluded");
        Check(RebirthExternalTreatmentTargetPolicy.Allows(true,true,true,true,false,16f),"native boundary");
        Check(RebirthExternalTreatmentTargetPolicy.Allows(true,true,true,true,false,0f),"near patient");
        foreach(float d in new[]{16.001f,-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            Check(!RebirthExternalTreatmentTargetPolicy.Allows(true,true,true,true,false,d),"bad distance");
        Check(!RebirthExternalTreatmentTargetPolicy.Allows(false,true,true,true,false,1f),"dead actor");
        Check(!RebirthExternalTreatmentTargetPolicy.Allows(true,false,true,true,false,1f),"dead patient");
        Check(!RebirthExternalTreatmentTargetPolicy.Allows(true,true,false,true,false,1f),"nonplayer");
        Check(!RebirthExternalTreatmentTargetPolicy.Allows(true,true,true,false,false,1f),"world changed");
        Check(!RebirthExternalTreatmentTargetPolicy.Allows(true,true,true,true,true,1f),"self target");
        Console.WriteLine("PASS actual external treatment classification and target policy");
    }
}
