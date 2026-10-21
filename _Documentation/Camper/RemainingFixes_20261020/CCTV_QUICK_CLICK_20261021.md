# CCTV quick-click input delivery

A live55ms click sometimes consumed no ammunition despite valid aim and a loaded powered turret. CCTV previously sampled the effective fire boolean only on its50ms heartbeat, allowing a frame-observed press/release pair between send deadlines to disappear.

The production cadence helper sends immediately when effective fire changes, including release. Unchanged held/idle input retains its50ms heartbeat. Camera start/stop reset state. The initial-release guard, server lease, power/ammo custody, native shot interval and held automatic fire remain unchanged. This does not fabricate clicks never observed by the input provider.

`dotnet run --project Tools/CamperManualInputTests/ManualInputTests.csproj`:25PASS. Tests link production cadence and exercise both edges between deadlines, throttling, clock/session reset,55ms clicks at four phases, and held input against an explicit125ms firing-admission model. Actual firing remains a separate Game Bridge qualification.

After parent rebuild: ten55ms clicks separated by250ms should each debit exactly one round. Hold400ms to verify native repeated cadence, then release and verify firing stops. Exit/reopen must not retain firing. Empty ammunition and disabled power must still reject shots.
