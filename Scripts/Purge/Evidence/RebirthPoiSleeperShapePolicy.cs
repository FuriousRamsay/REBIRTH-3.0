// Native Write() toggles only bit16 to serialize MinScript state. Authored
// gameplay flags remain authoritative; save-state data is not a shape change.
internal static class RebirthPoiSleeperShapePolicy
{
    internal const int SavedScriptData=16;
    internal static bool FlagsMatch(int runtime,int authored)=>(runtime&~SavedScriptData)==(authored&~SavedScriptData);
}
