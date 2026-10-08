using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// BCL-only workstation protocol/credential seam. Public metadata never contains
/// a credential or verifier. The transport still owns sender authentication and
/// confidentiality; a public epoch is freshness data, NOT an authentication key.
/// </summary>
internal static class RebirthWorkstationCredentials
{
    internal const string Protocol = "RBWS2";
    internal const string ResetRequired = "RESET_REQUIRED";
    private const int Iterations = 100000; // Persisted, versioned work factor; not an FPS/security certification.
    private const string VerifierPrefix = "P2$100000$";

    internal static string ClientCredential(string password)
    {
        if (string.IsNullOrEmpty(password)) return string.Empty;
        using (SHA256 sha = SHA256.Create())
            return "C2:" + Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(password)));
    }

    internal static bool ValidCredential(string value, bool allowEmpty)
    {
        if (string.IsNullOrEmpty(value)) return allowEmpty;
        if (value.Length != 47 || !value.StartsWith("C2:", StringComparison.Ordinal)) return false;
        try { return Convert.FromBase64String(value.Substring(3)).Length == 32; }
        catch (FormatException) { return false; }
    }

    internal static bool IsCurrentVerifier(string record)
    {
        if (record == null || !record.StartsWith(VerifierPrefix, StringComparison.Ordinal)) return false;
        string[] fields = record.Split('$');
        if (fields.Length != 4) return false;
        try { return Convert.FromBase64String(fields[2]).Length == 16 && Convert.FromBase64String(fields[3]).Length == 32; }
        catch (FormatException) { return false; }
    }

    internal static string CreateVerifier(string credential)
    {
        if (!ValidCredential(credential, true)) throw new ArgumentException("Unsupported credential format.", "credential");
        if (string.IsNullOrEmpty(credential)) return string.Empty;
        byte[] salt = new byte[16];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
        // This overload deliberately uses the framework's PBKDF2-HMAC-SHA1,
        // not a custom KDF or a newer overload absent from older game targets.
        using (Rfc2898DeriveBytes derive = new Rfc2898DeriveBytes(credential, salt, Iterations))
            return VerifierPrefix + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(derive.GetBytes(32));
    }

    internal static bool Verify(string record, string credential)
    {
        if (!ValidCredential(credential, false) || !IsCurrentVerifier(record)) return false;
        string[] fields = record.Split('$');
        byte[] expected = Convert.FromBase64String(fields[3]);
        byte[] actual;
        using (Rfc2898DeriveBytes derive = new Rfc2898DeriveBytes(credential, Convert.FromBase64String(fields[2]), Iterations))
            actual = derive.GetBytes(expected.Length);
        int difference = 0;
        for (int i = 0; i < actual.Length; i++) difference |= actual[i] ^ expected[i];
        return difference == 0;
    }

    internal static bool IsToken(string value)
    {
        Guid parsed;
        return value != null && value.Length == 32 && Guid.TryParseExact(value, "N", out parsed);
    }

    internal static string Request(string epoch, string requestId, byte action, string credential)
    {
        return Protocol + "|" + epoch + "|" + requestId + "|" + action.ToString(CultureInfo.InvariantCulture) + "|" + (credential ?? string.Empty);
    }

    internal static bool TryReadRequest(string wire, byte action, out string epoch, out string requestId, out string credential)
    {
        epoch = requestId = credential = string.Empty;
        if (wire == null || wire.Length > 160) return false;
        string[] fields = wire.Split('|');
        byte encodedAction;
        if (fields.Length != 5 || fields[0] != Protocol || !IsToken(fields[1]) || !IsToken(fields[2]) ||
            !byte.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out encodedAction) || encodedAction != action ||
            action < 1 || action > 4 || !ValidCredential(fields[4], action != 4)) return false;
        if (action <= 2 && fields[4].Length != 0) return false;
        epoch = fields[1]; requestId = fields[2]; credential = fields[4];
        return true;
    }

    internal static string PublicState(string epoch, bool hasPassword, bool resetRequired, string replyId, int result)
    {
        return Protocol + "|" + epoch + "|" + (hasPassword ? "1" : "0") + "|" + (resetRequired ? "1" : "0") + "|" +
            (replyId ?? string.Empty) + "|" + result.ToString(CultureInfo.InvariantCulture);
    }

    internal static bool TryReadPublicState(string wire, out string epoch, out bool hasPassword, out bool resetRequired, out string replyId, out int result)
    {
        epoch = replyId = string.Empty; hasPassword = resetRequired = false; result = 0;
        if (wire == null || wire.Length > 128) return false;
        string[] fields = wire.Split('|');
        if (fields.Length != 6 || fields[0] != Protocol || !IsToken(fields[1]) ||
            (fields[2] != "0" && fields[2] != "1") || (fields[3] != "0" && fields[3] != "1") ||
            (fields[4].Length != 0 && !IsToken(fields[4])) ||
            !int.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out result) || result < 0 || result > 6) return false;
        epoch = fields[1]; hasPassword = fields[2] == "1"; resetRequired = fields[3] == "1"; replyId = fields[4];
        return true;
    }

    /// <summary>Caller holds its authority lock. Bounded authenticated-user cooldown and replay ledger.</summary>
    internal sealed class Attempts
    {
        private sealed class Entry
        {
            internal double Next, SeenAt;
            internal readonly Queue<string> Ids = new Queue<string>();
            internal readonly HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        }
        private readonly Dictionary<string, Entry> users = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private double nextCredentialWork;
        internal void Clear() { users.Clear(); nextCredentialWork = 0; }
        internal bool Begin(string user, string id, double now, bool expensive)
        {
            if (string.IsNullOrEmpty(user) || !IsToken(id)) return false;
            Entry entry;
            if (!users.TryGetValue(user, out entry))
            {
                if (users.Count >= 1024)
                {
                    var stale = new List<string>();
                    foreach (var pair in users) if (now - pair.Value.SeenAt > 600) stale.Add(pair.Key);
                    foreach (string key in stale) users.Remove(key);
                    if (users.Count >= 1024) return false;
                }
                users[user] = entry = new Entry();
            }
            entry.SeenAt = now;
            if (entry.Seen.Contains(id) || now < entry.Next || (expensive && now < nextCredentialWork)) return false;
            entry.Next = now + (expensive ? 2.0 : 0.1);
            if (expensive) nextCredentialWork = now + 0.25;
            entry.Seen.Add(id); entry.Ids.Enqueue(id);
            while (entry.Ids.Count > 64) entry.Seen.Remove(entry.Ids.Dequeue());
            return true;
        }
    }
}
