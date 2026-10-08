using System;
public static class Test
{
// PRODUCTION_CLASS
    static void Check(bool value){if(!value)throw new Exception("Fracture predicate failed");}
    public static void Main()
    {
        foreach(string item in new[]{"medicalSplint","medicalPlasterCast"})
        {
            Check(IsNewFractureTreatment(item,true,false,false,false,true,false));
            Check(IsNewFractureTreatment(item,false,true,false,false,false,true));
            Check(!IsNewFractureTreatment(item,true,false,true,false,true,false));
            Check(!IsNewFractureTreatment(item,true,false,false,false,false,true));
            Check(!IsNewFractureTreatment(item,false,true,false,false,true,false));
            Check(!IsNewFractureTreatment(item,false,false,false,false,true,true));
            Check(!IsNewFractureTreatment(item,true,true,false,false,false,false));
        }
        Check(!IsNewFractureTreatment("medicalBandage",true,false,false,false,true,false));
        Check(!IsNewFractureTreatment(null,true,false,false,false,true,false));
        Console.WriteLine("PASS actual fracture Insight predicate: exact limb, unchanged, sprain-only, failed, other item");
    }
}
