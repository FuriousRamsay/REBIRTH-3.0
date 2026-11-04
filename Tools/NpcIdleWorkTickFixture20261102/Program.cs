using System;
using System.Collections.Generic;
static class RebirthNpcWorkReleaseGate { public static bool Enabled; }
public static partial class RebirthNpcWorkAssignmentCoordinator
{
    sealed class Session { public ulong Id; }
    sealed class Schedule
    {
        readonly Queue<ulong> queue = new();
        public int Count => queue.Count;
        public bool TryTake(out ulong id) => queue.TryDequeue(out id);
        public void Return(ulong id) => queue.Enqueue(id);
    }
    static readonly object Sync = new();
    static readonly Dictionary<ulong,Session> Sessions = new();
    static Schedule SessionSchedule = new();
    static int checks,authorityCalls,ticks;
    static bool allow=true,throwRead,remove;
    static readonly List<ulong> visited=new();
    static bool UseAssignmentCoordinatorScope()=>Authorize();
    static bool UseLiteratureCoordinatorTickScope()=>Authorize();
    static bool Authorize(){authorityCalls++;if(throwRead)throw new Exception("simulated filesystem probe");return allow;}
    static void TickSession(Session session){ticks++;visited.Add(session.Id);if(remove)Sessions.Remove(session.Id);}
    static void Reset(bool enabled){Sessions.Clear();SessionSchedule=new();authorityCalls=ticks=0;allow=true;throwRead=remove=false;visited.Clear();RebirthNpcWorkReleaseGate.Enabled=enabled;}
    static void Add(ulong id,bool schedule=true){Sessions.Add(id,new Session{Id=id});if(schedule)SessionSchedule.Return(id);}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
    public static int Main()
    {
        try
        {
            foreach(bool enabled in new[]{false,true})
            {
                Reset(enabled);throwRead=true;for(int i=0;i<20000;i++)Tick(32);
                Check(authorityCalls==0&&ticks==0,"20000 empty ticks avoid authority/filesystem dispatch; release="+enabled);
                Reset(enabled);Add(1);allow=false;Tick(32);
                Check(authorityCalls==1&&ticks==0&&SessionSchedule.Count==1,"nonempty tick retains denied authority; release="+enabled);
                Reset(enabled);Add(1);Add(2);Tick(1);
                Check(authorityCalls==1&&ticks==1&&visited[0]==1&&SessionSchedule.Count==2,"one-session budget and requeue preserved; release="+enabled);
                Tick(1);Check(ticks==2&&visited[1]==2,"round-robin next session preserved; release="+enabled);
                Reset(enabled);Add(1);Add(2);Tick(0);
                Check(authorityCalls==1&&ticks==2,"zero budget means all original sessions; release="+enabled);
                Reset(enabled);Add(1);Tick(500);
                Check(ticks==1,"large budget clamps to original session count; release="+enabled);
                Reset(enabled);Add(1,false);Tick(32);
                Check(authorityCalls==1&&ticks==0,"custodied session without schedule still checks authority; release="+enabled);
                Reset(enabled);SessionSchedule.Return(9);Tick(32);
                Check(authorityCalls==1&&ticks==0,"schedule-only state does not take empty shortcut; release="+enabled);
                Reset(enabled);Add(1);remove=true;Tick(32);
                Check(ticks==1&&Sessions.Count==0&&SessionSchedule.Count==0,"terminal session is not requeued; release="+enabled);
                throwRead=true;Tick(32);Check(authorityCalls==1,"newly empty coordinator avoids the next filesystem probe; release="+enabled);
                Reset(enabled);Add(1);throwRead=true;bool threw=false;try{Tick(32);}catch{threw=true;}
                Check(threw&&authorityCalls==1&&ticks==0,"nonempty authority failure still propagates before work; release="+enabled);
            }
            Console.WriteLine("RESULT "+checks+" PASS: actual extracted production Tick; session/scheduler/authority boundaries doubled. No native performance claim.");return 0;
        }
        catch(Exception error){Console.WriteLine("FAIL "+error.Message);return 1;}
    }
}
