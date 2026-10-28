// AUTHORED_NOT_RUN. Sender adapters isolate actual intent stage scheduling.
internal static class DispatchProbe {internal static int Sends;internal static string Kind;internal static bool Allow=true;internal static Action Callback;internal static bool Send(string kind){Sends++;Kind=kind;Callback?.Invoke();return Allow;}}
internal static class RebirthHydrationIntentClient {internal static bool TrySend(RebirthHydrationProposalClient.Intent intent)=>DispatchProbe.Send("intent");}
internal static class RebirthHydrationPreparedClient {internal static bool TrySend(RebirthHydrationProposalClient.Intent intent)=>DispatchProbe.Send("prepared");}
internal static class RebirthHydrationDeclineClient {internal static bool TrySend(RebirthHydrationProposalClient.Intent intent)=>DispatchProbe.Send("decline");}
internal static class RebirthHydrationRemoteClientDispatch {internal static bool TryRequestConfirmation(EntityPlayerLocal player)=>DispatchProbe.Send("confirmation");}
internal static class RebirthHydrationAcknowledgmentClient {internal static bool TrySend(EntityPlayerLocal player,Guid operation,string digest)=>DispatchProbe.Send("acknowledgment");}
