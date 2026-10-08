using System;
class World{}class EntityPlayerLocal{public int Id=7;}
static class NativeScope{public static World World;public static EntityPlayerLocal Player;public static object Peer;public static string Creation;public static bool Allowed=true;}
public class RebirthBackpackLibraryReceipt{public string CreationId,TransactionId;public long ExpectedGearRevision;}
class Program{static int n;static void Check(bool b,string label){if(!b)throw new Exception(label);n++;}
static void Main(){var world=new World();var player=new EntityPlayerLocal();var peer=new object();var creation=Guid.NewGuid().ToString("N");var tx=Guid.NewGuid();var receipt=new RebirthBackpackLibraryReceipt{CreationId=creation,TransactionId=tx.ToString("N"),ExpectedGearRevision=4};var result=RebirthBackpackLibrarySettlement.Create(receipt,true);
NativeScope.World=world;NativeScope.Player=player;NativeScope.Peer=peer;NativeScope.Creation=creation;ActualSettlementReadback.Seed(world,player,peer,creation,result);
Check(ActualSettlementReadback.TryGetSettledRevision(world,7,out var revision)&&revision==5,"confirmed exact original revision exposed read-only");
Check(!ActualSettlementReadback.TryGetSettledRevision(world,8,out revision)&&revision==-1,"foreign recipient refused");
NativeScope.Peer=new object();Check(!ActualSettlementReadback.TryGetSettledRevision(world,7,out revision)&&revision==-1,"replacement peer refuses old settlement");NativeScope.Peer=peer;
NativeScope.Player=new EntityPlayerLocal();Check(!ActualSettlementReadback.TryGetSettledRevision(world,7,out revision),"replacement actor refuses old settlement");NativeScope.Player=player;
NativeScope.World=new World();Check(!ActualSettlementReadback.TryGetSettledRevision(NativeScope.World,7,out revision),"same actor peer in replacement world refuses old settlement");NativeScope.World=world;
NativeScope.Creation=Guid.NewGuid().ToString("N");Check(!ActualSettlementReadback.TryGetSettledRevision(world,7,out revision),"replacement character refuses old settlement");NativeScope.Creation=creation;
NativeScope.Allowed=false;Check(!ActualSettlementReadback.TryGetSettledRevision(world,7,out revision),"failed native resolve refuses hint");NativeScope.Allowed=true;
Check(!ActualSettlementReadback.TryGetSettlement(world,7,Guid.NewGuid(),out _),"foreign original transaction refuses settlement");
ActualSettlementReadback.Seed(world,player,peer,creation,RebirthBackpackLibrarySettlement.Create(receipt,false));Check(ActualSettlementReadback.TryGetSettledRevision(world,7,out revision)&&revision==5,"confirmed rejection also advances authoritative shared revision");
ActualSettlementReadback.Seed(world,player,peer,creation,null);Check(!ActualSettlementReadback.TryGetSettledRevision(world,7,out revision)&&revision==-1,"absent terminal cannot invent revision");
Console.WriteLine($"PASS {n} actual extracted settlement readback/current-world guards and real terminal record; native Resolve and receipt adapter doubled, no full receive/release dispatch");}}