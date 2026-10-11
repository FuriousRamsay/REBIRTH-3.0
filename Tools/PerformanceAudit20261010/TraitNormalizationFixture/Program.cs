public sealed class RebirthTraitSupportEffectDefinition
{
    public string Target { get; private set; }
    public string Operation { get; private set; }
    public float Value { get; private set; }
    public string Scope { get; private set; }
    public string State { get; private set; }
    public string Note { get; private set; }
    internal string NormalizedTarget { get; private set; }
    internal string NormalizedState { get; private set; }
    public RebirthTraitSupportEffectDefinition(string target,string operation,float value,string scope,string state,string note)
    { Target=target??string.Empty;Operation=operation??string.Empty;Value=value;Scope=scope??string.Empty;State=state??string.Empty;Note=note??string.Empty;NormalizedTarget=Target.Trim().ToLowerInvariant();NormalizedState=State.Trim().ToLowerInvariant(); }
}


class Program { static void Main(){
string[] values={null,"","equipped"," UNSATISFIED ","Managed","positive","ARMED"," Energy.Use ","\u0130","\u0131","\u017f","\u212a","\u03a3","\u2000Mood.Target\u2000"};
foreach(var x in values)foreach(var y in values){var d=new RebirthTraitSupportEffectDefinition(x,"add",1,"universal",y,"");if(d.NormalizedTarget!=(x??"").Trim().ToLowerInvariant()||d.NormalizedState!=(y??"").Trim().ToLowerInvariant()||d.Target!=(x??"")||d.State!=(y??""))throw new Exception("normalization changed");}
var effect=new RebirthTraitSupportEffectDefinition(" Energy.Use ","add",1,""," MANAGED ","");
long Measure(bool old){long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++){string a=old?effect.Target.Trim().ToLowerInvariant():effect.NormalizedTarget;string b=old?effect.State.Trim().ToLowerInvariant():effect.NormalizedState;if(a!="energy.use"||b!="managed")throw new Exception();}return GC.GetAllocatedBytesForCurrentThread()-before;}
Console.WriteLine($"PASS {values.Length*values.Length} exact normalization/original-field cases; 10000 reads bytes old={Measure(true)},new={Measure(false)}. Extracted production definition; no native FPS claim.");}}
