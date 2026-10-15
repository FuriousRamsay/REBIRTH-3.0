# NPC native/content closure

2026-10-15. Bounded audit only; no production/build/game/tests. Reused NPC_SPAWN_AGENT_TRACE, NPC_SURVIVOR_XML_CLOSURE_AGENT_TRACE, NPC_DOG_COMPLETE_AGENT_TRACE and prior NPC native combat evidence before new reads. Fresh literal full XML reads: Config/_NPC/entityclasses.xml172, Config/_NPC/entitygroups.xml53, Config/entitygroups.xml3. Native base XML read only exact nodes listed below; whole-file hashes are identity anchors, NOT full-read credit. Native extracts are prior installed assembly evidence, not newly built/decompiled bodies.

## Human authored class versus actual population route

Root Config/entityclasses.xml includes _NPC/entityclasses.xml (prior exact include evidence); root entitygroups.xml fully read now includes _NPC/entitygroups.xml. Bandit base npcRebirthBanditManSDCS explicitly declares EntityRebirthBandit, bandit.standard, SDCS model/controller, UserSpawnType Menu, bandits faction and RebirthSelectHostileTarget/RebirthMeleeCombat/RebirthRangedCombat tasks. Female inherits base. Survivor male inherits bandit base, replaces Class with EntityRebirthSurvivor, profile survivor.ambient, faction whiteriver and corresponding tags; female and persistent variants inherit menu exposure/combat task fields. Twelve paired male/female specialist profiles inherit persistent survivor bases. All are real authored native class definitions, not merely C# profile strings.

Specialist entitygroups pair each of combat instructor, firearms instructor, wilderness guide, farmer, technician, medic, cook, builder, chemist, scout, quartermaster, mystic. The comment that population/placement is controlled by persistent spawn service is descriptive intent, not installed orchestration. Current -uuu Config search found no references to npcRebirth* or npcSpecialist_* outside entityclasses/entitygroups files. No ordinary biome/spawning placement route is thereby established. Catalogue npcSurvivors/npcSurvivorsPersistent/npcBandits/npcSpecialHumanoids group strings remain unestablished native groups, distinct from actual specialist groups.

Prior full WorldIntegration/Spawn trace is current corroborated by -uuu caller search: TryPrepareSpawn/TryConstructPreparedSpawn/TryPublishConstructedSpawn are declarations without external production invocation; adapters Spawn/ResolveAmbient have no assignment found. TryComposeSpawnCore explicitly returns Native spawn adapter unavailable if null; TryPromoteAmbient similarly rejects missing resolver. These are unavailable/inactive orchestration, not a working ambient population spawner or automatic cold restore. Do not enable them as an audit correction. World journal/persistence parsing can be active independently of absent fresh-spawn producer.

Reachability conclusion: authored menu/admin/native explicit instantiation can select defined classes, so MP058 optional-stamina melee contract applies to those actual class instances once spawned. Ordinary naturally populated Survivor/Bandit gameplay frequency remains NOT ESTABLISHED; it is incorrect either to call every human actor dormant or claim an installed natural population route. Host/dedicated native server owns actual NPC runtime; client model/presentation does not activate missing server population. No menu spawn or native actor runtime was executed here.

## Dog exact inherited hand and template nodes

Fresh exact literal native XML node reads from ../../Data/Config/items.xml: meleeHandAnimalWolf and meleeHandMaster. Wolf directly extends Master (there is no intervening meleeHandAnimal node used here). Master Action0 Class=Melee, Delay.8, Range1.65, Sphere.1, Block_range2.5, entity10/block16, hidden/nonholdable/nondroppable organic stack1, blunt/melee/light/perkFlurryOfFortitude tags, and DamageFalloffRange base_set1.65. Wolf overrides HoldType35, Delay.6, Range1.7, entity14/block16; adds ModSlots base_set0, earth-tagged DamageModifier perc_add3 and onSelfAttackedOther AddBuff other/fireOneBuff with seven weighted fatigue/sprain/laceration/abrasion/stun/bleeding buffs, weights .11,.07,.07,.05,.29,.36,.11.

Scoped current Config/items.xml dog node again read: FuriousRamsayMeleeHandDog extends wolf, overrides HoldType35 and Action0 Sphere1/Delay2/Range2/entity30/block60, declares no effect_group removal/replacement. It therefore retains inherited native melee action contract and authored wolf event effects under native extension rules; it is not the custom human RebirthMeleeCombat task. Range2 does not itself prove all damage falloff values become2: Master has explicit inherited1.65 effect. Do not infer exact final DPS or impairment probabilities from these source numbers alone.

Fresh exact native entityclasses.xml animalTemplateHostile node: EntityEnemyAnimal base, native animal/edible/hostile tags, Standard model, animal/enemy flags, native attack/break/wander/target tasks, default bear hand, death/twitch/tracker properties; dog overrides Class/profile/faction/tags/hand/tasks as previously fully read. Template Base Effects adds noTeethNoInfection0/1 onSelfAttackedOther conditional on buffBrawlerNoTeeth, tagged BuffResistance base_set5, random HealthMax perc_add-.2,.15, and onSelfFirstSpawn buffEntitySpawnHeal. Dog authoring base_setHealthMax500 does not logically remove inherited percent-add health range; final runtime HealthMax also includes C# projection/level/difficulty and native evaluation. Dog's own high tagged resistance replaces matching type/modifier only according to exact native merge rules below, not a blanket deletion of template effects.

Dog own native AITask1 ApproachAndAttackTarget classEntityAlive0/Look and empty remaining inherited target slots keep C# authoritative targeting distinct from inherited combat effects. Dog XML native onSelfKilledOther kill cvar remains progression reconciliation producer. Exact inherited hand does not authorize owner inventory or change NPC metabolism custody.

## Native effect merge and event contract

Read prior exact MinEffectController.cs AddEffectGroup109–148 and ParseXml150–197 literally as bounded methods. ParseXml first adds child's effect_group nodes, then walks supplied parent and its extends ancestors by exact named element. AddEffectGroup(_extends:true) removes a parent passive only where an already present no-requirement child/current passive has empty tags and matching type/modifier for base_set or perc_set. It does not generally remove triggered effects or parent perc_add effects. Parent groups are inserted in parsed order. Thus child damage properties/no effect groups do not erase inherited wolf triggered effects; animal template triggered effects and random health perc_add are not removed merely by dog base_setHealthMax. Do not assume effect_group name alone is an override key.

Exact prior ItemActionAttack extract has actual damagingItemValue.FireEvent(onSelfAttackedOther,entityAlive.MinEventContext) and entityAlive.FireEvent same trigger in native accepted hit branches (scoped791/837/839 evidence). ItemClass.FireEvent forwards to Effects.FireEvent. Those are native event routes, not proof every attack hits or a specific buff applies. Native MinEffectController runtime evaluates requirements/passives/events; target resistance/immunity and event context determine effects. Parent caller XML extension construction was not newly extracted, so this closes the inherited authored/native merge contract using prior parser provenance, not an observed loaded merged table. Current runtime effect instances still require actual native host/remote combat observation.

## Exact coverage and hashes

```csv
file,sha256,coverageMethod
Config/_NPC/entityclasses.xml,55F42F1132940F276E195B78E902DB275FFED509C2C179940090B44A74F112E4,FULL_LITERAL_XML_CURRENT_BODY
Config/_NPC/entitygroups.xml,35D9ABC34795498722CCB206E46462054E47287FA8AA7B0B4E2EB04568C24CB8,FULL_LITERAL_XML_CURRENT_BODY
Config/entitygroups.xml,319E681C0955E8936EBDF8332797C84995F00509680558EDD1BCA2DA9D3FD38D,FULL_LITERAL_XML_CURRENT_BODY
Config/items.xml,61E45859B9B9CB895AD9DDAF374071FEEF8BA8EC0D7E71EA2AD0FF35D0C50581,SCOPED_DOG_HAND_NODE_ONLY
../../Data/Config/items.xml,BDC45D552E8DF7A5C48D9159516EA145C52F351C2A444091EE738D9284FF405B,SCOPED_WOLF_AND_MASTER_NODES_ONLY
../../Data/Config/entityclasses.xml,5F930212BD315039A641841A571938F5F5AA855178E1859B3E6F5F1D05E79061,SCOPED_ANIMAL_TEMPLATE_HOSTILE_NODE_ONLY
E:/_Haven/shared/REBIRTHTemp/FireAgentNative20261015/MinEffectController.cs,476F77E262498E1B593CA6792B728D1DED1266A25ADCA5EA21A7AACA2E9CAD01,BOUNDED_NATIVE_ADDEFFECTGROUP_AND_PARSEXML_ONLY
```

No new defect promoted or intended dog balance changed. Population unavailable/inactive and loaded-table/runtime effects are explicit limits. Native assembly baseline reused from prior evidence: Assembly-CSharp SHA256 FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705,3.2b10; no fresh native build/extraction. No production source coverage credited from search results or hashes.

