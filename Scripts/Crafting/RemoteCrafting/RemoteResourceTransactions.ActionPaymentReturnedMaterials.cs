using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    private static bool AdoptReturnedActionSavedMaterials(ActionPayment payment)
    {
        var target=payment.RestartTargetSource;
        if(!payment.ReturnedPublicationNeedsRestore||!payment.PublicationReturned||!payment.Events.ContainsKey("publication-returned")||target?.Current()!=true)return false;
        string servicePath=Path.Combine(payment.Directory,"service-after.native");
        if(!payment.ServiceAfterSaved&&File.Exists(servicePath))
        {
            if(!RebirthInfrastructureWorkService.ValidatePaidActionArtifact(payment,servicePath))return false;
            using(var material=RememberActionMaterial("service",servicePath,24*1024*1024))
                if(!FinishActionMaterial(payment,material,"service","service-after-saved"))return false;
            payment.ServiceAfterSaved=true;
        }
        if(!payment.ServiceAfterSaved)return true; // The ordinary saved-result path still owns this boundary.
        if(!payment.TargetAfterSaved&&target.LoadedAfterPublication)
        {
            string path=Path.Combine(payment.Directory,"target-after-saved.native.xml");
            using(var material=RememberActionMaterial("after",path,48*1024*1024))
            {
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8*material.Bytes.LongLength+65536),out var scratch))return false;
                using(scratch)
                {
                    var image=ReadActionBoundedXml(material.Bytes,48*1024*1024);var markers=image.Element("markers")?.Elements("marker").ToArray();
                    if(image.Name!="nativeActionSource"||(int?)image.Attribute("version")!=1||(long?)image.Attribute("offset")!=target.Offset||markers?.Length!=1||
                        (string)markers[0].Attribute("key")!=target.MarkerKey||!ActionNativeBase64(image.Element("snapshot"),RebirthStationSnapshotEvidence.MaximumSnapshotBytes).SequenceEqual(target.Snapshot)||
                        !ActionNativeBase64(image.Element("tile"),4*1024*1024).SequenceEqual(target.Tile)||
                        !ActionNativeBase64(image.Element("payload"),RebirthStationRegionPayload.MaximumPayloadBytes).SequenceEqual(target.Payload)||
                        !ActionNativeBase64(markers[0].Element("data"),32768).SequenceEqual(target.Marker)||!target.Current())return false;
                }
                if(!FinishActionMaterial(payment,material,"after","target-after-saved"))return false;
            }
            payment.TargetAfterSaved=true;
        }
        return true;
    }
}
