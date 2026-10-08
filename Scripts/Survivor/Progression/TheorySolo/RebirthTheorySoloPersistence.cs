using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

internal static class RebirthTheorySoloPersistence
{
    private static string N(double n)=>n.ToString("R",CultureInfo.InvariantCulture);
    internal static XElement Write(RebirthTheorySoloState state)
    {
        if(state==null)return null;
        var node=new XElement("soloTheory",new XAttribute("version",1),new XAttribute("creation",state.CreationId),new XAttribute("lastSettled",state.LastSettledId??""),new XAttribute("cancelled",state.LastCancelledId??""),new XAttribute("cancelPending",state.CancelPendingId??""),new XAttribute("cancelSubject",state.CancelPendingSubject??""));
        foreach(var p in state.Streams.OrderBy(p=>p.Key,StringComparer.Ordinal))node.Add(new XElement("stream",new XAttribute("subject",p.Key),new XAttribute("ordinal",p.Value),new XAttribute("lastEvidence",N(state.LastEvidence[p.Key])),new XAttribute("lastSettlement",N(state.LastSettlement.TryGetValue(p.Key,out var t)?t:0))));
        foreach(var e in state.Evidence.OrderBy(e=>e.Subject,StringComparer.Ordinal).ThenBy(e=>e.Ordinal))node.Add(new XElement("evidence",new XAttribute("subject",e.Subject),new XAttribute("family",e.Family),new XAttribute("ordinal",e.Ordinal),new XAttribute("original",e.OriginalEvent),new XAttribute("difficulty",N(e.Difficulty)),new XAttribute("active",N(e.ActiveSeconds))));
        if(state.TeachingOriginal!=null)node.Add(state.TeachingOriginal.Write());
        if(state.LockpickOriginal!=null)node.Add(state.LockpickOriginal.Write(state.CreationId));
        if(state.CombatOriginal!=null)node.Add(state.CombatOriginal.Write());
        if(state.OriginalTasks!=null)node.Add(state.OriginalTasks.Write(state.CreationId));
        if(state.Session!=null)
        {
            var s=state.Session;var session=new XElement("session",new XAttribute("id",s.Id),new XAttribute("subject",s.Subject),new XAttribute("elapsed",N(s.Elapsed)),new XAttribute("duration",N(s.Duration)),new XAttribute("started",N(s.StartedActive)),new XAttribute("consumed",s.Consumed?"1":"0"));
            foreach(long ordinal in s.Ordinals)session.Add(new XElement("reserved",new XAttribute("ordinal",ordinal)));
            node.Add(session);
        }
        return node;
    }
    internal static bool TryRead(XElement progression,out RebirthTheorySoloState state,out string error)
    {
        state=null;error=null;var nodes=progression.Elements("soloTheory").ToArray();if(nodes.Length==0)return true;
        try
        {
            if(nodes.Length!=1)throw new FormatException();var node=nodes[0];
            if((node.Attributes().Count()!=3&&node.Attributes().Count()!=4&&node.Attributes().Count()!=6)||(string)node.Attribute("version")!="1"||!RebirthSurvivorRequestScope.TryNormalize((string)node.Attribute("creation"),out var creation)||creation!=(string)node.Attribute("creation"))throw new FormatException();
            var next=new RebirthTheorySoloState{CreationId=creation,LastSettledId=(string)node.Attribute("lastSettled"),LastCancelledId=(string)node.Attribute("cancelled")??"",CancelPendingId=(string)node.Attribute("cancelPending")??"",CancelPendingSubject=(string)node.Attribute("cancelSubject")??""};
            if(next.CancelPendingId.Length>0&&(!Id(next.CancelPendingId)||next.CancelPendingId!=next.LastCancelledId||!RebirthTheorySoloRegistry.TryGet(next.CancelPendingSubject,out _))||next.CancelPendingId.Length==0&&next.CancelPendingSubject.Length>0)throw new FormatException();
            if(next.LastCancelledId.Length>0&&!Id(next.LastCancelledId))throw new FormatException();
            if(next.LastSettledId==null||next.LastSettledId.Length>0&&!Id(next.LastSettledId))throw new FormatException();
            foreach(var row in node.Elements())
            {
                if(row.Name=="stream")
                {
                    string subject=(string)row.Attribute("subject");long ordinal=(long)row.Attribute("ordinal");double evidence=(double)row.Attribute("lastEvidence"),settled=(double)row.Attribute("lastSettlement");
                    if(row.HasElements||row.Attributes().Count()!=4||!RebirthTheorySoloRegistry.TryGet(subject,out _)||ordinal<1||next.Streams.ContainsKey(subject)||!Time(evidence)||!Time(settled))throw new FormatException();
                    next.Streams.Add(subject,ordinal);next.LastEvidence.Add(subject,evidence);next.LastSettlement.Add(subject,settled);
                }
                else if(row.Name=="evidence")
                {
                    var e=new RebirthTheorySoloEvidence{Subject=(string)row.Attribute("subject"),Family=(string)row.Attribute("family"),Ordinal=(long)row.Attribute("ordinal"),OriginalEvent=(string)row.Attribute("original"),Difficulty=(float)row.Attribute("difficulty"),ActiveSeconds=(double)row.Attribute("active")};
                    if(row.HasElements||row.Attributes().Count()!=6||!RebirthTheorySoloRegistry.Matches(e.Subject,e.Family)||e.Ordinal<1||!RebirthTheorySoloState.Text(e.OriginalEvent,256)||!Time(e.ActiveSeconds)||!RebirthTheorySoloState.Finite(e.Difficulty)||e.Difficulty<0||e.Difficulty>100||next.Evidence.Any(x=>x.Subject==e.Subject&&(x.Ordinal==e.Ordinal||x.OriginalEvent==e.OriginalEvent)))throw new FormatException();
                    next.Evidence.Add(e);
                }
                else if(row.Name=="session")
                {
                    if(next.Session!=null||row.Attributes().Count()!=6)throw new FormatException();
                    var s=new RebirthTheorySoloSession{Id=(string)row.Attribute("id"),Subject=(string)row.Attribute("subject"),Elapsed=(float)row.Attribute("elapsed"),Duration=(float)row.Attribute("duration"),StartedActive=(double)row.Attribute("started"),Consumed=(string)row.Attribute("consumed")=="1"};
                    if(!Id(s.Id)||!RebirthTheorySoloRegistry.TryGet(s.Subject,out _)||!Time(s.Elapsed)||!Time(s.Duration)||s.Duration<=0||s.Duration>3600||s.Elapsed>s.Duration||!Time(s.StartedActive)||((string)row.Attribute("consumed")!="0"&&(string)row.Attribute("consumed")!="1"))throw new FormatException();
                    foreach(var r in row.Elements()){long ordinal=(long)r.Attribute("ordinal");if(r.Name!="reserved"||r.HasElements||r.Attributes().Count()!=1||ordinal<1||s.Ordinals.Contains(ordinal))throw new FormatException();s.Ordinals.Add(ordinal);}
                    if(s.Ordinals.Count<1||s.Ordinals.Count>16)throw new FormatException();next.Session=s;
                }
                else if(row.Name=="teachingOriginal")
                {
                    if(next.TeachingOriginal!=null||!RebirthTheorySoloTeachingRetirement.TryRead(row,out var teaching))throw new FormatException();
                    next.TeachingOriginal=teaching;
                }
                else if(row.Name=="lockpickOriginal")
                {
                    if(next.LockpickOriginal!=null||!RebirthTheorySoloLockpickLedger.TryRead(row,creation,out var lockpick))throw new FormatException();
                    next.LockpickOriginal=lockpick;
                }
                else if(row.Name=="combatOriginal")
                {
                    if(next.CombatOriginal!=null||!RebirthTheorySoloCombatLedger.TryRead(row,creation,out var combat)||combat.Pending.Any(t=>!RebirthTheorySoloRegistry.Matches(t.Subject,t.Family)))throw new FormatException();
                    next.CombatOriginal=combat;
                }
                else if(row.Name=="originalTasks")
                {
                    if(next.OriginalTasks!=null||!RebirthTheorySoloOriginalTaskLedger.TryRead(row,creation,out var tasks)||row.Elements().Any(t=>!RebirthTheorySoloRegistry.TryGet((string)t.Attribute("subject"),out _)))throw new FormatException();
                    next.OriginalTasks=tasks;
                }
                else throw new FormatException();
            }
            if(next.Streams.Count>48||next.Evidence.Count>768||next.Evidence.Any(e=>!next.Streams.TryGetValue(e.Subject,out var ordinal)||e.Ordinal>ordinal||e.ActiveSeconds>next.LastEvidence[e.Subject]))throw new FormatException();
            if(next.Session!=null&&next.Session.Ordinals.Any(o=>!next.Evidence.Any(e=>e.Subject==next.Session.Subject&&e.Ordinal==o)))throw new FormatException();
            // Canonical round-trip also rejects hidden text, unknown attributes and duplicate nodes.
            var canonical=new XElement(node.Name,node.Attributes(),node.Elements());if(canonical.Attribute("cancelled")==null)canonical.Add(new XAttribute("cancelled",""));
            if(canonical.Attribute("cancelPending")==null)canonical.Add(new XAttribute("cancelPending",""));if(canonical.Attribute("cancelSubject")==null)canonical.Add(new XAttribute("cancelSubject",""));
            if(next.CancelPendingId.Length>0&&next.Session!=null||next.Session!=null&&next.Session.Id==next.LastCancelledId||!XNode.DeepEquals(Write(next),canonical))throw new FormatException();
            if(node.DescendantNodes().OfType<XText>().Any(t=>!string.IsNullOrWhiteSpace(t.Value)))throw new FormatException();
            state=next;return true;
        }
        catch{error="Malformed solo Theory evidence/session";return false;}
    }
    internal static bool MatchesOwner(RebirthTheorySoloState state,string creation)=>state==null||state.CreationId==creation;
    private static bool Id(string id)=>Guid.TryParseExact(id,"N",out var g)&&g!=Guid.Empty;
    private static bool Time(double t)=>RebirthTheorySoloState.Finite(t)&&t>=0;
}
