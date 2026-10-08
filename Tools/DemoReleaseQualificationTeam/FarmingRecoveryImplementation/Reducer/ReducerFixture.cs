using System;
using R = FarmingPredictionRecoveryReducer;
internal static class ReducerFixture
{
    static int checks;
    static readonly object World = new object(), Player = new object(), Action = new object(), Source = new object();
    static readonly byte[] Seed = new byte[] { 1, 2, 3, 4 };
    static readonly R.Correlation Correlation = new R.Correlation(11, 22);
    static readonly R.Context Context = new R.Context(World, Player, Action);
    static readonly R.TargetWitness Old = new R.TargetWitness(3,4,5,7,0,0,"raw:0/damage:0");
    static readonly R.TargetWitness Predicted = new R.TargetWitness(3,4,5,7,8,1,"raw:42/damage:0");
    static readonly R.TargetWitness Committed = new R.TargetWitness(3,4,5,7,90,2,"raw:42/damage:0");
    static SeedDebitObservation Observation(int count, byte[] seed = null, object source = null, ulong nonce = 22, bool reentrant = false)
    { return new SeedDebitObservation(11,nonce,1,count,seed ?? Seed,World,Player,Action,source ?? Source,reentrant); }
    static SeedDebitReceipt Capture() { return new SeedDebitReceipt(11,22,1,3,Seed,World,Player,Action,Source); }
    static R.DebitReceipt NoDebit(bool settled = true) { return new R.DebitReceipt(Capture(),Observation(3),settled,false); }
    static R.DebitReceipt Debited(int currentCount = 2, byte[] currentSeed = null, object source = null, bool consumed = false, bool settled = true)
    { var receipt = Capture().ObserveOriginalDebit(Observation(3),Observation(2),true,SeedPlacementOutcome.Unknown).Receipt;
      return new R.DebitReceipt(receipt,Observation(currentCount,currentSeed,source),settled,consumed); }
    static R.RecoveryObservation O(R.Outcome outcome = R.Outcome.RejectedBeforeMutation, R.TargetWitness current = null, R.TargetWitness authoritative = null,
        R.DebitReceipt debit = null, R.Correlation reply = null, R.Context context = null, bool prediction = true, bool sent = true,
        bool proof = true, bool neverStarted = true, bool handled = false, bool complete = false, bool nullDebit = false, R.TargetWitness predicted = null)
    { return new R.RecoveryObservation(outcome,Correlation,reply ?? Correlation,Context,context ?? Context,Old,predicted ?? Predicted,current ?? Predicted,authoritative ?? Old,
        prediction,sent,proof,neverStarted,handled,complete,Convert.ToBase64String(Seed),1,nullDebit ? null : debit ?? NoDebit()); }
    static void Check(bool value,string name) { if(!value)throw new Exception("FAIL: "+name);checks++;Console.WriteLine("PASS "+name); }
    static void Holds(R.RecoveryObservation o,string name) { var d=R.Decide(o).Decisions;Check((d&R.Decision.Hold)!=0&&(d&(R.Decision.CorrectExactPrediction|R.Decision.ConsiderExactDebitCompensation|R.Decision.AcceptObservedCommit))==0,name); }
    static void Main()
    {
        Check(R.Decide(O()).Decisions==R.Decision.CorrectExactPrediction,"exact rejected prediction settled no debit correct-only");
        Check(R.Decide(O(debit:Debited())).Decisions==(R.Decision.CorrectExactPrediction|R.Decision.ConsiderExactDebitCompensation),"exact witnessed debit permits consideration only");
        Holds(null,"missing observation");
        Holds(O(reply:new R.Correlation(12,22)),"wrong epoch");
        Holds(O(reply:new R.Correlation(11,23)),"wrong nonce");
        Holds(O(reply:new R.Correlation(0,22)),"zero epoch");
        Holds(O(context:new R.Context(new object(),Player,Action)),"world replaced");
        Holds(O(context:new R.Context(World,new object(),Action)),"player replaced");
        Holds(O(context:new R.Context(World,Player,new object())),"action replaced");
        Holds(O(handled:true,debit:Debited()),"duplicate handled reply");
        Holds(O(current:new R.TargetWitness(3,4,5,8,8,1,"raw:42/damage:0")),"world generation changed");
        Holds(O(current:new R.TargetWitness(3,4,5,7,9,1,"raw:42/damage:0")),"plant incarnation replaced");
        Holds(O(current:new R.TargetWitness(3,4,5,7,8,2,"raw:42/damage:0")),"newer revision");
        Holds(O(current:new R.TargetWitness(3,4,5,7,8,1,"raw:42/damage:1")),"same type changed damage");
        Holds(O(current:new R.TargetWitness(4,4,5,7,8,1,"raw:42/damage:0")),"target moved");
        Holds(O(R.Outcome.Indeterminate,debit:Debited()),"unknown commit preserves debit custody");
        Holds(O(neverStarted:false,debit:Debited()),"generic rejection no no-mutation proof");
        Holds(O(proof:false,debit:Debited()),"unproven authoritative outcome");
        Holds(O(complete:true,debit:Debited()),"rejection with commit proof contradiction");
        Holds(O(authoritative:Committed,debit:Debited()),"rejection server target changed");
        Holds(O(nullDebit:true),"unknown debit never assumed absent");
        Holds(O(debit:NoDebit(false)),"pending delayed native debit");
        Holds(O(debit:Debited(currentCount:1)),"postdebit count changed");
        Holds(O(debit:Debited(currentSeed:new byte[]{9})),"postdebit seed replacement");
        Holds(O(debit:Debited(source:new object())),"postdebit original source changed");
        Holds(O(debit:Debited(consumed:true)),"consumed receipt");
        Holds(O(debit:Debited(settled:false)),"observed debit still pending native action");
        var uncertain=Capture().ObserveOriginalDebit(Observation(3),Observation(2),false,SeedPlacementOutcome.Unknown).Receipt;
        Holds(O(debit:new R.DebitReceipt(uncertain,Observation(2),true,false)),"unwitnessed count delta not receipt");
        Holds(O(prediction:false,current:Old,debit:Debited()),"debit without captured prediction contradiction");
        Holds(O(prediction:false,current:Old),"no predicted effect requires no correction");
        Check(R.Decide(O(R.Outcome.Committed,current:Committed,authoritative:Committed,neverStarted:false,complete:true,debit:Debited())).Decisions==R.Decision.AcceptObservedCommit,"exact commit never compensates");
        Holds(O(R.Outcome.Committed,current:Committed,authoritative:Committed,neverStarted:true,complete:true),"commit with no mutation contradiction");
        Holds(O(R.Outcome.Committed,current:Committed,authoritative:Committed,neverStarted:false,complete:false),"commit incomplete");
        Holds(O(R.Outcome.Committed,current:Committed,authoritative:Committed,neverStarted:false,complete:true,sent:false),"commit no correlated send");
        Holds(O(R.Outcome.Committed,current:Predicted,authoritative:Committed,neverStarted:false,complete:true),"commit current stale prediction");
        Holds(O((R.Outcome)99),"unknown enum outcome");
        var first=R.Decide(O(debit:Debited()));var second=R.Decide(O(debit:Debited()));
        Check(first.Decisions==second.Decisions,"pure decisions deterministic no internal effects or registry");
        Check(Seed[0]==1&&Capture().State==SeedDebitState.Captured,"canonical input and captured receipt preserved");
        Console.WriteLine("PASS TOTAL "+checks+"; pure reducer and actual debit core with explicit witness doubles only; no native proof.");
    }
}
