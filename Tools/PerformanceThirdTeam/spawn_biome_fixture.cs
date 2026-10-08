using System;
using System.Collections.Generic;
public enum RebirthSpawnCategory { A,B,C }
public enum RebirthSpawnProgressionMode { Biome,Gamestage }
public sealed class RebirthSpawnContext {public int Surface,GameStage;public string Biome;public RebirthSpawnProgressionMode ProgressionMode;}
public sealed class RebirthEntityRule {public int Id,MinGs,MaxGs;public bool Registered;public RebirthSpawnCategory Category;public HashSet<int> Contexts=new HashSet<int>();public HashSet<string> Biomes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);}
public sealed class RebirthCompiledSpawnData {public List<RebirthEntityRule> Entities=new List<RebirthEntityRule>();}
// PRODUCTION_CLASSES
public static class SpawnBiomeChecks {
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 public static void Run(){var random=new Random(71006);string[] biomes={null,""," FOREST ","Pine Forest","BURNT forest","Desert"," snow ","unknown"};int count=0;
 for(int mode=0;mode<2;mode++)for(int surface=0;surface<2;surface++)foreach(string biome in biomes)for(int sample=0;sample<20;sample++){
  var data=new RebirthCompiledSpawnData();for(int i=0;i<100;i++){var e=new RebirthEntityRule{Id=i%9==0?-i:i,Registered=i%7!=0,Category=(RebirthSpawnCategory)(i%3),MinGs=i%40,MaxGs=30+i%70};e.Contexts.Add(i%2);if(i%3!=0)e.Biomes.Add(i%2==0?"forest":"burnt");data.Entities.Add(e);}
  if(sample==0)data.Entities.Clear();var context=new RebirthSpawnContext{Surface=surface,ProgressionMode=(RebirthSpawnProgressionMode)mode,Biome=biome,GameStage=random.Next(100)};
  Before.NormalizeCalls=After.NormalizeCalls=0;var a=Before.Run(context,data);var b=After.Run(context,data);Check(a.Count==b.Count,"category count");
  foreach(var pair in a){List<RebirthEntityRule> found;Check(b.TryGetValue(pair.Key,out found),"category identity");Check(pair.Value.Count==found.Count,"candidate count");for(int i=0;i<found.Count;i++)Check(Object.ReferenceEquals(pair.Value[i],found[i]),"candidate order");}
  Check(After.NormalizeCalls==(Before.NormalizeCalls>0?1:0),"lazy normalization count");count++;
 }
 Check(count==640,"coverage count");
 }
}
