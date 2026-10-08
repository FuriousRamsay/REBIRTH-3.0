using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// New Theory authoring only: seconds remain native facts; no practical XP enters this model.
internal sealed class RebirthTheorySoloLockpickRelevance
{
    internal readonly float NativeReference,TheoryReference,Maximum;
    private RebirthTheorySoloLockpickRelevance(float seconds,float theory,float maximum){NativeReference=seconds;TheoryReference=theory;Maximum=maximum;}
    internal static bool TryReadPolicy(XElement policy,out RebirthTheorySoloLockpickRelevance model)
    {
        model=null;if(policy==null)return false;
        var names=new[]{"lockpick_reference_native_seconds","lockpick_reference_theory_difficulty","lockpick_maximum_theory_difficulty"};
        int supplied=names.Count(n=>policy.Attribute(n)!=null);
        if(supplied==0){model=new RebirthTheorySoloLockpickRelevance(15,50,75);return true;} // Existing schema-1 compatibility.
        if(supplied!=3)return false;
        var values=new float[3];for(int i=0;i<3;i++)if(!float.TryParse((string)policy.Attribute(names[i]),NumberStyles.Float,CultureInfo.InvariantCulture,out values[i])||!Finite(values[i]))return false;
        if(values[0]<.25f||values[0]>3600||values[1]<=0||values[1]>100||values[2]<values[1]||values[2]>100)return false;
        model=new RebirthTheorySoloLockpickRelevance(values[0],values[1],values[2]);return true;
    }
    internal bool TryDifficulty(float nativeSeconds,out float difficulty)
    {
        difficulty=0;if(!Finite(nativeSeconds)||nativeSeconds<=0)return false;
        double scaled=TheoryReference*Math.Sqrt((double)nativeSeconds/NativeReference);
        if(double.IsNaN(scaled)||double.IsInfinity(scaled))return false;
        difficulty=(float)Math.Min(Maximum,scaled);return true;
    }
    private static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
}