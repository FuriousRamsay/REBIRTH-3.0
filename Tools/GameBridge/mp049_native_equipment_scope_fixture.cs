// AUTHORED_NOT_RUN. Actual equipment-domain adapter, domain container and path
// predicates. Native World/projection producers are explicit adapters.
using System;using System.Collections.Generic;using System.Globalization;using System.IO;using System.Security.Cryptography;using System.Text;using System.Xml;
class World{}
struct RebirthNpcStableId{internal string Value;internal bool IsEmpty=>string.IsNullOrEmpty(Value);public override string ToString()=>Value;}
enum RebirthNpcEquipmentSlot:byte{Head=0,Body=1}
internal static class RebirthNpcSaveQuarantineScope{
 // ACTUAL_SCOPE_DIRECTORY_MEMBERS
}
// ACTUAL_DOMAIN
public static partial class RebirthNpcNativeEquipmentBridge{
 class ProjectionState{internal ulong Generation=3;internal bool Converged=true,Completed=true;internal uint RequestedRevision=4,AppliedRevision=4;internal Dictionary<RebirthNpcEquipmentSlot,string> RequestedSlots=new Dictionary<RebirthNpcEquipmentSlot,string>(),AppliedSlots=new Dictionary<RebirthNpcEquipmentSlot,string>();}
 static readonly object Sync=new object();static World projectionWorld;static string projectionDirectory;static ulong generationFloor;static Dictionary<RebirthNpcStableId,ProjectionState> Applied=new Dictionary<RebirthNpcStableId,ProjectionState>();
 static string save=Path.Combine(Path.GetTempPath(),"rebirth-mp049-equipment-scope");static World original;static RebirthNpcStableId id;static int checks;
 static void Seed(){original=new World();projectionWorld=original;projectionDirectory=save;generationFloor=9;id=new RebirthNpcStableId{Value="npc"};var state=new ProjectionState();state.RequestedSlots.Add(RebirthNpcEquipmentSlot.Head,"helmet");state.AppliedSlots.Add(RebirthNpcEquipmentSlot.Head,"helmet");Applied=new Dictionary<RebirthNpcStableId,ProjectionState>{{id,state}};}
 static void Need(bool value){if(!value)throw new Exception("native equipment scope "+checks);checks++;}
 static void Refuses(Action action){bool refused=false;try{action();}catch(InvalidOperationException){refused=true;}Need(refused);}
 public static void Main(){
  Seed();var prior=Applied;var domain=PrepareNativeProjectionQuarantine(original,save);Need(domain.IsCurrent);var evidence=domain.CopyEvidence(new XmlDocument());Need(evidence.SelectSingleNode("nativeEquipmentProjectionQuarantine/npc[@id='npc']/applied/slot[@item='helmet']")!=null);
  domain.Detach();Need(domain.IsDetached&&Applied.Count==0&&projectionWorld==null&&projectionDirectory==null&&generationFloor==0);domain.Rollback();Need(domain.IsCurrent&&ReferenceEquals(Applied,prior)&&ReferenceEquals(projectionWorld,original)&&generationFloor==9);domain.Rollback();Need(domain.IsCurrent);
  Seed();Refuses(()=>PrepareNativeProjectionQuarantine(null,save));Seed();Refuses(()=>PrepareNativeProjectionQuarantine(new World(),save));Seed();Refuses(()=>PrepareNativeProjectionQuarantine(original,save+"-foreign"));
  Seed();domain=PrepareNativeProjectionQuarantine(original,Path.Combine(save,"."));Need(domain.IsCurrent);
  Seed();string changed=Path.Combine(Path.GetDirectoryName(save),Path.GetFileName(save).ToUpperInvariant());if(Path.DirectorySeparatorChar=='\\'){domain=PrepareNativeProjectionQuarantine(original,changed);Need(domain.IsCurrent);}else Refuses(()=>PrepareNativeProjectionQuarantine(original,changed));
  Seed();Refuses(()=>PrepareNativeProjectionQuarantine(original,Path.GetFileName(save)));Seed();Refuses(()=>PrepareNativeProjectionQuarantine(original,null));
  Seed();domain=PrepareNativeProjectionQuarantine(original,save);Applied[id].AppliedSlots[RebirthNpcEquipmentSlot.Head]="changed";Need(!domain.IsCurrent);Refuses(()=>domain.Detach());
  Seed();domain=PrepareNativeProjectionQuarantine(original,save);generationFloor++;Need(!domain.IsCurrent);
  Seed();projectionWorld=null;Refuses(()=>PrepareNativeProjectionQuarantine(original,save));
  Seed();projectionWorld=null;projectionDirectory=null;Applied.Clear();domain=PrepareNativeProjectionQuarantine(original,save);Need(domain.IsCurrent);domain.Detach();domain.Rollback();Need(domain.IsCurrent&&projectionWorld==null&&Applied.Count==0);
  Seed();Applied.Clear();Applied.Add(new RebirthNpcStableId(),new ProjectionState());Refuses(()=>PrepareNativeProjectionQuarantine(original,save));
  Seed();Applied[id]=null;Refuses(()=>PrepareNativeProjectionQuarantine(original,save));
  Console.WriteLine("PASS "+checks+" actual native equipment quarantine scope assertions; native producers excluded");
 }
}
// ACTUAL_NATIVE_EQUIPMENT_ADAPTER
