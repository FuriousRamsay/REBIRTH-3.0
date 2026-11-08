using System.Collections.Generic;
using System.Xml.Linq;

public class MinEventActionMadmanExplosion : MinEventActionTargetedBase
{
    
    private static readonly FastTags<TagGroup.Global> TAG_ALLY = FastTags<TagGroup.Global>.Parse("ally");
    private static readonly FastTags<TagGroup.Global> TAG_EXPLOSION = FastTags<TagGroup.Global>.Parse("explosion");

public static FastTags<TagGroup.Global> explosionTag = TAG_EXPLOSION;
    public static Dictionary<int, DamageRecord> hitEntities = new Dictionary<int, DamageRecord>();

    float _initialForceStrength = 50f;
    Vector2Int _XZMultiplier = new Vector2Int(2, 4); // side to side range - x and z coord
    Vector2Int _heightMultiplier = new Vector2Int(5, 10); // y coord

    int blastPower = 0;
    int blockDamage = 0;
    int blockRadius = 0;
    int entityDamage = 0;
    int entityRadius = 0;
    int ParticleIndex = 13;
    int explosionType = 0;
    int ignoreHeatmap = 0;
    string blockTags = "";
    string particleName = "";
    string impactPosition = "";
    string soundName = "";
    ExplosionData explosionData;
    Vector3 TargetPos = Vector3.zero;

    public int SourceEntityID;
    public EntityAlive SourceEntity;
    public bool IsNPC = false;

    public override void Execute(MinEventParams _params)
    {
        var entityAlive = _params.Self as EntityAlive;
        //Log.Out($"MinEventActionMadmanExplosion::Execute - this Entity: {entityAlive}, other: {_params.Other}, others: {_params.Others}");

        if (entityAlive == null) return;


        SourceEntity = entityAlive;
        SourceEntityID = entityAlive.entityId;
        int entityId = EntityClass.FromString("FuriousRamsayNoEarthDamage");

        DynamicProperties properties = EntityClass.list[entityId].Properties;

        ExplosionData tmpExplosionData = new ExplosionData(properties);

        bool ignore = false;

        if (ignoreHeatmap == 1)
        {
            ignore = true;
        }

        explosionData = new ExplosionData
        {
            BlastPower = blastPower,
            BlockDamage = (float)blockDamage,
            BlockRadius = (float)blockRadius,
            BlockTags = blockTags,
            EntityDamage = (float)entityDamage,
            EntityRadius = entityRadius,
            ParticleIndex = ParticleIndex,
            damageMultiplier = tmpExplosionData.damageMultiplier,
            IgnoreHeatMap = ignore

        };

        ItemValue itemExplosion = null;

        if (explosionType == 1)
        {
            itemExplosion = ItemClass.GetItem("thrownGrenadeContact", false);
        }
        else if (explosionType == 9997)
        {
            itemExplosion = ItemClass.GetItem("otherExplosion2", false);
        }
        else if (explosionType == 9998)
        {
            itemExplosion = ItemClass.GetItem("otherExplosion", false);
        }
        else if (explosionType == 9999)
        {
            itemExplosion = ItemClass.GetItem("weaponBonusExplosion", false);
        }
        else if (explosionType == 9996)
        {
            itemExplosion = ItemClass.GetItem("weaponBonusExplosion2", false);
        }

        //Log.Out("MinEventActionMadmanExplosion-Execute instigatorId: " + _params.Buff.instigatorId);
        //Log.Out("MinEventActionMadmanExplosion-Execute instigatorPos: " + _params.Buff.instigatorPos);

        if (_params.Buff.instigatorId != -1)
        {
            //Log.Out("MinEventActionMadmanExplosion-Execute 1");
            TargetPos = _params.Buff.instigatorPos;
        }
        else if (_params.Other != null)
        {
            //Log.Out("MinEventActionMadmanExplosion-Execute 2");
            TargetPos = _params.Other.position;
        }
        else
        {
            //Log.Out("MinEventActionMadmanExplosion-Execute 2");
            TargetPos = _params.Self.position;
        }

        if (blastPower == 201)
        {
            RebirthWeaponBonusParticles.Play(particleName, impactPosition, soundName, _params.Self, TargetPos);
            //Log.Out($"InstantiateParticleEffect: {particleName}, impactPosition: {impactPosition}, TargetPos: {TargetPos}");
        }

        Explode(itemExplosion);
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        var flag = base.ParseXmlAttribute(_attribute);
        if (flag) return true;
        var name = _attribute.Name.LocalName;

        if (string.IsNullOrEmpty(name))
        {
            return flag;
        }
        else if (name == "blastPower")
        {
            blastPower = Int32.Parse(_attribute.Value);
            return true;
        }
        else if (name == "blockDamage")
        {
            blockDamage = Int32.Parse(_attribute.Value);
            return true;
        }
        else if (name == "blockRadius")
        {
            blockRadius = Int32.Parse(_attribute.Value);
            return true;
        }
        else if (name == "entityDamage")
        {
            entityDamage = Int32.Parse(_attribute.Value);
            return true;
        }
        else if (name == "entityRadius")
        {
            entityRadius = Int32.Parse(_attribute.Value);
            return true;
        }
        else if (name == "ParticleIndex")
        {
            ParticleIndex = Int32.Parse(_attribute.Value);
            return true;
        }
        else if (name == "explosionType")
        {
            explosionType = Int32.Parse(_attribute.Value);
            return true;
        }
        else if (name == "blockTags")
        {
            blockTags = _attribute.Value;
            return true;
        }
        else if (name == "ignoreHeatmap")
        {
            ignoreHeatmap = Int32.Parse(_attribute.Value);
            return true;
        }
        else if (name == "particleName")
        {
            particleName = _attribute.Value;
            return true;
        }
        else if (name == "impactPosition")
        {
            impactPosition = _attribute.Value;
            return true;
        }
        else if (name == "soundName")
        {
            soundName = _attribute.Value;
            return true;
        }
        else
        {
            return false;
        }
    }

    public void Explode(ItemValue _itemValueExplosionSource)
    {
        //Log.Out($"MadmanExplosion::AttackEntities - _entityThatCausedExplosion: {SourceEntityID}, _itemValueExplosionSource: {_itemValueExplosionSource}, damageType: {damageType}");

        FastTags<TagGroup.Global> tags = explosionTag;

        if (_itemValueExplosionSource != null)
        {
            ItemClass itemClass = _itemValueExplosionSource.ItemClass;
            tags |= itemClass.ItemTags;
        }

        float value = EffectManager.GetValue(PassiveEffects.ExplosionEntityDamage, _itemValueExplosionSource, explosionData.EntityDamage, SourceEntity, null, tags);
        float value2 = EffectManager.GetValue(PassiveEffects.ExplosionRadius, _itemValueExplosionSource, explosionData.EntityRadius, SourceEntity, null, tags);
        int layerMask = -295563285;// ~(1 << 2 | 1 << 4 | 1 << 12 | 1 << 13 | 1 << 14 | 1 << 15 | 1 << 16 | 1 << 18 | 1 << 19 | 1 << 20 | 1 << 23 | 1 << 24 | 1 << 28); // -18739221;
        int prevLayer = SourceEntity.GetModelLayer();
        SourceEntity.SetModelLayer(2, true);
        Collider[] allColliders = Physics.OverlapSphere(TargetPos - Origin.position, value2, layerMask);// -538480645);
        //Log.Out($"Found allColliders: {allColliders.Length}, layerMask: {layerMask}");
        SourceEntity.SetModelLayer(prevLayer, true);
        bool flag = false;

        foreach (Collider collider in allColliders)
        {
            if (collider == null)
            {
                continue;
            }

            Transform transform = collider.transform;
            string tag = transform.tag;

            if (!tag.StartsWith("E_BP_"))
            {
                continue;
            }

            flag = true;
            Transform hitRootTransform = GameUtils.GetHitRootTransform(tag, transform);
            EntityAlive entityAlive2 = (hitRootTransform ? hitRootTransform.GetComponent<EntityAlive>() : null);

            if (entityAlive2 == null)
            {
                continue;
            }

            if (entityAlive2.entityId == SourceEntityID)
            {
                continue;
            }

            if (entityAlive2.HasAnyTags(TAG_ALLY))
            {
                continue;
            }

            int controller; RebirthNpcStableId boundId;
            if (RebirthBlackMagicService.IsControlled(entityAlive2.entityId, out controller) || RebirthBoundUndeadService.IsBoundEntity(entityAlive2.entityId, out boundId))
            {
                continue;
            }

            entityAlive2.ConditionalTriggerSleeperWakeUp();
            Vector3 vector = transform.position + Origin.position - TargetPos;
            float magnitude = vector.magnitude;
            vector.Normalize();

            // check for collision with terrain layer 16
            if (Voxel.Raycast(GameManager.Instance.World, new Ray(TargetPos, vector), magnitude, 65536, 66, 0f))
            {
                continue;
            }

            EntityClass entityClass = EntityClass.list[entityAlive2.entityClass];
            float num = ((!transform.CompareTag("E_BP_LArm") && !transform.CompareTag("E_BP_RArm")) ? ((transform.CompareTag("E_BP_LLeg") || transform.CompareTag("E_BP_RLeg")) ? entityClass.LegsExplosionDamageMultiplier : ((!transform.CompareTag("E_BP_Head")) ? entityClass.ChestExplosionDamageMultiplier : entityClass.HeadExplosionDamageMultiplier)) : entityClass.ArmsExplosionDamageMultiplier);
            float num2 = value * num;
            num2 *= 1f - magnitude / value2;
            num2 = (int)EffectManager.GetValue(PassiveEffects.ExplosionIncomingDamage, null, num2, entityAlive2);

            if (num2 >= 3f)
            {
                if (!hitEntities.TryGetValue(entityAlive2.entityId, out var value3))
                {
                    value3.entity = entityAlive2;
                }

                EnumBodyPartHit enumBodyPartHit = DamageSource.TagToBodyPart(tag);

                if (num2 > value3.damage)
                {
                    value3.damage = num2;
                    value3.dir = vector;
                    value3.hitTransform = transform;
                    value3.mainPart = enumBodyPartHit;
                }

                value3.parts |= enumBodyPartHit;
                hitEntities[entityAlive2.entityId] = value3;
            }
        }

        foreach (KeyValuePair<int, DamageRecord> hitEntity in hitEntities)
        {
            EntityAlive entity = hitEntity.Value.entity;

            if (entity == null)
            {
                continue;
            }

            bool bIsDead = entity.IsDead();
            int health = entity.Health;
            bool bCrit = hitEntity.Value.damage > (float)entity.GetMaxHealth() * 0.1f;
            EnumBodyPartHit mainPart = hitEntity.Value.mainPart;
            EnumBodyPartHit parts = hitEntity.Value.parts;
            parts &= ~mainPart;
            float num3 = (bIsDead ? 0.85f : 0.6f);

            for (int j = 0; j < 11; j++)
            {
                int num4 = 1 << j;

                if (((uint)parts & (uint)num4) != 0 && entity.rand.RandomFloat <= num3)
                {
                    parts = (EnumBodyPartHit)((int)parts & ~num4);
                }
            }

            DamageSourceEntity damageSourceEntity = new DamageSourceEntity(EnumDamageSource.External, EnumDamageTypes.KnockDown, SourceEntityID, hitEntity.Value.dir, hitEntity.Value.hitTransform.name, Vector3.zero, Vector2.zero);
            damageSourceEntity.bodyParts = mainPart | parts;
            damageSourceEntity.AttackingItem = _itemValueExplosionSource;
            damageSourceEntity.DismemberChance = (bCrit ? 0.5f : 0f);

            int dmgDone = entity.DamageEntity(damageSourceEntity, (int)hitEntity.Value.damage, bCrit);

            if (!entity.Buffs.HasBuff("FuriousRamsayBuffRangedExplosionBonus"))
            {
                Vector3 force = GetRandomForce();
                if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient)
                {
                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer((NetPackage) NetPackageManager.GetPackage<NetPackageEntityRagdoll>().Setup(entity, (byte)EModelBase.RagdollMode.Default, 4f, EnumBodyPartHit.Torso, force, Vector3.zero));
                }
                else
                {
                    entity.emodel.DoRagdoll(EModelBase.RagdollMode.Default, 4f, EnumBodyPartHit.Torso, force, Vector3.zero, entity.isEntityRemote);
                }
            }

            //Log.Out($"entity: {entity}, hitEntity.Value.damage: {hitEntity.Value.damage}, bCrit: {bCrit}, dmgDone: {dmgDone}, dmg source entity id: {SourceEntityID}, {SourceEntity}");

            if (SourceEntity != null)
            {
                MinEventTypes eventType = ((health != entity.Health) ? MinEventTypes.onSelfExplosionDamagedOther : MinEventTypes.onSelfExplosionAttackedOther);
                SourceEntity.MinEventContext.Self = SourceEntity;
                SourceEntity.MinEventContext.Other = entity;
                SourceEntity.MinEventContext.ItemValue = _itemValueExplosionSource;
                _itemValueExplosionSource?.FireEvent(eventType, SourceEntity.MinEventContext);
                SourceEntity.FireEvent(eventType, useInventory: false);
            }

            if (explosionData.BuffActions != null)
            {
                for (int k = 0; k < explosionData.BuffActions.Count; k++)
                {
                    BuffClass buff = BuffManager.GetBuff(explosionData.BuffActions[k]);

                    if (buff != null)
                    {
                        float originalValue = 1f;
                        originalValue = EffectManager.GetValue(PassiveEffects.BuffProcChance, null, originalValue, SourceEntity, null, FastTags<TagGroup.Global>.Parse(buff.Name));

                        if (entity.rand.RandomFloat <= originalValue)
                        {
                            entity.Buffs.AddBuff(explosionData.BuffActions[k], SourceEntityID);
                        }
                    }
                }
            }

            if (bIsDead || !entity.IsDead())
            {
                continue;
            }

            AddKillExp(entity);

            if (SourceEntity != null)
            {
                if (SourceEntity.isEntityRemote)
                {
                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageMinEventFire>().Setup(SourceEntity.entityId, entity.entityId, MinEventTypes.onSelfKilledOther, _itemValueExplosionSource), _onlyClientsAttachedToAnEntity: false, SourceEntity.entityId);
                }
                else
                {
                    SourceEntity.FireEvent(MinEventTypes.onSelfKilledOther);
                }
            }
        }

        if (!flag && SourceEntity != null)
        {
            if (SourceEntity.isEntityRemote)
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageMinEventFire>().Setup(SourceEntity.entityId, -1, MinEventTypes.onSelfExplosionMissEntity, _itemValueExplosionSource), _onlyClientsAttachedToAnEntity: false, SourceEntity.entityId);
            }
            else
            {
                SourceEntity.MinEventContext.Self = SourceEntity;
                SourceEntity.MinEventContext.Other = null;
                SourceEntity.MinEventContext.ItemValue = _itemValueExplosionSource;
                SourceEntity.FireEvent(MinEventTypes.onSelfExplosionMissEntity);
            }
        }

        hitEntities.Clear();
    }

    public void AddKillExp(EntityAlive entity)
    {
        EntityPlayer entityPlayer = SourceEntity as EntityPlayer;

        if (entityPlayer != null)
        {
            entityPlayer.AddKillXP(entity, entityPlayer.inventory.holdingItemItemValue);
        }
    }

    public Vector3 GetRandomForce()
    {
        int xSign = UnityEngine.Random.Range(0, 2) * 2 - 1; // randomly go in the negative direction
        int zSign = UnityEngine.Random.Range(0, 2) * 2 - 1;
        int xStr = UnityEngine.Random.Range(_XZMultiplier.x, _XZMultiplier.y) * xSign;
        int yStr = UnityEngine.Random.Range(_heightMultiplier.x, _heightMultiplier.y);
        int zStr = UnityEngine.Random.Range(_XZMultiplier.x, _XZMultiplier.y) * zSign;
        Vector3 force = new Vector3(_initialForceStrength * xStr, _initialForceStrength * yStr, _initialForceStrength * zStr);
        //Log.Out($"force: {force}");
        return force;
    }

    public struct DamageRecord
    {
        public EntityAlive entity;
        public float damage;
        public Vector3 dir;
        public Transform hitTransform;
        public EnumBodyPartHit mainPart;
        public EnumBodyPartHit parts;
    }

}
