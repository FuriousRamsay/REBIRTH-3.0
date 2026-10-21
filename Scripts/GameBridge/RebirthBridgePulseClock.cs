// Native input injection witness for a discrete bridge /press request.
internal sealed class RebirthBridgePulseClock
{
    internal readonly float Seconds;
    private readonly float pendingUntil;
    internal int InjectedFrames{get;private set;}
    internal int FirstFrame{get;private set;}=-1;
    internal int LastFrame{get;private set;}=-1;
    internal float FirstTime{get;private set;}
    internal float LastTime{get;private set;}
    internal RebirthBridgePulseClock(float queuedAt,float seconds){Seconds=seconds;pendingUntil=queuedAt+1;}
    internal bool Expired(float now)=>InjectedFrames==0?now>pendingUntil:Seconds>0&&now>FirstTime+Seconds;
    internal void Injected(float now,int frame)
    {
        if(InjectedFrames==0){FirstTime=now;FirstFrame=frame;}
        if(LastFrame!=frame)InjectedFrames++;
        LastTime=now;LastFrame=frame;
    }
}
