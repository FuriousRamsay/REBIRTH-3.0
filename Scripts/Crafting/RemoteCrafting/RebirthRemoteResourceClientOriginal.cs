using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Durable original owner input, separate from both source payment and actual
// native usage. Presence of this intent never proves that anything was paid.
internal static class RebirthRemoteResourceClientOriginal
{
    internal const int MaximumOriginalBytes=16*1024*1024;
    internal static bool CompleteEndedPlacedNoEffect(RebirthRemoteResourcePlacedStationOriginal.EndedNeverEnteredClient proof)
    {
        try
        {
            if(proof?.Current!=true||Actions().Read(proof.Original.Operation)!=null||Sources().Read(proof.Original.Operation)!=null)return false;
            var original=proof.Original;var journal=Journal();var intent=journal.Read(original.Operation);
            if(intent==null||Decode(intent)?.Digest!=original.Digest)return false;
            var terminal=new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","PlacedStationEndedZeroTerminals"));
            byte[] bytes=Encoding.UTF8.GetBytes(proof.Terminal);var saved=terminal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            if(saved.Vehicle!=original.Operation||saved.OwnerCreation!=original.Creation||saved.Revision!=0||!saved.Before.SequenceEqual(bytes)||!saved.After.SequenceEqual(bytes))return false;
            if(saved.State==RebirthCamperTransferJournal.Phase.Prepared)saved=terminal.Advance(original.Operation,saved.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(saved.State==RebirthCamperTransferJournal.Phase.Uncertain)saved=terminal.Advance(original.Operation,saved.State,RebirthCamperTransferJournal.Phase.Applied);
            if(saved.State!=RebirthCamperTransferJournal.Phase.Applied||!proof.Current||!RebirthRemoteResourceOwnerAction.ReleaseEndedPlacedNoEffect(proof)||
                !RebirthRemoteResourcePlacedStationClientCustody.ReleaseEndedNoPayment(proof))return false;
            if(intent.State==RebirthCamperTransferJournal.Phase.Prepared)intent=journal.Advance(original.Operation,intent.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(intent.State==RebirthCamperTransferJournal.Phase.Uncertain)intent=journal.Advance(original.Operation,intent.State,RebirthCamperTransferJournal.Phase.Applied);
            return intent.State==RebirthCamperTransferJournal.Phase.Applied;
        }
        catch{return false;}
    }
    internal static bool CompleteEndedFetchedNoEffect(EntityPlayerLocal player,Intent original,string authenticatedTerminal)
    {
        try
        {
            if(!RebirthRemoteResourceFetchedStationOriginal.ValidateEndedClientTerminal(player,original,authenticatedTerminal)||
                !RebirthRemoteResourceOwnerAction.CanReleaseEndedFetchedNoEffect(player,original))return false;
            byte[] bytes=Encoding.UTF8.GetBytes(authenticatedTerminal);
            var terminal=new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","FetchedStationEndedZeroTerminals"));
            var saved=terminal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            if(!saved.Before.SequenceEqual(bytes)||!saved.After.SequenceEqual(bytes))return false;
            if(saved.State==RebirthCamperTransferJournal.Phase.Prepared)saved=terminal.Advance(original.Operation,saved.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(saved.State==RebirthCamperTransferJournal.Phase.Uncertain)saved=terminal.Advance(original.Operation,saved.State,RebirthCamperTransferJournal.Phase.Applied);
            if(saved.State!=RebirthCamperTransferJournal.Phase.Applied||!RebirthRemoteResourceFetchedStationOriginal.ValidateEndedClientTerminal(player,original,authenticatedTerminal))return false;
            var journal=Journal();var intent=journal.Read(original.Operation);
            if(intent==null||Decode(intent)?.Digest!=original.Digest)return false;
            // Release only the exact positively terminal metadata before the
            // pending scanner stops seeing this intent. Failed cleanup retries
            // from the durable terminal; it cannot strand a hidden old hold.
            if(!RebirthRemoteResourceOwnerAction.ReleaseEndedFetchedNoEffect(player,original))return false;
            if(intent.State==RebirthCamperTransferJournal.Phase.Prepared)intent=journal.Advance(original.Operation,intent.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(intent.State==RebirthCamperTransferJournal.Phase.Uncertain)intent=journal.Advance(original.Operation,intent.State,RebirthCamperTransferJournal.Phase.Applied);
            return intent.State==RebirthCamperTransferJournal.Phase.Applied;
        }
        catch{return false;}
    }
    internal sealed class Intent
    {
        private readonly XElement image;
        private readonly string digest;
        internal Guid Operation{get;}
        internal ulong Epoch=>(ulong)image.Attribute("epoch");
        internal ulong Request=>(ulong)image.Attribute("request");
        internal RemoteResourceClientOperation Kind=>(RemoteResourceClientOperation)checked((byte)(int)image.Attribute("kind"));
        internal string Owner=>(string)image.Attribute("owner");
        internal string Creation=>(string)image.Attribute("creation");
        internal int Deaths=>(int)image.Attribute("deaths");
        internal string World=>(string)image.Attribute("world");
        internal string Digest=>digest;
        internal XElement Copy()=>new XElement(image);
        internal void WriteDetachedTo(XmlWriter writer)=>image.WriteTo(writer);
        internal Intent(Guid operation,XElement root){Operation=operation;image=new XElement(root);digest=RebirthRemoteResourceDurableJournal.Digest(image.ToString(SaveOptions.DisableFormatting));}
    }
    private static RebirthRemoteResourceClientOriginalJournal Journal()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteResourceClientOriginals"));
    private static RebirthRemoteResourceClientOriginalJournal Terminals()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteResourceClientTerminals"));
    private static RebirthRemoteResourceClientOriginalJournal Actions()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteResourceClientActions"));
    private static RebirthRemoteResourceClientOriginalJournal Sources()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteResourceClientSources"));
    private static RebirthRemoteResourceClientOriginalJournal Rejections()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteResourceClientRejections"));
    private static bool Hex(string value)=>value?.Length==64&&value.All(character=>character>='0'&&character<='9'||character>='a'&&character<='f');
    internal static bool SaveRepairSource(EntityPlayerLocal player,Intent original,string sourceWorld,Guid sourceOperation,string binding,ItemStack[] paid,Func<bool> authenticatedPaidOriginal)
        =>original?.Kind==RemoteResourceClientOperation.ItemRepair&&SavePaidActionSource(player,original,sourceWorld,sourceOperation,binding,paid,authenticatedPaidOriginal);
    internal static bool SaveCraftSource(EntityPlayerLocal player,Intent original,string sourceWorld,Guid sourceOperation,string binding,ItemStack[] paid,Func<bool> authenticatedPaidOriginal)
        =>original?.Kind==RemoteResourceClientOperation.Craft&&SavePaidActionSource(player,original,sourceWorld,sourceOperation,binding,paid,authenticatedPaidOriginal);
    private static bool PaidActionKind(RemoteResourceClientOperation kind)=>kind==RemoteResourceClientOperation.ItemRepair||kind==RemoteResourceClientOperation.Craft||
        kind==RemoteResourceClientOperation.BlockRepair||kind==RemoteResourceClientOperation.BlockUpgrade;
    internal static bool SaveBlockSource(EntityPlayerLocal player,Intent original,string sourceWorld,Guid sourceOperation,string binding,ItemStack[] paid,Func<bool> authenticatedPaidOriginal)
        =>original!=null&&(original.Kind==RemoteResourceClientOperation.BlockRepair||original.Kind==RemoteResourceClientOperation.BlockUpgrade)&&RebirthRemoteResourceBlockAdoption.QueryScope(player,original)&&
            SavePaidActionSource(player,original,sourceWorld,sourceOperation,binding,paid,authenticatedPaidOriginal);
    private static bool SavePaidActionSource(EntityPlayerLocal player,Intent original,string sourceWorld,Guid sourceOperation,string binding,ItemStack[] paid,Func<bool> authenticatedPaidOriginal)
    {
        try
        {
            if(!RebirthRemoteResourceRepairAdoption.QueryScope(player,original)||!PaidActionKind(original.Kind)||!Hex(sourceWorld)||!Hex(binding)||sourceOperation==Guid.Empty||
                paid==null||paid.Length<1||paid.Length>short.MaxValue||paid.Any(item=>item==null||item.IsEmpty()||item.count<1)||
                authenticatedPaidOriginal?.Invoke()!=true||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||
                RebirthRemoteResourceDurableJournal.Operation(sourceWorld,identity.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind)!=sourceOperation)return false;
            byte[] snapshot=RebirthCamperInventorySnapshot.Capture(paid);if(snapshot.Length>6*1024*1024)return false;
            var body=new XElement("fetchRepairSource",new XAttribute("version",1),new XAttribute("original",original.Operation.ToString("N")),new XAttribute("originalDigest",original.Digest),
                new XAttribute("sourceWorld",sourceWorld),new XAttribute("sourceOperation",sourceOperation.ToString("N")),new XAttribute("binding",binding),
                new XElement("paid",Convert.ToBase64String(snapshot)));
            byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));var journal=Sources();var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes)||authenticatedPaidOriginal()!=true)return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied;
        }
        catch{return false;}
    }
    internal static bool TryReadRepairSource(Intent original,out string sourceWorld,out Guid sourceOperation,out string binding,out ItemStack[] paid)
    {sourceWorld=null;sourceOperation=Guid.Empty;binding=null;paid=null;return original?.Kind==RemoteResourceClientOperation.ItemRepair&&TryReadPaidActionSource(original,out sourceWorld,out sourceOperation,out binding,out paid);}
    internal static bool TryReadPaidActionSource(Intent original,out string sourceWorld,out Guid sourceOperation,out string binding,out ItemStack[] paid)
    {
        sourceWorld=null;sourceOperation=Guid.Empty;binding=null;paid=null;
        try
        {
            if(original==null||!PaidActionKind(original.Kind))return false;var record=Sources().Read(original.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Before.Length>9*1024*1024||!record.Before.SequenceEqual(record.After))return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),9*1024*1024);
            if(body.Name!="fetchRepairSource"||body.Attributes().Count()!=6||body.Elements().Count()!=1||body.Element("paid")==null||(int?)body.Attribute("version")!=1||
                (string)body.Attribute("original")!=original.Operation.ToString("N")||(string)body.Attribute("originalDigest")!=original.Digest||
                !Hex((string)body.Attribute("sourceWorld"))||!Hex((string)body.Attribute("binding"))||!Guid.TryParseExact((string)body.Attribute("sourceOperation"),"N",out var operation)||operation==Guid.Empty)return false;
            var values=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(body.Element("paid").Value));
            if(values.Length<1||values.Length>short.MaxValue||values.Any(item=>item==null||item.IsEmpty()||item.count<1))return false;
            sourceWorld=(string)body.Attribute("sourceWorld");sourceOperation=operation;binding=(string)body.Attribute("binding");paid=values;return true;
        }
        catch{sourceWorld=null;sourceOperation=Guid.Empty;binding=null;paid=null;return false;}
    }
    internal static bool PrepareRepairAction(EntityPlayerLocal player,Intent original,string encodedPayment,string queuedInput,int amount,
        RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after,out XElement action)
    {
        action=null;
        try
        {
            if(!Current(player,original)||Rejections().Read(original.Operation)!=null||original.Kind!=RemoteResourceClientOperation.ItemRepair||before==null||after==null||
                !before.Matches(RebirthCamperEnergyActorImage.Capture(player))||amount<1||amount>ushort.MaxValue||
                !RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(encodedPayment,out var payment)||payment.ClientOriginal!=original.Operation||
                payment.World!=original.World||payment.CanonicalOwner!=original.Owner||payment.Creation!=original.Creation||payment.Deaths!=original.Deaths||
                !RebirthNativeItemCodec.TryDecode(queuedInput,out var value)||RebirthNativeItemCodec.Encode(value)!=queuedInput||
                !value.TryGetMetadata(RebirthRemoteResourceOwnerAction.Marker,out string marker)||marker!=payment.SourceOperation.ToString("N")||
                !value.TryGetMetadata(RebirthRemoteResourceOwnerAction.BindingMarker,out string binding)||binding!=payment.Binding)return false;
            var root=new XElement("fetchRepairOwnerOriginal",new XAttribute("version",1),new XAttribute("original",original.Operation.ToString("N")),new XAttribute("originalDigest",original.Digest),
                new XElement("payment",encodedPayment),new XElement("queuedInput",queuedInput),new XElement("amount",amount),new XElement("beforeActor",ActorImage(before)),new XElement("afterActor",ActorImage(after)));
            byte[] bytes=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));if(bytes.Length>24*1024*1024)return false;
            var record=Actions().Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
            return TryReadRepairAction(original,out action);
        }
        catch{action=null;return false;}
    }
    internal static bool TryReadRepairAction(Intent original,out XElement action)
    {
        action=null;
        try
        {
            if(original?.Kind!=RemoteResourceClientOperation.ItemRepair)return false;var record=Actions().Read(original.Operation);
            if(record==null||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Before.Length>24*1024*1024||!record.Before.SequenceEqual(record.After))return false;
            var root=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),24*1024*1024);
            if(root.Name!="fetchRepairOwnerOriginal"||root.Attributes().Count()!=3||root.Elements().Count()!=5||(int?)root.Attribute("version")!=1||
                (string)root.Attribute("original")!=original.Operation.ToString("N")||(string)root.Attribute("originalDigest")!=original.Digest||
                root.Elements().GroupBy(node=>node.Name).Any(group=>group.Count()!=1)||root.Elements().Any(node=>!new[]{"payment","queuedInput","amount","beforeActor","afterActor"}.Contains(node.Name.LocalName)||node.Name.NamespaceName.Length!=0)||
                !RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(root.Element("payment")?.Value,out var payment)||payment.ClientOriginal!=original.Operation||
                payment.World!=original.World||payment.CanonicalOwner!=original.Owner||payment.Creation!=original.Creation||payment.Deaths!=original.Deaths||
                (int?)root.Element("amount") is not int amount||amount<1||amount>ushort.MaxValue||
                !RebirthNativeItemCodec.TryDecode(root.Element("queuedInput")?.Value,out var queued)||RebirthNativeItemCodec.Encode(queued)!=root.Element("queuedInput").Value||
                !queued.TryGetMetadata(RebirthRemoteResourceOwnerAction.Marker,out string marker)||marker!=payment.SourceOperation.ToString("N")||
                !queued.TryGetMetadata(RebirthRemoteResourceOwnerAction.BindingMarker,out string binding)||binding!=payment.Binding)return false;
            ReadActor(root.Element("beforeActor")?.Value);ReadActor(root.Element("afterActor")?.Value);action=new XElement(root);return true;
        }
        catch{action=null;return false;}
    }
    internal static bool BeginRepairOriginalExecution(EntityPlayerLocal player,Intent original,Func<bool> authenticatedPermission)
    {
        try
        {
            if(!Current(player,original)||Rejections().Read(original.Operation)!=null||authenticatedPermission?.Invoke()!=true||!TryReadRepairAction(original,out var action)||
                !ReadActor(action.Element("beforeActor").Value).Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            var journal=Actions();var record=journal.Read(original.Operation);
            // Uncertain is an attempted callback, never permission to invoke
            // it again. Recovery must observe its original accepted queue.
            if(record?.State!=RebirthCamperTransferJournal.Phase.Prepared)return false;
            record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            return record.State==RebirthCamperTransferJournal.Phase.Uncertain&&authenticatedPermission();
        }
        catch{return false;}
    }
    // A separate monotone tombstone prevents delayed permission from invoking
    // the callback. Do not mark Actions rejected: a previously accepted native
    // queue must remain observable as used until the host proves no-use.
    internal static bool SaveRepairRejection(EntityPlayerLocal player,Intent original,Func<bool> authenticatedOriginal)
    {
        try
        {
            if(!Current(player,original)||authenticatedOriginal?.Invoke()!=true||!TryReadRepairAction(original,out var action))return false;
            byte[] body=Encoding.UTF8.GetBytes(action.ToString(SaveOptions.DisableFormatting));var journal=Rejections();
            var saved=journal.Prepare(original.Operation,original.Operation,original.Creation,0,body,body);
            if(saved.Vehicle!=original.Operation||saved.OwnerCreation!=original.Creation||!saved.Before.SequenceEqual(body)||!saved.After.SequenceEqual(body))return false;
            if(saved.State==RebirthCamperTransferJournal.Phase.Prepared)saved=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(saved.State==RebirthCamperTransferJournal.Phase.Uncertain)saved=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            return saved.State==RebirthCamperTransferJournal.Phase.Applied;
        }
        catch{return false;}
    }
    internal static bool SaveUnpreparedRepairRejection(EntityPlayerLocal player,Intent original,Func<bool> authenticatedOriginal)
    {
        try
        {
            if(!Current(player,original)||original.Kind!=RemoteResourceClientOperation.ItemRepair||authenticatedOriginal?.Invoke()!=true||Actions().Read(original.Operation)!=null)return false;
            byte[] bytes=Encoding.UTF8.GetBytes(original.Copy().ToString(SaveOptions.DisableFormatting));var journal=Rejections();var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied;
        }
        catch{return false;}
    }
    internal static bool SaveUnpreparedRepairOwnerFive(EntityPlayerLocal player,Intent original,Func<bool> authenticatedRejecting)
    {
        try
        {
            if(!Current(player,original)||authenticatedRejecting?.Invoke()!=true||Actions().Read(original.Operation)!=null||
                !SaveUnpreparedRepairRejection(player,original,authenticatedRejecting))return false;
            var root=original.Copy();var source=root.Element("action");int kind=(int)source.Attribute("sourceKind"),slot=(int)source.Attribute("sourceSlot");
            var slots=kind==1?player.bag?.ItemGrid.items:kind==2?player.inventory?.ItemGrid.items:player.equipment?.itemGrid?.items;
            if(!ReadActor(root.Element("actorBefore").Value).Matches(RebirthCamperEnergyActorImage.Capture(player))||slots==null||slot>=slots.Length||slots[slot]?.count!=1||
                RebirthNativeItemCodec.Encode(slots[slot].itemValue)!=source.Element("nativeInput").Value)return false;
            if(!RebirthRemoteResourceOwnerAction.IsHeld(player)&&!RebirthRemoteResourceOwnerAction.TryHoldPreparedOriginal(player,original))return false;
            player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation),5f,true);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
        }
        catch{return false;}
    }
    internal static bool CompleteRefundOriginal(EntityPlayerLocal player,Intent original,Guid source,string binding,string proof,Func<bool> authenticatedTerminal)
    {
        try
        {
            if(!RebirthRemoteResourceRepairRefund.Current(player,original)||authenticatedTerminal?.Invoke()!=true||!RebirthRemoteResourceRepairRefund.HasTerminal(original,source,binding,proof))return false;
            if(Journal().Read(original.Operation)==null)return false;
            foreach(var journal in new[]{Actions(),Journal()})
            {
                var record=journal.Read(original.Operation);if(record==null)continue;
                if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
                if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
                if(record.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
            }
            return RebirthRemoteResourceOwnerAction.ReleaseRepairTerminal(player,original,source,binding,proof);
        }
        catch{return false;}
    }
    internal static bool CompleteBlockRefundMetadata(Intent original,Guid source,string binding,string proof)
    {
        try
        {
            if(!RebirthRemoteResourceBlockRefund.HasClientTerminal(original,source,binding,proof))return false;
            var journal=Journal();var record=journal.Read(original.Operation);if(record==null)return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied;
        }
        catch{return false;}
    }
    internal static bool TrySaveRejectedRepairOwner(EntityPlayerLocal player,Intent original,ItemActionEntryRepair entry,Func<bool> authenticatedHostRejecting)
    {
        try
        {
            if(!Current(player,original)||authenticatedHostRejecting?.Invoke()!=true||!RebirthRemoteResourceOwnerAction.IsHeld(player)||
                !TryReadRepairAction(original,out var action)||!RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(action.Element("payment").Value,out var payment))return false;
            byte[] body=Encoding.UTF8.GetBytes(action.ToString(SaveOptions.DisableFormatting));var rejected=Rejections().Read(original.Operation);
            if(rejected?.State!=RebirthCamperTransferJournal.Phase.Applied||rejected.Vehicle!=original.Operation||rejected.OwnerCreation!=original.Creation||
                !rejected.Before.SequenceEqual(body)||!rejected.After.SequenceEqual(body)||
                !ReadActor(action.Element("beforeActor").Value).Matches(RebirthCamperEnergyActorImage.Capture(player))||
                !RebirthRemoteResourceOwnerAction.CurrentRepairSource(player,payment))return false;
            // Phase five alone grants no refund: host must validate both actual
            // queue captures, original source slot and this full Before image.
            player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation),5f,true);
            GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
        }
        catch{return false;}
    }
    internal static bool TrySaveRepairOwnerUsage(EntityPlayerLocal player,Intent original,ItemActionEntryRepair entry)
    {
        try
        {
            if(!Current(player,original)||!TryReadRepairAction(original,out var action)||Actions().Read(original.Operation)?.State!=RebirthCamperTransferJournal.Phase.Uncertain||
                !RebirthRemoteResourceOwnerAction.IsHeld(player)||
                !RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(action.Element("payment").Value,out var payment)||
                !RebirthRepairOriginalCorrection.TryGetCapturedRemoteOriginal(entry,payment.Encode(),out var repair,out string queued,out int amount)||
                queued!=action.Element("queuedInput").Value||amount!=(int)action.Element("amount")||
                !RebirthNativeItemCodec.TryDecode(queued,out var input)||!input.TryGetMetadata("RebirthRepairOriginalV1",out string marker)||marker!=repair.ToString("N")||
                !ReadActor(action.Element("afterActor").Value).Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation),2f,true);
            GameManager.Instance.SaveLocalPlayerData();
            var persistent=GameManager.Instance.GetPersistentLocalPlayer();var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(persistent?.PrimaryId==null||connection==null||connection.IsServer||!connection.IsConnected)return false;
            connection.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(player),false);
            return true;
        }
        catch{return false;}
    }
    internal static bool TrySaveRestoredRepairOwnerUsage(EntityPlayerLocal player,Intent original)
    {
        try
        {
            if(!Current(player,original)||!TryReadRepairAction(original,out var action)||Actions().Read(original.Operation)?.State!=RebirthCamperTransferJournal.Phase.Uncertain||
                !RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(action.Element("payment").Value,out var payment)||
                !RebirthRepairOriginalCorrection.TryObserveRestoredRemoteOriginal(player,payment.Encode(),original.Copy().Element("action")?.Element("nativeInput")?.Value,
                    action.Element("queuedInput").Value,(int)action.Element("amount"),out _)||
                !ReadActor(action.Element("afterActor").Value).Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            // This is bookkeeping/upload of an already accepted original. It
            // cannot create a host ownerAction, a queue or another item debit.
            player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation),2f,true);
            GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
        }
        catch{return false;}
    }
    internal static bool SaveRepairTerminal(EntityPlayerLocal player,Intent original,Guid sourceOperation,string binding,string hostProof,Func<bool> authenticatedTerminal)
    {
        try
        {
            if(!Current(player,original)||!Hex(binding)||!Hex(hostProof)||authenticatedTerminal?.Invoke()!=true||!TryReadRepairAction(original,out var action)||
                !RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(action.Element("payment").Value,out var payment)||payment.SourceOperation!=sourceOperation||payment.Binding!=binding)return false;
            string queued=action.Element("queuedInput").Value;
            if(!RebirthNativeItemCodec.TryDecode(queued,out var native)||!native.TryGetMetadata("RebirthRepairOriginalV1",out string repair)||!Guid.TryParseExact(repair,"N",out var repairOriginal)||repairOriginal==Guid.Empty||
                hostProof!=RebirthRemoteResourceDurableJournal.Digest(payment.Encode()+"|"+repairOriginal.ToString("N")+"|"+queued+"|"+(int)action.Element("amount")+"|"+action.Element("afterActor").Value))return false;
            var body=new XElement("fetchClientRepairTerminal",new XAttribute("version",1),new XAttribute("original",original.Operation.ToString("N")),new XAttribute("originalDigest",original.Digest),
                new XAttribute("sourceOperation",sourceOperation.ToString("N")),new XAttribute("binding",binding),new XAttribute("hostProof",hostProof),
                new XAttribute("actionDigest",RebirthRemoteResourceDurableJournal.Digest(action.ToString(SaveOptions.DisableFormatting))));
            byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));var terminal=Terminals();var record=terminal.Prepare(sourceOperation,original.Operation,original.Creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes)||authenticatedTerminal()!=true)return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=terminal.Advance(sourceOperation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=terminal.Advance(sourceOperation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            if(record.State!=RebirthCamperTransferJournal.Phase.Applied||!authenticatedTerminal())return false;
            // Publish the positive terminal mirror before retiring either
            // original. Interruption between these stages remains recoverable.
            foreach(var journal in new[]{Actions(),Journal()})
            {
                var retained=journal.Read(original.Operation);if(retained==null)return false;
                if(retained.State==RebirthCamperTransferJournal.Phase.Prepared)retained=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
                if(retained.State==RebirthCamperTransferJournal.Phase.Uncertain)retained=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
                if(retained.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
            }
            return RebirthRemoteResourceOwnerAction.ReleaseRepairTerminal(player,original,sourceOperation,binding,hostProof);
        }
        catch{return false;}
    }
    private static bool NoPaymentActionSupported(Intent original)=>original!=null&&(original.Kind==RemoteResourceClientOperation.ItemRepair||original.Kind==RemoteResourceClientOperation.Craft||
        (original.Kind==RemoteResourceClientOperation.BlockRepair||original.Kind==RemoteResourceClientOperation.BlockUpgrade)&&RebirthRemoteResourceBlockOriginal.CanUploadInitial(original));
    internal static bool SaveNoPaymentTerminal(EntityPlayerLocal player,Intent original,string sourceWorld,Guid sourceOperation,string binding,string proof,Func<bool> authenticatedTerminal)
    {
        try
        {
            if(!RebirthRemoteResourceRepairAdoption.QueryScope(player,original)||!NoPaymentActionSupported(original)||!Hex(sourceWorld)||!Hex(binding)||!Hex(proof)||authenticatedTerminal?.Invoke()!=true||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||
                RebirthRemoteResourceDurableJournal.Operation(sourceWorld,identity.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind)!=sourceOperation||
                proof!=RebirthRemoteResourceDurableJournal.Digest("authenticated-no-payment|"+identity.StorageKey+"|"+binding)||Actions().Read(original.Operation)!=null||Sources().Read(original.Operation)!=null)return false;
            var image=new XElement("fetchClientNoPaymentTerminal",new XAttribute("version",1),new XAttribute("original",original.Operation.ToString("N")),new XAttribute("originalDigest",original.Digest),
                new XAttribute("sourceWorld",sourceWorld),new XAttribute("sourceOperation",sourceOperation.ToString("N")),new XAttribute("binding",binding),new XAttribute("proof",proof));
            byte[] bytes=Encoding.UTF8.GetBytes(image.ToString(SaveOptions.DisableFormatting));var terminal=Terminals();var record=terminal.Prepare(sourceOperation,original.Operation,original.Creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes)||authenticatedTerminal()!=true)return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=terminal.Advance(sourceOperation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=terminal.Advance(sourceOperation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            if(record.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
            var journal=Journal();var intent=journal.Read(original.Operation);if(intent==null||Decode(intent).Digest!=original.Digest)return false;
            if(intent.State==RebirthCamperTransferJournal.Phase.Prepared)intent=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(intent.State==RebirthCamperTransferJournal.Phase.Uncertain)intent=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            return intent.State==RebirthCamperTransferJournal.Phase.Applied&&authenticatedTerminal()&&RebirthRemoteResourceOwnerAction.ReleaseNoPaymentTerminal(player,original,sourceOperation,binding,proof);
        }
        catch{return false;}
    }
    internal static bool HasNoPaymentTerminal(Intent original,Guid operation,string binding,string proof)
    {
        try
        {
            if(!NoPaymentActionSupported(original)||!Hex(binding)||!Hex(proof)||Actions().Read(original.Operation)!=null||Sources().Read(original.Operation)!=null)return false;
            var record=Terminals().Read(operation);if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Before.Length>16384)return false;
            var image=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),16384);
            return image.Name=="fetchClientNoPaymentTerminal"&&image.Attributes().Count()==7&&!image.Nodes().Any()&&(int?)image.Attribute("version")==1&&
                (string)image.Attribute("original")==original.Operation.ToString("N")&&(string)image.Attribute("originalDigest")==original.Digest&&
                (string)image.Attribute("sourceOperation")==operation.ToString("N")&&(string)image.Attribute("binding")==binding&&(string)image.Attribute("proof")==proof&&Hex((string)image.Attribute("sourceWorld"));
        }
        catch{return false;}
    }
    internal static bool HasRepairTerminal(Intent original,Guid sourceOperation,string binding,string proof)
    {
        try
        {
            if(original==null||!Hex(binding)||!Hex(proof)||!TryReadRepairAction(original,out var action))return false;
            var record=Terminals().Read(sourceOperation);if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Before.Length>16384)return false;
            var root=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),16384);
            return root.Name=="fetchClientRepairTerminal"&&root.Attributes().Count()==7&&!root.Nodes().Any()&&(int?)root.Attribute("version")==1&&
                (string)root.Attribute("original")==original.Operation.ToString("N")&&(string)root.Attribute("originalDigest")==original.Digest&&
                (string)root.Attribute("sourceOperation")==sourceOperation.ToString("N")&&(string)root.Attribute("binding")==binding&&(string)root.Attribute("hostProof")==proof&&
                (string)root.Attribute("actionDigest")==RebirthRemoteResourceDurableJournal.Digest(action.ToString(SaveOptions.DisableFormatting));
        }
        catch{return false;}
    }
    internal static bool SaveCraftTerminal(EntityPlayerLocal player,Intent original,Guid sourceOperation,string binding,string actualHostProof,
        string exactMarkedRecipe,int count,int quality,RebirthCamperEnergyActorImage after,Func<bool> authenticatedSavedTerminal)
    {
        try
        {
            if(!RebirthRemoteResourceCraftAdoption.QueryScope(player,original)||original.Kind!=RemoteResourceClientOperation.Craft||sourceOperation==Guid.Empty||!Hex(binding)||!Hex(actualHostProof)||
                count<1||count>short.MaxValue||quality<0||quality>byte.MaxValue||exactMarkedRecipe==null||exactMarkedRecipe.Length>6*1024*1024||after==null||authenticatedSavedTerminal?.Invoke()!=true)return false;
            var root=new XElement("fetchClientCraftTerminal",new XAttribute("version",1),new XAttribute("sourceOperation",sourceOperation.ToString("N")),
                new XAttribute("binding",binding),new XAttribute("hostProof",actualHostProof),new XAttribute("original",original.Operation.ToString("N")),new XAttribute("originalDigest",original.Digest),
                new XAttribute("world",original.World),new XAttribute("owner",original.Owner),new XAttribute("creation",original.Creation),new XAttribute("deaths",original.Deaths),new XAttribute("count",count),new XAttribute("quality",quality),
                new XElement("markedRecipe",exactMarkedRecipe),new XElement("afterActor",ActorImage(after)));
            byte[] bytes=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));if(bytes.Length>16*1024*1024)return false;
            var journal=Terminals();var record=journal.Prepare(sourceOperation,original.Operation,original.Creation,0,bytes,bytes);
            if(!authenticatedSavedTerminal())return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(sourceOperation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(sourceOperation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied&&authenticatedSavedTerminal();
        }
        catch{return false;}
    }
    internal static bool TryReadCraftTerminal(Guid sourceOperation,string binding,out XElement terminal)
    {
        terminal=null;
        try
        {
            if(!ThreadManager.IsMainThread()||sourceOperation==Guid.Empty||!Hex(binding))return false;
            var record=Terminals().Read(sourceOperation);if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Before.Length>16*1024*1024)return false;
            XElement root;using(var input=new MemoryStream(record.Before,false))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16*1024*1024}))root=XElement.Load(reader);
            if(root.Name!="fetchClientCraftTerminal"||root.Attributes().Count()!=12||root.Elements().Count()!=2||(int?)root.Attribute("version")!=1||
                (string)root.Attribute("sourceOperation")!=sourceOperation.ToString("N")||(string)root.Attribute("binding")!=binding||!Hex((string)root.Attribute("hostProof"))||
                !Guid.TryParseExact((string)root.Attribute("original"),"N",out var original)||original!=record.Vehicle||!TryRead(original,out var intent)||
                intent.Kind!=RemoteResourceClientOperation.Craft||intent.Digest!=(string)root.Attribute("originalDigest")||intent.World!=(string)root.Attribute("world")||
                intent.Owner!=(string)root.Attribute("owner")||intent.Creation!=(string)root.Attribute("creation")||intent.Deaths!=(int?)root.Attribute("deaths")||
                (int?)root.Attribute("count") is not int count||count<1||count>short.MaxValue||(int?)root.Attribute("quality") is not int quality||quality<0||quality>byte.MaxValue||root.Element("markedRecipe")?.Value.Length>6*1024*1024)return false;
            ReadActor(root.Element("afterActor")?.Value);if(root.Element("markedRecipe")==null)return false;terminal=new XElement(root);return true;
        }
        catch{terminal=null;return false;}
    }
    internal static bool CompleteCraftTerminal(Intent original,Guid sourceOperation,string binding)
    {
        try
        {
            if(original==null||!TryReadCraftTerminal(sourceOperation,binding,out var proof)||(string)proof.Attribute("original")!=original.Operation.ToString("N")||
                (string)proof.Attribute("originalDigest")!=original.Digest)return false;
            var journal=Journal();var record=journal.Read(original.Operation);if(record==null||Decode(record).Digest!=original.Digest)return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied;
        }
        catch{return false;}
    }
    internal static bool CompletePlacedTerminal(EntityPlayerLocal player,Intent original,Guid source,string binding,string proof)
    {
        try
        {
            if(!Current(player,original)||!RebirthRemoteResourcePlacedStationOriginal.HasClientRetired(original,source,binding,proof))return false;
            var journal=Journal();var record=journal.Read(original.Operation);if(record==null||Decode(record).Digest!=original.Digest)return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied;
        }
        catch{return false;}
    }
    internal static bool CompleteFetchedStationTerminal(EntityPlayerLocal player,Intent original,Guid source,string binding,string proof,byte[] payload,RebirthCamperStationAddress address)
    {
        try
        {
            if(!Current(player,original)||!RebirthRemoteResourceFetchedStationOriginal.HasClientUsedTerminal(player,original.Operation,address,original.Creation,payload,out var actual,out var actualBinding,out var actualProof)||
                actual!=source||actualBinding!=binding||actualProof!=proof)return false;
            var journal=Journal();var record=journal.Read(original.Operation);if(record==null||Decode(record).Digest!=original.Digest)return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied;
        }
        catch{return false;}
    }
    internal static bool CompleteBlockTerminal(EntityPlayerLocal player,Intent original,Guid sourceOperation,string binding,string usedProof)
    {
        try
        {
            if(!RebirthRemoteResourceBlockOriginal.TerminalScope(player,original)||!RebirthRemoteResourceBlockOriginal.HasUsedTerminal(original,sourceOperation,binding,usedProof))return false;
            var journal=Journal();var record=journal.Read(original.Operation);if(record==null||Decode(record).Digest!=original.Digest)return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied;
        }
        catch{return false;}
    }
    private static string Image(ItemStack[] items)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(items));
    internal static string ActorImage(RebirthCamperEnergyActorImage actor)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {actor.Write(writer);writer.Flush();if(stream.Length>6*1024*1024)throw new InvalidDataException();return Convert.ToBase64String(stream.ToArray());}
    }
    internal static RebirthCamperEnergyActorImage ReadActor(string text)
    {
        if(text==null||text.Length>8*1024*1024)throw new InvalidDataException();
        using(var stream=new MemoryStream(Convert.FromBase64String(text),false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
        {var value=RebirthCamperEnergyActorImage.Read(reader);if(stream.Position!=stream.Length||ActorImage(value)!=text)throw new InvalidDataException();return value;}
    }
    internal static string QueueImage(CraftingData data)
    {
        if(data?.RecipeQueueItems==null||data.RecipeQueueItems.Length>255)throw new InvalidDataException();
        using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(stream);data.Write(writer);writer.Flush();if(stream.Length>4*1024*1024)throw new InvalidDataException();return Convert.ToBase64String(stream.ToArray());}
    }
    private static Guid Key(string world,string owner,string creation,ulong epoch,ulong request,RemoteResourceClientOperation kind)=>
        RebirthCookingRemoteAdmissionBackend.StationKey(world,owner+"|fetch-owner|"+creation+"|"+epoch+"|"+request+"|"+(byte)kind);
    internal static Guid OperationFor(string world,string canonicalOwner,string creation,ulong epoch,ulong request,RemoteResourceClientOperation kind)=>Key(world,canonicalOwner,creation,epoch,request,kind);
    private static Intent Decode(RebirthCamperTransferJournal.Record record)
    {
        if(record?.Before==null||record.Before.Length>MaximumOriginalBytes)throw new InvalidDataException();XElement root;
        using(var input=new MemoryStream(record.Before,false))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumOriginalBytes}))root=XElement.Load(reader);
        if(root.Name!="fetchClientOriginal"||(int?)root.Attribute("version")!=1||root.Attributes().Count()!=9||root.Elements().Count()!=5||
            !Guid.TryParse((string)root.Attribute("world"),out _)||string.IsNullOrEmpty((string)root.Attribute("owner"))||((string)root.Attribute("owner")).Length>512||
            !RebirthOriginalActionScope.TryNormalize((string)root.Attribute("creation"),out var creation)||creation!=(string)root.Attribute("creation")||
            (int?)root.Attribute("deaths") is not int deaths||deaths<0||(ulong?)root.Attribute("epoch") is not ulong epoch||epoch==0||
            (ulong?)root.Attribute("request") is not ulong request||request==0||checked((byte?)(int?)root.Attribute("kind")) is not byte kind||kind<1||kind>4||
            record.OwnerCreation!=creation||(string)root.Attribute("operation")!=record.Operation.ToString("N")||Key((string)root.Attribute("world"),(string)root.Attribute("owner"),creation,epoch,request,(RemoteResourceClientOperation)kind)!=record.Operation||
            root.Elements().Any(node=>!new[]{"requirements","remoteRequirements","actorBefore","queueBefore","action"}.Contains(node.Name.LocalName)||node.Name.NamespaceName.Length!=0)||
            root.Elements().GroupBy(node=>node.Name).Any(group=>group.Count()!=1))throw new InvalidDataException();
        if(kind==(byte)RemoteResourceClientOperation.Craft&&root.Element("action")?.Attribute("placed")!=null&&
            !RebirthRemoteResourcePlacedStationAllocation.TryAdmitIntentBeforeHydration(root))throw new InvalidDataException("Placed original allocation preflight refused before native hydration.");
        foreach(string name in new[]{"requirements","remoteRequirements"})
        {
            var stacks=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element(name)?.Value??throw new InvalidDataException()));
            if(stacks.Length<1||stacks.Length>4096||stacks.Any(item=>item==null||item.IsEmpty()||item.count<1))throw new InvalidDataException();
        }
        ReadActor(root.Element("actorBefore")?.Value);string queue=root.Element("queueBefore")?.Value;
        if(kind==(byte)RemoteResourceClientOperation.ItemRepair)
        {
            var action=root.Element("action");int sourceKind=(int?)action.Attribute("sourceKind")??0,sourceSlot=(int?)action.Attribute("sourceSlot")??-1;
            if(action.Attributes().Count()!=2||action.Elements().Count()!=1||action.Element("nativeInput")==null||
                sourceSlot<0||!(sourceKind==1&&sourceSlot<169||sourceKind==2&&sourceSlot<20||sourceKind==3&&sourceSlot<13)||
                !RebirthNativeItemCodec.TryDecode(action.Element("nativeInput").Value,out var source)||RebirthNativeItemCodec.Encode(source)!=action.Element("nativeInput").Value)throw new InvalidDataException();
        }
        if(queue==null||queue.Length>6*1024*1024||root.Element("action")==null)throw new InvalidDataException();
        byte[] queueBytes=Convert.FromBase64String(queue);
        if(queueBytes.Length<3||queueBytes.Length>4*1024*1024||queueBytes[0]!=2||queueBytes[1]!=0||Convert.ToBase64String(queueBytes)!=queue)throw new InvalidDataException();
        // Opaque exact native BEFORE bytes, never restored or effect authority.
        // Do not hydrate/rewrite through a lossy native item reader here. Actual
        // accepted usage is independently read from the final native file.
        return new Intent(record.Operation,root);
    }
    internal static bool TryDecodeOriginal(string text,out Intent original)
    {
        original=null;
        try
        {
            var root=RebirthCookingRemoteAdmission.ReadProtocolXml(text,MaximumOriginalBytes);
            if(!Guid.TryParseExact((string)root.Attribute("operation"),"N",out var operation))return false;
            byte[] body=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));
            original=Decode(new RebirthCamperTransferJournal.Record(operation,operation,(string)root.Attribute("creation"),0,RebirthCamperTransferJournal.Phase.Prepared,body,body));
            return original.Copy().ToString(SaveOptions.DisableFormatting)==text;
        }
        catch{original=null;return false;}
    }
    internal static bool CompleteNeverAdmitted(EntityPlayerLocal player,Intent original)
    {
        try
        {
            if(!Current(player,original)||!RebirthRemoteResourcePreAdmissionRefusal.HasClientTerminal(original))return false;
            var journal=Journal();var record=journal.Read(original.Operation);if(record==null||Decode(record).Digest!=original.Digest)return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied&&RebirthRemoteResourceOwnerAction.ReleaseNeverAdmittedTerminal(player,original);
        }
        catch{return false;}
    }
    internal static bool HasNeverPreparedRepairAction(Intent original)
    {try{return original?.Kind==RemoteResourceClientOperation.ItemRepair&&Actions().Read(original.Operation)==null;}catch{return false;}}
    internal static bool Current(EntityPlayerLocal player,Intent original)=>ThreadManager.IsMainThread()&&player?.world!=null&&
        (player.world.IsRemote()||original?.Kind==RemoteResourceClientOperation.Craft&&RebirthRemoteResourceCraftOriginal.ValidateInput(original,out _,out _,out _,out _,out _))&&player.IsSpawned()&&!player.IsDead()&&
        ReferenceEquals(GameManager.Instance?.World,player.world)&&ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&ReferenceEquals(player.world.GetEntity(player.entityId),player)&&
        original!=null&&original.World==player.world.worldState.Guid&&original.Deaths==player.Died&&original.Creation==RebirthCamperCharacterAccess.CurrentCreation(player)&&
        RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)&&identity.CanonicalId==original.Owner;
    internal static bool TryPrepare(EntityPlayerLocal player,ulong epoch,ulong request,RemoteResourceClientOperation kind,
        IList<ItemStack> fullRequirements,IList<ItemStack> remoteRequirements,XElement actualAction,out Intent original)
    {
        original=null;
        try
        {
            if(!ThreadManager.IsMainThread()||player?.world==null||!player.world.IsRemote()&&kind!=RemoteResourceClientOperation.Craft||!player.IsSpawned()||player.IsDead()||epoch==0||request==0||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||actualAction?.Name!="action")return false;
            string creation=RebirthCamperCharacterAccess.CurrentCreation(player);var key=Key(player.world.worldState.Guid,identity.CanonicalId,creation,epoch,request,kind);
            var root=new XElement("fetchClientOriginal",new XAttribute("version",1),new XAttribute("world",player.world.worldState.Guid),new XAttribute("owner",identity.CanonicalId),
                new XAttribute("creation",creation),new XAttribute("deaths",player.Died),new XAttribute("epoch",epoch),new XAttribute("request",request),new XAttribute("kind",(byte)kind),new XAttribute("operation",key.ToString("N")),
                new XElement("requirements",Image(fullRequirements.ToArray())),new XElement("remoteRequirements",Image(remoteRequirements.ToArray())),
                new XElement("actorBefore",ActorImage(RebirthCamperEnergyActorImage.Capture(player))),new XElement("queueBefore",QueueImage(player.PlayerUI.xui.GetCraftingData())),new XElement(actualAction));
            byte[] body=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));var candidate=Decode(new RebirthCamperTransferJournal.Record(key,key,creation,0,RebirthCamperTransferJournal.Phase.Prepared,body,body));
            if(!Current(player,candidate))return false;var saved=Journal().Prepare(key,key,creation,0,body,body);original=Decode(saved);return Current(player,original);
        }
        catch{original=null;return false;}
    }
    internal static bool TryPrepareRepair(EntityPlayerLocal player,ItemActionEntryRepair entry,ulong epoch,ulong request,IList<ItemStack> fullRequirements,IList<ItemStack> remoteRequirements,out Intent original)
    {
        original=null;
        try
        {
            if(!ReferenceEquals(entry?.ItemController?.xui?.playerUI?.entityPlayer,player)||fullRequirements==null||fullRequirements.Count!=1||
                fullRequirements[0]==null||fullRequirements[0].IsEmpty()||
                !RebirthRepairOriginalCorrection.TryAssessRemoteOriginal(entry,fullRequirements[0].itemValue.type,fullRequirements[0].count,out _)||
                !RebirthRemoteResourceOwnerAction.TrySource(entry,out int kind,out int slot,out string input))return false;
            var action=new XElement("action",new XAttribute("sourceKind",kind),new XAttribute("sourceSlot",slot),new XElement("nativeInput",input));
            if(!TryPrepare(player,epoch,request,RemoteResourceClientOperation.ItemRepair,fullRequirements,remoteRequirements,action,out original))return false;
            return RebirthRemoteResourceOwnerAction.TrySource(entry,out int currentKind,out int currentSlot,out string currentInput)&&currentKind==kind&&currentSlot==slot&&currentInput==input;
        }
        catch{original=null;return false;}
    }
    internal static bool TryRead(Guid operation,out Intent original)
    {original=null;try{var record=Journal().Read(operation);if(record==null)return false;original=Decode(record);return true;}catch{return false;}}
    // Read-only scenario observation; no terminal is created or advanced here.
    internal static bool IsCompletedBlockOriginal(Intent original)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||original==null||original.Kind!=RemoteResourceClientOperation.BlockRepair&&original.Kind!=RemoteResourceClientOperation.BlockUpgrade)return false;
            var record=Journal().Read(original.Operation);
            return record?.State==RebirthCamperTransferJournal.Phase.Applied&&Decode(record).Digest==original.Digest;
        }
        catch{return false;}
    }
    internal static bool TryReadPending(out Intent[] originals)
    {
        originals=null;var result=new List<Intent>();
        try
        {
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteResourceClientOriginals");if(!Directory.Exists(directory)){originals=new Intent[0];return true;}
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return false;int count=0;
            foreach(string file in Directory.EnumerateFiles(directory,"*.0.rcj"))
            {
                if(!Guid.TryParseExact(Path.GetFileName(file).Substring(0,32),"N",out var operation))return false;
                var record=Journal().Read(operation);if(record==null)return false;
                if(record.State==RebirthCamperTransferJournal.Phase.Applied||record.State==RebirthCamperTransferJournal.Phase.Rejected){Journal().Archive(operation);continue;}
                if(++count>4096)return false;result.Add(Decode(record));
            }
            originals=result.ToArray();return true;
        }
        catch{return false;}
    }
}
