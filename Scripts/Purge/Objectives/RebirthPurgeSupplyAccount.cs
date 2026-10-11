using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// A drop entitlement is earned from credited clears and consumed on accepted
// native launch. Legacy delivery fields remain readable for existing saves.
internal sealed class RebirthPurgeSupplyAccount
{
    internal readonly string Player;
    internal readonly long ObservedCredits,SpentCredits,EarnedDrops,DeliveredDrops,InFlight;
    internal readonly RebirthPurgeSupplyDeliveryPlan Delivery;
    internal RebirthPurgeSupplyAccount(string player,long observed=0,long spent=0,long earned=0,long delivered=0,long inFlight=0,RebirthPurgeSupplyDeliveryPlan delivery=null)
    {
        if(!RebirthPoiSupplyCredits.ValidKey(player)||observed<0||spent<0||spent>observed||earned<0||delivered<0||delivered>earned||inFlight<0||inFlight!=0&&(delivered==long.MaxValue||inFlight!=delivered+1||inFlight>earned))throw new ArgumentException("Invalid supply account custody.");
        if(delivery!=null&&inFlight==0)throw new ArgumentException("Delivery plan without an outstanding entitlement.");
        Player=player;ObservedCredits=observed;SpentCredits=spent;EarnedDrops=earned;DeliveredDrops=delivered;InFlight=inFlight;Delivery=delivery;
    }
    internal bool TryEarn(long confirmedTotal,RebirthPurgeSupplyPolicy.Rules rules,out RebirthPurgeSupplyAccount next)
    {
        next=null;if(rules==null||confirmedTotal<ObservedCredits)return false;
        RebirthPurgeSupplyPolicy.Redemption result;if(!rules.TryRedeem(confirmedTotal-SpentCredits,EarnedDrops,128,out result))return false;
        next=new RebirthPurgeSupplyAccount(Player,confirmedTotal,confirmedTotal-result.Credits,result.EarnedDrops,DeliveredDrops,InFlight,Delivery);return true;
    }
    internal bool TryReserve(out RebirthPurgeSupplyAccount next)
    {
        next=null;if(InFlight!=0||DeliveredDrops>=EarnedDrops||DeliveredDrops==long.MaxValue)return false;
        next=new RebirthPurgeSupplyAccount(Player,ObservedCredits,SpentCredits,EarnedDrops,DeliveredDrops,DeliveredDrops+1);return true;
    }
    internal bool TryReserve(RebirthPurgeSupplyDeliveryPlan plan,out RebirthPurgeSupplyAccount next)
    {
        next=null;
        if(plan==null||plan.Phase!=RebirthPurgeSupplyDeliveryPhase.Prepared||InFlight!=0||DeliveredDrops>=EarnedDrops||DeliveredDrops==long.MaxValue)return false;
        next=new RebirthPurgeSupplyAccount(Player,ObservedCredits,SpentCredits,EarnedDrops,DeliveredDrops,DeliveredDrops+1,plan);return true;
    }
    internal bool TryStart(Guid world,Guid token,out RebirthPurgeSupplyAccount next)
    {
        next=null;
        if(InFlight==0||Delivery==null||Delivery.Phase!=RebirthPurgeSupplyDeliveryPhase.Prepared||world==Guid.Empty||token!=Token(world))return false;
        next=new RebirthPurgeSupplyAccount(Player,ObservedCredits,SpentCredits,EarnedDrops,DeliveredDrops,InFlight,Delivery.Started());return true;
    }
    internal bool TryBeginCrate(Guid world,Guid token,out RebirthPurgeSupplyAccount next)
    {
        next=null;
        if(InFlight==0||Delivery==null||Delivery.Phase!=RebirthPurgeSupplyDeliveryPhase.Started||world==Guid.Empty||token!=Token(world))return false;
        next=new RebirthPurgeSupplyAccount(Player,ObservedCredits,SpentCredits,EarnedDrops,DeliveredDrops,InFlight,Delivery.CrateStarted());return true;
    }
    internal Guid Token(Guid world)
    {
        if(world==Guid.Empty||InFlight==0)throw new ArgumentException("No original supply delivery.");
        return DeliveryToken(world,Player,InFlight);
    }
    internal static Guid DeliveryToken(Guid world,string player,long sequence)
    {
        if(world==Guid.Empty||!RebirthPoiSupplyCredits.ValidKey(player)||sequence<1)throw new ArgumentException("Invalid original delivery identity.");
        using(var hash=SHA256.Create())
        {
            var digest=hash.ComputeHash(Encoding.UTF8.GetBytes(world.ToString("N")+"|"+player+"|"+sequence.ToString(CultureInfo.InvariantCulture)));
            var bytes=new byte[16];Array.Copy(digest,bytes,16);return new Guid(bytes);
        }
    }
    internal bool TryComplete(Guid world,Guid token,out RebirthPurgeSupplyAccount next)
    {
        next=null;if(InFlight==0||world==Guid.Empty||token!=Token(world)||Delivery!=null&&Delivery.Version>=2&&Delivery.Phase!=RebirthPurgeSupplyDeliveryPhase.CrateStarted)return false;
        next=new RebirthPurgeSupplyAccount(Player,ObservedCredits,SpentCredits,EarnedDrops,InFlight,0);return true;
    }
    // Native SpawnAirDrop accepted this entitlement. The saved schema stays
    // unchanged; delivered now means accepted native launch, matching 2.6.
    internal bool TryLaunched(Guid world,Guid token,out RebirthPurgeSupplyAccount next)
    {
        next=null;
        if(InFlight==0||world==Guid.Empty||token!=Token(world)||Delivery==null||
           Delivery.Phase!=RebirthPurgeSupplyDeliveryPhase.Prepared&&!Delivery.CanResumeFlight)return false;
        next=new RebirthPurgeSupplyAccount(Player,ObservedCredits,SpentCredits,EarnedDrops,InFlight,0);return true;
    }
    internal bool IsSuccessorOf(RebirthPurgeSupplyAccount old)
    {
        if(old==null)return true;
        if(Player!=old.Player||ObservedCredits<old.ObservedCredits||SpentCredits<old.SpentCredits||EarnedDrops<old.EarnedDrops||DeliveredDrops<old.DeliveredDrops)return false;
        if(old.InFlight!=0)
        {
            // Uncertain flight cannot be silently discarded or retried with a new token.
            if(InFlight!=old.InFlight&&(InFlight!=0||DeliveredDrops!=old.InFlight))return false;
            if(InFlight==old.InFlight)
            {
                if(old.Delivery==null){if(Delivery!=null)return false;}
                else if(Delivery==null||!Delivery.IsSuccessorOf(old.Delivery))return false;
            }
        }
        else if(DeliveredDrops!=old.DeliveredDrops)return false;
        return true;
    }
    internal XElement Write()=>new XElement("account",new XAttribute("player",Player),new XAttribute("observed",ObservedCredits),new XAttribute("spent",SpentCredits),new XAttribute("earned",EarnedDrops),new XAttribute("delivered",DeliveredDrops),new XAttribute("flight",InFlight),Delivery?.Write());
    internal static RebirthPurgeSupplyAccount Read(XElement node)
    {
        RebirthPoiClearanceCodec.Shape(node,"account","player,observed,spent,earned,delivered,flight","delivery");
        return new RebirthPurgeSupplyAccount(RebirthPoiClearanceCodec.Text(node,"player"),RebirthPoiClearanceCodec.Number(node,"observed"),RebirthPoiClearanceCodec.Number(node,"spent"),RebirthPoiClearanceCodec.Number(node,"earned"),RebirthPoiClearanceCodec.Number(node,"delivered"),RebirthPoiClearanceCodec.Number(node,"flight"),RebirthPurgeSupplyDeliveryPlan.Read(RebirthPoiClearanceCodec.Single(node,"delivery")));
    }
}
// A missing plan denotes an older, uncertain flight. It must never be promoted
// to Prepared merely because the aircraft is absent after a reload.
internal enum RebirthPurgeSupplyDeliveryPhase { Prepared=1,Started=2,CrateStarted=3 }
internal sealed class RebirthPurgeSupplyDeliveryPlan
{
    internal readonly int X,Y,Z;
    internal readonly RebirthPurgeSupplyDeliveryPhase Phase;
    internal readonly int Version;
    internal bool CanResumeFlight=>Version>=2&&Phase==RebirthPurgeSupplyDeliveryPhase.Started;
    internal RebirthPurgeSupplyDeliveryPlan(int x,int y,int z,RebirthPurgeSupplyDeliveryPhase phase=RebirthPurgeSupplyDeliveryPhase.Prepared,int version=2)
    {
        if(x<-1000000||x>1000000||z<-1000000||z>1000000||y<-4096||y>4096||
           phase!=RebirthPurgeSupplyDeliveryPhase.Prepared&&phase!=RebirthPurgeSupplyDeliveryPhase.Started&&phase!=RebirthPurgeSupplyDeliveryPhase.CrateStarted||version<1||version>2)
            throw new ArgumentException("Invalid original supply destination.");
        X=x;Y=y;Z=z;Phase=phase;Version=version;
    }
    internal RebirthPurgeSupplyDeliveryPlan Started()=>new RebirthPurgeSupplyDeliveryPlan(X,Y,Z,RebirthPurgeSupplyDeliveryPhase.Started,Version);
    internal RebirthPurgeSupplyDeliveryPlan CrateStarted()=>new RebirthPurgeSupplyDeliveryPlan(X,Y,Z,RebirthPurgeSupplyDeliveryPhase.CrateStarted,Version);
    internal bool IsSuccessorOf(RebirthPurgeSupplyDeliveryPlan old)=>
        old!=null&&Version==old.Version&&X==old.X&&Y==old.Y&&Z==old.Z&&(int)Phase>=(int)old.Phase;
    internal XElement Write()=>new XElement("delivery",new XAttribute("version",Version),new XAttribute("phase",(int)Phase),
        new XAttribute("x",X),new XAttribute("y",Y),new XAttribute("z",Z));
    internal static RebirthPurgeSupplyDeliveryPlan Read(XElement node)
    {
        if(node==null)return null;
        RebirthPoiClearanceCodec.Shape(node,"delivery","version,phase,x,y,z","");
        int version=RebirthPoiClearanceCodec.Int(node,"version");if(version<1||version>2)throw new FormatException();
        return new RebirthPurgeSupplyDeliveryPlan(RebirthPoiClearanceCodec.Int(node,"x"),RebirthPoiClearanceCodec.Int(node,"y"),
            RebirthPoiClearanceCodec.Int(node,"z"),(RebirthPurgeSupplyDeliveryPhase)RebirthPoiClearanceCodec.Int(node,"phase"),version);
    }
}