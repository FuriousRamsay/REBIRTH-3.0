# Native beam width and camper-aware turret stops

Target: current REBIRTH 3.0 workspace / 7 Days to Die 3.0 b259. Source-only changes; parent owns compilation and installation. No model bundle, camera placement, firing code, ammunition or native material is changed by this correction.

## Beam

The prior visual already cloned native `turret_laser` mesh/material from the junk-turret prefab. However it copied only source.localScale when extracting the nested object, dropping source ancestor scale. Its destination compensation also assumed parent XYZ scales equal beam XYZ scales despite pitch/yaw rotations. Both assumptions can turn a native thin beam into a ribbon in CCTV.

The corrected implementation captures native effective basis lengths with source.TransformVector(unit axes), including ancestors. After setting the desired world pose, it measures the destination's actual rotated basis and compensates each axis. Native width stays independent of endpoint distance, destination hierarchy scale and pitch/yaw. Native mesh, material, alpha, UVs, muzzle offset and -Z orientation remain unchanged; the existing surface-ray visual reach remains unchanged. Invalid/degenerate scale inputs fail closed. The beam remains visible in CCTV.

`dotnet run --project Tools/CamperLaserTests/LaserTests.csproj`: PASS39 production scale-helper checks covering nested/nonuniform bases, zero/near/far endpoint lengths and invalid inputs. `RebirthNativeJunkLaserAudit.cs` provides a read-only installed-prefab ancestor/material audit for a coordinated Unity session; no game was launched here. Exact on-axis CCTV appearance remains manual qualification, not certified by the algebra fixture.

## Geometry-aware stops

Previously BarrelClear checked only the 25mm capsule between pitch pivot and muzzle. A camera could therefore point through its own window/frame/plough several metres ahead without the barrel intersecting anything. ClampAim checked only rectangular authored limits, and the remote-pose path bypassed Move entirely.

The existing barrel check remains, and a short forward sphere cast now rejects actual owned geometry within the camper's authored collider extent. Extent is cached once per bound rig from local Box/Mesh bounds, including inactive body/plough shapes; no full mesh scan occurs per frame. Actual active colliders are queried for each candidate pose. Exact broad model-root selection boxes, triggers, seated actors and the gun's own rotating yaw subtree are excluded. Real walls, cabin hulls, window frames, plough, fixed mount structures and other owned solids remain blockers.

All four rigs retain their authored yaw/pitch limits. The production boundary helper sweeps at most .5 degrees between samples, binary-refines the first collision boundary, and tries both axis-slide orders to retain the closest safe requested pose. It stops at geometry instead of reverting the entire mouse movement or narrowing every turret's limits. Host preview, authoritative manual input, automatic motion and remote interpolation use the same safe movement boundary. Native firing retains its existing BarrelClear gate.

`dotnet run --project Tools/CamperAimBoundaryTests/AimBoundaryTests.csproj`: PASS37 linked-production checks over four mirrored corners, maximal refined horizontal/vertical boundaries, free directions, axis sliding, thin obstruction sweep and invalid input. The callback geometry is deterministic test geometry; actual native collider coverage, both axes/all four CCTV views, moving/tilted camper and deployed/retracted body/plough remain manual qualification. No game or heavy Unity job was launched.
