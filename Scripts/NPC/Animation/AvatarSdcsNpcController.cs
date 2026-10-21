// Player-rig motion admission and lifecycle safeguards reference NPCCore-main/0-XNPCCore.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class AvatarSdcsNpcController : LegacyAvatarController
{
    public Transform meshTransform;
    public HashSet<int> hitStates;
    public AnimatorStateInfo painLayer;
    public AnimatorStateInfo LocomotionLayer;

    public bool bNewModel;
    public static bool DebugLogging = false;
    private float debugLogTimer = 0f;
    private readonly RebirthSdcsLocomotionSampler locomotionSampler = new RebirthSdcsLocomotionSampler();


    public override void assignStates()
    {
        jumpState = Animator.StringToHash("Base Layer.Jump");
        fpvJumpState = Animator.StringToHash("Base Layer.FPVFemaleJump");
        AvatarCharacterController.GetThirdPersonDeathStates(deathStates = new HashSet<int>());
        AvatarCharacterController.GetThirdPersonReloadStates(reloadStates = new HashSet<int>());
        AvatarCharacterController.GetThirdPersonHitStates(hitStates = new HashSet<int>());
    }

    public void assignParts(bool _bFPV)
    {
        if (bipedTransform == null) return;
        if (!_bFPV)
        {
            pelvis = bipedTransform.FindInChilds("Hips");
            spine = pelvis != null ? pelvis.Find("Spine") : null;
            spine1 = spine != null ? spine.Find("Spine1") : null;
            spine2 = spine1 != null ? spine1.Find("Spine2") : null;
            spine3 = spine2 != null ? spine2.Find("Spine3") : null;
            head = spine3 != null ? spine3.Find("Neck/Head") : null;
            cameraNode = head != null ? head.Find("CameraNode") : null;
            rightHand = bipedTransform.FindInChilds("RightWeapon");
        }
        else
        {
            bNewModel = bipedTransform.FindInChilds("Origin") != null;

            if (!bNewModel)
            {
                pelvis = bipedTransform.Find("Hips");
                spine = pelvis != null ? pelvis.Find("Spine") : null;
                spine1 = spine != null ? spine.Find("Spine1") : null;
                spine2 = spine1 != null ? spine1.Find("Spine2") : null;
                spine3 = spine2 != null ? spine2.Find("Spine3") : null;
                head = spine3 != null ? spine3.Find("Neck/Head") : null;
                cameraNode = head != null ? head.Find("CameraNode") : null;
                cameraNode = spine3;
                rightHand = bipedTransform.FindInChilds("RightWeapon");
            }
            else
            {
                pelvis = null;
                spine = null;
                spine1 = null;
                spine2 = null;
                spine3 = null;
                head = null;
                cameraNode = null;
                rightHand = bipedTransform.FindInChilds("RightWeapon");
            }
        }

        meshTransform = bipedTransform.FindInChilds("body");

        if (meshTransform == null)
        {
            meshTransform = bipedTransform.FindInChilds("TraderBob");
        }
    }

    public override void Update()
    {
        if (anim == null && m_bVisible)
        {
            SetAnimator(GetComponentInChildren<Animator>());
        }

        if (entity == null || anim == null || anim.avatar == null) return;

        //base.Update();

        AvatarControllerBaseUpdate(); // replacement for AvatarController base.Update()

        LegacyAvatarControllerBaseUpdate(); // replacement for LegacyAvatarController base.Update()
    }

    public void LegacyAvatarControllerBaseUpdate()
    {
        if (timeAttackAnimationPlaying > 0f)
        {
            timeAttackAnimationPlaying -= Time.deltaTime;

            if (timeAttackAnimationPlaying <= 0f)
            {
                isAttackImpact = true;
            }
        }

        if (timeUseAnimationPlaying > 0f)
        {
            timeUseAnimationPlaying -= Time.deltaTime;
        }

        if (timeHarestingAnimationPlaying > 0f)
        {
            timeHarestingAnimationPlaying -= Time.deltaTime;

            if (timeHarestingAnimationPlaying <= 0f && anim != null)
            {
                _setBool(AvatarController.harvestingHash, _value: false);
            }
        }

        if (timeSpecialAttack2Playing > 0f)
        {
            timeSpecialAttack2Playing -= Time.deltaTime;
        }

        if ((!m_bVisible && (!entity || !entity.RootMotion || entity.isEntityRemote)) || bipedTransform == null || !bipedTransform.gameObject.activeInHierarchy || anim == null || anim.avatar == null || !anim.avatar.isValid || !anim.enabled)
        {
            //Debug.Log($"SdcsNpc AvatarSdcsNpcController::Update - entity: {entity}, ending Update m_bVisible: {m_bVisible}, bipedTransform: {bipedTransform}, anim: {anim}");
            return;
        }

        updateLayerStateInfo();
        setLayerWeights();
        int value = entity.inventory.holdingItem.HoldType.Value;

        _setInt(AvatarController.weaponHoldTypeHash, value);
        float speedForward = entity.speedForward;
        float speedStrafe = entity.speedStrafe;
        float num = speedStrafe;

        if (num >= 1234f)
        {
            num = 0f;
        }

        float fasterSpeed = speedForward * 12.2f;
        float animatedForward = fasterSpeed, animatedStrafe = num;
        // Measuring a locally root-driven rig feeds animation back into its own
        // movement. Retain its commanded feed; measure code-driven/remote bodies.
        if ((!entity.RootMotion || entity.isEntityRemote) && !entity.IsDead() && !entity.emodel.IsRagdollActive)
        {
            if (locomotionSampler.Sample(entity.position.x, entity.position.z, entity.rotation.y, Time.time,
                out float measuredForward, out float measuredStrafe))
            { animatedForward = measuredForward; animatedStrafe = measuredStrafe; }
        }
        else locomotionSampler.Reset();

        _setFloat(AvatarController.forwardHash, animatedForward, false);
        _setFloat(AvatarController.strafeHash, animatedStrafe, false);

        if (!entity.IsDead())
        {
            if (movementStateOverride != -1)
            {
                _setInt(AvatarController.movementStateHash, movementStateOverride);
                movementStateOverride = -1;
            }
            else if (speedStrafe >= 1234f)
            {
                _setInt(AvatarController.movementStateHash, 4);
            }
            else
            {
                _setInt(AvatarController.movementStateHash, entity.MovementState);
            }
        }

        if (IsMoving(animatedForward, animatedStrafe))
        {
            idleTime = 0f;
            _setBool(AvatarController.isMovingHash, _value: true);
        }
        else
        {
            _setBool(AvatarController.isMovingHash, _value: false);
        }

        if (DebugLogging)
        {
            debugLogTimer -= Time.deltaTime;
            if (debugLogTimer <= 0f)
            {
                debugLogTimer = 1f;
                float num2 = speedForward * speedForward + (speedStrafe >= 1234f ? 0f : speedStrafe) * (speedStrafe >= 1234f ? 0f : speedStrafe);
                Log.Out($"[SdcsNpcAnim] id={entity.entityId}" +
                        $" isRemote={entity.isEntityRemote}" +
                        $" speedFwd={speedForward:F3}" +
                        $" fasterSpeed={fasterSpeed:F3}" +
                        $" speedMagSq={num2:F3}" +
                        $" entity.MovementState={entity.MovementState}" +
                        $" moveSpeed={entity.moveSpeed:F3}" +
                        $" moveSpeedAggro={entity.moveSpeedAggro:F3}" +
                        $" animFwd={anim.GetFloat(AvatarController.forwardHash):F3}" +
                        $" isMoving={anim.GetBool(AvatarController.isMovingHash)}" +
                        $" RootMotion={entity.RootMotion}" +
                        $" m_bVisible={m_bVisible}");

                bool foundMovementState = false;
                var sb = new System.Text.StringBuilder("[SdcsNpcAnim] AnimParams id=" + entity.entityId + ": ");
                foreach (var p in anim.parameters)
                {
                    sb.Append(p.name + "(" + p.type + ")=");
                    switch (p.type)
                    {
                        case UnityEngine.AnimatorControllerParameterType.Int:   sb.Append(anim.GetInteger(p.nameHash)); break;
                        case UnityEngine.AnimatorControllerParameterType.Float: sb.Append(anim.GetFloat(p.nameHash).ToString("F2")); break;
                        case UnityEngine.AnimatorControllerParameterType.Bool:  sb.Append(anim.GetBool(p.nameHash)); break;
                        default: sb.Append("?"); break;
                    }
                    sb.Append(" ");
                    if (p.name == "MovementState") foundMovementState = true;
                }
                sb.Append($"| MovementStateParamExists={foundMovementState}");
                Log.Out(sb.ToString());
            }
        }

        if (useIdle)
        {
            float num3 = idleTime - idleTimeSent;
            if (num3 * num3 > 0.25f)
            {
                idleTimeSent = idleTime;
                _setFloat(AvatarController.idleTimeHash, idleTime, false);
            }
            idleTime += Time.deltaTime;
        }

        TryGetFloat(AvatarController.rotationPitchHash, out var _value);
        float value2 = Mathf.Lerp(_value, entity.rotation.x, Time.deltaTime * 12f);
        _setFloat(AvatarController.rotationPitchHash, value2, false);
        TryGetFloat(AvatarController.yLookHash, out var _value2);
        UpdateFloat(AvatarController.yLookHash, Mathf.Lerp(_value2, (0f - base.Entity.rotation.x) / 90f, Time.deltaTime * 12f), _netsync: false);
    }

    public void AvatarControllerBaseUpdate()
    {
        float deltaTime = Time.deltaTime;

        if (electrocuteTime > 0f)
        {
            electrocuteTime -= deltaTime;
        }
        else if (electrocuteTime <= 0f)
        {
            Electrocute(enabled: false);
            electrocuteTime = 0f;
        }

        if (anim == null || hitLayerIndex < 0 || hitLayerIndex >= anim.layerCount)
        {
            return;
        }

        if (hitWeightTarget > 0f && hitWeight == hitWeightTarget)
        {
            if (hitDuration > 999f)
            {
                if (!IsAnimationHitRunning() || entity.IsDead() || entity.emodel.IsRagdollActive)
                {
                    hitWeightDuration = 0.4f;
                    hitWeightTarget = 0f;
                }
            }
            else if (hitWeightTarget > 0.15f)
            {
                hitWeightDuration = (hitDurationOut + 0.2f) / (hitWeight - 0.15f);
                hitWeightTarget = 0.15f;
            }
            else
            {
                hitWeightDuration = 4f;
                hitWeightTarget = 0f;
            }
        }

        if (hitWeight != hitWeightTarget)
        {
            hitWeight = Mathf.MoveTowards(hitWeight, hitWeightTarget, deltaTime / hitWeightDuration);
            anim.SetLayerWeight(hitLayerIndex, hitWeight);
        }

    }

    public override void SetInRightHand(Transform _transform)
    {
        base.SetInRightHand(_transform);
    }

    public override void setLayerWeights()
    {
        if (anim == null)
        {
            return;
        }

        int layerCount = anim.layerCount;
        if (entity.IsDead())
        {
            if (layerCount > 1) anim.SetLayerWeight(1, 0f);
            if (layerCount > 2) anim.SetLayerWeight(2, 0f);
            if (layerCount > 3) anim.SetLayerWeight(3, 0f);
            return;
        }

        if (layerCount > 3) anim.SetLayerWeight(3, 1f);
        if (layerCount <= 2) return;

        if (anim.GetBool("MinibikeIdle"))
        {
            anim.SetLayerWeight(1, 0f);
            anim.SetLayerWeight(2, 0f);
        }
        else if (!anim.IsInTransition(1) && IsTwoHanded())
        {
            anim.SetLayerWeight(1, 0f);
            anim.SetLayerWeight(2, 1f);
        }
        else if (!anim.IsInTransition(2))
        {
            anim.SetLayerWeight(1, 1f);
            anim.SetLayerWeight(2, 0f);
        }
    }

    public override void updateLayerStateInfo()
    {
        if (anim != null)
        {
            int count = anim.layerCount;
            baseStateInfo = count > 0 ? anim.GetCurrentAnimatorStateInfo(0) : default;
            int holdLayer = IsTwoHanded() ? 2 : 1;
            currentWeaponHoldLayer = count > holdLayer ? anim.GetCurrentAnimatorStateInfo(holdLayer) : default;
            painLayer = count > 4 ? anim.GetCurrentAnimatorStateInfo(4) : default;
            LocomotionLayer = count > 5 ? anim.GetCurrentAnimatorStateInfo(5) : default;
            //Debug.Log($"LocomotionLayer: {LocomotionLayer.IsName("Locomotion")}, fullPathHash: {LocomotionLayer.fullPathHash}");
        }
    }

    public override void SwitchModelAndView(string _modelName, bool _bFPV, bool _bMale)
    {
        if (modelTransform == null) return;
        string n = (_bFPV ? "baseRigFP" : _modelName);
        Transform transform = modelTransform.FindInChilds(n);

        if (transform == null && _bFPV)
        {
            transform = modelTransform.Find(_modelName);
        }

        if (transform == null) return;

        if (bipedTransform != null && bipedTransform != transform)
        {
            bipedTransform.gameObject.SetActive(value: false);
        }

        bipedTransform = transform;
        bipedTransform.gameObject.SetActive(value: true);
        modelName = _modelName;
        bMale = _bMale;
        bFPV = _bFPV;
        assignParts(bFPV);

        // A replacement rig must not retain the old rig's animator reference.
        SetAnimator(bipedTransform.GetComponentInChildren<Animator>());
        locomotionSampler.Reset();
        if (anim == null) return;

        if (HasParameter("IsMale"))
        {
            _setBool("IsMale", _bMale);
        }

        if (anim != null)
        {
            anim.logWarnings = false;
            anim.GetBool(AvatarController.isDeadHash);
            anim.GetInteger(AvatarController.weaponHoldTypeHash);
        }

        if ((bool)rightHandItemTransform && rightHand != null)
        {
            rightHandItemTransform.SetParent(rightHand);
            var offsets = AnimationGunjointOffsetData.AnimationGunjointOffset;
            int holdType = entity.inventory.holdingItem.HoldType.Value;
            if (offsets != null && holdType >= 0 && holdType < offsets.Length)
            {
                var offset = offsets[holdType];
                rightHandItemTransform.SetLocalPositionAndRotation(offset.position, Quaternion.Euler(offset.rotation));
            }
        }

        SetWalkType(entity.GetWalkType());
        _setBool(AvatarController.isDeadHash, entity.IsDead());
        _setBool(AvatarController.isFPVHash, bFPV);
        _setBool(AvatarController.isAliveHash, entity.IsAlive());
    }

    public bool HasParameter(string paramName)
    {
        if (anim == null) return false;
        AnimatorControllerParameter[] parameters = anim.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == paramName)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsTwoHanded()
    {
        var delays = AnimationDelayData.AnimationDelay;
        int holdType = entity.inventory.holdingItem.HoldType.Value;
        return delays != null && holdType >= 0 && holdType < delays.Length && delays[holdType].TwoHanded;
    }

    public override void updateSpineRotation()
    {
    }

    public override Transform GetRightHandTransform()
    {
        if (rightHand == null && bipedTransform != null)
        {
            rightHand = bipedTransform.FindInChilds("RightWeapon");
        }

        return rightHand;
    }

    public override Transform GetMeshTransform()
    {
        if (!(meshTransform != null))
        {
            return bipedTransform;
        }

        return meshTransform;
    }

    public override bool IsAnimationHitRunning()
    {
        if (!base.IsAnimationHitRunning())
        {
            return hitStates != null && hitStates.Contains(painLayer.fullPathHash);
        }

        return true;
    }

    public override void LateUpdate()
    {
        if (entity == null || bipedTransform == null || !bipedTransform.gameObject.activeInHierarchy || anim == null || !anim.enabled)
        {
            return;
        }

        updateLayerStateInfo();
        updateSpineRotation();

        var actions = entity.inventory.holdingItem.Actions;
        var actionData = entity.inventory.holdingItemData?.actionData;
        if (actions != null && actions.Length > 0 && actionData != null && actionData.Count > 0 && actions[0] != null && actionData[0] != null)
        {
            actions[0].UpdateNozzleParticlesPosAndRot(actionData[0]);
        }

        if (actions != null && actions.Length > 1 && actionData != null && actionData.Count > 1 && actions[1] != null && actionData[1] != null)
        {
            actions[1].UpdateNozzleParticlesPosAndRot(actionData[1]);
        }

        if (anim.GetBool(AvatarController.isDeadHash) && !anim.IsInTransition(0))
        {
            _setBool(AvatarController.isDeadHash, _value: false);
        }

        if (anim.layerCount > 2 && !anim.IsInTransition(2) && anim.GetBool("Reload") && reloadStates.Contains(currentWeaponHoldLayer.fullPathHash) && rightHandAnimator != null)
        {
            rightHandAnimator.SetBool("Reload", value: false);
        }

        bool num = anim.layerCount > 4 && anim.IsInTransition(4);
        int integer = anim.GetInteger(AvatarController.hitBodyPartHash);
        bool flag = IsAnimationHitRunning();

        if (!num && integer != 0 && flag)
        {
            _setInt(AvatarController.hitBodyPartHash, 0);
            _setBool("isCritical", _value: false);
        }

        if (anim != null && anim.GetBool(AvatarController.itemUseHash) && --itemUseTicks <= 0)
        {
            _setBool(AvatarController.itemUseHash, _value: false);

            if (rightHandAnimator != null)
            {
                rightHandAnimator.SetBool(AvatarController.itemUseHash, value: false);
            }
        }

        if (isInDeathAnim && deathStates.Contains(baseStateInfo.fullPathHash) && !anim.IsInTransition(0))
        {
            didDeathTransition = true;
        }

        if (isInDeathAnim && didDeathTransition && (baseStateInfo.normalizedTime >= 1f || anim.IsInTransition(0)))
        {
            isInDeathAnim = false;
            if (entity.HasDeathAnim)
            {
                entity.emodel.DoRagdoll(DamageResponse.New(_fatal: true));
            }
        }

        if (isInDeathAnim && entity.HasDeathAnim && entity.RootMotion && entity.isCollidedHorizontally)
        {
            isInDeathAnim = false;
            entity.emodel.DoRagdoll(DamageResponse.New(_fatal: true));
        }

        _setBool(AvatarController.isFPVHash, bFPV);
    }

    public override bool IsAnimationWithMotionRunning()
    {
        // The player SDCS controller does not promise legacy Move-tagged states.
        // EntityMoveHelper otherwise zeros commanded movement before the blend
        // tree can leave idle. Sleeping, stun and ragdoll have separate native
        // guards in that method. Match NPCCore's player-rig motion admission.
        return true;
    }

}
