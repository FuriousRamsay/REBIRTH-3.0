// AUTHORED_NOT_RUN. Native publication/body/UI/vehicle serialization adapters.
// SHARED_NATIVE_ADAPTERS
class EntityVehicle{}
class RebirthCamperModuleRuntime{internal bool TransferPending;internal static RebirthCamperModuleRuntime Runtime=new RebirthCamperModuleRuntime();internal static RebirthCamperModuleRuntime For(EntityVehicle vehicle)=>Runtime;}
static class RebirthCamperStationSnapshot{internal static byte[] Capture(Guid id,RebirthCamperStationState station)=>RebirthProductiveEarnedNativeBody.Image(station);}
static class RebirthCamperInventorySnapshot{internal static byte[] Capture(ItemStack[] cells)=>Encoding.UTF8.GetBytes(string.Join(",",cells.Select(cell=>cell.count)));}
static class RebirthCamperSavedVehicle{
 internal static bool Saved;internal static int Requests;
 internal class Expected:IDisposable{internal int Id=7,Class=4;internal byte[] Bag={1,2},Entity={3,4};public void Dispose(){}}
 internal static Expected RequestSave(EntityVehicle vehicle){Requests++;return new Expected();}
 internal static bool Verify(EntityVehicle vehicle,Expected expected,Func<bool> current)=>Saved&&expected!=null&&current();}
static class RebirthWorkstationCompletionPublication{
 internal static bool Write(string path,XElement image){var text=image.ToString(SaveOptions.DisableFormatting);if(File.Exists(path))return File.ReadAllText(path)==text;File.WriteAllText(path,text);return true;}
 internal static XElement ReadPrimary(string path)=>File.Exists(path)?XElement.Load(path):null;}
// ACTUAL_SAVED_OUTPUT
internal static partial class RebirthCookingMillingPublication{
 private class Original{
  internal bool NativeAccepted=true,Live=true,Mutated=true,Cleared=true;internal World World;internal TileEntityWorkstation Station;internal XUiC_WorkstationWindowGroup Window;
  internal EntityVehicle Vehicle;internal string StationBirth="birth",Path;internal byte[] QueueAfter=Encoding.UTF8.GetBytes("1"),SavedBody;internal int SavedOffset;
  internal IDisposable SavedMemory;internal RebirthRemoteResourceNativePublication.Watch SavedWatch;internal RebirthCamperSavedVehicle.Expected SavedVehicle;
  internal XElement OriginalImage=new XElement("immutable-original");internal ItemStack[] Output={new ItemStack(4)};
  internal bool MetadataCurrent()=>Live;
  // ACTUAL_TERMINAL
 }
 private static bool Same(ItemStack[] left,ItemStack[] right)=>left!=null&&right!=null&&left.Length==right.Length&&left.Select(value=>value.count).SequenceEqual(right.Select(value=>value.count));
 private static byte[] Queue(RecipeQueueItem[] rows)=>Encoding.UTF8.GetBytes(RebirthRemoteResourcePlacedStationQueue.Encode(rows));
 private static RecipeQueueItem[] Rows(XUiC_WorkstationWindowGroup window)=>window.craftingQueue.Cards.Select(card=>new RecipeQueueItem{Count=card.Count}).ToArray();
 private static XElement Envelope(Original original,XElement before,XElement after)=>new XElement("original",new XElement("before",new XElement(before)),new XElement("after",new XElement(after)));
 static int checks;static void Need(bool value,string label){checks++;if(!value)throw new Exception(label);}
 static Original Fresh(string root,bool mobile=false){RebirthRemoteResourceNativePublication.Published=false;RebirthCamperSavedVehicle.Saved=false;RebirthCamperSavedVehicle.Requests=0;RebirthCamperModuleRuntime.Runtime.TransferPending=false;
  var world=new World();var tile=mobile?(TileEntityWorkstation)new RebirthCamperStationState{BoundVehicle=new EntityVehicle()}:new TileEntityWorkstation();return new Original{World=world,Station=tile,Vehicle=(tile as RebirthCamperStationState)?.BoundVehicle,Path=Path.Combine(root,Guid.NewGuid().ToString("N"))};}
 public static void Main(string[] args){var original=Fresh(args[0]);Need(!ObserveSavedNativeOutput(original)&&!File.Exists(original.Path+".saved"),"native sync return substituted for saved tile");RebirthRemoteResourceNativePublication.Published=true;Need(ObserveSavedNativeOutput(original),"exact native tile publication did not finish saved source");var proof=XElement.Load(original.Path+".saved").Element("after").Elements().Single();Need(proof.Name=="cookingMillingSavedTile"&&(string)proof.Attribute("birth")==original.StationBirth&&(string)proof.Element("queue")==Convert.ToBase64String(original.QueueAfter),"saved tile proof lost original incarnation/full queue");
  original=Fresh(args[0]);ObserveSavedNativeOutput(original);original.Station.Output[0].count++;RebirthRemoteResourceNativePublication.Published=true;Need(!ObserveSavedNativeOutput(original),"changed combined output/body minted saved original");
  original=Fresh(args[0]);original.Station.Birth="replacement";RebirthRemoteResourceNativePublication.Published=true;Need(!ObserveSavedNativeOutput(original),"replacement station inherited milling original");
  original=Fresh(args[0],true);Need(!ObserveSavedNativeOutput(original)&&RebirthCamperSavedVehicle.Requests==1,"native mobile runtime projection substituted for vehicles.dat");RebirthCamperSavedVehicle.Saved=true;Need(ObserveSavedNativeOutput(original)&&RebirthCamperSavedVehicle.Requests==1,"positive vehicle readback failed or repeated native save original");proof=XElement.Load(original.Path+".saved").Element("after").Elements().Single();Need(proof.Name=="cookingMillingSavedVehicle"&&(string)proof.Element("vehicleBag")==Convert.ToBase64String(original.SavedVehicle.Bag)&&(string)proof.Element("vehicleEntity")==Convert.ToBase64String(original.SavedVehicle.Entity),"mobile saved proof omitted full native vehicle record");
  original=Fresh(args[0],true);ObserveSavedNativeOutput(original);RebirthCamperModuleRuntime.Runtime.TransferPending=true;RebirthCamperSavedVehicle.Saved=true;Need(!ObserveSavedNativeOutput(original),"mobile installation transfer minted terminal");
  original=Fresh(args[0]);original.Mutated=false;RebirthRemoteResourceNativePublication.Published=true;Need(!ObserveSavedNativeOutput(original),"unaccepted native queue/output minted saved proof");
  original=Fresh(args[0]);original.Window=new XUiC_WorkstationWindowGroup{outputWindow=new Grid(),craftingQueue=new QueueUi{Cards=new[]{new XUiC_RecipeStack{Count=7}}}};original.NativeAccepted=false;Need(!original.Terminal(),"unreturned native projection borrowed changed UI terminal");original.NativeAccepted=true;RebirthRemoteResourceNativePublication.Published=true;Need(ObserveSavedNativeOutput(original),"positively returned native source was stranded by reused UI controller");
  Console.WriteLine("PASS "+checks+" actual managed/mobile saved-output assertions; native codecs/publication are explicit adapters.");}
}
