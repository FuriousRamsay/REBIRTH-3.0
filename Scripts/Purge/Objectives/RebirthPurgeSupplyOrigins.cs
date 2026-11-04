using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

// Account-side source watermarks and credit segments prevent a restart from
// spending the same offline clear twice. Destination is tied to earned sequence.
internal sealed class RebirthPurgeSupplyOrigins
{
    private sealed class Segment
    {internal long Count;internal RebirthPurgeSupplyOrigin Origin;internal Segment(long count,RebirthPurgeSupplyOrigin origin){Count=count;Origin=origin;}}
    private readonly Dictionary<string,long> seen;
    private readonly List<Segment> balance;
    private readonly SortedDictionary<long,RebirthPurgeSupplyOrigin> destinations;
    private RebirthPurgeSupplyOrigins(Dictionary<string,long> seen,List<Segment> balance,SortedDictionary<long,RebirthPurgeSupplyOrigin> destinations)
    {this.seen=seen;this.balance=balance;this.destinations=destinations;}
    internal RebirthPurgeSupplyOrigin Destination(long sequence){RebirthPurgeSupplyOrigin value;return destinations.TryGetValue(sequence,out value)?value:null;}
    internal RebirthPurgeSupplyOrigins Delivered(long sequence)=>new RebirthPurgeSupplyOrigins(new Dictionary<string,long>(seen,StringComparer.Ordinal),CopyBalance(),new SortedDictionary<long,RebirthPurgeSupplyOrigin>(destinations.Where(p=>p.Key>sequence).ToDictionary(p=>p.Key,p=>p.Value)));
    private List<Segment> CopyBalance()=>balance.Select(s=>new Segment(s.Count,s.Origin)).ToList();
    internal bool IsSuccessorOf(RebirthPurgeSupplyOrigins old,long delivered)
    {
        foreach(var p in old.seen){long value;if(!seen.TryGetValue(p.Key,out value)||value<p.Value)return false;}
        foreach(var p in old.destinations)if(p.Key>delivered)
        {RebirthPurgeSupplyOrigin value;if(!destinations.TryGetValue(p.Key,out value)||value.X!=p.Value.X||value.Y!=p.Value.Y||value.Z!=p.Value.Z)return false;}
        return true;
    }
    internal static bool TryEarn(RebirthPurgeSupplyAccount account,long total,IEnumerable<RebirthPurgeSupplyOriginSource> sources,RebirthPurgeSupplyPolicy.Rules rules,int tier,out RebirthPurgeSupplyAccount next)
    {
        next=null;if(total<account.ObservedCredits||rules==null)return false;
        try
        {
            var old=account.Origins;
            var seen=old==null?new Dictionary<string,long>(StringComparer.Ordinal):new Dictionary<string,long>(old.seen,StringComparer.Ordinal);
            var segments=old==null?new List<Segment>():old.CopyBalance();
            if(old==null&&account.ObservedCredits>account.SpentCredits)segments.Add(new Segment(account.ObservedCredits-account.SpentCredits,null));
            var destinations=old==null?new SortedDictionary<long,RebirthPurgeSupplyOrigin>():new SortedDictionary<long,RebirthPurgeSupplyOrigin>(old.destinations);
            long added=total-account.ObservedCredits,offline=0;
            foreach(var source in sources.OrderBy(s=>s.Credit.Time).ThenBy(s=>s.Key,StringComparer.Ordinal))
            {
                long prior;seen.TryGetValue(source.Key,out prior);
                if(source.Credit.Count<prior)return false;
                long count=source.Credit.Count-prior;
                offline=checked(offline+count);if(offline>added)return false;
                if(count>0){segments.Add(new Segment(count,source.Credit.Origin));seen[source.Key]=source.Credit.Count;}
            }
            if(added>offline)segments.Add(new Segment(added-offline,null));
            long earned=account.EarnedDrops,spent=account.SpentCredits;
            int consumed=0;
            // Bounded redemption, same quota policy as the existing account.
            for(int batch=0;batch<128;batch++)
            {
                int target=rules.Target(earned,tier);long available=total-spent;if(available<target)break;
                long left=target;RebirthPurgeSupplyOrigin origin=null;
                while(left>0)
                {
                    if(consumed>=segments.Count)throw new InvalidOperationException("Supply balance lost provenance.");
                    var first=segments[consumed];long used=Math.Min(left,first.Count);origin=first.Origin;first.Count-=used;left-=used;
                    if(first.Count==0)consumed++;
                }
                earned=checked(earned+1);spent=checked(spent+target);if(origin!=null)destinations.Add(earned,origin);
            }
            if(consumed>0)segments.RemoveRange(0,consumed);
            if(seen.Count>20000||segments.Count>20000||destinations.Count>20000)return false;
            next=new RebirthPurgeSupplyAccount(account.Player,total,spent,earned,account.DeliveredDrops,account.InFlight,account.Delivery,new RebirthPurgeSupplyOrigins(seen,segments,destinations));
            return true;
        }
        catch(ArgumentException){return false;}catch(OverflowException){return false;}
    }
    internal XElement Write()=>new XElement("origins",new XAttribute("version",1),
        seen.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new XElement("seen",new XAttribute("key",p.Key),new XAttribute("count",p.Value))),
        balance.Select(s=>new XElement("balance",new XAttribute("count",s.Count),s.Origin?.Write("position"))),
        destinations.Select(p=>new XElement("destination",new XAttribute("sequence",p.Key),p.Value.Write("position"))));
    internal static RebirthPurgeSupplyOrigins Read(XElement node,long expectedBalance,long delivered,long earned)
    {
        if(node==null)return null;
        RebirthPoiClearanceCodec.Shape(node,"origins","version","seen,balance,destination");if(RebirthPoiClearanceCodec.Int(node,"version")!=1)throw new FormatException();
        var seen=new Dictionary<string,long>(StringComparer.Ordinal);var balance=new List<Segment>();var dest=new SortedDictionary<long,RebirthPurgeSupplyOrigin>();long sum=0;
        foreach(var n in node.Elements())
        {
            if(n.Name=="seen")
            {RebirthPoiClearanceCodec.Shape(n,"seen","key,count","");string key=RebirthPoiClearanceCodec.Text(n,"key");long count=RebirthPoiClearanceCodec.Number(n,"count");if(key.Length>1024||count<1||seen.Count>=20000||seen.ContainsKey(key))throw new FormatException();seen.Add(key,count);}
            else
            {
                bool isBalance=n.Name=="balance";RebirthPoiClearanceCodec.Shape(n,isBalance?"balance":"destination",isBalance?"count":"sequence","position");
                var p=RebirthPoiClearanceCodec.Single(n,"position");if(p!=null)RebirthPoiClearanceCodec.Shape(p,"position","x,y,z","");
                var origin=p==null?null:RebirthPurgeSupplyOrigin.Read(p);long count=RebirthPoiClearanceCodec.Number(n,isBalance?"count":"sequence");
                if(isBalance){if(count<1||balance.Count>=20000)throw new FormatException();sum=checked(sum+count);balance.Add(new Segment(count,origin));}
                else{if(origin==null||count<=delivered||count>earned||dest.Count>=20000||dest.ContainsKey(count))throw new FormatException();dest.Add(count,origin);}
            }
        }
        if(sum!=expectedBalance)throw new FormatException();return new RebirthPurgeSupplyOrigins(seen,balance,dest);
    }
}
