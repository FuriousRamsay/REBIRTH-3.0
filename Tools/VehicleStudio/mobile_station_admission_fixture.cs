// Native item/recipe codecs are explicit doubles. Admission/address/scope code
// is loaded from current production sources by Test-MobileStationAdmission.ps1.
public class ItemValue
{
    public int type=1;public Dictionary<string,object> Metadata;public bool IsEmpty()=>type==0;
    public bool TryGetMetadata(string key,out int value){value=key.EndsWith("batches")&&MobileStationAdmissionFixture.PerBatch?3:key.EndsWith("version")?(MobileStationAdmissionFixture.PerBatch?(MobileStationAdmissionFixture.Material?4:3):MobileStationAdmissionFixture.Material?2:1):1;return true;}
    public bool TryGetMetadata(string key,out string value){value=key.EndsWith("definition")?new string('a',64):MobileStationAdmissionFixture.Job;return true;}
}
public class ItemStack{public int count;public ItemValue itemValue=new ItemValue();public ItemStack(int n){count=n;}public ItemStack(ItemValue v,int n){itemValue=v;count=n;}public ItemStack Clone()=>new ItemStack(new ItemValue{type=itemValue.type},count);}
public class Recipe
{
    public int itemValueType,count,craftExpGain,craftingTier;public float craftingTime;public string craftingArea;
    public bool IsScrap,materialBasedRecipe;public List<ItemStack> ingredients=new List<ItemStack>();
}
public static class RebirthStationInputLayout{public const int PhysicalSlots=10;}
public static class RebirthStationNativeInputCodec{public static bool IsRoundTrippable(IList<ItemStack> input)=>input!=null;}
public static class RebirthStationGridIngredients
{
    public class Plan{public bool IsMaterialPayment;public int PhysicalSlotCount;}
    public static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.count==b.count;
}
public static class RebirthStationMaterialInputCodec
{
    public static bool TryWrite(IList<ItemStack> input,int physical,out XElement node){node=null;if(input==null||input.Count!=4||physical!=3)return false;node=new XElement("materialInput",new XAttribute("count",input[3].count));return true;}
    public static bool TryRead(XElement node,out ItemStack[] input,out int physical){input=null;physical=0;if(node==null||!int.TryParse((string)node.Attribute("count"),out int n)||n<0)return false;input=new[]{new ItemStack(0),new ItemStack(0),new ItemStack(0),new ItemStack(n)};physical=3;return true;}
}
public static class RebirthStationGridSnapshotCodec
{
    public const int MaximumItemText=262144;
    public static bool TryRead(XElement node,out ItemStack[] items){items=null;if(node==null||!int.TryParse((string)node.Attribute("count"),out var n))return false;items=new[]{new ItemStack(n)};return true;}
    public static bool TryWrite(IList<ItemStack> items,out XElement node){node=null;if(items==null||items.Count!=1)return false;node=new XElement("grid",new XAttribute("count",items[0].count));return true;}
}
public static class RebirthStationGridQueue
{
    public static ItemStack RefundSnapshot(ItemStack paid)=>MobileStationAdmissionFixture.Material?new ItemStack(123456):paid.Clone();
    public const string Prefix="queue.";
    public class Binding{public string DefinitionId=new string('a',64);}
    public static bool TryPrepare(RebirthStationGridIngredients.Plan p,IList<ItemStack> input,float seconds,IList<Recipe> definitions,out Recipe recipe,out ItemStack[] after,out Binding binding,bool perBatch=false){recipe=null;after=null;binding=null;return false;}
    public static bool TryBindJob(Recipe recipe,Guid job)=>true;
    public static bool TryRestore(Recipe recipe,IList<Recipe> definitions){recipe.craftingTier=1;return true;}
    public static bool TryGetJobId(Recipe recipe,out string job){job=MobileStationAdmissionFixture.Job;return true;}
    public static bool TryGetDefinitionBinding(Recipe recipe,IList<Recipe> definitions,out Binding binding){binding=new Binding();return true;}
    public static bool ConservesPayment(ItemStack[] before,ItemStack[] after,Recipe recipe)=>before[MobileStationAdmissionFixture.Material?3:0].count-after[MobileStationAdmissionFixture.Material?3:0].count==(MobileStationAdmissionFixture.Material?123456:recipe.ingredients[0].count);
}
public static class MobileStationAdmissionFixture
{
    public static bool Material,PerBatch;
    public static readonly string Job=Guid.NewGuid().ToString("N");
    private static readonly string Creation=Guid.NewGuid().ToString("N");
    private static void Check(bool result,string label){if(!result)throw new Exception(label);}
    private static XElement Image(int schema,string address=null)
    {
        var node=new XElement("stationAdmission",new XAttribute("version",schema),new XAttribute("job",Job),new XAttribute("creation",Creation),
            new XAttribute("definition",new string('a',64)),new XAttribute("x",1),new XAttribute("y",2),new XAttribute("z",3),new XAttribute("block","forge"),
            new XElement("before",new XElement("grid",new XAttribute("count",2))),new XElement("after",new XElement("grid",new XAttribute("count",1))),
            new XElement("recipe",new XAttribute("type",1),new XAttribute("count",1),new XAttribute("xp",0),new XAttribute("seconds","2"),new XAttribute("area","forge"),new XElement("grid",new XAttribute("count",1))));
        if(schema==2||schema==4)node.SetAttributeValue("phase","publicationAttempted");
        if(address!=null)node.SetAttributeValue("stationAddress",address);return node;
    }
    public static void Run()
    {
        var address=new RebirthCamperStationAddress(Guid.NewGuid(),Guid.NewGuid());
        foreach(var schema in new[]{1,2,3,4}){
            Check(RebirthStationGridAdmission.TryReadStored(Image(schema,schema>=3?address.ToString():null),out var read),"schema read "+schema);
            Check(RebirthStationGridAdmission.TryReadStored(read.Clone().Write(),out var roundtrip)&&XNode.DeepEquals(read.Write(),roundtrip.Write()),"round trip "+schema);
        }
        Check(RebirthStationGridAdmission.TryReadStored(Image(3,address.ToString()),out var mobile),"mobile read");
        var moved=mobile.Write();moved.SetAttributeValue("x",999);
        Check(RebirthStationGridAdmission.TryReadStored(moved,out var moving)&&mobile.SharesStation(moving),"movement retains custody");
        var second=new RebirthCamperStationAddress(address.Vehicle,Guid.NewGuid());
        Check(RebirthStationGridAdmission.TryReadStored(Image(3,second.ToString()),out var duplicate)&&!mobile.SharesStation(duplicate),"duplicate forge isolation");
        Check(RebirthStationGridAdmission.TryReadStored(Image(1),out var terrain)&&!mobile.SharesStation(terrain),"terrain isolation");
        Check(ExactReservationKey.StationKey(mobile)==ExactReservationKey.StationKey(moving),"reservation survives movement");
        Check(ExactReservationKey.StationKey(mobile)!=ExactReservationKey.StationKey(duplicate),"duplicate stations reserve independently");
        Check(ExactReservationKey.StationKey(mobile)!=ExactReservationKey.StationKey(terrain),"mobile reservation cannot collide with terrain");
        var otherVehicle=new RebirthCamperStationAddress(Guid.NewGuid(),address.Installation);
        Check(RebirthStationGridAdmission.TryReadStored(Image(3,otherVehicle.ToString()),out var otherTruck)&&
            ExactReservationKey.StationKey(mobile)!=ExactReservationKey.StationKey(otherTruck),"vehicle identity separates reservations");
        var terrainMoved=terrain.Write();terrainMoved.SetAttributeValue("x",-999);
        Check(RebirthStationGridAdmission.TryReadStored(terrainMoved,out var otherTerrain)&&
            ExactReservationKey.StationKey(terrain)!=ExactReservationKey.StationKey(otherTerrain),"stationary reservations retain coordinate identity");
        Check(mobile.TryMarkPublicationAttempted(out var attempted)&&(string)attempted.Write().Attribute("version")=="4"&&attempted.TryGetMobileAddress(out var retained)&&retained.Equals(address),"attempt retains address");
        Check(!mobile.MatchesUnpaidObservation(Creation,1,2,3,"forge",new[]{new ItemStack(2)}),"positional path refuses mobile");
        Check(moving.MatchesMobileUnpaidObservation(Creation,address,"forge",new[]{new ItemStack(2)}),"mobile observation ignores diagnostic coordinates");
        Check(!moving.MatchesMobileUnpaidObservation(Creation,second,"forge",new[]{new ItemStack(2)}),"other installation refused");
        Check(!RebirthStationGridAdmission.TryReadStored(Image(3,"camper:bad"),out _),"malformed address refused");
        Check(!RebirthStationGridAdmission.TryReadStored(Image(1,address.ToString()),out _),"address smuggling into legacy schema refused");
        Check(!RebirthStationGridAdmission.TryReadStored(Image(5,address.ToString()),out _),"counter schema rejects physical image");
        Material=true;
        var molten=Image(5,address.ToString());molten.Element("before").ReplaceNodes(new XElement("materialInput",new XAttribute("count",123456)));
        molten.Element("after").ReplaceNodes(new XElement("materialInput",new XAttribute("count",0)));
        Check(RebirthStationGridAdmission.TryReadStored(molten,out var paid)&&paid.IsMaterialPayment&&paid.TryGetPhysicalSlotCount(out int slots)&&slots==3,"molten prepared parser");
        Check(paid.TryMarkPublicationAttempted(out var counterAttempt)&&(string)counterAttempt.Write().Attribute("version")=="6"&&
            RebirthStationGridAdmission.TryReadStored(counterAttempt.Write(),out var reload)&&reload.IsMaterialPayment&&reload.IsPublicationAttempted,"counter attempted roundtrip");
        Check(paid.MatchesMobileUnpaidObservation(Creation,address,"forge",new[]{new ItemStack(0),new ItemStack(0),new ItemStack(0),new ItemStack(123456)})&&
            !paid.MatchesMobileUnpaidObservation(Creation,address,"forge",new[]{new ItemStack(0),new ItemStack(0),new ItemStack(0),new ItemStack(123455)}),"counter exact witness");
        var tampered=new XElement(molten);tampered.Element("after").Element("materialInput").SetAttributeValue("count",1);
        Check(!RebirthStationGridAdmission.TryReadStored(tampered,out _),"counter conservation refuses changed units");
        Material=false;
        PerBatch=true;
        var batches=Image(8,address.ToString());batches.Element("before").Element("grid").SetAttributeValue("count",4);
        batches.Element("recipe").Element("grid").SetAttributeValue("count",3);
        Check(RebirthStationGridAdmission.TryReadStored(batches,out var batchAdmission)&&batchAdmission.IsPerBatch&&batchAdmission.OriginalBatches==3,"perbatch admission read");
        Check(batchAdmission.TryMarkPublicationAttempted(out var batchAttempt)&&(string)batchAttempt.Write().Attribute("version")=="9","perbatch attempt");
        Check(batchAttempt.TryMaterialize(new List<Recipe>(),out var batchRecipe,out _,out _)&&batchAttempt.MatchesQueuedQuality(batchRecipe,1,1)&&
            batchAttempt.MatchesQueuedQuality(batchRecipe,3,1)&&!batchAttempt.MatchesQueuedQuality(batchRecipe,4,1)&&
            batchAttempt.MatchesInitialQueuedQuality(batchRecipe,3,1)&&!batchAttempt.MatchesInitialQueuedQuality(batchRecipe,1,1),"initial vs remaining queue identity");
        Check(!RebirthStationTerminalIntent.TryCreate(batchAttempt,false,7,out _),"missing remaining cancellation refused");
        Check(RebirthStationTerminalIntent.TryCreate(batchAttempt,false,7,1,out var terminal)&&terminal.RemainingBatches==1&&
            RebirthStationTerminalIntent.TryRead(terminal.Write(),batchAttempt,out _),"remaining intent roundtrip");
        Check(!RebirthStationTerminalIntent.TryCreate(batchAttempt,false,7,4,out _)&&!RebirthStationTerminalIntent.TryCreate(batchAttempt,false,7,0,out _),"remaining bounds");
        Check(RebirthStationCancellationRefund.TryCreate(batchAttempt,new List<Recipe>(),terminal,out var partial)&&partial.CopyRefunds()[0].count==1&&
            RebirthStationCancellationRefund.TryReadStored(partial.Write(),batchAttempt,terminal,out _),"refund exact remaining one of three");
        var inflated=partial.Write();inflated.Element("grid").SetAttributeValue("count",3);
        Check(!RebirthStationCancellationRefund.TryReadStored(inflated,batchAttempt,terminal,out _),"completed units cannot refund");
        var destination=new RebirthCamperStationAddress(Guid.NewGuid(),address.Installation);
        var correlation=new RebirthCamperStationRelocationCorrelation{Source=address,OriginalAdmission=batchAttempt,OriginalPublication=new RebirthCamperStationPublicationRecord{Image=new XElement("camperStationPublication",new XAttribute("valid","yes"))},Destination=destination};
        Check(batchAttempt.TryRelocate(correlation,out var relocated)&&relocated.IsPerBatch&&relocated.OriginalBatches==3&&relocated.TryGetMobileAddress(out var relocatedAddress)&&relocatedAddress.Equals(destination)&&
            relocated.MatchesQueuedQuality(batchRecipe,1,1)&&XNode.DeepEquals(relocated.Write(),relocated.Clone().Write()),"relocation preserves partial batch identity and immutable lineage");
        Check(RebirthStationGridAdmission.TryReadStored(relocated.Write(),out var relocatedReload)&&relocatedReload.IsPerBatch,"relocation reload");
        var invalidRelocation=relocated.Write();invalidRelocation.SetAttributeValue("destination",address.ToString());
        Check(!RebirthStationGridAdmission.TryReadStored(invalidRelocation,out _),"same vehicle relocation refused");
        invalidRelocation=relocated.Write();invalidRelocation.SetAttributeValue("installation",(string)invalidRelocation.Attribute("removal"));
        Check(!RebirthStationGridAdmission.TryReadStored(invalidRelocation,out _),"same operation relocation refused");
        invalidRelocation=relocated.Write();invalidRelocation.SetAttributeValue("removalAfter","bad");
        Check(!RebirthStationGridAdmission.TryReadStored(invalidRelocation,out _),"malformed relocation digest refused");
        invalidRelocation=relocated.Write();invalidRelocation.Element("parentPublication").Elements().Single().SetAttributeValue("valid","no");
        Check(!RebirthStationGridAdmission.TryReadStored(invalidRelocation,out _),"unbound parent publication refused");
        var chain=relocated;
        for(int depth=2;depth<=9;depth++)
        {
            chain.TryGetMobileAddress(out var currentSource);
            var next=new RebirthCamperStationRelocationCorrelation{Source=currentSource,Destination=new RebirthCamperStationAddress(Guid.NewGuid(),address.Installation),
                OriginalAdmission=chain,OriginalPublication=correlation.OriginalPublication};
            bool accepted=chain.TryRelocate(next,out var nextChain);Check(accepted==(depth<=8),"bounded relocation lineage "+depth);
            if(accepted)chain=nextChain;
        }
        Material=true;
        var materialBatch=Image(10,address.ToString());materialBatch.Element("before").ReplaceNodes(new XElement("materialInput",new XAttribute("count",123456)));
        materialBatch.Element("after").ReplaceNodes(new XElement("materialInput",new XAttribute("count",0)));
        Check(RebirthStationGridAdmission.TryReadStored(materialBatch,out var materialPrepared)&&materialPrepared.TryMarkPublicationAttempted(out var materialAttempt)&&
            RebirthStationTerminalIntent.TryCreate(materialAttempt,false,7,1,out var materialTerminal)&&
            RebirthStationCancellationRefund.TryCreate(materialAttempt,new List<Recipe>(),materialTerminal,out var materialRefund)&&
            materialRefund.IsMaterialReturn&&materialRefund.CopyRefunds().Length==0&&materialRefund.CopyCounterRefunds()[0].count==41152&&
            RebirthStationCancellationRefund.TryReadStored(materialRefund.Write(),materialAttempt,materialTerminal,out _),"remaining Int32 counters return only to station");
        Material=false;
        PerBatch=false;
        Console.WriteLine("PASS actual admission/terminal/refund records: legacy1-6, perbatch8-11, strictinitial vs remaining, partialphysical and Int32 counter refunds, schema7 immutable relocation, roundtrips and malformed/conservation refusal. Native item/recipe/publication codecs doubled; no gameplay proof.");
    }
}
public static class RebirthNativeItemCodec {public static string Encode(ItemValue v)=>"item";public static bool TryDecode(string s,out ItemValue v){v=new ItemValue();return s=="item";}}
internal class RebirthCamperStationRelocationCorrelation {internal RebirthStationGridAdmission OriginalAdmission=null;internal RebirthCamperStationPublicationRecord OriginalPublication=null;internal RebirthCamperStationAddress Source=default(RebirthCamperStationAddress),Destination=default(RebirthCamperStationAddress);internal Guid RemovalOperation=Guid.NewGuid(),InstallationOperation=Guid.NewGuid();internal string RemovalAfterHash=new string('a',64),InstallationAfterHash=new string('b',64);}
public class RebirthCamperStationPublicationRecord {public XElement Image=new XElement("camperStationPublication");public XElement Write()=>new XElement(Image);public static bool TryRead(XElement n,RebirthStationGridAdmission a,out RebirthCamperStationPublicationRecord p){p=new RebirthCamperStationPublicationRecord{Image=n};return n!=null&&n.Name=="camperStationPublication"&&(string)n.Attribute("valid")=="yes";}}
