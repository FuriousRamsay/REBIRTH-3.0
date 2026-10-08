using System;
class RebirthGearTransferState{}
internal static partial class FixtureReservation{
internal static void Stage(EntityPlayerLocal p,object session,Guid world,RebirthGearPreparationIntent intent,bool offer=false){Entries[p]=new Entry{World=p.world,Session=session,SavedWorld=world,Intent=intent,Offer=offer?new():null};}
internal static void Clear(){Entries.Clear();}
}
partial class ReservationTest{
internal static int Count;
static void C(bool b,string n){if(!b)throw new Exception(n);Count++;}
internal static void Run(EntityPlayerLocal p,Peer peer,Guid world,RebirthGearPreparationIntent intent){
void Stage()=>FixtureReservation.Stage(p,peer,world,intent);
Stage();C(FixtureReservation.MatchesUnpreparedIntent(p,peer,intent),"actual reservation match");
FixtureReservation.Stage(p,peer,world,intent,true);C(!FixtureReservation.MatchesUnpreparedIntent(p,peer,intent),"actual offer refuses");
Stage();C(!FixtureReservation.MatchesUnpreparedIntent(p,new Peer(),intent),"actual session refuses");
Stage();C(!FixtureReservation.ReleaseRefusedOriginal(p,peer,intent,()=>false)&&FixtureReservation.IsHeld(p),"false witness retains");
Stage();C(!FixtureReservation.ReleaseRefusedOriginal(p,peer,intent,()=>throw new Exception())&&FixtureReservation.IsHeld(p),"throw retains");
Stage();C(!FixtureReservation.ReleaseRefusedOriginal(p,peer,intent,()=>{Stage();return true;})&&FixtureReservation.IsHeld(p),"identical replacement entry refuses");
Stage();C(!FixtureReservation.ReleaseRefusedOriginal(p,peer,intent,()=>{SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer=new[]{new Peer()};return true;})&&FixtureReservation.IsHeld(p),"context changed witness refuses");
SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer=new[]{peer};
p.Buffs.CVars.Clear();p.Buffs.CVars["rbGear_"+intent.TransactionId.ToString("N")]=-1;Stage();C(!FixtureReservation.ReleaseRefusedOriginal(p,peer,intent,()=>{p.Buffs.CVars["_RBGEARINTENT_FOREIGN"]=0;return true;})&&FixtureReservation.IsHeld(p),"actual final foreignzero refuses release");p.Buffs.CVars.Clear();p.Buffs.CVars["rbGear_"+intent.TransactionId.ToString("N")]=-1;Stage();C(!FixtureReservation.ReleaseRefusedOriginal(p,peer,intent,()=>{p.Buffs.CVars["rbGear_"+intent.TransactionId.ToString("N")]=0;return true;})&&FixtureReservation.IsHeld(p),"actual final receipt mutation refuses release");p.Buffs.CVars["rbGear_"+intent.TransactionId.ToString("N")]=-1;Stage();C(FixtureReservation.ReleaseRefusedOriginal(p,peer,intent,()=>true)&&!FixtureReservation.IsHeld(p),"actual release after saved witness");
Stage();var originalGuid=D.WorldGuid;D.WorldGuid=Guid.NewGuid().ToString("N");C(!FixtureReservation.MatchesUnpreparedIntent(p,peer,intent),"saved world refuses");D.WorldGuid=originalGuid;
Stage();var creation=D.Creation;D.Creation=Guid.NewGuid().ToString("N");C(!FixtureReservation.MatchesUnpreparedIntent(p,peer,intent),"projection refuses");D.Creation=creation;FixtureReservation.Clear();
}
}
