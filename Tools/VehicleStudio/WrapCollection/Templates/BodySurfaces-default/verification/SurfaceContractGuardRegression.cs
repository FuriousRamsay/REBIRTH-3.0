using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

// Editor-only negative tests. Never invokes a bundle build or game operation.
public static class SurfaceContractGuardRegression {
 static string Arg(string name){var a=Environment.GetCommandLineArgs();return a[Array.IndexOf(a,name)+1];}
 static string Hash(string p){using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 public static void Run(){
  string templates=Arg("-wrapSurfaceTemplates"),output=Arg("-wrapReviewOutput");Directory.CreateDirectory(output);
  string path="Assets/REBIRTHCamperWrapCollection/Quality/NativeSurfaceCoverage.json";Directory.CreateDirectory(Path.GetDirectoryName(path));byte[] original=File.Exists(path)?File.ReadAllBytes(path):null;
  var owner=typeof(RebirthWrapCollectionBuild);var type=owner.GetNestedType("QualityReceipt",BindingFlags.NonPublic);var receipt=Activator.CreateInstance(type,true);
  var method=owner.GetMethod("VerifyNativeCoverage",BindingFlags.NonPublic|BindingFlags.Static);if(method==null)throw new Exception("Native coverage guard missing");
  string good=File.ReadAllText(Path.Combine(templates,"NativeSurfaceCoverage.json"));
  Action<string> write=text=>{File.WriteAllText(path,text);type.GetField("nativeCoverageSHA256").SetValue(receipt,Hash(path));type.GetField("geometrySHA256").SetValue(receipt,Regex.Match(good,"\"geometrySHA256\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value);};
  Action invoke=()=>method.Invoke(null,new[]{receipt});int rejected=0;
  Action<string> reject=text=>{write(text);try{invoke();}catch(TargetInvocationException e){if(e.InnerException is Exception){rejected++;return;}}throw new Exception("Invalid native contract accepted");};
  try{
   write(good);invoke();
   reject(new Regex("\"uncoveredPixels\": 0").Replace(good,"\"uncoveredPixels\": 1",1));
   reject(new Regex("\"version\": \"[^\"]+\"").Replace(good,"\"version\": \"obsolete\"",1));
   reject(new Regex("\"sha256\": \"[a-f0-9]+\"").Replace(good,"\"sha256\": \"incorrect\"",1));
   write(good);type.GetField("nativeCoverageSHA256").SetValue(receipt,"stale");try{invoke();throw new Exception("Stale proof accepted");}catch(TargetInvocationException){rejected++;}
   if(rejected!=4)throw new Exception("Missing regression rejection");
   File.WriteAllText(Path.Combine(output,"build-guard-regression.json"),"{\"status\":\"PASS\",\"validContractAccepted\":true,\"invalidContractsRejected\":4}");
   Debug.Log("SURFACE_BUILD_GUARD_REGRESSION_PASS rejected="+rejected);
  }finally{if(original!=null)File.WriteAllBytes(path,original);else File.Delete(path);}
 }
}
