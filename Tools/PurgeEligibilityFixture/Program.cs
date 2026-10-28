using System;
using System.IO;
class Log { public static void Warning(string text) { Console.WriteLine(text); } }
class Program {
 static int count;
 static void Check(bool ok,string label) { if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label); }
 static void Main() {
  RebirthPurgePoiEligibility.Load(Directory.GetCurrentDirectory());
  Check(RebirthPurgePoiEligibility.Available,"authored policy loads");
  Check(!RebirthPurgePoiEligibility.Allows("zztong_underground_01",false,false,false),"Purge exclusion");
  Check(!RebirthPurgePoiEligibility.Allows(" CF_TRADER_HUB ",false,false,false),"general exclusion, case and whitespace");
  Check(RebirthPurgePoiEligibility.Allows("gas_station_06",false,false,false),"commented legacy exclusion stays eligible");
  Check(RebirthPurgePoiEligibility.Allows("house",false,true,false),"rwgonly alone allowed");
  Check(RebirthPurgePoiEligibility.Allows("house",false,false,true),"streettile alone allowed");
  Check(!RebirthPurgePoiEligibility.Allows("house",false,true,true),"combined streettile/rwgonly excluded");
  Check(!RebirthPurgePoiEligibility.Allows("house",true,false,false),"trader excluded");
  Check(RebirthPurgePoiEligibility.Allows("house",false,false,false),"normal authored POI eligible regardless of runtime load state");
  Check(!RebirthPurgePoiEligibility.Allows(null,false,false,false),"missing identity withheld");
  Console.WriteLine("RESULT "+count+" PASS; production policy with real configuration; no game runtime claim.");
 }
}
