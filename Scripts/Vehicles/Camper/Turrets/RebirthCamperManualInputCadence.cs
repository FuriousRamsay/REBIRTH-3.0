internal sealed class RebirthCamperManualInputCadence
{
    private bool lastFire;
    private float next;
    internal void Reset(){lastFire=false;next=0;}
    internal bool Send(float now,bool fire)
    {
        // Press AND release edges bypass the held-input heartbeat. Otherwise a
        // quick click between heartbeats vanishes, or release keeps firing late.
        if(fire==lastFire&&now<next)return false;
        lastFire=fire;next=now+.05f;return true;
    }
}
