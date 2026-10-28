// Journal/correlation authority is explicitly doubled in record-only fixtures.
// This adapter cannot prove relocation authorization or owner-file persistence.
internal sealed class RebirthCamperStationRelocationCorrelation
{
    internal RebirthStationGridAdmission OriginalAdmission=null;
    internal RebirthCamperStationPublicationRecord OriginalPublication=null;
    internal RebirthCamperStationSaveEvidence.Publication DestinationProof=null;
    internal RebirthCamperStationAddress Destination = default;
    internal Guid RemovalOperation = Guid.Empty, InstallationOperation = Guid.Empty;
    internal string RemovalAfterHash = null, InstallationAfterHash = null;
}
