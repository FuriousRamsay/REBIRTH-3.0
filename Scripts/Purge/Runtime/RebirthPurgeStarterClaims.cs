using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal enum RebirthPurgeStarterStage { WaitingForSite, Prepared, PublicationAttempted, Published, Completed }
internal enum RebirthPurgeStarterRead { Missing, Found, Refused }
internal enum RebirthPurgeStarterAdmission { NormalSpawn, FreshClaim, ResumeClaim }
internal enum RebirthPurgeStarterPublication { None, FindSite, Create, AwaitOriginal, UseOriginal }

[JsonObject(MemberSerialization.Fields)]
internal sealed class RebirthPurgeStarterClaim
{
    public int Version = 1;
    public Guid World, Claim, Assembly;
    public string Owner, LegacyOriginal;
    public RebirthPurgeStarterStage Stage;
    public int Vehicle;
    public float X, Y, Z, Heading;
    public float AnchorX, AnchorY, AnchorZ, AnchorHeading;
    public bool HasAnchor;

    internal RebirthPurgeStarterClaim Copy() => (RebirthPurgeStarterClaim)MemberwiseClone();

    internal static RebirthPurgeStarterAdmission Admission(RebirthPurgeStarterRead read, RebirthPurgeStarterClaim claim,
        bool initialSpawn, bool firstNativeLife)
    {
        if (!initialSpawn || read == RebirthPurgeStarterRead.Refused) return RebirthPurgeStarterAdmission.NormalSpawn;
        if (read == RebirthPurgeStarterRead.Missing)
            return firstNativeLife ? RebirthPurgeStarterAdmission.FreshClaim : RebirthPurgeStarterAdmission.NormalSpawn;
        return claim != null && claim.Stage != RebirthPurgeStarterStage.Completed
            ? RebirthPurgeStarterAdmission.ResumeClaim : RebirthPurgeStarterAdmission.NormalSpawn;
    }

    internal RebirthPurgeStarterPublication Publication(bool exactNativeOriginal)
    {
        if (Stage == RebirthPurgeStarterStage.Completed) return RebirthPurgeStarterPublication.None;
        if (Stage == RebirthPurgeStarterStage.WaitingForSite) return RebirthPurgeStarterPublication.FindSite;
        if (Stage == RebirthPurgeStarterStage.Prepared) return RebirthPurgeStarterPublication.Create;
        return exactNativeOriginal ? RebirthPurgeStarterPublication.UseOriginal : RebirthPurgeStarterPublication.AwaitOriginal;
    }
}

// One claim per exact native principal in this save. Prepared and attempted
// are distinct because an interrupted native spawn is never proof of absence.
internal sealed class RebirthPurgeStarterClaims
{
    private const int MaximumBytes = 4096;
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        MissingMemberHandling = MissingMemberHandling.Error,
        DateParseHandling = DateParseHandling.None
    };
    private readonly Guid world;
    private readonly string directory, owners, legacy;

    internal RebirthPurgeStarterClaims(string saveDirectory, Guid world)
    {
        if (string.IsNullOrWhiteSpace(saveDirectory) || world == Guid.Empty)
            throw new ArgumentException("Current native starter save required.");
        this.world = world;
        directory = Path.Combine(Path.GetFullPath(saveDirectory), "REBIRTH", "PurgeRoadStart");
        owners = Path.Combine(directory, "owners");
        legacy = Path.Combine(directory, "initial-truck.json");
    }

    internal string OwnerPath(string owner)
    {
        if (!ValidOwner(owner)) throw new ArgumentException("Invalid native starter owner.");
        return Path.Combine(owners, Digest(Utf8.GetBytes(owner)) + ".json");
    }

    internal RebirthPurgeStarterRead Read(string owner, out RebirthPurgeStarterClaim claim)
    {
        claim = null;
        try
        {
            string path = OwnerPath(owner);
            var imported = ReadLegacy(owner);
            var current = ReadClaim(path, owner);
            if (imported != null)
            {
                if (current == null)
                {
                    Commit(path, owner, null, imported);
                    current = ReadClaim(path, owner);
                }
                if (!MatchesImport(current, imported)) return RebirthPurgeStarterRead.Refused;
            }
            else if (current?.LegacyOriginal != null) return RebirthPurgeStarterRead.Refused;
            if (current == null) return RebirthPurgeStarterRead.Missing;
            claim = current;
            return RebirthPurgeStarterRead.Found;
        }
        catch { return RebirthPurgeStarterRead.Refused; }
    }

    internal bool Publish(RebirthPurgeStarterClaim expected, RebirthPurgeStarterClaim next)
    {
        try
        {
            if (next == null || !Valid(next, next.Owner)) return false;
            var read = Read(next.Owner, out var current);
            if (read == RebirthPurgeStarterRead.Refused || !Same(current, expected) || !Successor(expected, next)) return false;
            if (Same(expected, next)) return true;
            return Commit(OwnerPath(next.Owner), next.Owner, expected, next);
        }
        catch { return false; }
    }

    private bool Commit(string path, string owner, RebirthPurgeStarterClaim expected, RebirthPurgeStarterClaim next)
    {
        string leasePath = path + ".lock";
        SafePath(leasePath);
        Directory.CreateDirectory(owners);
        SafePath(leasePath);
        using (var lease = new FileStream(leasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            if (!Same(ReadClaim(path, owner), expected)) return false;
            byte[] bytes = Utf8.GetBytes(Document(next).ToString(Formatting.None));
            if (bytes.Length > MaximumBytes) return false;
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    output.Write(bytes, 0, bytes.Length);
                    output.Flush(true);
                }
                SafePath(path);
                if (expected == null) File.Move(temporary, path);
                else File.Replace(temporary, path, null);
                return Same(ReadClaim(path, owner), next);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    private RebirthPurgeStarterClaim ReadClaim(string path, string owner)
    {
        var document = ReadDocument(path, out _);
        if (document == null) return null;
        var claim = document.ToObject<RebirthPurgeStarterClaim>(JsonSerializer.Create(JsonSettings));
        if (!Valid(claim, owner)) throw new InvalidDataException("Invalid scoped starter claim.");
        var canonical = Document(claim);
        if (document.Count != canonical.Count) throw new InvalidDataException("Incomplete starter claim.");
        foreach (var field in canonical.Properties())
            if (document.Property(field.Name) == null) throw new InvalidDataException("Missing starter claim field.");
        return claim;
    }

    private RebirthPurgeStarterClaim ReadLegacy(string owner)
    {
        var document = ReadDocument(legacy, out var bytes);
        if (document == null) return null;
        string legacyOwner = (string)document["Owner"];
        if (!Guid.TryParseExact((string)document["World"], "N", out var nativeWorld) || nativeWorld != world || !ValidOwner(legacyOwner))
            throw new InvalidDataException("Invalid legacy starter scope.");
        if (!string.Equals(legacyOwner, owner, StringComparison.Ordinal)) return null;

        int stage = RequiredInteger(document, "Stage"), vehicle = RequiredInteger(document, "Vehicle");
        if (stage < 0 || stage > 2 || vehicle < 0 || stage != 0 && vehicle == 0)
            throw new InvalidDataException("Invalid legacy starter stage.");
        string fingerprint = Digest(bytes);
        var claim = new RebirthPurgeStarterClaim
        {
            World = world, Owner = owner, Claim = LegacyClaim(bytes), LegacyOriginal = fingerprint, Vehicle = vehicle,
            Stage = stage == 2 ? RebirthPurgeStarterStage.Completed : stage == 1 ? RebirthPurgeStarterStage.Published
                : vehicle > 0 ? RebirthPurgeStarterStage.PublicationAttempted : RebirthPurgeStarterStage.Prepared,
            X = Required<float>(document, "X"), Y = Required<float>(document, "Y"), Z = Required<float>(document, "Z"),
            Heading = Required<float>(document, "Heading")
        };
        if (!Valid(claim, owner)) throw new InvalidDataException("Invalid legacy starter site.");
        return claim;
    }

    private static int RequiredInteger(JObject document, string name)
    {
        if (document[name]?.Type != JTokenType.Integer) throw new InvalidDataException("Invalid legacy starter integer.");
        return document[name].Value<int>();
    }

    private static T Required<T>(JObject document, string name)
    {
        var value = document[name];
        if (value == null || value.Type != JTokenType.Integer && value.Type != JTokenType.Float)
            throw new InvalidDataException("Invalid legacy starter field.");
        return value.Value<T>();
    }

    private static JObject ReadDocument(string path, out byte[] bytes)
    {
        bytes = null;
        SafePath(path);
        try
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                if (file.Length < 1 || file.Length > MaximumBytes) throw new InvalidDataException("Starter record size refused.");
                bytes = new byte[(int)file.Length];
                int count = 0;
                while (count < bytes.Length)
                {
                    int read = file.Read(bytes, count, bytes.Length - count);
                    if (read == 0) throw new EndOfStreamException();
                    count += read;
                }
                if (file.ReadByte() != -1) throw new InvalidDataException("Starter record changed while reading.");
            }
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        int offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
        using (var reader = new JsonTextReader(new StringReader(Utf8.GetString(bytes, offset, bytes.Length - offset))) { MaxDepth = 4, DateParseHandling = DateParseHandling.None })
        {
            var result = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new InvalidDataException("Trailing starter record data.");
            return result;
        }
    }

    private static void SafePath(string path)
    {
        for (string entry = path; !string.IsNullOrEmpty(entry); entry = Path.GetDirectoryName(entry))
        {
            try
            {
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Starter path cannot traverse a reparse point.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private bool Valid(RebirthPurgeStarterClaim claim, string owner)
    {
        if (claim == null || claim.Version != 1 || claim.World != world || claim.Claim == Guid.Empty ||
            !ValidOwner(owner) || !string.Equals(claim.Owner, owner, StringComparison.Ordinal) ||
            !Enum.IsDefined(typeof(RebirthPurgeStarterStage), claim.Stage) ||
            !Site(claim.X, claim.Y, claim.Z, claim.Heading) ||
            claim.HasAnchor && !Site(claim.AnchorX, claim.AnchorY, claim.AnchorZ, claim.AnchorHeading) ||
            claim.LegacyOriginal != null && (claim.LegacyOriginal.Length != 64 || claim.LegacyOriginal.Trim("0123456789abcdef".ToCharArray()).Length != 0)) return false;
        if (claim.Stage == RebirthPurgeStarterStage.WaitingForSite)
            return claim.LegacyOriginal == null && claim.Vehicle == 0 && claim.Assembly == Guid.Empty &&
                !claim.HasAnchor && claim.X == 0 && claim.Y == 0 && claim.Z == 0 && claim.Heading == 0 &&
                claim.AnchorX == 0 && claim.AnchorY == 0 && claim.AnchorZ == 0 && claim.AnchorHeading == 0;
        if (!claim.HasAnchor && claim.LegacyOriginal == null) return false;
        if (claim.Stage == RebirthPurgeStarterStage.Prepared) return claim.Vehicle == 0 && claim.Assembly == Guid.Empty;
        return claim.Vehicle > 0 && (claim.Assembly != Guid.Empty || claim.LegacyOriginal != null);
    }

    private static bool ValidOwner(string owner)
    {
        if (string.IsNullOrWhiteSpace(owner) || owner.Length > 256) return false;
        foreach (char value in owner) if (char.IsControl(value)) return false;
        return true;
    }

    private static bool Site(float x, float y, float z, float heading)
        => Math.Abs(x) <= 1000000 && y >= -4096 && y <= 4096 && Math.Abs(z) <= 1000000 && Math.Abs(heading) <= 3600;

    private static JObject Document(RebirthPurgeStarterClaim claim)
        => JObject.FromObject(claim, JsonSerializer.Create(JsonSettings));

    private static bool Same(RebirthPurgeStarterClaim left, RebirthPurgeStarterClaim right)
        => left == null || right == null ? left == right : JToken.DeepEquals(Document(left), Document(right));

    private static bool MatchesImport(RebirthPurgeStarterClaim current, RebirthPurgeStarterClaim original)
    {
        if (current == null || current.Claim != original.Claim || current.LegacyOriginal != original.LegacyOriginal ||
            current.Stage < original.Stage) return false;
        if (original.Stage == RebirthPurgeStarterStage.Completed) return Same(current, original);
        return original.Vehicle == 0 || current.Vehicle == original.Vehicle && SameSite(current, original);
    }

    private static bool Successor(RebirthPurgeStarterClaim previous, RebirthPurgeStarterClaim next)
    {
        if (previous == null) return next.Stage <= RebirthPurgeStarterStage.Prepared && next.LegacyOriginal == null;
        if (next.World != previous.World || next.Owner != previous.Owner || next.Claim != previous.Claim ||
            next.LegacyOriginal != previous.LegacyOriginal || next.Stage < previous.Stage ||
            (int)next.Stage > (int)previous.Stage + 1) return false;
        if (previous.Stage == RebirthPurgeStarterStage.WaitingForSite) return next.Stage == RebirthPurgeStarterStage.Prepared;
        if (previous.Stage == RebirthPurgeStarterStage.Prepared)
            return next.Stage == previous.Stage ? !SameSite(previous, next) : next.HasAnchor && next.Assembly != Guid.Empty && SameSite(previous, next);
        if (next.Stage == previous.Stage) return false;
        return next.Vehicle == previous.Vehicle && (previous.Assembly == Guid.Empty || next.Assembly == previous.Assembly) &&
            SameSite(previous, next);
    }

    private static bool SameSite(RebirthPurgeStarterClaim left, RebirthPurgeStarterClaim right)
        => left.X == right.X && left.Y == right.Y && left.Z == right.Z && left.Heading == right.Heading &&
            left.HasAnchor == right.HasAnchor && left.AnchorX == right.AnchorX && left.AnchorY == right.AnchorY &&
            left.AnchorZ == right.AnchorZ && left.AnchorHeading == right.AnchorHeading;

    private static string Digest(byte[] bytes)
    {
        using (var hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    private static Guid LegacyClaim(byte[] bytes)
    {
        using (var hash = SHA256.Create())
        {
            byte[] full = hash.ComputeHash(bytes), prefix = new byte[16];
            Buffer.BlockCopy(full, 0, prefix, 0, prefix.Length);
            return new Guid(prefix);
        }
    }
}
