import csv,io
from pathlib import Path
p=Path(__file__).resolve().parents[2]/'_Documentation/Purge/FullRegression20261102/LEGACY_REQUIREMENT_MATRIX.csv'
notes={
 'T02':'B28 native savedFresh RebirthOptions World screenshot045500 inspected; WorldThemePurge disabled, actual next-arrow click leftPurgeunchanged, backwithoutSave. RepeatedcoldPurge persistence already qualified. PASS_LOCAL savedtheme/UI context only.',
 'T03':'B28 diagnostic configuredBiome/effectiveBiome and native Entities editor disabledBiome visuallyconfirmed. Ordinary savedpreference was alreadyBiome, so different-preference preservation stillpending.',
 'T04':'B28 newqualification observer counted REAL native biome selection decisions:1647zombies/1647suppressed; no directpredicate calls.3animalsselected/0suppressed. PASS_LOCAL Purge nativeattempts; not asyncanimalcreation orMPclaim.',
 'T05':'B28 untouchedcabin05 nativeapproach activated4actors and shack10activated1 withoutforce. Entry40secondstimeout/3Droutes no-route, screenshotsoutsidewall/fence, so fullordinarytraversal NOTaccepted.',
 'T06':'B28 actualnative3animal selections leftallowed while1647zombies suppressed. Species/hostile-vs-passive/completedasyncspawns notcaptured; PARTIAL policydecisiononly.',
 'T09':'B28 FAIL_LOCAL_NOTICE: freshsite_shack10epoch0/discoveredhistory81858 yet no discoverytooltip/audio pair; cabin05positivepair captured. NativeEvidence can silentlyfillhistory beforeproximity, masking missingnotice. Minimalfirst-generation feedback helperfix10checksPASS/oldnegativecontrolFAIL, build38942299..., nativepostfixacceptancePENDING.',
 'T11':'B28 actualrawzero-sleeper rwg_tile_gateway_t and lot_rural_filler27 targets0sleepers/0triggers/0authoredslots/no record; alldenomunchanged. Hidden/specificcustomexclusions stillpending.',
 'T13':'B28 actualnative3room tier0site_shack10 boundaries remaining3false/2false/1true afterrealcombat, three-room-boundary-review.json. Marker0at1 because lastactorpendingrestoration; markerpopulationnotqualified. B24two-room exclusion retained; otherlowtiers remain separate.',
 'T19':'B28 shacknative3/3deathreceipts Clearrev386, one clearfeedbackpair3, personalcredits187->190. Firstunarmedfixturedeath/failedroutes notaccepted. Nativeforcependingrestore afterplayerdeath, no syntheticcompletion.',
 'T20':'B28 playerdeath/unloadedpendingactors keptshackDiscovered until3actualdeathreceipts; restorednativependingactor completednormally. Unfinishedcabin4participants remainsDiscovered.',
 'T32':'B28 actualbackpack670native search/R recovery restored actualhandsLumberjackGloves/ModPurgeTier5, observed via supplyRules. GenericSurvivorGear={} notnativearmorarray. Fullmodifierpersistence scopes remainpending.',
 'T44':'B28 completedcapture2408.609s/56188samples, GCmax1.839s,allocOFF; finishedreviewafterquit. Mixedfixture/failedroute/combat/nativeUI, no controlledFPS/leak attribution.',
 'T45':'B28 finalerrorobserver2nativeChunkProvider CurrentlyGeneratingData entries (Duration0.0123112/70.9359845). NoPurgeexceptionshown but NOT error-free acceptance. Trace/runtime diagnosticsretained.'
}
statuses={'T02':'PASS_LOCAL','T04':'PASS_LOCAL','T03':'PARTIAL','T06':'PARTIAL','T09':'FAIL_LOCAL_NOTICE','T11':'PARTIAL'}
lines=p.read_text(encoding='utf-8-sig').splitlines(keepends=True);header=next(csv.reader([lines[0]]));ei=header.index('current_evidence');si=header.index('current_status')
for i in range(1,len(lines)):
 row=next(csv.reader([lines[i]]));note=notes.get(row[0])
 if not note or note in row[ei]:continue
 row[ei]=(row[ei]+' '+note).strip()
 if row[0] in statuses:row[si]=statuses[row[0]]
 out=io.StringIO(newline='');csv.writer(out,lineterminator='\n').writerow(row);lines[i]=out.getvalue()
p.write_text(''.join(lines),encoding='utf-8');print('B28 evidence recorded; missing-notice failure retained, all67 rows unchanged')
