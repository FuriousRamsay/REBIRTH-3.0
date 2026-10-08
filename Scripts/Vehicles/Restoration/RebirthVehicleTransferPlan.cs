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
    public string ActorId { get; private set; }
    public string Fingerprint { get; private set; }
    public string BeforePayload { get; private set; }
    public string AfterPayload { get; private set; }
    public RebirthVehicleTransferTarget Target { get; private set; }
    public RebirthVehicleTransferFuel Fuel { get; private set; }
    public RebirthVehicleTransferHealth Health { get; private set; }

    public IReadOnlyList<RebirthVehicleTransferItem> Items { get; private set; }

    private RebirthVehicleTransferPlan() { }

    public static RebirthVehicleTransferPlan Create(Guid transactionId, Guid creationId,
        string actorId, string fingerprint, RebirthVehicleAssembly before, RebirthVehicleAssembly after,
        IEnumerable<RebirthVehicleTransferItem> items = null, RebirthVehicleTransferTarget target = null, RebirthVehicleTransferFuel fuel = null, RebirthVehicleTransferHealth health = null)
        =>Create(transactionId,creationId.ToString("N"),actorId,fingerprint,before,after,items,target,fuel,health);
    public static RebirthVehicleTransferPlan Create(Guid transactionId, string creationId,
        string actorId, string fingerprint, RebirthVehicleAssembly before, RebirthVehicleAssembly after,
        IEnumerable<RebirthVehicleTransferItem> items = null, RebirthVehicleTransferTarget target = null, RebirthVehicleTransferFuel fuel = null, RebirthVehicleTransferHealth health = null)
    {
        if (transactionId == Guid.Empty || !RebirthSurvivorRequestScope.TryNormalize(creationId,out var normalizedCreation)
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
            TransactionId = transactionId, CreationId = normalizedCreation, ActorId = actorId,
            Fingerprint = fingerprint, Target = target, Fuel = fuel, Health = health,
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

    public XElement ToXml()
    {
        return new XElement("vehicleTransferPlan", new XAttribute("version", Health != null ? 5 : Fuel != null ? 4 : Target == null ? 2 : 3),
            new XAttribute("transactionId", TransactionId.ToString("N")),
            new XAttribute("creationId", CreationId), new XAttribute("actor", ActorId),
            new XAttribute("fingerprint", Fingerprint),
            new XElement("before", BeforePayload), new XElement("after", AfterPayload),
            new XElement("inventory", Items.Select(i => i.ToXml())), Target?.ToXml(), Fuel?.ToXml(), Health?.ToXml());
    }

    public static RebirthVehicleTransferPlan Read(XElement element)
    {
        bool legacy = (string)element?.Attribute("version") == "1";
        bool fueled = (string)element?.Attribute("version") == "4";
        bool repaired = (string)element?.Attribute("version") == "5";
        bool targeted = repaired || fueled || (string)element?.Attribute("version") == "3";
        if (element == null || element.Name != "vehicleTransferPlan"
            || (!legacy && !targeted && (string)element.Attribute("version") != "2")
            || element.Attributes().Count() != 5 || element.Elements().Count() != (legacy ? 2 : (fueled || repaired) ? 5 : targeted ? 4 : 3)
            || element.Elements("before").Count() != 1 || element.Elements("after").Count() != 1
            || element.Elements().Where(e => e.Name != "inventory" && e.Name != "target" && e.Name != "fuel" && e.Name != "health").Any(e => e.HasElements || e.HasAttributes)
            || (repaired && element.Elements("health").Count() != 1)
            || (!repaired && element.Elements("health").Any())
            || (fueled && element.Elements("fuel").Count() != 1)
            || (!fueled && element.Elements("fuel").Any())
            || (targeted && element.Elements("target").Count() != 1)
            || (!targeted && element.Elements("target").Any())
            || (!legacy && (element.Elements("inventory").Count() != 1 || element.Element("inventory").HasAttributes)))
            throw new InvalidDataException("Invalid vehicle transfer plan structure.");
        Guid transaction; string creation;
        if (!Guid.TryParse((string)element.Attribute("transactionId"), out transaction)
            || !RebirthSurvivorRequestScope.TryNormalize((string)element.Attribute("creationId"), out creation))
            throw new InvalidDataException("Invalid vehicle transfer plan identity.");
        return Create(transaction, creation, (string)element.Attribute("actor"),
            (string)element.Attribute("fingerprint"),
            RebirthVehicleAssemblySerializer.FromBase64(element.Element("before").Value),
            RebirthVehicleAssemblySerializer.FromBase64(element.Element("after").Value),
            legacy ? null : element.Element("inventory").Elements().Select(RebirthVehicleTransferItem.Read),
            targeted ? RebirthVehicleTransferTarget.Read(element.Element("target")) : null,
            fueled ? RebirthVehicleTransferFuel.Read(element.Element("fuel")) : null,
            repaired ? RebirthVehicleTransferHealth.Read(element.Element("health")) : null);
    }
}
