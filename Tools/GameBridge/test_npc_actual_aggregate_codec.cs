using System;using System.IO;using System.Reflection;using System.Xml;using System.Globalization;
class AggregateCodecCheck {
 static BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
 static object Get(object o,string name){return o.GetType().GetField(name).GetValue(o);}
 static void Set(object o,string name,object value){o.GetType().GetField(name).SetValue(o,value);}
 static object Invoke(Type type,string name,params object[] args){try{return type.GetMethod(name,flags).Invoke(null,args);}catch(TargetInvocationException e){throw new Exception("Production method: "+name,e.InnerException);}}
 static int checks;
 static void Assert(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 static int Main(string[] args){try{
 string mod=args[0],managed=args[1];
 AppDomain.CurrentDomain.AssemblyResolve+=(sender,eventArgs)=>{string name=new AssemblyName(eventArgs.Name).Name+".dll";foreach(string dir in new[]{mod,managed}){string candidate=Path.Combine(dir,name);if(File.Exists(candidate))return Assembly.LoadFrom(candidate);}return null;};
 var assembly=Assembly.LoadFrom(Path.Combine(mod,"RebirthUtils.dll"));
 var store=assembly.GetType("RebirthNpcAggregatePersistenceStore",true);
 var stableType=assembly.GetType("RebirthNpcStableId",true);
 var stable=stableType.GetMethod("NewId",flags).Invoke(null,null);
 object record=Invoke(store,"CreateEmpty",stable,DateTime.UtcNow.Ticks);
 Set(Get(record,"Profile"),"ProfileId","specialist.medic");
 var transform=Get(record,"Transform");var vectorType=transform.GetType().GetField("WorldPosition").FieldType;
 var position=Activator.CreateInstance(vectorType,new object[]{14f,45f,99f});
 Set(transform,"AnchorPosition",Activator.CreateInstance(vectorType,new object[]{8f,45f,10f}));Set(transform,"AnchorRotation",60f);
 var extensionDocument=new XmlDocument();extensionDocument.LoadXml("<fixtureExtension value=\"one\" />");Set(record,"UnknownOptionalElements",new string[]{extensionDocument.DocumentElement.OuterXml});
 Set(transform,"WorldPosition",position);Set(transform,"RotationYaw",123f);Set(transform,"TransformRevision",7u);
  var slotType=assembly.GetType("RebirthNpcInventorySlotRecord",true);
 var slot=Activator.CreateInstance(slotType);Set(slot,"SlotIndex",3);Set(slot,"ItemCount",4);Set(slot,"ItemValueSerialization","opaque-item<&>");Set(slot,"MetadataOrCustomData","metadata<&>\"book\"");Set(slot,"Locked",true);Set(slot,"Reserved",true);
 var overflow=Activator.CreateInstance(slotType);Set(overflow,"SlotIndex",9);Set(overflow,"ItemCount",2);Set(overflow,"ItemValueSerialization","overflow-item");Set(overflow,"MetadataOrCustomData","overflow-metadata");
 var inventory=Activator.CreateInstance(assembly.GetType("RebirthNpcInventoryRecord",true));Set(inventory,"InventorySchemaVersion",1);Set(inventory,"SlotCount",8);Set(inventory,"InventoryRevision",5u);Set(inventory,"ContentChecksum","opaque-inventory-checksum");
 var slots=Array.CreateInstance(slotType,1);slots.SetValue(slot,0);Set(inventory,"SlotRecords",slots);var overflowSlots=Array.CreateInstance(slotType,1);overflowSlots.SetValue(overflow,0);Set(inventory,"OverflowRecords",overflowSlots);Set(record,"Inventory",inventory);
 var equipmentType=assembly.GetType("RebirthNpcEquipmentSlotRecord",true);var held=Activator.CreateInstance(equipmentType);Set(held,"SlotId","hand");Set(held,"ItemCount",1);Set(held,"ItemValueSerialization","opaque-weapon");Set(held,"MetadataOrCustomData","weapon<&>metadata");
 var equipment=Activator.CreateInstance(assembly.GetType("RebirthNpcEquipmentRecord",true));Set(equipment,"EquipmentSchemaVersion",1);Set(equipment,"EquipmentRevision",6u);Set(equipment,"ActiveHeldSlot","hand");Set(equipment,"ActiveAmmoStateIfNotNative","ammo-state<&>");var equipmentSlots=Array.CreateInstance(equipmentType,1);equipmentSlots.SetValue(held,0);Set(equipment,"EquipmentSlots",equipmentSlots);Set(record,"Equipment",equipment);
 string checksum=(string)Invoke(store,"ComputeRecordChecksum",record);Set(record,"AggregateChecksum",checksum);
 var document=new XmlDocument();var root=document.CreateElement("rebirthNpcPersistentRecords");document.AppendChild(root);
 int format=(int)store.GetField("CurrentFormat",flags).GetValue(null);
 root.SetAttribute("format",format.ToString(CultureInfo.InvariantCulture));root.SetAttribute("saveScope","scope");
 var node=(XmlElement)Invoke(store,"WriteRecord",document,record);root.AppendChild(node);
 var read=Invoke(store,"ReadRecord",node);
 object[] publicationArgs=new object[]{read,stable,null};bool publicationValid=(bool)store.GetMethod("ValidatePublication",flags).Invoke(null,publicationArgs);Assert(publicationValid,"actual record publication validation: "+publicationArgs[2]);
Assert((string)Invoke(store,"ComputeRecordChecksum",read)==checksum,"actual codec checksum roundtrip");
 Assert((string)Get(Get(read,"Profile"),"ProfileId")=="specialist.medic","profile roundtrip");
 var readTransform=Get(read,"Transform");var readPosition=Get(readTransform,"WorldPosition");
 Assert((float)Get(readPosition,"x")==14f&&(float)Get(readPosition,"z")==99f&&(float)Get(readTransform,"RotationYaw")==123f,"position/yaw roundtrip");
 Assert((float)Get(Get(readTransform,"AnchorPosition"),"x")==8f&&(float)Get(readTransform,"AnchorRotation")==60f,"nullable anchor roundtrip separate from location");
 Assert(((string[])Get(read,"UnknownOptionalElements"))[0]==extensionDocument.DocumentElement.OuterXml,"unknown optional extension preservation");
  var readInventory=Get(read,"Inventory");var readSlot=((Array)Get(readInventory,"SlotRecords")).GetValue(0);var readOverflow=((Array)Get(readInventory,"OverflowRecords")).GetValue(0);
 Assert((int)Get(readSlot,"SlotIndex")==3&&(int)Get(readSlot,"ItemCount")==4&&(bool)Get(readSlot,"Locked")&&(bool)Get(readSlot,"Reserved"),"inventory slot count and flags");
 Assert((string)Get(readSlot,"ItemValueSerialization")=="opaque-item<&>"&&(string)Get(readSlot,"MetadataOrCustomData")=="metadata<&>\"book\"","inventory opaque metadata preservation");
 Assert((int)Get(readOverflow,"SlotIndex")==9&&(int)Get(readOverflow,"ItemCount")==2&&(string)Get(readOverflow,"MetadataOrCustomData")=="overflow-metadata","overflow preservation independent from normal slots");
 var readEquipment=Get(read,"Equipment");var readHeld=((Array)Get(readEquipment,"EquipmentSlots")).GetValue(0);
 Assert((string)Get(readEquipment,"ActiveHeldSlot")=="hand"&&(string)Get(readEquipment,"ActiveAmmoStateIfNotNative")=="ammo-state<&>"&&(string)Get(readHeld,"MetadataOrCustomData")=="weapon<&>metadata","equipment and ammo metadata preservation");
  foreach(var fault in new[]{new[]{"inventory/slot","index","not-an-index"},new[]{"inventory/slot","count","bad"},new[]{"inventory/slot","count","-1"},new[]{"inventory/overflow","count","2147483648"},new[]{"equipment/slot","count","bad"},new[]{"equipment/slot","count","-1"}}){
   var damaged=(XmlElement)node.CloneNode(true);((XmlElement)damaged.SelectSingleNode(fault[0])).SetAttribute(fault[1],fault[2]);
   bool rejected=false;try{Invoke(store,"ReadRecord",damaged);}catch(Exception error){rejected=error.InnerException is InvalidDataException;}
   Assert(rejected,"malformed custody record refused: "+fault[0]+"/"+fault[1]);
 }
  foreach(string section in new[]{"equipment"}){
   var duplicated=(XmlElement)node.CloneNode(true);var container=(XmlElement)duplicated.SelectSingleNode(section);container.AppendChild(container.SelectSingleNode("slot").CloneNode(true));
   bool rejected=false;try{Invoke(store,"ReadRecord",duplicated);}catch(Exception error){rejected=error.InnerException is InvalidDataException;}
   Assert(rejected,"duplicate custody slot refused: "+section);
 }
  var legacyDuplicate=(XmlElement)node.CloneNode(true);var legacyInventory=(XmlElement)legacyDuplicate.SelectSingleNode("inventory");var legacyLast=(XmlElement)legacyInventory.SelectSingleNode("slot").CloneNode(true);legacyLast.SetAttribute("count","0");legacyInventory.AppendChild(legacyLast);
 var legacyRead=Invoke(store,"ReadRecord",legacyDuplicate);var legacySlots=(Array)Get(Get(legacyRead,"Inventory"),"SlotRecords");Assert(legacySlots.Length==2&&(int)Get(legacySlots.GetValue(1),"ItemCount")==0,"legacy duplicate order retained for dog last-slot-wins normalizer");
 var distinct=(XmlElement)node.CloneNode(true);var inventoryNode=(XmlElement)distinct.SelectSingleNode("inventory");var distinctSlot=(XmlElement)inventoryNode.SelectSingleNode("slot").CloneNode(true);distinctSlot.SetAttribute("index","4");inventoryNode.AppendChild(distinctSlot);
 var distinctRead=Invoke(store,"ReadRecord",distinct);Assert(((Array)Get(Get(distinctRead,"Inventory"),"SlotRecords")).Length==2,"different slot identities preserve both records");
 string file=Path.GetTempFileName();try{
 document.Save(file);Invoke(store,"ValidateStagedAggregate",file,"scope",1);Assert(true,"actual staged validator accepts actual codec");
 node.SetAttribute("checksum","bad");document.Save(file);bool refused=false;try{Invoke(store,"ValidateStagedAggregate",file,"scope",1);}catch(Exception error){refused=error.InnerException is InvalidDataException;}Assert(refused,"actual checksum corruption refused");
 }finally{File.Delete(file);}
  var stackRecordType=assembly.GetType("RebirthNpcNativeStackRecord",true);
 Func<XmlElement,bool> parseStack=element=>{object[] arguments=new object[]{System.Xml.Linq.XElement.Parse(element.OuterXml),null};bool accepted=(bool)stackRecordType.GetMethod("TryRead",flags).Invoke(null,arguments);if(!accepted&&arguments[1]!=null)throw new Exception("Rejected native stack leaked record");return accepted;};
 var stackDoc=new XmlDocument();stackDoc.LoadXml("<nativeStack version='1' id='"+Guid.NewGuid().ToString("N")+"' owner='"+stable.ToString()+"' item='gunHandgunT1Pistol' count='3' payload='AQID'/>");
 Assert(parseStack(stackDoc.DocumentElement),"native stack structural parse");
 foreach(var fault in new[]{new[]{"id",new string('0',32)},new[]{"owner",new string('0',32)},new[]{"count","0"},new[]{"count","-1"},new[]{"count","bad"},new[]{"payload","AQID "},new[]{"payload",""},new[]{"version","2"},new[]{"item"," key "}}){var broken=(XmlElement)stackDoc.DocumentElement.CloneNode(true);broken.SetAttribute(fault[0],fault[1]);Assert(!parseStack(broken),"native stack refuses invalid "+fault[0]);}
 var unknown=(XmlElement)stackDoc.DocumentElement.CloneNode(true);unknown.SetAttribute("extra","x");Assert(!parseStack(unknown),"native stack refuses unknown fields");
 object[] readStackArgs=new object[]{System.Xml.Linq.XElement.Parse(stackDoc.DocumentElement.OuterXml),null};stackRecordType.GetMethod("TryRead",flags).Invoke(null,readStackArgs);
 var detached=(System.Xml.Linq.XElement)stackRecordType.GetMethod("Write",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(readStackArgs[1],null);detached.SetAttributeValue("count",99);
 int retained=(int)stackRecordType.GetProperty("Count",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(readStackArgs[1],null);Assert(retained==3,"native stack immutable XML ownership");
  var setType=assembly.GetType("RebirthNpcNativeStackSet",true);var setNode=new System.Xml.Linq.XElement("nativeStacks",new System.Xml.Linq.XAttribute("version",1),new System.Xml.Linq.XAttribute("owner",stable.ToString()),new System.Xml.Linq.XAttribute("revision",7),System.Xml.Linq.XElement.Parse(stackDoc.DocumentElement.OuterXml));
 object[] setArguments=new object[]{setNode,null};Assert((bool)setType.GetMethod("TryRead",flags).Invoke(null,setArguments),"typed inventory set parse");
 var inventoryRecordType=assembly.GetType("RebirthNpcInventoryPersistentRecord",true);var ownedInventory=Activator.CreateInstance(inventoryRecordType);
 inventoryRecordType.GetProperty("NpcId").SetValue(ownedInventory,stable,null);inventoryRecordType.GetProperty("Revision").SetValue(ownedInventory,7u,null);
 var quantities=new System.Collections.Generic.Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);quantities.Add("gunHandgunT1Pistol",5);
 inventoryRecordType.GetProperty("Quantities").SetValue(ownedInventory,quantities,null);inventoryRecordType.GetProperty("Reservations").SetValue(ownedInventory,new System.Collections.Generic.Dictionary<string,int>{{"gunHandgunT1Pistol",1}},null);inventoryRecordType.GetProperty("ReplayJournal").SetValue(ownedInventory,new Guid[0],null);
 var nativeField=inventoryRecordType.GetField("NativeStacks",BindingFlags.Instance|BindingFlags.NonPublic);nativeField.SetValue(ownedInventory,setArguments[1]);
 var transactionType=assembly.GetType("RebirthNpcInventoryTransactionService",true);object[] validArguments=new object[]{ownedInventory,null};Assert((bool)transactionType.GetMethod("ValidatePersistentRecord",flags).Invoke(null,validArguments),"typed backing quantity reservation validates");
 var inventoryStoreType=assembly.GetType("RebirthNpcInventoryPersistenceStore",true);var inventoryDocument=new XmlDocument();var inventoryImage=(XmlElement)Invoke(inventoryStoreType,"Write",inventoryDocument,ownedInventory);inventoryDocument.AppendChild(inventoryImage);
 var restoredInventory=Invoke(inventoryStoreType,"Read",inventoryImage);var restoredSet=nativeField.GetValue(restoredInventory);
 Assert(restoredSet!=null&&System.Xml.Linq.XNode.DeepEquals((System.Xml.Linq.XElement)setType.GetMethod("Write",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(setArguments[1],null),(System.Xml.Linq.XElement)setType.GetMethod("Write",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(restoredSet,null)),"actual inventory codec preserves native set");
 object[] restoreArguments=new object[]{restoredInventory,null};Assert((bool)transactionType.GetMethod("TryRestorePersistentRecord",flags).Invoke(null,restoreArguments),"restore typed runtime inventory");
 var captured=(Array)transactionType.GetMethod("CapturePersistentRecords",flags).Invoke(null,null);Assert(captured.Length==1&&nativeField.GetValue(captured.GetValue(0))!=null,"runtime recapture retains exact set");
 quantities["gunHandgunT1Pistol"]=2;validArguments=new object[]{ownedInventory,null};Assert(!(bool)transactionType.GetMethod("ValidatePersistentRecord",flags).Invoke(null,validArguments),"quantity below native backing refused");quantities["gunHandgunT1Pistol"]=5;
 inventoryRecordType.GetProperty("Revision").SetValue(ownedInventory,8u,null);validArguments=new object[]{ownedInventory,null};Assert(!(bool)transactionType.GetMethod("ValidatePersistentRecord",flags).Invoke(null,validArguments),"native revision mismatch refused");inventoryRecordType.GetProperty("Revision").SetValue(ownedInventory,7u,null);
 var repeated=(XmlElement)inventoryImage.CloneNode(true);repeated.AppendChild(repeated.SelectSingleNode("nativeStacks").CloneNode(true));Assert(Invoke(inventoryStoreType,"Read",repeated)==null,"duplicate typed section refused");
 transactionType.GetMethod("ClearRuntimeState",flags).Invoke(null,null);
 Console.WriteLine("PASS "+checks+" built production DLL aggregate and native-stack structural checks; real managed dependencies; no game world or Unity native calls");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}}
}