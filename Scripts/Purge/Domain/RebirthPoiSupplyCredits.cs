using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;

// Earned kills are published atomically with clearance, and survive native resets.
internal sealed class RebirthPoiSupplyCredits
{
    public readonly IReadOnlyDictionary<string,long> Totals;
    internal readonly IReadOnlyDictionary<string,RebirthPurgeOfflineCredit> Offline;
    private readonly HashSet<Guid> credited;
    public readonly int ConservativeCharacters;
    private RebirthPoiSupplyCredits(Dictionary<string,long> totals,HashSet<Guid> tokens,Dictionary<string,RebirthPurgeOfflineCredit> offline=null)
    {
        if(totals.Count>16384||tokens.Count>8192||totals.Any(p=>!ValidKey(p.Key)||p.Value<1)||tokens.Contains(Guid.Empty))throw new ArgumentException("Invalid supply credit custody.");
        Totals=new ReadOnlyDictionary<string,long>(new Dictionary<string,long>(totals,StringComparer.Ordinal));
        offline=offline??new Dictionary<string,RebirthPurgeOfflineCredit>(StringComparer.Ordinal);
        if(offline.Any(p=>!totals.ContainsKey(p.Key)||p.Value.Count>totals[p.Key]))throw new ArgumentException("Offline credits exceed earned custody.");
        Offline=new ReadOnlyDictionary<string,RebirthPurgeOfflineCredit>(new Dictionary<string,RebirthPurgeOfflineCredit>(offline,StringComparer.Ordinal));
        credited=new HashSet<Guid>(tokens);ConservativeCharacters=128+totals.Count*160+tokens.Count*80+offline.Count*320;
    }
    internal static bool ValidKey(string key){return key!=null&&key.Length==64&&key.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');}
    internal static bool TryCredit(RebirthPoiSupplyCredits old,RebirthPoiPartialObservation observations,out RebirthPoiSupplyCredits next,HashSet<string> offlineContributors=null,RebirthPoiIdentity identity=null,ulong worldTime=0)
    {
        next=old;if(observations==null)return true;
        Dictionary<string,long> totals=null;
        HashSet<Guid> tokens=null;
        Dictionary<string,RebirthPurgeOfflineCredit> offline=null;
        foreach(var actor in observations.Volumes.Values.SelectMany(v=>v.Actors.Values))
        {
            if(!RebirthPoiCombatantPolicy.IsRequired(actor.ClassName)||!actor.Dead||actor.TerminalKind!=RebirthPoiActorTerminalKind.Death||actor.Contributor==null||
                (tokens==null?old!=null&&old.credited.Contains(actor.Token):tokens.Contains(actor.Token)))continue;
            // Most successor records contain no newly creditable death. Copy custody
            // only when the first new token actually needs to be added.
            if(tokens==null)
            {
                totals=old==null?new Dictionary<string,long>(StringComparer.Ordinal):old.Totals.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
                tokens=old==null?new HashSet<Guid>():new HashSet<Guid>(old.credited);
                offline=old==null?new Dictionary<string,RebirthPurgeOfflineCredit>(StringComparer.Ordinal):old.Offline.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
            }
            long count;totals.TryGetValue(actor.Contributor,out count);if(count==long.MaxValue)return false;
            totals[actor.Contributor]=count+1;tokens.Add(actor.Token);
            if(identity!=null&&offlineContributors!=null&&offlineContributors.Contains(actor.Contributor))
            {
                RebirthPurgeOfflineCredit prior;offline.TryGetValue(actor.Contributor,out prior);
                var center=new RebirthPurgeSupplyOrigin(Math.Max(-10000000,Math.Min(10000000,identity.X+identity.SizeX/2)),identity.Y,Math.Max(-10000000,Math.Min(10000000,identity.Z+identity.SizeZ/2)));
                offline[actor.Contributor]=new RebirthPurgeOfflineCredit(checked((prior?.Count??0)+1),worldTime,center);
            }
        }
        if(tokens==null)return true;
        try{next=new RebirthPoiSupplyCredits(totals,tokens,offline);return true;}catch(ArgumentException){next=old;return false;}
    }
    internal static RebirthPoiSupplyCredits Retain(RebirthPoiSupplyCredits old,RebirthPoiPartialObservation observation)
    {
        if(old==null)return null;
        var retained=observation==null?new HashSet<Guid>():new HashSet<Guid>(observation.Volumes.Values.SelectMany(v=>v.Actors.Keys));
        retained.IntersectWith(old.credited);
        if(retained.SetEquals(old.credited))return old;
        return new RebirthPoiSupplyCredits(old.Totals.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal),retained,old.Offline.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal));
    }
    internal bool Matches(RebirthPoiPartialObservation observation)
    {
        if(credited.Count==0)return true;
        if(observation==null)return false;
        var actors=observation.Volumes.Values.SelectMany(v=>v.Actors.Values).ToDictionary(a=>a.Token);
        var witnessed=new Dictionary<string,long>(StringComparer.Ordinal);
        foreach(var token in credited)
        {
            RebirthPoiActorObservation actor;long total,count;
            if(!actors.TryGetValue(token,out actor)||!actor.Dead||actor.TerminalKind!=RebirthPoiActorTerminalKind.Death||actor.Contributor==null||!Totals.TryGetValue(actor.Contributor,out total))return false;
            witnessed.TryGetValue(actor.Contributor,out count);
            if(count>=total)return false;
            witnessed[actor.Contributor]=count+1;
        }
        return true;
    }
    internal XElement Write(string nodeName="supplyCredits")
    {
        return new XElement(nodeName,new XAttribute("version",1),
            Totals.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new XElement("player",new XAttribute("key",p.Key),new XAttribute("count",p.Value))),
            credited.OrderBy(t=>t).Select(t=>new XElement("credited",new XAttribute("token",t.ToString("N")))),
            Offline.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new XElement("offline",new XAttribute("key",p.Key),new XAttribute("count",p.Value.Count),new XAttribute("time",p.Value.Time),p.Value.Origin.Write("position"))));
    }
    internal static RebirthPoiSupplyCredits Read(XElement node,string nodeName="supplyCredits")
    {
        if(node==null)return null;
        RebirthPoiClearanceCodec.Shape(node,nodeName,"version","player,credited,offline");
        if(RebirthPoiClearanceCodec.Int(node,"version")!=1)throw new FormatException();
        var totals=new Dictionary<string,long>(StringComparer.Ordinal);var tokens=new HashSet<Guid>();var offline=new Dictionary<string,RebirthPurgeOfflineCredit>(StringComparer.Ordinal);
        foreach(var item in node.Elements())
        {
            if(item.Name=="player")
            {
                RebirthPoiClearanceCodec.Shape(item,"player","key,count","");
                string key=RebirthPoiClearanceCodec.Text(item,"key");
                if(totals.Count>=16384||totals.ContainsKey(key))throw new FormatException();
                totals.Add(key,RebirthPoiClearanceCodec.Number(item,"count"));
            }
            else if(item.Name=="credited")
            {
                RebirthPoiClearanceCodec.Shape(item,"credited","token","");
                if(tokens.Count>=8192||!tokens.Add(RebirthPoiClearanceCodec.Id(item,"token")))throw new FormatException();
            }
            else
            {
                RebirthPoiClearanceCodec.Shape(item,"offline","key,count,time","position");string key=RebirthPoiClearanceCodec.Text(item,"key");
                var position=RebirthPoiClearanceCodec.Single(item,"position");if(position==null||offline.Count>=16384||offline.ContainsKey(key))throw new FormatException();
                RebirthPoiClearanceCodec.Shape(position,"position","x,y,z","");ulong time;if(!ulong.TryParse(RebirthPoiClearanceCodec.Text(item,"time"),out time))throw new FormatException();
                offline.Add(key,new RebirthPurgeOfflineCredit(RebirthPoiClearanceCodec.Number(item,"count"),time,RebirthPurgeSupplyOrigin.Read(position)));
            }
        }
        if(totals.Count==0)throw new FormatException();
        return new RebirthPoiSupplyCredits(totals,tokens,offline);
    }
}
