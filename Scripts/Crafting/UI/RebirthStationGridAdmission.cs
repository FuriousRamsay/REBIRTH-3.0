using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Immutable preparation record. Persistence/queue commit must be coordinated by its owner.
public sealed class RebirthStationGridAdmission
{
    private readonly XElement image;
    private readonly ItemStack[] paidSnapshot;
    public bool IsPublicationAttempted=>(string)image.Attribute("phase")=="publicationAttempted";
    public string JobId {get;private set;}
    public string CreationId {get;private set;}
    public string DefinitionId {get;private set;}
    private RebirthStationGridAdmission(XElement node,string job,string creation,string definition)
    {image=new XElement(node);JobId=job;CreationId=creation;DefinitionId=definition;
     RebirthStationGridSnapshotCodec.TryRead(image.Element("recipe")?.Element("grid"),out paidSnapshot);}
    // Unresolved preparations at the same station share one native input inventory.
    public bool SharesStation(RebirthStationGridAdmission other)
        =>other!=null &&
            (int)image.Attribute("x")==(int)other.image.Attribute("x") &&
            (int)image.Attribute("y")==(int)other.image.Attribute("y") &&
            (int)image.Attribute("z")==(int)other.image.Attribute("z");
    public XElement Write()=>new XElement(image);
    public RebirthStationGridAdmission Clone()=>new RebirthStationGridAdmission(image,JobId,CreationId,DefinitionId);
    public bool TryMarkPublicationAttempted(out RebirthStationGridAdmission attempted)
    {
        attempted=null;if(IsPublicationAttempted)return false;
        var next=Write();next.SetAttributeValue("version",2);next.SetAttributeValue("phase","publicationAttempted");
        attempted=new RebirthStationGridAdmission(next,JobId,CreationId,DefinitionId);return true;
    }

    public static bool TryCreate(Guid job,Guid creation,int x,int y,int z,string stationBlock,
        RebirthStationGridIngredients.Plan plan,IList<ItemStack> before,float seconds,IList<Recipe> definitions,
        out RebirthStationGridAdmission admission)
        =>TryCreate(job,creation.ToString("N"),x,y,z,stationBlock,plan,before,seconds,definitions,out admission);

    public static bool TryNormalizeCreation(string value,out string normalized)
        =>RebirthSurvivorRequestScope.TryNormalize(value,out normalized);

    public static bool TryCreate(Guid job,string creation,int x,int y,int z,string stationBlock,
        RebirthStationGridIngredients.Plan plan,IList<ItemStack> before,float seconds,IList<Recipe> definitions,
        out RebirthStationGridAdmission admission)
    {
        admission=null;
        if(job==Guid.Empty||!TryNormalizeCreation(creation,out var creationKey)||string.IsNullOrEmpty(stationBlock)||stationBlock.Length>256||
            !RebirthStationNativeInputCodec.IsRoundTrippable(before)||
            !RebirthStationGridQueue.TryPrepare(plan,before,seconds,definitions,out var queued,out var after,out var binding)||
            !RebirthStationGridQueue.TryBindJob(queued,job)||
            !RebirthStationGridSnapshotCodec.TryWrite(before,out var beforeImage)||
            !RebirthStationGridSnapshotCodec.TryWrite(after,out var afterImage)||
            !RebirthStationGridSnapshotCodec.TryWrite(queued.ingredients,out var paidImage))return false;
        var node=new XElement("stationAdmission",new XAttribute("version",1),
            new XAttribute("job",job.ToString("N")),new XAttribute("creation",creationKey),
            new XAttribute("definition",binding.DefinitionId),new XAttribute("x",x),new XAttribute("y",y),new XAttribute("z",z),
            new XAttribute("block",stationBlock),
            new XElement("before",beforeImage),new XElement("after",afterImage),
            new XElement("recipe",new XAttribute("type",queued.itemValueType),new XAttribute("count",queued.count),
                new XAttribute("xp",queued.craftExpGain),new XAttribute("seconds",queued.craftingTime.ToString("R",CultureInfo.InvariantCulture)),
                new XAttribute("area",queued.craftingArea??""),paidImage));
        return TryRead(node,definitions,out admission);
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
            RebirthStationGridSnapshotCodec.TryRead(wrapper.Element("grid"),out slots);
    }
    // Stored records preserve payment images even when their recipe is no longer available.
    // Successful storage parsing is NOT permission to enqueue, refund or award progression.
    public static bool TryReadStored(XElement node,out RebirthStationGridAdmission admission)
        =>TryReadCore(node,null,false,out admission,out _,out _,out _);

    public bool TryResolve(IList<Recipe> definitions,out RebirthStationGridAdmission resolved)
        =>TryRead(image,definitions,out resolved);

    public static bool TryRead(XElement node,IList<Recipe> definitions,out RebirthStationGridAdmission admission)
        =>TryReadCore(node,definitions,true,out admission,out _,out _,out _);

    // Detached values only. Caller still needs authenticated custody and a durable commit.
    public bool TryMaterialize(IList<Recipe> definitions,out Recipe queued,out ItemStack[] before,out ItemStack[] after)
    {
        queued=null;before=null;after=null;
        if(!TryReadCore(image,definitions,true,out _,out var candidate,out var original,out var remainder)||
            !RebirthStationGridQueue.TryRestore(candidate,definitions))return false;
        queued=candidate;before=original;after=remainder;return true;
    }
    public bool TryGetPhysicalSlotCount(out int count)
    {
        count=0;
        if(!Grid(image.Element("before"),out var slots))return false;
        count=slots.Length;return count>0&&count<=RebirthStationInputLayout.PhysicalSlots;
    }
    // Pre-payment recovery witness. Exact original grid, never a refund or debit grant.
    public bool MatchesUnpaidObservation(string creation,int x,int y,int z,string block,IList<ItemStack> input)
    {
        if(IsPublicationAttempted||!TryNormalizeCreation(creation,out var current)||current!=CreationId||
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
        if(!TryNormalizeCreation(creation,out var owner)||owner!=CreationId||
            (string)image.Attribute("block")!=block||!Integer(image,"x",out var sx)||sx!=x||
            !Integer(image,"y",out var sy)||sy!=y||!Integer(image,"z",out var sz)||sz!=z||
            !MatchesQueuedRecipe(queued,multiplier))return false;
        return RebirthStationGridSnapshotCodec.TryWrite(input,out var observedInput)&&
            XNode.DeepEquals(observedInput,image.Element("after").Element("grid"));
    }
    // Ongoing recipe identity check; later legitimate input additions do not rewrite paid ingredients.
    internal bool MatchesQueuedRecipe(Recipe queued,int multiplier)
    {
        if(multiplier!=1||queued==null||queued.IsScrap||queued.materialBasedRecipe||
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
    private static bool TryReadCore(XElement node,IList<Recipe> definitions,bool requireDefinition,
        out RebirthStationGridAdmission admission,out Recipe queueImage,out ItemStack[] beforeImage,out ItemStack[] afterImage)
    {
        admission=null;queueImage=null;beforeImage=null;afterImage=null;
        bool attempted=(string)node?.Attribute("version")=="2";
        if(!Shape(node,"stationAdmission","version,job,creation,definition,x,y,z,block"+(attempted?",phase":""),"before,after,recipe")||
            (attempted?(string)node.Attribute("phase")!="publicationAttempted":(string)node.Attribute("version")!="1")||
            !Guid.TryParseExact((string)node.Attribute("job"),"N",out var job)||job==Guid.Empty||
            !TryNormalizeCreation((string)node.Attribute("creation"),out var creation)||
            !Integer(node,"x",out _)||!Integer(node,"y",out _)||!Integer(node,"z",out _)||
            string.IsNullOrEmpty((string)node.Attribute("block"))||((string)node.Attribute("block")).Length>256||
            !Grid(node.Element("before"),out var before)||!Grid(node.Element("after"),out var after))return false;
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
        if(!marker.TryGetMetadata(prefix+"version",out int version)||version!=1||
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