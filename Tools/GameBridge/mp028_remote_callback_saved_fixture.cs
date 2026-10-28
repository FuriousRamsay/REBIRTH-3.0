// AUTHORED_NOT_RUN. Full production final callback observer. Native Saved
// capability, sidecar decoder, rich codec and scratch publisher are adapters.
using System;using System.Linq;using System.Text;using System.Collections.Generic;using System.Xml.Linq;using System.Runtime.CompilerServices;
class World{internal bool IsRemote()=>true;}
class Recipe{}
class Row{internal Recipe Recipe=new Recipe();}
class EntityPlayerLocal{}
class XUiC_RecipeStack{}
static class RebirthCamperInventorySnapshot{internal static byte[] Restore(byte[] value)=>value;}
static class RebirthRemoteResourcePlacedStationQueue{internal static bool TryDecode(string text,out Row[] rows){rows=new[]{new Row()};return text=="recipe";}}
static class RebirthWorkstationCompletionPipeline{
 internal sealed class Observation{internal EntityPlayerLocal Viewer;internal XUiC_RecipeStack Card;internal World World=new World();internal string ViewerOwnerKey="owner",ViewerCreation="creation";internal int ViewerDeaths=1;internal Guid Operation;}
 internal sealed class AdditionalOutputOwnerSaved{
  internal Observation Observation;internal XElement Request,Acknowledgment;internal bool Valid=true;internal bool Current()=>Valid;
 }
}
public static partial class RebirthRecipeOutputIntegration{
 private sealed class UiScope{internal XUiC_RecipeStack Card;}
 private sealed class UiOutputOriginal{internal World World=new World();internal EntityPlayerLocal Actor;internal UiScope Scope=new UiScope();internal string Path,Creation;internal int Deaths;internal Guid Operation;internal bool Current()=>true;internal bool CallbackOwnerCurrent()=>true;}
 private static bool MatchesRemotePrefixFile(string path,byte[] expected)=>files.TryGetValue(path,out var value)&&UiOriginalBytes(value).SequenceEqual(expected);
 private sealed class Prefix{internal bool Remote=true;internal EntityPlayerLocal Actor;internal XUiC_RecipeStack Card;internal string SourcePath;}
 private sealed class Prefixes{internal List<Prefix> Values=new List<Prefix>();}
 private static readonly object uiOwnerGate=new object();
 private static readonly Dictionary<EntityPlayerLocal,Prefixes> uiOwnerPrefixes=new Dictionary<EntityPlayerLocal,Prefixes>();
 private static readonly Dictionary<string,XElement> files=new Dictionary<string,XElement>();
 private static readonly Dictionary<string,byte[]> writes=new Dictionary<string,byte[]>();
 private static bool ScrapValid=true,Publish=true;private static int checks;
 private static bool ReadPersonalOutputSidecar(string path,out byte[] bytes,out XElement value){if(!files.TryGetValue(path,out var raw)){bytes=null;value=null;return false;}value=new XElement(raw);bytes=UiOriginalBytes(value);return true;}
 private static byte[] UiOriginalBytes(XElement value)=>Encoding.UTF8.GetBytes(value.ToString(SaveOptions.DisableFormatting));
 private static string UiOriginalHash(byte[] value){using(var sha=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(value));}
 private static byte[] PersonalColdImage(XElement value,string name)=>Convert.FromBase64String(value.Element(name).Value);
 private static bool PersonalColdIdentity(XElement value,string name,string attrs,string children,Guid op,string hash)=>value.Name==name&&(string)value.Attribute("operation")==op.ToString("N")&&(string)value.Attribute("original")==hash;
 private static bool TryReadScrapCompletedEquipment(string path,Guid op,string hash,Recipe recipe,int count,byte[] before,out byte[] after){after=before;return ScrapValid;}
 private static bool PublishPersonalColdBinding(string path,byte[] value){if(!Publish)return false;writes[path]=value;return true;}
 private static XElement Grid(string name,byte value)=>new XElement(name,Convert.ToBase64String(new[]{value}));
 private static void Need(bool value,string name){if(!value)throw new Exception(name);checks++;}
 private static RebirthWorkstationCompletionPipeline.AdditionalOutputOwnerSaved Setup(){
  files.Clear();writes.Clear();uiOwnerPrefixes.Clear();ScrapValid=Publish=true;
  var local=Guid.NewGuid();var native=Guid.NewGuid();var actor=new EntityPlayerLocal();var card=new XUiC_RecipeStack();
  var original=new XElement("recipeOutputOriginal",new XAttribute("callbackFence",1),new XAttribute("cached",false),new XAttribute("owner","owner"),new XAttribute("creation","creation"),new XAttribute("deaths",1),new XAttribute("operation",local.ToString("N")),
   Grid("outputs",1),new XElement("recipe","recipe"),new XElement("remoteOutputSource",new XAttribute("cached",false),new XAttribute("operation",native.ToString("N")),new XAttribute("nonce","nonce"),new XAttribute("source","source")));
  string hash=UiOriginalHash(UiOriginalBytes(original));files["original"]=original;
  files["original.native-continuation-returned"]=new XElement("recipeOutputNativeContinuationReturned",new XAttribute("operation",local.ToString("N")),new XAttribute("original",hash),new XAttribute("outputPrefix",1));
  files["original.output-0.accepted"]=new XElement("recipeOutputAccepted",new XAttribute("operation",local.ToString("N")),new XAttribute("original",hash),new XAttribute("index",0),new XAttribute("attempt",0),Grid("bag",1),Grid("belt",2),Grid("equipment",3));
  uiOwnerPrefixes[actor]=new Prefixes();uiOwnerPrefixes[actor].Values.Add(new Prefix{Actor=actor,Card=card,SourcePath="original"});
  var localPrefix=new XElement("nativeOutputPrefix",new XAttribute("operation",local.ToString("N")),new XAttribute("original",hash),new XAttribute("count",1),
   new XAttribute("accepted",UiOriginalHash(UiOriginalBytes(files["original.output-0.accepted"]))));
  var request=new XElement("nativeViewer",new XAttribute("stage","after"),new XAttribute("generation","generation"),new XAttribute("pdsDigest","native"),new XAttribute("pdsLength",100),
   new XElement("nativeOwnerFrame",Grid("bag",1),Grid("belt",2),Grid("equipment",3)),new XElement("nativeOutputOriginal",localPrefix));
  return new RebirthWorkstationCompletionPipeline.AdditionalOutputOwnerSaved{Observation=new RebirthWorkstationCompletionPipeline.Observation{Viewer=actor,Card=card,Operation=native},
   Request=request,
   Acknowledgment=new XElement("ack",new XAttribute("nativeNonce","nonce"),new XAttribute("nativeSource","source"),new XAttribute("digest","digest"))};
 }
 public static void Main(){
  var saved=Setup();ObserveRemoteOutputCallbackOwnerSaved(saved);Need(writes.Count==1,"positive actual final owner source");
  var record=XElement.Parse(Encoding.UTF8.GetString(writes.Values.Single()));Need(record.Name=="recipeOutputRemoteCallbackOwnerSaved"&&record.Element("request")!=null&&record.Element("acknowledgment")!=null,"immutable exact final request and ack");
  Need(!writes.ContainsKey("original.output-0.owner-saved"),"intermediate prefix is never rewritten");
  saved=Setup();saved.Valid=false;ObserveRemoteOutputCallbackOwnerSaved(saved);Need(writes.Count==0,"unsaved native source refused");
  saved=Setup();files.Remove("original.native-continuation-returned");ObserveRemoteOutputCallbackOwnerSaved(saved);Need(writes.Count==0,"entered unknown callback refused");
  saved=Setup();saved.Observation.ViewerCreation="other";ObserveRemoteOutputCallbackOwnerSaved(saved);Need(writes.Count==0,"foreign creation refused");
  saved=Setup();saved.Observation.ViewerDeaths++;ObserveRemoteOutputCallbackOwnerSaved(saved);Need(writes.Count==0,"foreign life refused");
  saved=Setup();saved.Acknowledgment.SetAttributeValue("nativeNonce","foreign");ObserveRemoteOutputCallbackOwnerSaved(saved);Need(writes.Count==0,"foreign original nonce refused");
  saved=Setup();saved.Request.Element("nativeOwnerFrame").Element("belt").Value="CQ==";ObserveRemoteOutputCallbackOwnerSaved(saved);Need(writes.Count==0,"foreign belt refused");
  saved=Setup();saved.Request.Element("nativeOwnerFrame").Element("equipment").Value="CQ==";ObserveRemoteOutputCallbackOwnerSaved(saved);Need(writes.Count==0,"foreign cosmetic image refused");
  saved=Setup();ScrapValid=false;ObserveRemoteOutputCallbackOwnerSaved(saved);Need(writes.Count==0,"unproved scrap delta refused");
  saved=Setup();Publish=false;ObserveRemoteOutputCallbackOwnerSaved(saved);Need(writes.Count==0,"failed immutable publication grants no receipt");
  saved=Setup();var original=files["original"];var op=Guid.ParseExact((string)original.Attribute("operation"),"N");var callback=new UiOutputOriginal{Actor=saved.Observation.Viewer,Scope=new UiScope{Card=saved.Observation.Card},World=saved.Observation.World,Path="original",Creation="creation",Deaths=1,Operation=op};
  RegisterRemoteCallbackOwner(callback,UiOriginalBytes(files["original.native-continuation-returned"]));uiOwnerPrefixes.Clear();ObserveRemoteOutputCallbackOwnerSaved(saved);Need(writes.Count==1,"actual returned callback source survives released intermediate registry");
  Console.WriteLine("PASS "+checks+" actual remote final callback source assertions");
 }
}
// ACTUAL_CALLBACK_SOURCE
