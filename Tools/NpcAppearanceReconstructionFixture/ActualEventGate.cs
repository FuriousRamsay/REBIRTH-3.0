namespace EventGateDoubles { static class ActualEventGate {    internal static bool Prefix(MinEventParams _eventParms)
    {
        return !(_eventParms?.Self is EntityRebirthHumanoidNPC npc&&npc.IsPreparedRestorationPending);
    }}}