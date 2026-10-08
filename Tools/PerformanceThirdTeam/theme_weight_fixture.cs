using System;
using System.Collections.Generic;
using System.Threading;
public enum RebirthSpawnSurface { A,B }
// MODELS
// HELPER
// CLASSES
// SCRATCH_CHECKS
public static class ThemeWeightChecks {
 static void Equal(RebirthCompiledSpawnData data,string prefab,RebirthEntityRule entity){double a=Before.Run(data,prefab,entity),b=After.Run(data,prefab,entity);if(BitConverter.DoubleToInt64Bits(a)!=BitConverter.DoubleToInt64Bits(b))throw new Exception("numerical bits differ");}
 public static void Run(){
 var entity=new RebirthEntityRule{ThemeTags=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"a","b"}};
 var data=new RebirthCompiledSpawnData(new string[0]);Equal(null,null,entity);Equal(data,"missing",entity);Equal(data,"",entity);
 var throwingData=new RebirthCompiledSpawnData(new string[0]);var throwPrefab=new RebirthPrefabTheme();throwPrefab.Themes.Add(new KeyValuePair<string,double>("t",1));throwingData.Prefabs["throw"] = throwPrefab;throwingData.Themes["t"] = new RebirthTheme();bool oldThrow=false,newThrow=false;try{Before.Run(throwingData,"throw",new RebirthEntityRule());}catch(NullReferenceException){oldThrow=true;}try{After.Run(throwingData,"throw",new RebirthEntityRule());}catch(NullReferenceException){newThrow=true;}if(!oldThrow||!newThrow)throw new Exception("null tag exception differs");Equal(throwingData,"throw",entity);
 double[] values={0,-1,1,2,1e-300,1e300,double.MaxValue,double.NaN,double.PositiveInfinity,double.NegativeInfinity};var random=new Random(71006);
 for(int test=0;test<1000;test++){
  data=new RebirthCompiledSpawnData(new string[0]);var prefab=new RebirthPrefabTheme();data.Prefabs["fixture"]=prefab;int count=test==0?300:random.Next(20);
  for(int i=0;i<count;i++){string key="theme"+i;prefab.Themes.Add(new KeyValuePair<string,double>(key,values[random.Next(values.Length)]));if(i%7==0)continue;var theme=new RebirthTheme{General=values[random.Next(values.Length)]};theme.TagMultipliers["a"]=values[random.Next(values.Length)];theme.TagMultipliers["b"]=values[random.Next(values.Length)];data.Themes[key]=theme;}
  Equal(data,"FIXTURE",entity);Equal(data,"fixture",entity);Equal(data,"missing",entity);
 }
 }
}
