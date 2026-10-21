class Program
{
    static int checks;
    static void Check(bool yes,string label){if(!yes)throw new Exception(label);checks++;}
    static void Main()
    {
        foreach(float delay in new[]{.01f,.055f,.1f,.2f,.9f})
        {
            var pulse=new RebirthBridgePulseClock(10,.055f);
            Check(!pulse.Expired(10+delay),"Native first update is not lost because requested55ms elapsed in queue");
            pulse.Injected(10+delay,100);
            Check(pulse.InjectedFrames==1&&pulse.FirstFrame==100&&pulse.LastFrame==100,"Actual injection frame is witnessed");
            Check(!pulse.Expired(10+delay+.05f),"Duration runs from first actual native injection");
            Check(pulse.Expired(10+delay+.06f),"Pulse does not silently extend past requested duration");
            pulse.Injected(10+delay,100);
            Check(pulse.InjectedFrames==1,"Multiple native action updates in one frame count once");
        }
        var unavailable=new RebirthBridgePulseClock(0,.055f);
        Check(unavailable.Expired(1.01f)&&unavailable.InjectedFrames==0,"Missing native updates are bounded and cannot claim successful injection");
        var framePulse=new RebirthBridgePulseClock(0,0);framePulse.Injected(.1f,10);
        Check(!framePulse.Expired(10),"Frame-based pulse remains governed by existing frame expiry");
        var first=new RebirthBridgePulseClock(0,.055f);first.Injected(.1f,10);first.Injected(.14f,11);
        Check(first.InjectedFrames==2&&first.FirstFrame==10&&first.LastFrame==11&&Math.Abs(first.LastTime-.14f)<.0001f,"Witness records distinct native injection frames and time");
        var replacement=new RebirthBridgePulseClock(.11f,.055f);
        Check(replacement.InjectedFrames==0&&first.InjectedFrames==2,"Separate pulses never share or replay injection receipts");
        Console.WriteLine("PASS "+checks+" production native-input pulse clock/witness checks; full InControl/game delivery requires live witness.");
    }
}
