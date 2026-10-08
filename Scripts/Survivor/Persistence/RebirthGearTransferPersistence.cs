using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

public static class RebirthGearTransferPersistence
{
    public static XElement Write(long revision, RebirthGearTransferState pending,
        RebirthGearTransferPhase phase = RebirthGearTransferPhase.Prepared)
    {
        if (!Valid(revision, pending, phase)) throw new ArgumentException("Gear transfer stage/revision mismatch.");
        return new XElement("gearTransfers", new XAttribute("revision", revision), new XAttribute("phase", (int)phase),
            pending == null ? null : pending.ToXml());
    }

    // Prepared-only compatibility helper. Never silently discard a persisted stage.
    public static bool TryRead(XElement node, out long revision, out RebirthGearTransferState pending)
    {
        RebirthGearTransferPhase phase;
        if (!TryRead(node, out revision, out pending, out phase)) return false;
        if (phase == RebirthGearTransferPhase.Prepared) return true;
        pending = null; return false;
    }

    public static bool TryRead(XElement node, out long revision, out RebirthGearTransferState pending,
        out RebirthGearTransferPhase phase)
    {
        revision = 0; pending = null; phase = RebirthGearTransferPhase.Prepared;
        if (node == null || node.Name != "gearTransfers"
            || node.Attributes().Any(a => a.Name != "revision" && a.Name != "phase")
            || !long.TryParse((string)node.Attribute("revision"), NumberStyles.Integer, CultureInfo.InvariantCulture, out revision)
            || revision < 0 || node.Elements().Count() > 1
            || node.Nodes().Any(n => !(n is XElement) && (!(n is XText) || !string.IsNullOrWhiteSpace(((XText)n).Value)))) return false;
        int stage = 0;
        if (node.Attribute("phase") != null && !int.TryParse((string)node.Attribute("phase"), NumberStyles.Integer, CultureInfo.InvariantCulture, out stage)) return false;
        phase = (RebirthGearTransferPhase)stage;
        XElement entry = node.Elements().FirstOrDefault();
        RebirthGearTransferState candidate = null;
        if (entry != null && !RebirthGearTransferState.TryRead(entry, out candidate)) return false;
        if (!Valid(revision, candidate, phase)) return false;
        pending = candidate;
        return true;
    }

    private static bool Valid(long revision, RebirthGearTransferState pending, RebirthGearTransferPhase phase)
    {
        if (revision < 0 || phase < RebirthGearTransferPhase.Prepared || phase > RebirthGearTransferPhase.GearCommitted) return false;
        if (pending == null) return phase == RebirthGearTransferPhase.Prepared;
        if(pending.HasRecoveryAttempts&&phase!=RebirthGearTransferPhase.GearCommitted)return false;
        return revision == pending.ExpectedRevision + (phase == RebirthGearTransferPhase.GearCommitted ? 1 : 0);
    }
}
