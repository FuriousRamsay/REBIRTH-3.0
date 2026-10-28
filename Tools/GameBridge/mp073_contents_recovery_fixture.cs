using System;
using System.Collections.Generic;
using System.Linq;
// AUTHORED_NOT_RUN. Actual recovery/recipient predicates injected; native
// snapshot/journal/UI adapters are explicit and provide no delivery/save proof.
internal static class ThreadManager {internal static bool IsMainThread()=>true;}
internal sealed class World {internal EntityPlayerLocal Primary;internal Dictionary<int,object> Entities=new Dictionary<int,object>();internal EntityPlayerLocal GetPrimaryPlayer()=>Primary;internal object GetEntity(int id)=>Entities.TryGetValue(id,out var e)?e:null;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal sealed class EntityVehicle {internal World world;internal int entityId=2;}
internal sealed class EntityPlayerLocal {internal World world;internal int entityId=1;internal string Creation="creation";internal bool Dead;internal UI PlayerUI=new UI();internal bool IsDead()=>Dead;}
internal sealed class UI {internal XUi xui;internal EntityPlayerLocal entityPlayer;}
internal sealed class XUi {internal UI playerUI;internal Drag DragAndDropWindow=new Drag();}
internal sealed class Drag {internal ItemStack CurrentStack=new ItemStack();}
internal sealed class ItemStack {internal int Id=7;internal ItemValue itemValue=new ItemValue();}
internal sealed class ItemValue {internal Dictionary<string,string> Metadata=new Dictionary<string,string>{{"contents","payload"}};}
internal static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayerLocal p)=>p.Creation;}
internal sealed class Address {internal Guid Vehicle=Guid.NewGuid(),Installation=Guid.NewGuid();}
internal static class RebirthCamperPortableStation {internal static Address Address=new Address();internal static bool TryGetSourceAddress(ItemStack s,out Address a){a=Address;return true;}}
internal static class RebirthCamperStationRemovalContents {internal const string ContentsKey="contents";}
internal static class RebirthCamperRemoteStationGridTransfer {internal static bool Same(ItemStack a,ItemStack b)=>a.Id==b.Id;}
internal sealed class Stations {internal bool Has;internal object Find(Guid id)=>Has?new object():null;}
internal sealed class RebirthCamperTransferSnapshot {internal Stations Stations=new Stations();internal ItemStack Cursor=new ItemStack();internal static Dictionary<byte,RebirthCamperTransferSnapshot> Images=new Dictionary<byte,RebirthCamperTransferSnapshot>();internal static RebirthCamperTransferSnapshot Restore(Guid id,byte[] bytes)=>Images[bytes[0]];}
internal sealed class RebirthCamperTransferJournal {
 internal sealed class Record {internal Guid Vehicle;internal byte[] Before=new byte[]{1},After=new byte[]{2};}
 internal List<Record> Records=new List<Record>();internal IEnumerable<Record> FindApplied(Guid id,string creation)=>Records;
}
internal static class RebirthCamperStationContentsDelivery {
 internal static int Calls;
 internal static void Confirm(EntityPlayerLocal p,EntityVehicle v,XUi ui,RebirthCamperTransferSnapshot before,RebirthCamperTransferSnapshot after){Calls++;ui.DragAndDropWindow.CurrentStack=new ItemStack{Id=8};}
// ACTUAL_CONTENTS_RECOVERY
}
internal static class Program {
 static void Need(bool b,string m){if(!b)throw new Exception(m);}
 public static void Main(){
  for(int variant=0;variant<6;variant++){
   var w=new World();GameManager.Instance.World=w;var p=new EntityPlayerLocal{world=w};var v=new EntityVehicle{world=w};w.Primary=p;w.Entities[1]=p;w.Entities[2]=v;
   var ui=new XUi{playerUI=p.PlayerUI};p.PlayerUI.xui=ui;p.PlayerUI.entityPlayer=p;var journal=new RebirthCamperTransferJournal();journal.Records.Add(new RebirthCamperTransferJournal.Record{Vehicle=RebirthCamperPortableStation.Address.Vehicle});
   RebirthCamperTransferSnapshot.Images[1]=new RebirthCamperTransferSnapshot{Stations=new Stations{Has=true}};RebirthCamperTransferSnapshot.Images[2]=new RebirthCamperTransferSnapshot();RebirthCamperStationContentsDelivery.Calls=0;
   if(variant==1)journal.Records.Add(new RebirthCamperTransferJournal.Record{Vehicle=RebirthCamperPortableStation.Address.Vehicle});
   if(variant==2)p.Creation="other";if(variant==3)w.Entities[1]=new object();if(variant==4)w.Entities[2]=new object();if(variant==5)ui.playerUI=new UI();
   bool recovered=RebirthCamperStationContentsDelivery.RecoverHeld(p,v,ui,journal,"creation");
   Need(recovered==(variant==0)&&RebirthCamperStationContentsDelivery.Calls==(variant==0?1:0),"ambiguous/stale contents recovery variant "+variant);
  }
  Console.WriteLine("PASS actual camper contents recovery uniqueness/current-recipient predicates; delivery/native persistence doubled");
 }
}
