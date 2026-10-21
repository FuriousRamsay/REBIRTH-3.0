# Native CCTV beam near-field correction

The live CCTV screenshot `20261021-054042-222_cctv_manual_shot_0.png` still showed a bright triangular ribbon after transform-scale correction. This is a separate near-camera projection defect.

Read-only UnityPy inspection of the installed `automatic_assets_other/items.bundle` confirms the junk turret's actual `turret_laser`:
- Native tube mesh transverse size0.00937176682m; local XYscale0.25, targeting ancestor2.54, prefab0.75. World diameter0.004463m.
- Native material `SemiTransGlowRed`, shader `Game/SemiTransGlowLaser`, color(1,0,0,0.61). Shader uses SrcAlpha/OneMinusSrcAlpha, front-face culling and depth writes. These properties remain unchanged.
- The other asset named `turret_laser` under junkSledge uses a different Standard transparent material with alpha0.12549. It was inspected but is not substituted.

CCTV is only25mm above the beam with a10mm camera near plane. A4.46mm tube close to that camera projects as a broad near-field wedge even when its world diameter is correct. Altering the whole beam's scale/color or using the sledge material would change the approved native visual for everyone.

The correction retains the native mesh, material, diameter, world beam and hit endpoint. Only the camper CCTV camera clips the near-camera beam segment before its render, using the camera's actual image height and vertical field of view to locate where native beam diameter projects to two pixels. At720px/80degrees this is approximately one metre. The remaining native beam is still visible toward its hit point. Short-hit beams retain their final1mm segment. OnPostRender, disable, destroy and the next BeginCamera restore the complete world transform. Other cameras never opt into this correction. Camera aim, camera near plane, firing origin, hit test and power policies are unchanged.

`dotnet run --project Tools/CamperLaserTests/LaserTests.csproj --no-restore`:149PASS. Fixture links production width/near-field math and covers resolutions, zoom/FOV, short hits, endpoint preservation and off-axis refusal. Actual live CCTV rendering remains to be verified by the parent-controlled game session after rebuild.

Installed-bundle evidence: `E:/_Haven/shared/REBIRTHStaged/CamperLiveCorrections_20261021/native-laser-data.txt` and `native-laser-shader.txt`. The attempted read-only Unity batch audit exited because no valid batch Editor license was available; UnityPy inspection succeeded instead. No game asset was rebuilt or changed.
