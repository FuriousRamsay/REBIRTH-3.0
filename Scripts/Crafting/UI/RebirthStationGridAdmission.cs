using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Immutable preparation record. Persistence/queue commit must be coordinated by its owner.
public sealed class RebirthStationGridAdmission
{
    private readonly XElement image;
    private readonly XElement storedImage;
    private readonly ItemStack[] paidSnapshot;
    public bool IsPublicationAttempted=>(string)image.Attribute("phase")=="publicationAttempted";
    public string JobId {get;private set;}
    public string CreationId {get;private set;}
    public string DefinitionId {get;private set;}
    public bool IsMobile => image.Attribute("stationAddress") != null;
    public bool IsMaterialPayment => image.Element("before")?.Element("materialInput")!=null;
    public bool IsPlayerPayment => PlayerPaymentNode(storedImage)!=null;
    public bool IsPlayerPaymentConfirmed => (string)PlayerPaymentAdmissionNode(storedImage)?.Attribute("version")=="14";
    internal bool TryGetPlayerPayment(out RebirthCamperStationPlayerPaymentImage payment)
        =>RebirthCamperStationPlayerPaymentImage.TryRead(PlayerPaymentNode(storedImage),out payment);
    internal XElement WritePlayerPaymentBinding()
    {
        var binding=new XElement(PlayerPaymentAdmissionNode(storedImage));
        if((string)binding.Attribute("version")=="14")
        {binding.SetAttributeValue("version",13);binding.Element("ownerPaymentConfirmed").Remove();}
        return binding;
    }
    private static XElement PlayerPaymentNode(XElement node)=>PlayerPaymentAdmissionNode(node)?.Element("playerPayment");
    private static XElement PlayerPaymentAdmissionNode(XElement node)
    {
        for(int depth=0;node!=null&&depth<9;depth++)
        {
            string version=(string)node.Attribute("version");
            if(version=="12"||version=="13"||version=="14")return node;
            if(version!="7")return null;
            node=node.Element("parentAdmission")?.Elements().SingleOrDefault();
        }
        return null;
    }
    public bool IsPerBatch => (int?)image.Attribute("version")>=8;
    public int OriginalBatches => IsPerBatch&&paidSnapshot?.Length>0&&
        paidSnapshot[0]?.itemValue!=null&&paidSnapshot[0].itemValue.TryGetMetadata(RebirthStationGridQueue.Prefix+"batches",out int batches)?batches:1;
    public bool TryGetMobileAddress(out RebirthCamperStationAddress address)
        => RebirthCamperStationAddress.TryParse((string)image.Attribute("stationAddress"), out address);
    private RebirthStationGridAdmission(XElement node,string job,string creation,string definition)
    {storedImage=new XElement(node);image=EffectiveImage(node);JobId=job;CreationId=creation;DefinitionId=definition;
     RebirthStationGridSnapshotCodec.TryRead(image.Element("recipe")?.Element("grid"),out paidSnapshot);}
    private static XElement EffectiveImage(XElement node)
    {
        if((string)node.Attribute("version")=="12"||(string)node.Attribute("version")=="13"||(string)node.Attribute("version")=="14")
            return EffectiveImage(node.Element("parentAdmission").Elements().Single());
        if((string)node.Attribute("version")!="7")return new XElement(node);
        var parent=EffectiveImage(node.Element("parentAdmission").Elements().Single());
        parent.SetAttributeValue("stationAddress",(string)node.Attribute("destination"));return parent;
    }
    private static int RelocationDepth(XElement node)
    {
        int depth=0;
        while((string)node?.Attribute("version")=="7")
        {
            if(++depth>8)return depth;
            var parent=node.Element("parentAdmission");
            if(parent==null||parent.Elements().Count()!=1)return 9;
            node=parent.Elements().Single();
        }
        return depth;
    }
    // Unresolved preparations at the same station share one native input inventory.
    public bool SharesStation(RebirthStationGridAdmission other)
    {
        if (other == null) return false;
        // Actor-source preparations at different installations still share one
        // owner's bag/belt. Existing exclusive saved intent check fences both.
        if(IsPlayerPayment&&other.IsPlayerPayment&&TryGetPlayerPayment(out var ownPayment)&&
            other.TryGetPlayerPayment(out var otherPayment)&&ownPayment.OwnerKey==otherPayment.OwnerKey)return true;
        if (IsMobile || other.IsMobile)
            return TryGetMobileAddress(out var ownAddress) && other.TryGetMobileAddress(out var otherAddress)
                && ownAddress.Equals(otherAddress);
        return (int)image.Attribute("x")==(int)other.image.Attribute("x") &&
            (int)image.Attribute("y")==(int)other.image.Attribute("y") &&
            (int)image.Attribute("z")==(int)other.image.Attribute("z");
    }
    public XElement Write()=>new XElement(storedImage);
    internal XElement WritePaymentImage()=>new XElement(image);
    public RebirthStationGridAdmission Clone()=>new RebirthStationGridAdmission(storedImage,JobId,CreationId,DefinitionId);
    internal bool TryRelocate(RebirthCamperStationRelocationCorrelation correlation,out RebirthStationGridAdmission moved)
    {
        moved=null;
        if(correlation==null||correlation.OriginalAdmission==null||correlation.OriginalPublication==null||
            !TryGetMobileAddress(out var source)||!source.Equals(correlation.Source)||
            !XNode.DeepEquals(correlation.OriginalAdmission.Write(),Write()))return false;
        var node=new XElement("stationAdmission",new XAttribute("version",7),new XAttribute("destination",correlation.Destination.ToString()),
            new XAttribute("removal",correlation.RemovalOperation.ToString("N")),new XAttribute("installation",correlation.InstallationOperation.ToString("N")),
            new XAttribute("removalAfter",correlation.RemovalAfterHash),new XAttribute("installationAfter",correlation.InstallationAfterHash),
            new XElement("parentAdmission",Write()),new XElement("parentPublication",correlation.OriginalPublication.Write()));
        return TryReadStored(node,out moved);
    }
    public bool TryMarkPublicationAttempted(out RebirthStationGridAdmission attempted)
    {
        attempted=null;if(IsPublicationAttempted)return false;
        if(IsPlayerPayment)
        {
            var actor=Write();
            if((string)actor.Attribute("version")!="12"||
                !TryReadStored(actor.Element("parentAdmission")?.Elements().SingleOrDefault(),out var parent)||
                !parent.TryMarkPublicationAttempted(out var nextParent))return false;
            actor.SetAttributeValue("version",13);actor.SetAttributeValue("phase","publicationAttempted");actor.Element("parentAdmission").ReplaceNodes(nextParent.Write());
            return TryReadStored(actor,out attempted);
        }
        var next=Write();next.SetAttributeValue("version",IsPerBatch?(IsMaterialPayment?11:9):IsMaterialPayment?6:IsMobile?4:2);next.SetAttributeValue("phase","publicationAttempted");
        attempted=new RebirthStationGridAdmission(next,JobId,CreationId,DefinitionId);return true;
    }
    // Detached phase projection only. Caller must prove exact saved owner after
    // and persist this transition before granting queue visibility.
    internal bool TryConfirmPlayerPayment(out RebirthStationGridAdmission confirmed)
    {
        confirmed=null;
        if(!IsPlayerPayment||IsPlayerPaymentConfirmed||!IsPublicationAttempted||
            (string)storedImage.Attribute("version")!="13"||!TryGetPlayerPayment(out var payment))return false;
        var node=Write();node.SetAttributeValue("version",14);
        node.Add(new XElement("ownerPaymentConfirmed",new XAttribute("version",1),
            new XAttribute("phase","exactNativeOwnerAfterSaved"),new XAttribute("operation",RebirthCamperStationPlayerPayment.Operation(this).ToString("N")),
            new XAttribute("ownerKey",payment.OwnerKey),new XAttribute("ownerId",payment.OwnerId),
            new XAttribute("source",PaymentSourceHash(payment))));
        return TryReadStored(node,out confirmed);
    }
    private static string PaymentSourceHash(RebirthCamperStationPlayerPaymentImage payment)
    {using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(
        System.Text.Encoding.UTF8.GetBytes(payment.Write().ToString(SaveOptions.DisableFormatting)))).Replace("-","");}

    public static bool TryCreate(Guid job,Guid creation,int x,int y,int z,string stationBlock,
        RebirthStationGridIngredients.Plan plan,IList<ItemStack> before,float seconds,IList<Recipe> definitions,
        out RebirthStationGridAdmission admission)
        =>TryCreate(job,creation.ToString("N"),x,y,z,stationBlock,plan,before,seconds,definitions,out admission);

    public static bool TryNormalizeCreation(string value,out string normalized)
        =>RebirthSurvivorRequestScope.TryNormalize(value,out normalized);

    public static bool TryCreate(Guid job,string creation,int x,int y,int z,string stationBlock,
        RebirthStationGridIngredients.Plan plan,IList<ItemStack> before,float seconds,IList<Recipe> definitions,
        out RebirthStationGridAdmission admission)
        =>TryCreateCore(job,creation,x,y,z,stationBlock,plan,before,seconds,definitions,false,out admission);
    private static bool TryCreateCore(Guid job,string creation,int x,int y,int z,string stationBlock,
        RebirthStationGridIngredients.Plan plan,IList<ItemStack> before,float seconds,IList<Recipe> definitions,
        bool mobile,out RebirthStationGridAdmission admission)
    {
        admission=null;
        if(job==Guid.Empty||!TryNormalizeCreation(creation,out var creationKey)||string.IsNullOrEmpty(stationBlock)||stationBlock.Length>256||
            plan==null||(plan.IsMaterialPayment&&!mobile)||(!plan.IsMaterialPayment&&!RebirthStationNativeInputCodec.IsRoundTrippable(before))||
            !RebirthStationGridQueue.TryPrepare(plan,before,seconds,definitions,out var queued,out var after,out var binding,mobile)||
            !RebirthStationGridQueue.TryBindJob(queued,job)||
            !WriteInput(before,plan.IsMaterialPayment,plan.PhysicalSlotCount,out var beforeImage)||
            !WriteInput(after,plan.IsMaterialPayment,plan.PhysicalSlotCount,out var afterImage)||
            !RebirthStationGridSnapshotCodec.TryWrite(queued.ingredients,out var paidImage))return false;
        var node=new XElement("stationAdmission",new XAttribute("version",1),
            new XAttribute("job",job.ToString("N")),new XAttribute("creation",creationKey),
            new XAttribute("definition",binding.DefinitionId),new XAttribute("x",x),new XAttribute("y",y),new XAttribute("z",z),
            new XAttribute("block",stationBlock),
            new XElement("before",beforeImage),new XElement("after",afterImage),
            new XElement("recipe",new XAttribute("type",queued.itemValueType),new XAttribute("count",queued.count),
                new XAttribute("xp",queued.craftExpGain),new XAttribute("seconds",queued.craftingTime.ToString("R",CultureInfo.InvariantCulture)),
                new XAttribute("area",queued.craftingArea??""),paidImage));
        // Material admissions require a mobile address before parsing; ground physical
        // admissions retain the original shape and serialization.
        if(mobile){admission=new RebirthStationGridAdmission(node,job.ToString("N"),creationKey,binding.DefinitionId);return true;}
        return TryRead(node,definitions,out admission);
    }
    public static bool TryCreateMobile(Guid job,string creation,RebirthCamperStationAddress address,
        int observedX,int observedY,int observedZ,string stationBlock,RebirthStationGridIngredients.Plan plan,
        IList<ItemStack> before,float seconds,IList<Recipe> definitions,out RebirthStationGridAdmission admission)
    {
        admission=null;
        if(!address.IsValid || !TryCreateCore(job,creation,observedX,observedY,observedZ,stationBlock,
            plan,before,seconds,definitions,true,out var candidate))return false;
        var node=candidate.Write();
        node.SetAttributeValue("version",plan.IsMaterialPayment?10:8);
        node.SetAttributeValue("stationAddress",address.ToString());
        return TryRead(node,definitions,out admission);
    }
    internal static bool TryCreateMobilePlayer(Guid job,string creation,RebirthCamperStationAddress address,
        int x,int y,int z,string block,RebirthStationGridIngredients.Plan fragmentPlan,IList<ItemStack> fragments,
        float seconds,IList<Recipe> definitions,RebirthCamperStationPlayerPaymentImage payment,
        out RebirthStationGridAdmission admission)
    {
        admission=null;
        if(payment==null||!TryCreateMobile(job,creation,address,x,y,z,block,fragmentPlan,fragments,seconds,definitions,out var parent)||
            parent.IsMaterialPayment)return false;
        var wrapper=new XElement("stationAdmission",new XAttribute("version",12),new XAttribute("paymentSource","ownerInventory"),
            new XElement("parentAdmission",parent.Write()),payment.Write());
        foreach(var attribute in parent.Write().Attributes())
            if(attribute.Name!="version")wrapper.SetAttributeValue(attribute.Name,attribute.Value);
        return TryRead(wrapper,definitions,out admission);
    }

    private static bool Shape(XElement node,string name,string attributes,string children)
    {
        if(node==null||node.Name!=name)return false;
        var a=attributes.Split(',');var c=children.Split(',');
        return node.Attributes().Count()==a.Length&&a.All(k=>node.Attribute(k)!=null)&&
            node.Elements().Count()==c.Length&&c.All(k=>node.Elements(k).Count()==1)&&
            !node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)));
    }
    private static bool Integer(XElement node,string name,out int value)
        =>int.TryParse((string)node.Attribute(name),NumberStyles.Integer,CultureInfo.InvariantCulture,out value);
    private static bool Grid(XElement wrapper,out ItemStack[] slots)
    {
        slots=null;
        return wrapper!=null&&!wrapper.HasAttributes&&wrapper.Elements().Count()==1&&
            !wrapper.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))&&
            (wrapper.Element("materialInput")!=null?
                RebirthStationMaterialInputCodec.TryRead(wrapper.Element("materialInput"),out slots,out _):
                RebirthStationGridSnapshotCodec.TryRead(wrapper.Element("grid"),out slots));
    }
    private static bool WriteInput(IList<ItemStack> slots,bool material,int physicalSlots,out XElement image)
        =>material?RebirthStationMaterialInputCodec.TryWrite(slots,physicalSlots,out image):
            RebirthStationGridSnapshotCodec.TryWrite(slots,out image);
    private static bool MaterialPair(XElement beforeNode,XElement afterNode)
    {
        if(!RebirthStationMaterialInputCodec.TryRead(beforeNode,out var before,out int physical)||
            !RebirthStationMaterialInputCodec.TryRead(afterNode,out var after,out int nextPhysical)||
            physical!=nextPhysical||before.Length!=after.Length)return false;
        // A molten job cannot consume the visible physical smelting inputs.
        for(int i=0;i<physical;i++)
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(before[i],after[i]))return false;
        return true;
    }
    private bool MatchesAfter(IList<ItemStack> slots)
    {
        int physical=0;
        if(IsMaterialPayment&&!RebirthStationMaterialInputCodec.TryRead(image.Element("after")?.Element("materialInput"),out _,out physical))return false;
        return WriteInput(slots,IsMaterialPayment,physical,out var observed)&&
            XNode.DeepEquals(observed,image.Element("after")?.Elements().SingleOrDefault());
    }
    // Stored records preserve payment images even when their recipe is no longer available.
    // Successful storage parsing is NOT permission to enqueue, refund or award progression.
    public static bool TryReadStored(XElement node,out RebirthStationGridAdmission admission)
        =>TryReadCore(node,null,false,out admission,out _,out _,out _);

    public bool TryResolve(IList<Recipe> definitions,out RebirthStationGridAdmission resolved)
        =>TryRead(storedImage,definitions,out resolved);

    public static bool TryRead(XElement node,IList<Recipe> definitions,out RebirthStationGridAdmission admission)
        =>TryReadCore(node,definitions,true,out admission,out _,out _,out _);

    // Detached values only. Caller still needs authenticated custody and a durable commit.
    public bool TryMaterialize(IList<Recipe> definitions,out Recipe queued,out ItemStack[] before,out ItemStack[] after)
    {
        queued=null;before=null;after=null;
        if(!TryReadCore(storedImage,definitions,true,out _,out var candidate,out var original,out var remainder)||
            !RebirthStationGridQueue.TryRestore(candidate,definitions))return false;
        queued=candidate;before=original;after=remainder;return true;
    }
    public bool TryGetPhysicalSlotCount(out int count)
    {
        count=0;
        if(IsMaterialPayment)return RebirthStationMaterialInputCodec.TryRead(image.Element("before")?.Element("materialInput"),out _,out count);
        if(!Grid(image.Element("before"),out var slots))return false;
        count=slots.Length;return count>0&&count<=RebirthStationInputLayout.PhysicalSlots;
    }
    // Pre-payment recovery witness. Exact original grid, never a refund or debit grant.
    public bool MatchesUnpaidObservation(string creation,int x,int y,int z,string block,IList<ItemStack> input)
    {
        if(IsMobile||IsPublicationAttempted||!TryNormalizeCreation(creation,out var current)||current!=CreationId||
            x!=(int)image.Attribute("x")||y!=(int)image.Attribute("y")||z!=(int)image.Attribute("z")||
            block!=(string)image.Attribute("block")||!Grid(image.Element("before"),out var before)||
            input==null||input.Count!=before.Length)return false;
        for(int i=0;i<before.Length;i++)
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(input[i],before[i]))return false;
        return true;
    }
    // Compares one initial queued observation to this preparation. The caller must
    // authenticate the world/owner and establish unique queue occurrence and persistence.
    // No mutation or refund/queue authority is granted by this comparison.
    public bool MatchesQueuedObservation(string creation,int x,int y,int z,string block,
        IList<ItemStack> input,Recipe queued,int multiplier)
    {
        if(IsMobile||!TryNormalizeCreation(creation,out var owner)||owner!=CreationId||
            (string)image.Attribute("block")!=block||!Integer(image,"x",out var sx)||sx!=x||
            !Integer(image,"y",out var sy)||sy!=y||!Integer(image,"z",out var sz)||sz!=z||
            !MatchesQueuedRecipe(queued,multiplier))return false;
        return MatchesAfter(input);
    }
    public bool MatchesMobileUnpaidObservation(string creation,RebirthCamperStationAddress address,
        string block,IList<ItemStack> input)
    {
        if(IsPlayerPayment)
            return !IsPublicationAttempted&&TryGetMobileAddress(out var actorAddress)&&actorAddress.Equals(address)&&
                TryNormalizeCreation(creation,out var actorCreation)&&actorCreation==CreationId&&
                block==(string)image.Attribute("block")&&TryGetPlayerPayment(out var payment)&&payment.MatchesStation(input);
        if(IsPublicationAttempted||!TryGetMobileAddress(out var retained)||!retained.Equals(address)||
            !TryNormalizeCreation(creation,out var current)||current!=CreationId||
            block!=(string)image.Attribute("block")||!Grid(image.Element("before"),out var before)||
            input==null||input.Count!=before.Length)return false;
        for(int i=0;i<before.Length;i++)
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(input[i],before[i]))return false;
        return true;
    }
    public bool MatchesMobileQueuedObservation(string creation,RebirthCamperStationAddress address,
        string block,IList<ItemStack> input,Recipe queued,int multiplier)
    {
        if(multiplier!=OriginalBatches||!TryGetMobileAddress(out var retained)||!retained.Equals(address)||
            !TryNormalizeCreation(creation,out var current)||current!=CreationId||
            block!=(string)image.Attribute("block")||!MatchesQueuedRecipe(queued,multiplier))return false;
        return IsPlayerPayment?TryGetPlayerPayment(out var payment)&&payment.MatchesStation(input):MatchesAfter(input);
    }

    // Ongoing recipe identity check; later legitimate input additions do not rewrite paid ingredients.
    internal bool MatchesQueuedRecipe(Recipe queued,int multiplier)
    {
        if((IsPerBatch?(multiplier<1||multiplier>OriginalBatches):multiplier!=1)||queued==null||queued.IsScrap||(!IsMaterialPayment&&queued.materialBasedRecipe)||
            !RebirthStationGridQueue.TryGetJobId(queued,out var job)||job!=JobId||
            !RebirthStationGridSnapshotCodec.TryWrite(queued.ingredients,out var observedPaid))return false;
        var expected=image.Element("recipe");
        return Integer(expected,"type",out var type)&&queued.itemValueType==type&&
            Integer(expected,"count",out var count)&&queued.count==count&&
            Integer(expected,"xp",out var xp)&&queued.craftExpGain==xp&&
            float.TryParse((string)expected.Attribute("seconds"),NumberStyles.Float,CultureInfo.InvariantCulture,out var seconds)&&
            queued.craftingTime==seconds&&(queued.craftingArea??"")==(string)expected.Attribute("area")&&
            MatchesPaidSnapshot(queued.ingredients);
    }
    private bool MatchesPaidSnapshot(IList<ItemStack> current)
    {
        if(current==null||paidSnapshot==null||current.Count!=paidSnapshot.Length)return false;
        for(int i=0;i<paidSnapshot.Length;i++)
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(current[i],paidSnapshot[i]))return false;
        return true;
    }
    internal bool MatchesQueuedQuality(Recipe queued,int multiplier,int quality)
    {
        return MatchesQueuedRecipe(queued,multiplier)&&queued.ingredients[0].itemValue.TryGetMetadata(
            RebirthStationGridQueue.Prefix+"tier",out int tier)&&tier>=0&&tier<=6&&quality==tier&&queued.craftingTier==tier;
    }
    internal bool MatchesInitialQueuedQuality(Recipe queued,int multiplier,int quality)
        =>multiplier==OriginalBatches&&MatchesQueuedQuality(queued,multiplier,quality);
    private static bool TryReadCore(XElement node,IList<Recipe> definitions,bool requireDefinition,
        out RebirthStationGridAdmission admission,out Recipe queueImage,out ItemStack[] beforeImage,out ItemStack[] afterImage)
    {
        admission=null;queueImage=null;beforeImage=null;afterImage=null;
        string schema=(string)node?.Attribute("version");
        if(schema=="12"||schema=="13"||schema=="14")
        {
            try
            {
                if(!Shape(node,"stationAdmission","version,paymentSource,job,creation,definition,x,y,z,block,stationAddress"+(schema!="12"?",phase":""),
                    "parentAdmission,playerPayment"+(schema=="14"?",ownerPaymentConfirmed":""))||
                    (string)node.Attribute("paymentSource")!="ownerInventory")return false;
                var parentNode=node.Element("parentAdmission");
                if(parentNode.HasAttributes||parentNode.Elements().Count()!=1||
                    parentNode.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
                var original=parentNode.Elements().Single();
                foreach(var attribute in original.Attributes())
                    if(attribute.Name!="version"&&(string)node.Attribute(attribute.Name)!=attribute.Value)return false;
                if((string)original.Attribute("version")!=(schema=="12"?"8":"9")||
                    !TryReadCore(original,definitions,requireDefinition,out var parent,out var actorQueued,out var originalFragments,out var remainingFragments)||
                    parent.IsPlayerPayment||parent.IsMaterialPayment||!parent.IsMobile||
                    !RebirthCamperStationPlayerPaymentImage.TryRead(node.Element("playerPayment"),out var payment)||
                    !payment.MatchesPaid(actorQueued.ingredients))return false;
                var actorPaid=payment.PaidSnapshots();
                if(actorPaid.Length!=originalFragments.Length)return false;
                for(int i=0;i<actorPaid.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(actorPaid[i],originalFragments[i])||
                    !remainingFragments[i].IsEmpty())return false;
                admission=new RebirthStationGridAdmission(node,parent.JobId,parent.CreationId,parent.DefinitionId);
                if(schema=="14")
                {
                    var witness=node.Element("ownerPaymentConfirmed");
                    if(witness==null||witness.HasElements||witness.Attributes().Count()!=6||
                        witness.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
                        (string)witness.Attribute("version")!="1"||(string)witness.Attribute("phase")!="exactNativeOwnerAfterSaved"||
                        (string)witness.Attribute("ownerKey")!=payment.OwnerKey||(string)witness.Attribute("ownerId")!=payment.OwnerId||
                        (string)witness.Attribute("source")!=PaymentSourceHash(payment)||
                        (string)witness.Attribute("operation")!=RebirthCamperStationPlayerPayment.Operation(admission).ToString("N"))
                    {admission=null;return false;}
                }
                queueImage=actorQueued;beforeImage=originalFragments;afterImage=remainingFragments;return true;
            }
            catch{return false;}
        }
        if(schema=="7")
        {
            try
            {
                if(RelocationDepth(node)>8||
                    !Shape(node,"stationAdmission","version,destination,removal,installation,removalAfter,installationAfter","parentAdmission,parentPublication"))return false;
                var parentNode=node.Element("parentAdmission");var publicationNode=node.Element("parentPublication");
                if(parentNode.HasAttributes||publicationNode.HasAttributes||parentNode.Elements().Count()!=1||publicationNode.Elements().Count()!=1||
                    parentNode.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
                    publicationNode.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
                    !TryReadCore(parentNode.Elements().Single(),definitions,requireDefinition,out var parent,out var queue,out var parentBefore,out var parentAfter)||
                    !parent.IsPublicationAttempted||!parent.TryGetMobileAddress(out var source)||
                    !RebirthCamperStationAddress.TryParse((string)node.Attribute("destination"),out var destination)||
                    destination.Installation!=source.Installation||destination.Vehicle==source.Vehicle||
                    !Guid.TryParseExact((string)node.Attribute("removal"),"N",out var removal)||removal==Guid.Empty||
                    !Guid.TryParseExact((string)node.Attribute("installation"),"N",out var installation)||installation==Guid.Empty||removal==installation||
                    new[]{"removalAfter","installationAfter"}.Any(k=>((string)node.Attribute(k)).Length!=64||!((string)node.Attribute(k)).All(Uri.IsHexDigit))||
                    !RebirthCamperStationPublicationRecord.TryRead(publicationNode.Elements().Single(),parent,out _))return false;
                admission=new RebirthStationGridAdmission(node,parent.JobId,parent.CreationId,parent.DefinitionId);
                queueImage=queue;beforeImage=parentBefore;afterImage=parentAfter;return true;
            }
            catch{return false;}
        }
        bool perBatch=schema=="8"||schema=="9"||schema=="10"||schema=="11";
        bool material=schema=="5"||schema=="6"||schema=="10"||schema=="11";
        bool mobile=schema=="3"||schema=="4"||material||perBatch, attempted=schema=="2"||schema=="4"||schema=="6"||schema=="9"||schema=="11";
        if((schema!="1"&&schema!="2"&&schema!="3"&&schema!="4"&&!material&&!perBatch)||
            !Shape(node,"stationAdmission","version,job,creation,definition,x,y,z,block"+(attempted?",phase":"")+(mobile?",stationAddress":""),"before,after,recipe")||
            (attempted&&(string)node.Attribute("phase")!="publicationAttempted")||
            (mobile&&!RebirthCamperStationAddress.TryParse((string)node.Attribute("stationAddress"),out _))||
            !Guid.TryParseExact((string)node.Attribute("job"),"N",out var job)||job==Guid.Empty||
            !TryNormalizeCreation((string)node.Attribute("creation"),out var creation)||
            !Integer(node,"x",out _)||!Integer(node,"y",out _)||!Integer(node,"z",out _)||
            string.IsNullOrEmpty((string)node.Attribute("block"))||((string)node.Attribute("block")).Length>256||
            !Grid(node.Element("before"),out var before)||!Grid(node.Element("after"),out var after)||
            material!=(node.Element("before").Element("materialInput")!=null)||
            material!=(node.Element("after").Element("materialInput")!=null)||
            (material&&!MaterialPair(node.Element("before").Element("materialInput"),node.Element("after").Element("materialInput"))))return false;
        var recipe=node.Element("recipe");
        if(!Shape(recipe,"recipe","type,count,xp,seconds,area","grid")||
            !Integer(recipe,"type",out int type)||type<=0||!Integer(recipe,"count",out int count)||count<1||count>32767||
            !Integer(recipe,"xp",out int xp)||xp<0||
            !float.TryParse((string)recipe.Attribute("seconds"),NumberStyles.Float,CultureInfo.InvariantCulture,out float seconds)||
            float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0||
            ((string)recipe.Attribute("area")).Length>256||
            !RebirthStationGridSnapshotCodec.TryRead(recipe.Element("grid"),out var paid))return false;
        var queued=new Recipe{itemValueType=type,count=count,craftExpGain=xp,craftingTime=seconds,craftingArea=(string)recipe.Attribute("area")};
        queued.ingredients.AddRange(paid);
        if(!RebirthStationGridQueue.TryGetJobId(queued,out var queuedJob)||queuedJob!=job.ToString("N"))return false;
        string definition=(string)node.Attribute("definition");
        if(definition==null||definition.Length!=64||definition.Any(c=>!Uri.IsHexDigit(c))||paid.Length==0||
            paid[0]==null||paid[0].itemValue==null)return false;
        var marker=paid[0].itemValue;
        string prefix=RebirthStationGridQueue.Prefix;
        if(!marker.TryGetMetadata(prefix+"version",out int version)||version!=(perBatch?(material?4:3):material?2:1)||
            !marker.TryGetMetadata(prefix+"definition",out string markedDefinition)||markedDefinition!=definition||
            !marker.TryGetMetadata(prefix+"batches",out int batches)||batches<1||batches>9999||
            !marker.TryGetMetadata(prefix+"tier",out int tier)||tier<0||tier>6||
            !RebirthStationGridQueue.ConservesPayment(before,after,queued))return false;
        if(requireDefinition&&(!RebirthStationGridQueue.TryGetDefinitionBinding(queued,definitions,out var binding)||
            binding.DefinitionId!=definition))return false;
        admission=new RebirthStationGridAdmission(node,job.ToString("N"),creation,definition);
        queueImage=queued;beforeImage=before;afterImage=after;return true;
    }
}
