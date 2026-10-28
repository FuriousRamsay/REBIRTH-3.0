using System;using System.Collections.Generic;using UnityEngine;
public static class RebirthVehicleRuntimePerformance
{
    private sealed class WheelNormalization
    {
        internal readonly Dictionary<WheelCollider, float[]> Original = new Dictionary<WheelCollider, float[]>();
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityVehicle, WheelNormalization> CamperWheels =
        new System.Runtime.CompilerServices.ConditionalWeakTable<EntityVehicle, WheelNormalization>();

    private static void NormalizeCamperWheels(EntityVehicle entity)
    {
        if (entity.vehicle.GetName() != "rebirthCamper80s" || entity.wheels == null) return;
        WheelNormalization state = CamperWheels.GetValue(entity, ignored => new WheelNormalization());
        for (int i = 0; i < entity.wheels.Length; i++)
        {
            EntityVehicle.Wheel wheel = entity.wheels[i];
            if (wheel == null || wheel.wheelC == null || wheel.wheelC.radius <= 0f) continue;
            float[] original;
            if (!state.Original.TryGetValue(wheel.wheelC, out original))
            {
                original = new[] { wheel.motorTorqueScale, wheel.brakeTorqueScale };
                state.Original.Add(wheel.wheelC, original);
            }
            // Native 4x4 uses .63m wheels. Preserve this truck's visible/physical radius,
            // but cancel the smaller radius' extra contact force for identical XML torque.
            float ratio = WheelTorqueRatio(wheel.wheelC.radius);
            wheel.motorTorqueScale = original[0] * ratio;
            wheel.brakeTorqueScale = original[1] * ratio;
            wheel.wheelC.wheelDampingRate = 0.7f;
            WheelFrictionCurve side = wheel.sideFriction;
            side.stiffness = i < 2 ? 1.8f : 1.7f;
            wheel.sideStiffnessBase = side.stiffness;
            wheel.sideFriction = side;
            wheel.wheelC.sidewaysFriction = side;
        }
    }

    public struct PhysicsState
    {
        public bool Captured;
        public Vehicle Vehicle;
        public float MotorTorqueForward, MotorTorqueBackward, MotorTorqueTurboForward, MotorTorqueTurboBackward;
        public float VelocityMaxForward, VelocityMaxBackward, VelocityMaxTurboForward, VelocityMaxTurboBackward;
        public float BrakeTorque, SteerRate, SteerCenteringRate;
        public float ResponseMultiplier, ResponseTarget, ResponseBaseRate;
        public float AccelerationAssist, HillAssist, BrakeMultiplier;
        public bool CanOperate;
    }

    internal static bool HasPhysicsModifiers(RebirthVehiclePerformanceSnapshot p)
    {
        return p != null && (p.AccelerationMultiplier != 1f || p.TorqueMultiplier != 1f ||
            p.VelocityMultiplier != 1f || p.TurboVelocityMultiplier != 1f ||
            p.ReverseVelocityMultiplier != 1f || p.BrakingMultiplier != 1f ||
            p.HandlingMultiplier != 1f || p.HillClimbMultiplier != 1f || !(p.ResponseMultiplier <= 1f));
    }

    public static void BeforePhysics(EntityVehicle entity, out PhysicsState state)
    {
        state = default(PhysicsState);
        if (entity == null || entity.vehicle == null || entity.isEntityRemote) return;
        NormalizeCamperWheels(entity);
        RebirthVehicleAssembly assembly;
        if (!RebirthVehicleAssemblyIndex.TryGetEntity(entity, out assembly) || assembly == null || assembly.Performance == null) return;

        Vehicle vehicle = entity.vehicle;
        RebirthVehiclePerformanceSnapshot p = assembly.Performance;
        if (!HasPhysicsModifiers(p)) return;
        state = new PhysicsState
        {
            Captured = true,
            Vehicle = vehicle,
            MotorTorqueForward = vehicle.MotorTorqueForward,
            MotorTorqueBackward = vehicle.MotorTorqueBackward,
            MotorTorqueTurboForward = vehicle.MotorTorqueTurboForward,
            MotorTorqueTurboBackward = vehicle.MotorTorqueTurboBackward,
            VelocityMaxForward = vehicle.VelocityMaxForward,
            VelocityMaxBackward = vehicle.VelocityMaxBackward,
            VelocityMaxTurboForward = vehicle.VelocityMaxTurboForward,
            VelocityMaxTurboBackward = vehicle.VelocityMaxTurboBackward,
            BrakeTorque = vehicle.brakeTorque,
            SteerRate = vehicle.steerRate,
            SteerCenteringRate = vehicle.steerCenteringRate,
            ResponseMultiplier = p.ResponseMultiplier,
            CanOperate = p.CanOperate
        };

        float selectedVelocity = SelectedVelocity(entity, vehicle, p);
        float speedRatio = selectedVelocity > 0.01f ? Mathf.Clamp01(Mathf.Abs(vehicle.CurrentForwardVelocity) / selectedVelocity) : 0f;
        float lowSpeedFactor = 1f - speedRatio;
        // Terrain sampling cannot affect torque when the hill multiplier is neutral.
        float uphillFactor = p.HillClimbMultiplier == 1f ? 0f : GetUphillFactor(entity);
        state.BrakeMultiplier = p.BrakingMultiplier;
        if (p.CanOperate && CanAssist(entity))
        {
            float input = entity.movementInput.moveForward;
            if (Mathf.Abs(input) > 0.01f && selectedVelocity > 0.01f)
            {
                float speed = Vector3.Dot(entity.vehicleRB.velocity, entity.PhysicsTransform.forward) * Mathf.Sign(input);
                state.AccelerationAssist = DirectAcceleration(p.AccelerationMultiplier, speed, selectedVelocity);
            }
            if (input > 0f)
                state.HillAssist = DirectHill(p.HillClimbMultiplier, uphillFactor);
        }

        float forwardTorqueMultiplier = p.AccelerationMultiplier;
        forwardTorqueMultiplier *= 1f + (p.TorqueMultiplier - 1f) * lowSpeedFactor;
        forwardTorqueMultiplier *= 1f + (p.HillClimbMultiplier - 1f) * uphillFactor;
        float reverseTorqueMultiplier = p.AccelerationMultiplier * (1f + (p.TorqueMultiplier - 1f) * lowSpeedFactor);

        vehicle.MotorTorqueForward *= forwardTorqueMultiplier;
        vehicle.MotorTorqueTurboForward *= forwardTorqueMultiplier;
        vehicle.MotorTorqueBackward *= reverseTorqueMultiplier;
        vehicle.MotorTorqueTurboBackward *= reverseTorqueMultiplier;
        vehicle.VelocityMaxForward *= p.VelocityMultiplier;
        vehicle.VelocityMaxTurboForward *= p.TurboVelocityMultiplier;
        vehicle.VelocityMaxBackward *= p.ReverseVelocityMultiplier;
        vehicle.VelocityMaxTurboBackward *= p.ReverseVelocityMultiplier;
        vehicle.brakeTorque *= p.BrakingMultiplier;
        vehicle.steerRate *= p.HandlingMultiplier;
        vehicle.steerCenteringRate *= p.HandlingMultiplier;

        if (p.ResponseMultiplier > 1f)
        {
            state.ResponseTarget = selectedVelocity * vehicle.EffectVelocityMaxPer;
            state.ResponseBaseRate = state.ResponseTarget > entity.velocityMax ? 2.5f : 1.5f;
        }
    }

    public static void AfterPhysics(EntityVehicle entity, PhysicsState state)
    {
        RestorePhysics(entity, state);
        if (entity == null || !state.Captured) return;
        if (state.CanOperate && state.ResponseMultiplier > 1f && state.ResponseTarget > 0.01f)
        {
            float extraRate = (state.ResponseMultiplier - 1f) * state.ResponseBaseRate;
            entity.velocityMax = Mathf.MoveTowards(entity.velocityMax, state.ResponseTarget, extraRate * Time.deltaTime);
        }
        if (!state.CanOperate || !CanAssist(entity)) return;
        Rigidbody rb = entity.vehicleRB;
        Vector3 forward = entity.PhysicsTransform.forward;
        float input = entity.movementInput.moveForward;
        // Keep the legacy direct assists in native acceleration units, not mass-scaled torque.
        if (state.AccelerationAssist > 0.01f && Mathf.Abs(input) > 0.01f && !entity.movementInput.jump && entity.brakeTorque <= 0.01f)
            rb.AddForce(forward * Mathf.Sign(input) * state.AccelerationAssist, ForceMode.Acceleration);
        if (state.HillAssist > 0.01f && input > 0f && !entity.movementInput.jump && entity.brakeTorque <= 0.01f)
            rb.AddForce(forward * state.HillAssist, ForceMode.Acceleration);
        Vector3 horizontal = rb.velocity;
        horizontal.y = 0f;
        float forwardSpeed = Vector3.Dot(rb.velocity, forward);
        bool braking = entity.brakeTorque > 0.01f || entity.movementInput.jump ||
            (input > 0.01f && forwardSpeed < -0.5f) || (input < -0.01f && forwardSpeed > 0.5f);
        if (braking && horizontal.sqrMagnitude > 0.25f && state.BrakeMultiplier > 1f)
            rb.AddForce(-horizontal.normalized * DirectBrake(state.BrakeMultiplier), ForceMode.Acceleration);
    }

    private static bool CanAssist(EntityVehicle entity)
    {
        return entity != null && !entity.isEntityRemote && entity.RBActive && entity.IsEngineRunning &&
            entity.vehicleRB != null && !entity.vehicleRB.isKinematic && entity.PhysicsTransform != null &&
            entity.movementInput != null && entity.GetWheelsOnGround() > 0;
    }

    internal static float WheelTorqueRatio(float radius) => radius / 0.63f;
    internal static float DirectAcceleration(float multiplier, float signedSpeed, float selectedVelocity)
    {
        if (selectedVelocity <= 0.01f) return 0f;
        float fade = Mathf.Clamp01(1f - Mathf.Max(0f, signedSpeed) / Mathf.Max(1f, selectedVelocity * 0.9f));
        return Mathf.Max(0f, multiplier - 1f) * 6f * fade;
    }
    internal static float DirectBrake(float multiplier) => Mathf.Max(0f, multiplier - 1f) * 10f;
    internal static float DirectHill(float multiplier, float uphill) => Mathf.Clamp((multiplier - 1f) * uphill * 2f, 0f, 28f);

    private static float SelectedVelocity(EntityVehicle entity, Vehicle vehicle, RebirthVehiclePerformanceSnapshot p)
    {
        MovementInput input = entity.movementInput;
        bool reverse = input != null && input.moveForward < 0f;
        bool turbo = input != null && input.running && vehicle.CanTurbo && input.moveForward != 0f;
        if (reverse) return (turbo ? vehicle.VelocityMaxTurboBackward : vehicle.VelocityMaxBackward) * p.ReverseVelocityMultiplier;
        return turbo ? vehicle.VelocityMaxTurboForward * p.TurboVelocityMultiplier : vehicle.VelocityMaxForward * p.VelocityMultiplier;
    }

    public static void RestorePhysics(EntityVehicle entity, PhysicsState state)
    {
        if (!state.Captured || state.Vehicle == null) return;
        Vehicle vehicle = state.Vehicle;
        vehicle.MotorTorqueForward = state.MotorTorqueForward;
        vehicle.MotorTorqueBackward = state.MotorTorqueBackward;
        vehicle.MotorTorqueTurboForward = state.MotorTorqueTurboForward;
        vehicle.MotorTorqueTurboBackward = state.MotorTorqueTurboBackward;
        vehicle.VelocityMaxForward = state.VelocityMaxForward;
        vehicle.VelocityMaxBackward = state.VelocityMaxBackward;
        vehicle.VelocityMaxTurboForward = state.VelocityMaxTurboForward;
        vehicle.VelocityMaxTurboBackward = state.VelocityMaxTurboBackward;
        vehicle.brakeTorque = state.BrakeTorque;
        vehicle.steerRate = state.SteerRate;
        vehicle.steerCenteringRate = state.SteerCenteringRate;

    }

    public static void BeforeFuel(VPEngine engine, out float originalFuelKmPerL)
    {
        originalFuelKmPerL = 0f;
        if (engine == null || engine.vehicle == null || engine.fuelKmPerL <= 0f) return;
        RebirthVehicleAssembly assembly;
        EntityVehicle entity = engine.vehicle.entity as EntityVehicle;
        if (entity == null || !RebirthVehicleAssemblyIndex.TryGetEntity(entity, out assembly) || assembly == null || assembly.Performance == null) return;
        originalFuelKmPerL = engine.fuelKmPerL;
        float consumptionMultiplier = assembly.Performance.FuelUseMultiplier;
        if (engine.vehicle.IsTurbo) consumptionMultiplier *= assembly.Performance.TurboFuelUseMultiplier;
        // Survivor Trait fuel efficiency is resolved from the authoritative completed character of
        // the player currently attached to this vehicle. The vehicle system remains the sole fuel writer.
        consumptionMultiplier *= RebirthTraitGameplayModifierService.GetVehicleFuelUseMultiplier(entity);
        engine.fuelKmPerL = originalFuelKmPerL / Mathf.Max(0.1f, consumptionMultiplier);
    }

    public static void AfterFuel(VPEngine engine, float originalFuelKmPerL)
    {
        RestoreFuel(engine, originalFuelKmPerL);
    }

    public static void RestoreFuel(VPEngine engine, float originalFuelKmPerL)
    {
        if (engine != null && originalFuelKmPerL > 0f) engine.fuelKmPerL = originalFuelKmPerL;
    }

    private static float GetUphillFactor(EntityVehicle entity)
    {
        if (entity == null || entity.PhysicsTransform == null || entity.movementInput == null || entity.movementInput.moveForward <= 0f) return 0f;
        Vector3 forward = entity.PhysicsTransform.forward;
        float pitch = Mathf.Clamp01(Vector3.Dot(forward, Vector3.up) * 3f);
        RaycastHit hit;
        float ground = 0f;
        if (Physics.Raycast(entity.PhysicsTransform.position + Vector3.up * 0.75f, Vector3.down, out hit, 3f))
        {
            Vector3 uphill = Vector3.ProjectOnPlane(Vector3.up, hit.normal);
            if (uphill.sqrMagnitude > 0.000001f)
            {
                uphill.Normalize();
                float facing = Mathf.Clamp01(Vector3.Dot(forward, uphill));
                float slope = Mathf.Clamp01((1f - Vector3.Dot(hit.normal, Vector3.up)) * 6f);
                ground = facing * slope;
            }
        }
        float vertical = entity.vehicle != null && entity.vehicle.CurrentForwardVelocity > 0.25f && entity.vehicleRB != null
            ? Mathf.Clamp01((entity.vehicleRB.velocity.y - 0.05f) / 1.25f) : 0f;
        return Mathf.Clamp01(Mathf.Max(pitch, Mathf.Max(ground, vertical)));
    }
}
