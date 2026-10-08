using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Xml.Linq;
public class RebirthStationGridAdmission{public string JobId,CreationId,DefinitionId;public XElement Write()=>throw new Exception("admission authority not exercised");}
class RebirthStablePlayerIdentity{public readonly string StorageKey;public RebirthStablePlayerIdentity(string key){StorageKey=key;}}
class Origin{public string CreationId;}
class Progression{public Dictionary<string,RebirthStationRecipeDiscoveryRecord> RecipeDiscoveries;}
class SavedRecord{public Origin Origin;public Progression Progression;}
partial class RebirthWorldCharacterRepository{
 internal static bool serverAuthority;internal static string CurrentPath;internal static bool Migrated,ThrowRead;internal static Action OnRead;internal static int Reads,PathCalls;
 static object gate=new();static object GetWriteLock(string key)=>gate;
 static string GetPath(string key){PathCalls++;return CurrentPath;}
 static bool TryLoadValidatedRecord(string path,RebirthStablePlayerIdentity identity,out SavedRecord saved,out bool migrated,out string error,out bool reserved){
 Reads++;saved=null;migrated=Migrated;error=null;reserved=false;
 if(ThrowRead)throw new IOException("readfailure");if(!File.Exists(path))return false;
 var root=XElement.Load(path);if((string)root.Attribute("owner")!=identity.StorageKey)return false;
 if(!RebirthStationRecipeDiscoveryPersistence.TryRead(root.Element("progression"),identity.StorageKey,out var records,out error))return false;
 saved=new(){Origin=new(){CreationId=(string)root.Attribute("creation")},Progression=new(){RecipeDiscoveries=records}};
 OnRead?.Invoke();return true;
 }
}
class Program{
 static int n;static void C(bool value,string name){n++;if(!value)throw new Exception(name);}
 static string owner=new string('a',64),job=Guid.NewGuid().ToString("N");
 static RebirthStationRecipeDiscoveryRecord Create(string creation){var w=new XElement("stationDiscoveryWitness",new XAttribute("version",1),new XAttribute("job",job),new XAttribute("creation",creation),new XAttribute("owner",owner),new XAttribute("save",new string('b',64)),new XAttribute("policy",new string('c',64)),new XAttribute("knowledge","knowledge.test"),new XAttribute("definition",new string('D',64)),new XAttribute("admission",new string('E',64)));
 if(!RebirthStationDiscoveryWitness.TryReadStored(w,out var witness)||!RebirthStationRecipeDiscoveryRecord.TryCreate(witness,"recipe.test",new string('F',64),out var record))throw new Exception("actual DTO creation");return record;}
 static void Save(string path,RebirthStationRecipeDiscoveryRecord record,string creation){var records=new Dictionary<string,RebirthStationRecipeDiscoveryRecord>{{record.CanonicalRecipe,record}};
 new XElement("character",new XAttribute("owner",owner),new XAttribute("creation",creation),new XElement("progression",RebirthStationRecipeDiscoveryPersistence.Write(records,owner))).Save(path);}
 static void Reset(string path){RebirthWorldCharacterRepository.serverAuthority=true;RebirthWorldCharacterRepository.CurrentPath=path;RebirthWorldCharacterRepository.Migrated=false;RebirthWorldCharacterRepository.ThrowRead=false;RebirthWorldCharacterRepository.OnRead=null;RebirthWorldCharacterRepository.Reads=0;RebirthWorldCharacterRepository.PathCalls=0;}
 static bool Has(RebirthStationRecipeDiscoveryRecord r)=>RebirthWorldCharacterRepository.HasSavedRecipeDiscovery(new(owner),r);
 static void Main(){
 var folder=Path.Combine(AppContext.BaseDirectory,"fixture-files");Directory.CreateDirectory(folder);var path=Path.Combine(folder,"final.xml");
 foreach(var creation in new[]{Guid.NewGuid().ToString("N"),"legacy-"+new string('a',64)}){
 var expected=Create(creation);Save(path,expected,creation);Reset(path);C(Has(expected),"exact actual final image "+creation);
 C(RebirthWorldCharacterRepository.Reads==1,"actual loader once");
 C(!RebirthWorldCharacterRepository.HasSavedRecipeDiscovery(null,expected),"null identity");C(!Has(null),"null expected");
 C(!RebirthWorldCharacterRepository.HasSavedRecipeDiscovery(new(new string('b',64)),expected),"foreign owner before read");
 Reset(path);RebirthWorldCharacterRepository.serverAuthority=false;C(!Has(expected)&&RebirthWorldCharacterRepository.Reads==0,"no authority no read");
 Reset(path);RebirthWorldCharacterRepository.CurrentPath=null;C(!Has(expected),"missing path");
 Reset(path);RebirthWorldCharacterRepository.Migrated=true;C(!Has(expected),"migrated refuses");
 Reset(path);RebirthWorldCharacterRepository.ThrowRead=true;C(!Has(expected),"read exception false");
 Reset(path);RebirthWorldCharacterRepository.OnRead=()=>RebirthWorldCharacterRepository.serverAuthority=false;C(!Has(expected),"authority changes during load");
 Reset(path);RebirthWorldCharacterRepository.OnRead=()=>RebirthWorldCharacterRepository.CurrentPath=path+".other";C(!Has(expected),"path changes during load");
 foreach(var fault in new[]{"savedowner","savedcreation","key","job","terminal","witness","malformed","nullprogression"}){
 Save(path,expected,creation);var root=XElement.Load(path);var row=root.Element("progression").Element("recipeDiscoveries").Elements().Single();
 switch(fault){case "savedowner":root.SetAttributeValue("owner",new string('b',64));break;case "savedcreation":root.SetAttributeValue("creation",Guid.NewGuid().ToString("N"));break;
 case "key":row.SetAttributeValue("recipe","differentRecipe");break;
 case "job":var different=Guid.NewGuid().ToString("N");row.SetAttributeValue("job",different);row.Element("stationDiscoveryWitness").SetAttributeValue("job",different);break;
 case "terminal":row.SetAttributeValue("terminal",new string('A',64));break;
 case "witness":row.Element("stationDiscoveryWitness").SetAttributeValue("knowledge","knowledge.other");break;
 case "malformed":row.SetAttributeValue("unexpected",1);break;
 case "nullprogression":root.Element("progression").Remove();break;}
 root.Save(path);Reset(path);C(!Has(expected),"saved "+fault+" refuses");}
 Save(path,expected,creation);File.Copy(path,path+".bak",true);File.Delete(path);Reset(path);C(!Has(expected)&&File.Exists(path+".bak"),"backup only not final");
 Save(path,expected,creation);File.WriteAllText(path,"<broken>");Reset(path);C(!Has(expected),"corrupt final read false");
 var bad=expected.Clone();var image=(XElement)typeof(RebirthStationRecipeDiscoveryRecord).GetField("image",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(bad);image.SetAttributeValue("terminal","bad");
 Reset(path);C(!Has(bad)&&RebirthWorldCharacterRepository.Reads==0,"malformed expected no read");
 Save(path,expected,creation);Reset(path);var before=File.ReadAllBytes(path);C(Has(expected)&&before.SequenceEqual(File.ReadAllBytes(path)),"witness leaves actual file unchanged");
 }
 C(!RebirthStationRecipeDiscoveryRecord.TryReadStored(new XElement("broken"),out var invalid)&&invalid==null,"malformed out remains null");
 Console.WriteLine($"PASS {n} exact extracted saved witness + actual DTO/Witness/Scope/Persistence parser; real temporary XML final-data reads, native filesystem publication/terminal/grant not proven.");
 }
}