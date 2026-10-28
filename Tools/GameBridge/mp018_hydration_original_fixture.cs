// AUTHORED_NOT_RUN. Actual original + hydration/shared plan/codec/scope classes.
// ACTUAL_PLAN_SOURCES
internal static class Program {
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static RebirthGearInventoryPlan.Stack Cell(int count=0)=>new RebirthGearInventoryPlan.Stack{Count=count,ItemData=count==0?"":Convert.ToBase64String(new byte[]{7,9,3,25})};
 private static RebirthGearInventoryPlan.Stack[] Cells(int length){var cells=new RebirthGearInventoryPlan.Stack[length];for(int i=0;i<length;i++)cells[i]=Cell();return cells;}
 public static void Main(){
  var bag=Cells(44);var belt=Cells(20);bag[3]=Cell(3);var operation=Guid.NewGuid();var world=Guid.NewGuid();string creation=Guid.NewGuid().ToString("N");
  Need(RebirthHydrationTransferPlan.TryCreate(bag,belt,null,4,Cell(),true,3,out var plan),"fixture original plan refused");
  Need(RebirthHydrationTransferOriginal.TryCreate(operation,world,"account",creation,2,8,4,null,plan,out var original),"qualified original refused");
  Need(original.Matches(world,"account",creation,2)&&original.Operation==operation&&original.Equip&&original.Revision==8&&original.Digest.Length==64,"original native identity lost");
  Need(!original.Matches(Guid.NewGuid(),"account",creation,2)&&!original.Matches(world,"foreign",creation,2)&&!original.Matches(world,"account",Guid.NewGuid().ToString("N"),2)&&!original.Matches(world,"account",creation,3),"foreign native scope matched");
  var xml=original.Write();Need(RebirthHydrationTransferOriginal.TryRead(xml,out var decoded)&&decoded.Digest==original.Digest,"canonical roundtrip drifted");
  xml.SetAttributeValue("owner","changed caller");Need(original.Owner=="account"&&original.Write().Attribute("owner").Value=="account","Write leaked mutable original image");
  Need(original.TryGetPlan(out var copy),"original plan unavailable");copy.GearAfter.ItemData=Convert.ToBase64String(new byte[]{1});Need(original.TryGetPlan(out var next)&&next.GearAfter.ItemData==plan.GearAfter.ItemData,"plan clone mutated immutable original");
  for(int variant=0;variant<14;variant++){
   xml=original.Write();switch(variant){
    case 0:xml.SetAttributeValue("version",2);break;case 1:xml.SetAttributeValue("operation",Guid.Empty.ToString("N"));break;case 2:xml.SetAttributeValue("world",Guid.Empty.ToString("N"));break;
    case 3:xml.SetAttributeValue("owner","account ");break;case 4:xml.SetAttributeValue("creation","invalid");break;case 5:xml.SetAttributeValue("deaths",-1);break;case 6:xml.SetAttributeValue("revision",int.MaxValue);break;
    case 7:xml.SetAttributeValue("ownedBelt",3);break;case 8:xml.SetAttributeValue("locks",new string('1',44));break;case 9:xml.SetAttributeValue("action","unequip");break;
    case 10:xml.Add(new XAttribute("unknown","1"));break;case 11:xml.Add(new XElement(xml.Element("gearPlan")));break;case 12:xml.Add(new XComment("noncanonical content"));break;case 13:xml.SetAttributeValue("revision","08");break;
   }Need(!RebirthHydrationTransferOriginal.TryRead(xml,out _),"malformed hydration original accepted "+variant);
  }
  xml=original.Write();xml.Element("gearPlan").SetAttributeValue("bagAfter",45);Need(!RebirthHydrationTransferOriginal.TryRead(xml,out _),"capacity-changing original accepted");
  xml=original.Write();var change=xml.Element("gearPlan").Element("changes").Element("change");change.SetAttributeValue("bag",false);change.SetAttributeValue("index",19);Need(!RebirthHydrationTransferOriginal.TryRead(xml,out _),"retired backing original accepted");
  Need(RebirthHydrationTransferPlan.TryCreate(bag,belt,null,4,Cell(1),true,-1,out plan)&&RebirthHydrationTransferOriginal.TryCreate(operation,world,"account",creation,2,8,4,null,plan,out decoded)&&!decoded.Equip,"native hydration return original refused");
  var receipt=RebirthHydrationMetabolismReceiptCodec.Write(original,creation);
  Need(RebirthHydrationMetabolismReceiptCodec.TryRead(receipt,creation,out var saved)&&saved.Digest==original.Digest,"production receipt codec changed original bytes");
  Need(!RebirthHydrationMetabolismReceiptCodec.TryRead(receipt,Guid.NewGuid().ToString("N"),out _),"foreign creation receipt accepted");
  using(var reader=System.Xml.XmlReader.Create(new StringReader(receipt.ToString())))
   Need(RebirthHydrationMetabolismReceiptCodec.TryRead(XElement.Load(reader),creation,out var formatted)&&formatted.Digest==original.Digest,"indented repository receipt changed the canonical original");
  for(int variant=0;variant<9;variant++){
   var bad=new XElement(receipt);switch(variant){
    case 0:bad.Name="unknown";break;case 1:bad.Name=XName.Get("hydrationTransferReceipt","foreign");break;
    case 2:bad.Add(new XAttribute("extra",1));break;case 3:bad.Add(new XElement(original.Write()));break;
    case 4:bad.Add(new XComment("extra"));break;case 5:bad.Add(new XText("foreign content"));break;
    case 6:bad.RemoveNodes();break;case 7:bad.Element("hydrationTransfer").SetAttributeValue("deaths",-1);break;case 8:bad.Add(new XCData(" "));break;
   }Need(!RebirthHydrationMetabolismReceiptCodec.TryRead(bad,creation,out _),"malformed production receipt accepted "+variant);
  }
  bool refused=false;try{RebirthHydrationMetabolismReceiptCodec.Write(original,Guid.NewGuid().ToString("N"));}catch(InvalidDataException){refused=true;}Need(refused,"production writer persisted wrong-creation receipt");
  receipt.Element("hydrationTransfer").SetAttributeValue("owner","caller mutation");
  Need(RebirthHydrationMetabolismReceiptCodec.Write(original,creation).Element("hydrationTransfer").Attribute("owner").Value=="account","production receipt writer leaked original reference");
  var wire=RebirthHydrationOperationFrame.Encode(original.Operation,original.Digest);
  Need(wire.Length==96&&RebirthHydrationOperationFrame.TryDecode(wire,out var wireOperation,out var wireDigest)&&wireOperation==original.Operation&&wireDigest==original.Digest,"production wire description roundtrip failed");
  for(int variant=0;variant<7;variant++){
   var bad=(byte[])wire.Clone();switch(variant){case 0:bad=null;break;case 1:bad=bad.Take(95).ToArray();break;case 2:bad=bad.Concat(new byte[]{0}).ToArray();break;case 3:bad[0]=255;break;case 4:bad[32]=(byte)'A';break;case 5:for(int i=0;i<32;i++)bad[i]=(byte)'0';break;case 6:bad[95]=(byte)' ';break;}
   Need(!RebirthHydrationOperationFrame.TryDecode(bad,out wireOperation,out wireDigest)&&wireOperation==Guid.Empty&&wireDigest==null,"malformed wire retained prior operation/digest "+variant);
  }
  refused=false;try{RebirthHydrationOperationFrame.Encode(original.Operation,new string('A',64));}catch(InvalidDataException){refused=true;}Need(refused,"wire writer emitted noncanonical digest");
  Console.WriteLine("PASS actual immutable hydration original/plan binding; native receipt authority/joint publication NOT RUN");
 }
}
