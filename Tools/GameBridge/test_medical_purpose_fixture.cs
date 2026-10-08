using System;
public static class Test
{
// PRODUCTION_CLASS
    static void Check(bool value){if(!value)throw new Exception("Medical purpose mismatch");}
    public static void Main()
    {
        foreach(string item in new[]{"medicalAloeCream","medicalFirstAidKit","medicalFirstAidBandage","drugPainkillers"})
            Check(Purpose(item)=="Restore health");
        Check(HealthUnit("medicalAloeCream")==10&&HealthUnit("medicalFirstAidKit")==100);
        Check(Purpose("medicalBandage")=="Stop a new bleed");
        Check(Purpose("medicalPlasterCast")=="Treat an untreated fracture");
        Console.WriteLine("PASS actual medical purpose: health text excludes practice-normalization numbers; practice units preserved");
    }
}
