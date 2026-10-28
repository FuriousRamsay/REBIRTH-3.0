 public static void Main(){
  var world=new World();GameManager.Instance.World=world;var actor=new EntityPlayer{entityId=1,world=world};var npc=new EntityRebirthNPC{entityId=2,world=world};world.Entities.Add(1,actor);world.Entities.Add(2,npc);
  var plan=new RebirthRemoteResourceNpcEffectPlan();RebirthRemoteResourceDurableOutcome.Plan=plan;current=new InventoryState{Revision=4};current.Quantities.Add("ammo",5);current.Reservations.Add("ammo",1);
  Check(CanAdmitRemoteResourceOriginalPlan(actor,plan.Operation,"N:original")&&current.Quantities["ammo"]==5,"preflight has no quantity effect");
  for(int gate=0;gate<3;gate++){
   RebirthNpcTradeService.Held=gate==0;RebirthNpcMetabolismCustody.Held=gate==1;RemoteResourceTransactions.Held=gate==2;
   Check(!CanAdmitRemoteResourceOriginalPlan(actor,plan.Operation,"N:original")&&current.Revision==4&&current.Quantities["ammo"]==5,"independent custody hold bypassed preflight "+gate);
  }
  RebirthNpcTradeService.Held=RebirthNpcMetabolismCustody.Held=RemoteResourceTransactions.Held=false;
  Check(ApplyRemoteResourceOriginalPlan(actor,plan.Operation,"N:original","mutate",out _)==RebirthNpcInventoryTransactionResult.InvalidRequest,"prepared intent is not attempted payment");plan.Attempted=1;
  Check(ApplyRemoteResourceOriginalPlan(actor,plan.Operation,"N:original","transfer",out _)==RebirthNpcInventoryTransactionResult.AuthorityDenied,"logical fetch retains Mutate purpose");
  RebirthCompanionInventoryLockService.Locked=true;Check(ApplyRemoteResourceOriginalPlan(actor,plan.Operation,"N:original","mutate",out _)==RebirthNpcInventoryTransactionResult.RevisionConflict,"current item lock prevents source debit");RebirthCompanionInventoryLockService.Locked=false;
  RebirthNpcOriginalEffectAdmission.Reserved=false;Check(ApplyRemoteResourceOriginalPlan(actor,plan.Operation,"N:original","mutate",out _)==RebirthNpcInventoryTransactionResult.InvalidRequest,"whole-original receipt capacity required");RebirthNpcOriginalEffectAdmission.Reserved=true;
  Check(ApplyRemoteResourceOriginalPlan(actor,plan.Operation,"N:original","mutate",out uint revision)==RebirthNpcInventoryTransactionResult.Applied&&revision==5&&current.Quantities["ammo"]==3&&current.Reservations["ammo"]==1,"exact original logical debit and reservations retained");
  Check(ApplyRemoteResourceOriginalPlan(actor,plan.Operation,"N:original","mutate",out _)==RebirthNpcInventoryTransactionResult.Replayed&&RebirthNpcInventoryPersistenceStore.Dirty==1,"same actual effect cannot repeat");
  current.Revision=6;current.Quantities["ammo"]=7;Check(ApplyRemoteResourceOriginalPlan(actor,plan.Operation,"N:original","mutate",out _)==RebirthNpcInventoryTransactionResult.Replayed&&current.Quantities["ammo"]==7,"later legitimate work preserved by typed original replay");
  current.LogisticsEffects.Clear();Check(ApplyRemoteResourceOriginalPlan(actor,plan.Operation,"N:original","mutate",out _)==RebirthNpcInventoryTransactionResult.RevisionConflict,"remembered GUID alone is unknown");
  actor.Died=1;Check(ApplyRemoteResourceOriginalPlan(actor,plan.Operation,"N:original","mutate",out _)==RebirthNpcInventoryTransactionResult.InvalidRequest,"new owner life cannot apply original");
  Console.WriteLine("PASS actual remote NPC original admission/mutation with disclosed journal/codec/native doubles; publication not certified");
 }
}
internal static class RemoteResourcesRuntimePolicy{internal static bool Enabled=true;internal static float Radius=30;}
internal sealed class RebirthRemoteResourceNpcEffectPlan{
 internal Guid Operation=Guid.NewGuid(),MutationId=Guid.NewGuid();internal RebirthNpcStableId NpcId=new RebirthNpcStableId{Text=new string('1',32)};
 internal string Binding=new string('b',64),Creation="creation",Owner=new string('a',64),BeforeNative="",AfterNative="";internal uint Revision=4;internal int Deaths,Attempted;internal bool DogStorage;
 internal IReadOnlyDictionary<string,int> BeforeQuantities=new Dictionary<string,int>{{"ammo",5}},AfterQuantities=new Dictionary<string,int>{{"ammo",3}},Reservations=new Dictionary<string,int>{{"ammo",1}};
 internal bool IsCurrent()=>true;
}
internal static class RebirthRemoteResourceDurableOutcome{
 internal static RebirthRemoteResourceNpcEffectPlan Plan;
 internal static bool TryReadNpcEffectPlan(EntityPlayer actor,Guid operation,string source,out RebirthRemoteResourceNpcEffectPlan plan){plan=Plan;return plan!=null&&operation==plan.Operation&&source=="N:original";}
}
