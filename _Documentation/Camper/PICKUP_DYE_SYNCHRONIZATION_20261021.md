# Pickup infill dye synchronization, 2026-10-21

Target REBIRTH3.0 existing installed approved pickup bundle; preserve original truck materials, wheels, black bed floor and geometry. No source prefab, material, shader or bundle changed.

## Evidence

Both approved source prefab and actual installed `Resources/rebirth_camper80s.unity3d` contain nine single-material LOD-tagged painted addition renderers under `M/VisualBody/80sCamperTruck/body/REBIRTHPickupOnlyPaint`. All use `REBIRTH/PickupOriginalFinish`. Controlled Unity2022.3 GPU renders with native byte-range tint propagated through first/body material and single-slot LOD shared materials demonstrate correct shader support. Front wall, inner sides, tailgate and corner closures all turn red/blue. There is no evidence justifying replacement of their shader or paint-mask logic.

The exact in-game ordering responsible for reported stale additions was not reproduced; native assembly, rendering lifecycle and multiplayer are not running in this offline audit. The correction explicitly maintains the addition subtree's own native dye contract rather than relying only on one shared-material propagation event.

## Runtime correction

`RebirthCamperPickupPaint.Apply` runs from existing PatternAppearance.Apply before its unchanged-pattern early return, via existing native VPHeadlight.Update presentation postfix. It binds only nine added painted renderers (including inactive renderers), invalidates bindings when the vehicle model changes, and derives byte-range tint from the identical `item.GetPropertyOverride(Block.PropTintColor,"255,255,255")` contract used by native Vehicle.SetColors. No normalization, gamma conversion, metadata mutation or material cloning/replacement occurs.

It preserves unrelated material-property-block values and updates `_Color` in renderer and any existing single-material indexed block. Rechecking actual block contents recovers from material/model initialization that clears a block even if the dye metadata did not change. Unchanged blocks are not rewritten. Dedicated servers skip cosmetic work. Separate dark floor, original truck body, wheels, bumpers, license plate and lights are outside the subtree and never touched.

## Offline checks

`dotnet run --project Tools/CamperPickupPaintFixture/fixture.csproj` PASS97 assertions compiling the actual production helper. Covers red, blue and undyed byte tint; deliberately stale white shared material; all nine additions; unchanged material ownership/color; indexed and renderer block preservation; black floor unchanged; repeated unchanged calls; cleared-block recovery; model replacement; dedicated-server skip. Unity and item metadata are boundary doubles.

Actual installed GPU test compares native shared-material tint with per-renderer `_Color` MPB while deliberately keeping each panel's shared material white. Red/blue pixel means match for all nine additions at the receipt precision. Representative front wall: red(.42,0,0), blue(0,0,.42); sides red(.56,.01,.01), blue(.01,.01,.56); tailgate red(.64,.09,.09), blue(.09,.09,.64). White undyed restores neutral panels. Floor pixels remain(.12,.12,.12) and material(.08,.08,.08) across all three colors. Thus the shader receives equivalent byte colors through MPBs and materials.

GPU lens test also uses installed actual meshes, nativeRearLampEmission Standard template and copied actual production LampIntensity classifier: red segment off(.38,.07,.07) vs brake(.49,.07,.07); clear reverse segment off(.22,.22,.22) vs on(.28,.28,.28). This verifies visible emission response, not full native event timing or beam behavior.

Diagnostic source `Tools/VehicleStudio/UnitySources/RebirthPickupDyeAudit.cs` runs via Unity executeMethod `RebirthPickupDyeAudit.Run`, existing VehicleStudio_Camper project. Reports under PickupDyeAudit_20261021 preserve source, installed native-material and installed stale-material/MPB/lamp runs. Camera/light/exposure remains fixed per comparison; single painted mesh isolated for each render. These are GPU color checks, not screenshots of the running game. Manual runtime dye-change, reload and multiplayer verification remains pending.
