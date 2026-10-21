using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    private static bool AbsentArtifacts(string name,Guid operation)
    {
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name),prefix=operation.ToString("N");
        if(!Directory.Exists(directory))return true;if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateFileSystemEntries(directory,prefix+"*").Any())return false;
        foreach(string child in new[]{"completed","failedCandidates"})
        {string path=Path.Combine(directory,child);if(Directory.Exists(path)&&((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateFileSystemEntries(path,prefix+"*").Any()))return false;}
        return true;
    }
    internal static bool HasNoFetchedClientEffects(RebirthRemoteResourceClientOriginal.Intent original)
    {
        try{return original!=null&&AbsentArtifacts("FetchedStationNativePayloads",original.Operation);}
        catch{return false;}
    }
    internal static bool IsFetchedMetadataOnly(RebirthRemoteResourceClientOriginal.Intent original,Guid source,string world,string owner)
    {
        try
        {
            if(!IsCandidate(original)||!AbsentArtifacts("FetchedStationOriginalPlans",source)||!AbsentArtifacts("NativeActorPayments",original.Operation)||
                !AbsentArtifacts("FetchedStationNoUses",source)||!AbsentArtifacts("RemoteStationNativePaymentWitnesses",original.Operation)||
                !AbsentArtifacts("RemoteStationNativePaymentWitnesses/preflight",original.Operation)||!AbsentArtifacts("RemoteStationNativePaymentWitnesses/originals",original.Operation))return false;
            if(AbsentArtifacts("FetchedStationSourceAdmissions",source))return true;
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","FetchedStationSourceAdmissions"),prefix=source.ToString("N")+".";
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateFileSystemEntries(directory,source.ToString("N")+"*").Any(path=>
                (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||!new[]{prefix+"0.rcj",prefix+"1.rcj",prefix+"2.rcj"}.Contains(Path.GetFileName(path))))return false;
            var image=new XElement("resourceOutcome",new XAttribute("world",world),new XAttribute("owner",owner),new XAttribute("creation",original.Creation),
                new XAttribute("deaths",original.Deaths),new XAttribute("kind",(byte)original.Kind));
            return TryReadAdmission(new RebirthRemoteResourceDurableJournal.Record(source,0,image),out var retained)&&retained.Digest==original.Digest;
        }
        catch{return false;}
    }
    private static bool NoSourceEffects(RebirthRemoteResourceDurableJournal.Record root)=>root!=null&&root.Copy().Element("ownerAction")==null&&
        RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Copy().Element("removed").Value)).Length==0&&
        root.Copy().Element("sources").Elements("source").All(source=>(int)source.Attribute("attempted")==0)&&RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root);
    internal static bool RefuseUnpaid(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!Scope(actor,original)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string world))return false;
            Guid source=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
            if(!RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,source,out var root,out bool unknown)||unknown)return false;
            if(root==null)return RebirthRemoteResourcePreAdmissionRefusal.TryRefuse(actor,original,out _);
            if(root.Phase>=2)return root.Phase==3&&(int)root.Copy().Attribute("outcome")==2&&NoSourceEffects(root);
            if(!NoSourceEffects(root))return false;
            if(!AbsentArtifacts("NativeActorPayments",original.Operation))
            {
                if(!TryReadPlan(root,out var plan)||!CurrentStationOwner(actor,plan,out var station,out _)||!HasOnlyNeverEnteredNativeFiles(plan.Job,false))return false;
                var native=new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativeActorPayments")).Read(plan.Job);
                if(native?.State!=RebirthCamperTransferJournal.Phase.Prepared||!RebirthCamperStationAddress.TryParse(plan.Address,out var address)||
                    !RetainedFrameMatchesPlan(native.Before,plan,false)||!RetainedFrameMatchesPlan(native.After,plan,true)||
                    !RebirthCamperStationNativeActorPayment.MatchesFetchedPreparedForRefusal(actor,station,plan.Job,native.Before,native.After))return false;
            }
            else if(!AbsentArtifacts("FetchedStationOriginalPlans",source)&&!TryReadPlan(root,out _))return false;
            return RebirthRemoteResourceDurableJournal.TryAdvance(root,body=>{body.SetAttributeValue("phase",3);body.SetAttributeValue("outcome",2);body.Element("reason").Value="Original station request cancelled before source effects.";},()=>Scope(actor,original),out _);
        }
        catch{return false;}
    }
    internal static bool CanRejectNativeUnpaid(EntityPlayer actor,RebirthCamperStationState station,Guid job,byte[] before,byte[] after)
    {
        try
        {
            if(!HasOnlyNeverEnteredNativeFiles(job,false)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return false;
            var frame=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(after),8*1024*1024);
            var installed=RebirthCamperModuleRuntime.For(station.BoundVehicle).Stations.Find(station.InstallationId)?.InstalledItem;if(installed==null)return false;
            var model=RebirthCamperStationSnapshot.Restore(station.InstallationId,installed,Convert.FromBase64String(frame.Element("stationImage").Value));
            var recipe=model.Queue[(int)frame.Attribute("queueSlot")]?.Recipe;
            var marker=recipe?.ingredients?.FirstOrDefault(item=>item?.itemValue?.HasMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey)==true)?.itemValue;
            if(marker==null||!marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey,out string text)||!Guid.TryParseExact(text,"N",out var source)||
                !RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,source,out var root,out bool unknown)||unknown||root==null||
                (root.Phase!=3&&root.Phase!=4)||(int)root.Copy().Attribute("outcome")!=2||!NoSourceEffects(root)||
                !TryReadAdmission(root,out var original)||!Scope(actor,original)||!TryReadPlan(root,out var plan)||plan.Job!=job||
                !CurrentStationOwner(actor,plan,out var actual,out _)||!ReferenceEquals(actual,station)||!RebirthCamperStationAddress.TryParse(plan.Address,out var address))return false;
            // No owner setter is possible after this durable source refusal.
            // Reparse the ORIGINAL frames, rather than recomputing old belt
            // locks from legitimate later owner changes after terminal ACK.
            return RetainedFrameMatchesPlan(before,plan,false)&&RetainedFrameMatchesPlan(after,plan,true)&&
                RebirthCamperStationNativeActorPayment.MatchesCurrentFetchedPrepared(actor,station,job,before,after);
        }
        catch{return false;}
    }
}
