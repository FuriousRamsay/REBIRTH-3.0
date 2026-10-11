using System;using System.Collections.Generic;using System.Linq;using System.Xml.Linq;
public class XmlFile{public XDocument XmlDoc;}
public static class Localization{public static Dictionary<string,string[]> Dictionary=new Dictionary<string,string[]>();public static bool Exists(string key)=>Dictionary.ContainsKey(key);}
public static class TEFeatureStorage{public const string PropLootList="LootList";}
public static class TEFeatureRebirthContainerName{public const string FeatureName="TEFeatureRebirthContainerName";}
public static class RebirthContainerSizeService{public const string PropertyContainerSize="RebirthContainerSize",PropertyLegacyContainerSize="ContainerSize",PropertyLootContainerSize="LootContainerSize";}
public static class RebirthBlockPickupClassifier{public const string PropertyVanillaCanPickup="RebirthVanillaCanPickup";}
public static class RebirthBlockPickupTargetResolver{public const string PropertyVanillaPickupTarget="RebirthVanillaPickupTarget";}
public static class RebirthBlockPickupLegacyTargets{public static bool TryGetTarget(string source,out string target){target=null;return false;}}
public static class RebirthLogSettings{public static bool BlockPickupLoggingEnabled=false;}
public static class Log{public static void Out(string s){}public static void Warning(string s){throw new Exception(s);}}
class Program {
 static void Main(string[] args) {
  var source=XDocument.Load(args[0]);
  var baseline=new XmlFile{XmlDoc=new XDocument(source)};var current=new XmlFile{XmlDoc=new XDocument(source)};
  void Seed(){Localization.Dictionary.Clear();Localization.Dictionary["xuiStorage"]=new[]{"Storage"};foreach(var b in source.Root.Elements("block")){var n=(string)b.Attribute("name");Localization.Dictionary[n]=new[]{n.Replace("Empty","")};}}
  Seed();var sw=System.Diagnostics.Stopwatch.StartNew();long alloc=GC.GetAllocatedBytesForCurrentThread();BaselineGenerator.Prepare(baseline);sw.Stop();long before=GC.GetAllocatedBytesForCurrentThread()-alloc;var names=Localization.Dictionary.ToDictionary(p=>p.Key,p=>p.Value);Console.WriteLine($"Baseline: {sw.ElapsedMilliseconds} ms; {before} allocated bytes");
  Seed();sw.Restart();alloc=GC.GetAllocatedBytesForCurrentThread();RebirthBlockPickupEmptyVariantGenerator.Prepare(current);sw.Stop();long after=GC.GetAllocatedBytesForCurrentThread()-alloc;Console.WriteLine($"Indexed: {sw.ElapsedMilliseconds} ms; {after} allocated bytes");
  if(!XNode.DeepEquals(baseline.XmlDoc,current.XmlDoc))throw new Exception("Generated XML differs");
  if(names.Count!=Localization.Dictionary.Count||names.Any(p=>!Localization.Dictionary.TryGetValue(p.Key,out var n)||!n.SequenceEqual(p.Value)))throw new Exception("Localization differs");
  BaselineGenerator.Prepare(baseline);RebirthBlockPickupEmptyVariantGenerator.Prepare(current);
  if(!XNode.DeepEquals(baseline.XmlDoc,current.XmlDoc))throw new Exception("Second prepare differs");
  Console.WriteLine("PASS exact XML/localization equivalence and repeated generation; allocation reduction "+(100d*(before-after)/before).ToString("0.0")+"%");
 }
}