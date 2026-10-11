using System;using System.Collections.Generic;using System.Linq;
class RebirthTheorySoloState {public List<RebirthTheorySoloEvidence> Evidence=new();}
class RebirthTheorySoloSession {public string Subject;public float Duration;public List<long> Ordinals=new();}
class RebirthTheorySoloEvidence {public string Subject,Family;public long Ordinal;public float Difficulty;}
internal sealed class RebirthTheorySoloRule
{
    internal string Subject,Family;
    internal int MinimumOutcomes;
    internal float Duration,Gain,DifficultyMargin,DifficultyCeiling;
    internal double Cooldown,EventSpacing;
    internal bool Relevant(float theory,float difficulty)
        =>Finite(theory)&&Finite(difficulty)&&theory>=0&&theory<100&&difficulty>=0&&difficulty<=100&&
            difficulty>=Math.Max(0,Math.Min(theory,DifficultyCeiling)-DifficultyMargin);
    private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
}
class Program {
    private static bool Before(RebirthTheorySoloState state,RebirthTheorySoloSession session,float theory,RebirthTheorySoloRule rule)
    {
        if(state==null||session==null||session.Subject!=rule.Subject||session.Duration!=rule.Duration||session.Ordinals.Count!=rule.MinimumOutcomes||session.Ordinals.Distinct().Count()!=session.Ordinals.Count)return false;
        return session.Ordinals.All(o=>state.Evidence.Any(e=>e.Subject==session.Subject&&e.Ordinal==o&&e.Family==rule.Family&&rule.Relevant(theory,e.Difficulty)));
    }
    private static bool After(RebirthTheorySoloState state,RebirthTheorySoloSession session,float theory,RebirthTheorySoloRule rule)
    {
        if(state==null||session==null||session.Subject!=rule.Subject||session.Duration!=rule.Duration||session.Ordinals.Count!=rule.MinimumOutcomes)return false;
        // Authoring and persistence bound reserved outcomes to 16. Keep this check
        // allocation-free without caching evidence that can change during a session.
        for(int i=0;i<session.Ordinals.Count;i++)
            for(int j=0;j<i;j++)
                if(session.Ordinals[i]==session.Ordinals[j])return false;
        for(int i=0;i<session.Ordinals.Count;i++)
        {
            bool found=false;
            for(int j=0;j<state.Evidence.Count;j++)
            {
                var e=state.Evidence[j];
                if(e.Subject==session.Subject&&e.Ordinal==session.Ordinals[i]&&e.Family==rule.Family&&rule.Relevant(theory,e.Difficulty)){found=true;break;}
            }
            if(!found)return false;
        }
        return true;
    }

static void Main(){var rng=new Random(711);int matches=0;
for(int trial=0;trial<10000;trial++){
 var rule=new RebirthTheorySoloRule{Subject="skill.a",Family="craft",MinimumOutcomes=rng.Next(1,17),Duration=60,DifficultyCeiling=70,DifficultyMargin=10};
 var state=new RebirthTheorySoloState();var session=new RebirthTheorySoloSession{Subject="skill.a",Duration=60};float theory=rng.Next(0,100);
 for(int i=0;i<rule.MinimumOutcomes;i++){session.Ordinals.Add(i);state.Evidence.Add(new(){Subject="skill.a",Family="craft",Ordinal=i,Difficulty=100});}
 for(int i=0;i<rng.Next(0,752);i++)state.Evidence.Add(new(){Subject="skill.other",Family="other",Ordinal=i,Difficulty=0});
 switch(trial%8){case 0:if(session.Ordinals.Count>1)session.Ordinals[1]=session.Ordinals[0];break;case 1:state.Evidence[0].Difficulty=0;break;case 2:state.Evidence[0].Family="other";break;case 3:session.Subject="other";break;case 4:session.Duration=59;break;case 5:theory=float.NaN;break;case 6:session.Ordinals.Add(900);break;}
 bool a=Before(state,session,theory,rule),b=After(state,session,theory,rule);if(a!=b)throw new Exception("parity "+trial);matches+=b?1:0;
}
var r=new RebirthTheorySoloRule{Subject="s",Family="f",MinimumOutcomes=16,Duration=60,DifficultyCeiling=70,DifficultyMargin=10};var st=new RebirthTheorySoloState();var ss=new RebirthTheorySoloSession{Subject="s",Duration=60};for(int i=0;i<16;i++){ss.Ordinals.Add(i);st.Evidence.Add(new(){Subject="s",Family="f",Ordinal=i,Difficulty=100});}
for(int i=0;i<1000;i++){Before(st,ss,50,r);After(st,ss,50,r);}long start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)Before(st,ss,50,r);long oldBytes=GC.GetAllocatedBytesForCurrentThread()-start;start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)After(st,ss,50,r);long newBytes=GC.GetAllocatedBytesForCurrentThread()-start;if(newBytes!=0||matches==0)throw new Exception("allocation/positive coverage");Console.WriteLine($"PASS 10000 seeded old/new eligibility cases ({matches} eligible);10000 active16-outcome checks bytes before={oldBytes},after={newBytes}. Extracted production methods and actual rule; state containers doubled; no native runtime/FPS claim.");
}}
