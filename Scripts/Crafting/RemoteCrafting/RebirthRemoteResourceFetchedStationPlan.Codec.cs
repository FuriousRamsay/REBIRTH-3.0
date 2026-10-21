using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Immutable domain artifact codec only. A decoded original is not a setter
// permit: the coordinator must independently prove root2/full native Before,
// actual current lease/life and its retained original custody before Begin.
internal static partial class RebirthRemoteResourceFetchedStationPlan
{
    private static string Actor(RebirthCamperEnergyActorImage actor)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {actor.Write(writer);writer.Flush();if(stream.Length>12*1024*1024)throw new InvalidDataException();return Convert.ToBase64String(stream.ToArray());}
    }
    private static RebirthCamperEnergyActorImage ReadActor(string text)
    {
        if(text==null||text.Length>16*1024*1024)throw new InvalidDataException();
        using(var stream=new MemoryStream(Convert.FromBase64String(text),false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
        {var actor=RebirthCamperEnergyActorImage.Read(reader);if(stream.Position!=stream.Length||Actor(actor)!=text)throw new InvalidDataException();return actor;}
    }
    internal static XElement Image(Plan plan)=>new XElement("fetchedStationPlan",new XAttribute("version",1),new XAttribute("job",plan.Job.ToString("N")),
        new XAttribute("source",plan.Source.ToString("N")),new XAttribute("binding",plan.Binding),new XAttribute("sourceWorld",plan.SourceWorld),new XAttribute("world",plan.World),
        new XAttribute("address",plan.Address),new XAttribute("creation",plan.Creation),new XAttribute("ownerKey",plan.OwnerKey),new XAttribute("owner",plan.CanonicalOwner),
        new XAttribute("deaths",plan.Deaths),new XAttribute("slot",plan.QueueSlot),new XAttribute("batches",plan.Batches),new XAttribute("tier",plan.Tier),new XAttribute("belt",plan.OwnedBelt),new XAttribute("tool",plan.RequiredTool),
        new XElement("baseRecipe",plan.BaseRecipe),new XElement("definition",plan.InstalledDefinition),new XElement("beforeActor",Actor(plan.CopyBeforeActor())),new XElement("afterActor",Actor(plan.CopyAfterActor())),
        new XElement("beforeStation",Convert.ToBase64String(plan.CopyBeforeStation())),new XElement("afterStation",Convert.ToBase64String(plan.CopyAfterStation())));
    internal static bool TryDecode(RebirthRemoteResourceDurableJournal.Record actualSource,string text,out Plan plan)
    {
        plan=null;
        try
        {
            if(!ThreadManager.IsMainThread()||actualSource==null||text==null||Encoding.UTF8.GetByteCount(text)>64*1024*1024)return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,64*1024*1024);
            string[] attributes={"version","job","source","binding","sourceWorld","world","address","creation","ownerKey","owner","deaths","slot","batches","tier","belt","tool"};
            string[] elements={"baseRecipe","definition","beforeActor","afterActor","beforeStation","afterStation"};
            if(body.Name!="fetchedStationPlan"||body.Attributes().Count()!=attributes.Length||attributes.Any(name=>body.Attribute(name)==null)||body.Elements().Count()!=elements.Length||
                elements.Any(name=>body.Element(name)==null)||body.Nodes().Any(node=>node is not XElement)||body.Elements().Any(node=>node.HasAttributes||node.Nodes().Any(child=>child is not XText))||
                (int?)body.Attribute("version")!=1||!Guid.TryParseExact((string)body.Attribute("job"),"N",out var job)||job==Guid.Empty||
                (string)body.Attribute("source")!=actualSource.Operation.ToString("N")||(string)body.Attribute("binding")!=actualSource.Binding||(string)body.Attribute("sourceWorld")!=actualSource.World||
                (string)body.Attribute("ownerKey")!=actualSource.Owner||(string)body.Attribute("creation")!=actualSource.Creation||
                !RebirthCamperStationAddress.TryParse((string)body.Attribute("address"),out var address)||!Guid.TryParse((string)body.Attribute("world"),out _))return false;
            var root=actualSource.Copy();int deaths=(int)body.Attribute("deaths"),slot=(int)body.Attribute("slot"),batches=(int)body.Attribute("batches"),tier=(int)body.Attribute("tier"),belt=(int)body.Attribute("belt"),tool=(int)body.Attribute("tool");
            string owner=(string)body.Attribute("owner"),definition=body.Element("definition").Value,baseRecipe=body.Element("baseRecipe").Value;
            if((int)root.Attribute("kind")!=(int)RemoteResourceClientOperation.Craft||deaths<0||deaths!=(int)root.Attribute("deaths")||string.IsNullOrEmpty(owner)||owner.Length>512||
                batches<1||batches>9999||tier<0||tier>6||belt<0||belt>20||tool<0||tool!=0&&ItemClass.GetForId(tool)==null||definition.Length>262144||!RebirthNativeItemCodec.TryDecode(definition,out var installed)||
                !RebirthRemoteResourceCraftRecipe.TryRead(baseRecipe,out var authored)||authored.IsScrap||authored.materialBasedRecipe)return false;
            var before=ReadActor(body.Element("beforeActor").Value);var after=ReadActor(body.Element("afterActor").Value);
            byte[] prior=Convert.FromBase64String(body.Element("beforeStation").Value),next=Convert.FromBase64String(body.Element("afterStation").Value);
            if(prior.Length>8*1024*1024||next.Length>8*1024*1024||Convert.ToBase64String(prior)!=body.Element("beforeStation").Value||Convert.ToBase64String(next)!=body.Element("afterStation").Value)return false;
            var oldStation=RebirthCamperStationSnapshot.Restore(address.Installation,new ItemStack(installed,1),prior);
            var result=new Plan(job,actualSource,address,owner,(string)body.Attribute("world"),deaths,slot,batches,tier,belt,tool,baseRecipe,definition,before,after,prior,next);
            var newStation=result.RestoreAfterStation();
            if(slot<0||slot>=oldStation.Queue.Length||slot>=newStation.Queue.Length||oldStation.Queue.Length!=newStation.Queue.Length||belt>before.Belt.Length||
                !RebirthStationNativeQueueEntry.TryFindVacantSlot(oldStation.Queue,job,out int vacant)||vacant!=slot)return false;
            var row=newStation.Queue[slot];var recipe=row?.Recipe;
            if(recipe==null||row.Multiplier!=batches||row.Quality!=tier||recipe.craftingToolType!=tool||recipe.craftingTier!=tier||recipe.itemValueType!=authored.itemValueType||recipe.count!=authored.count||recipe.craftingArea!=authored.craftingArea||
                recipe.ingredients.Count<1||recipe.ingredients.Count>8192+189||!RebirthCamperStationNativeCrafting.TryJob(recipe,out var marked)||marked!=job)return false;
            var first=recipe.ingredients[0].itemValue;
            if(!first.TryGetMetadata(SourceKey,out string source)||source!=actualSource.Operation.ToString("N")||!first.TryGetMetadata(BindingKey,out string binding)||binding!=actualSource.Binding)return false;
            var remote=root.Element("sources").Elements("source").SelectMany(node=>RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Element("paid").Value))).ToArray();
            var fetch=recipe.ingredients.Where(fragment=>fragment.itemValue.TryGetMetadata(ActorSource,out string kind)&&kind=="fetch").ToArray();
            if(fetch.Length!=remote.Length)return false;
            for(int i=0;i<fetch.Length;i++)if(fetch[i].count!=remote[i].count||!fetch[i].itemValue.TryGetMetadata(ActorImage,out string image)||image!=RebirthNativeItemCodec.Encode(remote[i].itemValue)||
                !fetch[i].itemValue.TryGetMetadata(ActorSlot,out int origin)||origin!=-1)return false;
            // Quantity-only scratch requirements. All actual delivered/refund
            // values remain the retained rich fragments; these defaults are
            // never written into an owner, queue or resource source.
            var needs=recipe.ingredients.GroupBy(fragment=>fragment.itemValue.type).Select(group=>new ItemStack(ItemClass.GetItem(group.First().itemValue.ItemClass.GetItemName()),checked(group.Sum(fragment=>fragment.count)))).ToArray();
            if(!RebirthRemoteResourceCraftPlan.IsExactShortfall(before,needs,remote,belt)||!RebirthRemoteResourceCraftPlan.TryDebit(before,needs,remote,belt,out var calculated,out _)||!calculated.Matches(after))return false;
            var origins=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach(var fragment in recipe.ingredients)
            {
                if(fragment==null||fragment.IsEmpty()||fragment.count<1||fragment.count>ushort.MaxValue||!fragment.itemValue.TryGetMetadata(ActorOwner,out string originalOwner)||originalOwner!=owner||
                    !fragment.itemValue.TryGetMetadata(ActorSource,out string kind)||!fragment.itemValue.TryGetMetadata(ActorSlot,out int origin)||
                    !fragment.itemValue.TryGetMetadata(ActorImage,out string image)||!RebirthNativeItemCodec.TryDecode(image,out var originalValue))return false;
                if(kind=="fetch")continue;
                var cells=kind=="bag"?before.Bag:kind=="belt"?before.Belt:null;
                var nextCells=kind=="bag"?after.Bag:after.Belt;
                if(cells==null||origin<0||origin>=cells.Length||kind=="belt"&&origin>=belt||!origins.Add(kind+":"+origin)||
                    fragment.count!=cells[origin].count-(nextCells[origin].IsEmpty()?0:nextCells[origin].count)||
                    RebirthNativeItemCodec.Encode(cells[origin].itemValue)!=RebirthNativeItemCodec.Encode(originalValue))return false;
            }
            if(Image(result).ToString(SaveOptions.DisableFormatting)!=text)return false;plan=result;return true;
        }
        catch{plan=null;return false;}
    }
}
