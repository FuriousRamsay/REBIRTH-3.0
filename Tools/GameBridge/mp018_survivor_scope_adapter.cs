// AUTHORED_NOT_RUN. Explicit Survivor-only scope adapter for the detached
// transaction scenarios. These scenarios exercise actual custody/application/
// persistence sources, not generation enrollment or scope authentication.
// mp018_native_generation instead embeds the ACTUAL scope implementation.
// Generic fixture-field access accommodates the separate native owner/server
// adapter models; none of this reflection is production code.
internal static class RebirthSurvivorMode
{
    internal static bool Enabled=true;
    internal static bool IsEnabledForCurrentWorld()=>Enabled;
}
internal static class RebirthHydrationTransferScope
{
    private static string Field<T>(T value,string name)
    {
        if(ReferenceEquals(value,null))return null;
        for(var type=value.GetType();type!=null;type=type.BaseType)
        {
            var field=type.GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly);
            if(field!=null)return field.GetValue(value) as string;
        }
        return null;
    }
    internal static bool TryCapture<T>(T player,out string creation)
    {
        creation=null;
        return ThreadManager.IsMainThread()&&RebirthSurvivorMode.Enabled&&RebirthSurvivorRequestScope.TryNormalize(Field(player,"Creation"),out creation);
    }
    internal static bool TryObserve<T>(T player,out string creation)=>TryCapture(player,out creation);
    internal static bool MatchesState<T>(string creation,T state)
        =>ThreadManager.IsMainThread()&&RebirthSurvivorMode.Enabled&&RebirthSurvivorRequestScope.Matches(creation,Field(state,"SurvivorCreationId"));
}
