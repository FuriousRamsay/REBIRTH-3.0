using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

// Quotas are separate from biome completion percentages. Historical 2.6 capped
// the added quota at 150, making its final default target 75 + 150 = 225 kills.
internal static class RebirthPurgeSupplyPolicy
{
    internal sealed class Rules
    {
        internal readonly int BaseKills,Increment,MaximumBonus,FlightRadius;
        internal Rules(int baseKills,int increment,int maximumBonus,int flightRadius)
        {
            if(baseKills<1||baseKills>1000000||increment<0||increment>1000000||maximumBonus<0||maximumBonus>1000000||increment==0&&maximumBonus!=0||increment!=0&&maximumBonus%increment!=0||flightRadius<1||flightRadius>1000)throw new ArgumentException("Invalid Purge supply rules.");
            BaseKills=baseKills;Increment=increment;MaximumBonus=maximumBonus;FlightRadius=flightRadius;
        }
        internal int Target(long earnedDrops)
        {
            if(earnedDrops<0)throw new ArgumentOutOfRangeException(nameof(earnedDrops));
            return checked(BaseKills+(Increment==0?0:(int)Math.Min(earnedDrops,MaximumBonus/Increment)*Increment));
        }
        internal bool TryRedeem(long confirmedCredits,long earnedDrops,int maximumBatch,out Redemption result)
        {
            result=null;if(confirmedCredits<0||earnedDrops<0||maximumBatch<1||maximumBatch>128)return false;
            int count=0;long balance=confirmedCredits,next=earnedDrops;
            while(count<maximumBatch&&balance>=Target(next))
            {
                if(next==long.MaxValue)return false;
                balance-=Target(next);next++;count++;
            }
            result=new Redemption(count,balance,next);return true;
        }
    }
    internal sealed class Redemption
    {
        internal readonly int Drops;internal readonly long Credits,EarnedDrops;
        internal Redemption(int drops,long credits,long earnedDrops){Drops=drops;Credits=credits;EarnedDrops=earnedDrops;}
    }
    internal static Rules Current{get;private set;}
    internal static bool TryParse(string text,out Rules rules)
    {
        rules=null;if(text==null||text.Length>16384)return false;
        try
        {
            using(var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16384}))
            {
                var root=XElement.Load(reader);RebirthPoiClearanceCodec.Shape(root,"purge_supplies","version,baseKills,increment,maximumBonus,flightRadius","");
                if(RebirthPoiClearanceCodec.Int(root,"version")!=1)return false;
                rules=new Rules(RebirthPoiClearanceCodec.Int(root,"baseKills"),RebirthPoiClearanceCodec.Int(root,"increment"),RebirthPoiClearanceCodec.Int(root,"maximumBonus"),RebirthPoiClearanceCodec.Int(root,"flightRadius"));return true;
            }
        }
        catch(ArgumentException){return false;}catch(XmlException){return false;}catch(FormatException){return false;}catch(OverflowException){return false;}
    }
    internal static void Load(string modRoot)
    {
        Current=null;
        try
        {
            string path=Path.Combine(modRoot,"Config","_purge_supplies.xml");if(!File.Exists(path)||new FileInfo(path).Length>65536)throw new InvalidDataException("Missing or oversized supply rules.");
            Rules rules;if(!TryParse(File.ReadAllText(path),out rules))throw new InvalidDataException("Invalid supply rules.");Current=rules;
        }
        catch(Exception error){Log.Warning("[REBIRTH Purge] Supply rewards withheld: "+error.Message);}
    }
}