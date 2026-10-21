using System;

// Absolute fuel state commands must not reset the processing clock when the
// requested state already holds. Durable command settlement is separate.
internal static class RebirthCamperStationFuelState
{
    internal static bool Apply(RebirthCamperStationState state,bool burning)
    {
        if(state==null)throw new ArgumentNullException(nameof(state));
        if(state.IsBurning==burning)return false;
        state.IsBurning=burning;
        state.ResetTickTime();
        return true;
    }
}
