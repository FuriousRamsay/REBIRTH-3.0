# Camper implementation work packages — agent draft

2026-10-15. Planning artifact only. No implementation, scenario file edit, build, game/test or Git action performed. Parent integrates/approves execution scope and current checkpoint under project directives; this draft does not grant extra authorization. Based on completed Camper source/XML traces, current AUDIT_REPORT MP013/016/017 families and existing user outstanding Camper interaction/visibility/selection/lock issues. No new gameplay feature proposed.

## Preserved contracts

Keep native vehicle ownership/locks, native seats/camera/physics lifecycle and native player inventory save/replication. Keep original before/after item images, phased owner receipts, saved server journal and native vehicle/region primary-file witnesses. Queue/refund/training effects remain original owner/job/admission bound. Never resolve uncertainty by deleting receipts, granting replacement contents or releasing holds early. Keep existing portable module format/content identity, restored station object ownership and historical recipe actor distinct from current authenticated owner. No global NPC/Inventory/Survivor authority rewrite, new station rules/device caps, asset adoption or VehicleStudio bundle overwrite.

Before implementation parent revalidates hashes because workspace is shared. Source owners prepare exact narrow diffs and scenario code first, parent reviews disjoint changes, then authorized scoped checkpoint/build/restart and real qualification. Never run current template.plan scenarios as if complete. Existing tests are starting flows, not prior PASS evidence.

## WP1 — Correct Camper authenticated creation lookup

Owner: Camper access integrator. Main file RebirthCamperCharacterAccess.cs; bounded caller touches only when required ModeInput,CampDoor,Sink,EnergyAccess/RemoteOwnerEffects/PoiAccess. Reuse CharacterScope.ResolveLocal/projected creation for exact remote EntityPlayerLocal; server EntityPlayer continues current authoritative cached record. Do not make server repository readable/mutable on clients or accept client-supplied creation as authority. Preserve native-mode stable creation design and request/session current guards.

Confirmed PHYSICAL-A: remote native-mode helper returnsnull before M/door/sink request. This also bounds current Energy owner effect opening/creation behavior; close all actual callsites, do not fix only visible M symptom. Shared Survivor client projection/read accessor is a dependency, owned Survivor; request narrow public read contract from that owner if missing. No edits to Survivor repository/network.

Scenario code before execution: add native-mode remote creation/prompt flow using actual host+remote, Mdefault/rebound, closed/openwindows, door latejoin, sink heldconversion, Energyopen; exact actor/vehicle/world/creation mismatch rejects; reload/disconnect current creation recomputes without item changes. Extend camper_energy_modes_ui.json and entry deferred flows with concrete fixture IDs and exact expected values, no fake remote flags.

## WP2 — Restore authored UI installation paths

Owner: Camper XML/UI integration. Files Config/XUi_InGame/templates.xml, Config/XUi_InGame/station_templates.xml and new/existing narrowly scoped Camper common-navigation template include as implementation choice; Config/_Vehicles/camper_station_switch_windows.xml/picker references remain same public template ID. Move only Camper navigation template to shared valid template scope; do not expose all Rebirth-only station templates in native mode. Ensure exactly one definition per mode/native parser load. XML-CLAIM-A: native-mode missingtemplate currently becomes emptyview, no switchtabs.

SinkHUD: Config/_Vehicles/camper_sink_windows.xml, main Config/XUi_InGame/windows.xml include ordering. Preserve single current toolbelt replacement; append SinkHUD after replacement or explicit include in surviving node, avoid duplicate IDs/HUD controllers. Existing PHYSICAL-B unconditional earlyappend destroyed by laterwindowremove. TransitionHUD survives and must remain independently owned.

Scenario code: native andRebirth station picker+single/two-type tabs, nativeplacedstations never showCamper switch; toolbelt exactlyoneSinkHUD andTransitionHUD before/after mode/reload/menu; sinkgauge actualcapacity/fill updates andhidesoutsidevalidtarget. Use camper_single_navigation/two_type_navigation/station_switch,energy_hud_camp; exactID count/rect screenshot assertions. XML parse absence logs separate from gameplayconservation.

## WP3 — Selected module inspector and native input fidelity

Owner: Camper presentation. Files XUiC_RebirthCamperModules.cs and Config/_Vehicles/camper_modules_windows.xml; action/nativeVehicleFrame tinyfollowup onlyif direct reviewedbehaviorrequires. Map actual selectedstackcount/quality/durability/stats to currently authored placeholders or remove unused decorative placeholders only if user requested resulting presentation is preserved. Reuse existing item-context/stat renderer APIs with UI owner consent, no copied new global evaluator. Do not inventdurability onitemswithout nativeHasQuality/UseTimes; clear/hide stalevalues onempty/reselection.

XML-CLAIM-B currently plainselectedpanel has no updater for those nodes; actual Inspect maps name/description/icon/capacity only. Native backpack quality overlays are separate and not automatically broken by this finding. Existing item icon/palette property blocks and ModuleSlot.SelectedChanged border must preserve native IsSelected and item identity. Real locks are UserLockedSlot/StackLock/IsLocked; AttributeLock encumbrance is not insertion prohibition. NativeSlot caller lock-mode guard already exists; no speculative whole-lock rewrite.

Scenario code: extend direct_shift_storage/stations/direct_drag and station/storage_dashboard; clickselected actualqualityitem andplaincontainer, alternatehover/selection/empty, verify displayedquality/durability/count/capacity exact, icon/border stable, noitemchangefrominspection. Native owner backpack locktoggle andshift/drag locked/unlocked, encumberedunlocked cellsusable, nativevehiclepart/cosmeticservice selectedpreview. Capture screenshots andlookat them. UI/Input owners retain common editor/nativeBackpack classes; Camper owner can consume their interfaces only.

## WP4 — Client station request failure/reopen cleanup

Owner: Camper client lifecycle. Files RemoteStationClient.cs,StationWindowSession.cs,RemoteManagement.cs,XUiC_RebirthCamperModules.cs; StationPicker onlyif callbackfinallyneeded. Current confirmedCONTINUATION-A/B andCLIENT-A: CommandPending/SwitchClosePending setbeforesend withoutrollback/deadline; nativewindowclosecatch leaves Closingassociationblockingreopen. Modules baseOnClose-before-release is conditionalexceptiongap.

Separate presentation cleanup from transaction custody: only reset pending when no send/operationescaped, otherwiseretain durableopand explicitlytransitiontorecovery. Original response token/exactstation/lease mustpreventlatecallbackclosingreplacement. Window close must call release in robustfinally/retainedretry path without treating anfailedsaveascommit. Do not clear station/session holds merely topermitreopen; associate recoverable originaloperation and retirableUIlease separately wherecurrentcontractspermit. Native ordinaryplacedstationclose unchanged.

Scenario code: scoped failure injection plan forpackageconstruction/sendexception, delayed/lostreply, windowclosecallbackexception, death/disconnect/worldshutdown, lateoldreply afterreopen. Prepare deterministic injection boundaries and cleanup assertions before running; no permanent production throw switch. Assert no repeateddebit, no nativeclonescopied, current originalreceipt status retained, subsequent recovery/UI opensafely. Existing pointer_native_gap,card_navigation,pending_observation are coreflows; mustextend realremoteack path, not waits-only success.

## WP5 — Lifecycle handoff of unresolved watches

Owner: Camper custody lifecycle. Files StationCancellationWatch.cs,StationCompletionWatch.cs,StationRefundRecoveryWatch.cs,RemoteStationUpdates.cs; shared recovery helper onlyinsideCamper. Findings GAP-CANCEL-A/local64 +completionoffline64 +remoteRefundwatch retention, Updates.Watch earlyyield without lease-finally. No generic serverbootstrapcleanupchange.

Add explicit world-scoped retirement/handoff of transientwatch objects while durable owner/nativejournals retain unresolvedcustody. Unreleased original effects cannot simply be forgotten or replayed; classify immutable persistedstate and retain appropriate recovery refusal. Sameworldofflineowner mayresume originalbinding; worldshutdown mustdrop stronggraph/poll after persisting existingobligation, not mutate obsoleteworld. Updates stream onexception mustretireexactlease ormarkrestartable; sameTokenAdmit mustnot claimlivewatch ifstopped. Capcapacitymustreturnafterretirement, duplicates retain originalsinglepermit.

Scenario code beforetests: queue/cancel/refund at eachsave/nativeapplicationboundary, owneroffline, close/reopen/death, unloadvehicle, shutdown/reload; check transientcountsbounded andnooldworldreferences/replies with monotonicreplacement. 65abandonedoperations setup uses real preparedreceipts/failure-boundaries in disposable fixture, do not manufacture completeditems. Parent tests realremote server/client andseparate localhost. Watch cleanup unit-level exercise onlywhen validates actualstatecontract; gamequal required.

## WP6 — Portable station relocation correlation

Owner: Camper portable recovery. Files StationRelocationRecovery.cs +StationRelocationCorrelation.cs/RemovalContents shared exacthelper ifneeded. NEXT-A: discovery's exactremovalAfter vsinstallationBeforecomparison failsnormalconsumedContentsKey carrier, while downstreamcorrelationalready normalizes.

Use a single canonical comparison that validates original ContentsKey payload before excluding ONLY its consumed delivery envelope, preserves exactportable station/item/installation/sourcevehicle ancestry andbothApplied transferjournals, rejects ambiguoushistory. Do not strip malformedmetadata, recapturecurrentqueueasoriginal ordrop admittedjob/recipe/owner/savepath checks. Must handle cursor andbackpackquickremoval accordingcurrentjournal semantics; inspect whetherTryFind currentlycoversboth beforeclaimingfull support.

Scenario code: stationwithpaidqueuedjob+tools+fuel+output removed→extrasdelivered→normalcarrierreinstallanotherCamper→reopen/reload→actualjobresumeswithoutrepayment. Alsoemptyextras,unclearedvalid envelope,malformedenvelope,sameinstallationdifferentowner,duplicatedhistory/reusedID, changedrecipe/quality. Extend transfer_preserve_stations and packedreturn flows; count perfullnativeitempayload, queueattributionandmoltenstock notnameonly.

## WP7 — Exactly-once removal extras delivery

Owner: Camper ownerdelivery protocol. Files StationContentsDelivery.cs,StationRemovalContents.cs,PreferredRemoval.cs,RemoteManagement.cs; existingTransferJournal/SaveAcknowledgement/remoteOwner transportonlynarrowadapterswithsharedsourceownercoordination. NEXT-B partialnativegrantthenenveloperemains canretryfull; overflowspawn callnotacknowledgment. Qualifiedsourcefailurewindow, noordinaryduplicate reproduction yet.

Before coding choose compatible durable delivery binding/attempt mechanism withinexisting originaltransferreceipt owner design. Prepare full destination/overflow plan, save originalattempt before firstgrant; expose one-use originalnativeapplication, finalownerfile exactafter+receipt andactualoverflowpublicationproof before stripping carrier/settling. Onpartialcallback/crash neverretryfull grant orstripmarkerandloseitems. Preserve oldcurrent carriers/originalAppliedhistory forreconciliation; no automaticcreditonmissingreceipt. Parentmustreviewprotocoltransition/migrationbecause portablepayloadalreadyexists. Native ownerinventory callbacks staynative; no NPC tradegatewayrewrite/globalinventorysetterpatch.

Scenario code: exactcapacity/partialmerge/fullbagoverflow, multiplecontentrows, callbackthrowafterfirstrow, nativebagsavebeforemarkerretirement, overflowrequestfail/delayedspawn, close/disconnect/reload. Need actualphysicaloutput+cursor+ownerbag+overflowentity+savedreceipt conservation andatmostoneGiveExp/training. Existing packed_redrag/packed_return/repeated_shift flows areordinarypaths only; add dedicatedextrasfault scenarios. Faultinjectionquality remains unexecuteduntilpropercheckpoint.

## WP8 — Startup retry and journal-index performance

Owners: sharedbootstrap owner forMP013/017; Camper recovery owner forMP016. Files ModulePatchInstaller.cs/LocalAccess.cs and relevantSharedHarmonyBootstrap viaitsowner; EnergyCustody.cs/TransferJournal.cs/SourceCoordinator.cs/Maintenance.cs onlyindexedreaders/writers within agreed sharedjournalowner. PreserveotherHarmonyowners and completedclassbookkeeping, don't resetFlagonlywhennativehooksdisagree.

Confirmed parenttrace partialmoduleinstaller latefailure afterLocalAccess.Register blocksretry; sharedrollback/livepatchmetadata mustbeconsistent. MP016 EnergyReady beforecadence scansallhistoricalreceipts perloadedCamper/frame; implementverifiedworldscopedpervehicleunresolvedindex loadedatrecovery andupdatedsynchronouslywithjournalpublication. Othernestedhistory scans reuseconsistentindex wherevalid; don'tinventglobalmaxdevicecap/deleteAppliedhistory/movegateafterspend. Unknownindex failsclosed andcanrebuild.

Scenario code: everyinstallationstagefault/retry withnativepatchmetadata evidence, Debug/Release/Dedicatedregistration; historysizes/multipleCamper with file-read/decodedbyte/frame metrics, newpublication/reload/corruptionconcurrentquery; samecustodyproof andnoresourcecatchup. Prepare scripts beforeperformancebatch, no smoke-only claim.

## WP9 — Physical user-facing completion qualification

Owner: Camper physical/source author, paired asset owner separately. Sources ModeInput,CampDoor,CampEntryController,CampSeatReturn,PhysicalInteraction,CampStationAppearance,EnergyDrivingInput/Immobility,BottomHUD; onlyeditafterdirectreproductionpoints tosource. Resourcebundle changes exclusively assetowner withcurrenttargetfileagreement. Userrequested campentry/return,prompts/stationvisibility,noDrivingCamp,camera/seat/input fidelity,POI actualsupply,quality/locks areexistingfeature acceptance, notnewmode/seats.

Scenario code: deploy/pack20s currentconfig, targetdooropen/close, entercabinonrotated/slopedCamp, sit eachauthoredseat2..5, exitreturnwhilecampthenTravel, no torque/jump/displacementbutnativecamera/horn/headlights/exit, no oldpose ondeathdetach/reload/remote latejoin. Checkinstalledonlyroof/interior/powersources+sinksvisualandcollisionthroughtransitions, fadeopaque/preservepaint/tint, sinkwaterconversion/gauge. Native/Rebirth host+remote, Dedicatedwithclient. Use entry_seat_target_deferred/camp_presentation_capture/fade_deferred/driver_capture/vehicle_hud_visibility +energy_lighting/bounds flows, replace deferrednoteswithconcreteassertionsbeforeexecution. Physicalrender/shader/geometry NOT RUN isnot permissiontorebuildVehicleStudioassetsblindly.

## Integration ordering and disjoint ownership

First resolveWP1/WP2 source/XMLreachability sofixturescanreachfeatures. WP3 presentation canparallelwithcustodycodeonlydisjointfiles; WP4andWP7 bothRemoteManagement soonefileowner/sequentialintegration. WP5watchsourcesparallelWP6correlation; WP7deliveryprotocolreviewbeforechangingcarriersemanticsusedbyWP6. WP8sharedbootstrapphase coordinatedseparatelyfromCamperruntime; verifiedindexcontractbeforechangingreaderbehavior. WP9realgeometrytestaftercorrectbuildandpairedcurrentbundle.

Inventoryowner retains common selecteditem/creative/toolbelttrash/global lockinputs/nativeinventorysetters; noCamperworkpackagechanges themwithoutdirectrequiredinterface agreement. Survivorowner retains creation/progressionrepository/skillreceipt/traitbackgroundcapability. NPCowner retains activities/trade/economy/gateway; noCamperdebitdelegatesroutedthroughNPCAPI. XMLsharedmainfile oneintegrator, agentsprepare scopednodepatchesforreview. GameBridgesharedrunner/source onequalificationowner; no competinggameprocess/control. Thisauditdraft itself runsnone.

## Concrete review and exit evidence

Eachimplementationpackage delivers exactsource/XMLdiff, currentbefore/afterhashes andscenarioJSON/scriptcode markedNOT RUN untilactualexecution. Scenarioassertions useexistingRunner README primitives; unsupportednativefile/overflow/faultassertions requireexplicitminimal read-onlybridgeobservability scopedbybridgeowner, not inventstepnames. Reviewpreconditions/disposablesave/fixturepositions andactualnetworkpeerprovisioning beforecallingtestsready. Existingloopbackrunneralone doesnotprovisiontwoclients.

Laterverificationmatrix: localRebirth/localnative; remoteRebirth/remotenative; dedicatedauthority+ownerclient; fresh/load/reconnect/death/worldreset; normal/uncertainoriginalattempt. Countphysical nativeitemidentity/count/slots/locks/metadata/queue/overflow andfinalreceiptstatus; screenshotslookedatforvisualissues, errorsSincebaseline. Nativecraft GiveExp updatesXP/events only, stationOutput physicalitems countedonce. No blanket certificationfromallC#FULL orhashcoverage. ReportstillrequiresruntimeNOT RUN untilrealqualifiedevidenceexists.
