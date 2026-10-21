using System.Collections.Generic;
using System.Text;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Immutable assembly portion of durable custody. Inventory legs and authority are
// validated by the coordinator; this record alone never authorizes an item move.
public sealed class RebirthVehicleTransferPlan
{
    public Guid TransactionId { get; private set; }
    public string CreationId { get; private set; }
    public int NativeDeaths {get;private set;}=-1;
    internal string OwnerScope=>CreationId.Length>0?CreationId:"native-life:"+NativeDeaths.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string ActorId { get; private set; }
    public string Fingerprint { get; private set; }
    public string BeforePayload { get; private set; }
    public string AfterPayload { get; private set; }
    public RebirthVehicleTransferTarget Target { get; private set; }
    public RebirthVehicleTransferFuel Fuel { get; private set; }
    public RebirthVehicleTransferHealth Health { get; private set; }
    public RebirthVehicleTransferCustody Custody {get;private set;}
    public RebirthVehicleTransferTraining Training {get;private set;}

    public IReadOnlyList<RebirthVehicleTransferItem> Items { get; private set; }

    private RebirthVehicleTransferPlan() { }

    public static RebirthVehicleTransferPlan Create(Guid transactionId, Guid creationId,
        string actorId, string fingerprint, RebirthVehicleAssembly before, RebirthVehicleAssembly after,
        IEnumerable<RebirthVehicleTransferItem> items = null, RebirthVehicleTransferTarget target = null, RebirthVehicleTransferFuel fuel = null, RebirthVehicleTransferHealth health = null,int nativeDeaths=-1,RebirthVehicleTransferCustody custody=null)
        =>Create(transactionId,creationId.ToString("N"),actorId,fingerprint,before,after,items,target,fuel,health,nativeDeaths,custody);
    public static RebirthVehicleTransferPlan Create(Guid transactionId, string creationId,
        string actorId, string fingerprint, RebirthVehicleAssembly before, RebirthVehicleAssembly after,
        IEnumerable<RebirthVehicleTransferItem> items = null, RebirthVehicleTransferTarget target = null, RebirthVehicleTransferFuel fuel = null, RebirthVehicleTransferHealth health = null,int nativeDeaths=-1,RebirthVehicleTransferCustody custody=null)
    {
        string normalizedCreation=null;
        bool nativeOwner=creationId==string.Empty&&nativeDeaths>=0;
        if (transactionId == Guid.Empty || nativeDeaths< -1 ||nativeDeaths>=0&&target==null||custody!=null&&nativeDeaths<0||(!nativeOwner&&!RebirthSurvivorRequestScope.TryNormalize(creationId,out normalizedCreation))
            || string.IsNullOrWhiteSpace(actorId) || actorId.Length > 1024
            || string.IsNullOrWhiteSpace(fingerprint) || fingerprint.Length > 524288)
            throw new InvalidDataException("Invalid vehicle transfer identity.");
        if (before == null || after == null || before.AssemblyId == Guid.Empty
            || before.AssemblyId != after.AssemblyId || before.ContentId != after.ContentId
            || before.FamilyId != after.FamilyId || before.Revision < 0 || before.Revision == long.MaxValue
            || after.Revision != before.Revision + 1
            || before.PendingInventoryTransferId != Guid.Empty
            || before.LastAppliedInventoryTransferId == transactionId
            || after.PendingInventoryTransferId != Guid.Empty
            || after.LastAppliedInventoryTransferId != transactionId)
            throw new InvalidDataException("Vehicle transfer snapshots do not describe one completed revision.");
        if (fuel != null && (target == null || target.Carrier != RebirthVehicleAssemblyCarrier.VehicleEntity
            || before.FuelPercent != fuel.BeforeLevel / fuel.Capacity || after.FuelPercent != fuel.AfterLevel / fuel.Capacity))
            throw new InvalidDataException("Native fuel transition does not match the entity assembly snapshots.");
        if (health != null && (target == null || target.Carrier != RebirthVehicleAssemblyCarrier.VehicleEntity || fuel != null))
            throw new InvalidDataException("Repair transition requires an entity target without fuel mutation.");
        var operations = (items ?? Enumerable.Empty<RebirthVehicleTransferItem>()).Take(257).ToArray();
        if (operations.Length > 256 || operations.Any(i => i == null)
            || operations.Select(i => i.ReceiptId).Distinct().Count() != operations.Length
            || operations.Any(i => i.ReceiptId == transactionId))
            throw new InvalidDataException("Invalid or repeated vehicle item receipt identity.");
        var plan = new RebirthVehicleTransferPlan {
            TransactionId = transactionId, CreationId = normalizedCreation??string.Empty, NativeDeaths=nativeDeaths,ActorId = actorId,
            Fingerprint = fingerprint, Target = target, Fuel = fuel, Health = health,Custody=custody,
            BeforePayload = RebirthVehicleAssemblySerializer.ToBase64(before),
            AfterPayload = RebirthVehicleAssemblySerializer.ToBase64(after),
            Items = Array.AsReadOnly(operations)
        };
        if (Encoding.UTF8.GetByteCount(plan.ToXml().ToString(SaveOptions.DisableFormatting)) > 2097152)
            throw new InvalidDataException("Vehicle transfer plan exceeds size limit.");
        return plan;
    }

    // Fresh detached values prevent a caller changing custody after preparation.
    public RebirthVehicleAssembly ReadBefore() { return RebirthVehicleAssemblySerializer.FromBase64(BeforePayload); }
    public RebirthVehicleAssembly ReadAfter() { return RebirthVehicleAssemblySerializer.FromBase64(AfterPayload); }
    internal RebirthVehicleTransferPlan WithCustody(RebirthVehicleTransferCustody custody,IEnumerable<RebirthVehicleTransferItem> items=null)
    {
        var copy=Create(TransactionId,CreationId,ActorId,Fingerprint,ReadBefore(),ReadAfter(),items??Items,Target,Fuel,Health,NativeDeaths,custody);
        copy.Training=Training;return copy;
    }
    internal RebirthVehicleTransferPlan WithTraining(RebirthVehicleTransferTraining training)
    {
        if(training==null||Custody==null||NativeDeaths<0||CreationId.Length==0)throw new InvalidDataException("Vehicle training requires the native Survivor original.");
        var copy=WithCustody(Custody);copy.Training=training;
        if(Encoding.UTF8.GetByteCount(copy.ToXml().ToString(SaveOptions.DisableFormatting))>2097152)throw new InvalidDataException("Vehicle training original exceeds size limit.");
        return copy;
    }

    public XElement ToXml()
    {
        return new XElement("vehicleTransferPlan", new XAttribute("version",Training!=null?8: Custody!=null?7:NativeDeaths>=0?6:Health != null ? 5 : Fuel != null ? 4 : Target == null ? 2 : 3),
            NativeDeaths>=0?new XAttribute("deaths",NativeDeaths):null,
            new XAttribute("transactionId", TransactionId.ToString("N")),
            new XAttribute("creationId", CreationId), new XAttribute("actor", ActorId),
            new XAttribute("fingerprint", Fingerprint),
            new XElement("before", BeforePayload), new XElement("after", AfterPayload),
            new XElement("inventory", Items.Select(i => i.ToXml())), Target?.ToXml(), Fuel?.ToXml(), Health?.ToXml(),Custody?.Write(),Training?.Write());
    }

    public static RebirthVehicleTransferPlan Read(XElement element)
    {
        bool legacy = (string)element?.Attribute("version") == "1";
        bool training=(string)element?.Attribute("version")=="8";
        bool custody=training||(string)element?.Attribute("version")=="7";
        bool native = custody||(string)element?.Attribute("version") == "6";
        bool fueled = (string)element?.Attribute("version") == "4"||native&&element.Element("fuel")!=null;
        bool repaired = (string)element?.Attribute("version") == "5"||native&&element.Element("health")!=null;
        bool targeted = native||repaired || fueled || (string)element?.Attribute("version") == "3";
        if (element == null || element.Name != "vehicleTransferPlan"
            || (!legacy && !targeted && (string)element.Attribute("version") != "2")
            || element.Attributes().Count() != (native?6:5) || element.Elements().Count() != (legacy ? 2 : (fueled || repaired) ? 5 : targeted ? 4 : 3)+(custody?1:0)+(training?1:0)
            || element.Elements("before").Count() != 1 || element.Elements("after").Count() != 1
            || element.Elements().Where(e => e.Name != "inventory" && e.Name != "target" && e.Name != "fuel" && e.Name != "health"&&e.Name!="custody"&&e.Name!="training").Any(e => e.HasElements || e.HasAttributes)
            || element.Elements("custody").Count()!=(custody?1:0)
            || element.Elements("training").Count()!=(training?1:0)
            || (repaired && element.Elements("health").Count() != 1)
            || (!repaired && element.Elements("health").Any())
            || (fueled && element.Elements("fuel").Count() != 1)
            || (!fueled && element.Elements("fuel").Any())
            || (targeted && element.Elements("target").Count() != 1)
            || (!targeted && element.Elements("target").Any())
            || (!legacy && (element.Elements("inventory").Count() != 1 || element.Element("inventory").HasAttributes)))
            throw new InvalidDataException("Invalid vehicle transfer plan structure.");
        Guid transaction; string creation=(string)element.Attribute("creationId");int deaths=-1;
        if(native&&(!int.TryParse((string)element.Attribute("deaths"),out deaths)||deaths<0))throw new InvalidDataException("Invalid native vehicle owner life.");
        if (!Guid.TryParse((string)element.Attribute("transactionId"), out transaction)
            || (!(native&&creation==string.Empty)&&!RebirthSurvivorRequestScope.TryNormalize(creation,out creation)))
            throw new InvalidDataException("Invalid vehicle transfer plan identity.");
        var plan=Create(transaction, creation, (string)element.Attribute("actor"),
            (string)element.Attribute("fingerprint"),
            RebirthVehicleAssemblySerializer.FromBase64(element.Element("before").Value),
            RebirthVehicleAssemblySerializer.FromBase64(element.Element("after").Value),
            legacy ? null : element.Element("inventory").Elements().Select(RebirthVehicleTransferItem.Read),
            targeted ? RebirthVehicleTransferTarget.Read(element.Element("target")) : null,
            fueled ? RebirthVehicleTransferFuel.Read(element.Element("fuel")) : null,
            repaired ? RebirthVehicleTransferHealth.Read(element.Element("health")) : null,deaths,custody?RebirthVehicleTransferCustody.Read(element.Element("custody")):null);
        return training?plan.WithTraining(RebirthVehicleTransferTraining.Read(element.Element("training"))):plan;
    }
}
