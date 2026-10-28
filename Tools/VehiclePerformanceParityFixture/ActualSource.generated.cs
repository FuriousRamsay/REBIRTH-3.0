public static class ActualSource {
    internal static float WheelTorqueRatio(float radius) => radius / 0.63f;
    internal static float DirectAcceleration(float multiplier, float signedSpeed, float selectedVelocity)
    {
        if (selectedVelocity <= 0.01f) return 0f;
        float fade = Mathf.Clamp01(1f - Mathf.Max(0f, signedSpeed) / Mathf.Max(1f, selectedVelocity * 0.9f));
        return Mathf.Max(0f, multiplier - 1f) * 6f * fade;
    }
    internal static float DirectBrake(float multiplier) => Mathf.Max(0f, multiplier - 1f) * 10f;
    internal static float DirectHill(float multiplier, float uphill) => Mathf.Clamp((multiplier - 1f) * uphill * 2f, 0f, 28f);
    internal static float SelectedVelocity(EntityVehicle entity, Vehicle vehicle, RebirthVehiclePerformanceSnapshot p)
    {
        MovementInput input = entity.movementInput;
        bool reverse = input != null && input.moveForward < 0f;
        bool turbo = input != null && input.running && vehicle.CanTurbo && input.moveForward != 0f;
        if (reverse) return (turbo ? vehicle.VelocityMaxTurboBackward : vehicle.VelocityMaxBackward) * p.ReverseVelocityMultiplier;
        return turbo ? vehicle.VelocityMaxTurboForward * p.TurboVelocityMultiplier : vehicle.VelocityMaxForward * p.VelocityMultiplier;
    }
    internal static bool HasPhysicsModifiers(RebirthVehiclePerformanceSnapshot p)
    {
        return p != null && (p.AccelerationMultiplier != 1f || p.TorqueMultiplier != 1f ||
            p.VelocityMultiplier != 1f || p.TurboVelocityMultiplier != 1f ||
            p.ReverseVelocityMultiplier != 1f || p.BrakingMultiplier != 1f ||
            p.HandlingMultiplier != 1f || p.HillClimbMultiplier != 1f || !(p.ResponseMultiplier <= 1f));
    }
}