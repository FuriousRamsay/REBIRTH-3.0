using System;using System.IO;using System.Reflection;
class Program {
 static Assembly mod;static int checks;
 static object Call(Type t,string name,params object[] args)=>t.GetMethod(name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,args);
 static object F(object s,string name)=>s.GetType().GetField(name).GetValue(s);
 static void Set(object s,string name,object v)=>s.GetType().GetField(name).SetValue(s,v);
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 static void Main(string[] args){
 string root=Path.GetFullPath(args[0]);AppDomain.CurrentDomain.AssemblyResolve+=(sender,e)=>{
 string name=new AssemblyName(e.Name).Name+".dll";foreach(string folder in new[]{root,Path.Combine(root,"../../7DaysToDie_Data/Managed"),Path.Combine(root,"../0_TFP_Harmony")}){
 string p=Path.GetFullPath(Path.Combine(folder,name));if(File.Exists(p))return Assembly.LoadFrom(p);}return null;};
 mod=Assembly.LoadFrom(Path.Combine(root,"RebirthUtils.dll"));Run(root);}
 static void Run(string root){
 var st=mod.GetType("RebirthSandboxState");var manager=mod.GetType("RebirthSandboxOptionManager");var policy=mod.GetType("RebirthThemePolicy");
 object state=Activator.CreateInstance(st);var theme=mod.GetType("RebirthWorldTheme");var mode=mod.GetType("RebirthSpawnProgressionMode");
 Check(Convert.ToInt32(F(state,"SpawnProgression"))==0,"new game defaults to Biome zombie progression");
 string defaultCode=(string)Call(manager,"Encode",state);object[] defaultDecode={defaultCode,null};
 Check((bool)Call(manager,"TryDecode",defaultDecode)&&Convert.ToInt32(F(defaultDecode[1],"SpawnProgression"))==0,"new Biome default explicitly roundtrips");
 object[] legacyDecode={"RBX",null};Check((bool)Call(manager,"TryDecode",legacyDecode)&&Convert.ToInt32(F(legacyDecode[1],"SpawnProgression"))==1,"legacy omitted setting retains Gamestage");
 Set(state,"SpawnProgression",Enum.ToObject(mode,1));
 Check(Convert.ToInt32(F(state,"Theme"))==0&&!(bool)F(state,"ShowClearedPois"),"default None/Off");
 Check(!(bool)Call(policy,"TrackingEnabled",state),"None/Off no tracking");Set(state,"ShowClearedPois",true);
 Check((bool)Call(policy,"TrackingEnabled",state)&&(bool)Call(policy,"TraderJobsAllowed",state)&&(bool)Call(policy,"BiomeHazardsAllowed",state),"None/On tracking retains jobs and hazards");
 Set(state,"ShowClearedPois",false);Set(state,"Theme",Enum.ToObject(theme,1));
 Check((bool)Call(policy,"TrackingEnabled",state)&&!(bool)Call(policy,"TraderJobsAllowed",state)&&!(bool)Call(policy,"BiomeHazardsAllowed",state),"Purge effective policies");
 Check(Convert.ToInt32(Call(policy,"SpawnProgression",state))==0&&Convert.ToInt32(F(state,"SpawnProgression"))==1,"Purge effective Biome retains configured Gamestage");
 var clone=st.GetMethod("Clone").Invoke(state,null);Check(Convert.ToInt32(F(clone,"Theme"))==1&&!(bool)F(clone,"ShowClearedPois"),"clone preserves settings");
 string code=(string)Call(manager,"Encode",state);Check(code.StartsWith("RBX"),"new version X");
 object[] dec={code,null};Check((bool)Call(manager,"TryDecode",dec),"decode production code");
 Check(Convert.ToInt32(F(dec[1],"Theme"))==1&&Convert.ToInt32(F(dec[1],"SpawnProgression"))==1,"roundtrip theme and underlying progression");
 string old="RBW"+code.Substring(3);dec=new object[]{old,null};Check((bool)Call(manager,"TryDecode",dec)&&Convert.ToInt32(F(dec[1],"Theme"))==0&&!(bool)F(dec[1],"ShowClearedPois"),"old W ignores previously unknown theme ID");
 Set(state,"Theme",Enum.ToObject(theme,0));Check(Convert.ToInt32(Call(policy,"SpawnProgression",state))==1,"None restores configured Gamestage");
 Set(state,"ShowClearedPois",true);code=(string)Call(manager,"Encode",state);dec=new object[]{code,null};
 Check((bool)Call(manager,"TryDecode",dec)&&(bool)F(dec[1],"ShowClearedPois"),"QoL preference roundtrip");
 // 71 = CT, 72 = CU in the append-only base26 IDs.
 dec=new object[]{"RBXCTC",null};Check(!(bool)Call(manager,"TryDecode",dec),"invalid Theme rejected");
 dec=new object[]{"RBXCUC",null};Check(!(bool)Call(manager,"TryDecode",dec),"invalid QoL rejected");
 dec=new object[]{"RBWCTCCUC",null};Check((bool)Call(manager,"TryDecode",dec)&&Convert.ToInt32(F(dec[1],"Theme"))==0,"old unknown IDs stay ignored even with future invalid values");
 var gate=mod.GetType("RebirthPurgeReleasePolicy");Check(!(bool)gate.GetProperty("Enabled",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null),"unfinished release gate remains closed");
 Check(Convert.ToInt32(Enum.Parse(mod.GetType("RebirthSandboxOptionId"),"Theme"))==71&&Convert.ToInt32(Enum.Parse(mod.GetType("RebirthSandboxOptionId"),"ShowClearedPois"))==72,"stable appended IDs");
 var persistence=mod.GetType("RebirthSandboxPersistence");
 string purgeCode=(string)Call(manager,"Encode",clone);
 string noneCode=(string)Call(manager,"Encode",state);
 string preserved=(string)Call(persistence,"PreservePurgeTheme",noneCode,purgeCode);
 dec=new object[]{preserved,null};
 Check((bool)Call(manager,"TryDecode",dec)&&Convert.ToInt32(F(dec[1],"Theme"))==1&&(bool)F(dec[1],"ShowClearedPois"),"Purge locked while other requested options preserved");
 Check((string)Call(persistence,"PreservePurgeTheme",noneCode,"RBW")==noneCode,"legacy saves do not gain Purge");
 Check((string)Call(persistence,"PreservePurgeTheme","invalid",purgeCode)==purgeCode,"invalid replacement retains saved Purge snapshot");
 Check((string)Call(persistence,"PreservePurgeTheme",purgeCode,purgeCode)==purgeCode,"Purge save reload unchanged");
 // Exercise the actual persistence methods against disposable files only.
 string temp=Path.Combine(Path.GetTempPath(),"rebirth-options-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
 try {
  string path=Path.Combine(temp,"options.xml");var dt=mod.GetType("RebirthSandboxSaveData");object data=Activator.CreateInstance(dt);
  Set(data,"Code",purgeCode);Set(data,"Revision",1);Call(persistence,"Save",path,data);
  object[] load={path,null};Check((bool)Call(persistence,"TryLoad",load)&&(string)F(load[1],"Code")==purgeCode,"flushed option file reload");
  Set(data,"Revision",2);Call(persistence,"Save",path,data);
  Check(File.Exists(path+".bak")&&!File.Exists(path+".tmp"),"replacement retains readable backup and clears staging");
  File.WriteAllText(path,"broken XML");load=new object[]{path+".bak",null,null};
  Check((bool)Call(persistence,"TryLoadOne",load)&&Convert.ToInt32(F(load[1],"Revision"))==1,"previous Purge backup stays readable after primary corruption");
  string backupBefore=File.ReadAllText(path+".bak");Set(data,"Revision",3);Call(persistence,"Save",path,data);
  Check(File.ReadAllText(path+".bak")==backupBefore,"repair write never overwrites good backup with corrupt primary");
  load=new object[]{path,null};Check((bool)Call(persistence,"TryLoad",load)&&Convert.ToInt32(F(load[1],"Revision"))==3,"recovered option file publishes requested revision");
  string finalBefore=File.ReadAllText(path);Set(data,"Code","invalid");bool refused=false;
  try{Call(persistence,"Save",path,data);}catch(TargetInvocationException e){refused=e.InnerException is InvalidDataException;}
  Check(refused&&File.ReadAllText(path)==finalBefore&&!File.Exists(path+".tmp"),"invalid replacement leaves final and backup untouched");
 } finally {Directory.Delete(temp,true);}
 Console.WriteLine("RESULT "+checks+" PASS against compiled production options/policy; no game launch.");
 }}

