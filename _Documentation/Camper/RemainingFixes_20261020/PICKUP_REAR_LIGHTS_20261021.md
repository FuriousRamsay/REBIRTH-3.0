# Approved pickup rear-light binding

Target: REBIRTH 3.0 / 7 Days to Die 3.0 b259. Source-only changed-files integration is owned by the parent task. The approved pickup mesh, source materials, UVs, wheels and model bundle remain unchanged by this contribution.

## Confirmed defects

The previous runtime only bound the original native `80sCamperTruck/lights/RearRedLenses`. The visible camper split now lives at `M/VisualBody/REBIRTHCamperBodyModule/Camper shared lights RearRedLenses`; the original renderer remains as a native material/LOD reference. The new pickup tail lights were never bound. Their imported `lights` material is Legacy Shaders/Transparent/Bumped Diffuse, which has no usable emission channel. Merely setting `_EmissionColor` on it would not correct that omission.

The approved `TruckBodyworkRearDetails_20261020/textured-truck-meshes.json` was parsed as JSON rather than searched as a giant line. It contains six lens segments and twenty-four fine-flute meshes. Segments base/.001/.003/.004 use the red atlas area; .002/.005 use the clear area. Flutes 0–7 and 12–19 belong to red segments; 8–11 and 20–23 belong to clear segments. Thus twenty renderers are red and ten clear. The corresponding actual authored vertex heights and UV areas distinguish the channels. Existing installed-bundle audit independently confirms both native and camper-copy RearRedLenses use Standard RearLampEmission with `_EmissionColor` support.

## Correction

`RebirthCamperRearLights` binds the visible camper-copy renderer first, falling back to the original path for older models. Original camper spotlights are disabled while their actual lens is inactive.

For the pickup, it clones the existing native RearLampEmission material once per model and shares that clone among the thirty approved lens/flute renderers. This retains the same diffuse, normal and emission atlas, mesh and UVs. Only renderer-local emission values change. Rebinding a new native headlight part on the same model reuses the model-owned clone and lamp bindings, avoiding duplicate beams or materials. The clone is destroyed when the pickup object is destroyed; no source/shared asset is edited.

The pickup red channel is dim when headlights are on and bright when braking. Reverse movement alone does not activate the red brake channel. The clear channel emits white only when native forward velocity is below -0.05 m/s. Broken lights and nonfinite velocity produce no pickup emission. Each of the six actual segment meshes owns a short rear-facing spot beam at its mesh bounds center; flute meshes emit without duplicate beams. These lights inherit pickup visibility, while runtime intensity also checks active hierarchy.

The existing camper lamp mapping and engine marker policy remain intact. No engine, input, save format, network state or power policy changed.

## Verification and limits

`pwsh -NoProfile -File Tools/VehicleStudio/Test-CamperLampState.ps1`: PASS. It compiles actual production LampIntensity and checks native legacy mapping, separate pickup red/reverse behavior, threshold/broken/invalid cases, and all thirty exact approved mesh names with 20 red/10 clear totals and body-mesh exclusion.

The separate Unity agent reopened the installed bundle and rendered the actual approved red/clear lens meshes using the native emissive template and existing UVs. Same-camera/light/exposure pixel means changed red (.38,.07,.07) to (.49,.07,.07) and clear (.22,.22,.22) to (.28,.28,.28) when emission changed 0 to .65; clear classification used production LampIntensity. Receipt: pickup-lamp-gpu-receipt.txt (extracted from the separate DyeDurableAndLampAudit/dye-audit.txt). This confirms the actual material/UV emission channels on GPU, not full runtime VPHeadlight execution or six-beam placement. No game was launched or controlled. Full vehicle switching, native braking/reverse, save/reload and beam placement remain manual qualification; checklist Tools/CamperInteractionTests/pickup-rear-lights-manual.json is explicitly NOT_RUN.
