internal sealed class RebirthCamperManualPress
{
    private bool held,pending;
    private float until;
    internal void Observe(bool fire,float now)
    {
        if(fire&&!held){pending=true;until=now+.25f;}
        held=fire;
    }
    // Consume before admission checks: rejected clicks never become delayed shots.
    // Idle and held heartbeats do not extend the original edge's lifetime.
    internal bool Take(float now)
    {
        bool fresh=pending&&now<until;pending=false;return fresh;
    }
}
