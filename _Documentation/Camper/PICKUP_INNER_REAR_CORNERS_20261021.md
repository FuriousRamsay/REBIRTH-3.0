# Pickup inner rear corner closure — October 21, 2026

Source: existing cumulative REBIRTH3.0 workspace and approved pickup production prefab. Target: same installed REBIRTH3.0 camper pickup. User reported see-through openings on both inside rear corners of the closed pickup tailgate; latest screenshots show dyed bed walls and black floor correctly retained.

## Confirmed geometry

Independent source inspection agrees: inner bedside X±.7795–.79, rear Z−2.66497, Y.685–1.12497. Tailgate outer shell reaches X±.7454; the pressed inner sheet reaches X±.71497 and begins near Z−2.66861. Approximately34.1mm of corner return is missing between the bedside and outer tailgate edge, or64.5mm to the inner stamping. Existing painted corner closures address only the front wall near Z−.44.

## Scope

Add two enclosed formed rear corner jambs under REBIRTHPickupOnlyPaint, with appropriate thickness, smooth bends and capped ends. Keep a normal closed-tailgate seam. Do not change the approved outside tailgate, side panels, wheels, plate, bumper, dark floor, materials, dye shader or colliders/Rigidbody/wheel configuration. The existing runtime dye binding discovers the additional renderers; the existing body modifier visibility removes them together with other pickup-only paint. No new runtime state, XML, localization, save or network format is needed.

## Validation pending

Measure the new closure against both adjacent sheets; inspect rendered interior views from both sides, a rear exterior view, and geometric coverage. Verify original meshes, transforms/material references and complete physics signatures remain unchanged. Reopen the built bundle, validate new parts and lifecycle visibility. No game launched/controlled by agents; user gameplay validation remains separate. Final delivery record will include package inventory and installation hashes.
