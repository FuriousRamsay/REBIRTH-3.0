using System;
using System.IO;
public static class PersistenceFixture
{
    sealed class Owner:ISeedRecoveryOwnerAdmission
    { public bool Allow=true,Proof=true,Correction=true;public bool AcceptConflict(SeedRecoveryRecord r,SeedDebitReceipt s)=>Proof;public bool CanCorrectPrediction(SeedRecoveryRecord r,SeedDebitReceipt s)=>Correction;public bool Current(SeedRecoveryRecord r,SeedDebitReceipt s)=>Allow;
      public bool AcceptEvidence(SeedRecoveryRecord r,SeedDebitReceipt s,SeedPlacementOutcome o,bool no,bool partial)=>Proof; }
    static int checks;
    static void C(bool value,string label){checks++;if(!value)throw new Exception(label);}
    public static void Main(string[] args)
    {
        var path=Path.GetFullPath(args[0]);var journal=new SeedRecoveryJournal(path);Guid w=Guid.NewGuid(),c=Guid.NewGuid();
        object world=new object(),player=new object(),action=new object(),source=new object();byte[] seed={1,2,3};
        Func<ulong,SeedDebitReceipt> fresh=n=>new SeedDebitReceipt(11,n,0,2,seed,world,player,action,source);
        Func<ulong,SeedDebitObservation> before=n=>new SeedDebitObservation(11,n,0,2,seed,world,player,action,source);
        Func<ulong,SeedDebitObservation> after=n=>new SeedDebitObservation(11,n,0,1,seed,world,player,action,source);
        Func<ulong,SeedRecoveryRecord> original=n=>SeedRecoveryOriginalCommandAdapter.Capture(w,c,
            SeedPlacementOriginalCommandReview.Capture(11,n,9,2,new SeedPlacementIntentCodecReview.Intent{Slot=0,Seed=seed,Flags=0x15,Density=-3,Texture=0,
            Position=new Vector3i{x=1,y=2,z=3},Target=new BlockValue{rawData=12,damage=4},ExpectedOld=new BlockValue{rawData=1,damage=0}}),fresh(n));
        var owner=new Owner();C(journal.Retain(original(1),fresh(1)),"retain original");
        var read=journal.Find(w,c,11,1);C(read.Flags==0x15&&read.Density==-3&&read.Texture==0&&read.TargetDamage==4,"complete native payload retained");
        read.Seed[0]=9;C(journal.Find(w,c,11,1).Seed[0]==1,"defensive query copy");
        var changed=original(1);changed.X++;C(!journal.Retain(changed,fresh(1)),"different original command refused");
        Guid token;C(!journal.TryReserveSettlement(w,c,11,1,fresh(1),owner,out token),"unknown/no loss cannot settle");
        var exact=fresh(1).ObserveOriginalDebit(before(1),after(1),true,SeedPlacementOutcome.Unknown).Receipt;
        owner.Proof=false;C(!journal.RecordEvidence(w,c,11,1,exact,SeedPlacementOutcome.RejectedBeforeMutation,true,false,owner),"unauthenticated evidence refused");owner.Proof=true;
        C(journal.RecordEvidence(w,c,11,1,exact,SeedPlacementOutcome.Unknown,false,true,owner),"partial retained");
        C(journal.RecordEvidence(w,c,11,1,exact,SeedPlacementOutcome.RejectedBeforeMutation,true,false,owner),"later outcome retains partial bit");
        C(!journal.TryReserveSettlement(w,c,11,1,exact,owner,out token),"partial never refunds");
        C(journal.Retain(original(2),fresh(2)),"second command");
        var exact2=fresh(2).ObserveOriginalDebit(before(2),after(2),true,SeedPlacementOutcome.Unknown).Receipt;
        C(journal.RecordEvidence(w,c,11,2,exact2,SeedPlacementOutcome.RejectedBeforeMutation,true,false,owner),"authoritative rejection recorded");
        owner.Allow=false;C(!journal.TryReserveSettlement(w,c,11,2,exact2,owner,out token),"disconnected owner cannot settle");owner.Allow=true;
        C(!journal.TryReserveSettlement(w,c,11,2,exact2,owner,out token),"compensation before correction refused");
        Guid correction;C(journal.TryReserveCorrection(w,c,11,2,exact2,owner,out correction),"exact debit prediction correction reserved");
        C(!journal.TryReserveSettlement(w,c,11,2,exact2,owner,out token),"reserved or failed correction cannot compensate");
        C(!journal.CompleteReservation(w,c,11,2,Guid.NewGuid(),SeedRecoveryEffect.PredictionCorrection),"failed correction completion refused");
        C(!journal.TryReserveSettlement(w,c,11,2,exact2,owner,out token),"failed correction retains compensation hold");
        C(journal.CompleteReservation(w,c,11,2,correction,SeedRecoveryEffect.PredictionCorrection),"exact correction durably consumed");
        C(journal.TryReserveSettlement(w,c,11,2,exact2,owner,out token)&&token!=Guid.Empty,"durable one-use reservation after correction");
        var restarted=new SeedRecoveryJournal(path);Guid duplicate;
        C(restarted.Find(w,c,11,2).Phase==SeedRecoveryPhase.SettlementReserved,"restart holds reserved crash window");
        C(!restarted.TryReserveSettlement(w,c,11,2,exact2,owner,out duplicate),"no effect replay after restart");
        C(!restarted.CompleteReservation(w,c,11,2,Guid.NewGuid()),"wrong claim token refused");
        C(restarted.CompleteReservation(w,c,11,2,token),"claimed effect marks consumed");
        C(!restarted.CompleteReservation(w,c,11,2,token),"duplicate complete refused");
        C(journal.Retain(original(2),fresh(2)),"duplicate original accepted without rewrite");
        C(journal.Find(w,c,11,2).Phase==SeedRecoveryPhase.Consumed,"consumed history remains");
        C(journal.Find(w,c,12,2)==null,"new epoch never remaps old command");
        C(journal.Find(w,Guid.NewGuid(),11,2)==null,"wrong character can't query custody");
        C(journal.Retain(original(3),fresh(3)),"cancelled captured command retained");
        C(journal.RecordEvidence(w,c,11,3,fresh(3),SeedPlacementOutcome.RejectedBeforeMutation,true,false,owner),"actual cancellation evidence stored");
        owner.Correction=false;C(!journal.TryReserveCorrection(w,c,11,3,fresh(3),owner,out duplicate),"missing exact prediction/cancel proof refuses");owner.Correction=true;
        C(journal.TryReserveCorrection(w,c,11,3,fresh(3),owner,out duplicate),"captured cancelled correction-only claim");
        C(!journal.TryReserveSettlement(w,c,11,3,fresh(3),owner,out token),"correction claim grants no refund");
        C(!journal.CompleteReservation(w,c,11,3,duplicate),"wrong effect kind refuses completion");
        C(journal.CompleteReservation(w,c,11,3,duplicate,SeedRecoveryEffect.PredictionCorrection),"typed correction completes");
        C(!new SeedRecoveryJournal(path).TryReserveCorrection(w,c,11,3,fresh(3),owner,out duplicate),"correction no reconnect replay");
        C(journal.Retain(original(4),fresh(4)),"contradiction record");
        C(!journal.RecordEvidence(w,c,11,4,fresh(4),SeedPlacementOutcome.Committed,true,false,owner),"committed no-effect contradiction rejected");
        C(!journal.RecordEvidence(w,c,11,4,fresh(4),SeedPlacementOutcome.RejectedBeforeMutation,true,true,owner),"rejected partial contradiction rejected");
        var invalid=original(5);invalid.Flags=128;bool bounds=false;try{journal.Retain(invalid,fresh(5));}catch(InvalidDataException){bounds=true;}C(bounds,"unqualified flags rejected");
        C(journal.RecordEvidence(w,c,11,4,fresh(4),SeedPlacementOutcome.RejectedBeforeMutation,true,false,owner),"preconflict terminal evidence");
        owner.Proof=false;C(!journal.RecordConflict(w,c,11,4,fresh(4),owner),"unproven conflict cannot mutate");owner.Proof=true;
        C(journal.RecordConflict(w,c,11,4,fresh(4),owner),"authenticated conflict durable latch");
        var conflict=journal.Find(w,c,11,4);C(conflict.Conflict&&conflict.Outcome==SeedPlacementOutcome.RejectedBeforeMutation&&conflict.NoWorldEffectProven,"terminal evidence preserved on conflict");
        C(!journal.TryReserveCorrection(w,c,11,4,fresh(4),owner,out duplicate),"conflict correction refuses");
        C(!journal.RecordEvidence(w,c,11,4,fresh(4),SeedPlacementOutcome.RejectedBeforeMutation,true,false,owner),"conflict cannot clear via ordinary evidence");
        C(new SeedRecoveryJournal(path).Find(w,c,11,4).Conflict,"conflict survives restart");
        C(journal.RecordConflict(w,c,11,2,exact2,owner),"conflict consumed history retained");
        C(journal.Find(w,c,11,2).Phase==SeedRecoveryPhase.Consumed&&journal.Find(w,c,11,2).CorrectionPhase==SeedRecoveryPhase.Consumed,"effect history unchanged");
        C(!journal.TryReserveSettlement(w,c,11,2,exact2,owner,out duplicate),"conflict compensation refuses");
        C(journal.Retain(original(6),fresh(6)),"active reserved conflict fixture");
        C(journal.RecordEvidence(w,c,11,6,fresh(6),SeedPlacementOutcome.RejectedBeforeMutation,true,false,owner),"active reserved evidence");
        C(journal.TryReserveCorrection(w,c,11,6,fresh(6),owner,out duplicate),"active correction reserved");
        C(journal.RecordConflict(w,c,11,6,fresh(6),owner),"conflict during reserved effect");
        C(journal.CompleteReservation(w,c,11,6,duplicate,SeedRecoveryEffect.PredictionCorrection),"mark alreadydone effect consumed despite conflict");
        C(journal.Find(w,c,11,6).Conflict,"completion cannot clear conflict");
        var file=Directory.GetFiles(path,"*.json")[0];File.WriteAllText(file,"{}");bool corrupt=false;
        try{journal.Find(w,c,11,1);}catch(System.IO.InvalidDataException){corrupt=true;}
        C(corrupt,"corrupt data fails closed retained");
        Console.WriteLine("PASS "+checks+" durable journal fixture checks; DTO authority/codec/native observations doubled, no native execution");
    }
}





