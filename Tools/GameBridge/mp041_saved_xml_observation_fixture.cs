// AUTHORED_NOT_RUN. Actual production resource scope; shared budget is an adapter.
using System;
using System.IO;
using System.Xml;
// ACTUAL_SAVED_XML
static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class RebirthProductiveEarnedObservationBudget {
 internal static bool Admit=true,Grow=true;internal static long Held,Peak;internal static int Released;
 sealed class Lease:IDisposable {internal long Bytes;public void Dispose(){Held-=Bytes;Bytes=0;Released++;}}
 internal static bool TryRetainDetached(long size,out IDisposable lease){lease=null;if(!Admit)return false;var value=new Lease{Bytes=size};lease=value;Held+=size;Peak=Math.Max(Peak,Held);return true;}
 internal static bool TryResizeDetached(IDisposable lease,long size){if(!Grow)return false;var value=(Lease)lease;Held+=size-value.Bytes;value.Bytes=size;Peak=Math.Max(Peak,Held);return true;}
}
static class Program {
 static void Need(bool value,string error){if(!value)throw new Exception(error);}
 public static void Main(){
  string directory=Path.Combine(Path.GetTempPath(),"rebirth-saved-xml-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
  string path=Path.Combine(directory,"primary.xml");int callbacks=0;
  try{
   File.WriteAllText(path,"<root><record id='original'><value>saved</value></record></root>");
   Need(RebirthNpcSavedXmlObservation.Observe(path,()=>{
    callbacks++;Need(RebirthProductiveEarnedObservationBudget.Held>0,"reservation ended before caller predicate");
    var document=new XmlDocument();document.Load(path);Need(document.DocumentElement.FirstChild.Attributes["id"].Value=="original","domain reader did not see original primary");
    bool denied=false;try{using(var writer=new FileStream(path,FileMode.Open,FileAccess.Write,FileShare.ReadWrite)){} }catch(IOException){denied=true;}
    Need(denied,"primary writable during positive receipt observation");return true;
   })&&callbacks==1&&RebirthProductiveEarnedObservationBudget.Held==0,"positive observation scope failed");
   Need(!RebirthNpcSavedXmlObservation.Observe(path,()=>false)&&RebirthProductiveEarnedObservationBudget.Held==0,"false predicate leaked allocation or became positive");
   Need(!RebirthNpcSavedXmlObservation.Observe(path,()=>throw new IOException("injected readback failure"))&&RebirthProductiveEarnedObservationBudget.Held==0,"thrown predicate leaked allocation");
   RebirthProductiveEarnedObservationBudget.Admit=false;
   Need(!RebirthNpcSavedXmlObservation.Observe(path,()=>{callbacks++;return true;})&&callbacks==1,"initial budget refusal invoked decoder");
   RebirthProductiveEarnedObservationBudget.Admit=true;RebirthProductiveEarnedObservationBudget.Grow=false;
   Need(!RebirthNpcSavedXmlObservation.Observe(path,()=>{callbacks++;return true;})&&callbacks==1&&RebirthProductiveEarnedObservationBudget.Held==0,"measured growth refusal entered decoder or leaked");
   RebirthProductiveEarnedObservationBudget.Grow=true;
   foreach(string invalid in new[]{"<root>","<!DOCTYPE root [<!ENTITY x 'expanded'>]><root>&x;</root>",new string('<',500)}){
    File.WriteAllText(path,invalid);
    Need(!RebirthNpcSavedXmlObservation.Observe(path,()=>{callbacks++;return true;})&&callbacks==1&&RebirthProductiveEarnedObservationBudget.Held==0,"malformed/DTD primary entered domain reader");
   }
   File.WriteAllText(path,new string(' ',1024));
   using(var file=new FileStream(path,FileMode.Open,FileAccess.Write)){file.SetLength(64L*1024*1024+1);}
   Need(!RebirthNpcSavedXmlObservation.Observe(path,()=>{callbacks++;return true;})&&callbacks==1,"oversized primary entered reader");
   File.WriteAllText(path,"<r/>");ThreadManager.Main=false;
   Need(!RebirthNpcSavedXmlObservation.Observe(path,()=>{callbacks++;return true;})&&callbacks==1,"worker observation admitted");ThreadManager.Main=true;
   File.Delete(path);Need(!RebirthNpcSavedXmlObservation.Observe(path,()=>true),"missing primary interpreted as saved receipt");
   Console.WriteLine("PASS actual saved XML scope: positive read, write exclusion, reservation lifetime, pressure/malformed/DTD/oversize/thread/missing refusal; native schema and gameplay NOT RUN");
  }finally{ThreadManager.Main=true;if(File.Exists(path))File.Delete(path);Directory.Delete(directory);}
 }
}
