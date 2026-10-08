using System;
// Adversarial source only. Compile/run ONLY after fresh HAV3N check and root compiler slot.
public static class SeedDebitReceiptFixture
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    public static void Main()
    {
        ulong epoch=11,nonce=12;var world=new object();var player=new object();var action=new object();var source=new object();
        byte[] seed={1,2,3};
        Func<int,byte[],bool,SeedDebitObservation> obs=(count,bytes,reentrant)=>new SeedDebitObservation(epoch,nonce,0,count,bytes,world,player,action,source,reentrant);
        Func<SeedDebitReceipt> capture=()=>new SeedDebitReceipt(epoch,nonce,0,2,seed,world,player,action,source);
        var receipt=capture();seed[0]=99;Check(receipt.MatchesOriginal(obs(2,new byte[]{1,2,3},false)),"defensive input copy");
        var leaked=receipt.CanonicalSeed;leaked[0]=42;Check(receipt.CanonicalSeed[0]==1,"defensive output copy");seed[0]=1;
        var exact=receipt.ObserveOriginalDebit(obs(2,seed,false),obs(1,seed,false),true,SeedPlacementOutcome.Unknown);
        Check(exact.Receipt.State==SeedDebitState.ExactDebitObserved&&exact.Decision==SeedDebitDecision.PreserveCustody,"unknown retains debit evidence");
        Check(exact.Receipt.Resolve(SeedPlacementOutcome.RejectedBeforeMutation,true).Decision==SeedDebitDecision.RefundEligible,"rejection with proven debit");
        Check(exact.Receipt.Resolve(SeedPlacementOutcome.Committed).Decision==SeedDebitDecision.CommittedDebit,"committed seed retained");
        Check(receipt.Resolve(SeedPlacementOutcome.RejectedBeforeMutation,true).Decision==SeedDebitDecision.PreserveCustody,"no refund before proof");
        Check(exact.Receipt.ObserveOriginalDebit(obs(2,seed,false),obs(1,seed,false),true,SeedPlacementOutcome.RejectedBeforeMutation,true).Decision==SeedDebitDecision.DuplicateWitness,"duplicate no repeat");
        foreach(var fault in new[]{"beforeSeed","afterSeed","count","reentrant","noSetter","scope","nonce","slot"})
        {
            var before=obs(2,seed,false);var after=obs(1,seed,false);bool setter=true;
            if(fault=="beforeSeed")before=obs(2,new byte[]{9},false);
            if(fault=="afterSeed")after=obs(1,new byte[]{9},false);
            if(fault=="count")after=obs(0,null,false);
            if(fault=="reentrant")after=obs(1,seed,true);
            if(fault=="noSetter")setter=false;
            if(fault=="scope")after=new SeedDebitObservation(epoch,nonce,0,1,seed,new object(),player,action,source);
            if(fault=="nonce")after=new SeedDebitObservation(epoch,(nonce+1),0,1,seed,world,player,action,source);
            if(fault=="slot")after=new SeedDebitObservation(epoch,nonce,1,1,seed,world,player,action,source);
            var failed=capture().ObserveOriginalDebit(before,after,setter,SeedPlacementOutcome.RejectedBeforeMutation);
            Check(failed.Decision==SeedDebitDecision.PreserveCustody&&failed.Receipt.State==SeedDebitState.HeldUncertain,fault+" held");
            Check(failed.Receipt.ObserveOriginalDebit(obs(2,seed,false),obs(1,seed,false),true,SeedPlacementOutcome.RejectedBeforeMutation,true).Decision==SeedDebitDecision.PreserveCustody,fault+" cannot restart uncertain");
        }
        var single=new SeedDebitReceipt(epoch,nonce,0,1,seed,world,player,action,source);
        Check(single.ObserveOriginalDebit(obs(1,seed,false),obs(0,null,false),true,SeedPlacementOutcome.RejectedBeforeMutation,true).Decision==SeedDebitDecision.RefundEligible,"last seed native empty postimage");
        Check(single.ObserveOriginalDebit(obs(1,seed,false),obs(0,new byte[]{9},false),true,SeedPlacementOutcome.RejectedBeforeMutation,true).Decision==SeedDebitDecision.PreserveCustody,"last seed substitute bytes rejected");
        Check(capture().ObserveOriginalDebit(null,null,true,SeedPlacementOutcome.RejectedBeforeMutation,true).Decision==SeedDebitDecision.PreserveCustody,"missing witness");
        bool rejected=false;try{new SeedDebitReceipt(0,nonce,0,1,seed,world,player,action,source);}catch(ArgumentException){rejected=true;}
        Check(rejected,"empty epoch capture refused");
        Check(exact.Receipt.Resolve(SeedPlacementOutcome.RejectedBeforeMutation).Decision==SeedDebitDecision.PreserveCustody,"unproven no-world-effect held");
        Check(exact.Receipt.Resolve(SeedPlacementOutcome.Committed,true).Decision==SeedDebitDecision.PreserveCustody,"contradictory committed no-world-effect held");
        Check(exact.Receipt.Resolve(SeedPlacementOutcome.RejectedBeforeMutation,true,true).Decision==SeedDebitDecision.PreserveCustody,"partial mutation cannot refund");
        Console.WriteLine("PASS "+checks+" pure receipt fixture checks; no native execution");
    }
}

