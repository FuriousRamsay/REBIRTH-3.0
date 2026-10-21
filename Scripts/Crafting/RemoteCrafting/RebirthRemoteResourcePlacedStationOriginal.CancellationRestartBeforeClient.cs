using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    // Created exclusively by the actual authenticated PlayerId/ToPlayer pair.
    // Hydration establishes a hold; a separate authenticated host reconstruction
    // must qualify the remaining Prepared commands before any new setter.
    internal sealed class CancellationOwnerBeforeHydrated
    {
        internal readonly CancellationClient Original;
        private readonly CancellationJoining joining;private readonly World world;private readonly EntityPlayerLocal actor;
        private readonly ItemStackGrid bag,belt;private readonly string creation,owner,permission;private readonly int entity,deaths;
        private bool accepted,granted;internal bool MayResume;internal string Prefix;
        private CancellationOwnerBeforeHydrated(CancellationClient original,CancellationJoining source)
        {Original=original;joining=source;world=source.World;actor=source.Actor;bag=actor.bag.ItemGrid;belt=actor.inventory.ItemGrid;
            entity=actor.entityId;deaths=original.Original.Deaths;creation=original.Original.Creation;owner=original.Original.Owner;permission=original.Permission;}
        internal static CancellationOwnerBeforeHydrated FromActualHydration(CancellationClient original,CancellationJoining source)
        {
            if(!ReferenceEquals(cancellationJoining,source)||!CancellationJoinConnectionCurrent(source)||source.Reads!=1||original.Stage!=1||
                !ReferenceEquals(source.Actor,original.Actor)||source.File?.bLoaded!=true||source.File.bDead||source.File.id!=original.NativeOwnerId||
                source.Actor.entityId!=original.NativeOwnerId||source.File.deaths!=original.Original.Deaths||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(source.File,CancellationReceipt(original.Operation),out float phase,out bool present)||!present||phase!=1f||
                source.Actor.Buffs.GetCustomVar(CancellationReceipt(original.Operation))!=1f)return null;
            var value=new CancellationOwnerBeforeHydrated(original,source);
            value.MayResume=ReadRestartOwnerProgress(original,source,out var progress,out string prefix);
            value.Prefix=prefix;original.OwnerProgress=value.MayResume?progress:null;
            return value.Current?value:null;
        }
        internal void NativeJoinCompleted(CancellationJoining source)
        {if(ReferenceEquals(joining,source)&&ReferenceEquals(cancellationJoining,source)&&CancellationJoinConnectionCurrent(source)&&source.Reads==1)accepted=true;}
        internal bool Accepted=>accepted&&Current;
        internal bool Current=>ThreadManager.IsMainThread()&&(accepted||ReferenceEquals(cancellationJoining,joining))&&CancellationJoinConnectionCurrent(joining)&&
            Original.Memory?.Current==true&&Original.Directory==GameIO.GetSaveGameDir()&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(actor.world,world)&&
            ReferenceEquals(Original.Actor,actor)&&actor.entityId==entity&&!actor.IsDead()&&actor.Died==deaths&&
            ReferenceEquals(actor.bag?.ItemGrid,bag)&&ReferenceEquals(actor.inventory?.ItemGrid,belt)&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.CanonicalId==owner&&RebirthCamperCharacterAccess.CurrentCreation(actor)==creation&&Original.Permission==permission&&
            (actor.Buffs.GetCustomVar(CancellationReceipt(Original.Operation))==1f||actor.Buffs.GetCustomVar(CancellationReceipt(Original.Operation))==2f||actor.Buffs.GetCustomVar(CancellationReceipt(Original.Operation))==3f);
        internal bool PrefixCurrent=>Current&&MayResume&&Prefix!=null&&RebirthRemoteResourceClientOriginal.ReadActor(Prefix).Matches(RebirthCamperEnergyActorImage.Capture(actor));
        internal bool FinancialCurrent=>accepted&&granted&&Current&&MayResume&&actor.Buffs.GetCustomVar(CancellationReceipt(Original.Operation))==1f;
        internal bool Accept(string text)
        {
            var native=cancellationNativeResult;
            if(granted)return FinancialCurrent&&native?.Current==true&&native.FullPermission==permission;
            if(!accepted||!PrefixCurrent||native?.Current!=true||!ReferenceEquals(native.Actor,actor)||!ReferenceEquals(native.Connection,joining.Connection)||
                native.Operation!=Original.Original.Operation||native.Reconstruction!=text||native.FullPermission!=permission||text==null)return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,MaximumResultBytes);
            if(body.Name!="placedCancellationBeforeReconstruction"||body.Attributes().Count()!=7||body.Elements().Count()!=4||(int?)body.Attribute("version")!=1||
                (string)body.Attribute("cancel")!=Original.Operation.ToString("N")||(string)body.Attribute("source")!=Original.Source.ToString("N")||
                (string)body.Attribute("birth")!=Original.Birth||(int?)body.Attribute("ownerId")!=entity||(string)body.Attribute("creation")!=creation||(int?)body.Attribute("deaths")!=deaths||
                body.Element("ownerPrefix")?.Value!=Prefix)return false;
            var permissionBody=RebirthCookingRemoteAdmission.ReadProtocolXml(permission,MaximumResultBytes);
            if(body.Element("savedBefore")?.Value!=permissionBody.Element("beforeEvidence")?.Value)return false;
            var custody=RebirthCookingRemoteAdmission.ReadProtocolXml(body.Element("hostCustody")?.Value,MaximumResultBytes);
            if(custody.Name!="placedCancellationHostCustody"||custody.Attributes().Count()!=9||custody.Elements().Count()!=4||(int?)custody.Attribute("version")!=2||
                (string)custody.Attribute("cancel")!=Original.Operation.ToString("N")||(string)custody.Attribute("source")!=Original.Source.ToString("N")||
                (string)custody.Attribute("binding")!=Original.Binding||(string)custody.Attribute("birth")!=Original.Birth||(string)custody.Attribute("world")!=Original.Original.World||
                (string)custody.Attribute("owner")!=owner||(string)custody.Attribute("creation")!=creation||(int?)custody.Attribute("deaths")!=deaths||
                custody.Element("request")?.Value!=Original.Header||custody.Element("permission")?.Value!=permission||custody.Element("savedBefore")?.Value!=body.Element("savedBefore")?.Value)return false;
            var origin=RebirthCookingRemoteAdmission.ReadProtocolXml(body.Element("nativeLoad")?.Value,MaximumResultBytes);
            bool retained=origin.Name=="placedCancellationRetainedNativeOriginal"&&(int?)origin.Attribute("version")==1&&origin.Attributes().Count()==7&&origin.Elements().Count()==1&&
                origin.Element("acceptedNativeOriginal")?.Value==custody.Element("acceptedNativeOriginal")?.Value;
            if(!retained&&(origin.Name!="placedCancellationReconstruction"||(int?)origin.Attribute("version")!=2||origin.Attributes().Count()!=12||origin.HasElements)||
                (string)origin.Attribute("cancel")!=Original.Operation.ToString("N")||(string)origin.Attribute("source")!=Original.Source.ToString("N")||
                (string)origin.Attribute("binding")!=Original.Binding||(string)origin.Attribute("birth")!=Original.Birth||(string)origin.Attribute("world")!=Original.Original.World||
                !Guid.TryParseExact((string)origin.Attribute("recovery"),"N",out var recovery)||recovery==Guid.Empty||!retained&&((long?)origin.Attribute("generation") is not long generation||generation<1))return false;
            granted=true;ObserveCancellationScenarioEvent(Original.Operation,"actualBeforeGrant",text);return FinancialCurrent;
        }
    }
    private static bool AcceptRestartBeforeGrant(CancellationClient original,string text)
        =>original.RestartBeforeOwner!=null&&original.RestartBeforeOwner.Accept(text);
    private static void HydrateRestartBefore(CancellationClient original,CancellationJoining joining)
    {
        var proof=CancellationOwnerBeforeHydrated.FromActualHydration(original,joining);if(proof==null||cancellationClients.ContainsKey(original.Operation))return;
        original.RestartBeforeOwner=proof;cancellationClients.Add(original.Operation,original);
        RebirthRemoteResourceOwnerAction.HoldRestartBeforeCancellation(proof);
    }
    private static bool ReadRestartOwnerProgress(CancellationClient original,CancellationJoining joining,out CancellationOwnerProgress progress,out string prefix)
    {
        progress=null;prefix=null;
        try
        {
            long floor=original.Memory.ReservedBytes;
            if(!original.Memory.Reserve(checked(floor+128L*1024*1024)))return false;
            string manifest=ReadArchivedCancellation(original.Operation,original.Source,original.Original.Creation,"OwnerPlan",original.Memory,RebirthCamperTransferJournal.Phase.Applied);
            if(manifest==null)return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(manifest,MaximumResultBytes);
            string before=RebirthRemoteResourceClientOriginal.ActorImage(original.Before),after=RebirthRemoteResourceClientOriginal.ActorImage(original.After);
            if(body.Name!="placedCancellationOwnerPlan"||body.Attributes().Count()!=4||body.Elements().Count()!=4||(int?)body.Attribute("version")!=1||
                (string)body.Attribute("cancel")!=original.Operation.ToString("N")||(string)body.Attribute("source")!=original.Source.ToString("N")||(string)body.Attribute("binding")!=original.Binding||
                body.Element("permission")?.Value!=original.Permission||body.Element("originalBefore")?.Value!=before||body.Element("originalAfter")?.Value!=after)return false;
            var value=new CancellationOwnerProgress{Original=original,Permission=original.Permission,Before=before,Plan=manifest,PlanBytes=Encoding.UTF8.GetBytes(manifest),ReadFloor=floor};
            if(value.PlanBytes.Length>MaximumResultBytes/3)return false;
            var image=original.Before.Copy();var expected=new XElement("slots");
            foreach(bool bag in new[]{true,false})
            {
                var from=bag?original.Before.Bag:original.Before.Belt;var to=bag?original.After.Bag:original.After.Belt;if(from.Length!=to.Length)return false;
                for(int i=0;i<from.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(from[i],to[i]))
                {
                    if(value.Steps.Count>=32)return false;int ordinal=value.Steps.Count;
                    var step=new CancellationOwnerStep{Owner=value,Key=OwnerProgressKey(original.Operation,ordinal),Ordinal=ordinal,Bag=bag,Slot=i,Before=RebirthRemoteResourceClientOriginal.ActorImage(image)};
                    if(bag)image.Bag[i]=to[i].Clone();else image.Belt[i]=to[i].Clone();step.After=RebirthRemoteResourceClientOriginal.ActorImage(image);
                    expected.Add(new XElement("slot",new XAttribute("ordinal",ordinal),new XAttribute("key",step.Key.ToString("N")),new XAttribute("bag",bag),new XAttribute("index",i),new XElement("beforeActor",step.Before),new XElement("afterActor",step.After)));
                    step.Command=Encoding.UTF8.GetBytes(new XElement("placedCancellationOwnerSlot",new XAttribute("version",1),new XAttribute("cancel",original.Operation.ToString("N")),new XAttribute("key",step.Key.ToString("N")),new XAttribute("ordinal",ordinal),new XElement("completePlan",manifest),new XElement("beforeActor",step.Before),new XElement("afterActor",step.After)).ToString(SaveOptions.DisableFormatting));
                    value.Steps.Add(step);
                }
            }
            if(!XNode.DeepEquals(expected,body.Element("slots"))||RebirthRemoteResourceClientOriginal.ActorImage(image)!=after)return false;
            PlayerDataFile latest;
            if(!ReadRestartCheckpoint(value,null,out latest))return false;
            bool prepared=false;prefix=before;
            foreach(var step in value.Steps)
            {
                var record=OwnerProgressJournal(value,step.Key,"OwnerSlot",step.Command.Length).Read(step.Key);
                if(OwnerProgressExact(value,record,step.Key,step.Command,RebirthCamperTransferJournal.Phase.Prepared)){prepared=true;continue;}
                if(prepared||!ReadSavedRestartOwnerStep(value,step,record,out latest))return false;
                value.Completed++;prefix=step.After;
            }
            // Exact native financial spans from the actual join must be the
            // saved prefix. A lagging host upload is retained, never replaced.
            if(latest==null||joining.File.id!=latest.id||joining.File.deaths!=latest.deaths||joining.File.bDead!=latest.bDead||
                !joining.File.bagData.ToArray().SequenceEqual(latest.bagData.ToArray())||!joining.File.inventoryData.ToArray().SequenceEqual(latest.inventoryData.ToArray())||
                !joining.File.buffData.ToArray().SequenceEqual(latest.buffData.ToArray())||
                !RebirthRemoteResourceClientOriginal.ReadActor(prefix).Matches(RebirthCamperEnergyActorImage.Capture(original.Actor)))return false;
            value.BaselineSaved=true;progress=value;return true;
        }
        catch{return false;}
    }
    // The native checkpoint is published only after the original setter's
    // actual return and exact complete native player-file readback. A crash
    // between that publication and the slot metadata transition needs no new
    // inventory action. Missing/uncertain checkpoints remain non-replayable.
    private static bool ReadSavedRestartOwnerStep(CancellationOwnerProgress progress,CancellationOwnerStep step,
        RebirthCamperTransferJournal.Record record,out PlayerDataFile saved)
    {
        saved=null;
        bool applied=OwnerProgressExact(progress,record,step.Key,step.Command,RebirthCamperTransferJournal.Phase.Applied);
        if(!applied&&!OwnerProgressExact(progress,record,step.Key,step.Command,RebirthCamperTransferJournal.Phase.Uncertain)||
            !ReadRestartCheckpoint(progress,step,out saved))return false;
        if(applied)return true;
        var journal=OwnerProgressJournal(progress,step.Key,"OwnerSlot",step.Command.Length);
        var current=journal.Read(step.Key);
        if(OwnerProgressExact(progress,current,step.Key,step.Command,RebirthCamperTransferJournal.Phase.Applied))return true;
        if(!OwnerProgressExact(progress,current,step.Key,step.Command,RebirthCamperTransferJournal.Phase.Uncertain))return false;
        current=journal.Advance(step.Key,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
        return OwnerProgressExact(progress,current,step.Key,step.Command,RebirthCamperTransferJournal.Phase.Applied);
    }
    private static bool ReadRestartCheckpoint(CancellationOwnerProgress progress,CancellationOwnerStep step,out PlayerDataFile saved)
    {
        saved=null;int ordinal=step?.Ordinal??-1;var key=OwnerProgressKey(progress.Original.Operation,ordinal);
        var record=OwnerProgressJournal(progress,key,"OwnerCheckpoint",MaximumResultBytes).Read(key);
        if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Operation!=key||record.Vehicle!=progress.Original.Source||record.OwnerCreation!=progress.Original.Original.Creation||record.Revision!=0||!record.Before.SequenceEqual(record.After))return false;
        if(!progress.Original.Memory.Reserve(Math.Max(progress.Original.Memory.ReservedBytes,checked(progress.ReadFloor+96L*record.Before.Length+128L*1024*1024))))return false;
        var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),MaximumResultBytes);
        string actor=step?.After??progress.Before;
        if(body.Name!="placedCancellationOwnerCheckpoint"||body.Attributes().Count()!=4||body.Elements().Count()!=3||(int?)body.Attribute("version")!=1||
            (string)body.Attribute("cancel")!=progress.Original.Operation.ToString("N")||(int?)body.Attribute("ordinal")!=ordinal||(bool?)body.Attribute("setterReturned")!=(step!=null)||
            body.Element("completePlan")?.Value!=progress.Plan||body.Element("actor")?.Value!=actor)return false;
        byte[] bytes=Convert.FromBase64String(body.Element("nativeOwner").Value);if(bytes.Length>16*1024*1024)return false;
        var data=new PlayerDataFile();RebirthCamperEnergyActorImage image;
        using(var capture=new RebirthCamperEnergySavedOwner.Capture())
        using(var stream=new MemoryStream(bytes,false))
        using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);if(reader.ReadChar()!='t'||reader.ReadChar()!='t'||reader.ReadChar()!='p'||reader.ReadChar()!='\0'||reader.ReadByte()!=PlayerDataFile.cFileVersion)return false;
            data.Read(reader,PlayerDataFile.cFileVersion,global::StreamModeRead.Persistency);
            if(stream.Position!=stream.Length||capture.Failed||data.dragAndDropItem==null||!capture.Items.TryGetValue(data.dragAndDropItem,out var cursor))return false;
            var bag=RebirthGearPlayerDataInventory.ReadSlots(data,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(data,false);if(bag==null||belt==null)return false;
            using(var gridStream=new MemoryStream(data.bagData.ToArray(),false))using(var gridReader=MemoryPools.poolBinaryReader.AllocSync(true))
            {gridReader.SetBaseStream(gridStream);if(gridReader.ReadByte()!=2)return false;var grid=ItemStackGrid.Read(gridReader,global::StreamModeRead.Persistency);
                if(grid==null||grid.Length!=bag.Length||gridStream.Position!=gridStream.Length)return false;
                image=new RebirthCamperEnergyActorImage{Bag=bag,Belt=belt,Cursor=cursor,BagLocks=RebirthCamperEnergyItems.Locks(grid.SlotLocks)};}
        }
        if(data.id!=progress.Original.NativeOwnerId||data.deaths!=progress.Original.Original.Deaths||data.bDead||!RebirthRemoteResourceClientOriginal.ReadActor(actor).Matches(image)||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,CancellationReceipt(progress.Original.Operation),out float phase,out bool present)||!present||phase!=1f)return false;
        saved=data;return true;
    }
}
