using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    private sealed class ClientLease{internal EntityPlayerLocal Player;internal World World;internal object Lease;internal string Directory,Payload;internal bool Recovery;}
    private static readonly System.Collections.Generic.Dictionary<Guid,ClientLease> ClientLeases=new System.Collections.Generic.Dictionary<Guid,ClientLease>();
    private static RebirthRemoteResourceClientOriginalJournal ClientPayloads()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","FetchedStationNativePayloads"));
    internal static bool IsClientCandidate(Guid job)
    {
        try
        {
            if(RebirthRemoteResourceClientOriginal.TryRead(job,out var original)&&IsCandidate(original))return true;
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","FetchedStationNativePayloads");
            return Directory.Exists(directory)&&((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateFileSystemEntries(directory,job.ToString("N")+"*").Any()||
                Directory.Exists(Path.Combine(directory,"completed",job.ToString("N"))));
        }
        catch{return true;}
    }
    private static bool SameArea(ItemStack[] left,ItemStack[] right)=>left!=null&&right!=null&&left.Length==right.Length&&
        left.Select((item,index)=>RebirthStationGridIngredients.IsSameStackSnapshot(item,right[index])).All(value=>value);
    private static byte[] CanonicalClientPayload(EntityPlayerLocal player,Guid job,RebirthCamperStationAddress address,string creation,byte[] payload)
    {
        var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(payload),8*1024*1024);
        if(body.Name!="camperNativeActorPayment"||body.Attributes().Count()!=6||body.Elements().Count()!=2||(int?)body.Attribute("version")!=1||
            (int?)body.Attribute("actor")!=player.entityId||(string)body.Attribute("original")!=job.ToString("N")||(string)body.Attribute("creation")!=creation||
            (string)body.Attribute("address")!=address.ToString()||body.Element("before")==null||body.Element("after")==null)throw new InvalidDataException();
        bool retired=(bool)body.Attribute("retired");
        // Normalize only reconnect addressing/terminal transport bookkeeping,
        // never native owner or station Frames. The actual receiver still must
        // authenticate the unnormalized packet and its retirement operation.
        body.SetAttributeValue("actor",0);body.SetAttributeValue("retired",false);
        return Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));
    }
    private static bool ClientPaidRow(EntityPlayerLocal player,RebirthCamperStationAddress address,RebirthRemoteResourceClientOriginal.Intent original,
        byte[] payload,Guid source,string binding,ItemStack[] paid,RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after,out string definition)
    {
        definition=null;
        try
        {
            if(!RebirthCamperStationWindowSession.TryGetCurrent(player,out var live,out var current)||!current.Equals(address)||
                !RebirthCamperModuleRuntime.TryGetExisting(live.BoundVehicle,out var runtime))return false;
            var installed=runtime.Stations.Find(address.Installation)?.InstalledItem;if(installed==null)return false;
            definition=RebirthNativeItemCodec.Encode(installed.itemValue);if(definition.Length>262144||!RebirthNativeItemCodec.TryDecode(definition,out _))return false;
            var envelope=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(payload),8*1024*1024);
            byte[] bytes=Convert.FromBase64String(envelope.Element("after").Value);
            var frame=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(bytes),8*1024*1024);
            var station=RebirthCamperStationSnapshot.Restore(address.Installation,installed,Convert.FromBase64String(frame.Element("stationImage").Value));
            int slot=(int)frame.Attribute("queueSlot");if(slot<0||slot>=station.Queue.Length)return false;
            var row=station.Queue[slot];if(!RebirthCamperStationNativeCrafting.TryJob(row?.Recipe,out var job)||job!=original.Operation||
                !RebirthRemoteResourceFetchedStationPlan.RestoreRecipeFields(row.Recipe)||!RebirthCamperStationNativeCrafting.RestoreActorImages(row.Recipe)||
                !ValidateInput(original,false,out _,out var recipe,out int batches,out int tier,out _,out int tool,out uint block)||
                station.InstalledBlock.rawData!=block||row.Multiplier!=batches||row.Quality!=tier||row.Recipe.GetName()!=recipe.GetName()||row.Recipe.count!=recipe.count||
                row.Recipe.craftingToolType!=tool||row.Recipe.craftingTier!=tier)return false;
            var marker=row.Recipe.ingredients.FirstOrDefault()?.itemValue;
            if(marker==null||!marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey,out string rowSource)||rowSource!=source.ToString("N")||
                !marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.BindingKey,out string rowBinding)||rowBinding!=binding)return false;
            var fetched=new System.Collections.Generic.List<ItemStack>();var local=new System.Collections.Generic.Dictionary<string,int>();
            foreach(var fragment in row.Recipe.ingredients)
            {
                var item=fragment.itemValue;
                if(fragment.count<1||!item.TryGetMetadata("rebirth.camper.native.actor.image",out string image)||!RebirthNativeItemCodec.TryDecode(image,out var value)||
                    !item.TryGetMetadata("rebirth.camper.native.actor.owner",out string owner)||owner!=original.Owner||
                    !item.TryGetMetadata("rebirth.camper.native.actor.source",out string kind)||!item.TryGetMetadata("rebirth.camper.native.actor.slot",out int index))return false;
                if(kind=="fetch"){if(index!=-1)return false;fetched.Add(new ItemStack(value,fragment.count));continue;}
                var prior=kind=="bag"?before.Bag:kind=="belt"?before.Belt:null;var next=kind=="bag"?after.Bag:kind=="belt"?after.Belt:null;
                if(prior==null||index<0||index>=prior.Length||RebirthNativeItemCodec.Encode(prior[index].itemValue)!=image||prior[index].count-next[index].count!=fragment.count||
                    !local.TryAdd(kind+":"+index,fragment.count))return false;
            }
            for(int area=0;area<2;area++)
            {var prior=area==0?before.Bag:before.Belt;var next=area==0?after.Bag:after.Belt;for(int i=0;i<prior.Length;i++)if(prior[i].count-next[i].count>0&&!local.ContainsKey((area==0?"bag:":"belt:")+i))return false;}
            return RebirthCamperInventorySnapshot.Capture(fetched.ToArray()).SequenceEqual(RebirthCamperInventorySnapshot.Capture(paid));
        }
        catch{definition=null;return false;}
    }
    // Caller must be the existing native payload receiver with its exact
    // authenticated server/connection and native-decoded Frame arrays. This
    // helper does not accept a server replica as an original owner image.
    internal static bool RetainClientPayload(EntityPlayerLocal player,Guid job,RebirthCamperStationAddress address,string creation,byte[] actualPayload,
        ItemStack[] beforeBag,ItemStack[] beforeBelt,ItemStack[] afterBag,ItemStack[] afterBelt,Func<bool> authenticatedCurrent)
    {
        try
        {
            if(authenticatedCurrent?.Invoke()!=true||actualPayload==null||actualPayload.Length>8*1024*1024||
                !RebirthRemoteResourceClientOriginal.TryRead(job,out var original)||!RebirthRemoteResourceClientOriginal.Current(player,original)||
                !ValidateInput(original,false,out var declared,out _,out _,out _,out int belt,out _,out _)||!declared.Equals(address)||creation!=original.Creation||belt!=player.inventory.Length||
                !RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out string sourceWorld,out var source,out string binding,out var paid)||
                !RebirthCamperStationWindowSession.TryGetCurrent(player,out _,out var leaseAddress)||!leaseAddress.Equals(address)||
                !RebirthCamperStationWindowSession.TryGetLeaseIdentity(player,out var lease))return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value);
            var requirements=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("requirements").Value));
            if(!RebirthRemoteResourceCraftPlan.IsExactShortfall(before,requirements,paid,belt)||!RebirthRemoteResourceCraftPlan.TryDebit(before,requirements,paid,belt,out var after,out _)||
                !SameArea(before.Bag,beforeBag)||!SameArea(before.Belt,beforeBelt)||!SameArea(after.Bag,afterBag)||!SameArea(after.Belt,afterBelt)||
                !ClientPaidRow(player,address,original,actualPayload,source,binding,paid,before,after,out string definition))return false;
            var canonicalPayload=CanonicalClientPayload(player,job,address,creation,actualPayload);
            string hash=RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(canonicalPayload));
            foreach(var old in ClientLeases.Where(pair=>!RebirthRemoteResourceOwnerAction.HasFetchedStationHandoff(pair.Key)).Select(pair=>pair.Key).ToArray())ClientLeases.Remove(old);
            if(ClientLeases.Count>=32&&!ClientLeases.ContainsKey(job))return false;
            bool leaseChanged=ClientLeases.TryGetValue(job,out var existing)&&(!ReferenceEquals(existing.Player,player)||!ReferenceEquals(existing.World,player.world)||
                !ReferenceEquals(existing.Lease,lease)||existing.Directory!=GameIO.GetSaveGameDir()||existing.Payload!=hash);
            if(existing!=null&&existing.Payload!=hash)return false;
            var body=new XElement("fetchedStationNativePayload",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("job",job.ToString("N")),
                new XAttribute("address",address.ToString()),new XAttribute("source",source.ToString("N")),new XAttribute("sourceWorld",sourceWorld),new XAttribute("binding",binding),
                new XAttribute("payload",hash),new XElement("native",Convert.ToBase64String(canonicalPayload)),new XElement("definition",definition));
            byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));if(bytes.Length>12*1024*1024)return false;
            var journal=ClientPayloads();bool hadAppliedPayload=journal.Read(job)?.State==RebirthCamperTransferJournal.Phase.Applied;
            var record=journal.Prepare(job,address.Vehicle,creation,0,bytes,bytes);
            if(record.Vehicle!=address.Vehicle||record.OwnerCreation!=creation||record.Revision!=0||!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes)||authenticatedCurrent()!=true)return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(job,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(job,record.State,RebirthCamperTransferJournal.Phase.Applied);
            if(record.State!=RebirthCamperTransferJournal.Phase.Applied||authenticatedCurrent()!=true||
                !RebirthCamperStationWindowSession.TryGetLeaseIdentity(player,out var finalLease)||!ReferenceEquals(lease,finalLease))return false;
            bool recovered=false;
            if(leaseChanged||!RebirthRemoteResourceOwnerAction.BindFetchedStationHandoff(player,original,source,binding,hash,before,after,authenticatedCurrent))
            {
                var raw=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(actualPayload),8*1024*1024);
                Guid effect=(bool)raw.Attribute("retired")?NativeRetirementOperation(job):job;
                bool Proof()=>authenticatedCurrent?.Invoke()==true&&RebirthCamperStationNativeActorPayment.TryReadFetchedClientRecovery(player,effect,actualPayload,before,after,hadAppliedPayload,out _);
                if(!RebirthRemoteResourceOwnerAction.BindRecoveredFetchedStationHandoff(player,original,source,binding,hash,before,after,authenticatedCurrent,Proof))return false;
                recovered=true;
            }
            foreach(var old in ClientLeases.Where(pair=>!ReferenceEquals(pair.Value.World,GameManager.Instance?.World)||pair.Value.Directory!=GameIO.GetSaveGameDir()).Select(pair=>pair.Key).ToArray())ClientLeases.Remove(old);
            ClientLeases[job]=new ClientLease{Player=player,World=player.world,Lease=lease,Directory=GameIO.GetSaveGameDir(),Payload=hash,Recovery=recovered};return true;
        }
        catch{return false;}
    }
    internal static bool HasClientRecoveryBinding(EntityPlayerLocal player,Guid job,byte[] payload)
    {
        try
        {
            if(!ClientLeases.TryGetValue(job,out var held)||!held.Recovery||!ReferenceEquals(held.Player,player)||!ReferenceEquals(held.World,player.world)||held.Directory!=GameIO.GetSaveGameDir()||
                !RebirthRemoteResourceClientOriginal.TryRead(job,out var original)||!RebirthRemoteResourceClientOriginal.Current(player,original)||
                !ValidateInput(original,false,out var address,out _,out _,out _,out _,out _,out _)||
                !RebirthCamperStationWindowSession.TryGetLeaseIdentity(player,out var lease)||!ReferenceEquals(lease,held.Lease))return false;
            return held.Payload==RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(CanonicalClientPayload(player,job,address,original.Creation,payload)))&&
                RebirthRemoteResourceOwnerAction.HasFetchedStationHandoff(job);
        }
        catch{return false;}
    }
    internal static bool MatchesClientPayload(EntityPlayerLocal player,Guid job,RebirthCamperStationAddress address,string creation,byte[] actualPayload,
        ItemStack[] beforeBag,ItemStack[] beforeBelt,ItemStack[] afterBag,ItemStack[] afterBelt)
    {
        try
        {
            if(actualPayload==null||actualPayload.Length>8*1024*1024||!RebirthRemoteResourceClientOriginal.TryRead(job,out var original)||
                !RebirthRemoteResourceClientOriginal.Current(player,original)||!IsCandidate(original)||original.Creation!=creation||
                !ClientLeases.TryGetValue(job,out var held)||!ReferenceEquals(held.Player,player)||!ReferenceEquals(held.World,player.world)||held.Directory!=GameIO.GetSaveGameDir()||
                !RebirthCamperStationWindowSession.TryGetCurrent(player,out _,out var currentAddress)||!currentAddress.Equals(address)||
                !RebirthCamperStationWindowSession.TryGetLeaseIdentity(player,out var currentLease)||!ReferenceEquals(currentLease,held.Lease))return false;
            var record=ClientPayloads().Read(job);if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=address.Vehicle||record.OwnerCreation!=creation||record.Revision!=0||
                record.Before.Length>12*1024*1024||!record.Before.SequenceEqual(record.After))return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),12*1024*1024);
            var canonicalPayload=CanonicalClientPayload(player,job,address,creation,actualPayload);
            string hash=RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(canonicalPayload));
            if(body.Name!="fetchedStationNativePayload"||body.Attributes().Count()!=8||body.Elements().Count()!=2||body.Element("native")==null||body.Element("definition")==null||
                body.Nodes().Any(node=>node is not XElement)||body.Elements().Any(node=>node.HasAttributes||node.Nodes().Any(child=>child is not XText))||
                body.Element("definition").Value.Length>262144||!RebirthNativeItemCodec.TryDecode(body.Element("definition").Value,out _)||
                (int?)body.Attribute("version")!=1||(string)body.Attribute("original")!=original.Digest||(string)body.Attribute("job")!=job.ToString("N")||
                (string)body.Attribute("address")!=address.ToString()||(string)body.Attribute("payload")!=hash||held.Payload!=hash||body.Element("native").Value!=Convert.ToBase64String(canonicalPayload)||
                !RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out var sourceWorld,out var source,out var binding,out _)||
                (string)body.Attribute("source")!=source.ToString("N")||(string)body.Attribute("sourceWorld")!=sourceWorld||(string)body.Attribute("binding")!=binding)return false;
            return RebirthRemoteResourceOwnerAction.MatchesFetchedStationHandoff(player,job,hash,beforeBag,beforeBelt,afterBag,afterBelt);
        }
        catch{return false;}
    }
    internal static bool TryGetClientPayloadBinding(EntityPlayerLocal player,Guid job,RebirthCamperStationAddress address,string creation,byte[] actualPayload,
        ItemStack[] beforeBag,ItemStack[] beforeBelt,ItemStack[] afterBag,ItemStack[] afterBelt,out string binding)
    {
        binding=null;
        try
        {
            if(!MatchesClientPayload(player,job,address,creation,actualPayload,beforeBag,beforeBelt,afterBag,afterBelt))return false;
            binding=RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(CanonicalClientPayload(player,job,address,creation,actualPayload)));return true;
        }
        catch{binding=null;return false;}
    }
    internal static bool TryReadClientPayload(EntityPlayerLocal player,Guid job,out byte[] payload)
    {
        payload=null;
        try
        {
            if(!RebirthRemoteResourceClientOriginal.TryRead(job,out var original)||!RebirthRemoteResourceClientOriginal.Current(player,original)||!IsCandidate(original))return false;
            var record=ClientPayloads().Read(job);if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.OwnerCreation!=original.Creation||record.Revision!=0||
                record.Before.Length>12*1024*1024||!record.Before.SequenceEqual(record.After))return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),12*1024*1024);
            if(body.Name!="fetchedStationNativePayload"||body.Element("native")==null)return false;
            var native=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(Convert.FromBase64String(body.Element("native").Value)),8*1024*1024);
            if(native.Name!="camperNativeActorPayment"||(int?)native.Attribute("actor")!=0||(bool?)native.Attribute("retired")!=false)return false;
            native.SetAttributeValue("actor",player.entityId);payload=Encoding.UTF8.GetBytes(native.ToString(SaveOptions.DisableFormatting));return true;
        }
        catch{payload=null;return false;}
    }
}
