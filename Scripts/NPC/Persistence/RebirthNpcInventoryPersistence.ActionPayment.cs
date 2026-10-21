using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

public static partial class RebirthNpcInventoryPersistenceStore
{
    internal static string CaptureActionPaymentSaved(RemoteResourceTransactions.ActionPayment payment,RemoteResourceTransactions.ActionLeg leg,bool after,bool returned)
    {
        if(!RemoteResourceTransactions.OwnsActionLeg(payment,leg)||leg.Npc==null||leg.Npc.IsDogStorage)return null;
        lock(Sync)try
        {
            if(!BoundCurrent||dirty)return null;var file=new FileInfo(boundPath);if(!file.Exists||file.Length<1||file.Length>8*1024*1024||(file.Attributes&FileAttributes.ReparsePoint)!=0||!CanRetireBacking)return null;
            var expected=RemoteResourceTransactions.ActionExpectedNpc(leg,after,returned);if(expected==null)return null;
            var actual=RebirthNpcInventoryTransactionService.GetSnapshot(expected.NpcId);
            if(!RemoteResourceTransactions.ActionSameNpc(actual,expected))return null;
            using var reader=XmlReader.Create(boundPath,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});var document=new XmlDocument{XmlResolver=null};document.Load(reader);
            if(!ValidateDocument(document))return null;var rows=document.DocumentElement.SelectNodes("inventory").Cast<XmlElement>().Where(row=>row.GetAttribute("stableId")==expected.NpcId.ToString()).ToArray();if(rows.Length!=1)return null;
            var saved=Read(rows[0]);Guid mutation=returned?leg.ReturnMutation:after?leg.DebitMutation:Guid.Empty;
            if(saved==null||saved.Revision!=expected.Revision||mutation!=Guid.Empty&&!saved.ReplayJournal.Contains(mutation)||saved.Quantities.Count!=expected.Quantities.Count||saved.Reservations.Count!=expected.Reservations.Count||
                saved.Quantities.Any(pair=>!expected.Quantities.TryGetValue(pair.Key,out int count)||count!=pair.Value)||saved.Reservations.Any(pair=>!expected.Reservations.TryGetValue(pair.Key,out int count)||count!=pair.Value)||!XNode.DeepEquals(saved.NativeStacks?.Write(),expected.NativeStacks?.Write()))return null;
            string full=document.OuterXml;return full.Length<=8*1024*1024&&RemoteResourceTransactions.OwnsActionLeg(payment,leg)?full:null;
        }catch{return null;}
    }
}

public static partial class RebirthNpcAggregatePersistenceStore
{
    internal static string CaptureActionPaymentDogSaved(RemoteResourceTransactions.ActionPayment payment,RemoteResourceTransactions.ActionLeg leg,bool after,bool returned)
    {
        if(!RemoteResourceTransactions.OwnsActionLeg(payment,leg)||leg.Npc?.IsDogStorage!=true)return null;
        try
        {
            string path=Path.Combine(payment.Save,FileName);var info=new FileInfo(path);if(!info.Exists||info.Length<1||info.Length>8*1024*1024||(info.Attributes&FileAttributes.ReparsePoint)!=0)return null;
            if(!TryReadSavedLogisticsDog(leg.Npc.NativeIdentity,out var row)||!RebirthRemoteResourceDogTransfer.Matches(row,after&&!returned?leg.After:leg.Before,leg.Locks,(uint)leg.Revision+(returned?2u:after?1u:0u)))return null;
            if((after||returned)&&!RemoteResourceTransactions.ActionDogReceiptMatches(payment,leg,row,returned))return null;
            using var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});var document=new XmlDocument{XmlResolver=null};document.Load(reader);
            string full=document.OuterXml;return full.Length<=8*1024*1024&&RemoteResourceTransactions.OwnsActionLeg(payment,leg)?full:null;
        }catch{return null;}
    }
}
