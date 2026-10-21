# Parent-run Energy qualification sources

The refreshed fixture was built and executed on 2026-10-21 for the approved battery-charging implementation. See `_Documentation/Camper/Energy/BATTERY_CHARGING_IMPLEMENTATION_20261021.md` for exact scope and limitations. The older qualification notes below describe the original fixture; current source keeps native seat admission and checks the registered driver guard's pin-reasserting postfix.

Prepare explicitly before **every** standalone simulation run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/CamperEnergyFixture/Prepare.ps1
dotnet run --project Tools/CamperEnergyFixture/fixture.csproj
```

Preparation extracts the unchanged actual `ModuleRuntime.TickStations` body using
unique checked boundaries. Its containing module/native persistence/custody shell
is a double. Model, State, Runtime, Config and VehicleHud are directly linked actual
source. The executable checks complete current source and generated fingerprints
before any assertions. No automatic MSBuild child shell masks preparation failures.

Coverage includes actual native0.16 period accounting across0.1/1/10-second schedules,
generator fuel/buffer and battery charge/buffer conservation, demand above native
MaxOutput and100 consumers, one shared solar budget across consumers, partial sun/
night, positive deployment/packing, current driver/movement refusal and actual
driver-postfix pin reassertion and Travel release, canonical malformed-empty/truncated/foreign frames,
packed source GUID/fuel/seed/paint/foreign-metadata preservation, idempotent afterimage,
exact operation holds/release, no paused station/resource catch-up, default-false
recovery, source-changed lease refusal and same-operation save retry after injected
sync failure, read-only genuine client HUD projection and native CURRENT/MAX.

The added charging cases compile the exact current production helper and Runtime with the decompiled installed native `PowerBatteryBank.AddPowerToBatteries` method body in `NativeBatteryCharging.cs`. Only the native root-dirty callback is doubled. They cover funded solar/alternator inputs, native per-period acceptance, full/off/refused banks, slow/running/off/fuel eligibility, no generator or battery feedback, actual Tick demand-before-charge, existing state publication, hold/unload/remote boundaries, cadence and bounded catch-up.

Explicit doubles: other native bank methods mirror cached current generator/battery/solar
code; world/time/owner/physics/save/transport/UI are deterministic doubles. The item
codec is a complete-data **double**, not native paint ABI evidence. Chunked device
lease storage is also a double; its actual framing/key-budget requires the adapter's
separate fixture. A simulation PASS never establishes a native multiplayer/save/UI
pass, and never makes a fake client or fake electrical-device claim.

`native-roundtrip.csproj` is a separate source-only actual-native helper. Parent may
compile/load it explicitly into an initialized game and call
`RebirthEnergyNativeRoundtripFixture.Run(actualDetachedBank, actualSixSlots, fuel)`
on the native game thread. It invokes the actual private Energy codec by reflection
and the real `RebirthNativeItemCodec`, compares every canonical complete native image
including paint/metadata and actual source fuel, rejects trailing data, and verifies
original inputs were not mutated. It reads no owner save, vehicle, world or real
inventory and writes no real save. Supply genuine painted/metadata-bearing source
and component clones; empty defaults alone cannot qualify paint preservation. The
helper is not registered with any production command or bridge route. Record its
loaded DLL hash and match the parent build manifest before treating it as current.

Game Bridge definitions now use the authored actual selectors/windowgroup:
`camper_energy_modes_ui.json`, `camper_energy_install_ui.json`,
`camper_energy_pack_source_ui.json`, `camper_energy_hud_camp.json`. Their setup must
identify the real disposable camper, actual endpoint and settled custody. Mode
scenario requires default5sec timers; other positive timing choices need adjusted
observation timeouts. Packing capture leaves the packed source on native cursor and
does not assert bag return or full metadata from UI row disappearance.

Generate local-only driver scenarios without game calls:

```powershell
python Tools/CamperEnergyFixture/make_driver_scenarios.py --entity ACTUAL_ID --out Tools/GameBridge/out/energy_scenarios
```

Use a verified actual entity ID. Generated scenarios explicitly assert native
driver/local attachment IDs through the existing `/camper` observer. Camp refusal
and Travel mounting are distinct preconditions; pressing E alone is never proof.
They use the local-authority observer and never claim a remote multiplayer pass.

After genuine remote UI/input, `capture_remote_driver.py` accepts explicit real
client/server sessions, server URL, native camper ID and expected-driver yes/no.
It requires client world.remote and a current authoritative native remote-player
connection witness, then compares server GetAttached(0) to that exact client actor.
No local-host substitution, packet injection, source mutation, input retry, trace
enable or mode command occurs. Failure evidence is retained in the explicit output.
It asserts a state checkpoint, not the causal success of an earlier action; record
the corresponding genuine input and native mode/HUD evidence separately.

Screenshots need visual review; action success is not proof of mounting, mode
restriction, native full-byte source custody or genuine remote authority.
