using System;

// Deterministic adapters for the real production lease/seat algorithms.
// This does not certify Unity rendering, packet delivery, native firing or multiplayer gameplay.
namespace UnityEngine { internal static class Time { internal static float unscaledTime; } }
internal sealed class World { internal bool Remote; internal bool IsRemote()=>Remote; }
internal sealed class EntityVehicle { internal World world=new World(); }
internal class EntityPlayer { internal int entityId; internal bool Allowed=true; }
internal sealed class EntityPlayerLocal:EntityPlayer { }
internal sealed class ClientInfo { internal bool Connected=true; }
internal sealed class Clients
{
    internal readonly Dictionary<int,ClientInfo> Entries=new();
    internal ClientInfo ForEntityId(int id)=>Entries.GetValueOrDefault(id);
}
internal sealed class ConnectionManager { internal Clients Clients=new(); }
internal static class SingletonMonoBehaviour<T> where T:new() { internal static T Instance=new T(); }
internal static class RebirthCamperTurretAccess
{
    internal static bool CanUse(EntityVehicle v,EntityPlayer p)=>p!=null&&p.Allowed;
    internal static bool Authenticated(World w,ClientInfo c,EntityPlayer p)=>c?.Connected==true;
}
internal sealed partial class RebirthCamperTurretRuntime
{
    private readonly EntityVehicle vehicle=new();
    private sealed class Slot { internal bool HasTurret=true; }
    private sealed class State { internal Slot[] Slots=Enumerable.Range(0,4).Select(_=>new Slot()).ToArray(); }
    private sealed class Gun { internal float YawAngle=12,PitchAngle=-4; }
    private readonly State state=new();
    private State Observe()=>state;
    private Gun Bind(int corner)=>new();
    private void ClampAim(int corner,ref float yaw,ref float pitch){yaw=Math.Clamp(yaw,-90,90);pitch=Math.Clamp(pitch,-75,29.5f);}
    private static int checks;
    private static void Check(bool result,string label){if(!result)throw new Exception("FAIL "+label);checks++;Console.WriteLine("PASS "+label);}
    public static void Main()
    {
        var r=new RebirthCamperTurretRuntime();
        var players=Enumerable.Range(1,4).Select(id=>new EntityPlayerLocal{entityId=id}).ToArray();
        var tokens=Enumerable.Range(0,4).Select(_=>Guid.NewGuid()).ToArray();
        for(int i=0;i<4;i++)Check(r.AcquireControl(players[i],i,tokens[i]),"independent corner claim "+i);
        Check(!r.AcquireControl(players[1],0,Guid.NewGuid()),"occupied corner denies second player");
        Check(!r.ManualInput(players[1],0,tokens[0],0,0,true),"other player cannot use owner's token");
        Check(!r.ManualInput(players[0],0,Guid.NewGuid(),0,0,true),"stale token rejected");
        Check(r.ManualInput(players[0],0,tokens[0],999,999,true),"owner input accepted");
        Check(r.controls[0].Yaw==90&&r.controls[0].Pitch==29.5f,"input clamped to rig limits");
        Check(!r.ManualInput(players[0],0,tokens[0],float.NaN,0,true),"non-finite input rejected");
        r.ReleaseControl(players[1],tokens[0]);Check(r.ControlOwner(0)==1,"other player cannot release lease");
        r.ReleaseControl(players[0],Guid.NewGuid());Check(r.ControlOwner(0)==1,"stale release cannot free current lease");
        UnityEngine.Time.unscaledTime=.26f;
        Check(r.controls[0].InputUntil<UnityEngine.Time.unscaledTime&&r.ControlOwner(0)==1,"fire freshness expires before reservation");
        Check(r.ManualInput(players[0],0,tokens[0],0,0,false)&&r.ManualInput(players[0],0,tokens[0],0,0,true),"new live press before lease release accepted");
        r.ReleaseControl(players[0],tokens[0]);Check(r.ControlOwner(0)==-1&&r.ControlOwner(1)==2,"release affects only selected corner");
        var replacement=Guid.NewGuid();Check(r.AcquireControl(players[0],0,replacement),"new token reacquires released corner");
        Check(!r.controls[0].Press.Take(UnityEngine.Time.unscaledTime),"revoked lease cannot transfer pending press into replacement session");
        Check(r.ManualInput(players[0],0,replacement,0,0,true),"replacement receives authorized press");
        UnityEngine.Time.unscaledTime=.315f;Check(r.ManualInput(players[0],0,replacement,0,0,false),"authorized quick release arrives before runtime tick");
        Check(r.controls[0].Press.Take(.36f)&&!r.controls[0].Fire&&!r.controls[0].Press.Take(.4f),"actual production ManualInput retains released click for exactly one tick");
        r.ReleaseControl(players[0],tokens[0]);Check(r.ControlOwner(0)==1,"delayed old release preserves replacement");
        r.ReleaseControl(players[1],tokens[1]);Check(r.AcquireControl(players[0],1,Guid.NewGuid())&&r.ControlOwner(0)==-1,"switching releases player's previous corner");
        players[0].Allowed=false;Check(r.ControlOwner(1)==-1,"lost access releases lease");
        r.state.Slots[2].HasTurret=false;Check(r.ControlOwner(2)==-1,"uninstalled turret releases lease");
        UnityEngine.Time.unscaledTime=4;Check(r.ControlOwner(3)==-1,"abandoned reservation expires");
        var remote=new EntityPlayer{entityId=9};var peer=new ClientInfo();SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Entries.Add(9,peer);
        Check(r.AcquireControl(remote,0,Guid.NewGuid()),"authenticated remote claim");peer.Connected=false;
        Check(r.ControlOwner(0)==-1,"disconnect releases reservation");
        Check(!r.AcquireControl(players[0],-1,Guid.NewGuid())&&!r.AcquireControl(players[0],4,Guid.NewGuid()),"invalid corners refused");
        Check(RebirthVehicleSeatCycle.Next(4,0,i=>i==1,true)==2,"seat cycle skips occupied next seat");
        Check(RebirthVehicleSeatCycle.Next(4,3,i=>false,true)==0,"seat cycle wraps to free driver seat");
        Check(RebirthVehicleSeatCycle.Next(4,3,i=>false,false)==1,"seat cycle respects driver permission");
        Check(RebirthVehicleSeatCycle.Next(4,1,i=>true,true)==-1,"full vehicle retains current seat");
        Check(RebirthVehicleSeatCycle.Next(1,0,i=>false,true)==-1,"single-seat vehicle stays seated");
        Console.WriteLine("PASS "+checks+" production control/seat assertions. Native game integration NOT TESTED.");
    }
}
