$ErrorActionPreference='Stop'
Add-Type -Path @((Join-Path $PSScriptRoot '../../Scripts/Survivor/Persistence/RebirthStationRegionPayload.cs'),(Join-Path $PSScriptRoot 'test_station_region_payload_fixture.cs'))
[StationRegionPayloadChecks]::Run()
'PASS actual bounded region payload extraction with synthetic native-format bytes; no world files or engine callbacks used'