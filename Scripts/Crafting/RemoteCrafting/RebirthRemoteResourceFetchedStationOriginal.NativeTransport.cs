using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    // Only the existing typed native Pending may call this send seam. Parsing
    // identifies its retained source; it never grants owner/world effects.
    internal static bool SendNativeFrame(EntityPlayer actor,Guid effect,byte[] payload,RebirthCamperStationState retainedStation=null)
    {
        try
        {
            if(!ProtocolActive||!RebirthRemoteResourceRepairProtocol.SupportsServerActor(actor)||payload==null||payload.Length>8*1024*1024)return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(payload),8*1024*1024);
            if(body.Name!="camperNativeActorPayment"||body.Attributes().Count()!=6||body.Elements().Count()!=2||(int?)body.Attribute("version")!=1||
                (int?)body.Attribute("actor")!=actor.entityId||!Guid.TryParseExact((string)body.Attribute("original"),"N",out var nativeJob)||
                !RebirthCamperStationAddress.TryParse((string)body.Attribute("address"),out var address))return false;
            bool retired=(bool)body.Attribute("retired");
            bool hasLease=RebirthStationLiveAccess.TryGetMobileLease(actor,address,(string)body.Attribute("creation"),out var station,out _);
            if(!hasLease){if(!retired||retainedStation==null)return false;station=retainedStation;}
            // Retirement binds the actual native operation, independently of
            // the source original or its derived cancellation operation.
            if(effect!=(retired?NativeRetirementOperation(nativeJob):nativeJob))return false;
            var installed=RebirthCamperModuleRuntime.For(station.BoundVehicle).Stations.Find(station.InstallationId)?.InstalledItem;
            if(installed==null)return false;
            Guid source=Guid.Empty;string binding=null;
            foreach(string side in new[]{"after","before"})
            {
                var frame=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(Convert.FromBase64String(body.Element(side).Value)),8*1024*1024);
                var model=RebirthCamperStationSnapshot.Restore(station.InstallationId,installed,Convert.FromBase64String(frame.Element("stationImage").Value));
                int slot=(int)frame.Attribute("queueSlot");if(slot<0||slot>=model.Queue.Length)return false;
                var recipe=model.Queue[slot]?.Recipe;if(!IsCancellationCandidate(recipe))continue;
                var marker=recipe.ingredients.FirstOrDefault(item=>item?.itemValue?.HasMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey)==true)?.itemValue;
                if(marker==null||!marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey,out string text)||!Guid.TryParseExact(text,"N",out var found)||
                    !marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.BindingKey,out string foundBinding))return false;
                if(source!=Guid.Empty&&(source!=found||binding!=foundBinding))return false;source=found;binding=foundBinding;
            }
            if(source==Guid.Empty||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,source,out var root,out bool unknown)||unknown||root==null||root.Binding!=binding||
                !TryReadAdmission(root,out var original)||!Scope(actor,original)||!TryReadPlan(root,out var plan)||
                plan.Address!=address.ToString()||nativeJob!=plan.Job&&nativeJob!=NativeCancellationOperation(plan.Job)||
                root.Phase<2||(int)root.Copy().Attribute("outcome")!=1||!MatchesNativePermission(root,plan))return false;
            if(nativeJob!=plan.Job&&(root.Phase!=4||(string)root.Copy().Attribute("disposition")!="used"))return false;
            string usedProof="";
            if(root.Phase==4)
            {
                if((string)root.Copy().Attribute("disposition")!="used"||!TryObservePublication(actor.world,root,out usedProof)||(string)root.Copy().Attribute("ownerProof")!=usedProof)return false;
            }
            if(!hasLease&&(!CanRebindNativeServer(actor,station,nativeJob,Convert.FromBase64String(body.Element("before").Value),Convert.FromBase64String(body.Element("after").Value),out bool used)||!used))return false;
            var message=new XElement("fetchedStationNativeFrame",new XAttribute("version",1),new XAttribute("original",original.Operation.ToString("N")),
                new XAttribute("digest",original.Digest),new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),
                new XAttribute("nativeOperation",effect.ToString("N")),new XAttribute("retired",retired),new XAttribute("usedProof",usedProof),new XElement("payload",Convert.ToBase64String(payload)));
            string textBody=message.ToString(SaveOptions.DisableFormatting);if(Encoding.UTF8.GetByteCount(textBody)>12*1024*1024)return false;
            NetPackageRebirthRemoteRepairResult.Send(actor.entityId,original,textBody);return true;
        }
        catch{return false;}
    }
    private static Guid NativeRetirementOperation(Guid operation)
    {
        // Kept separate from native cancellation: exact installed native name.
        using(var hash=System.Security.Cryptography.SHA256.Create())
        {var bytes=hash.ComputeHash(Encoding.UTF8.GetBytes(operation.ToString("N")+":retired"));var id=new byte[16];Array.Copy(bytes,id,16);return new Guid(id);}
    }
    internal static void NativeFrameResult(EntityPlayerLocal player,Guid job,string text)
    {
        try
        {
            if(!ProtocolActive||!RebirthRemoteResourceClientOriginal.TryRead(job,out var original)||!IsCandidate(original)||!RebirthRemoteResourceClientOriginal.Current(player,original))return;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,12*1024*1024);
            if(body.Name!="fetchedStationNativeFrame"||body.Attributes().Count()!=8||body.Elements().Count()!=1||body.Element("payload")==null||
                (int?)body.Attribute("version")!=1||(string)body.Attribute("original")!=job.ToString("N")||(string)body.Attribute("digest")!=original.Digest||
                !RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out var source,out var binding,out _)||
                (string)body.Attribute("source")!=source.ToString("N")||(string)body.Attribute("binding")!=binding||
                !Guid.TryParseExact((string)body.Attribute("nativeOperation"),"N",out var effect)||body.Element("payload").Value.Length>11184812)return;
            byte[] payload=Convert.FromBase64String(body.Element("payload").Value);
            if(payload.Length>8*1024*1024||Convert.ToBase64String(payload)!=body.Element("payload").Value)return;
            var native=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(payload),8*1024*1024);
            string usedProof=(string)body.Attribute("usedProof");
            if(!string.IsNullOrEmpty(usedProof)&&!SaveHostUsed(player,original,source,binding,usedProof,()=>RebirthRemoteResourceClientOriginal.Current(player,original)))return;
            if(!Guid.TryParseExact((string)native.Attribute("original"),"N",out var nativeJob))return;
            if(nativeJob!=job&&(nativeJob!=NativeCancellationOperation(job)||!RebirthCamperStationNativeActorPayment.TryRetainFetchedCancellation(player,job,effect,payload)))return;
            if((bool?)native.Attribute("retired")!=(bool?)body.Attribute("retired")||
                !RebirthCamperStationNativeActorPayment.TryApplyRemote(player,effect,payload))return;
            GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
        }
        catch{/* Original native journal and owner receipt remain pending. */}
    }
}
