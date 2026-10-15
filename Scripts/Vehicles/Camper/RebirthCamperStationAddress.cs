using System;

// Stable custody address. Position is an observation, never a mobile station id.
public struct RebirthCamperStationAddress : IEquatable<RebirthCamperStationAddress>
{
    public Guid Vehicle { get; }
    public Guid Installation { get; }
    public RebirthCamperStationAddress(Guid vehicle, Guid installation)
    {
        if (vehicle == Guid.Empty || installation == Guid.Empty)
            throw new ArgumentException("Vehicle and installation identities are required.");
        Vehicle = vehicle;
        Installation = installation;
    }
    public bool IsValid => Vehicle != Guid.Empty && Installation != Guid.Empty;
    public override string ToString()
    {
        if (!IsValid) throw new InvalidOperationException("An empty station address cannot be persisted.");
        return "camper:" + Vehicle.ToString("N") + ":" + Installation.ToString("N");
    }
    public static bool TryParse(string encoded, out RebirthCamperStationAddress address)
    {
        address = default(RebirthCamperStationAddress);
        Guid vehicle, installation;
        if (encoded == null || encoded.Length != 72 || !encoded.StartsWith("camper:", StringComparison.Ordinal)
            || encoded[39] != ':' || !Guid.TryParseExact(encoded.Substring(7,32), "N", out vehicle)
            || !Guid.TryParseExact(encoded.Substring(40,32), "N", out installation)
            || vehicle == Guid.Empty || installation == Guid.Empty) return false;
        var candidate = new RebirthCamperStationAddress(vehicle, installation);
        if (!string.Equals(candidate.ToString(), encoded, StringComparison.Ordinal)) return false;
        address = candidate;
        return true;
    }
    public bool Equals(RebirthCamperStationAddress other) => Vehicle == other.Vehicle && Installation == other.Installation;
    public override bool Equals(object other) => other is RebirthCamperStationAddress && Equals((RebirthCamperStationAddress)other);
    public override int GetHashCode() => Vehicle.GetHashCode() ^ Installation.GetHashCode();
}
