# Native Farming path closure

Audit-only2026-10-15. Closes bounded native/source follow-ups from AUDIT_CLOSURE_ACCEPTANCE_AGENT_REVIEW. Compared FARMING_TRACE223/311/317 and early MP034 reachability first. Reused full current C# body evidence, reread exact named methods/callers; no new full containing-file claim. Read-only Inspect.exe extracts exact installed Assembly-CSharp SHA256 rechecked FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705. No production/build/game/tests/Git. One failed type lookup BlockLayer was corrected to actual ChunkBlockLayer and is not evidence of native missingtype.

## Provider unload liveness — resolved source finding

Native ChunkCluster removal calls chunk.OnUnload(world) then RemoveChunkSync. Full native Chunk.OnUnload method inspected: sets InProgressUnloading=true, unloads entities, iterates every tile.OnUnload, removes render transforms and invokes ChunkBlockLayer.OnUnload. Native TileEntity.OnUnload is empty; GetChunk returns stored chunk reference. It neither marks IsRemoving nor calls OnDestroy or clears that reference. Native ChunkBlockLayer.OnUnload invokes Block.OnBlockUnloaded for registered callbackblocks; native Block.OnBlockUnloaded onlyshapehook/refund-on-unload, not OnBlockRemoved. Actual tile removal is different: Chunk.RemoveTileEntity sets IsRemoving=true, calls OnRemove→OnDestroy, removesmap then resets IsRemoving=false.

Current custom TileEntityWaterTankRebirth424-477 overrides OnDestroy only to unregister. FarmPlot283-422 likewise OnDestroy unregisters both provider/activearea, OnLoad registers activeplot; neither overrides OnUnload. BlockWaterTank.OnBlockRemoved79-87 unregisters, but native unload doesn'tcall that removalhook. Fresh instantiate patch registers provider and ordinary blockload can register again; reload registration does not retroactively invalidate alreadyreturnedoldcachedobject.

Registry IsTileEntityLive746 only te!=null&&te.GetChunk()!=null&&!te.IsRemoving. TryResolveEntry751-835 eight-game-second cache returns CachedTank/CachedPlot under that insufficient predicate without queryingworldmap/currentchunk/blocktype. Nativeunloaded TE satisfies it, including InProgressUnloading, and an olddetachedchunkreference neednotnull. Once cache age>8 or negativeage, resolver calls world.GetTileEntity and returnsnull/prunes absentprovider. World/Game shutdown clears allregistryentries via ModAPI reset, so scope is within sameworld/unloadboundary, not crossworldcachecontamination.

NATIVE-FARM-A confirmed stale cached provider admission: loaded crop/provider near chunkboundary, providerresolvedatT, providerchunkremovedbeforeT+8, nextregistryquery withinrange returnsoldtank. CheckForWater210-235 can reportit; TryDepleteBestWaterStorage299-369 choosesit and directly decrementsoldwaterCount/setsModified/sendswatertank. RebirthUtilities171/182/188-190 are actual depletionadapter callers. Catchup CollectProvidersAndPlots/CollectProvidersInArea also uses resolver and capturesreturned tank. It can therefore supplycropwater fromdetachedstate whilepersistenttanklastsave neednotinclude that laterdebit. This proves invalidliveness and mutationtopology, NOT a measured permanentwaterduplication/reloadincident. Exact native unloaded-chunk lifetime/freepool timing determines duration withincachewindow; no universal8wallsecondsclaim (clock is TotalGameSecondsPassed).

Avoidfixjustremovingcache: checkexactcurrentworld loadedchunk/tileobject/notunloading+appropriateproviderblock andinvalidate onunload; retain validpersistentwater ratherthan deletingit. These are future implementationnotes, not edits. ActiveArea analogous plotunloadregistration maykeepHasAnyFarmPlots untilreset/replacement; only providerstaleadmission outcome qualifiedhere.

## MP034 missing-column traversal — native dependency closed

Native World.GetBlock(x,y,z) returns ChunkCache?.GetBlock(...)??Air. Native ChunkCluster.GetBlock returns actualblockif validy/chunk exists, otherwiseAir. Vector overload delegates defaultIBlockAccess coordinates. Native World.GetHeight is byte and GetChunkSync(...)?height:0. These are exact current native bodies, not inferred from a null check.

LightService.SearchCoverAwareSunPath1044-1110 seeds loadedcropcell+above, boundedradius/down/up; TryEnqueueNeighbor1210-1275 callsGetBlockMemoized which directlyworld.GetBlock withnocurrentchunkguard. ClassifyFace1392 immediatelyreturnsPass forAir; a loadedpassablecell onedge can enqueue missingneighborAir. IsOpenSkyColumnMemoized1128-1160 thenGetHeight=0 andpos.y>=0 returnstrue before anyblock/heightcompletioncheck. Thus cropnearloadedcolumnedge withpassablehorizontalroute to missingcolumn reaches"openColumnAtReachedCell"; source reachability is nowpositive, notstillconditionalonunknownnativeGetBlock. No requirement that everyfarmregularlyseesunloadedneighbors; exactnativeobserverdistance/timing incidentnotmeasured.

Effect remains narrow: Evaluate449 runscoversearch when state-dependentcovernearby. geometryBlockerOverride = cropSunSufficient&&!exposure.Open&&coverblockers>0; missingcolumn Open suppressesthatcoverveto. FinalHasOpenSky495 remains cropSunSufficient&&!override; geometryLightOpenOverride=false, so this DOESNOT manufacturedaylight frominsufficientstoredSUN. Rawblocklightshortcut canindependentlypass. Cachemayretainresultunderexistingpolicy/TTL15worldseconds. Existing MP034description shouldupgrade native-neighborreachabilityqualification whilepreservingSUNthreshold and topologylimits.

## Null ChunkCache bypass — bounded negative mutation result

Evaluate329-379 returnsalltrue"bypass" whenworldnull/ChunkCachenull, sameasdisabled/mushroom. Native World.GetTileEntity explicitlyreturnsnull whenChunkCachenull/missingchunk. Native World teardown excerpt containsChunkCache=null thenunloadsentities/cleansworld; known ModAPI WorldShuttingDown reset clearsFarmingregistries/cache/schedules. No new unconditionalactivegrowthdefect isprovenjustfromthehelperbypass.

Currentordinarycropentry ProcessActiveAnchorPlant469 firstresolvesworld.GetTileEntity andreturnsfalse ifnull; CollectPlants1158 resolvesactualplantTE into localbatch, ProcessPlantsShared1442→PreparePlantCatchup1655 is synchronous ordinaryloop (noawait/yield boundary ininspectedflow). A freshnullcachecall cannotcollect/liveanchorcrop. NetPackages authoritativeRefresh933-980 enumeratescurrentchunk andskipsnull/InProgressUnloading beforeSetAuthoritativeLightPass; nullcachecannotenterplantloop. CatchupdiagnosticAppend2558 andhover/probeEvaluate consumers arepresentation/diagnostic, notgrowthcommit; actualdiagfreshTEalsofiltersnull. DescribePlantTiming acceptsanexplicitTE andcanreportbypassifobsoleteworldsupplied, butitwritesno cropstate.

A reentrant teardown/externallyinvoked helper afterretainedbatchcollection isnotmathematicallyexcluded; nativeGameManager complete shutdown-order/class andall arbitrary externalcallers weren'tdecompiled. Properauditconclusion: helperfail-opencontract exists but noordinarymutationroutewithnullcacheestablished; keepit adefensivecontractqualification, notconfirmedplantgrowthbypass. No runtimeexperimentneededtoavoidpromotingit; sourcepathscope isexplicit. Thisresolvesprior genericpendingwording withoutclaimingallpossibleteardowninterleavingssafe.

## Load/editor distinction

Native ChunkBlockLayer.OnLoad calls Block.OnBlockLoaded withouteditorcheck foritsregisteredcallbackblocks. BlockWaterTank.OnBlockAdded explicitlyrefusesEditMode scheduling; OnBlockLoaded onlyrequiresIsServer andenabledpolicy beforeWBT.AddScheduledBlockUpdate. Thus sourceeditor-load scheduling asymmetryisrealatthisboundary; whethereditorWBTactuallyexecutescropgrowth remainsseparateloopgate, so noeditorsavecorruptionfinding. Nofeatureactivationchanges authorized.

## Evidence methods and identity

Native extracted methods: World GetBlock/GetHeight/GetTileEntity/cleanupnullassignment; ChunkCluster GetBlock/unloadremove; Chunk OnUnload/RemoveTileEntity; ChunkBlockLayer OnLoad/OnUnload; TileEntity GetChunk/OnUnload/OnRemove/OnDestroy; Block OnBlockUnloaded. Wholelarge nativeclassesnotfull-readcredited. C#namedmethods/scopedranges above reuse currentfull-bodyaudit andhashbelow. Earlier no-unloadoverridequalification is superseded by actualnativecallbacks; missingneighborqualificationclosed; nullcacheoutcomeremainsboundedunpromoted. No game/build/test, unloadedwaterdebit or cropyield observed.

| Source | Current SHA256 | Coverage this tranche |
|---|---|---|
| Scripts/AdvancedFarming/AdvancedFarmingWaterProviderRegistry.cs | 2E1095DCE19224869C79F6E1210D1E9E7C2FD2CBA7DEA527F03C6EB7E47F2D69 | Named method/caller scope; prior fullbody reused |
| Scripts/AdvancedFarming/AdvancedFarmingWaterQueryService.cs | 815BD3366C226A465F7DB27EEA15692833360C311FA0FFB8361E23D5A74AE41A | Named method/caller scope; prior fullbody reused |
| Scripts/AdvancedFarming/AdvancedFarmingTileEntities.cs | 54EA85BD307031D8D2A1110B9CDF773C43F7E095C278C41467FCC9198DA98887 | Named method/caller scope; prior fullbody reused |
| Scripts/AdvancedFarming/BlockWaterTankRebirth.cs | A58181B1DE5585C58BBA3F1B081AAF8D3E98D46BD7CE2CADE101E490EFD49A63 | Named method/caller scope; prior fullbody reused |
| Scripts/AdvancedFarming/AdvancedFarmingLightService.cs | 5038B1B729DE88E993D150140DE1C7AC25B88133A335ADEA88CBAEBFF13E4EE8 | Named method/caller scope; prior fullbody reused |
| Scripts/AdvancedFarming/AdvancedFarmingCatchupService.cs | BA3ECCCEAD13D83F75AE103A47E1F3B881F07956C96E7BF94585A6D58AB9C3CF | Named method/caller scope; prior fullbody reused |
| Scripts/AdvancedFarming/AdvancedFarmingNetPackages.cs | 08AC259EFEE89205AD93A97391C67F2F155ED34E31E57E42F7F9B3E622B40EB8 | Named method/caller scope; prior fullbody reused |
| Scripts/AdvancedFarming/RebirthFreshAdvancedFarmingModApi.cs | 7B212F8E90C4B8D95BBC41391A422E9E4AF70D481A0888DBA971CF925A0EFED1 | Named method/caller scope; prior fullbody reused |
| Scripts/AdvancedFarming/RebirthUtilities.cs | 980099177F916F60F89C11DAFD73F69C3DBD5F16F84DEDDB0A87DED06DDCE369 | Named method/caller scope; prior fullbody reused |

