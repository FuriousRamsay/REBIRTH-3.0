/// <summary>Game-thread mesh work admission. Native cosmetic cleanup inside a
/// build is already represented by that build and must not request another one.</summary>
internal sealed class RebirthSdcsMeshRefreshGate
{
    private bool pending;
    internal bool Building { get; private set; }

    internal void Request() { if (!Building) pending = true; }
    internal bool TakeRequest()
    {
        if (!pending || Building) return false;
        pending = false;
        return true;
    }
    internal bool BeginBuild()
    {
        if (Building) return false;
        Building = true;
        pending = false;
        return true;
    }
    internal void EndBuild() { Building = false; }
}
