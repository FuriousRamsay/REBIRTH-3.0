using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace Rebirth.Vehicles.Authoring
{
    // Explicit publication is separate from experimental Save/Export. The same
    // identity always replaces the same file, regardless of the author's filename.
    internal static class SeatPosePublishedStore
    {
        internal static string Key(string vehicle, int seat, string rig)
        {
            if (string.IsNullOrWhiteSpace(vehicle) || string.IsNullOrWhiteSpace(rig) || seat < 0)
                throw new InvalidDataException("Missing published seat identity.");
            // Length prefixes avoid ambiguous delimiters; a digest prevents asset
            // names from becoming filesystem paths. Rig identity remains exact.
            string identity = vehicle.Length.ToString(CultureInfo.InvariantCulture) + ":" + vehicle + ":" + seat.ToString(CultureInfo.InvariantCulture) + ":" + rig.Length.ToString(CultureInfo.InvariantCulture) + ":" + rig;
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").ToLowerInvariant();
        }

        internal static void Publish(string directory, SeatPoseProfile profile)
        {
            profile.Validate();
            SeatPoseProfileStore.Save(directory, Key(profile.vehicle, profile.seat, profile.rig), profile);
        }

        internal static SeatPoseProfile Load(string directory, string vehicle, int seat, string rig)
        {
            string path = Path.Combine(directory, Key(vehicle, seat, rig) + ".json");
            if (!File.Exists(path)) return null;
            var profile = SeatPoseProfileStore.Load(path);
            if (!string.Equals(profile.vehicle, vehicle, StringComparison.Ordinal) || profile.seat != seat ||
                !string.Equals(profile.rig, rig, StringComparison.Ordinal))
                throw new InvalidDataException("Published profile identity does not match its binding.");
            return profile;
        }
        internal static void Remove(string directory, SeatPoseProfile profile)
        {
            profile.Validate();
            string path = Path.Combine(directory, Key(profile.vehicle, profile.seat, profile.rig) + ".json");
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
