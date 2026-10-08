using System;
class Program
{
    static int checks;
    static void Equal(float actual,float expected,string label){checks++;if(Math.Abs(actual-expected)>.000001)throw new Exception(label+": "+actual+" != "+expected);}
    static void Main()
    {
        float[] levels={0,10,20,30,50,75,99};
        string[] med={"medicalBandage","medicalFirstAidBandage","medicalFirstAidKit","medicalAloeCream","medicalSplint","medicalPlasterCast","drugHerbalAntibiotics","drugAntibiotics","drugSteroids","drugVitamins","drugPainkillers","foodHoney"};
        float[][] treatment={new[]{.010f,.0075f,.005f,.0025f,0,0,0},new[]{.030f,.027f,.024f,.021f,.015f,.0075f,.003f},new[]{.100f,.090f,.080f,.070f,.050f,.025f,.010f},new[]{.010f,.009f,.008f,.007f,.005f,.0025f,.001f},new[]{.020f,.0167f,.0133f,.010f,.0033f,0,0},new[]{.050f,.045f,.040f,.035f,.025f,.0125f,.005f},new[]{.020f,.018f,.016f,.014f,.010f,.005f,.002f},new[]{.050f,.045f,.040f,.035f,.025f,.0125f,.005f},new[]{.010f,.008f,.006f,.004f,0,0,0},new[]{.005f,.0033f,.0017f,0,0,0,0},new[]{.040f,.036f,.032f,.028f,.020f,.010f,.004f},new[]{.010f,.009f,.008f,.007f,.005f,.0025f,.001f}};
        for(int i=0;i<med.Length;i++)for(int j=0;j<levels.Length;j++)Equal(RebirthDifficultyPractice.Treatment(med[i],levels[j]),treatment[i][j],med[i]+" at "+levels[j]);
        string[] craft={"medicalBandage","medicalFirstAidBandage","medicalFirstAidKit","medicalAloeCream","medicalSplint","medicalPlasterCast","drugHerbalAntibiotics","drugAntibiotics","drugSteroids","drugFortBites","drugRecog"};
        float[][] expected={new[]{.015f,.015f,.0075f,0,0,0,0},new[]{.040f,.040f,.040f,.0267f,0,0,0},new[]{.120f,.120f,.120f,.120f,.096f,.036f,.024f},new[]{.010f,.0075f,.0025f,0,0,0,0},new[]{.020f,.020f,.0133f,.0067f,0,0,0},new[]{.055f,.055f,.055f,.055f,.0275f,0,0},new[]{.060f,.060f,.060f,.060f,.0343f,0,0},new[]{.140f,.140f,.140f,.140f,.140f,.070f,.035f},new[]{.100f,.100f,.100f,.100f,.100f,.0375f,.015f},new[]{.080f,.080f,.080f,.080f,.070f,.020f,.008f},new[]{.090f,.090f,.090f,.090f,.0788f,.0225f,.009f}};
        for(int i=0;i<craft.Length;i++)for(int j=0;j<levels.Length;j++){if(!RebirthDifficultyPractice.TryCraft(craft[i],"skill.medicine",levels[j],1,out float gain))throw new Exception("Missing curve");Equal(gain,expected[i][j],craft[i]);}
        Equal(RebirthDifficultyPractice.Treatment("drugFortBites",0),0,"Performance only");Equal(RebirthDifficultyPractice.Treatment("drugRecog",0),0,"Performance only");Equal(RebirthDifficultyPractice.Treatment("medicalBloodBag",0),0,"Unverified");
        Equal(RebirthDifficultyPractice.Treatment("medicalFirstAidKit",5),.095f,"Interpolation");
        RebirthDifficultyPractice.TryCraft("ammoGasCan","skill.chemistry",30,1,out float one);RebirthDifficultyPractice.TryCraft("ammoGasCan","skill.chemistry",30,100,out float batch);
        Equal(one,.001f,"Gas process at chemistry 30");Equal(batch,one,"Output packaging must not multiply gas practice");
        RebirthDifficultyPractice.TryCraft("ammoGasCan","skill.chemistry",0,100,out float noviceGas);Equal(noviceGas,.002f,"Novice gas process");
        RebirthDifficultyPractice.TryCraft("ammoGasCan","skill.chemistry",75,100,out float expertGas);Equal(expertGas,0,"Gas trivializes at chemistry 75");
        RebirthDifficultyPractice.TryCraft("resourceGlue","skill.chemistry",75,1,out float glue);Equal(glue,0,"Trivial chemistry");
        for(int i=1;i<=6;i++)for(int level=0;level<=100;level++)
        {float v=RebirthDifficultyPractice.Cooking(level,i,60);checks++;if(v<0||v>.12f)throw new Exception("Cooking bound");if(level>0 && v>RebirthDifficultyPractice.Cooking(level-1,i,60)+.000001f)throw new Exception("Non-monotonic cooking");}
        if(RebirthDifficultyPractice.Cooking(0,5,120)<=RebirthDifficultyPractice.Cooking(0,1,10))throw new Exception("Cooking difficulty order");
        Equal(RebirthDifficultyPractice.Cooking(99,1,10),0,"Simple cooking trivializes");
        Console.WriteLine(checks+" progression checks passed.");
    }
}
