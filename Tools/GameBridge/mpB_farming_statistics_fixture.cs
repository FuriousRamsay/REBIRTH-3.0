// Actual original statistics API bodies. Repo publication, native actor/world,
// archived proof and milestone/daily helpers are disclosed doubles.
using System;using System.Linq;using System.Collections.Generic;
class World {public bool Remote;public object Actor;public bool IsRemote()=>Remote;public object GetEntity(int id)=>Actor;}
class EntityPlayer {public int entityId;public World world;}
static class GameManager {public static Manager Instance=new Manager();public class Manager {public World World;}}
static class ThreadManager {public static bool IsMainThread()=>true;}
static class RebirthSurvivorMode {public static bool IsEnabledForCurrentWorld()=>true;}
class RebirthStatisticsDailyBucket {public long ResourcesGathered;}
class RebirthStatisticsRecord {public sealed class FarmingOriginalReceipt {public string Item;public int Count,Day;public bool Applied;}public Dictionary<string,FarmingOriginalReceipt> FarmingOriginalReceipts=new Dictionary<string,FarmingOriginalReceipt>();public long ResourcesGathered,BestResourcesInDay;public bool Dirty;public void Touch(string why){Dirty=true;}}
static class RebirthStatisticsRepository {public static RebirthStatisticsRecord Record=new RebirthStatisticsRecord();public static bool Durable=true;public static int Saves;public static RebirthStatisticsRecord GetOrCreate(EntityPlayer p)=>Record;public static bool SaveIfDirty(EntityPlayer p,string why){Saves++;if(!Durable)return false;Record.Dirty=false;return true;}}
static class AdvancedFarmingHarvestEvents {public static bool Archived;public static bool StatisticsArchived(EntityPlayer p,string op,string item,int count)=>Archived;}
static class RebirthStatisticsService {
 private static RebirthStatisticsDailyBucket Bucket=new RebirthStatisticsDailyBucket();private static int Day=9;private static RebirthStatisticsDailyBucket GetDaily(RebirthStatisticsRecord r,int day){if(day!=9)throw new Exception("original day lost");return Bucket;}private static int GetWorldDay(EntityPlayer p)=>Day;private static bool EvaluateMilestones(RebirthStatisticsRecord r,int day)=>false;
// PRODUCTION_METHODS
 internal static void Run(){var w=new World();var p=new EntityPlayer{world=w};w.Actor=p;GameManager.Instance.World=w;var r=RebirthStatisticsRepository.Record;string op=Guid.NewGuid().ToString("N");
  RebirthStatisticsRepository.Durable=false;Check(!PrepareResourcesGatheredOriginal(p,"plot",1,op)&&r.ResourcesGathered==0,"failed prepare allowed effect");RebirthStatisticsRepository.Durable=true;Check(PrepareResourcesGatheredOriginal(p,"plot",1,op),"prepare retry failed");Day=20;
  RebirthStatisticsRepository.Durable=false;Check(!RecordResourcesGatheredOriginal(p,"plot",1,op)&&r.ResourcesGathered==1,"effect failure not retained");Check(!RecordResourcesGatheredOriginal(p,"plot",2,op)&&r.ResourcesGathered==1,"original count substitution accepted");RebirthStatisticsRepository.Durable=true;Check(RecordResourcesGatheredOriginal(p,"plot",1,op)&&r.ResourcesGathered==1,"effect retry duplicated");
  Check(!RetireResourcesGatheredOriginal(p,op)&&r.FarmingOriginalReceipts.ContainsKey(op),"receipt discarded without archive proof");AdvancedFarmingHarvestEvents.Archived=true;Check(RetireResourcesGatheredOriginal(p,op)&&!r.FarmingOriginalReceipts.ContainsKey(op),"proven completed receipt not compacted");Check(RecordResourcesGatheredOriginal(p,"plot",1,op)&&r.ResourcesGathered==1,"archived operation replayed counter");
  AdvancedFarmingHarvestEvents.Archived=false;w.Remote=true;Check(!PrepareResourcesGatheredOriginal(p,"plot",1,Guid.NewGuid().ToString("N")),"remote owner authority accepted");w.Remote=false;Check(!PrepareResourcesGatheredOriginal(p,"plot",1,"bad"),"invalid original identity accepted");
 }
 private static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
}
static class Fixture {static void Main(){RebirthStatisticsService.Run();Console.WriteLine("PASS actual farming original statistics API: prepare/effect failure, exact binding, retry idempotence, saved archive-only compaction and owner refusal; repository/archive proof doubled");}}
