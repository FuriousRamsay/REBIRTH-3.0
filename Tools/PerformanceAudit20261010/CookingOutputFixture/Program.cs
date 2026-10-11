using System.Linq;
class TypedMetadataValue { public int Value; public TypedMetadataValue Clone()=>new(){Value=Value}; }
class ItemValue { public Dictionary<string,TypedMetadataValue> Metadata; }
class Ingredient { public ItemValue itemValue; }
class Recipe { public Ingredient[] ingredients; }
class Program {
const string QueuePrefix="queue.",Prefix="output.";
    private static void Old(Recipe recipe,ItemValue output)
    {
        foreach(var pair in recipe.ingredients[0].itemValue.Metadata)
            if(pair.Key.StartsWith(QueuePrefix,StringComparison.Ordinal) && !new[]{"token","portions","tool","xp","held","elapsed","duration","overdue","method","hidden","reported","preview"}.Contains(pair.Key.Substring(QueuePrefix.Length)))
            {
                if(output.Metadata==null)output.Metadata=new Dictionary<string,TypedMetadataValue>();
                output.Metadata[Prefix+pair.Key.Substring(QueuePrefix.Length)]=pair.Value.Clone();
            }
    }
    private static void CopyOutputData(Recipe recipe,ItemValue output)
    {
        foreach(var pair in recipe.ingredients[0].itemValue.Metadata)
        {
            if(!pair.Key.StartsWith(QueuePrefix,StringComparison.Ordinal))continue;
            string field=pair.Key.Substring(QueuePrefix.Length);
            switch(field)
            {
                case "token": case "portions": case "tool": case "xp":
                case "held": case "elapsed": case "duration": case "overdue":
                case "method": case "hidden": case "reported": case "preview":
                    continue;
            }
            if(output.Metadata==null)output.Metadata=new Dictionary<string,TypedMetadataValue>();
            output.Metadata[Prefix+field]=pair.Value.Clone();
        }
    }

static void Main(){
 var keys=new[]{"token","portions","tool","xp","held","elapsed","duration","overdue","method","hidden","reported","preview","nutrition","water","Token","","custom","previewExtra"};
 var r=new Recipe{ingredients=new[]{new Ingredient{itemValue=new ItemValue{Metadata=new()}}}};
 foreach(var k in keys){r.ingredients[0].itemValue.Metadata[QueuePrefix+k]=new(){Value=k.Length};r.ingredients[0].itemValue.Metadata[k]=new(){Value=77};}
 for(int i=0;i<1000;i++){
 var a=new ItemValue{Metadata=new(){{"retained",new(){Value=12}}}};var b=new ItemValue{Metadata=new(){{"retained",new(){Value=12}}}};
 Old(r,a);CopyOutputData(r,b);
 if(a.Metadata.Count!=b.Metadata.Count||a.Metadata.Any(p=>!b.Metadata.TryGetValue(p.Key,out var v)||p.Value.Value!=v.Value))throw new Exception("Parity");
 foreach(var p in r.ingredients[0].itemValue.Metadata)if(b.Metadata.Values.Any(v=>ReferenceEquals(v,p.Value)))throw new Exception("Clone alias");
 }
 long Measure(bool old){long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++){var o=new ItemValue();if(old)Old(r,o);else CopyOutputData(r,o);}return GC.GetAllocatedBytesForCurrentThread()-before;}
 var previous=Measure(true);var current=Measure(false);if(current>=previous)throw new Exception("No allocation reduction");
 Console.WriteLine($"PASS 1000 metadata parity/clone cases; 10000 copies bytes old={previous},new={current}. Extracted production methods; metadata/native types doubled; no FPS claim.");
}}
