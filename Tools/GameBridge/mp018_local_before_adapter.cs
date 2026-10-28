// AUTHORED_NOT_RUN. Schema and disk writer remain the existing explicit
// repository adapter; the actual LocalHydrationBefore qualification is embedded.
public static partial class RebirthMetabolismStateRepository
{
    private static Dictionary<string,RebirthMetabolismState> ReadCandidate(string path)=>DecodeStore(XDocument.Load(path));
}
