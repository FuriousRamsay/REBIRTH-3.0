public sealed class RebirthNpcInventoryPersistentRecord
{
 internal Dictionary<Guid,string> LogisticsEffects;internal RebirthNpcNativeStackSet NativeStacks;
 public RebirthNpcStableId NpcId;public uint Revision;public Dictionary<string,int> Quantities,Reservations;public Guid[] ReplayJournal;
}
public static class InventoryReceiptFixture
{
 /* ACTUAL_ROW_CODEC */
 static void Check(bool value,string name){if(!value)throw new Exception(name);}
 static XmlElement Clone(XmlElement element)=>(XmlElement)element.CloneNode(true);
 public static void Main()
 {
  var id=new RebirthNpcStableId{Text=new string('2',32)};var original=new RebirthLogisticsTransferJournal.Record{Operation=Guid.NewGuid()};
  var leg=new RebirthLogisticsTransferJournal.NpcLeg{StableId=id.ToString(),Revision=7,DebitId=Guid.NewGuid(),CreditId=Guid.NewGuid()};leg.Entries.Add(new RebirthLogisticsTransferJournal.NpcEntry{Key="ammo",Before=4,After=2});original.Npcs.Add(leg);
  string receipt=RebirthLogisticsNpcEffectReceipt.ExpectedLogistics(original,leg,false);
  var record=new RebirthNpcInventoryPersistentRecord{NpcId=id,Revision=8,Quantities=new Dictionary<string,int>{{"ammo",2}},Reservations=new Dictionary<string,int>(),ReplayJournal=new[]{leg.DebitId},LogisticsEffects=new Dictionary<Guid,string>{{leg.DebitId,receipt}}};
  var document=new XmlDocument();var row=Write(document,record);var loaded=Read(row);
  Check(loaded!=null&&loaded.LogisticsEffects[leg.DebitId]==receipt&&RebirthNpcInventoryTransactionService.ValidatePersistentRecord(loaded,out _),"actual native XML row typed receipt roundtrip");
  var legacy=Clone(row);legacy.RemoveChild(legacy.SelectSingleNode("logisticsEffects"));Check(Read(legacy).LogisticsEffects.Count==0,"legacy absent effects remains empty");
  var duplicate=Clone(row);duplicate.AppendChild(duplicate.SelectSingleNode("logisticsEffects").CloneNode(true));Check(Read(duplicate)==null,"duplicate effects container refused");
  var duplicateId=Clone(row);var container=duplicateId.SelectSingleNode("logisticsEffects");container.AppendChild(container.FirstChild.CloneNode(true));Check(Read(duplicateId)==null,"duplicate effect transaction refused");
  var malformed=Clone(row);malformed.SelectSingleNode("logisticsEffects/effect").InnerText="%%%";Check(Read(malformed)==null,"invalid stored UTF8/Base64 refused");
  var wrong=Clone(row);wrong.SelectSingleNode("logisticsEffects/effect").InnerText=Convert.ToBase64String(Encoding.UTF8.GetBytes(receipt.Replace("npc=\""+id+"\"","npc=\""+new string('3',32)+"\"")));
  Check(!RebirthNpcInventoryTransactionService.ValidatePersistentRecord(Read(wrong),out _),"normal restore validator rejects wrong receipt NPC");
  var future=Clone(row);future.SetAttribute("revision","7");Check(!RebirthNpcInventoryTransactionService.ValidatePersistentRecord(Read(future),out _),"effect postrevision cannot precede record");
  var excessive=Clone(legacy);var effects=excessive.OwnerDocument.CreateElement("logisticsEffects");effects.SetAttribute("version","1");excessive.AppendChild(effects);
  for(int i=0;i<129;i++){original.Operation=Guid.NewGuid();leg.DebitId=Guid.NewGuid();var element=effects.OwnerDocument.CreateElement("effect");element.SetAttribute("id",leg.DebitId.ToString("N"));element.InnerText=Convert.ToBase64String(Encoding.UTF8.GetBytes(RebirthLogisticsNpcEffectReceipt.ExpectedLogistics(original,leg,false)));effects.AppendChild(element);}
  Check(Read(excessive)==null,"129 actual valid individual receipts exceed admission bound");
  Console.WriteLine("PASS actual NPC inventory XML row/record validation (native custody and journal doubles)");
 }
}
