using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// Live authority-thread reservation across generic station preparation/commit.
// A claim is not payment, queue acceptance, or a durable publication receipt.
public static class RebirthStationPreparationReservation
{
    private sealed class Claims
    {
        internal readonly Dictionary<Vector3i,Claim> Entries=new Dictionary<Vector3i,Claim>();
    }
    private sealed class Claim
    {
        internal string Owner,Job;
        internal RebirthStationGridAdmission Admission;
    }
    private static readonly object Sync=new object();
    private static readonly ConditionalWeakTable<World,Claims> Worlds=new ConditionalWeakTable<World,Claims>();
    private const int MaximumClaims=1024;
    public static bool TryAcquire(World world,RebirthWorldCharacterRecord owner,RebirthStationGridAdmission admission)
    {
        if(world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||
            !RebirthWorldCharacterRepository.IsServerAuthority||owner?.Progression==null||!owner.IsComplete||
            admission==null||!RebirthSurvivorRequestScope.Matches(admission.CreationId,owner.Origin?.CreationId))return false;
        var image=admission.Write();var position=new Vector3i((int)image.Attribute("x"),(int)image.Attribute("y"),(int)image.Attribute("z"));
        lock(Sync)
        {
            var claims=Worlds.GetOrCreateValue(world);
            Claim existing;
            if(claims.Entries.TryGetValue(position,out existing))
                return existing.Owner==owner.StablePlayerKey&&existing.Job==admission.JobId&&
                    System.Xml.Linq.XNode.DeepEquals(existing.Admission.Write(),image)&&
                    RebirthWorldCharacterRepository.HasExclusiveStationPreparation(owner,admission);
            if(claims.Entries.Count>=MaximumClaims||!RebirthWorldCharacterRepository.HasExclusiveStationPreparation(owner,admission))return false;
            claims.Entries.Add(position,new Claim{Owner=owner.StablePlayerKey,Job=admission.JobId,Admission=admission.Clone()});
            return true;
        }
    }
    // A failed/uncertain save retains BOTH intent and live reservation for recovery.
    public static bool TryRegister(World world,RebirthWorldCharacterRecord owner,RebirthStationGridAdmission admission,Func<bool> save)
    {
        if(save==null||owner?.Progression==null||admission==null||
            !RebirthStationPreparationPersistence.MatchesOwner(owner.Progression.StationPreparations,owner.Origin?.CreationId)||
            owner.Progression.StationPreparations.Count>=RebirthStationPreparationPersistence.MaximumRecords&&
                !owner.Progression.StationPreparations.ContainsKey(admission.JobId)||
            !TryAcquire(world,owner,admission))return false;
        return RebirthStationPreparationPersistence.TryRegister(owner.Progression.StationPreparations,
            owner.Origin.CreationId,admission,save);
    }
    // Dispatcher may release only after its terminal settlement save succeeds.
    // Never use timeout, lost UI lock, or a missing live queue as terminal evidence.
    public static bool TryReleaseSettled(World world,RebirthWorldCharacterRecord owner,RebirthStationGridAdmission admission,Func<bool> saveTerminal)
    {
        if(world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||
            !RebirthWorldCharacterRepository.IsServerAuthority||owner?.Progression==null||!owner.IsComplete||admission==null||saveTerminal==null||
            !RebirthSurvivorRequestScope.Matches(admission.CreationId,owner.Origin?.CreationId))return false;
        var image=admission.Write();var position=new Vector3i((int)image.Attribute("x"),(int)image.Attribute("y"),(int)image.Attribute("z"));
        lock(Sync)
        {
            Claims claims;Claim claim;
            if(!Worlds.TryGetValue(world,out claims)||!claims.Entries.TryGetValue(position,out claim)||
                claim.Owner!=owner.StablePlayerKey||claim.Job!=admission.JobId||
                !System.Xml.Linq.XNode.DeepEquals(claim.Admission.Write(),image)||
                owner.Progression.StationPreparations.ContainsKey(admission.JobId))return false;
            try{if(!saveTerminal())return false;}catch{return false;}
            claims.Entries.Remove(position);return true;
        }
    }
}public struct Vector3i { public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;} }
public class World {public bool Remote;public bool IsRemote(){return Remote;}}
public class GameManager {public static GameManager Instance=new GameManager();public World World;}
public class Origin {public string CreationId="owner";}
public class RebirthStationRefundArchive{public RebirthStationGridAdmission Admission;public System.Xml.Linq.XElement Write(){return new System.Xml.Linq.XElement("archive",new System.Xml.Linq.XElement("stationAdmission",Admission.Write().Attributes()));}}public class Progression {public System.Collections.Generic.Dictionary<string,RebirthStationRefundArchive> StationRefundArchives=new System.Collections.Generic.Dictionary<string,RebirthStationRefundArchive>();public System.Collections.Generic.Dictionary<string,object> StationPublications=new System.Collections.Generic.Dictionary<string,object>();public readonly System.Collections.Generic.Dictionary<string,RebirthStationGridAdmission> StationPreparations=new System.Collections.Generic.Dictionary<string,RebirthStationGridAdmission>();}
public class RebirthWorldCharacterRecord {public bool Dirty;public Progression Progression=new Progression();public Origin Origin=new Origin();public bool IsComplete=true;public string StablePlayerKey="a";}
public class RebirthStationGridAdmission {public bool IsPublicationAttempted;public bool TryMarkPublicationAttempted(out RebirthStationGridAdmission next){next=null;if(IsPublicationAttempted)return false;next=Clone();next.IsPublicationAttempted=true;return true;}public string CreationId="owner",JobId="job",Payload="one";public System.Xml.Linq.XElement Write(){return new System.Xml.Linq.XElement("stationAdmission",new System.Xml.Linq.XAttribute("x",1),new System.Xml.Linq.XAttribute("y",2),new System.Xml.Linq.XAttribute("z",3),new System.Xml.Linq.XAttribute("payload",Payload),new System.Xml.Linq.XAttribute("phase",IsPublicationAttempted?"attempted":"prepared"));}public RebirthStationGridAdmission Clone(){return (RebirthStationGridAdmission)MemberwiseClone();}}
public static class RebirthWorldCharacterRepository {public static bool IsServerAuthority=true,Exclusive=true;public static bool HasExclusiveStationPreparation(RebirthWorldCharacterRecord o,RebirthStationGridAdmission a){return Exclusive;}}
public static class RebirthSurvivorRequestScope {public static bool Matches(string a,string b){return a==b;}}
public static class RebirthStationPreparationPersistence {public const int MaximumRecords=64;public static bool MatchesOwner(System.Collections.Generic.IDictionary<string,RebirthStationGridAdmission> r,string c){return true;}public static bool TryRegister(System.Collections.Generic.IDictionary<string,RebirthStationGridAdmission> r,string c,RebirthStationGridAdmission a,System.Func<bool> s){r[a.JobId]=a;return s();}}
public static class RebirthWorldCharacterService {public static void MarkDirty(RebirthWorldCharacterRecord r,string why){r.Dirty=true;}}
public static class StationClaimFixture {
static void Check(bool b){if(!b)throw new System.Exception("claim assertion failed");}
public static string Run(){var w=new World();GameManager.Instance.World=w;var a=new RebirthWorldCharacterRecord();var b=new RebirthWorldCharacterRecord{StablePlayerKey="b"};var job=new RebirthStationGridAdmission();
Check(RebirthStationPreparationReservation.TryAcquire(w,a,job));
Check(!RebirthStationPreparationReservation.TryAcquire(w,b,job));
Check(RebirthStationPreparationReservation.TryAcquire(w,a,job));
Check(!RebirthStationPreparationReservation.TryAcquire(w,a,new RebirthStationGridAdmission{Payload="changed"}));
Check(!RebirthStationPreparationReservation.TryRegister(w,a,job,()=>false)&&a.Progression.StationPreparations.ContainsKey("job"));
Check(!RebirthStationPreparationReservation.TryReleaseSettled(w,a,job,()=>true));
a.Progression.StationPreparations.Clear();Check(!RebirthStationPreparationReservation.TryReleaseSettled(w,a,job,()=>false));
Check(!RebirthStationPreparationReservation.TryAcquire(w,b,job));
Check(RebirthStationPreparationReservation.TryReleaseSettled(w,a,job,()=>true));
Check(RebirthStationPreparationReservation.TryAcquire(w,b,job));var nextWorld=new World();GameManager.Instance.World=nextWorld;var owner=new RebirthWorldCharacterRecord();var prepared=new RebirthStationGridAdmission();RebirthStationGridAdmission attempted;
Check(RebirthStationPreparationReservation.TryRegister(nextWorld,owner,prepared,()=>true));
Check(!RebirthStationPreparationReservation.TryMarkPublicationAttempt(nextWorld,owner,prepared,next=>false,out attempted)&&attempted.IsPublicationAttempted&&owner.Dirty&&owner.Progression.StationPreparations["job"].IsPublicationAttempted);
Check(RebirthStationPreparationReservation.TryAcquire(nextWorld,owner,attempted)&&!RebirthStationPreparationReservation.TryAcquire(nextWorld,owner,prepared));
int callbacks=0;Check(!RebirthStationPreparationReservation.TryMarkPublicationAttempt(nextWorld,owner,prepared,next=>{callbacks++;return true;},out var repeated)&&callbacks==0);
Check(!RebirthStationPreparationReservation.TryAcquire(nextWorld,new RebirthWorldCharacterRecord{StablePlayerKey="foreign"},attempted));
nextWorld=new World();GameManager.Instance.World=nextWorld;owner=new RebirthWorldCharacterRecord();RebirthStationPreparationReservation.TryRegister(nextWorld,owner,prepared,()=>true);
Check(!RebirthStationPreparationReservation.TryMarkPublicationAttempt(nextWorld,owner,prepared,next=>{throw new System.Exception("save fault");},out attempted)&&attempted.IsPublicationAttempted&&owner.Dirty);
Check(!RebirthStationPreparationReservation.TryConsumeNativeAttempt(nextWorld,owner,attempted));
nextWorld=new World();GameManager.Instance.World=nextWorld;owner=new RebirthWorldCharacterRecord();RebirthStationPreparationReservation.TryRegister(nextWorld,owner,prepared,()=>true);
Check(RebirthStationPreparationReservation.TryMarkPublicationAttempt(nextWorld,owner,prepared,next=>true,out attempted));
Check(!RebirthStationPreparationReservation.TryConsumeNativeAttempt(nextWorld,new RebirthWorldCharacterRecord{StablePlayerKey="foreign"},attempted));
Check(RebirthStationPreparationReservation.TryConsumeNativeAttempt(nextWorld,owner,attempted));
Check(!RebirthStationPreparationReservation.TryConsumeNativeAttempt(nextWorld,owner,attempted));
nextWorld=new World();GameManager.Instance.World=nextWorld;owner=new RebirthWorldCharacterRecord();owner.Progression.StationPreparations["job"]=attempted.Clone();
Check(RebirthStationPreparationReservation.TryAcquire(nextWorld,owner,attempted)&&!RebirthStationPreparationReservation.TryConsumeNativeAttempt(nextWorld,owner,attempted));
owner.Progression.StationRefundArchives[attempted.JobId]=new RebirthStationRefundArchive{Admission=attempted};Check(!RebirthStationPreparationReservation.TryAcquire(nextWorld,owner,attempted));int saves=0;Check(!RebirthStationPreparationReservation.TryRegister(nextWorld,owner,attempted,()=>{saves++;return true;})&&saves==0);Check(!RebirthStationPreparationReservation.TryConsumeNativeAttempt(nextWorld,owner,attempted));
nextWorld=new World();GameManager.Instance.World=nextWorld;owner=new RebirthWorldCharacterRecord();Check(!RebirthStationPreparationReservation.TryReleaseSettled(nextWorld,owner,prepared,()=>true));owner.Progression.StationRefundArchives[prepared.JobId]=new RebirthStationRefundArchive{Admission=prepared};Check(!RebirthStationPreparationReservation.TryReleaseSettled(nextWorld,owner,prepared,()=>false));Check(RebirthStationPreparationReservation.TryReleaseSettled(nextWorld,owner,prepared,()=>true));Check(RebirthStationPreparationReservation.TryReleaseSettled(nextWorld,owner,prepared,()=>true));var other=new RebirthWorldCharacterRecord{StablePlayerKey="other"};Check(RebirthStationPreparationReservation.TryAcquire(nextWorld,other,prepared));Check(!RebirthStationPreparationReservation.TryReleaseSettled(nextWorld,owner,prepared,()=>true));Check(RebirthStationPreparationReservation.TryAcquire(nextWorld,other,prepared));
return "PASS32 actual reservation/attempt-phase checks with explicit authority/admission/persistence doubles; no native custody or disk save";
}}

public static class RebirthStationObservationDispatcher{public static bool IsCurrentAuthorityThread(World w){return true;}}
