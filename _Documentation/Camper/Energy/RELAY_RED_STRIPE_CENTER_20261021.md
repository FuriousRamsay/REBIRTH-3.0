# Native relay centered on the red stripe

This changes runtime relay placement only. No Unity build, bundle change, game input or root DLL build was performed by this subtask.

The exact painted-band definition is `Tools/VehicleStudio/UnitySources/RebirthCamperAlignedStripe.shader`: `nativeStripe=step(2.23,IN.vehiclePos.y)*step(IN.vehiclePos.y,2.46)*stripeWall`. `RebirthCamperCorrectedWrap.shader` and the production pickup finish shader retain the same native red-band limits. `RebirthCamperReferenceWrap.cs` authors `_VehicleRow0..2` from `root.worldToLocalMatrix * renderer.localToWorldMatrix`, so these are vehicle-root metre coordinates. The preserved aligned-stripe material's row1 is `(0,1,0.000000022351742,0)`, confirming the height basis.

The band midpoint is therefore `(2.23+2.46)/2 = 2.345m`, with0.115m to each edge. This is derived from actual shader/authoring data, not estimated from a screenshot. The earlier installed-bundle skin receipt reported mountY2.238950, only0.008950m above the stripe's lower edge; its bank-top-plus0.12m policy explains the user's too-low relay.

`RebirthCamperRelayGeometry` now exposes the two authored bounds and their exact midpoint. `RebirthCamperRelayVisuals.Attach` casts both mounting rays at that midpoint while preserving the battery bank's root Z coordinate. The native relay remains half-size, seated2mm off the actual nearest skin surface, with the same orientation, native asset, visibility and wire endpoint contract. Removed the obsolete activation-independent battery-top calculation because bank height no longer determines stripe alignment.

The directly linked C# fixture checks equal upper/lower stripe margins and exact Y2.345. Its optional actual-mesh input additionally checks that the center remains above the authored bank top and both mounting rays hit outward-facing camper skin. The exporter and manual scenario now use the stripe center. A live visual check must confirm both sides in Travel/Camp and after reload; no game test is claimed here.

Changed files: `RebirthCamperRelayVisuals.cs`, `RebirthCamperRelayGeometry.cs`, `Tools/CamperInteractionTests/Program.cs`, `export_geometry.py`, `scenario.json`, and this note. No wire input or authority/power policy was edited.
