using UnityEngine;

public abstract partial class EntityRebirthHumanoidNPC
{
    // 2.6 SDCS humans inherited EntityAlive's live-actor push behavior.
    // The 3.x EntityNPC intermediate base disables it for all NPCs; restore
    // the former human behavior so close companions can yield to one another.
    public override bool CanBePushed()
    {
        return !IsDead();
    }

    // The SDCS avatar uses commanded forward/strafe values. Native 3.x skips
    // publishing those values for non-root-motion actors after moving them.
    // 2.6 published them after the same native physics move.
    public override void MoveEntityHeaded(Vector3 direction, bool isDirAbsolute)
    {
        base.MoveEntityHeaded(direction, isDirAbsolute);
        if (isEntityRemote || RootMotion || AttachedToEntity != null || jumpIsMoving)
            return;

        // 3.x EntityAlive converts an absolute path command to local facing
        // before feeding root-motion avatars. This non-root-motion feed must
        // use the same axes; otherwise it competes with the local displacement
        // blend in updateSpeedForwardAndStrafe as the NPC turns along a path.
        if (isDirAbsolute)
            direction = transform.InverseTransformDirection(moveDirection);

        float animationSpeed = landMovementFactor * 2.5f;
        if (inWaterPercent > 0.3f)
        {
            if (animationSpeed > 0.01f)
            {
                float t = (inWaterPercent - 0.3f) * 1.4285715f;
                animationSpeed = Mathf.Lerp(animationSpeed, 0.01f + (animationSpeed - 0.01f) * 0.1f, t);
            }
            if (isSwimming) animationSpeed = landMovementFactor * 5f;
        }

        float magnitude = direction.magnitude;
        if (magnitude > 1f) animationSpeed /= magnitude;
        float forward = direction.z * animationSpeed;
        if (lerpForwardSpeed)
        {
            if (Utils.FastAbs(speedForwardTarget - forward) > 0.05f)
                speedForwardTargetStep = Utils.FastAbs(forward - speedForward);
            speedForwardTarget = forward;
        }
        else speedForward = forward;
        speedStrafe = direction.x * animationSpeed;
        SetMovementState();
        ReplicateSpeeds();
    }
}
