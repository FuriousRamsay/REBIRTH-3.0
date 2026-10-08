using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// New Theory authoring only: maximum health remains a native fact; no practical XP enters this model.
internal sealed class RebirthTheorySoloCombatRelevance
{
    internal readonly float NativeReference,TheoryReference,Maximum;
    private RebirthTheorySoloCombatRelevance(float seconds,float theory,float maximum){NativeReference=seconds;TheoryReference=theory;Maximum=maximum;}
    internal static bool TryReadPolicy(XElement policy,out RebirthTheorySoloCombatRelevance model)
    {
        model=null;if(policy==null)return false;
        var names=new[]{"combat_reference_native_max_health","combat_reference_theory_difficulty","combat_maximum_theory_difficulty"};
        int supplied=names.Count(n=>policy.Attribute(n)!=null);
        if(supplied==0){model=new RebirthTheorySoloCombatRelevance(300,50,75);return true;} // Existing schema-1 compatibility.
        if(supplied!=3)return false;
        var values=new float[3];for(int i=0;i<3;i++)if(!float.TryParse((string)policy.Attribute(names[i]),NumberStyles.Float,CultureInfo.InvariantCulture,out values[i])||!Finite(values[i]))return false;
        if(values[0]<1||values[0]>1000000||values[1]<=0||values[1]>100||values[2]<values[1]||values[2]>100)return false;
        model=new RebirthTheorySoloCombatRelevance(values[0],values[1],values[2]);return true;
    }
    internal bool TryDifficulty(float nativeMaxHealth,out float difficulty)
    {
        difficulty=0;if(!Finite(nativeMaxHealth)||nativeMaxHealth<=0)return false;
        double scaled=TheoryReference*Math.Sqrt((double)nativeMaxHealth/NativeReference);
        if(double.IsNaN(scaled)||double.IsInfinity(scaled))return false;
        difficulty=(float)Math.Min(Maximum,scaled);return true;
    }
    private static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
}