using System;using System.Collections.Generic;
static class AdvancedFarmingRuntimePolicy { public static bool Enabled=true; }
static class Program {
const int DeferTicks=24,MaxRetryTicks=160;
struct PendingVisualRefresh { public int RemainingTicks,AgeTicks; }
static Dictionary<int,PendingVisualRefresh> _pending=new();
static List<int> _scratchKeys=new(),_scratchReady=new();
static List<(int Tick,int Age)> attempts=new();static int tick;static bool succeed;
static bool RefreshDoorVisualsInternal(int pos,int age){attempts.Add((tick,age));return succeed;}
static void Check(bool pass,string name){if(!pass)throw new Exception(name);Console.WriteLine("PASS "+name);}
static void Reset(){_pending.Clear();attempts.Clear();tick=0;succeed=false;AdvancedFarmingRuntimePolicy.Enabled=true;}
static void Add(int id){_pending[id]=new(){RemainingTicks=DeferTicks};}
static void Step(int count){for(int i=0;i<count;i++){tick++;PumpPendingVisualRefreshesForGameUpdate();}}
static void Main(){
Reset();Add(1);Step(23);Check(attempts.Count==0,"Initial defer lasts 24 updates");Step(1);Check(attempts.Count==1&&attempts[0].Tick==24,"First attempt on update 24");
Step(2);Check(attempts.Count==2&&attempts[1].Tick==26,"Retry cadence remains two updates");
Step(400);Console.WriteLine($"OBSERVED lastTick={attempts[^1].Tick} lastAge={attempts[^1].Age} attempts={attempts.Count}");
Check(attempts[^1].Tick==160&&attempts[^1].Age==160&&_pending.Count==0,"Failed visual refresh expires at actual 160-update bound");
Reset();Add(1);succeed=true;Step(100);Check(attempts.Count==1&&_pending.Count==0,"Success removes entry without retry");
Reset();Add(1);Step(20);Add(1);Step(23);Check(attempts.Count==0,"New door change restarts defer");Step(1);Check(attempts.Count==1&&attempts[0].Age==24,"Requeued age starts fresh");
Reset();Add(1);AdvancedFarmingRuntimePolicy.Enabled=false;Step(50);Check(attempts.Count==0&&_pending[1].AgeTicks==0,"Disabled policy performs no retry work");
Reset();for(int i=0;i<512;i++)Add(i);Step(160);Check(attempts.Count==512*69&&_pending.Count==0,"512 failed entries drain by bound");
}
    public static void PumpPendingVisualRefreshesForGameUpdate()
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        if (_pending.Count == 0)
            return;

        _scratchKeys.Clear();
        _scratchKeys.AddRange(_pending.Keys);
        _scratchReady.Clear();

        for (int i = 0; i < _scratchKeys.Count; i++)
        {
            int pos = _scratchKeys[i];
            PendingVisualRefresh pending = _pending[pos];
            pending.AgeTicks++;
            pending.RemainingTicks--;

            if (pending.RemainingTicks <= 0)
            {
                _scratchReady.Add(pos);
            }
            // The ready pass reads the dictionary again. Persist age on attempt ticks too,
            // otherwise every retry loses one update and exceeds MaxRetryTicks.
            _pending[pos] = pending;
        }

        for (int i = 0; i < _scratchReady.Count; i++)
        {
            int pos = _scratchReady[i];
            PendingVisualRefresh pending;
            if (!_pending.TryGetValue(pos, out pending))
                continue;

            _pending.Remove(pos);
            if (!RefreshDoorVisualsInternal(pos, pending.AgeTicks))
            {
                if (pending.AgeTicks < MaxRetryTicks)
                {
                    pending.RemainingTicks = 2;
                    _pending[pos] = pending;
                }
            }
        }
    }

}
