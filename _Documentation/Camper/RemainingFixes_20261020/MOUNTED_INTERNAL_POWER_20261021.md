# Mounted turret internal camper power

The four existing mounted consumers were already enumerated by EnergyRuntime, but each demanded zero watts unless `TurretConnected` found a relay cable. TurretPowered independently imposed the same cable requirement. Both filters are removed for built-in mounted turrets. Installed enabled/manual-controlled turrets retain their profile RequiredPower (currently 15 W), distance/priority, native source spending, replicated paid mask and expiring supply. No link is inserted or invented in the external wire graph.

EnergyRuntime still calls HasUsableEnergy and Spend before SetSupply; zero/partial paid supply cannot power a 15 W turret. The mobile-consumer policy already governs Travel/Camp. External plain/native circuit consumers still call RelayWiring.Connected in EnergyDeviceAdapter lines 289/332 and Circuits lines 25/94. No external wiring or authorization path was edited.

Localization.txt and Localization.csv now describe enabling a charged camper source; the relay-selection prompt describes external electrical devices. CCTV/permission/lease/ammunition behavior is unchanged.

`dotnet run --project Tools/CamperMountedPowerTests/MountedPowerTests.csproj`: PASS 26 checks linking the production RelayWiring.Power.cs. Its TurretConnected stub throws if consulted. It verifies all four 15 W loads without links, zero/partial/full paid supply, expiry, replicated mask, disabled/manual/uninstalled states and client authority. Native source spending and actual world-circuit execution were source-reviewed, not simulated by that fixture. No game was launched.
