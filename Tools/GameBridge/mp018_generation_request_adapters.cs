// AUTHORED_NOT_RUN. Packet mapping/envelope/send are adapters; current native
// request scope/rate limiting and generation projection are actual sources.
internal static class GenerationRequestTransport
{
    internal static int Sends;internal static bool MissingMapping,ThrowAfterSend;
    internal static Action DuringMap,DuringSend;
    internal static void Reset(){Sends=0;MissingMapping=ThrowAfterSend=false;DuringMap=DuringSend=null;}
}
internal static class NetPackageManager
{
    internal static int GetPackageId(Type type){GenerationRequestTransport.DuringMap?.Invoke();if(GenerationRequestTransport.MissingMapping)throw new InvalidOperationException("fixture missing mapping");return 1;}
    internal static T GetPackage<T>() where T:new()=>new T();
}
internal sealed class NetPackageRebirthHydrationGenerationRequest
{
    internal NetPackageRebirthHydrationGenerationRequest Setup(RebirthHydrationGenerationClient.RequestScope scope)
    {if(!scope.Current())throw new InvalidOperationException("fixture request scope changed");return this;}
}
internal sealed partial class ConnectionManager
{
    internal void SendToServer(NetPackageRebirthHydrationGenerationRequest packet)
    {GenerationRequestTransport.Sends++;GenerationRequestTransport.DuringSend?.Invoke();if(GenerationRequestTransport.ThrowAfterSend)throw new InvalidOperationException("fixture send unknown");}
}
