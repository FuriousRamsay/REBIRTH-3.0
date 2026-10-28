// NOT RUN. Actual registry Register/TryGet/DefinitionIdentity bodies injected by
// the runner. Native thread and config-file loading are explicit doubles.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
class Recipe {internal bool IsScrap;internal string craftingArea="campfire";internal string GetName()=>"meal";}
static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
// PRODUCTION_OUTPUT_TYPES
static class RebirthRecipeOutputs
{
    static readonly Dictionary<string,RebirthRecipeOutputDefinition> definitions=new Dictionary<string,RebirthRecipeOutputDefinition>(StringComparer.OrdinalIgnoreCase);
    static void EnsureLoaded(){} // File loading outside this fixture's scope.
    static void Issue(string issue){}
    static string Key(string name,string area)=>(name??string.Empty)+"#"+(area??string.Empty);
    // PRODUCTION_OUTPUT_METHODS
    static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
    public static void Main()
    {
        var recipe=new Recipe();
        Need(DefinitionIdentity(recipe)=="none","Absent output definition changed");
        Register(XElement.Parse("<recipe name='meal' craft_area='campfire'><output name='jar' count='1'/><output name='can' count='2'/></recipe>"));
        string original=DefinitionIdentity(recipe);
        Need(original!="none"&&TryGet(recipe,out var definition)&&definition.Outputs.Count==2,"Actual XML registration not reflected in identity");
        Register(XElement.Parse("<recipe name='meal' craft_area='campfire'><output name='jar' count='1'/><output name='can' count='2'/></recipe>"));
        Need(DefinitionIdentity(recipe)==original,"Unchanged XML changed identity");
        foreach(string xml in new[]{
            "<recipe name='meal' craft_area='campfire'><output name='jar' count='2'/><output name='can' count='2'/></recipe>",
            "<recipe name='meal' craft_area='campfire'><output name='bottle' count='1'/><output name='can' count='2'/></recipe>",
            "<recipe name='meal' craft_area='campfire'><output name='can' count='2'/><output name='jar' count='1'/></recipe>"})
        {Register(XElement.Parse(xml));Need(DefinitionIdentity(recipe)!=original,"Output XML change not bound");}
        Register(XElement.Parse("<recipe name='meal' craft_area='campfire' disable='true'/>"));
        Need(DefinitionIdentity(recipe)=="none","Disabled output did not remove definition");
        ThreadManager.Main=false;bool refused=false;
        try{DefinitionIdentity(recipe);}catch(InvalidOperationException){refused=true;}finally{ThreadManager.Main=true;}
        Need(refused,"Unavailable catalogue silently treated as no output");
    }
}
