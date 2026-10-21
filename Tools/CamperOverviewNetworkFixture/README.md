# Camper Overview reply-order simulation

This fixture links `RebirthCamperOverviewSnapshotPolicy.cs` directly. It simulates host/client and dedicated-server/client reply ordering with detached station-state doubles, including installation slot 20. It verifies that delayed replies cannot restore old queue data, while equal duplicates and newer installation manifests remain valid.

It does **not** run a game, real sockets, native UI, serialization, access leases or inventory transfers. Passing it is evidence only for the production freshness decision under the simulated ordering.

2026-10-12 result: **18 assertions passed**, exit 0. Outputs and intermediate files were placed under `E:/_Haven/shared/REBIRTHTemp/CamperOverviewNetworkBuild` and `CamperOverviewNetworkObj`. No installed DLL was changed by this fixture.

Run:

```
dotnet run --project Tools/CamperOverviewNetworkFixture/fixture.csproj -p:NuGetAudit=false -p:BaseIntermediateOutputPath=E:/_Haven/shared/REBIRTHTemp/CamperOverviewNetworkObj/ -p:OutputPath=E:/_Haven/shared/REBIRTHTemp/CamperOverviewNetworkBuild/ --no-launch-profile
```
