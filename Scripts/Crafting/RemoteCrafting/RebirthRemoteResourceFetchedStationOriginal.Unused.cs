using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    private static RebirthCamperTransferJournal FetchedNoUses()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","FetchedStationNoUses"));
    private static RebirthRemoteResourceClientOriginalJournal FetchedClientRejections()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","FetchedStationClientRejections"));
    internal static bool HasOnlyNeverEnteredNativeFiles(Guid job,bool allowRejected)
    {
        try
        {
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativeActorPayments"),prefix=job.ToString("N")+".";
            return Directory.Exists(directory)&&(File.GetAttributes(directory)&FileAttributes.ReparsePoint)==0&&
                Directory.EnumerateFileSystemEntries(directory,job.ToString("N")+"*").All(path=>(File.GetAttributes(path)&FileAttributes.ReparsePoint)==0&&
                    (Path.GetFileName(path)==prefix+"0.rcj"||allowRejected&&Path.GetFileName(path)==prefix+"3.rcj"));
        }
        catch{return false;}
    }
    private static bool NoUseFilesKnown(Guid source)
    {
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","FetchedStationNoUses"),prefix=source.ToString("N")+".";
        return !Directory.Exists(directory)||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)==0&&
            Directory.EnumerateFileSystemEntries(directory,source.ToString("N")+"*").All(path=>(File.GetAttributes(path)&FileAttributes.ReparsePoint)==0&&
                new[]{prefix+"0.rcj",prefix+"1.rcj",prefix+"2.rcj"}.Contains(Path.GetFileName(path)));
    }
    private static bool NativeNeverEntered(EntityPlayer actor,RebirthCamperStationState station,RebirthRemoteResourceFetchedStationPlan.Plan plan,out RebirthCamperTransferJournal.Record record)
    {
        record=null;
        try
        {
            if(!HasOnlyNeverEnteredNativeFiles(plan.Job,true))return false;
            record=new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativeActorPayments")).Read(plan.Job);
            return record!=null&&(record.State==RebirthCamperTransferJournal.Phase.Prepared||record.State==RebirthCamperTransferJournal.Phase.Rejected&&
                    NoUseFilesKnown(plan.Source)&&FetchedNoUses().Read(plan.Source)?.State==RebirthCamperTransferJournal.Phase.Applied)&&
                record.OwnerCreation==plan.Creation&&RebirthCamperStationAddress.TryParse(plan.Address,out var address)&&record.Vehicle==address.Vehicle&&
                MatchesNativeFrames(actor,station,address,plan,record.Before,record.After)&&
                RebirthRemoteResourceStationPaymentWitness.TryReadSavedBefore(actor.world,plan.Job,plan.Address,plan.OwnerKey,plan.Creation,record.Before,record.After,out _);
        }
        catch{return false;}
    }
    internal static bool CanRejectNativePrepared(EntityPlayer actor,RebirthCamperStationState station,Guid job,Guid source,string binding,byte[] before,byte[] after)
    {
        try
        {
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,source,out var root,out bool unknown)||unknown||root?.Phase!=2||root.Binding!=binding||
                (int?)root.Copy().Attribute("ownerRejecting")!=1||root.Copy().Element("ownerAction")!=null||!RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root)||
                !TryReadPlan(root,out var plan)||plan.Job!=job||!CurrentStationOwner(actor,plan,out var actual,out _)||!ReferenceEquals(actual,station)||
                !NativeNeverEntered(actor,station,plan,out var native)||!native.Before.SequenceEqual(before)||!native.After.SequenceEqual(after)||
                !RebirthCamperEnergySavedOwner.TryRead(owner,out var image,out var saved)||saved.id!=actor.entityId||saved.deaths!=plan.Deaths||!plan.CopyBeforeActor().Matches(image)||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(job),out float phase,out bool present)||!present||phase!=5f)return false;
            return true;
        }
        catch{return false;}
    }
    internal static bool RejectUnused(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,bool observe)
    {
        try
        {
            if(!Scope(actor,original)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string world))return false;
            Guid source=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
            if(!RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,source,out var root,out bool unknown)||unknown||root?.Phase!=2||(int)root.Copy().Attribute("outcome")!=1||
                !TryReadPlan(root,out var plan)||root.Copy().Element("ownerAction")!=null||!RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root))return false;
            string proof=RebirthRemoteResourceCraftSettlement.UnusedProof(original,root.Binding);
            if((int?)root.Copy().Attribute("ownerRejecting")==1&&ValidateFetchedNoUse(root,original,plan))
                return (string)root.Copy().Attribute("ownerNoUseProof")==proof||RebirthRemoteResourceDurableJournal.TryAdvance(root,body=>body.SetAttributeValue("ownerNoUseProof",proof),()=>Scope(actor,original),out _);
            if(!CurrentStationOwner(actor,plan,out var station,out _)||!NativeNeverEntered(actor,station,plan,out var native))return false;
            if((int?)root.Copy().Attribute("ownerRejecting")!=1)
                return !observe&&RebirthRemoteResourceDurableJournal.TryAdvance(root,body=>body.SetAttributeValue("ownerRejecting",1),()=>Scope(actor,original),out _);
            if(!observe)return true;
            if(!RebirthCamperEnergySavedOwner.TryRead(owner,out var image,out var saved)||saved.id!=actor.entityId||saved.deaths!=plan.Deaths||!plan.CopyBeforeActor().Matches(image)||
                saved.buffData==null||saved.buffData.Length>1024*1024||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(plan.Job),out float phase,out bool present)||!present||phase!=5f)return false;
            var evidence=new XElement("fetchedStationNoUse",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("source",source.ToString("N")),
                new XAttribute("binding",root.Binding),new XAttribute("plan",RebirthRemoteResourceDurableJournal.Digest(RebirthRemoteResourceFetchedStationPlan.Image(plan).ToString(SaveOptions.DisableFormatting))),
                new XAttribute("before",RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(native.Before))),new XAttribute("after",RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(native.After))),
                new XAttribute("actor",saved.id),new XAttribute("deaths",saved.deaths),new XElement("owner",RebirthRemoteResourceClientOriginal.ActorImage(image)),new XElement("buffs",Convert.ToBase64String(saved.buffData.ToArray())));
            if(!NoUseFilesKnown(source))return false;
            var journal=FetchedNoUses();var prior=journal.Read(source);
            byte[] bytes=Encoding.UTF8.GetBytes(evidence.ToString(SaveOptions.DisableFormatting));
            if(bytes.Length>24*1024*1024)return false;
            // The first positive native observation is immutable. Later valid
            // upload variations cannot replace its actual saved receipt.
            if(prior==null)prior=journal.Prepare(source,plan.Job,plan.Creation,0,bytes,bytes);
            if(prior.State==RebirthCamperTransferJournal.Phase.Prepared)prior=journal.Advance(source,prior.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(prior.State==RebirthCamperTransferJournal.Phase.Uncertain)prior=journal.Advance(source,prior.State,RebirthCamperTransferJournal.Phase.Applied);
            // Retain the actual saved native5 observation BEFORE retiring the
            // native Pending. A crash after Rejected cannot lose its proof or
            // require an obsolete station image after later legitimate work.
            return prior.State==RebirthCamperTransferJournal.Phase.Applied&&ValidateFetchedNoUse(root,original,plan,false)&&
                RebirthCamperStationNativeActorPayment.RejectFetchedPrepared(actor,station,plan.Job,source,root.Binding)&&ValidateFetchedNoUse(root,original,plan)&&
                ((string)root.Copy().Attribute("ownerNoUseProof")==proof||RebirthRemoteResourceDurableJournal.TryAdvance(root,body=>body.SetAttributeValue("ownerNoUseProof",proof),()=>Scope(actor,original),out _));
        }
        catch{return false;}
    }
    private static bool ValidateFetchedNoUse(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceFetchedStationPlan.Plan plan,bool requireRejected=true)
    {
        try
        {
            if(!NoUseFilesKnown(root.Operation)||!HasOnlyNeverEnteredNativeFiles(plan.Job,true))return false;
            var record=FetchedNoUses().Read(root.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=plan.Job||record.OwnerCreation!=plan.Creation||record.Revision!=0||
                record.Before.Length>24*1024*1024||!record.Before.SequenceEqual(record.After))return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),24*1024*1024);
            if(body.Name!="fetchedStationNoUse"||body.Attributes().Count()!=9||body.Elements().Count()!=2||body.Element("owner")==null||body.Element("buffs")==null||
                (int?)body.Attribute("version")!=1||(string)body.Attribute("original")!=original.Digest||(string)body.Attribute("source")!=root.Operation.ToString("N")||
                (string)body.Attribute("binding")!=root.Binding||(string)body.Attribute("plan")!=RebirthRemoteResourceDurableJournal.Digest(RebirthRemoteResourceFetchedStationPlan.Image(plan).ToString(SaveOptions.DisableFormatting))||
                (int?)body.Attribute("deaths")!=plan.Deaths||(int?)body.Attribute("actor") is not int actorId||actorId<0||
                !plan.CopyBeforeActor().Matches(RebirthRemoteResourceClientOriginal.ReadActor(body.Element("owner").Value)))return false;
            var native=new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativeActorPayments")).Read(plan.Job);
            if(native==null||(requireRejected?native.State!=RebirthCamperTransferJournal.Phase.Rejected:
                native.State!=RebirthCamperTransferJournal.Phase.Prepared&&native.State!=RebirthCamperTransferJournal.Phase.Rejected)||native.OwnerCreation!=plan.Creation||
                !RebirthCamperStationAddress.TryParse(plan.Address,out var address)||native.Vehicle!=address.Vehicle||
                (string)body.Attribute("before")!=RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(native.Before))||
                (string)body.Attribute("after")!=RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(native.After))||
                !RebirthRemoteResourceStationPaymentWitness.TryReadSavedBefore(GameManager.Instance?.World,plan.Job,plan.Address,plan.OwnerKey,plan.Creation,native.Before,native.After,out _,out var definition)||
                definition!=plan.InstalledDefinition||!RetainedFrameMatchesPlan(native.Before,plan,false)||!RetainedFrameMatchesPlan(native.After,plan,true))return false;
            byte[] buffs=Convert.FromBase64String(body.Element("buffs").Value);
            if(buffs.Length>1024*1024||Convert.ToBase64String(buffs)!=body.Element("buffs").Value)return false;
            var saved=new PlayerDataFile{id=actorId,deaths=plan.Deaths,buffData=new MemoryStream(buffs,false)};
            return RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(plan.Job),out float phase,out bool present)&&present&&phase==5f;
        }
        catch{return false;}
    }
    private static bool RetainedFrameMatchesPlan(byte[] bytes,RebirthRemoteResourceFetchedStationPlan.Plan plan,bool after)
    {
        var frame=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(bytes),8*1024*1024);
        var actor=after?plan.CopyAfterActor():plan.CopyBeforeActor();
        if(!RebirthCamperStationAddress.TryParse(plan.Address,out var address)||(int?)frame.Attribute("version")!=2||
            (string)frame.Attribute("station")!=address.Installation.ToString("N")||(string)frame.Attribute("owner")!=plan.CanonicalOwner||
            (string)frame.Attribute("ownerKey")!=plan.OwnerKey||(string)frame.Attribute("world")!=plan.World||(int?)frame.Attribute("deaths")!=plan.Deaths||
            (int?)frame.Attribute("queueSlot")!=plan.QueueSlot||(int?)frame.Attribute("ownedBelt")!=plan.OwnedBelt||(bool?)frame.Attribute("remote")!=true||
            !Convert.FromBase64String(frame.Element("stationImage").Value).SequenceEqual(after?plan.CopyAfterStation():plan.CopyBeforeStation()))return false;
        bool Area(string name,ItemStack[] items)
        {
            var rows=frame.Element(name).Elements("slot").ToArray();
            return rows.Length==items.Length&&rows.Select((row,index)=>(int?)row.Attribute("count")==items[index].count&&
                (string)row.Attribute("item")==RebirthNativeItemCodec.Encode(items[index].itemValue)).All(match=>match);
        }
        var locks=frame.Element("bagLocks");bool nullLocks=(bool)locks.Attribute("null");
        return Area("bag",actor.Bag)&&Area("belt",actor.Belt)&&(actor.BagLocks==null?nullLocks:
            !nullLocks&&locks.Value==new string(actor.BagLocks.Select(value=>value?'1':'0').ToArray()));
    }
    internal static bool ValidateFetchedNoUseRoot(XElement image)
    {
        try
        {
            if(!Guid.TryParseExact((string)image.Attribute("operation"),"N",out var source))return false;
            var root=new RebirthRemoteResourceDurableJournal.Record(source,0,image);
            return (int?)image.Attribute("ownerRejecting")==1&&(int?)image.Attribute("outcome")==1&&
                (root.Phase==2||root.Phase==4&&(string)image.Attribute("disposition")=="refunded")&&image.Element("ownerAction")==null&&
                TryReadAdmission(root,out var original)&&IsCandidate(original)&&TryReadPlan(root,out var plan)&&
                (string)image.Attribute("ownerNoUseProof")==RebirthRemoteResourceCraftSettlement.UnusedProof(original,root.Binding)&&ValidateFetchedNoUse(root,original,plan);
        }
        catch{return false;}
    }
    internal static bool SaveClientRejection(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,Func<bool> authenticated)
    {
        try
        {
            if(authenticated?.Invoke()!=true||!RebirthRemoteResourceClientOriginal.Current(player,original)||!IsCandidate(original)||
                RebirthRemoteResourceOwnerAction.HasFetchedStationHandoff(original.Operation)||RebirthCamperStationNativeActorPayment.Blocks(player.bag)||
                !RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value).Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            var body=new XElement("fetchedStationClientRejection",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding));
            byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));var journal=FetchedClientRejections();
            var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            if(record.State!=RebirthCamperTransferJournal.Phase.Applied||authenticated?.Invoke()!=true)return false;
            player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation),5f,true);
            GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
        }
        catch{return false;}
    }
}
