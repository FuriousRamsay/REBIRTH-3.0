using System;
using System.Collections.Generic;
using System.Xml.Linq;

#nullable disable

/// <summary>
/// Deterministic, file-system-free vectors for the HUD tracking foundation. These can be invoked
/// from a future debug/acceptance command after compilation; they intentionally do not require a
/// live player, world, network connection, or XUi instance.
/// </summary>
public static class RebirthHudTrackingFoundationVectorHarness
{
    public static IList<string> Run()
    {
        List<string> errors = new List<string>();
        TestOrdering(errors);
        TestUnknownTypeRoundTrip(errors);
        TestDuplicateRecovery(errors);
        TestFutureSchemaRefusal(errors);
        return errors;
    }

    private static void TestOrdering(List<string> errors)
    {
        RebirthHudTrackingPreferences p = new RebirthHudTrackingPreferences();
        p.Add(RebirthHudTrackType.Skill, "skill.cooking");
        p.Add(RebirthHudTrackType.Attribute, "strength");
        p.Add(RebirthHudTrackType.Knowledge, "knowledge.electrical.fundamentals");
        if (!p.Move(RebirthHudTrackType.Knowledge, "knowledge.electrical.fundamentals", -2))
            errors.Add("ordering: move failed");
        if (p.Entries.Count != 3 || p.Entries[0].Type != RebirthHudTrackType.Knowledge || p.Entries[0].Order != 0 || p.Entries[2].Order != 2)
            errors.Add("ordering: normalized order mismatch");
        if (!p.Remove(RebirthHudTrackType.Attribute, "strength") || p.Entries.Count != 2 || p.Entries[1].Order != 1)
            errors.Add("ordering: remove/reindex mismatch");
    }

    private static void TestUnknownTypeRoundTrip(List<string> errors)
    {
        XDocument input = XDocument.Parse(
            "<rebirthHudTracking schema_version='1' enabled='true' show_contextual_active_skill='false'>" +
            "<entry type='skill' id='skill.cooking' order='0' enabled='true'/>" +
            "<entry type='future_type' id='future.example' order='1' enabled='false'/>" +
            "</rebirthHudTracking>");
        RebirthHudTrackingPreferences parsed;
        string error;
        if (!RebirthHudTrackingPreferenceStore.TryParse(input, out parsed, out error))
        {
            errors.Add("unknown-roundtrip: parse failed: " + error);
            return;
        }
        if (parsed.Entries.Count != 1 || parsed.UnknownEntries.Count != 1)
            errors.Add("unknown-roundtrip: unknown type was not separated/preserved");
        XDocument output = RebirthHudTrackingPreferenceStore.Serialize(parsed);
        string xml = output.ToString(SaveOptions.DisableFormatting);
        if (xml.IndexOf("future_type", StringComparison.Ordinal) < 0 || xml.IndexOf("future.example", StringComparison.Ordinal) < 0)
            errors.Add("unknown-roundtrip: unknown entry was not reserialized");
    }

    private static void TestDuplicateRecovery(List<string> errors)
    {
        XDocument input = XDocument.Parse(
            "<rebirthHudTracking schema_version='1'>" +
            "<entry type='skill' id='skill.cooking' order='0' enabled='true'/>" +
            "<entry type='SKILL' id='SKILL.COOKING' order='1' enabled='false'/>" +
            "</rebirthHudTracking>");
        RebirthHudTrackingPreferences parsed;
        string error;
        if (!RebirthHudTrackingPreferenceStore.TryParse(input, out parsed, out error) || parsed.Entries.Count != 1)
            errors.Add("duplicate-recovery: duplicate typed ID was not collapsed");
    }

    private static void TestFutureSchemaRefusal(List<string> errors)
    {
        XDocument input = XDocument.Parse("<rebirthHudTracking schema_version='999'/>");
        RebirthHudTrackingPreferences parsed;
        string error;
        if (RebirthHudTrackingPreferenceStore.TryParse(input, out parsed, out error))
            errors.Add("future-schema: newer schema was accepted instead of protected from overwrite");
    }
}
