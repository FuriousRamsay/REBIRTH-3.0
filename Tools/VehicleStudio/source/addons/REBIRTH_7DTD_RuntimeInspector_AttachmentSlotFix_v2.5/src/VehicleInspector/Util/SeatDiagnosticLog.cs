using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace RebirthVehicleInspector.Util
{
    /// <summary>
    /// A seatdiag report is written to its own text file AND copied to the
    /// standard 7DTD log. No F1 console copy/paste is required. Only the
    /// bounded, safe primitive/member-name diagnostics from SeatMemberProbe
    /// may be passed here; arbitrary object graphs are never serialized.
    /// </summary>
    internal static class SeatDiagnosticLog
    {
        internal const string Signature = "REBIRTH Vehicle Inspector seat diagnostics v2.5";
        private const int MaxBodyCharacters = 65536;
        private const int MaxGameLogLineCharacters = 1800;
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        internal static string Write(string exportFolder, string trigger, int selectedVehicleId,
            string boundVehicleXml, string text)
        {
            if (String.IsNullOrEmpty(exportFolder))
                throw new ArgumentException("Missing exports directory.", nameof(exportFolder));
            if (text == null) text = "(no seat diagnostic data)";
            bool truncated = text.Length > MaxBodyCharacters;
            if (truncated) text = text.Substring(0, MaxBodyCharacters);
            // Use an independent file rather than appending to one long-lived log:
            // previous diagnostic evidence is not overwritten on subsequent tests.
            string folder = Path.Combine(exportFolder, "Diagnostics");
            Directory.CreateDirectory(folder);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            string id = selectedVehicleId >= 0 ? selectedVehicleId.ToString(CultureInfo.InvariantCulture) : "unselected";
            string name = "seatdiag_" + timestamp + "_" + id + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".txt";
            string target = Path.Combine(folder, name);
            string temp = target + ".tmp";
            var body = new StringBuilder();
            body.AppendLine(Signature);
            body.Append("capturedUtc=").AppendLine(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            body.Append("trigger=").AppendLine(trigger ?? "unknown");
            body.Append("vehicleEntityId=").AppendLine(id);
            body.Append("boundVehicleXml=").AppendLine(boundVehicleXml ?? "UNBOUND");
            body.AppendLine("seatIndexVerification=requires explicit game slot evidence; -1 is unknown");
            body.AppendLine("----- SEAT DIAGNOSTIC OUTPUT -----");
            body.AppendLine(text);
            if (truncated) body.AppendLine("WARNING: diagnostic content was truncated at 65536 characters");
            body.AppendLine("----- END SEAT DIAGNOSTICS -----");
            string report = body.ToString();
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, Utf8))
                {
                    writer.Write(report);
                    writer.Flush();
                    stream.Flush(true);
                }
                File.Move(temp, target);
            }
            catch
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                throw;
            }
            // Also write the content to the game's ordinary log. File creation
            // above must succeed before claiming that a report was saved.
            Log.Out("[VehicleInspector][seatdiag] Saved " + target);
            foreach (string raw in report.Replace("\r\n", "\n").Split('\n'))
            {
                if (raw.Length == 0) continue;
                for (int offset = 0; offset < raw.Length; offset += MaxGameLogLineCharacters)
                {
                    int count = Math.Min(MaxGameLogLineCharacters, raw.Length - offset);
                    Log.Out("[VehicleInspector][seatdiag] " + raw.Substring(offset, count));
                }
            }
            return target;
        }
    }
}
