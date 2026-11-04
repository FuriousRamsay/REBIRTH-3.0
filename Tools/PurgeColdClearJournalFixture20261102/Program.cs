using System;
using System.Collections.Generic;

enum RebirthPoiClearanceState { Discovered,Cleared,ResetPending }
enum RebirthPoiStoreResult { Published,Duplicate,Refused }
sealed class RebirthPoiActorObservation
{
    public static int Reads;
    public Guid Token=Guid.NewGuid(),DeathReceipt=Guid.NewGuid();
    public string Digest="original",Contributor="owner";public long DeathTime=50;public int TerminalKind=1;
    public string CausalDigest {get{Reads++;return Digest;}}
}
sealed class VolumeObservation
{public string Descriptor="room";public Dictionary<Guid,RebirthPoiActorObservation> Actors=new();}
sealed class RebirthPoiPartialObservation
{public long Epoch=7;public Dictionary<int,VolumeObservation> Volumes=new();}
sealed class RebirthPoiClearanceRecord
{public RebirthPoiClearanceState State=RebirthPoiClearanceState.Cleared;public long Epoch=7;public RebirthPoiPartialObservation Observations;}
sealed class RebirthPoiRepopulationEvidence(Guid generation,int live,ulong time)
{public Guid Generation=generation;public int Live=live;public ulong Time=time;}
static class RebirthPoiNativeRestorationConnection
{public static bool Pending;public static bool HasPending(string identity)=>Pending;}

partial class Probe
{
    sealed class Room{}
    sealed class Binding {public int Id=1;public string Descriptor="room";}
    sealed class Actor {public RebirthPoiActorObservation Observation;public List<RebirthPoiActorObservation> Versions=new();}
    sealed class World {public ulong worldTime=123;}
    sealed class Candidate
    {
        public Dictionary<Room,Binding> Bindings=new();
        public Dictionary<Room,Dictionary<int,Actor>> Actors=new();
        public Dictionary<int,VolumeObservation> Unbound=new();
        public bool JournalDirty=true;public string Identity="poi";public long? Epoch=7;
        public Guid Repopulation=Guid.NewGuid(),Proof=Guid.NewGuid();public World World=new();
        public RebirthPoiRepopulationEvidence PendingRepopulation;public object PendingClear=new();
    }
    sealed class Store
    {
        public bool Pending;public int Writes,PendingReads;public RebirthPoiStoreResult Result=RebirthPoiStoreResult.Published;
        public RebirthPoiRepopulationEvidence LastEvidence;
        public bool HasPending {get{PendingReads++;return Pending;}}
        public RebirthPoiStoreResult TryObserveRepopulation(object snapshot,string identity,long epoch,RebirthPoiRepopulationEvidence evidence)
        {Writes++;LastEvidence=evidence;return Result;}
    }
    static bool PendingDeaths;static int DirtyCalls,checks,failed;
    static bool HasPendingDeaths(string identity)=>PendingDeaths;
    static void Dirty(Candidate candidate)=>DirtyCalls++;
    sealed class Setup
    {
        public Candidate C=new();public Store S=new();public RebirthPoiClearanceRecord R=new();public Actor A;
        public Setup()
        {
            PendingDeaths=RebirthPoiNativeRestorationConnection.Pending=false;DirtyCalls=0;RebirthPoiActorObservation.Reads=0;
            var room=new Room();A=new Actor{Observation=new()};C.Bindings.Add(room,new Binding());C.Actors.Add(room,new(){{11,A}});
            var volume=new VolumeObservation();volume.Actors.Add(A.Observation.Token,A.Observation);
            R.Observations=new();R.Observations.Volumes.Add(1,volume);
        }
        public void Run(int live=0)=>Process(C,R,S,live);
    }
    static void Must(bool value,string reason){if(!value)throw new Exception(reason);}
    static void Test(string name,Action body){checks++;try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
    static void Retains(Action<Setup> alter){var x=new Setup();alter(x);x.Run();Must(x.C.JournalDirty&&x.S.Writes==0,"unacknowledged state retired / written");}
    static void Main(string[] args)
    {
        Test("cold cleared exact journal becomes clean without a write",()=>{var x=new Setup();var proof=x.R.Observations;var epoch=x.C.Epoch;x.Run();Must(!x.C.JournalDirty&&ReferenceEquals(proof,x.R.Observations)&&x.C.Epoch==epoch&&x.S.Writes==0&&DirtyCalls==0,"cold journal still dirty or authority changed");});
        Test("cold candidate satisfies unchanged clean-journal reset prerequisite",()=>{var x=new Setup();for(int i=0;i<66&&x.C.JournalDirty;i++)x.Run();Must(!x.C.JournalDirty&&AcknowledgedLatest(x.C,x.R.Observations)&&x.S.Writes==0,"bounded drain cannot finish");});
        Test("pending publication retains dirty journal",()=>Retains(x=>x.S.Pending=true));
        Test("pending native death retains dirty journal",()=>Retains(x=>PendingDeaths=true));
        Test("pending native restoration retains dirty journal",()=>Retains(x=>RebirthPoiNativeRestorationConnection.Pending=true));
        Test("missing published observations retains dirty journal",()=>Retains(x=>x.R.Observations=null));
        Test("wrong observation epoch retains dirty journal",()=>Retains(x=>x.R.Observations.Epoch=6));
        Test("different room descriptor retains dirty journal",()=>Retains(x=>x.R.Observations.Volumes[1].Descriptor="different"));
        Test("different volume count retains dirty journal",()=>Retains(x=>x.R.Observations.Volumes.Add(2,new())));
        Test("missing actor token retains dirty journal",()=>Retains(x=>x.R.Observations.Volumes[1].Actors.Clear()));
        Test("different causal proof retains dirty journal",()=>Retains(x=>{var old=x.A.Observation;var other=new RebirthPoiActorObservation{Token=old.Token,Digest="different",DeathReceipt=old.DeathReceipt};x.R.Observations.Volumes[1].Actors[old.Token]=other;}));
        Test("acknowledged older actor version does not retire latest dirty proof",()=>Retains(x=>{var old=x.A.Observation;x.A.Versions.Add(old);x.A.Observation=new RebirthPoiActorObservation{Token=old.Token,Digest="latest"};}));
        Test("10000 clean pulses do not reread actor proofs or pending store",()=>{var x=new Setup();x.C.JournalDirty=false;for(int i=0;i<10000;i++)x.Run();Must(!x.C.JournalDirty&&RebirthPoiActorObservation.Reads==0&&x.S.PendingReads==0&&x.S.Writes==0,"clean path added repeated work");});
        Test("discovered journal does not use cleared acknowledgment",()=>Retains(x=>x.R.State=RebirthPoiClearanceState.Discovered));
        Test("live native participant still follows repopulation publication",()=>{var x=new Setup();x.Run(1);Must(x.S.Writes==1&&x.C.Epoch==8&&x.C.JournalDirty&&x.C.PendingClear==null&&DirtyCalls==1,"live repopulation changed");});
        Test("failed live repopulation retains epoch and original evidence",()=>{var x=new Setup();x.S.Result=RebirthPoiStoreResult.Refused;var epoch=x.C.Epoch;x.Run(1);Must(x.S.Writes==1&&x.C.Epoch==epoch&&x.C.JournalDirty&&x.C.PendingRepopulation!=null&&DirtyCalls==1,"refused native repopulation lost custody");});
        Test("existing repopulation evidence is not acknowledged as empty",()=>{var x=new Setup();var old=new RebirthPoiRepopulationEvidence(Guid.NewGuid(),2,122);x.C.PendingRepopulation=old;x.S.Result=RebirthPoiStoreResult.Refused;x.Run();Must(x.S.Writes==1&&ReferenceEquals(x.S.LastEvidence,old)&&ReferenceEquals(x.C.PendingRepopulation,old)&&x.C.JournalDirty,"pending repopulation bypassed");});
        Console.WriteLine($"{checks-failed}/{checks} passed; {failed} failures");
        Environment.ExitCode=args.Length>0&&args[0]=="baseline"?(failed==2?0:1):(failed==0?0:1);
    }
}
