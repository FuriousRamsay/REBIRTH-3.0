using System;
using System.Collections.Generic;
using System.Globalization;

internal static partial class RebirthNpcEquipmentProjectionReceipts
{
    internal static bool CanRetireEmptyReceipts=>ThreadManager.IsMainThread()&&receipts.Count==0;
    internal static bool TryRetireEmptyReceiptObservation(){if(RebirthNpcPersistenceCoordinator.HasActiveRecoveryWriterLease||!CanRetireEmptyReceipts)return false;receiptScope=null;return true;}
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {if(!ThreadManager.IsMainThread()||receipts.Count>MaximumReceipts)throw new InvalidOperationException("Equipment receipt retention bound");foreach(var r in receipts.Values){if(r==null)throw new InvalidOperationException("Equipment receipt missing");RebirthNpcSaveQuarantineIds.Add(ids,r.Stable);}}
    private static System.Xml.XmlDocument ReceiptImage()
    {
        if(receipts.Count>MaximumReceipts)throw new InvalidOperationException("Equipment receipt retention bound");
        var doc=RebirthNpcAncillaryOriginalScope.Image("equipmentProjectionReceipts",receiptScope,Array.Empty<long>());var root=doc.DocumentElement;var keys=new List<string>(receipts.Keys);keys.Sort(StringComparer.Ordinal);
        foreach(string key in keys)
        {
            var r=receipts[key];if(r==null||r.Sender==null||r.Manager==null||r.Transport==null||r.Actor==null||r.Runtime==null||r.Stable.IsEmpty||r.Entity<=0||r.Recipient<=0||r.Connection==0||r.Epoch==0||!HashValid(r.Digest)||key!=r.Recipient+"|"+r.Entity||receiptScope==null||r.Generation!=receiptScope.Generation||!ReferenceEquals(r.Actor.world,receiptScope.World)||r.Runtime.StableId!=r.Stable)throw new InvalidOperationException("Original equipment receipt scope corrupt");
            var e=doc.CreateElement("receipt");root.AppendChild(e);e.SetAttribute("key",key);e.SetAttribute("stable",r.Stable.ToString());e.SetAttribute("generation",r.Generation.ToString(CultureInfo.InvariantCulture));e.SetAttribute("revision",r.Revision.ToString(CultureInfo.InvariantCulture));e.SetAttribute("epoch",r.Epoch.ToString(CultureInfo.InvariantCulture));e.SetAttribute("connection",r.Connection.ToString(CultureInfo.InvariantCulture));e.SetAttribute("digest",r.Digest);
        }
        return doc;
    }
    internal static RebirthNpcSaveQuarantineDomain PrepareReceiptQuarantine(RebirthNpcSaveQuarantineScope scope,RebirthNpcRecoveryPrimitiveCaches.Context transition=null)
    {
        if(transition==null)RebirthNpcAncillaryOriginalScope.RequireScope(scope,receiptScope);else transition.RequireOriginal(scope,receiptScope);if(receiptScope==null&&receipts.Count!=0)throw new InvalidOperationException("Original equipment receipts unbound");
        var records=receipts;var original=receiptScope;var observed=new Dictionary<string,Receipt>(receipts,StringComparer.Ordinal);var doc=ReceiptImage();string image=doc.OuterXml;var empty=new Dictionary<string,Receipt>(StringComparer.Ordinal);
        return new RebirthNpcSaveQuarantineDomain("equipment-projection-receipts",doc,
            ()=>{if(!ThreadManager.IsMainThread()||!ReferenceEquals(receipts,records)||!ReferenceEquals(receiptScope,original)||receipts.Count!=observed.Count)return false;foreach(var row in observed)if(!receipts.TryGetValue(row.Key,out var current)||!ReferenceEquals(current,row.Value))return false;return ReceiptImage().OuterXml==image;},
            ()=>{receipts=empty;receiptScope=null;},
            ()=>{receipts=records;receiptScope=original;});
    }
}
