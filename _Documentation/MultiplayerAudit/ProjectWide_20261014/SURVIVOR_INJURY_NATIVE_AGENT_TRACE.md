# Native injury acquisition and server statistics closure

2026-10-15 audit-only exact installed native assembly SHA256 FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705. No production/build/tests/game/commands/git/shared report/ledger changes; no interaction with externally launched game. HAV3N coordination read: QUIET expired2026-10-05, light read-only extraction allowed. Existing inspector invoked without build to extract two native types into E:/_Haven/shared/REBIRTHTemp/SurvivorInjuryNative20261015.

## Exact native provenance and read scope

Full small/current types:

| Extract | Lines | SHA256 |
|---|---:|---|
| SurvivorInjuryNative20261015/NetPackageAddRemoveBuff.cs |79|0574C5C53FDB310A557587CB4BE22713E95E98AAF38441A599CE1EEF98AF033C|
| SurvivorInjuryNative20261015/NetPackageDamageEntity.cs |284|343E047F34BF12442D2F8AF25B45BC54DD51728ADE46E97971B90E3255F1EB37|
| DescriptionBindingsInspect/MinEventActionAddBuff.cs |111|1646630864EE3C3701B698C3F53CE3D9C8EBC7BC50342385D0854B72CD2056E6|

Damage packet middle170–230 re-read after combined display truncated small region, complete type seen. No new compiled project source full-read credit: this is native qualification. Existing native partial classes:

- DescriptionBindingsInspect/EntityBuffs.cs SHA9B73A8AB0CCC95FB844422A673CCDB94F4A0DFA04D9EDB3D35B6A60816E75D27: FULL named Tick52–132, AddBuff overloads165–274 and AddBuffNetwork277–290, FireEvent159–162; prior named Read694–737 reused. No whole-class credit.
- MultiplayerAuditNative20261014/EntityAlive.cs SHA FB348FDE65FEDC93A93F2CF7DEF4B07895E576B35CC8C98923C1F347C1A4C4C1: FULL named DamageEntity4659–4734 and FireAttackedEvents4944–4964; partial damageEntityLocal4885–4942 and ProcessDamageResponse/Local4970–5088. No full entity/damage-local method credit.
- EntityPlayer.cs SHA E230295A7B6356C9EB258BD135C7FF9F70D253E1F23B5992806D08F327C93DFD: FULL named DamageEntity961–969.
- EntityPlayerLocal.cs SHA EA0C73D90A279C9D5BC598CE66784A1923F551E163963DE8F1ACAF800E31E889: FULL named DamageEntity4408–4417, ProcessDamageResponseLocal5164–5171; legacy BreakLeg/SprainLeg methods4594–4618 read but current named buff producer relevance NOT established.

Actual XML named scopes read: base items9480–9500 meleeHandZombie01 attack group; base buffs4615–4656 leg sprained CH/fall handlers and5104–5140 arm CH handler. Mod Config/items.xml70 retains exact CH weighted AddBuff list. No whole XML file credit.

## Result: ordinary owner injury is not snapshot-only

Earlier HORDE_STATISTICS trace correctly proved EntityBuffs.Read directly inserts restored BuffValue and bypasses AddBuff. That alone did NOT establish ordinary remote injury missing statistics. Current native source closes the alternative path:

1. Authored zombie hit effects AddBuff weighted injury trigger on target other. Mod retains these names. CH trigger native onSelfBuffStart adds actual buffLegSprained/buffArmSprained, unless already broken/treated; event also rolls other crit handler.
2. MinEventActionAddBuff computes netSync=!Self.isEntityRemote | IsLocal. Native owning player Self is notremote, so secondary clinical AddBuff normally netSync=true. Existing buff instance may refresh but normal new acquisition creates BuffValue then AddBuffNetwork.
3. EntityBuffs.AddBuffNetwork emits NetPackageAddRemoveBuff to server from remote process; server-origin sends to clients. Exact current package ProcessPackage calls entityAlive.Buffs.AddBuff(buffName,instigatorPos,instigatorId,netSync:false,...). This is the SAME implementation overload patched by RebirthSurvivalHealthEpisodePatch.
4. Server observer world is nonremote and patient exact ready player. New recognized episode can therefore be counted on remote owner's AddRemove receipt even though owner-side observer refuses remote world. No architecture change to server clinical simulation required.

Withdraw any broad inference that all normal remote injuries reach server only as StatsBuff and are omitted. No confirmed active general remote-injury statistics gap from this chain. NetPackageAddRemoveBuff is an actual native operation bridge distinct from unreliable complete snapshot. Source checker must preserve this distinction when integrating prior conditional observations.

## Damage dispatch and native owner architecture

EntityPlayer DamageEntity checks damage-enabled and incomingmodifier then base; local override only camera shake after base result. EntityAlive DamageEntity computes native response via damageEntityLocal and sends NetPackageDamageEntity remote→server or tracked peers/entity for host. Native damage-local sets ModStrength0 for isEntityRemote, fires attacked events and processes local response. FireAttackedEvents sets MinEventContext.IsLocal for remote player victim when attacker locally simulated; this deliberately allows appropriate target event network action. Exact routing not replaced by custom server damage calculation.

Damage packet reconstructs response/source/flags/equipment/attacking item then invokes entity.FireAttackedEvents and ProcessDamageResponse, not damageEntityLocal. It excludes owner echo for local falling/environment special cases. Thus mod statistics damage-local hook is not automatically invoked for every replicated native response. This remains a precise event-scope limitation, not evidence that direct server-origin zombie damage or owner counter fallback is absent. Prior statistics native max-baseline fallback handles certain lifetime counts; no assumption it restores detailed injury events.

Native EntityBuffs.Tick starts unstarted BuffValue, sets live Instigator, invokes BuffClass.FireEvent then marks Started and ticks/updates/removes; it has no local/remote filter in this method. Individual effect dispatch/action ownership controls matter. Owner AddBuff's reliable network bridge, not full snapshot diffing, is enough to expose ordinary new secondary injuries to server observer.

## Qualified ordering and replay limits

Unreliable StatsBuff may carry same accepted injury and Read directly installs it; if such snapshot arrives before AddRemove operation, server prefix sees Active and refuses new-episode credit. Exact reliable/unreliable transport ordering/channel/delivery timing not established here; no measured reproduction, no promoted missing-count finding. Conversely a restored snapshot deliberately should not increment acquisition counters. Any future fix must distinguish new operation from restored state without replaying native effects or counting save restore.

Native AddRemove forwards packet before local application and uses operation body without custom receipt. Current statistics prefix/postfix counts new active acquisition once; duplicate ordinary AddBuff refresh sees active, so no repeatcount from exact duplicate while buff stays present. Actual reordering around remove/re-add or case normalization requires native stream evidence before claim. No arbitrary owner CVar/client payload redesign proposed.

Package authorization uses native dispatcher conventions; this tranche did not re-audit all default NetPackage.ValidEntityIdForSender/attached routing or infer new native exploit from absence inside ProcessPackage. Existing authenticated transport audit remains source. MinEventActionAddBuff duration references/weights are native original semantics; no custom mutation/retry added.

## Remaining cohort

No new compiled Survivor source credited; native dependency closed and broad remote injury gap not promoted. Actual remaining source inventory must reconcile concurrent tradegraph/HUD/Debug/WorldSkills agents with parent canonical manifests. Existing candidate CSV is only pending-ledger+no-trace-filename heuristic, not reviewed/unreviewed authority. Current exact main Statistics/Horde/model/store bodies all provided in this agent's recent manifests. No source beyond assigned native qualification consumed as broad scan credit.
