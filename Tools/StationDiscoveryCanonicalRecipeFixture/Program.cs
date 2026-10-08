using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Reflection;using System.Xml.Linq;
public class ItemClass{public static Dictionary<int,ItemClass> Registry=new();public string Name;public static ItemClass GetForId(int id)=>Registry.TryGetValue(id,out var item)?item:null;public string GetItemName()=>Name;}
public class ItemValue{public int type;public Dictionary<string,object> Metadata=new();public bool IsEmpty()=>type==0;public bool HasMetadata(string key)=>Metadata.ContainsKey(key);public bool TryGetMetadata<T>(string key,out T value){value=default;if(!Metadata.TryGetValue(key,out var o)||o is not T v)return false;value=v;return true;}}
public class ItemStack{public ItemValue itemValue;public int count;}
public class Recipe{public List<ItemStack> ingredients=new();public int itemValueType=10,count=1,craftExpGain=5,craftingToolType;public string craftingArea="campfire",tags="",Variant="bone",wildcardForgeCategory="",wildcardCampfireCategory="",FakeName;public bool IsScrap,materialBasedRecipe,UseIngredientModifier;public float craftingTime=1;
 public string GetName()=>FakeName??ItemClass.GetForId(itemValueType)?.GetItemName()??"";
 public void Write(BinaryWriter w){w.Write(itemValueType);w.Write(count);w.Write(craftExpGain);w.Write(craftingArea);w.Write(Variant);}}
class FixtureWriter:BinaryWriter{public FixtureWriter():base(new MemoryStream()){}public void SetBaseStream(Stream s){OutStream=s;}}
class Pool{public FixtureWriter AllocSync(bool _)=>new();}
static class MemoryPools{public static Pool poolBinaryWriter=new();}
public class EntityPlayer{}
static class Log{public static void Error(string _) {}}
static class RebirthKnowledgeService{public static bool HasKnowledge(EntityPlayer p,string s)=>false;}
static class RebirthLiteratureService{public static string RecipeReadMarker(string s)=>"read."+s;}
class RebirthRecipeKnowledgeRule{public string RecipeName="resourceGlue",KnowledgeId="recipe.glue";}
static class RebirthProgressionRuntimeConfig{public static RebirthRecipeKnowledgeRule Rule=new();public static bool TryGetRecipeRule(string name,out RebirthRecipeKnowledgeRule rule){rule=Rule;return rule!=null&&StringComparer.OrdinalIgnoreCase.Equals(rule.RecipeName,name);}}
static class RebirthCraftingProgressionRegistry{public static bool IsReady=true;public static RebirthCraftingProgressionDefinition Policy;
 public static bool TryGetRecipe(string n,out RebirthCraftingProgressionDefinition p){p=Policy;return p!=null&&StringComparer.OrdinalIgnoreCase.Equals(p.RecipeId,n);}}
class Program{
 static int tests;static void C(bool v,string name){tests++;if(!v)throw new Exception(name);}
 static string Job=Guid.NewGuid().ToString("N");
 static Recipe Source(string variant="bone")=>new(){Variant=variant,ingredients=new(){new(){itemValue=new(){type=1},count=1}}};
 static Recipe Queued(Recipe source){var key=(string)typeof(RebirthStationGridQueue).GetMethod("DefinitionKey",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{source});
 var item=new ItemValue{type=1};var p=RebirthStationGridQueue.Prefix;item.Metadata[p+"version"]=1;item.Metadata[p+"definition"]=key;item.Metadata[p+"batches"]=1;item.Metadata[p+"tier"]=0;item.Metadata[p+"job"]=Job;
 return new(){Variant=source.Variant,itemValueType=source.itemValueType,count=source.count,craftExpGain=source.craftExpGain,craftingArea=source.craftingArea,ingredients=new(){new(){itemValue=item,count=1}}};}
 static void Policy(string mode="gated",string knowledge="recipe.glue",string implementation="existing_capability",string id="resourceGlue")=>RebirthCraftingProgressionRegistry.Policy=new(id,mode,"cap.glue","skill.cooking","cook",implementation,"fixture","authored","approved",new[]{knowledge});
 static string config=Path.Combine(AppContext.BaseDirectory,"Config","_CraftingDiscovery","recipe_policy.xml");
 static void Reading(string name=null,string knowledge="recipe.glue",bool absent=false){Directory.CreateDirectory(Path.GetDirectoryName(config));if(absent){if(File.Exists(config))File.Delete(config);}else new XElement("recipeDiscoveryPolicy",new XAttribute("version",1),name==null?null:new XElement("recipe",new XAttribute("name",name),new XAttribute("requires_read",knowledge))).Save(config);
 var flags=BindingFlags.Static|BindingFlags.NonPublic;typeof(RebirthRecipeDiscoveryRules).GetField("loaded",flags).SetValue(null,false);typeof(RebirthRecipeDiscoveryRules).GetField("policy",flags).SetValue(null,null);}
 static void Reset(){ItemClass.Registry.Clear();ItemClass.Registry[10]=new(){Name="resourceGlue"};RebirthProgressionRuntimeConfig.Rule=new();RebirthCraftingProgressionRegistry.IsReady=true;Policy();Reading();}
 static bool Resolve(Recipe q,List<Recipe> defs,out RebirthStationDiscoveryCanonicalRecipe.Resolution r)=>RebirthStationDiscoveryCanonicalRecipe.TryResolve(q,defs,out r);
 static void Main(){Reset();var source=Source();var queued=Queued(source);var defs=new List<Recipe>{source};
 C(Resolve(queued,defs,out var result)&&result.CanonicalRecipe=="resourceGlue"&&result.KnowledgeId=="recipe.glue"&&result.JobId==Job,"canonical actual source binding");
 C(result.Matches("resourceGlue",result.DefinitionId)&&!result.Matches("RESOURCEGLUE",result.DefinitionId)&&!result.Matches("resourceGlue",new string('A',64)),"exact frozen name definition predicate");
 var second=Source("corn");var other=Queued(second);C(Resolve(other,new(){source,second},out var another)&&another.CanonicalRecipe==result.CanonicalRecipe&&another.DefinitionId!=result.DefinitionId,"same WHAT distinct paid ingredient definitions");
 queued.FakeName="CLIENT_ALIAS";C(Resolve(queued,defs,out another)&&another.CanonicalRecipe=="resourceGlue","client GetName ignored");queued.FakeName=null;
 C(!Resolve(Source(),defs,out another)&&another==null,"unmarked arbitrary native type refuses");
 C(!Resolve(queued,new(){second},out another),"changed source definition refuses");C(!Resolve(queued,new(){source,source},out another),"duplicate exact definitions refuse");
 foreach(var mode in new[]{"universal","disabled"}){Policy(mode);C(!Resolve(queued,defs,out another),mode+" policy refused");}Policy(implementation:"planned_capability");C(!Resolve(queued,defs,out another),"unsupported planned implementation");Policy();
 RebirthCraftingProgressionRegistry.IsReady=false;C(!Resolve(queued,defs,out another),"unready");RebirthCraftingProgressionRegistry.IsReady=true;
 Policy(knowledge:"other.knowledge");C(!Resolve(queued,defs,out another),"policy rule knowledge mismatch");Policy(id:"RESOURCEGLUE");C(!Resolve(queued,defs,out another),"case-mismatched authored policy refused");Policy();
 RebirthProgressionRuntimeConfig.Rule.RecipeName="RESOURCEGLUE";C(Resolve(queued,defs,out another)&&another.CanonicalRecipe=="resourceGlue","case compatible rule returns native spelling");RebirthProgressionRuntimeConfig.Rule.RecipeName="resourceGlue";
 RebirthProgressionRuntimeConfig.Rule.KnowledgeId="";C(!Resolve(queued,defs,out another),"empty knowledge");RebirthProgressionRuntimeConfig.Rule=null;C(!Resolve(queued,defs,out another),"external missing rule");RebirthProgressionRuntimeConfig.Rule=new();
 Reading("resourceGlue");C(Resolve(queued,defs,out another),"optional matching reading mapping classification");C(!RebirthRecipeDiscoveryRules.Allows(new EntityPlayer(),"resourceGlue"),"classification proves no read marker");
 Reading("resourceGlue","wrong.knowledge");C(!Resolve(queued,defs,out another),"optional wrong reading mapping refused");Reading(absent:true);C(!Resolve(queued,defs,out another),"unavailable reading policy fail closed");Reading();
 ItemClass.Registry[10].Name="";C(!Resolve(queued,defs,out another),"empty canonical native name");ItemClass.Registry.Clear();C(!Resolve(queued,defs,out another),"unknown native ID");ItemClass.Registry[10]=new(){Name="resourceGlue"};source.FakeName="foreign";C(!Resolve(queued,defs,out another),"source versus native registry name mismatch");source.FakeName=null;
 var empty=RebirthRecipeDiscoveryPolicy.Read(new XElement("recipeDiscoveryPolicy",new XAttribute("version",1)));C(!empty.TryGetRequiredReading(null,out var knowledge)&&knowledge=="","actual optional policy null outempty");C(!empty.TryGetRequiredReading("resourceGlue",out knowledge)&&knowledge=="","actual optional empty policy");
 Console.WriteLine($"PASS {tests} actual canonical resolver + extracted actual queue authority methods + actual policy/rules; native registry/recipe serialization/config registry adapters, no payment/read/terminal/grant authority.");}
}