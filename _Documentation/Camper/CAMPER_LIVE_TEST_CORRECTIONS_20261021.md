# Camper live-test corrections — October 21, 2026

Source/target: current REBIRTH 3.0 cumulative workspace. Delivery type: changed-files package. This phase follows owner testing of installed DLL AB4CA652 and bundle24AC7C1F. Runtime fixes were installed and checkpointed as 6bcbe721ad510d01a7528b7f18e07132098b673d, verified on the remote branch before agent gameplay began. The owner authorized Game Bridge testing in disposable CodexTest. Actual game observed: 7 Days to Die v3.3.0(b18), REBIRTH2026.10.08.1830.

Accepted requests: suppress Duke note pin only REBIRTH progression; correct native laser in CCTV; remove per-shot CCTV smoke; improve single-shot manual accuracy and verify camera alignment; restore hood/ladder access in Travel; center half-size relays vertically within red stripe; native wire-tool preview/sound without selection notification; maximize safe turret reach across all four mounts without intersecting vehicle; OFF toggle yellow/ON green; relay fade only with station Camp deployment; complete player heads in CCTV.

## Toggle background correction

The old paint code cast turretEnabled (an XML rect) to XUiV_Sprite, so it could never recolor the actual child actionFill. The shared RebirthCookingAction then also reset actionFill to its default palette every update. An optional StateFillColor on that established action renderer now supplies the fill; all four turret rows set yellow (180,140,0) when OFF and green(20,145,55) when ON. Hover lightens the state color, retaining both states. Every other action retains its default palette because StateFillColor is null. Text/icon behavior unchanged. No persistence/network/XML schema changes.

## CCTV aim alignment review

The camera is parented to TurretMuzzle at local(0,0,.04), identityrotation. Native fire Cone rotation is muzzle rotation*Euler(0,180,0), and native Fire uses minusCone.forward, matching muzzle/camera forward. The reticle(912,-420) equals center of camera texture rect(240,-42,1344,756), and rendertexture1280x720 has same16:9aspect. No sideways parallax or static off-center reticle found. Native getSpread supplies block/ammo random angular spread in manual mode; correction is scoped to the active manual controller and one effective ray, preserving pellets, automatic fire, range, damage and obstruction rules. Live headshot verification is still required.


## Pre-test checkpoint

The owner explicitly authorized autonomous Game Bridge gameplay testing overnight on October21, superseding the earlier no-agent-game instruction for this task. Commit and push precede gameplay tests. Five-minute stalled-goal retry heartbeat: retry-stalled-rebirth-overnight-goal. Only disposable CodexTest may be modified; its baseline is clone input only.

Working-tree integration build passed:19warnings/0errors using cached .NET4.8.1 reference assemblies. Eight current production-source fixtures passed176assertions total (laser39,aim37,manual13,CCTV8,wire16,Duke29,rear18,pattern16). Hood/ladder/relay geometry and lifecycle additional evidence is in companion docs. No new in-game PASS is claimed at this checkpoint.

The rear cylinders are original paired round rear-lamp metal bezels: installed parts mesh6130890988526752843 has circular shells matching exactly the X/Ybounds of the two RearRedLenses components. Runtime light objects have no mesh. They were retained. Reverse-only illumination is boosted from.65 to3.25intensity,4to6mrange and48to60degrees; brake/tailcolor semantics retained.

Selected modifier and cosmetics rows now use a flat editor palette in native IsSelected state; this avoids multiplying the dark row tint against native ui_game_select_row into black. No armor materials changed for this UI correction.

## Completion assessment at checkpoint

NOT COMPLETE: owner-required live Game Bridge scenarios have not yet been run. Implementation source and executable scenario preparation are present; local/remote checkpoint and live qualification are in progress. The final report will distinguish actual screenshots/audio/state evidence and remaining limits.

## First disposable-save execution

Native bare-truck placement, native body modifier drag/install, forward driving (16.41m), steering drive (31.02m), reversing and dismount completed without spinning or launching. No game errors since the placement sequence. Eight-second Deploying transition observed at 0.9s samples. Four installed turret toggles and CCTV claim/release passed the executable driver; prior first-person flag restored. Three real manual rounds consumed exactly and were funded by camper battery with externalRelayLinkCount0. Duke native buff pin suppression passed in REBIRTH. Captures reviewed: yellow OFF controls and CCTV-only label present, near-camera smoke absent; broad near-field laser wedge remained and required correction. Headshot attempt fired no shots because its bounded servo could not reach a poorly chosen target sector; this is not accuracy PASS.

Live qualification also found observer points using static factory root, plus relay mount/shader lookup using incorrect frame/registration assumptions. Scoped corrections now use moving ModelTransform and loaded station fade shader. CCTV laser uses exact native mesh/material but clips its extreme near-field segment only during the CCTV camera render; other cameras restore complete beam. Rebuild passed19warnings/0errors. Further live qualification required.


## Second execution and corrective build

Remote checkpoint9ec86658c71af61df606da36503ee47fe8a5c108 verified. In rebuilt game relayCount2, platecenterY2.34500027..2.34500074; both fadegroups identical every sampledopacity in Deploy/Pack, endpoints0duringtransition/Travel and2Camp. NativeCCTVbeam visually reviewed: broadwedgegone, retainedthin2pixeltrace nearreticle. Completeplayerhead visible atbottomofCCTV. Twelve consecutive head-aligned rounds across two executableattempts damageddurablezombie by26each with oneammunitionperround; attempts stopwhenquickclickproducesnoround, so no10/10clickPASSyet. Native50mssendpollcanlosequickpressedges; immediateeffectivefiretransitiondeliveryfixadded, nativefiringcadenceretained.

Camp hoodsupport andfrontladderactualPASS usinghood→jumpintoraisedladder, ratherthangroundapproach. TravelhoodactualFAIL: nativeCabHull highfrontwall; scopedplayerfrontzonefilter+exactauthoredslopedhoodsupportcorrectionprepared. Reverseilluminationexecutable12stepsPASS, actual3.25intensity/6m/60degrees, release0; nighttimegroundredilluminationreviewed. SelectedarmorlistrowpurplePASS; nativeDiscopatternapplicationvisibleoncamperbody, pipes/grillesweatheredbrownmatchadjacentplates inactualrender. Build19warnings/0errors; nextretestrequired.

