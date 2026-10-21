# Camper hood, ladders, relay fade and reverse-light input scenarios

Status: authored and offline generation-tested; NOT RUN IN GAME. Parent task owns committing/building before launching the disposable game and owns all live input. This subagent did not launch or control the game.

`RebirthCamperTrace.Snapshot`, called only by explicit diagnostic queries, now includes `presentation`. Values come from actual native mode, current CharacterController feet/ground contact, bInElevator, actual relay mounting transforms and property blocks, effective enabled Light components and the layer14/21 collision matrix. It does not inject movement, set mode, change materials or run per-tick capture. `hoodApproach`/`hoodAim` use the native vehicle-root frame; `ladderApproach`/`ladderAim` use the actual authored front ladder volume. All points add Origin exactly once for native world input.

After native setup of a disposable fitted-body camper, capture a current observation and generate scenario JSON with its actual entity and world coordinates:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/GameBridge/gamebridge.ps1 camper entity=ACTUAL_ID > E:/_Haven/shared/REBIRTHStaged/camper-surface-observation.json
python Tools/GameBridge/prepare_camper_surface_scenarios.py E:/_Haven/shared/REBIRTHStaged/camper-surface-observation.json E:/_Haven/shared/REBIRTHStaged/camper-surface-scenarios
powershell -ExecutionPolicy Bypass -File Tools/GameBridge/gamebridge.ps1 test E:/_Haven/shared/REBIRTHStaged/camper-surface-scenarios/camper_hood_ladders_travel.json
```

The preparer itself makes no bridge call and cannot move the game. It emits four executable JSON files:

- `camper_hood_ladders_travel.json`: requires on-foot Travel and closed gameplay UI. Uses real walk/look/jump movement; checks native grounding and actual vehicle ground support in the hood zone, then native ladder admission. Guard remains enabled; cleararea/restore prepares safety without faking a result.
- `camper_hood_ladders_camp.json`: the same actual movement and support checks in Camp. Enter the mode through real UI before running it.
- `camper_relays_station_fade.json`: requires Travel and the already-open native Energy Management lease. Clicks the real mode button, observes Deploying/Packing halfway, compares actual relay opacity with actual station opacity, checks exact stripe-center bounds and deployed-only endpoint colliders, then closes the UI. It does not open a window through an admission bypass or call a mode mutation console.
- `camper_reverse_illumination.json`: requires the local player already in the native driver seat through real interaction, flat clear road and Travel. It sets nighttime as fixture preparation, reverses with actual movement input, checks velocity and actual effective Light intensity/range/cone, releases input and verifies return to the original profile. Screenshots require human visual review; scalar light fields alone do not certify ground brightness or visual quality.

Regenerate from a fresh snapshot if the vehicle moves before a hood/ladder scenario. The scenario's `precondition` is explicit rather than silently manufacturing a fixture. Each is stop-on-failure and includes screenshots/error checks. Keep guard enabled and inspect surroundings before movement as required by AGENTS. GameBridge action success means input dispatch, while `expectCamper` checks the resulting native state. A missed jump or failed ladder admission fails rather than being replaced with teleport or forced state.

Offline validation: six tests in `Tools/VehicleStudio/tests/test_camper_surface_scenario_generation.py` verify actual entity substitution, executable one-key step shape, real walk/jump/reverse actions, native mode-button use, actual station opacity comparison, and existence of every asserted observer field. The generated scenarios are not claimed executed.
