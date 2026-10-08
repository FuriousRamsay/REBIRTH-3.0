using System;using System.IO;
namespace HydrationDoubles {
enum StreamModeRead{Persistency}
sealed class PooledBinaryReader:BinaryReader{internal PooledBinaryReader(byte[] b):base(new MemoryStream(b)){}}
sealed class Grid {internal int Length; internal bool Throw;internal void ReadInto(PooledBinaryReader r,StreamModeRead m){Length=r.ReadByte();if(Throw)throw new InvalidDataException("injected grid failure");}}
sealed class Inventory {internal bool isReading;internal int SlotCount=4,Selected;internal Grid ItemGrid=new Grid();internal void SetSelectedSlot(int n){Selected=n;}}
sealed class Equipment {internal EntityRebirthHumanoidNPC m_entity;internal Grid ItemGrid=new Grid{Length=13};internal bool Throw,OwnerDetached;internal int Builds,Resets,Events;internal void ReadInto(PooledBinaryReader r,StreamModeRead m){OwnerDetached=m_entity==null;if(m_entity!=null)Events++;if(Throw)throw new InvalidDataException("injected equipment failure");}internal void BuildSlot(int i){Builds++;}internal void ResetArmorGroups(){Resets++;}}
sealed class EntityRebirthHumanoidNPC {internal Inventory inventory=new Inventory();internal Equipment equipment=new Equipment();}
static class HydrationFixture {
internal static int Run(){int checks=0;Action<bool,string> check=(b,n)=>{if(!b)throw new Exception(n);checks++;};var npc=new EntityRebirthHumanoidNPC();npc.equipment.m_entity=npc;
using(var r=new PooledBinaryReader(new byte[]{1,4,2}))ActualHydration.RestoreToolbelt(npc,r);
check(npc.inventory.Selected==2&&!npc.inventory.isReading,"selected hydrated and reading restored");
foreach(var frame in new[]{new byte[]{2,4,2},new byte[]{1,5,2},new byte[]{1,4,4}}){bool refused=false;try{using(var r=new PooledBinaryReader(frame))ActualHydration.RestoreToolbelt(npc,r);}catch(InvalidDataException){refused=true;}check(refused&&!npc.inventory.isReading,"bad toolbelt rejected reading restored");}
npc.inventory.ItemGrid.Throw=true;npc.inventory.isReading=true;try{using(var r=new PooledBinaryReader(new byte[]{1,4,0}))ActualHydration.RestoreToolbelt(npc,r);}catch(InvalidDataException){}check(npc.inventory.isReading,"prior reading state preserved on exception");
using(var r=new PooledBinaryReader(new byte[0]))ActualHydration.RestoreEquipment(npc,r);
check(npc.equipment.OwnerDetached&&ReferenceEquals(npc.equipment.m_entity,npc)&&npc.equipment.Events==0,"equipment owner restored no read events");
check(npc.equipment.Builds==13&&npc.equipment.Resets==1,"equipment flags and armor data rebuilt");
npc.equipment.Throw=true;bool failed=false;try{using(var r=new PooledBinaryReader(new byte[0]))ActualHydration.RestoreEquipment(npc,r);}catch(InvalidDataException){failed=true;}check(failed&&ReferenceEquals(npc.equipment.m_entity,npc)&&npc.equipment.Events==0,"equipment exception restores owner");
return checks;}}
}