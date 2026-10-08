$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$stub=@"
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
namespace HarmonyLib {public class HarmonyPatch:Attribute {public HarmonyPatch(Type t,string n,Type[] args){}}}
public class GUIWindow {public string Id,openWindowOnEsc;public bool isShowing,isModal=true;}
public class GUIWindowManager {
 public Dictionary<string,GUIWindow> Windows=new Dictionary<string,GUIWindow>();
 public bool TryGetWindow(string id,out GUIWindow window)=>Windows.TryGetValue(id,out window);
 public bool IsWindowOpen(string id)=>Windows.ContainsKey(id)&&Windows[id].isShowing;
 public void Open(string id,bool modal){Windows[id].isShowing=true;}
 public void CloseAllOpenModalWindows(GUIWindow except=null,bool fromEsc=false){
 foreach(var window in Windows.Values){if(!window.isShowing||window==except||!window.isModal)continue;window.isShowing=false;if(fromEsc&&!string.IsNullOrEmpty(window.openWindowOnEsc))Open(window.openWindowOnEsc,true);}
 }
}
public static class ParentReturnFixture {
 public static string Run(){
 var prefix=typeof(RebirthBackpackSectionEscapeReturnPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static);
 var postfix=typeof(RebirthBackpackSectionEscapeReturnPatch).GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static);
 foreach(string section in new[]{"rebirthBackpackLibrary","rebirthBackpackSellStash"})
 foreach(bool escape in new[]{false,true}){
  var manager=new GUIWindowManager();
  var child=new GUIWindow{Id=section,openWindowOnEsc="parent",isShowing=true};
  manager.Windows.Add(section,child);manager.Windows.Add("parent",new GUIWindow{Id="parent"});
  object[] args={manager,escape,null};prefix.Invoke(null,args);
  manager.CloseAllOpenModalWindows(null,escape);postfix.Invoke(null,new[]{(object)manager,args[2]});
  if(manager.IsWindowOpen(section)||manager.IsWindowOpen("parent")!=escape)throw new Exception("Incorrect close/parent return");
  if(child.openWindowOnEsc!="parent")throw new Exception("Caller metadata not restored");
 }
 return "PASS: production Escape patch returns both compartments to their parent after the complete close pass; ordinary navigation does not reopen parents.";
 }
}
"@
$production=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Survivor/UI/RebirthBackpackSectionEscapeReturnPatch.cs')).Replace('using System;','').Replace('using HarmonyLib;','')
Add-Type -TypeDefinition ($stub+$production)
[ParentReturnFixture]::Run()