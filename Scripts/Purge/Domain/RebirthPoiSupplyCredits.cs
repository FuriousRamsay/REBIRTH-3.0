using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;

// Earned kills are published atomically with clearance, and survive native resets.
internal sealed class RebirthPoiSupplyCredits
{
    public readonly IReadOnlyDictionary<string,long> Totals;
    private readonly HashSet<Guid> credited;
    public readonly int ConservativeCharacters;
    private RebirthPoiSupplyCredits(Dictionary<string,long> totals,HashSet<Guid> tokens)
    {
        if(totals.Count>16384||tokens.Count>8192||totals.Any(p=>!ValidKey(p.Key)||p.Value<1)||tokens.Contains(Guid.Empty))throw new ArgumentException("Invalid supply credit custody.");
        Totals=new ReadOnlyDictionary<string,long>(new Dictionary<string,long>(totals,StringComparer.Ordinal));
        credited=new HashSet<Guid>(tokens);ConservativeCharacters=128+totals.Count*160+tokens.Count*80;
    }
    internal static bool ValidKey(string key){return key!=null&&key.Length==64&&key.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');}
    internal static bool TryCredit(RebirthPoiSupplyCredits old,RebirthPoiPartialObservation observations,out RebirthPoiSupplyCredits next)
    {
        next=old;if(observations==null)return true;
        var totals=old==null?new Dictionary<string,long>(StringComparer.Ordinal):old.Totals.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
        var tokens=old==null?new HashSet<Guid>():new HashSet<Guid>(old.credited);bool changed=false;
        foreach(var actor in observations.Volumes.Values.SelectMany(v=>v.Actors.Values))
        {
            if(!actor.Dead||actor.TerminalKind!=RebirthPoiActorTerminalKind.Death||actor.Contributor==null||tokens.Contains(actor.Token))continue;
            long count;totals.TryGetValue(actor.Contributor,out count);if(count==long.MaxValue)return false;
            totals[actor.Contributor]=count+1;tokens.Add(actor.Token);changed=true;
        }
        if(!changed)return true;
        try{next=new RebirthPoiSupplyCredits(totals,tokens);return true;}catch(ArgumentException){next=old;return false;}
    }
    internal static RebirthPoiSupplyCredits Retain(RebirthPoiSupplyCredits old,RebirthPoiPartialObservation observation)
    {
        if(old==null)return null;
        var retained=observation==null?new HashSet<Guid>():new HashSet<Guid>(observation.Volumes.Values.SelectMany(v=>v.Actors.Keys));
        retained.IntersectWith(old.credited);
        if(retained.SetEquals(old.credited))return old;
        return new RebirthPoiSupplyCredits(old.Totals.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal),retained);
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
    internal XElement Write()
    {
        return new XElement("supplyCredits",new XAttribute("version",1),
            Totals.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new XElement("player",new XAttribute("key",p.Key),new XAttribute("count",p.Value))),
            credited.OrderBy(t=>t).Select(t=>new XElement("credited",new XAttribute("token",t.ToString("N")))));
    }
    internal static RebirthPoiSupplyCredits Read(XElement node)
    {
        if(node==null)return null;
        RebirthPoiClearanceCodec.Shape(node,"supplyCredits","version","player,credited");
        if(RebirthPoiClearanceCodec.Int(node,"version")!=1)throw new FormatException();
        var totals=new Dictionary<string,long>(StringComparer.Ordinal);var tokens=new HashSet<Guid>();
        foreach(var item in node.Elements())
        {
            if(item.Name=="player")
            {
                RebirthPoiClearanceCodec.Shape(item,"player","key,count","");
                string key=RebirthPoiClearanceCodec.Text(item,"key");
                if(totals.Count>=16384||totals.ContainsKey(key))throw new FormatException();
                totals.Add(key,RebirthPoiClearanceCodec.Number(item,"count"));
            }
            else
            {
                RebirthPoiClearanceCodec.Shape(item,"credited","token","");
                if(tokens.Count>=8192||!tokens.Add(RebirthPoiClearanceCodec.Id(item,"token")))throw new FormatException();
            }
        }
        if(totals.Count==0)throw new FormatException();
        return new RebirthPoiSupplyCredits(totals,tokens);
    }
}