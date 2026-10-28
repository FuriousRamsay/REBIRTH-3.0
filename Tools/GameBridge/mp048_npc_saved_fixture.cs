// The whole read-only checkpoint producer is injected. Backing schema/row
// decoder, clean-backing predicate and observed generation are explicit adapters;
// fixture XML I/O is not native publication or an authenticated trade admission.
class RebirthNpcInventoryPersistentRecord
{
 public RebirthNpcStableId NpcId;public uint Revision;public Dictionary<string,int> Quantities,Reservations;
 public RebirthNpcNativeStackSet NativeStacks;public Dictionary<Guid,string> LogisticsEffects;public Guid[] ReplayJournal;
}
public static partial class RebirthNpcInventoryPersistenceStore
{
 private static readonly object Sync=new object();private static bool BoundCurrent=true,dirty=false,CanRetireBacking=true;private static string boundPath;
 private static RebirthNpcInventoryPersistentRecord saved;
 private static bool ValidateDocument(XmlDocument document)=>document.DocumentElement?.Name=="inventorySet";
 private static RebirthNpcInventoryPersistentRecord Read(XmlElement row)=>saved;
// ACTUAL_TRADE_NPC_SAVED
 private static RebirthNpcInventoryPersistentRecord Record(RebirthNpcInventorySnapshot snapshot,params Guid[] ids)=>new RebirthNpcInventoryPersistentRecord
 {NpcId=snapshot.NpcId,Revision=snapshot.Revision,Quantities=snapshot.Quantities,Reservations=snapshot.Reservations,NativeStacks=snapshot.NativeStacks,LogisticsEffects=snapshot.LogisticsEffects,ReplayJournal=ids};
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 internal static void Run(RebirthNpcTradeOriginal original)
 {
  string directory=Path.Combine(Path.GetTempPath(),"rebirth-trade-saved-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
  try
  {
   boundPath=Path.Combine(directory,"npc.xml");File.WriteAllText(boundPath,"<inventorySet><inventory stableId=\"npc\" /></inventorySet>");
   var world=new TradeWorld();world.worldState.Guid=original.World;GameManager.Instance.World=world;
   var snapshot=new RebirthNpcInventorySnapshot{NpcId=original.CopyQuote().NpcId,Revision=1,Quantities=new Dictionary<string,int>{{"goods",10},{"currency",20}},
    Reservations=new Dictionary<string,int>(),LogisticsEffects=new Dictionary<Guid,string>()};
   Need(RebirthNpcTradeNpcPlan.TryCreate(original,snapshot,out var plan),"saved NPC plan missing");
   RebirthNpcInventoryTransactionService.Snapshot=plan.CopySnapshot(false);saved=Record(plan.CopySnapshot(false));
   Need(CaptureTradeSaved(original,plan,false)!=null,"exact saved/current NPC Before refused");
   string savedBefore=File.ReadAllText(boundPath);
   Need(TryReadTradePlanFromSavedBefore(original,savedBefore,out var historicalPlan)&&historicalPlan.Matches(plan.CopySnapshot(true),true),"historical original NPC plan not reconstructed");
   Need(!TryReadTradePlanFromSavedBefore(original,"<foreign />",out _),"foreign history schema reconstructed");
   Need(!TryReadTradePlanFromSavedBefore(original,"<inventorySet><inventory stableId=\"other\" /></inventorySet>",out _),"foreign history owner reconstructed");
   Need(!TryReadTradePlanFromSavedBefore(original,"<inventorySet><inventory stableId=\"npc\" /><inventory stableId=\"npc\" /></inventorySet>",out _),"duplicate history owner reconstructed");
   saved.Reservations["goods"]=1;Need(!TryReadTradePlanFromSavedBefore(original,savedBefore,out _),"reserved history reconstructed as exclusive trade");saved.Reservations.Clear();
   saved.ReplayJournal=new[]{original.Operation};Need(!TryReadTradePlanFromSavedBefore(original,savedBefore,out _),"applied history reconstructed as Before");
   saved.ReplayJournal=null;Need(!TryReadTradePlanFromSavedBefore(original,savedBefore,out _),"unknown replay history reconstructed");saved.ReplayJournal=new Guid[0];
   saved.Revision++;Need(!TryReadTradePlanFromSavedBefore(original,savedBefore,out _),"wrong revision history reconstructed");saved.Revision--;
   dirty=true;Need(CaptureTradeSaved(original,plan,false)==null,"dirty backing certified");dirty=false;
   CanRetireBacking=false;Need(CaptureTradeSaved(original,plan,false)==null,"unqualified complete backing certified");CanRetireBacking=true;
   RebirthNpcRecoveryGeneration.Known=false;Need(CaptureTradeSaved(original,plan,false)==null,"unknown observed generation certified");RebirthNpcRecoveryGeneration.Known=true;
   saved.ReplayJournal=new[]{original.Operation};Need(CaptureTradeSaved(original,plan,false)==null,"already applied ID accepted as original Before");
   saved=Record(plan.CopySnapshot(true));RebirthNpcInventoryTransactionService.Snapshot=plan.CopySnapshot(true);
   Need(CaptureTradeSaved(original,plan,true)==null,"matching saved After without original replay ID certified");
   saved.ReplayJournal=new[]{original.Operation};Need(CaptureTradeSaved(original,plan,true)!=null,"full saved After and original mutation ID refused");
   string retained=File.ReadAllText(boundPath);
   Need(MatchesTradeSavedImage(original,plan,retained,true),"retained full original After refused");
   Need(!MatchesTradeSavedImage(original,plan,retained,false),"saved After classified as Before");
   Need(!MatchesTradeSavedImage(original,plan,null,true),"missing saved image certified");
   Need(!MatchesTradeSavedImage(original,plan,"<foreign />",true),"foreign saved schema certified");
   Need(!MatchesTradeSavedImage(original,plan,"<inventorySet><inventory stableId=\"other\" /></inventorySet>",true),"foreign NPC row certified");
   Need(!MatchesTradeSavedImage(original,plan,"<inventorySet><inventory stableId=\"npc\" /><inventory stableId=\"npc\" /></inventorySet>",true),"duplicate retained NPC row certified");
   Need(!MatchesTradeSavedImage(original,plan,"<!DOCTYPE inventorySet [<!ENTITY x 'x'>]><inventorySet>&x;</inventorySet>",true),"DTD retained image certified");
   saved.ReplayJournal=new[]{Guid.NewGuid()};Need(!MatchesTradeSavedImage(original,plan,retained,true),"foreign saved effect ID certified");
   saved=Record(plan.CopySnapshot(true),original.Operation);
   Need(MatchesTradeSavedImage(original,plan,"<inventorySet><inventory stableId=\"other\" /><inventory stableId=\"npc\" /></inventorySet>",true),"unrelated saved NPC row invalidated original row");
   saved.Quantities["currency"]++;Need(CaptureTradeSaved(original,plan,true)==null,"changed saved NPC balance certified");saved=Record(plan.CopySnapshot(true),original.Operation);
   RebirthNpcInventoryTransactionService.Snapshot.Quantities["goods"]++;Need(CaptureTradeSaved(original,plan,true)==null,"changed live NPC balance certified");
   RebirthNpcInventoryTransactionService.Snapshot=plan.CopySnapshot(true);world.Remote=true;Need(CaptureTradeSaved(original,plan,true)==null,"remote view certified as authoritative NPC backing");world.Remote=false;
   File.WriteAllText(boundPath,"<inventorySet><inventory stableId=\"npc\" /><inventory stableId=\"npc\" /></inventorySet>");
   Need(CaptureTradeSaved(original,plan,true)==null,"ambiguous saved NPC row certified");
  }
  finally
  {
   GameManager.Instance.World=null;RebirthNpcRecoveryGeneration.Known=true;RebirthNpcRecoveryGeneration.Generation=1;
   dirty=false;BoundCurrent=true;CanRetireBacking=true;boundPath=null;saved=null;
   if(Path.GetFileName(directory).StartsWith("rebirth-trade-saved-",StringComparison.Ordinal)&&Path.GetDirectoryName(Path.GetFullPath(directory))==Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar))Directory.Delete(directory,true);
   else throw new InvalidDataException("Unsafe fixture directory");
  }
 }
}
