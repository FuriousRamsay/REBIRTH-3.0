class Program
{
    static int checks;
    static void Check(bool yes,string label){if(!yes)throw new Exception(label);checks++;}
    static void Main()
    {
        var cadence=new RebirthCamperManualInputCadence();
        Check(cadence.Send(10,false),"First session publishes neutral state");
        Check(!cadence.Send(10.01f,false),"Unchanged idle is throttled");
        Check(cadence.Send(10.012f,true),"Press between heartbeat deadlines is delivered immediately");
        Check(!cadence.Send(10.02f,true),"Held press does not bypass heartbeat repeatedly");
        Check(cadence.Send(10.03f,false),"Release between heartbeat deadlines is delivered immediately");
        Check(!cadence.Send(10.04f,false),"Release does not flood duplicate neutral packets");
        Check(cadence.Send(10.081f,false),"Neutral heartbeat resumes");
        cadence.Reset();Check(cadence.Send(1,false),"New session accepts a reset clock");
        Check(cadence.Send(1.01f,true),"New session press arrives");
        cadence.Reset();Check(cadence.Send(1.02f,false),"Stop/reset never retains pressed state");
        foreach(float phase in new[]{.002f,.012f,.026f,.042f})
        {
            var result=Run(phase,.055f);
            Check(result.rounds==1,"55ms click at every heartbeat phase gives one admitted round");
            Check(result.releaseObserved,"Release is sent at its input frame");
            Check(result.firePackets<=2,"Short click does not manufacture held-fire packets");
        }
        var held=Run(.012f,.4f);
        Check(held.rounds==4,"Held fire retains125ms native cadence rather than packet-rate shots");
        Check(held.firePackets>=7&&held.firePackets<=9,"Held input uses50ms heartbeat");
        Check(held.releaseObserved,"Held release immediately clears fire");
        Console.WriteLine("PASS "+checks+" production manual input cadence checks; native firing remains separately game-tested.");
    }
    static (int rounds,int firePackets,bool releaseObserved) Run(float press,float duration)
    {
        var cadence=new RebirthCamperManualInputCadence();
        int rounds=0,packets=0;float nextShot=-1,inputUntil=-1;bool serverFire=false,release=false;
        for(int frame=0;frame<500;frame++)
        {
            float now=frame*.002f;bool fire=now>=press&&now<press+duration;
            if(cadence.Send(now,fire))
            {
                if(fire)packets++;
                if(serverFire&&!fire)release=true;
                serverFire=fire;inputUntil=now+.25f;
            }
            if(serverFire&&now<inputUntil&&now>=nextShot){rounds++;nextShot=now+.125f;}
        }
        return(rounds,packets,release);
    }
}
