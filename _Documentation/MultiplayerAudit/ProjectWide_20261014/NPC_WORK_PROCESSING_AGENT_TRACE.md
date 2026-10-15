# NPC work processing/farming remainder — 2026-10-15

Audit only, exact scope announced before reads. Full six sources1251 lines: FarmingPackage371, HarvestAdapter212, PlantWater298, WorkCustodyReceipt60, WorldTarget204, WorkNavigation106. No production/tests/build/game/Git/shared ledger/report edits. Prior farming/hauling/activity traces retained. Negative caller searches used -uuu so ignored DEBUG paths included.

## Current authority/release

No external FarmingWorkPackageService.Plan/CreateAndStart caller found in Scripts -uuu. ConcreteWorkInfrastructure.Register has WorkReleaseGatefalse earlyreturn before these adapter/definition registrations; Tick earlyreturn before package/executor work. WorkNavigation.Request always returns TemporarilyUnavailable with explicit unqualified original-target/movement reason; Complete intentionally does not stop a possibly combat-owned route. Thus these packages do not currently move workers or mutate crops through ordinary Work. Active player harvest/water behavior and remote conservation findings in FARMING_TRACE are separate native-owner paths; do not duplicate/generalize them to held NPC adapters.

WorkCustodyView/ExecutionWorkSaveScope are immutable descriptive receipts carrying original stable owner, world/path/load generation and quarantined/held disposition; they grant no authority merely by construction. Actual execution lease/persistence producers compare private original scopes (prior activity trace). RecordCopy includes full persisted execution fields, no mutation/IO. World pointer remains internal, descriptor getters do not release custody. No new custody scope bug found in this file.

## Planner and target semantics

Farming plan clamps radius32/8 and256 assignments, requires authoritative nonremote world/AdvancedFarming, inspects crops/plant tile state and farm plot below. It selects harvest/water/plant by conditions and sorts priority/coordinates after truncation. Limit breaks only innermost z loop, so it continues bounded outer loops after filling limit; it does not find global highest priority before selection. Coordinates near integer limits could wrap loop bounds; no accepted gameplay-world coordinate path established.

Assignments are created through existing session/capability/revision services, per-target reservation and coordinator. StartImmediately tries each assignment, later reconciliation retries Ready rows. Packages snapshot cloned ID arrays but grouping is process-only. No explicit owner claim/terrain access/native actor action is added here; future admission must supply actual policy. Held Work excludes current arbitrary plot modification claim.

PositionTarget marks IsLoaded/Available merely world!=null, not target chunk loaded; its revision includes type/meta/rotation but omits meta2/damage/tile state/incarnation. Stable target resolves registry native ID and runtime revision; Validate moves current position but does not enforce original revision. WorkNavigation.TryResolveNpc returns current world EntityRebirthNPC by mapped ID without independently rechecking stable/profile/dead/presence. Native registry invariants are dependencies, not proven ID reuse exploit. Target helpers and Request are internal execution contracts rather than network authority.

## Harvest held mutation/custody

Harvest CanHandle/Prepare accepts fully grown player crop, resolves seed replant and first usable nonseed Harvest drop, fixes quantity1 regardless configured count/probability. World preimage compares type/meta/meta2/rotation (not damage/tile incarnation), writes seed-stage via SetBlockRPC, verifies same tuple then credits abstract NPC inventory with context.OperationId and authority key. Failure restores crop only while same seed postimage remains, avoiding overwrite of a newer different block. No final restoration witness is checked; native callback side effects and exception after world mutation have no durable original outcome receipt here.

Physical full native item custody is not supplied by this item-key quantity transaction; existing held executor/authority mismatch and physical endpoint qualification remain prior audit scope. Replayed inventory transaction is treated as credited only if current world crop was still expected and replanted; no independent durable crop incarnation binds replay. This is dormant activation dependency, not an ordinary replay exploit demonstrated. Active native player farming has different yields/ownership and cannot be proven by this fixed-one adapter comment.

## Plant/water held commit and compensation

Plant Begin reserves one seed in abstract NPC inventory, release before debit, Apply context operation ID (or Replayed) then direct SetBlockRPC. Failed retained placement refunds with deterministic XOR-derived compensation GUID. A thrown native mutation after debit propagates without refund in this file; failpostimage refund can coexist with an earlier temporary placed effect. Seed rollback is revision/authority checked and result reported. Per-assignment pending state and reservations are process-local; durable original item/world witness requires outer controller custody, not shown by this adapter.

TryPrepare checks known seed item/block, Air target and native CanPlaceBlockAt on server. Tick rechecks target tuple before mutation but not current support/access/native placement constraints again; no structural/native actor stroke. Water Begin records current block and depletion1; Tick gets current plant/crop, returns no-mutation completed when already watered, consumes existing storage-first water provider then sets bWatered/timestamp/modified and publishes AdvancedFarmingSync. It uses active shared water helper but is not its installed caller under current Work gate. Water debit and plant state publication have no durable combined receipt/refund on postconsume exception; no native owner inventory transfer is involved. Actual water-provider behavior already full-audited in FARMING_TRACE; no duplicate native claim.

## Completion limits

No new active gameplay finding promoted. These six sources close real pending source coverage while retaining release isolation. Future Work qualification must prove original target/current actor movement, live chunk/access/placement, exact input/output custody and crash settlement. Native SetBlockRPC/CanPlaceBlockAt details were not independently decompiled here; no tests or gameplay run. Remaining pending Work source candidates include FairRoundRobin57 and report/qualification files (FarmingLogistics111, DutyMedical119, ProductionResource134); parent ledger reconciliation may already own some. Earlier source manifests cover domain/outcomes/construction/mining/medical/guard/hauling/physical and native workstation chains; do not count scans as reads.

## Full-read manifest
- `Scripts/NPC/Work/RebirthNpcFarmingWorkPackageService.cs` — 371 lines; SHA256 `80BEBF02079E12CD58D2B6F404EAE1C2E5677498159475649AB40ECF74D2D3AE` (full read).
- `Scripts/NPC/Work/RebirthNpcAdvancedFarmingWorkAdapter.cs` — 212 lines; SHA256 `6E8B90306FD36000A4D2720B4E1DC4D1861638AF98EC72AAE77F704795C1F9A7` (full read).
- `Scripts/NPC/Work/RebirthNpcAdvancedFarmingPlantingWateringAdapter.cs` — 298 lines; SHA256 `A9BFCF1A8C8DC9CB0D991661504A6750C65D2D89F51FA06E5D9407027376B1BD` (full read).
- `Scripts/NPC/Commands/RebirthNpcWorkCustodyReceipt.cs` — 60 lines; SHA256 `A9BE662713063097C1A5BC5C07988272ACE495FC36EF09848B89A857CFB8A8BD` (full read).
- `Scripts/NPC/Work/RebirthNpcWorldTargetAdapters.cs` — 204 lines; SHA256 `78D308856D26E0EF2AA12298A13791C49FA2F332DA3B02C03E43C9EDD5F66106` (full read).
- `Scripts/NPC/Work/RebirthNpcWorkNavigationService.cs` — 106 lines; SHA256 `8519C869F9B45DACC439F3FC2A9FCF677337C21559F0917B67B55CA9F698C197` (full read).
