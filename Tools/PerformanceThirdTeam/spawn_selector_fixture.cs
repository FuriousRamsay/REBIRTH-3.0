using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
public sealed class StubTags {public bool Forbidden;public bool Test_AnySet(object unused){return Forbidden;}}
public sealed class EntityClass {
 public string entityClassName;public StubTags Tags=new StubTags();
 public static readonly Dictionary<int,EntityClass> Registry=new Dictionary<int,EntityClass>();
 public static EntityClass GetEntityClass(int id){EntityClass value;return Registry.TryGetValue(id,out value)?value:null;}
}
// MODELS
// HELPERS
// CLASSES
// SCRATCH_CHECKS
public static class SelectorChecks {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static Dictionary<string,Queue<int>> History(){return new Dictionary<string,Queue<int>>(StringComparer.Ordinal){{"fixture",new Queue<int>(new[]{3,4,5})}};}
 static string HistoryText(Dictionary<string,Queue<int>> h){var text=new StringBuilder();foreach(var p in h){text.Append(p.Key).Append(':');foreach(int x in p.Value)text.Append(x).Append(',');text.Append(';');}return text.ToString();}
 static string TraceText(bool ok,RebirthSpawnTrace t){return ok+"|"+t.EntityClassId+"|"+t.EntityName+"|"+t.Category+"|"+t.Text;}
 static RebirthCompiledSpawnData Data(int seed,bool oversize){
  var random=new Random(seed);var data=new RebirthCompiledSpawnData(new[]{"preserved"});data.SleeperExact.Add("exact");double[] numbers={0,-1,1,2,1e-300,1e300,double.MaxValue,double.NaN,double.PositiveInfinity,double.NegativeInfinity};EntityClass.Registry.Clear();
  int count=oversize?300:random.Next(0,65);
  for(int i=0;i<count;i++){
   var category=oversize?RebirthSpawnCategory.RegularLow:(RebirthSpawnCategory)(i%4);var e=new RebirthEntityRule{Name=i%11==0?"screamer"+i:"managed"+i,Id=i,Category=category,MinGs=i%20,MaxGs=50+i%30,EntityWeight=oversize?1:numbers[random.Next(numbers.Length)],Contexts=new HashSet<RebirthSpawnSurface>(),Biomes=new HashSet<string>(StringComparer.OrdinalIgnoreCase),ThemeTags=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"a"}};
   foreach(RebirthSpawnSurface surface in Enum.GetValues(typeof(RebirthSpawnSurface)))if(oversize||i%3!=0||surface==RebirthSpawnSurface.Biome)e.Contexts.Add(surface);
   if(!oversize&&i%3!=0)e.Biomes.Add(i%2==0?"forest":"burnt");data.Entities.Add(e);
   if(oversize||i%13!=0)EntityClass.Registry[i]=new EntityClass{entityClassName=e.Name,Tags=new StubTags{Forbidden=!oversize&&i%17==0}};
   if(!data.Categories.ContainsKey(category)){double w=oversize?1:numbers[random.Next(numbers.Length)];var rule=new RebirthCategoryRule{Category=category};rule.GameStageCurve.Add(new RebirthWeightPoint{X=0,Weight=w});rule.GameStageCurve.Add(new RebirthWeightPoint{X=100,Weight=w});rule.BiomeWeights["forest"]=w;rule.BiomeWeights["burnt"]=w;data.Categories[category]=rule;}
  }
  var prefab=new RebirthPrefabTheme{Prefab="fixture"};prefab.Themes.Add(new KeyValuePair<string,double>("valid",1));prefab.Themes.Add(new KeyValuePair<string,double>("missing",1));data.Prefabs["fixture"]=prefab;var theme=new RebirthTheme{Name="valid",General=1};theme.TagMultipliers["a"]=2;data.Themes["valid"]=theme;return data;
 }
 static void Compare(RebirthCompiledSpawnData data,RebirthSpawnContext context,bool detailed,double first,double second){
  Before.Configure(data);After.Configure(data);var oldHistory=History();var newHistory=History();int oldCalls=0,newCalls=0;RebirthSpawnTrace a,b;
  bool oldOk=Before.Run(context,()=>{oldCalls++;return oldCalls==1?first:second;},oldHistory,detailed,out a);bool newOk=After.Run(context,()=>{newCalls++;return newCalls==1?first:second;},newHistory,detailed,out b);
  Check(TraceText(oldOk,a)==TraceText(newOk,b),"whole trace/identity");Check(oldCalls==newCalls,"RNG call count");Check(HistoryText(oldHistory)==HistoryText(newHistory),"history state");
 }
 public static void Run(){
  double[] rolls={0,.5,1,-1,double.NaN,double.PositiveInfinity};string[] biomes={null," Pine Forest ","BURNT forest","unknown"};
  for(int test=0;test<256;test++){var data=Data(test,false);for(int mode=0;mode<2;mode++)for(int detail=0;detail<2;detail++){var context=new RebirthSpawnContext{Surface=(RebirthSpawnSurface)(test%4+1),ProgressionMode=(RebirthSpawnProgressionMode)mode,GameStage=test%101,Biome=biomes[test%4],PrefabName=test%3==0?"unknown":"FIXTURE",HistoryKey="fixture",RequestedGroup=test%19==0?"exact":"normal"};Compare(data,context,detail==1,rolls[test%6],rolls[(test+1)%6]);}}
  var large=Data(999,true);var ready=new RebirthSpawnContext{Surface=RebirthSpawnSurface.Biome,ProgressionMode=RebirthSpawnProgressionMode.Biome,Biome="forest",PrefabName="fixture",HistoryKey="fixture"};Compare(large,ready,true,.4,.8);Compare(null,ready,true,0,0);Compare(large,null,true,0,0);
  var nestedData=Data(1001,true);nestedData.Entities.RemoveRange(64,nestedData.Entities.Count-64);Before.Configure(nestedData);After.Configure(nestedData);var ah=History();var bh=History();RebirthSpawnTrace a,b;int ac=0,bc=0;string anested=null,bnested=null;
  bool aok=Before.Run(ready,()=>{ac++;if(ac==2){RebirthSpawnTrace t;bool k=Before.Run(ready,()=>{ac++;return .2;},ah,true,out t);anested=TraceText(k,t);}return .6;},ah,true,out a);
  bool bok=After.Run(ready,()=>{bc++;if(bc==2){RebirthSpawnTrace t;bool k=After.Run(ready,()=>{bc++;return .2;},bh,true,out t);bnested=TraceText(k,t);}return .6;},bh,true,out b);
  Check(aok==bok&&TraceText(aok,a)==TraceText(bok,b)&&anested==bnested&&ac==bc&&HistoryText(ah)==HistoryText(bh),"nested selection/RNG/history/trace");
  for(int throwCall=1;throwCall<=2;throwCall++){ac=bc=0;bool athrew=false,bthrew=false;ah=History();bh=History();try{Before.Run(ready,()=>{if(++ac==throwCall)throw new InvalidOperationException();return .4;},ah,true,out a);}catch(InvalidOperationException){athrew=true;}try{After.Run(ready,()=>{if(++bc==throwCall)throw new InvalidOperationException();return .4;},bh,true,out b);}catch(InvalidOperationException){bthrew=true;}Check(athrew&&bthrew&&ac==bc&&HistoryText(ah)==HistoryText(bh),"RNG exception parity");Compare(large,ready,true,.4,.6);}
  using(var selection=RebirthSpawnSelectionScratchLease.Acquire(4)){selection.EntityWeights.Add(13);List<double> first;using(var theme=RebirthThemeScratchLease.Acquire(4)){first=theme.Multipliers;theme.Multipliers.Add(3);}using(var theme=RebirthThemeScratchLease.Acquire(4)){Check(Object.ReferenceEquals(first,theme.Multipliers)&&theme.Multipliers.Count==0,"theme domain remains reusable under selection lease");}Check(selection.EntityWeights[0]==13,"theme domain leaves selection intact");}
 }
}
