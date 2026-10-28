// Explicit seen persistence adapter; actual checkpoint has its own composition runner.
internal static class RebirthHydrationTerminalSeenCheckpoint {
 internal static bool Allow=true;
 internal static bool TryPersist(RebirthHydrationRemoteClientTerminal terminal)=>Allow;
 internal static bool HasSaved(RebirthHydrationRemoteClientGrant grant)=>Allow;
}
